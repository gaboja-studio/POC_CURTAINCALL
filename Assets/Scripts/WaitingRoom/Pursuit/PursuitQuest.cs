using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.UI.Wayfinding;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 추격 시간 동안 화면 오른쪽에 띄우는 퀘스트(Task-103, 2026-11-04 사용자).
    /// - 제목: 추격자를 피해 달아나기. 본문: 공연장 입구 집결 줄 + (이번 런에서 누구든 탈출로 입구를 방문했으면) 탈출로 진입 줄.
    /// - 탈출로 입구 방문은 판정 화면이 <see cref="ExplorationState"/>에 런 범위로 기록해 모두에게 같게 보인다(세대 교체 때 비움).
    /// - 화면의 제목과 본문은 공통 설정의 쉬는 시간·정비 표시 항목을 사용한다(<see cref="PursuitTexts"/>).
    /// </summary>
    public sealed class PursuitQuest : MonoBehaviour
    {
        /// <summary>탈출로 입구 구역 콜라이더 이름(Frame8Areas 아래, 탐색 타이머 구역과 같은 것).</summary>
        const string EscapeEntranceArea = "Area_B1_EscapeEntrance";
        const float FindInterval = 1f, CheckInterval = .2f;
        const float GuideDistance = 12f;

        static PursuitQuest instance;
        WaitingRoomRightPanel panel;
        BoxCollider escapeArea;
        float findAt, checkAt;
        StageEntranceGather stageEntrance;
        NavigationHandle guide;
        Vector3? guideDestination;
        float nextGuideCheck;
        bool guiding, previousPassageOpen, previousDormClosed;

        public static bool IsVisible => instance != null && instance.panel != null && instance.panel.Visible;
        public static string CurrentTitle => IsVisible ? instance.panel.Title : "";
        public static string CurrentBody => IsVisible ? instance.panel.Body : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(PursuitQuest));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PursuitQuest>();
        }

        void Awake() => panel = new WaitingRoomRightPanel(transform, "PursuitQuestCanvas");

        void Update()
        {
            var run = CampaignRun.Instance;
            var state = ExplorationState.Instance;
            if (run != null && run.State.IsRunning && state != null && state.HasAuthority && !state.HasVisited(ExplorationState.EscapeEntranceVisit))
                WatchEscapeEntrance(state);
        }

        void LateUpdate()
        {
            var run = CampaignRun.Instance;
            if (run == null || !ShouldShow(run.State)) { panel.Hide(); StopGuide(); return; }
            var s = run.State;
            var state = ExplorationState.Instance;
            bool visited = state != null && state.HasVisited(ExplorationState.EscapeEntranceVisit);
            bool night = s.Phase == CampaignPhase.Night;
            // 정비 시간에 무대 입구 통로가 철창으로 닫혀 있으면 공연장 이동 임무를 띄우지 않는다(Task-129).
            bool stageOpen = !s.IsFinalMorning && (!night || s.Day < CurtainCall.Settings.GameSettings.Base.Run.DayCount) && !StageCorridorClosure.BlocksStage(s);
            panel.Show(PursuitTexts.QuestTitleFor(s.Phase), PursuitTexts.QuestBody(stageOpen, visited, night));
            UpdateGuide(s, stageOpen, visited, state);
        }

        void UpdateGuide(CampaignSnapshot s, bool stageOpen, bool escapeVisited, ExplorationState exploration)
        {
            // The escape reveal records the route as open before its physical doors finish moving.
            // Resume with a fresh bake once the cutscene releases pursuit and the player.
            if (PursuitDirector.CutsceneHold) { StopGuide(); return; }
            var local = NetworkPlayer.Local;
            var start = local != null ? (Vector3?)local.transform.position : exploration?.LocalFeet;
            if (!start.HasValue || (local != null && !local.IsSpawned)) { StopGuide(); return; }

            var escape = EscapeEntrance.Current;
            bool passageOpen = escape != null && escape.isActiveAndEnabled && escape.IsPassageOpen;
            bool dormClosed = DormEntryEvent.IsDoorClosed;
            if (!guiding || passageOpen != previousPassageOpen || (s.Phase == CampaignPhase.Night && dormClosed != previousDormClosed))
            {
                // The rehearsal guide can have baked the room before these doors opened.
                NavigationService.EnsureExists().InvalidatePaths();
                guiding = true;
                previousPassageOpen = passageOpen;
                previousDormClosed = dormClosed;
                nextGuideCheck = 0;
                SetGuide(null, default);
            }
            if (Time.unscaledTime < nextGuideCheck) return;
            nextGuideCheck = Time.unscaledTime + CheckInterval;
            var feet = start.Value;
            if (s.Phase != CampaignPhase.Night && stageOpen && (stageEntrance == null || !stageEntrance.isActiveAndEnabled))
                stageEntrance = FindFirstObjectByType<StageEntranceGather>();

            Vector3? primary = s.Phase == CampaignPhase.Night ? DormEntryEvent.EntranceGuideDestination()
                : stageOpen && stageEntrance != null ? stageEntrance.GuideDestination : null;
            bool atPrimary = s.Phase != CampaignPhase.Night && stageOpen && stageEntrance != null && stageEntrance.IsInsideAt(feet);
            // First morning leads only to the first performance. The normal rehearsal gather guide owns the untimed tutorial.
            bool firstMorning = s.Phase == CampaignPhase.RestTime && s.Day == 1 && s.Rest == RestKind.Morning;
            var assembly = !firstMorning && escapeVisited && passageOpen ? escape.AssemblyArea : null;
            Vector3? secondary = assembly != null && assembly.enabled ? assembly.bounds.center : null;
            bool atEscape = secondary.HasValue && EscapeEntrance.Contains(assembly, feet);
            var destination = atPrimary || atEscape || (s.Phase == CampaignPhase.Night && DormEntryEvent.IsInsideDormitory(feet))
                ? null : ClosestEntrance(feet, primary, secondary);
            SetGuide(destination, local != null ? NavigationTarget.LocalPlayer : NavigationTarget.Position(feet));
        }

        static Vector3? ClosestEntrance(Vector3 feet, Vector3? primary, Vector3? secondary)
        {
            float best = GuideDistance;
            Vector3? destination = null;
            Consider(primary);
            Consider(secondary);
            return destination;

            void Consider(Vector3? candidate)
            {
                if (!candidate.HasValue || (candidate.Value - feet).sqrMagnitude > GuideDistance * GuideDistance) return;
                if (!NavigationService.EnsureExists().TryGetPathDistance(feet, candidate.Value, out float distance) || distance >= best) return;
                best = distance;
                destination = candidate;
            }
        }

        void SetGuide(Vector3? destination, NavigationTarget start)
        {
            if (!destination.HasValue)
            {
                guide?.Dispose(); guide = null; guideDestination = null;
            }
            else if (guide == null || guide.IsDisposed)
            {
                guide = WorldNavigation.Show(destination.Value, start);
                guideDestination = destination;
            }
            else if (guideDestination != destination || NetworkPlayer.Local == null)
            {
                guide.Update(destination.Value, start);
                guideDestination = destination;
            }
        }

        void StopGuide()
        {
            guiding = false;
            SetGuide(null, default);
        }

        static bool ShouldShow(CampaignSnapshot s)
        {
            if (!PursuitDirector.IsPursuitPhase(s.Phase) || s.Timer != RestTimerState.Pursuit || SceneCurtainTransition.IsActive) return false;
            var local = NetworkPlayer.Local;
            // 접속하지 않은 씬 확인(오프라인 추격 시작)에서는 내 캐릭터 상태 없이 띄운다.
            if (local == null) return NetworkPlayer.All.Count == 0;
            return local.State == PlayerState.Normal && !PursuitDirector.IsCaptured(local);
        }

        // 판정 화면: 플레이 중인 누구든 탈출로 입구 구역에 들어가면 이번 런의 방문으로 기록한다.
        void WatchEscapeEntrance(ExplorationState state)
        {
            if (Time.unscaledTime < checkAt) return;
            checkAt = Time.unscaledTime + CheckInterval;
            if (escapeArea == null)
            {
                if (Time.unscaledTime < findAt) return;
                findAt = Time.unscaledTime + FindInterval;
                var go = GameObject.Find(EscapeEntranceArea);
                escapeArea = go != null ? go.GetComponent<BoxCollider>() : null;
                if (escapeArea == null) return;
            }
            if (NetworkPlayer.All.Count > 0)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || player.Slot == NetworkPlayer.NoSlot || player.IsApplyingInitialCondition) continue;
                    if (EscapeEntrance.Contains(escapeArea, player.transform.position)) { state.MarkVisited(ExplorationState.EscapeEntranceVisit); return; }
                }
                return;
            }
            foreach (var mover in FindObjectsByType<PlayerMover>(FindObjectsSortMode.None))
                if (mover.isActiveAndEnabled && EscapeEntrance.Contains(escapeArea, mover.transform.position))
                { state.MarkVisited(ExplorationState.EscapeEntranceVisit); return; }
        }

        void OnDisable() { panel?.Hide(); StopGuide(); }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
