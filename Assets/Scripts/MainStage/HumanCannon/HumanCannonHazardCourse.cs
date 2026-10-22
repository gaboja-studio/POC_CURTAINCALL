using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    [DefaultExecutionOrder(-15)]
    public sealed partial class HumanCannonHazardCourse : MonoBehaviour
    {
        [SerializeField] HumanCannonHazard[] hazards = Array.Empty<HumanCannonHazard>();
        [SerializeField] HumanCannonWindZone[] windZones = Array.Empty<HumanCannonWindZone>();
        [SerializeField, Range(1, 3)] int difficulty = 1;
        NetworkSessionManager session;
        HumanCannonSystem cannon;
        readonly HashSet<(int, ulong)> touching = new(), touchingNow = new();
        readonly Dictionary<ulong, Vector3> previousFeet = new();
        readonly Dictionary<ulong, Vector3> restartPositions = new();
        readonly HashSet<ulong> cutThisFrame = new();
        readonly List<ulong> stale = new();
        double epoch, previousElapsed = -1, runStart = -1, runEnd = -1, nextSnapshot;
        bool synchronized;
        uint revision;
        public static HumanCannonHazardCourse Current { get; private set; }
        public int Difficulty => difficulty;
        /// <summary>판정 화면의 실제 절단 결과(플레이어·장애물·잘린 부위·검출 위치).</summary>
        public event Action<NetworkPlayer, HumanCannonHazard, BodyPart, Vector3> HitConfirmed;
        /// <summary>절단 판정을 호출하기 직전의 실제 접촉. 피해 성공과는 구분한다.</summary>
        public event Action<NetworkPlayer, HumanCannonHazard, Vector3> ContactDetected;
        /// <summary>새 접촉의 최종 관측. 부상 없는 접촉과 같은 프레임의 판정 제한도 구분한다.</summary>
        public event Action<NetworkPlayer, HumanCannonHazard, BodyPart, Vector3, bool> ContactResolved;

        public int SawIndex(HumanCannonHazard target)
        {
            int index = 0;
            foreach (var hazard in hazards)
            {
                if (hazard == target) return index;
                if (hazard != null && !hazard.IsPoleSaw && hazard.Kind == target.Kind) index++;
            }
            return index;
        }

        public bool IsHazardEnabled(int index)
        {
            if (index < 0 || index >= hazards.Length || hazards[index] == null) return false;
            var hazard = hazards[index];
            if (hazard.IsPoleSaw || hazard.Kind == CannonHazardKind.SpikeMace) return true;
            int count = 0;
            foreach (var other in hazards)
                if (other != null && !other.IsPoleSaw && other.Kind == hazard.Kind) count++;
            return TrickSettings.IncludesSaw(SawIndex(hazard), count, GameSettings.HumanCannon.SawDensity);
        }
        public int HazardCount => hazards.Length;
        public HumanCannonHazard Hazard(int index) => hazards[index];
        public bool IsSynchronized => synchronized && !resetBarrier.Waiting;
        public double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public double Elapsed => IsSynchronized ? Math.Max(0, Clock - epoch) : 0;
        public double RunSeconds => runStart < 0 ? 0 : Math.Max(0, (runEnd >= 0 ? runEnd : Clock) - runStart);
        public bool CanChangeDifficulty => session != null && session.IsHost && runStart < 0
            && (HumanCannonDestination.Current == null || !HumanCannonDestination.Current.PerformanceOpen);
        bool Resolved => HumanCannonDestination.Current != null && HumanCannonDestination.Current.IsResolved;

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            NetworkPlayer.Restarted += Restart;
        }

        void Update()
        {
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += Connection;
                Connection(session.ConnectionState);
            }
            if (cannon != HumanCannonSystem.Current)
            {
                if (cannon != null) cannon.StateChanged -= OnCannonAction;
                cannon = HumanCannonSystem.Current;
                if (cannon != null) cannon.StateChanged += OnCannonAction;
            }
            if (session == null || !session.IsHost) return;
            if (runStart >= 0 && runEnd < 0 && Resolved) { runEnd = Clock; revision++; Publish(); }
            if (Clock >= nextSnapshot) { nextSnapshot = Clock + 0.5; Publish(); }
        }

        void OnCannonAction(int station, CannonAction action, ulong actor)
        {
            if (session == null || !session.IsHost || action != CannonAction.Launch || runStart >= 0) return;
            runStart = Clock; revision++; Publish();
        }

        public bool SetDifficulty(int level)
        {
            if (!CanChangeDifficulty || !CannonDifficulty.Valid(level)) return false;
            if (difficulty == level) return true;
            difficulty = level; epoch = Clock; ClearContacts(); revision++; Publish();
            return true;
        }

        public Vector3 WindVelocity(Vector3 feet, float height, bool airborne)
        {
            if (!IsSynchronized || Resolved) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var zone in windZones)
                if (zone != null && zone.Contains(feet, height, airborne)) sum += zone.Direction;
            // 겹친 구역에서도 승인한 최대 세기를 넘지 않아 외팔의 역방향 이동 여지를 유지한다.
            return Vector3.ClampMagnitude(sum, 1f) * CannonDifficulty.WindSpeed(difficulty);
        }

        void LateUpdate()
        {
            if (!IsSynchronized) return;
            double elapsed = Elapsed;
            float speed = CannonDifficulty.HazardSpeed(difficulty);
            for (int i = 0; i < hazards.Length; i++)
            {
                var hazard = hazards[i];
                if (hazard == null) continue;
                bool enabled = IsHazardEnabled(i);
                hazard.SetVisible(enabled, SawIndex(hazard));
                if (enabled) hazard.Present(elapsed, hazard.IsPoleSaw ? 1f : speed);
            }
            if (session != null && session.IsHost && !Resolved) HostContacts(elapsed, speed);
            previousElapsed = elapsed;
        }

        void HostContacts(double elapsed, float speed)
        {
            touchingNow.Clear(); cutThisFrame.Clear();
            bool forwardTime = previousElapsed >= 0 && elapsed > previousElapsed;
            // 긴 프레임에도 최근 0.5초는 검사한다. 초기화/시간 역행 때는 현재 자세만 검사한다.
            double beforeTime = forwardTime ? Math.Max(previousElapsed, elapsed - 0.5) : elapsed;
            float startRatio = forwardTime ? (float)((beforeTime - previousElapsed) / (elapsed - previousElapsed)) : 1f;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal
                    || !player.TryGetComponent(out PlayerMover mover)) continue;
                Vector3 feet = player.transform.position;
                // 원격 소유자의 재시작 순간이동이 도착하기 전 옛 함정 위치로 다시 절단하지 않는다.
                if (restartPositions.TryGetValue(player.OwnerClientId, out var spawn))
                {
                    if ((feet - spawn).sqrMagnitude > 9f) continue;
                    restartPositions.Remove(player.OwnerClientId);
                    previousFeet.Remove(player.OwnerClientId);
                }
                // 시간창을 자른 비율만큼 몸의 경로도 잘라 톱 시각과 같은 시점에서 시작한다.
                // 4m 이상 순간이동은 기존처럼 연결하지 않는다.
                Vector3 before = previousFeet.TryGetValue(player.OwnerClientId, out var old) && (old - feet).sqrMagnitude < 16f
                    ? Vector3.Lerp(old, feet, startRatio) : feet;
                for (int h = 0; h < hazards.Length; h++)
                {
                    var hazard = hazards[h];
                    if (hazard == null || !hazard.isActiveAndEnabled || !IsHazardEnabled(h)) continue;
                    float hazardSpeed = hazard.IsPoleSaw ? 1f : speed;
                    int steps = hazard.SweepSteps(before, feet, elapsed - beforeTime, hazardSpeed);
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = i / (float)steps;
                        if (!hazard.Touches(Vector3.Lerp(before, feet, t), mover.BodyHeight, mover.BodyRadius,
                            beforeTime + (elapsed - beforeTime) * t, hazardSpeed)) continue;
                        var key = (h, player.OwnerClientId);
                        touchingNow.Add(key);
                        if (!touching.Contains(key))
                        {
                            bool evaluated = cutThisFrame.Add(player.OwnerClientId);
                            BodyPart cut = BodyPart.None;
                            if (evaluated)
                            {
                              if (ContactDetected != null)
                                foreach (Action<NetworkPlayer, HumanCannonHazard, Vector3> observer in ContactDetected.GetInvocationList())
                                    try { observer(player, hazard, Vector3.Lerp(before, feet, t)); }
                                    catch (Exception error) { Debug.LogException(error, this); }
                              cut = player.ServerCut();
                              if (cut != BodyPart.None && HitConfirmed != null)
                                foreach (Action<NetworkPlayer, HumanCannonHazard, BodyPart, Vector3> observer in HitConfirmed.GetInvocationList())
                                    try { observer(player, hazard, cut, Vector3.Lerp(before, feet, t)); }
                                    catch (Exception error) { Debug.LogException(error, this); }
                            }
                            if (ContactResolved != null)
                                foreach (Action<NetworkPlayer, HumanCannonHazard, BodyPart, Vector3, bool> observer in ContactResolved.GetInvocationList())
                                    try { observer(player, hazard, cut, Vector3.Lerp(before, feet, t), evaluated); }
                                    catch (Exception error) { Debug.LogException(error, this); }
                        }
                        break;
                    }
                }
                previousFeet[player.OwnerClientId] = feet;
            }
            touching.Clear(); touching.UnionWith(touchingNow);
            stale.Clear();
            foreach (ulong actor in previousFeet.Keys)
            {
                bool alive = false;
                foreach (var player in NetworkPlayer.All)
                    if (player != null && player.IsSpawned && player.OwnerClientId == actor && player.State == PlayerState.Normal) { alive = true; break; }
                if (!alive) stale.Add(actor);
            }
            foreach (ulong actor in stale) previousFeet.Remove(actor);
        }

        void ClearContacts() { previousElapsed = -1; touching.Clear(); touchingNow.Clear(); previousFeet.Clear(); }

        void OnDisable()
        {
            NetworkPlayer.Restarted -= Restart;
            if (cannon != null) cannon.StateChanged -= OnCannonAction;
            if (session != null) session.ConnectionStateChanged -= Connection;
            Unregister(); ClearContacts(); restartPositions.Clear(); resetBarrier.Clear(); synchronized = false;
            cannon = null; session = null;
            if (Current == this) Current = null;
        }
    }
}
