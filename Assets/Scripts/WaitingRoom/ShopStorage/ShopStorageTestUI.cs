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
    /// 3단계 테스트 씬 전용 작은 패널(화면 오른쪽 아래). 사람이 상점·운반·보관을 직접 확인할 수 있게 판정 화면(호스트)에서만 쓰는 버튼을 둔다.
    /// - 1인 테스트 시작: (에디터) 개발자 모드를 잠시 켜고 LAN 방 만들기 → 게임 시작 → 도입 끝 → 테스트 자금까지 한 번에 한다.
    ///   실제 플레이어 캐릭터가 ① 로비에 생긴다. 켠 개발자 모드는 Play를 끝낼 때 원래 값으로 되돌린다.
    /// - 테스트 자금 받기: 공연 단계일 때 공연을 열고 전원에게 환호를 넣은 뒤 성공 정산한다(공동 자금 수입). 정산 뒤 런은 다음 단계로 넘어간다.
    /// - 외팔 만들기 / 몸 복구: 내 캐릭터의 팔다리 상태를 바꿔 소지 한도 감소를 확인한다.
    /// 여러 명 접속은 같은 씬의 공용 NetworkSessionTestUI, 런 진행은 CampaignFlowTestUI로 한다. 정식 기능이 아니다.
    /// </summary>
    public sealed class ShopStorageTestUI : MonoBehaviour
    {
        [Tooltip("테스트 자금 받기 때 참가자마다 넣는 환호 횟수.")]
        [SerializeField, Min(1)] int cheers = 10;

        [Tooltip("1인 테스트 시작 각 단계를 기다리는 최대 시간(초).")]
        [SerializeField, Min(1f)] float stepTimeout = 15f;

        bool folded;
        bool starting;
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
            const float width = 270;
            float height = folded ? 34 : 220;
            GUILayout.BeginArea(new Rect(Screen.width - width - 10, Screen.height - height - 10, width, height), GUI.skin.box);
            if (GUILayout.Button(folded ? "상점 테스트 패널 펴기" : "상점 테스트 패널 접기")) folded = !folded;
            if (folded) { GUILayout.EndArea(); return; }

            var rewards = PerformanceRewards.Instance;
            var run = CampaignRun.Instance;
            var session = NetworkSessionManager.Instance;
            var local = NetworkPlayer.Local;
            bool authority = rewards != null && rewards.HasAuthority;
            bool offline = session == null || session.ConnectionState == SessionConnectionState.Offline;

            GUI.enabled = offline && !starting;
            if (GUILayout.Button(starting ? "1인 테스트 준비 중…" : "1인 테스트 시작(방·캐릭터·자금)")) StartCoroutine(StartSolo());
            GUI.enabled = true;
            GUILayout.Label(authority ? $"단계: {(run != null ? CampaignRun.Describe(run.State) : "-")}"
                : offline ? "Play 후 위 버튼을 누르세요." : "호스트만 아래 버튼을 쓸 수 있습니다.");

            GUI.enabled = authority && !starting;
            if (GUILayout.Button("테스트 자금 받기(공연 성공 정산)")) message = GrantFunds(rewards, run);
            GUI.enabled = authority && local != null && !starting;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("외팔 만들기")) { local.ServerSetOneArm(); message = "외팔 상태로 바꿨습니다."; }
            if (GUILayout.Button("몸 복구")) { local.ServerRestoreAll(); message = "몸을 복구했습니다."; }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);
            GUILayout.EndArea();
        }

        IEnumerator StartSolo()
        {
            starting = true;
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
            if (!starting) yield break;

            message = "게임 시작…";
            if (!session.StartGame()) { Fail($"게임을 시작하지 못했습니다(최소 {session.MinPlayers}명)."); yield break; }
            var run = CampaignRun.EnsureExists();
            yield return Wait(() => run.HasAuthority && run.State.Phase == CampaignPhase.Intro, "런이 시작되지 않았습니다.");
            if (!starting) yield break;

            run.EndIntro();
            yield return Wait(() => run.State.Phase == CampaignPhase.Performance, "공연 단계로 넘어가지 않았습니다.");
            if (!starting) yield break;

            var rewards = PerformanceRewards.EnsureExists();
            yield return Wait(() => rewards.HasAuthority, "공동 자금 판정 권한이 없습니다.");
            if (!starting) yield break;
            message = GrantFunds(rewards, run) + " — ③ 상점으로 가 보세요.";
            starting = false;
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
            starting = false;
        }

        string GrantFunds(PerformanceRewards rewards, CampaignRun run)
        {
            if (run == null || run.State.Phase != CampaignPhase.Performance)
                return "공연 단계에서만 됩니다. 런 진행 패널에서 '전원 무대 입구 통로 진입'을 누르세요.";
            if (rewards.State == null || !rewards.State.IsOpen)
            {
                if (!rewards.BeginPerformance(1)) return "공연을 열지 못했습니다(이미 정산 중일 수 있음).";
            }
            foreach (var segment in rewards.State.Segments)
                for (int i = 0; i < cheers; i++) rewards.AddCheer(segment.ParticipantId, true);
            long before = rewards.Balance;
            if (!rewards.FinishPerformance(true, "상점 테스트 자금")) return "정산하지 못했습니다.";
            return $"공동 자금 {before} → {rewards.Balance}";
        }
    }
}
