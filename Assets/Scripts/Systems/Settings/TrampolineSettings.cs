using UnityEngine;

namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Trampoline", fileName = "TrampolineSettings")]
    public sealed class TrampolineSettings : ScriptableObject
    {
        [InspectorName("반동 상승 속도"), Tooltip("트램펄린에서 튀어 오를 때 위쪽 속도(m/s). 점프 높이(m)가 아니다. 모든 씬의 트램펄린과 스프링 신발 반동의 기준값이며, 신발은 아이템 효과 배율도 곱한다. 기본 12m/s.")]
        [SerializeField, Min(.1f)] float upwardSpeed = 12f;
        [InspectorName("운반 이동 속도"), Tooltip("트램펄린을 들고 이동하는 기본 속도(m/s). 부상 배율은 별도로 적용하며 반동 높이에는 영향을 주지 않는다. 기본 4.5m/s.")]
        [SerializeField, Min(0f)] float carryMoveSpeed = 4.5f;
        [InspectorName("반동에 운반자 필요")]
        [SerializeField, Tooltip("켜면 다른 단원이 손잡이를 잡고 있어야 튀어 오른다. 끄면 바닥에 내려놓고 혼자서도 사용할 수 있다. 본 공연과 연습실에 공통 적용. 기본 끔. 켜면 연습 마지막 운반자의 혼자 반동하는 단계도 제한된다.")]
        bool requireCarrierForBounce;
        public float UpwardSpeed => Mathf.Max(.1f, upwardSpeed);
        public float CarryMoveSpeed => Mathf.Max(0, carryMoveSpeed);
        public bool RequireCarrierForBounce => requireCarrierForBounce;
        public float AirControlSpeed => GameSettings.Base.AirControl.Speed;
        public float GrabDistance => GameSettings.Base.Interaction.Distance;
    }
}
