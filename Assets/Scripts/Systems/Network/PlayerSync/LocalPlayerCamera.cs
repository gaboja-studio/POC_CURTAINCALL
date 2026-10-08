using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// Cinemachine 가상 카메라가 내 캐릭터(<see cref="NetworkPlayer.Local"/>)를 따라가게 한다.
    /// 거리·높이·바라보는 높이·화각·흔들림 같은 카메라 연출은 이 오브젝트의 Cinemachine 컴포넌트에서 조정한다.
    /// 프리팹: Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab (메인 카메라에는 CinemachineBrain 필요)
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class LocalPlayerCamera : MonoBehaviour
    {
        CinemachineCamera virtualCamera;

        void Awake() => virtualCamera = GetComponent<CinemachineCamera>();

        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += Follow;
            NetworkPlayer.LocalDespawned += Clear;
            if (NetworkPlayer.Local != null) Follow(NetworkPlayer.Local);
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Follow;
            NetworkPlayer.LocalDespawned -= Clear;
        }

        void Follow(NetworkPlayer player)
        {
            virtualCamera.Target.TrackingTarget = player.transform;
            virtualCamera.Target.CustomLookAtTarget = false; // 바라보는 대상도 같은 캐릭터
        }

        void Clear() => virtualCamera.Target.TrackingTarget = null;
    }
}
