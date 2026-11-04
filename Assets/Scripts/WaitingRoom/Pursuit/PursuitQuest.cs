using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 추격 시간 동안 화면 오른쪽에 띄우는 퀘스트(Task-103, 2026-11-04 사용자).
    /// - 제목: 추격자를 피해 달아나세요. 본문: 공연장 입구 집결 줄 + (이번 런에서 누구든 탈출로 입구를 방문했으면) 탈출로 진입 줄.
    /// - 탈출로 입구 방문은 판정 화면이 <see cref="ExplorationState"/>에 런 범위로 기록해 모두에게 같게 보인다(세대 교체 때 비움).
    /// - 문구는 WaitingRoomTexts.txt의 <c>pursuit.quest.*</c>에서 고친다(<see cref="PursuitTexts"/>).
    /// </summary>
    public sealed class PursuitQuest : MonoBehaviour
    {
        /// <summary>오늘 할 일·리허설 퀘스트와 같은 자리(1920×1080 UI 단위). 추격 중에는 그 둘이 뜨지 않는다.</summary>
        const float Top = 300f, Width = 400f;
        /// <summary>탈출로 입구 구역 콜라이더 이름(Frame8Areas 아래, 탐색 타이머 구역과 같은 것).</summary>
        const string EscapeEntranceArea = "Area_B1_EscapeEntrance";
        const float FindInterval = 1f, CheckInterval = .2f;

        static PursuitQuest instance;
        WaitingRoomRightPanel panel;
        BoxCollider escapeArea;
        float findAt, checkAt;

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

        void Awake() => panel = new WaitingRoomRightPanel(transform, "PursuitQuestCanvas", Top, Width, "first-day.panel.") { GuideStyle = true };

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
            if (run == null || !ShouldShow(run.State)) { panel.Hide(); return; }
            var s = run.State;
            var state = ExplorationState.Instance;
            bool visited = state != null && state.HasVisited(ExplorationState.EscapeEntranceVisit);
            panel.Show(PursuitTexts.QuestTitle, PursuitTexts.QuestBody(!s.IsFinalMorning, visited));
        }

        static bool ShouldShow(CampaignSnapshot s)
        {
            if (s.Phase != CampaignPhase.RestTime || s.Timer != RestTimerState.Pursuit || SceneCurtainTransition.IsActive) return false;
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

        void OnDisable() => panel?.Hide();
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
