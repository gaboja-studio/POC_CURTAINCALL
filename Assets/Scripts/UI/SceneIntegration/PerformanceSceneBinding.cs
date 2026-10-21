using System;
using System.Collections.Generic;
using CurtainCall.CheerGauge;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Fire;
using CurtainCall.UI.PerformanceHudV1;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>기존 게임 상태의 읽기 전용 표시. 네트워크·점수·입력 소유권을 변경하지 않는다.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class PerformanceSceneBinding : MonoBehaviour
    {
        PerformanceHUDView hud;
        C1CheerGaugeView cheer;
        [SerializeField] TightropeBalanceView localBalance;
        [SerializeField] bool sharedScene = true;
        [SerializeField] BalanceGaugeView legacyBalance;
        [SerializeField] bool humanCannon;
        [SerializeField] Transform cannonStart;
        [SerializeField] Transform cannonFinish;
        [SerializeField] HumanCannonFireWall[] cannonFires = Array.Empty<HumanCannonFireWall>();
        readonly C1GaugeProjection projection = new();
        readonly List<RewardSegment> roster = new();
        readonly Dictionary<string, float> lastDistances = new();
        readonly Dictionary<string, bool> previousDead = new();
        readonly List<float> falls = new();
        readonly List<float> distances = new();
        PerformanceRewards rewards;
        string performanceId;
        TightropeRun observedRun;
        int round = -1;
        bool legacyEnabled;
        Canvas legacyCanvas;
        bool legacyCanvasEnabled;
        bool ownsCommon;
        public PerformanceHUDView Hud => hud;
        public C1CheerGaugeView Cheer => cheer;
        public TightropeBalanceView LocalBalance => localBalance;

        public void ConfigureSpace(TightropeBalanceView balance, BalanceGaugeView previous)
        {
            sharedScene = true; localBalance = balance; legacyBalance = previous;
        }

        public void ConfigureCannon(Transform start, Transform finish, HumanCannonFireWall[] fires)
        {
            humanCannon = true; cannonStart = start; cannonFinish = finish;
            cannonFires = fires ?? Array.Empty<HumanCannonFireWall>();
        }

        /// <summary>불의 기존 경계/속도/경과시간을 표시 초로 변환할 뿐 종료 판정을 호출하지 않는다.</summary>
        public static double? FireBoundarySeconds(double start, double boundary, double speed, double delay, double elapsed)
        {
            if (!double.IsFinite(start) || !double.IsFinite(boundary) || !double.IsFinite(speed)
                || !double.IsFinite(delay) || !double.IsFinite(elapsed) || speed <= 0 || boundary < start || delay < 0 || elapsed < 0)
                return null;
            return Math.Max(0, delay + (boundary - start) / speed - elapsed);
        }

        public void Configure(PerformanceHUDView target, C1CheerGaugeView gauge, BalanceGaugeView previous)
        {
            sharedScene = false; hud = target; cheer = gauge; legacyBalance = previous; localBalance = target.Balance;
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (sharedScene) CommonUiSceneLoader.Register(this);
            if (legacyBalance != null)
            {
                legacyEnabled = legacyBalance.enabled;
                legacyCanvas = legacyBalance.GetComponent<Canvas>();
                legacyCanvasEnabled = legacyCanvas != null && legacyCanvas.enabled;
                legacyBalance.enabled = false;
                if (legacyCanvas != null) legacyCanvas.enabled = false;
            }
            BindRewards();
        }

        void OnDisable()
        {
            if (!Application.isPlaying) return;
            bool owned = !sharedScene || CommonUiSceneLoader.CurrentSpace == this;
            if (sharedScene) CommonUiSceneLoader.Unregister(this);
            if (!ReferenceEquals(rewards, null)) { rewards.Changed -= RefreshRewards; rewards.SampleRecords -= SampleRecords; }
            rewards = null;
            ClearHistory();
            if (owned && hud != null) hud.ResetForRuntime();
            if (owned && cheer != null) { cheer.SetUnavailable(); cheer.gameObject.SetActive(false); }
            if (localBalance != null) { localBalance.SetContext(false, false); localBalance.SetUnavailable(); }
            if (sharedScene) { hud = null; cheer = null; }
            if (legacyBalance != null) legacyBalance.enabled = legacyEnabled;
            if (legacyCanvas != null) legacyCanvas.enabled = legacyCanvasEnabled;
        }

        void BindRewards()
        {
            if (!ReferenceEquals(rewards, null)) { rewards.Changed -= RefreshRewards; rewards.SampleRecords -= SampleRecords; }
            rewards = PerformanceRewards.Instance;
            ClearHistory();
            if (rewards != null) { rewards.Changed += RefreshRewards; rewards.SampleRecords += SampleRecords; }
            RefreshRewards();
        }

        void SampleRecords()
        {
            var state = rewards != null ? rewards.State : null;
            if (state?.Performance == null || !state.IsOpen || !rewards.HasAuthority
                || state.Performance.TrickId != (humanCannon ? "HumanCannon" : "Tightrope")) return;
            double? elapsed = null;
            float length;
            Vector3 axis = Vector3.zero;
            var course = TightropeCourse.Current;
            if (humanCannon)
            {
                if (cannonStart == null || cannonFinish == null) return;
                axis = Vector3.ProjectOnPlane(cannonFinish.position - cannonStart.position, Vector3.up);
                length = axis.magnitude; axis = axis.normalized;
                var route = HumanCannonRouteSystem.Current;
                double first = double.PositiveInfinity;
                foreach (var wall in cannonFires)
                    if (wall != null && wall.Armed) first = Math.Min(first, wall.ArmedAt);
                if (route != null && double.IsFinite(first)) elapsed = Math.Max(0, route.Clock - first);
            }
            else
            {
                if (course == null) return;
                length = course.FinishDistance;
                var run = TightropeRun.Current;
                var fire = TightropeFire.Current;
                if (run != null && run.IsPerformanceStarted && fire != null && fire.Round == run.Round)
                    elapsed = fire.Elapsed;
            }
            foreach (var person in state.Segments)
                foreach (var player in NetworkPlayer.All)
                {
                    if (!player.IsSpawned || player.OwnerClientId != person.ClientId) continue;
                    float distance = humanCannon ? Vector3.Dot(player.transform.position - cannonStart.position, axis)
                        : course.GetDistance(player.transform.position);
                    rewards.RecordProgress(state.Performance.Id, person.ParticipantId, elapsed, length, distance);
                    break;
                }
        }

        void ClearHistory()
        {
            if (ownsCommon && CommonPerformanceUI.Current != null) CommonPerformanceUI.Current.Feedback?.Clear();
            projection.Reset(); performanceId = null; roster.Clear();
            previousDead.Clear(); lastDistances.Clear(); falls.Clear();
        }

        void RefreshRewards()
        {
            if (sharedScene && CommonUiSceneLoader.CurrentSpace != this) return;
            if (cheer == null) return;
            var snapshot = rewards != null ? rewards.State : null;
            if (sharedScene && CommonPerformanceUI.Current != null) CommonPerformanceUI.Current.Feedback?.Observe(snapshot);
            if (!projection.TryProject(snapshot, out var frame))
            {
                ClearHistory(); cheer.SetUnavailable(); return;
            }
            if (performanceId != frame.Identity)
            {
                previousDead.Clear(); lastDistances.Clear(); falls.Clear();
                performanceId = frame.Identity;
            }
            roster.Clear();
            foreach (var segment in snapshot.Segments) roster.Add(segment);
            roster.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : StringComparer.Ordinal.Compare(a.ParticipantId, b.ParticipantId));
            for (int i = 0; i < roster.Count; i++)
            {
                var person = roster[i];
                if (previousDead.TryGetValue(person.ParticipantId, out bool dead) && !dead && person.Dead)
                {
                    // 실제 사망 전이만 강조한다. 야유/접속 이탈은 추락으로 추정하지 않는다.
                    cheer.FlashFall(i);
                    if (lastDistances.TryGetValue(person.ParticipantId, out float distance)) falls.Add(distance);
                }
                previousDead[person.ParticipantId] = person.Dead;
            }
            cheer.SetScores(frame.Scores, frame.Capacity, true);
        }

        void LateUpdate()
        {
            if (sharedScene)
            {
                if (CommonUiSceneLoader.CurrentSpace != this)
                {
                    ownsCommon = false;
                    if (localBalance != null) localBalance.SetContext(false, false);
                    return;
                }
                var common = CommonPerformanceUI.Current;
                if (common == null) return;
                if (!ownsCommon || hud != common.Hud)
                {
                    ownsCommon = true;
                    hud = common.Hud; cheer = common.Cheer;
                    BindRewards();
                }
            }
            if (hud == null || cheer == null) return;
            if (!ReferenceEquals(rewards, PerformanceRewards.Instance)) BindRewards();
            var run = TightropeRun.Current;
            var course = TightropeCourse.Current;
            if (run != observedRun || (run != null && run.Round != round))
            {
                observedRun = run; round = run != null ? run.Round : -1;
                ClearHistory(); RefreshRewards();
            }
            bool active = humanCannon
                ? HumanCannonDestination.Current != null && HumanCannonDestination.Current.PerformanceOpen
                : run != null && course != null && run.State == TightropeRunState.Running;
            var local = NetworkPlayer.Local;
            var balance = local != null ? local.GetComponent<PlayerBalance>() : null;
            bool observing = local != null && (local.State == PlayerState.Fallen || local.State == PlayerState.Retired
                || (balance != null && balance.HasFallen));
            hud.SetContext(active, false, observing);
            if (sharedScene && CommonPerformanceUI.Current != null)
                CommonPerformanceUI.Current.Feedback?.SetContext(active && !observing && !InputGate.IsBlocked && rewards?.State?.IsOpen == true);
            if (localBalance != null) localBalance.SetContext(active && !humanCannon && balance != null && balance.IsActive, observing);
            bool wakingCheer = active && !cheer.gameObject.activeSelf;
            cheer.gameObject.SetActive(active);
            // 씬에 비활성으로 저장된 C1은 첫 활성화 때 Awake에서 데이터를 초기화한다.
            if (wakingCheer) RefreshRewards();
            if (!active) { hud.Timer.SetUnavailable(); hud.Progress.SetUnavailable(); if (localBalance != null) localBalance.SetUnavailable(); return; }
            RefreshTimer();
            RefreshProgress(course, local);
            if (localBalance != null && (humanCannon || balance == null)) localBalance.SetUnavailable();
            else if (localBalance != null)
            {
                var reader = local.GetComponent<PlayerInputReader>();
                float posture = reader != null && reader.isActiveAndEnabled ? reader.Current.Posture : 0f;
                localBalance.SetBalance(balance.Value / PlayerBalance.MaxValue,
                    -balance.GreenLimit / PlayerBalance.MaxValue, balance.GreenLimit / PlayerBalance.MaxValue,
                    -balance.WarningLimit / PlayerBalance.MaxValue, balance.WarningLimit / PlayerBalance.MaxValue,
                    balance.RedTimeRatio, "A", "D", posture < -0.5f, posture > 0.5f);
            }
        }

        void RefreshTimer()
        {
            double? remaining = null;
            if (!humanCannon)
            {
                var fire = TightropeFire.Current;
                var settings = fire != null ? fire.Settings : null;
                if (settings != null && settings.chaseEnabled && observedRun != null && fire.Round == observedRun.Round)
                    remaining = FireBoundarySeconds(settings.chaseStart, settings.safeDistance, settings.chaseSpeed,
                        settings.chaseDelay, fire.Elapsed);
            }
            else
            {
                var route = HumanCannonRouteSystem.Current;
                bool completed = false;
                if (route != null)
                    foreach (var wall in cannonFires)
                        if (wall != null && wall.isActiveAndEnabled && wall.Available && wall.IsConfigured && wall.Armed)
                        {
                            if (wall.Finished) { completed = true; continue; }
                            double seconds = Math.Max(0, wall.EndTime - route.Clock);
                            if (!remaining.HasValue || seconds < remaining.Value) remaining = seconds;
                        }
                if (!remaining.HasValue && completed) remaining = 0;
            }
            hud.Timer.SetRemainingSeconds(remaining);
        }

        void RefreshProgress(TightropeCourse course, NetworkPlayer local)
        {
            Vector3 axis = Vector3.zero;
            float length;
            if (humanCannon)
            {
                if (cannonStart == null || cannonFinish == null) { hud.Progress.SetUnavailable(); return; }
                axis = Vector3.ProjectOnPlane(cannonFinish.position - cannonStart.position, Vector3.up);
                length = axis.magnitude;
                if (length <= 0) { hud.Progress.SetUnavailable(); return; }
                axis /= length;
            }
            else length = course.FinishDistance;
            distances.Clear();
            int localIndex = -1;
            for (int i = 0; i < roster.Count; i++)
            {
                var person = roster[i];
                NetworkPlayer player = null;
                foreach (var candidate in NetworkPlayer.All)
                    if (candidate != null && candidate.OwnerClientId == person.ClientId) { player = candidate; break; }
                if (player != null && person.Connected && !person.Dead)
                    lastDistances[person.ParticipantId] = humanCannon
                        ? Vector3.Dot(player.transform.position - cannonStart.position, axis)
                        : course.GetDistance(player.transform.position);
                if (!lastDistances.TryGetValue(person.ParticipantId, out float distance))
                { hud.Progress.SetUnavailable(); return; } // 늦은 접속의 과거 추락 위치를 만들어내지 않는다.
                distances.Add(distance);
                if (local != null && person.Connected && !person.Dead && person.ClientId == local.OwnerClientId) localIndex = i;
            }
            var fire = TightropeFire.Current;
            bool fireValid = !humanCannon && fire != null && fire.IsPerformanceActive;
            float? fireDistance = fireValid && fire.Settings.chaseEnabled ? fire.ChaseDistance : (float?)null;
            float? maximum = fireValid ? fire.FurthestDistance : (float?)null;
            if (humanCannon && HumanCannonRouteSystem.Current != null)
            {
                // 같은 코스 축으로 투영한 실제 활성 불벽의 최전방. 경고 중에는 출발 위치다.
                double clock = HumanCannonRouteSystem.Current.Clock;
                foreach (var wall in cannonFires)
                    if (wall != null && wall.isActiveAndEnabled && wall.Available && wall.IsConfigured && wall.Armed && !wall.Finished)
                    {
                        float front = Vector3.Dot(wall.Front(clock) - cannonStart.position, axis);
                        if (!fireDistance.HasValue || front > fireDistance.Value) fireDistance = front;
                    }
            }
            var solo = hud.Progress.GetComponent<SoloPerformanceProgress>();
            if (distances.Count == 1 && solo != null)
                solo.Show(length, distances[0], localIndex == 0, fireDistance, maximum, falls.Count > 0 ? falls[falls.Count - 1] : (float?)null);
            else
            {
                if (solo != null) solo.Clear();
                hud.Progress.SetProgress(length, distances, localIndex, fireDistance, maximum, falls, false);
            }
            solo?.RemoveRemainingPrefix();
        }
    }
}
