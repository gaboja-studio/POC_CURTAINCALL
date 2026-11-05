using System.Text;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 진단 전용(동작 변경 없음). 대기실에서 내 캐릭터의 게임 입력이 <see cref="StuckSeconds"/>초 넘게 막혀 있으면
    /// 누가 막는지·안내 진행·캠페인 상태를 한 번 경고로 남기고, 풀리면 걸린 시간을 남긴다.
    /// 제보: 대기실 진입 직후 특정 플레이어만 첫 안내(G01·G11)가 안 뜨고 조작·상호작용 표시가 안 됨(Task-20261105-108).
    /// G01·G11은 입력이 막힌 동안 시작하지 않으므로, 남은 입력 막기가 공통 원인인지 확인한다. 빌드에서는 Player.log에 남는다.
    /// </summary>
    public sealed class WaitingRoomInputDiagnostics : MonoBehaviour
    {
        // 정상 연계 안내도 약 10~15초 입력을 막으므로 그보다 길게 잡는다.
        public const float StuckSeconds = 20f;
        static WaitingRoomInputDiagnostics instance;
        float blockedSince = -1f;
        bool reported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(WaitingRoomInputDiagnostics));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WaitingRoomInputDiagnostics>();
        }

        void Update()
        {
            var local = NetworkPlayer.Local;
            bool watching = local != null && local.IsSpawned && local.State == PlayerState.Normal
                && WaitingRoomScenes.IsWaitingRoom(SceneManager.GetActiveScene());
            if (!watching || !InputGate.IsBlocked) { Clear(); return; }
            float now = Time.unscaledTime;
            if (blockedSince < 0f) blockedSince = now;
            if (reported || now - blockedSince < StuckSeconds) return;
            reported = true;
            Debug.LogWarning($"[InputStuck] 대기실 입력이 {now - blockedSince:0}초째 막힘 · {Describe(local)}");
        }

        void Clear()
        {
            if (reported) Debug.Log($"[InputStuck] 입력 막힘 풀림 ({Time.unscaledTime - blockedSince:0}초 걸림) · {InputGate.Describe()}");
            blockedSince = -1f;
            reported = false;
        }

        static string Describe(NetworkPlayer local)
        {
            var text = new StringBuilder();
            text.Append(InputGate.Describe());
            text.Append(" · 안내 켜짐=").Append(WaitingRoomTutorial.Enabled)
                .Append(" 연계중=").Append(WaitingRoomTutorial.IsSequenceActive)
                .Append(" 패널=").Append(WaitingRoomTutorialPanel.IsVisible)
                .Append(" 본안내=[").Append(WaitingRoomTutorial.SeenIds).Append(']');
            text.Append(" · 커튼=").Append(SceneCurtainTransition.IsActive);
            var run = CampaignRun.Instance;
            if (run != null)
            {
                var s = run.State;
                text.Append($" · 캠페인 {s.Day}일차 {s.Phase}/{s.Rest}/{s.Timer} 준비 {s.ReadyCount}/{s.RequiredCount} 호스트={run.HasAuthority}");
            }
            var reader = local.GetComponent<PlayerInputReader>();
            text.Append($" · 나 client={local.OwnerClientId} slot={local.Slot} 입력읽기={(reader != null && reader.isActiveAndEnabled)}");
            text.Append(" · 씬[");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                text.Append(i > 0 ? "," : "").Append(SceneManager.GetSceneAt(i).name);
            return text.Append(']').ToString();
        }
    }
}
