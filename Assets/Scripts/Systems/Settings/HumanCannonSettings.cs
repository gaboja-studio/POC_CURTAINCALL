using UnityEngine;

namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Human Cannon Settings", fileName = "HumanCannonSettings")]
    public sealed class HumanCannonSettings : TrickSettings
    {
        [Header("일반 이동")]
        [Tooltip("인간대포 공간에서 일반 점프의 높이(m, 발 기준 최고점). 부상 배율은 별도로 적용. 대포 발사와 트램폴린 반동에는 적용하지 않는다.")]
        [SerializeField, Min(0f)] float jumpHeight = 1.2f;

        public float JumpHeight => jumpHeight;
    }
}
