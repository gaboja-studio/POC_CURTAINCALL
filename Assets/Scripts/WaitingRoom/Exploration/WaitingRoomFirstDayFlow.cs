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
        readonly object teamLock = new();
        StageEntranceGather gather;
        ScheduleBoardView board;
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
            bool rehearsal = run != null && run.State.Phase == CampaignPhase.Rehearsal;
            if (rehearsal && tutorial != null && tutorial.gameObject.activeSelf)
            { tutorial.gameObject.SetActive(false); suspendedTutorial = true; }
            else if (!rehearsal) RestoreTutorial();
        }
        bool GuideDone()
        {
            if (!WaitingRoomTutorial.Enabled) return true;
            if (WaitingRoomTutorial.IsSequenceActive) return false;
            var seen = WaitingRoomTutorial.SeenIds.Split(',');
            return System.Array.IndexOf(seen, "G01") >= 0 && System.Array.IndexOf(seen, "G11") >= 0;
        }
        bool GuideWaiting(CampaignRun run)
        {
            var s = run.State;
            return s.Phase == CampaignPhase.RestTime && s.Rest == RestKind.Guide
                && s.Timer == RestTimerState.WaitingForExit;
        }
        void LateUpdate()
        {
            var run = CampaignRun.Instance;
            var player = NetworkPlayer.Local;
            bool waiting = run != null && GuideWaiting(run);
            bool ready = waiting && GuideDone();
            if (ready)
            {
                string key = run.State.Generation + "/" + run.State.Day;
                if (tutorialDoneKey != key && run.NotifyTutorialDone()) tutorialDoneKey = key;
                waiting = GuideWaiting(run);
            }
            bool teamLocked = waiting && ready && player != null && player.State == PlayerState.Normal
                && !SceneCurtainTransition.IsActive;
            InputGate.SetBlocking(teamLock, teamLocked);
            if (teamLocked)
                teamWait.Show(WaitingRoomText.Get("tutorial.team-wait-title", "동료 기다리는 중"),
                    WaitingRoomText.Format("tutorial.team-wait", "모든 공연 준비자가 안내를 확인하면 움직일 수 있습니다. ({ready}/{total})",
                        ("ready", run.State.ReadyCount), ("total", Mathf.Max(run.State.RequiredCount, run.State.ReadyCount))));
            else teamWait.Hide();

            if (run == null || run.State.Day != 1 || player == null || player.State != PlayerState.Normal
                || SceneCurtainTransition.IsActive || teamLocked)
            { firstDayQuest.Hide(); return; }
            var s = run.State;
            string quest;
            if (s.Phase == CampaignPhase.RestTime && s.Rest == RestKind.Guide)
                quest = s.Timer == RestTimerState.WaitingForExit ? "tutorial"
                    // 둘째 공연은 호스트가 스케줄표에서 고르고 확정해야 집결로 시작한다(CC-215).
                    : ScheduleBoard.Instance != null && !ScheduleBoard.Instance.Book.FirstDayConfirmed
                        ? (run.HasAuthority ? "second-pick" : "second-pick-wait") : "gather";
            else if (s.Phase == CampaignPhase.Night)
            {
                quest = run.IsLocalReady ? "night-wait" : NightBed.ScheduleReady ? "night-bed"
                    : run.HasAuthority ? "night-schedule" : "night-schedule-wait";
                if (board == null) board = FindFirstObjectByType<ScheduleBoardView>();
                if (board != null && board.IsOpen) { firstDayQuest.Hide(); return; }
            }
            else { firstDayQuest.Hide(); return; }
            if (gather == null) gather = FindFirstObjectByType<StageEntranceGather>();
            if (gather == null) { firstDayQuest.Hide(); return; }
            // 정본의 밤 할 일 패널을 가리지 않고 그 아래에 연결 퀘스트를 놓는다.
            firstDayQuest.SetMinimumTop(WaitingRoomTutorial.TodoVisible ? 560f : 0f);
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
