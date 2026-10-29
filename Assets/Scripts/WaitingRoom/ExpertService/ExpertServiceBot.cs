using System.Text;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 5단계 4인 AI 사전 점검용 자동 참가자(테스트 씬 전용, 정식 기능 아님). 에디터의 MPPM 가상 플레이어 창(프로젝트 경로에 Library/VP/)에서만 움직인다.
    /// - 오프라인이면 같은 PC의 LAN 방(127.0.0.1:7777)에 계속 접속을 시도한다.
    /// - 보이는 상태(일차·시간대·⑧ 단계·구매자·커튼 열림·모든 참가자의 잃은 팔다리·팀 이용 횟수·안내 문구)가 바뀔 때마다 "[ExpertBot]" 로그를 남긴다.
    ///   가상 플레이어 로그(Library/VP/*/Logs/Editor.log)를 모아 네 화면이 같은지 대조한다.
    /// - 자리 번호별 행동(쉬는 시간·밤 정비):
    ///   자리 1 = 구매자: 신체 복구를 사서 커튼 앞으로 가고, 커튼이 열리면 들어간다.
    ///   자리 2 = 끼어들기: 다른 사람이 예약했을 때 구매·입장을 시도해 거절되는지 남긴다.
    /// </summary>
    public sealed class ExpertServiceBot : MonoBehaviour
    {
        const string HostAddress = "127.0.0.1:7777";

        bool active;
        float nextJoin, nextAct;
        string lastLog;
        int boughtNight = -1, intrudedNight = -1, enteredNight = -1;
        ExpertRoomView view;

        void Awake()
        {
#if UNITY_EDITOR
            active = Application.dataPath.Replace('\\', '/').Contains("/Library/VP/");
#endif
            if (!active) enabled = false;
        }

        void Update()
        {
            var session = NetworkSessionManager.EnsureExists();
            if (session == null) return;
            if (session.ConnectionState == SessionConnectionState.Offline && Time.unscaledTime >= nextJoin)
            {
                nextJoin = Time.unscaledTime + 3f;
                _ = session.JoinLanAsync(HostAddress);
                return;
            }
            var service = ExpertService.Instance;
            var me = NetworkPlayer.Local;
            if (service == null || me == null) return;
            if (view == null) view = FindFirstObjectByType<ExpertRoomView>();

            Report(service, me);
            if (Time.unscaledTime < nextAct || !ExpertService.IsServiceTime) return;
            nextAct = Time.unscaledTime + .5f;
            Act(service, me);
        }

        void Act(ExpertService service, NetworkPlayer me)
        {
            int day = ExpertService.Day;
            switch (me.Slot)
            {
                case 1: Buyer(service, me, day); break;
                case 2: Intruder(service, me, day); break;
            }
        }

        void Buyer(ExpertService service, NetworkPlayer me, int day)
        {
            if (service.Phase == ExpertRoomPhase.Idle && boughtNight != day && PlayerCondition.CountLimbs(me.LostParts) > 0)
            {
                boughtNight = day;
                service.RequestMedical();
                Log($"자리1 신체 복구 구매 요청(잃은 {PlayerCondition.CountLimbs(me.LostParts)})");
                return;
            }
            if (service.Phase != ExpertRoomPhase.Booked || !service.LocalIsBuyer || view == null || enteredNight == day) return;
            var outside = view.transform.Find("ExpertRoomOutside");
            if (outside != null && !view.InReach(me.transform.position)) { Move(me, outside); Log("자리1 커튼 앞으로 이동"); return; }
            if (view.CurtainOpen < .9f) return;
            enteredNight = day;
            service.RequestEnter();
            Log($"자리1 입장 요청(커튼 {view.CurtainOpen:F2})");
        }

        void Intruder(ExpertService service, NetworkPlayer me, int day)
        {
            if (service.Phase != ExpertRoomPhase.Booked || service.LocalIsBuyer || intrudedNight == day || view == null) return;
            intrudedNight = day;
            var outside = view.transform.Find("ExpertRoomOutside");
            if (outside != null) Move(me, outside);
            view.Interact();
            Log($"자리2 커튼 앞 F(구매자 아님) → '{service.LastMessage}'");
            service.RequestMedical();
            Log("자리2 신체 복구 구매 시도(예약 중)");
        }

        static void Move(NetworkPlayer me, Transform point)
        {
            var mover = me.GetComponent<PlayerMover>();
            if (mover != null) mover.SetStartPose(point.position, point.rotation);
        }

        void Report(ExpertService service, NetworkPlayer me)
        {
            var run = CampaignRun.Instance;
            var text = new StringBuilder();
            text.Append($"자리{me.Slot} {(run != null ? run.State.Day : 0)}일 {(run != null ? run.State.Phase.ToString() : "-")} ⑧={service.Phase}");
            if (service.Phase != ExpertRoomPhase.Idle) text.Append($"(구매 {WaitingRoomShop.PlayerLabel(service.Buyer)}, {service.BookedLimbs}개)");
            text.Append($" 커튼={(view != null && view.CurtainOpen > .5f ? "열림" : "닫힘")} 팀이용={service.TeamUses} 자금={service.Balance} |");
            foreach (var player in NetworkPlayer.All)
                if (player.IsSpawned) text.Append($" P{player.Slot + 1}:{PlayerCondition.CountLimbs(player.LostParts)}");
            if (!string.IsNullOrEmpty(service.LastMessage)) text.Append($" | 안내='{service.LastMessage}'");
            string line = text.ToString();
            if (line == lastLog) return;
            lastLog = line;
            Log(line);
        }

        static void Log(string text) => Debug.Log($"[ExpertBot] {text}");
    }
}
