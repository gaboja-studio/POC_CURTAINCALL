using System;
using System.Collections.Generic;
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
        int pendingSegment = -1;
        float pendingIgniteAt = -1f, furthestDistance;
        float elapsed;
        double sampledAt;
        bool ticking;
        float nextBroadcast;
        readonly List<Vector2> segmentSpans = new(FireCourseSpans.MaximumPieces);

        public static TightropeFire Current => current != null ? current : FindAnyObjectByType<TightropeFire>();
        /// <summary>이번 회차의 고정 설정. 결과 기록용 JSON은 ExportSettings를 쓴다.</summary>
        public FireSettings Settings => snapshot;
        public int Round => round;
        public float Elapsed => ticking ? elapsed + (float)Math.Max(0d, Clock - sampledAt) : elapsed;
        public bool IsPerformanceActive => snapshot != null && run != null && course != null && round == run.Round
            && run.IsPerformanceStarted && run.State == TightropeRunState.Running && course.IsGameplayActive;
        public int PendingSegment => pendingSegment;
        public float PendingIgniteAt => pendingIgniteAt;
        public float FurthestDistance => furthestDistance;
        public bool IsChasing => IsPerformanceActive && snapshot.chaseEnabled && Elapsed >= snapshot.chaseDelay;
        public float ChaseDistance => snapshot == null ? float.NaN : FireMath.ChaseDistance(snapshot, Elapsed);
        public string ExportSettings() => snapshot?.ToJson() ?? "";

        /// <summary>이 거리에서 소각 뒤 정확히 하나만 남도록 예정된 줄(0-based). 아직 타기 전에도 같은 줄을 반환한다.</summary>
        public int SurvivingLaneAt(float distance)
        {
            if (snapshot == null || course == null || run == null || round != run.Round) return TightropeCourse.NoLane;
            int survivor = TightropeCourse.NoLane;
            for (int lane = 0; lane < course.LaneCount; lane++)
            {
                bool burns = false;
                for (int i = 0; i < snapshot.segments.Length && !burns; i++)
                {
                    if (snapshot.segments[i].lane - 1 != lane) continue;
                    GetSegmentSpans(i, segmentSpans);
                    foreach (var span in segmentSpans)
                        if (distance >= span.x && distance < span.y) { burns = true; break; }
                }
                if (burns || !course.IsLaneUsable(lane, distance)) continue;
                if (survivor != TightropeCourse.NoLane) return TightropeCourse.NoLane;
                survivor = lane;
            }
            return survivor;
        }

        public bool FinaleReadyAt(float distance)
        {
            int survivor = SurvivingLaneAt(distance);
            if (survivor == TightropeCourse.NoLane) return false;
            for (int lane = 0; lane < course.LaneCount; lane++)
                if (lane != survivor && course.IsLaneUsable(lane, distance)) return false;
            return true;
        }
        /// <summary>호스트에서만 발생. 사망 상태로 바꾸기 직전에 확정 원인을 알린다.</summary>
        public event Action<FireHit> Hit;
        public event Action Changed;
        public bool IsSegmentActive(int index) => index >= 0 && index < 64 && (activeMask & (1UL << index)) != 0;
        public float WarningRemaining(int index)
        {
            if (!IsPerformanceActive
                || index < 0 || index >= snapshot.segments.Length || IsSegmentActive(index)) return -1f;
            return FireMath.WarningRemaining(index, pendingSegment, pendingIgniteAt, Elapsed);
        }

        /// <summary>원래 이벤트 번호는 유지하고 실제 소각 부분만 반환한다. 구간 끝은 기존처럼 제외한다.</summary>
        public void GetSegmentSpans(int index, List<Vector2> result)
        {
            result.Clear();
            if (snapshot == null || index < 0 || index >= snapshot.segments.Length) return;
            FireCourseSpans.Get(snapshot.segments[index], course, result);
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
            var settings = GameSettings.Tightrope.Fire ?? new FireSettings();
            int seed = settings.laneOrder != FireLaneOrder.Configured ? UnityEngine.Random.Range(0, int.MaxValue) : 0;
            snapshot = settings.Snapshot(course.FinishDistance, course.LaneCount, seed);
            round = run.Round;
            activeMask = appliedMask = 0;
            pendingSegment = -1;
            pendingIgniteAt = -1f;
            furthestDistance = 0f;
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

        void HandleRebuilt()
        {
            // 봉 구간이 바뀌면 이전 파생 소각 범위도 제거하고 현재 보호 범위로 다시 만든다.
            course.ClearBlockedSegments();
            appliedMask = 0;
            ApplyBlocks();
        }

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
            if (session == null || !session.IsHost || run == null || !run.IsPerformanceStarted
                || run.State != TightropeRunState.Running || !course.IsGameplayActive) return;
            if (snapshot == null || round != run.Round) HandleRound();
            elapsed = run.PerformanceElapsed;
            sampledAt = Clock;
            ticking = true;
            foreach (var player in NetworkPlayer.All)
                if (player.isActiveAndEnabled && player.State == PlayerState.Normal && (run == null || run.IsParticipantReady(player))
                    && (!player.TryGetComponent(out PlayerBalance balance) || !balance.HasFallen))
                    furthestDistance = Mathf.Max(furthestDistance, Mathf.Clamp(course.GetDistance(player.transform.position), 0f, course.FinishDistance));
            bool changed = FireMath.AdvanceSequence(snapshot, elapsed, furthestDistance, course.LaneCount,
                ref activeMask, ref pendingSegment, ref pendingIgniteAt);
            ApplyBlocks();
            if (changed || Time.unscaledTime >= nextBroadcast)
            {
                nextBroadcast = Time.unscaledTime + 1f;
                Broadcast();
                if (changed) Changed?.Invoke();
            }
            foreach (var player in NetworkPlayer.All)
            {
                if (run.State != TightropeRunState.Running || !course.IsGameplayActive) break;
                if ((player.State != PlayerState.Normal || (run != null && !run.IsParticipantReady(player)))) continue;
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
                    GetSegmentSpans(i, segmentSpans);
                    bool hit = false;
                    foreach (var span in segmentSpans)
                    {
                        segment.from = span.x; segment.to = span.y;
                        if (!FireMath.SegmentContains(segment, distance, side, course.Layout.GetLaneSide(lane),
                            course.Shape.RopeWalkWidth * 0.5f + radius)) continue;
                        hit = true;
                        break;
                    }
                    if (!hit) continue;
                    Kill(player, FireKind.Segment, i, distance);
                    break;
                }
            }
        }

        void Kill(NetworkPlayer player, FireKind kind, int segment, float distance)
        {
            if (!player.ServerKill()) return;
            Hit?.Invoke(new FireHit(player, kind, segment, round, elapsed, distance));
            Debug.Log($"[Fire] 회차 {round} · 슬롯 {player.Slot} · {kind}({segment}) · {elapsed:0.00}s · {distance:0.00}m", this);
        }

        void ApplyBlocks()
        {
            if (snapshot == null || run == null || course == null || round != run.Round || !course.IsGameplayActive) return;
            for (int i = 0; i < snapshot.segments.Length; i++)
            {
                ulong bit = 1UL << i;
                if ((activeMask & bit) == 0 || (appliedMask & bit) != 0) continue;
                var segment = snapshot.segments[i];
                GetSegmentSpans(i, segmentSpans);
                foreach (var span in segmentSpans) course.BlockSegment(segment.lane - 1, span.x, span.y, false);
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
                pendingSegment = -1;
                pendingIgniteAt = -1f;
                furthestDistance = 0f;
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
            writer.WriteValueSafe(pendingSegment);
            writer.WriteValueSafe(pendingIgniteAt);
            writer.WriteValueSafe(furthestDistance);
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
            reader.ReadValueSafe(out int incomingPending);
            reader.ReadValueSafe(out float incomingIgniteAt);
            reader.ReadValueSafe(out float incomingFurthest);
            reader.ReadValueSafe(out string json);
            var settings = FireSettings.FromJson(json);
            if (settings == null || settings.segments == null || settings.segments.Length > 64) return;
            if (incomingPending < -1 || incomingPending >= settings.segments.Length
                || float.IsNaN(incomingIgniteAt) || float.IsInfinity(incomingIgniteAt)
                || float.IsNaN(incomingFurthest) || float.IsInfinity(incomingFurthest)) return;
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
            pendingSegment = incomingPending;
            pendingIgniteAt = incomingIgniteAt;
            furthestDistance = incomingFurthest;
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
