using System.Collections;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 4단계 테스트 씬 전용 작은 패널(화면 오른쪽 아래). 밤 정비까지 빨리 가서 확인하려는 것이며 정식 기능이 아니다.
    /// - 1인 테스트 시작: (에디터) 개발자 모드를 잠시 켜고 LAN 방 만들기 → 게임 시작 → 도입 끝 → 테스트 자금까지 한 번에 한다.
    /// - 런 진행(호스트): 공연 성공 정산·공연 종료, 쉬는 시간 → 다음 공연, 밤까지 건너뛰기.
    /// - 오늘 스케줄(세트리스트)과 런이 부르는 공연을 함께 보이고, 밤이면 준비 완료(스케줄표와 같은 확정 검사)를 둔다.
    /// 공용 런 진행 패널(CampaignFlowTestUI)은 스케줄표 확정 검사 없이 밤 준비 완료를 누를 수 있어 이 씬에 두지 않는다.
    /// 여러 명 접속은 같은 씬의 공용 NetworkSessionTestUI로 한다.
    /// </summary>
    public sealed class NightScheduleTestUI : MonoBehaviour
    {
        [Tooltip("테스트 자금 받기 때 참가자마다 넣는 환호 횟수.")]
        [SerializeField, Min(1)] int cheers = 10;

        [Tooltip("1인 테스트 시작 각 단계를 기다리는 최대 시간(초).")]
        [SerializeField, Min(1f)] float stepTimeout = 15f;

        ScheduleBoardView boardView;
        bool folded;
        bool busy;
        bool? devModeBefore;
        string message = "";

        void OnDestroy()
        {
#if UNITY_EDITOR
            // 이 패널이 켠 개발자 모드는 Play를 끝낼 때 되돌린다.
            if (devModeBefore.HasValue && DevMode.Enabled != devModeBefore.Value) DevMode.SetEditorEnabled(devModeBefore.Value);
#endif
        }

        void OnGUI()
        {
            // 스케줄표 창이 열려 있으면 그 버튼을 가리지 않게 숨긴다.
            if (boardView == null) boardView = FindFirstObjectByType<ScheduleBoardView>();
            if (boardView != null && boardView.IsOpen) return;

            const float width = 300;
            float height = folded ? 34 : 300;
            GUILayout.BeginArea(new Rect(Screen.width - width - 10, Screen.height - height - 10, width, height), GUI.skin.box);
            if (GUILayout.Button(folded ? "밤 정비 테스트 패널 펴기" : "밤 정비 테스트 패널 접기")) folded = !folded;
            if (folded) { GUILayout.EndArea(); return; }

            var run = CampaignRun.Instance;
            var session = NetworkSessionManager.Instance;
            bool offline = session == null || session.ConnectionState == SessionConnectionState.Offline;
            bool authority = run != null && run.HasAuthority;

            GUI.enabled = offline && !busy;
            if (GUILayout.Button(busy ? "준비 중…" : "1인 테스트 시작(방·캐릭터·자금)")) StartCoroutine(StartSolo());
            GUI.enabled = true;

            if (run == null || (!authority && offline))
            {
                GUILayout.Label("Play 후 위 버튼을 누르세요.");
            }
            else
            {
                var s = run.State;
                GUILayout.Label($"{s.Day}일차 · {CampaignRun.Describe(s)}");
                DrawTodaySetlist(s);
                if (s.Phase == CampaignPhase.Night) DrawReady(run);
                if (!authority) GUILayout.Label("런 진행 버튼은 호스트만 씁니다.");
            }

            GUI.enabled = authority && !busy;
            if (authority) DrawHostButtons(run);
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);
            GUILayout.EndArea();
        }

        /// <summary>오늘 공연 순서(스케줄표)와 런이 실제로 부를 공연이 같은지 확인용으로 보인다.</summary>
        static void DrawTodaySetlist(in CampaignSnapshot s)
        {
            var schedule = ScheduleBoard.Instance;
            if (schedule == null || s.Day < 1 || s.Day > ScheduleBook.Days) return;
            var list = schedule.Book.Setlist(s.Day);
            GUILayout.Label($"오늘 스케줄: {Name(schedule, list[0])} → {Name(schedule, list[1])}");
        }

        static string Name(ScheduleBoard schedule, string trick) => string.IsNullOrEmpty(trick) ? "미정" : schedule.TrickName(trick);

        /// <summary>밤 정비 준비 완료(테스트용 바로 전환). 실제 입력은 숙소 침대(<see cref="NightBed"/>)다.</summary>
        static void DrawReady(CampaignRun run)
        {
            var s = run.State;
            GUILayout.Label($"밤 정비 — 준비 완료 {s.ReadyCount}/{s.RequiredCount} (침대 대신)");
            if (GUILayout.Button(run.IsLocalReady ? "준비 취소" : "준비 완료")) run.SetReady(!run.IsLocalReady);
        }

        void DrawHostButtons(CampaignRun run)
        {
            var s = run.State;
            switch (s.Phase)
            {
                case CampaignPhase.Intro:
                    if (GUILayout.Button("도입 끝")) run.EndIntro();
                    break;
                case CampaignPhase.Performance:
                    if (GUILayout.Button("공연 성공 정산(자금 받기)")) message = GrantFunds(run);
                    if (GUILayout.Button("공연 종료")) run.EndPerformance();
                    break;
                case CampaignPhase.RestTime:
                    if (!s.IsFinalMorning && GUILayout.Button("다음 공연으로(전원 무대 입구 진입)")) run.NotifyAllAtStageEntrance();
                    break;
            }
            if ((s.Phase is CampaignPhase.Performance or CampaignPhase.RestTime) && GUILayout.Button("밤 정비까지 건너뛰기"))
                StartCoroutine(SkipToNight(run));
        }

        /// <summary>지금 날의 남은 공연을 바로 끝내 밤 정비로 간다.</summary>
        IEnumerator SkipToNight(CampaignRun run)
        {
            busy = true;
            int day = run.State.Day;
            for (int guard = 0; guard < 8 && run.State.Day == day && run.State.Phase != CampaignPhase.Night; guard++)
            {
                if (run.State.Phase == CampaignPhase.RestTime) run.NotifyAllAtStageEntrance();
                else if (run.State.Phase == CampaignPhase.Performance) run.EndPerformance();
                else break;
                yield return null; // 장소 전환·알림이 한 프레임 돌게 둔다.
            }
            message = run.State.Phase == CampaignPhase.Night ? "밤 정비입니다." : $"밤까지 가지 못했습니다({CampaignRun.Describe(run.State)}).";
            busy = false;
        }

        IEnumerator StartSolo()
        {
            busy = true;
            var session = NetworkSessionManager.EnsureExists();
#if UNITY_EDITOR
            if (!DevMode.Enabled)
            {
                devModeBefore ??= DevMode.Enabled;
                DevMode.SetEditorEnabled(true); // 1명으로 게임을 시작하려면 필요하다(Play를 끝내면 되돌림).
            }
#endif
            message = "LAN 방을 만드는 중…";
            _ = session.HostLanAsync();
            yield return Wait(() => session.IsHost && NetworkPlayer.Local != null, "방을 만들지 못했습니다(포트 사용 중일 수 있음).");
            if (!busy) yield break;

            message = "게임 시작…";
            if (!session.StartGame()) { Fail($"게임을 시작하지 못했습니다(최소 {session.MinPlayers}명)."); yield break; }
            var run = CampaignRun.EnsureExists();
            yield return Wait(() => run.HasAuthority && run.State.Phase == CampaignPhase.Intro, "런이 시작되지 않았습니다.");
            if (!busy) yield break;

            run.EndIntro();
            yield return Wait(() => run.State.Phase == CampaignPhase.Performance, "공연 단계로 넘어가지 않았습니다.");
            if (!busy) yield break;

            var rewards = PerformanceRewards.EnsureExists();
            yield return Wait(() => rewards.HasAuthority, "공동 자금 판정 권한이 없습니다.");
            if (!busy) yield break;
            message = GrantFunds(run);
            busy = false;
        }

        IEnumerator Wait(System.Func<bool> done, string failure)
        {
            float until = Time.unscaledTime + stepTimeout;
            while (!done())
            {
                if (Time.unscaledTime > until) { Fail(failure); yield break; }
                yield return null;
            }
        }

        void Fail(string text)
        {
            message = text;
            busy = false;
        }

        /// <summary>공연 단계에서 전원 환호 → 성공 정산으로 공동 자금을 받는다. 정산 뒤 런은 다음 단계로 넘어간다.</summary>
        string GrantFunds(CampaignRun run)
        {
            var rewards = PerformanceRewards.Instance;
            if (rewards == null || !rewards.HasAuthority) return "공동 자금 판정 권한이 없습니다.";
            if (run.State.Phase != CampaignPhase.Performance) return "공연 단계에서만 됩니다.";
            if (rewards.State == null || !rewards.State.IsOpen)
            {
                if (!rewards.BeginPerformance(1)) return "공연을 열지 못했습니다(이미 정산 중일 수 있음).";
            }
            foreach (var segment in rewards.State.Segments)
                for (int i = 0; i < cheers; i++) rewards.AddCheer(segment.ParticipantId, true);
            long before = rewards.Balance;
            if (!rewards.FinishPerformance(true, "밤 정비 테스트 자금")) return "정산하지 못했습니다.";
            return $"공동 자금 {before} → {rewards.Balance}";
        }
    }
}
