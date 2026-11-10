using UnityEngine;

namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Human Cannon Settings", fileName = "HumanCannonSettings")]
    public sealed class HumanCannonSettings : TrickSettings
    {
        [Header("대포")]
        [Tooltip("발사부터 다시 탑승할 수 있을 때까지의 시간(초). 포신이 내려간 상태의 대기 시간을 조절합니다.")]
        [SerializeField, Range(4f, 20f)] float cannonCooldown = 5f;


        public float CannonCooldown => Mathf.Clamp(cannonCooldown, 4f, 20f);
        public float PoleJumpHorizontalSpeedLimit => GameSettings.Poles.HorizontalSpeedLimit;
    }
}
