using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>원본 튜토리얼의 공개 완료 기록만 읽는 첫날 진행 연결. 안내 순서·기록·문구는 수정하지 않는다.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class WaitingRoomFirstDayFlow : MonoBehaviour
    {
        static WaitingRoomFirstDayFlow instance;
        WaitingRoomRightPanel firstDayQuest, teamWait;
        WaitingRoomTutorial tutorial;
        bool suspendedTutorial;
        string tutorialDoneKey = "";
        CampaignRun tutorialRun;
        // 진단 로그(InputGate.Describe)에서 이름으로 보이도록 전용 타입을 쓴다.
        sealed class TeamWaitLock { }
        readonly TeamWaitLock teamLock = new();
        StageEntranceGather gather;
        ScheduleBoardView board;
        DemoRunEnding dayTransition;
        public static bool FirstDayQuestVisible => instance != null && instance.firstDayQuest.Visible;
        public static string FirstDayQuestTitle => FirstDayQuestVisible ? instance.firstDayQuest.Title : "";
        public static string FirstDayQuestBody => FirstDayQuestVisible ? instance.firstDayQuest.Body : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(WaitingRoomFirstDayFlow));
            DontDestroyOnLoad(go);
            go.AddComponent<WaitingRoomFirstDayFlow>();
        }
        void Awake()
        {
            instance = this;
            firstDayQuest = new WaitingRoomRightPanel(transform, "FirstDayQuestCanvas", 300f, 400f, "first-day.panel.") { GuideStyle = true };
            teamWait = new WaitingRoomRightPanel(transform, "FirstDayTeamWaitCanvas", 300f, 400f) { GuideStyle = true };
        }
        void Update()
        {
            var run = CampaignRun.Instance;
            if (tutorial == null) tutorial = FindFirstObjectByType<WaitingRoomTutorial>();
            // 리허설은 자기 안내를 사용한다. 정본 컴포넌트를 수정하지 않고 해당 시간대에만 실행을 양보받는다.
            bool rehearsal = run != null && run.State.Phase == CampaignPhase.Rehearsal
                || WaitingRoomRehearsal.IsPracticing || WaitingRoomCannonRehearsal.IsActive;
            if (rehearsal) SuspendForRehearsal();
            else RestoreTutorial();
        }
        // 시작 요청이 Update 도중 들어와도 첫 설명을 띄우기 전에 일반 안내의 실행을 멈춘다.
        internal static void SuspendForRehearsal()
        {
            var flow = instance;
            if (flow == null) return;
            if (flow.tutorial == null) flow.tutorial = FindFirstObjectByType<WaitingRoomTutorial>();
            if (flow.tutorial == null || !flow.tutorial.gameObject.activeSelf) return;
            flow.tutorial.gameObject.SetActive(false);
            flow.suspendedTutorial = true;
        }
        bool GuideDone()
        {
            return CampaignRun.Instance != null && WaitingRoomTutorial.TimeGuideFinished(CampaignRun.Instance.State);
        }
        bool GuideWaiting(CampaignRun run)
        {
            var s = run.State;
            return (s.Phase == CampaignPhase.RestTime || s.Phase == CampaignPhase.Night)
                && s.Timer == RestTimerState.WaitingForExit;
        }
        void LateUpdate()
        {
            var run = CampaignRun.Instance;
            var player = NetworkPlayer.Local;
            bool waiting = run != null && GuideWaiting(run);
            // CampaignRun survives leaving/rehosting a room and the new run reuses generation/day 1.
            // End the acknowledgement period even when the component instance stays the same.
            if (tutorialRun != run || !waiting) { tutorialRun = run; tutorialDoneKey = ""; }
            bool ready = waiting && GuideDone();
            if (ready)
            {
                string key = run.State.Generation + "/" + run.State.Day + "/" + run.State.Phase + "/" + run.State.Rest + "/" + run.State.PerformanceIndex;
                if (tutorialDoneKey != key && run.NotifyTutorialDone()) tutorialDoneKey = key;
                waiting = GuideWaiting(run);
            }
            // Rehearsal suspends teammates' ordinary guide; waiting for that guide here would deadlock it.
            bool rehearsing = WaitingRoomRehearsal.IsPracticing || WaitingRoomCannonRehearsal.IsActive;
            bool teamLocked = !rehearsing && waiting && ready && player != null && player.State == PlayerState.Normal
                && !SceneCurtainTransition.IsActive;
            InputGate.SetBlocking(teamLock, teamLocked);
            if (teamLocked)
                teamWait.Show(WaitingRoomText.Get("tutorial.team-wait-title", "동료 기다리는 중"),
                    WaitingRoomText.Format("tutorial.team-wait", "모든 공연 준비자가 안내를 확인하면 움직일 수 있습니다. ({ready}/{total})",
                        ("ready", run.State.ReadyCount), ("total", Mathf.Max(run.State.RequiredCount, run.State.ReadyCount))));
            else teamWait.Hide();

            if (run == null || player == null || player.State != PlayerState.Normal
                || SceneCurtainTransition.IsActive || teamLocked
                || rehearsing)
            { firstDayQuest.Hide(); return; }
            // 스케줄표를 가까이서 편집할 때는 같은 목적의 화면 퀘스트가 판을 가리지 않는다.
            if (board == null) board = FindFirstObjectByType<ScheduleBoardView>();
            if (board != null && board.IsOpen) { firstDayQuest.Hide(); return; }
            var s = run.State;
            if (s.Day == 2 && s.Phase == CampaignPhase.RestTime && s.Rest == RestKind.Morning && s.PerformanceIndex == 0)
            {
                if (dayTransition == null) dayTransition = FindFirstObjectByType<DemoRunEnding>();
                if (InputGate.IsBlocked || dayTransition != null && dayTransition.IsDayTitleVisible) { firstDayQuest.Hide(); return; }
                firstDayQuest.Show("[tutorial] 다음 공연 준비하기",
                    "대기실에서 다른 공연 준비자들이 남긴 기록을 발견할 수 있습니다. 그러나 공연은 계속되어야 합니다. 다음 공연을 준비하세요.");
                return;
            }
            if (s.Day != 1) { firstDayQuest.Hide(); return; }
            string quest;
            // 추격 시간에는 추격 퀘스트(PursuitQuest)가 같은 자리를 쓴다.
            if ((s.Phase == CampaignPhase.RestTime || s.Phase == CampaignPhase.Night) && s.Timer == RestTimerState.Pursuit) { firstDayQuest.Hide(); return; }
            if (s.Phase == CampaignPhase.RestTime && s.Rest == RestKind.Guide)
                quest = s.Timer == RestTimerState.WaitingForExit ? "tutorial" : "gather";
            else if (s.Phase == CampaignPhase.Night)
            {
                quest = run.IsLocalReady ? "night-wait" : "night-bed";
            }
            else { firstDayQuest.Hide(); return; }
            if (gather == null) gather = FindFirstObjectByType<StageEntranceGather>();
            if (gather == null) { firstDayQuest.Hide(); return; }
            // 정본의 밤 할 일 패널을 가리지 않고 그 아래에 연결 퀘스트를 놓는다.
            firstDayQuest.SetMinimumTop(0f);
            firstDayQuest.Show(WaitingRoomText.Get("first-day.quest." + quest + ".title", "오늘 할 일"),
                WaitingRoomText.Format("first-day.quest." + quest + ".body", "화면 안내에 따라 오늘 일정을 진행하세요.",
                    ("ready", s.ReadyCount), ("total", s.RequiredCount),
                    ("inside", gather.InsideCount), ("alive", gather.AliveCount)));
        }
        void RestoreTutorial()
        {
            if (suspendedTutorial && tutorial != null) tutorial.gameObject.SetActive(true);
            suspendedTutorial = false;
        }
        void OnDisable()
        {
            InputGate.SetBlocking(teamLock, false);
            firstDayQuest?.Hide(); teamWait?.Hide(); RestoreTutorial();
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
