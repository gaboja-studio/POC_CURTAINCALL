using CurtainCall.Cameras;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// Cinemachine 가상 카메라가 내 캐릭터(<see cref="NetworkPlayer.Local"/>)를 따라가게 한다.
    /// 시점 연출은 CameraViewDirector에 맡기고, 로컬 생성·소멸과 따라갈 대상만 연결한다.
    /// 프리팹: Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab (메인 카메라에는 CinemachineBrain 필요)
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class LocalPlayerCamera : MonoBehaviour
    {
        CinemachineCamera virtualCamera;
        CameraViewDirector director;

        void Awake()
        {
            virtualCamera = GetComponent<CinemachineCamera>();
            director = GetComponent<CameraViewDirector>();
        }

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
            if (director != null) director.SetLocalPlayer(null);
        }

        void Follow(NetworkPlayer player)
        {
            if (director != null) director.SetLocalPlayer(player);
            virtualCamera.Target.TrackingTarget = player.transform;
            virtualCamera.Target.CustomLookAtTarget = false; // 바라보는 대상도 같은 캐릭터
        }

        void Clear()
        {
            if (director != null) director.SetLocalPlayer(null);
            virtualCamera.Target.TrackingTarget = null;
        }
    }
}
