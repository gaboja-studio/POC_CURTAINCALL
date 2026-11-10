using UnityEngine;

namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Trampoline", fileName = "TrampolineSettings")]
    public sealed class TrampolineSettings : ScriptableObject
    {
        [SerializeField, Min(.1f)] float upwardSpeed = 12f;
        [SerializeField, Min(0f)] float carryMoveSpeed = 4.5f;
        [SerializeField, Tooltip("다른 단원이 운반 손잡이를 잡고 있어야 반동한다. 본 공연과 연습실에 같은 규칙을 적용한다.")]
        bool requireCarrierForBounce;
        public float UpwardSpeed => Mathf.Max(.1f, upwardSpeed);
        public float CarryMoveSpeed => Mathf.Max(0, carryMoveSpeed);
        public bool RequireCarrierForBounce => requireCarrierForBounce;
        public float AirControlSpeed => GameSettings.Base.AirControl.Speed;
        public float GrabDistance => GameSettings.Base.Interaction.Distance;
    }
}
