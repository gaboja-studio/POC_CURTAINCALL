using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// 씬의 플레이어 UI(균형 게이지·테스트 HUD)가 "씬에서 처음 찾은 플레이어" 대신 내 캐릭터를 보여 주게 한다.
    /// 테스트 HUD의 재시작 키는 혼자 재시작 대신 호스트에 "모두 재시작"을 요청한다.
    /// 목록을 비워 두면 씬에서 찾은 것을 모두 쓴다.
    /// </summary>
    public sealed class LocalPlayerViews : MonoBehaviour
    {
        [Tooltip("내 캐릭터 균형을 보여 줄 게이지. 비워 두면 씬에서 모두 찾는다.")]
        [SerializeField] BalanceGaugeView[] gauges;

        [Tooltip("내 캐릭터를 대상으로 할 테스트 HUD. 비워 두면 씬에서 모두 찾는다.")]
        [SerializeField] PlayerCommandDebugHud[] huds;

        void Awake()
        {
            if (gauges == null || gauges.Length == 0) gauges = FindObjectsByType<BalanceGaugeView>(FindObjectsSortMode.None);
            if (huds == null || huds.Length == 0) huds = FindObjectsByType<PlayerCommandDebugHud>(FindObjectsSortMode.None);

            // 내 캐릭터가 생기기 전에 남의 캐릭터를 잡지 않게 자동 찾기를 끈다
            foreach (var gauge in gauges) gauge.AutoFindTarget = false;
            foreach (var hud in huds) hud.AutoFindTarget = false;
        }

        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += Bind;
            NetworkPlayer.LocalDespawned += Clear;
            if (NetworkPlayer.Local != null) Bind(NetworkPlayer.Local);
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Bind;
            NetworkPlayer.LocalDespawned -= Clear;
        }

        void Bind(NetworkPlayer player)
        {
            var balance = player.GetComponent<PlayerBalance>();
            var input = player.GetComponent<PlayerInputReader>();
            foreach (var gauge in gauges) gauge.Target = balance;
            foreach (var hud in huds)
            {
                hud.Target = input;
                hud.RestartOverride = () =>
                {
                    var run = CurtainCall.Tightrope.TightropeRun.Current;
                    if (run != null) run.RestartByHost();
                    else player.RequestRestartAll();
                };
            }
        }

        void Clear()
        {
            foreach (var gauge in gauges) gauge.Target = null;
            foreach (var hud in huds)
            {
                hud.Target = null;
                hud.RestartOverride = null;
            }
        }
    }
}
