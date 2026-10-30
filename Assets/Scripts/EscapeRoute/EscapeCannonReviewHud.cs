using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.EscapeRoute
{
    /// <summary>세 개발씬의 출발 시선과 R 재시작. 상시 조작 안내는 표시하지 않는다.</summary>
    [RequireComponent(typeof(EscapeDevelopmentSession))]
    public sealed class EscapeCannonReviewHud : MonoBehaviour
    {
        EscapeDevelopmentSession practice;

        void OnEnable() => NetworkPlayer.LocalSpawned += FaceStart;

        static void FaceStart(NetworkPlayer player)
        {
            // 공용 카메라는 첫 마우스 시점을 현재 화면 방향에서 이어 받는다.
            // 현재 출발점의 진행 방향을 첫 로컬 생성 때만 화면에 전달한다.
            if (player != null && Camera.main != null)
                Camera.main.transform.SetPositionAndRotation(player.transform.position + Vector3.up * 1.6f, player.transform.rotation);
        }

        void Start()
        {
            practice = GetComponent<EscapeDevelopmentSession>();
            InputGate.AddReleaseKeys(Key.R);
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= FaceStart;
        }

        void Update()
        {
            if (InputGate.IsBlocked || Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame) return;
            var player = NetworkPlayer.Local;
            if (player == null || !player.IsSpawned) return;
            if (practice == null || !practice.Restart()) player.RequestRestartAll();
            InputGate.RequireRelease();
        }

    }
}
