using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    public enum FireKind : byte { Chase, Segment }

    /// <summary>호스트가 확정한 화재 사망. 구간 번호는 0부터, 추격 불은 -1.</summary>
    public readonly struct FireHit
    {
        public readonly NetworkPlayer Player;
        public readonly FireKind Kind;
        public readonly int Segment, Round;
        public readonly float Elapsed, Distance;

        public FireHit(NetworkPlayer player, FireKind kind, int segment, int round, float elapsed, float distance)
        {
            Player = player;
            Kind = kind;
            Segment = segment;
            Round = round;
            Elapsed = elapsed;
            Distance = distance;
        }
    }

    /// <summary>화재 공개 진입점. 판정은 Run의 도착 확인 뒤 호스트만, 표시·차단은 호스트 스냅샷으로 모든 화면에서 적용.</summary>
    public sealed class TightropeFire : MonoBehaviour
    {
        const string StateMessage = "CurtainCall.Tightrope.Fire.State";
        const string RequestMessage = "CurtainCall.Tightrope.Fire.Request";
        const int MaxMessageBytes = 32768;
        static TightropeFire current;
        TightropeCourse course;
        TightropeRun run;
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        FireSettings snapshot;
        ulong activeMask, appliedMask;
        int round = -1, sequence, receivedSequence = -1;
        float elapsed;
        double sampledAt;
        bool ticking;
        float nextBroadcast;

        public static TightropeFire Current => current != null ? current : FindAnyObjectByType<TightropeFire>();
        /// <summary>이번 회차의 고정 설정. 결과 기록용 JSON은 ExportSettings를 쓴다.</summary>
        public FireSettings Settings => snapshot;
        public int Round => round;
        public float Elapsed => ticking ? elapsed + (float)Math.Max(0d, Clock - sampledAt) : elapsed;
        public bool IsChasing => snapshot != null && run != null && round == run.Round
            && run.IsPerformanceStarted && snapshot.chaseEnabled && Elapsed >= snapshot.chaseDelay;
        public float ChaseDistance => snapshot == null ? float.NaN : FireMath.ChaseDistance(snapshot, Elapsed);
        public string ExportSettings() => snapshot?.ToJson() ?? "";
        /// <summary>호스트에서만 발생. 사망 상태로 바꾸기 직전에 확정 원인을 알린다.</summary>
        public event Action<FireHit> Hit;
        public event Action Changed;
        public bool IsSegmentActive(int index) => index >= 0 && index < 64 && (activeMask & (1UL << index)) != 0;
        public float WarningRemaining(int index)
        {
            if (snapshot == null || run == null || round != run.Round || !run.IsPerformanceStarted
                || index < 0 || index >= snapshot.segments.Length || IsSegmentActive(index)) return -1f;
            float remaining = snapshot.segments[index].igniteAt - Elapsed;
            return remaining >= 0f && remaining <= snapshot.warningSeconds ? remaining : -1f;
        }

        double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        void OnEnable()
        {
            current = this;
            BindRun();
        }

        void OnDisable()
        {
            if (current == this) current = null;
            Unregister();
            if (run != null)
            {
                run.ServerHazardCheck -= Judge;
                run.RoundChanged -= HandleRound;
                run.RunRestarted -= HandleRestart;
                run.StateChanged -= HandleRunState;
                run.PerformanceStarted -= HandlePerformance;
                run = null;
            }
            if (course != null) course.Rebuilt -= HandleRebuilt;
            if (session != null)
            {
                session.ConnectionStateChanged -= HandleConnection;
                session = null;
            }
        }

        void BindRun()
        {
            if (run != null || TightropeRun.Current == null || TightropeCourse.Current == null) return;
            run = TightropeRun.Current;
            course = TightropeCourse.Current;
            run.ServerHazardCheck += Judge;
            run.RoundChanged += HandleRound;
            run.RunRestarted += HandleRestart;
            run.StateChanged += HandleRunState;
            run.PerformanceStarted += HandlePerformance;
            course.Rebuilt += HandleRebuilt;
        }

        void Update()
        {
            BindRun();
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += HandleConnection;
                HandleConnection(session.ConnectionState);
            }
            if (run == null || session == null) return;
            if (session.IsHost && (snapshot == null || round != run.Round)) HandleRound();
            ApplyBlocks();
        }

        void HandleRound()
        {
            if (run == null || course == null) return;
            course.ClearBlockedSegments();
            appliedMask = 0;
            if (session == null || !session.IsHost) { ApplyBlocks(); return; }
            snapshot = (GameSettings.Tightrope.Fire ?? new FireSettings()).Snapshot(course.FinishDistance);
            round = run.Round;
            activeMask = appliedMask = 0;
            elapsed = 0f;
            sampledAt = Clock;
            ticking = false;
            nextBroadcast = 0f;
            Broadcast();
            Changed?.Invoke();
        }

        void HandleRestart()
        {
            // 플레이어 복구가 상태 메시지보다 늦어도 현재 회차의 확정 구간만 다시 적용한다.
            appliedMask = 0;
            ApplyBlocks();
        }

        void HandleRebuilt() { appliedMask = 0; ApplyBlocks(); }

        void HandlePerformance()
        {
            if (session == null || !session.IsHost) return;
            elapsed = run.PerformanceElapsed;
            sampledAt = Clock;
            ticking = true;
            Broadcast();
        }

        void HandleRunState(TightropeRunState previous, TightropeRunState next)
        {
            if (session == null || !session.IsHost) return;
            elapsed = run.PerformanceElapsed;
            sampledAt = Clock;
            ticking = next == TightropeRunState.Running && run.IsPerformanceStarted;
            Broadcast();
        }

        void Judge()
        {
            if (session == null || !session.IsHost || run == null || !run.IsPerformanceStarted) return;
            if (snapshot == null || round != run.Round) HandleRound();
            elapsed = run.PerformanceElapsed;
            sampledAt = Clock;
            ticking = true;
            ulong before = activeMask;
            for (int i = 0; i < snapshot.segments.Length; i++)
                if (elapsed >= snapshot.segments[i].igniteAt) activeMask |= 1UL << i;
            ApplyBlocks();
            if (before != activeMask || Time.unscaledTime >= nextBroadcast)
            {
                nextBroadcast = Time.unscaledTime + 1f;
                Broadcast();
                if (before != activeMask) Changed?.Invoke();
            }
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal) continue;
                float distance = course.GetDistance(player.transform.position);
                if (distance >= snapshot.safeDistance) continue;
                if (IsChasing && distance <= ChaseDistance) { Kill(player, FireKind.Chase, -1, distance); continue; }
                float side = course.GetSide(player.transform.position);
                float radius = player.TryGetComponent(out PlayerMover mover) ? mover.BodyRadius : 0f;
                for (int i = 0; i < snapshot.segments.Length; i++)
                {
                    var segment = snapshot.segments[i];
                    int lane = segment.lane - 1;
                    if (!IsSegmentActive(i) || lane < 0 || lane >= course.LaneCount) continue;
                    if (!FireMath.SegmentContains(segment, distance, side, course.Layout.GetLaneSide(lane),
                        course.Shape.RopeWalkWidth * 0.5f + radius)) continue;
                    Kill(player, FireKind.Segment, i, distance);
                    break;
                }
            }
        }

        void Kill(NetworkPlayer player, FireKind kind, int segment, float distance)
        {
            Hit?.Invoke(new FireHit(player, kind, segment, round, elapsed, distance));
            player.ServerSetState(PlayerState.Fallen);
            Debug.Log($"[Fire] 회차 {round} · 슬롯 {player.Slot} · {kind}({segment}) · {elapsed:0.00}s · {distance:0.00}m", this);
        }

        void ApplyBlocks()
        {
            if (snapshot == null || run == null || round != run.Round) return;
            for (int i = 0; i < snapshot.segments.Length; i++)
            {
                ulong bit = 1UL << i;
                if ((activeMask & bit) == 0 || (appliedMask & bit) != 0) continue;
                var segment = snapshot.segments[i];
                course.BlockSegment(segment.lane - 1, segment.from, segment.to, false);
                appliedMask |= bit;
            }
        }

        void HandleConnection(SessionConnectionState state)
        {
            Unregister();
            messaging = session.NetworkManager != null ? session.NetworkManager.CustomMessagingManager : null;
            if (state == SessionConnectionState.Host)
            {
                messaging?.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
                HandleRound();
            }
            else if (state == SessionConnectionState.Client)
            {
                round = -1;
                receivedSequence = -1;
                messaging?.RegisterNamedMessageHandler(StateMessage, HandleState);
                if (messaging == null) return;
                using var writer = new FastBufferWriter(1, Allocator.Temp);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer);
            }
            else if (state == SessionConnectionState.Offline)
            {
                snapshot = null;
                activeMask = appliedMask = 0;
                ticking = false;
                round = -1;
                Changed?.Invoke();
            }
        }

        void Broadcast()
        {
            if (messaging == null || snapshot == null || !session.IsHost) return;
            using var writer = WriteState();
            messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        FastBufferWriter WriteState()
        {
            var writer = new FastBufferWriter(1024, Allocator.Temp, MaxMessageBytes);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(++sequence);
            writer.WriteValueSafe(activeMask);
            writer.WriteValueSafe(Elapsed);
            writer.WriteValueSafe(Clock);
            writer.WriteValueSafe(ticking);
            writer.WriteValueSafe(snapshot.ToJson());
            return writer;
        }

        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            if (messaging == null || snapshot == null || !session.IsHost) return;
            using var writer = WriteState();
            messaging.SendNamedMessage(StateMessage, sender, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        void HandleState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || session.IsHost) return;
            reader.ReadValueSafe(out int incomingRound);
            reader.ReadValueSafe(out int incomingSequence);
            if (incomingRound < round || (run != null && incomingRound < run.Round)
                || (incomingRound == round && incomingSequence <= receivedSequence)) return;
            reader.ReadValueSafe(out ulong mask);
            reader.ReadValueSafe(out float incomingElapsed);
            reader.ReadValueSafe(out double at);
            reader.ReadValueSafe(out bool running);
            reader.ReadValueSafe(out string json);
            var settings = FireSettings.FromJson(json);
            if (settings == null || settings.segments == null || settings.segments.Length > 64) return;
            bool newRound = incomingRound != round;
            if (newRound)
            {
                appliedMask = 0;
                if (run != null && incomingRound == run.Round) course.ClearBlockedSegments();
            }
            round = incomingRound;
            receivedSequence = incomingSequence;
            if (newRound || snapshot == null) snapshot = settings;
            activeMask = mask;
            elapsed = incomingElapsed;
            sampledAt = at;
            ticking = running;
            ApplyBlocks();
            Changed?.Invoke();
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging = null;
        }
    }
}
