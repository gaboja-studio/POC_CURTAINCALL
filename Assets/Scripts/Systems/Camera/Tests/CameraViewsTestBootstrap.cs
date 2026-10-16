using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>CameraViews 테스트 씬에서 외줄 균형 없이 플랫폼 이동·시점을 확인한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraViewsTestBootstrap : MonoBehaviour
    {
        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += DisableBalance;
            if (NetworkPlayer.Local != null) DisableBalance(NetworkPlayer.Local);
        }

        void OnDisable() => NetworkPlayer.LocalSpawned -= DisableBalance;

        static void DisableBalance(NetworkPlayer player)
        {
            if (player.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
        }
    }
}
