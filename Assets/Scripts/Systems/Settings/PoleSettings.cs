using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>모든 씬의 일반 봉·스위치 봉이 함께 사용하는 조작/운동 설정.</summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Pole Settings", fileName = "PoleSettings")]
    public sealed class PoleSettings : ScriptableObject
    {
        [Header("매달리기")]
        [SerializeField, Min(.1f)] float holdSeconds = 30f;
        [SerializeField, Min(.2f)] float hangLength = 1.5f;
        [SerializeField, Min(.2f)] float passengerSeparation = 1.45f;
        [Header("앞뒤 운동 — 외형 케이블 길이와 독립")]
        [SerializeField, Min(.1f)] float swingLength = 1.5f;
        [SerializeField, Range(1f, 85f)] float maxSwingAngle = 80f;
        [SerializeField, Min(1f)] float maxSwingSpeed = 180f;
        [SerializeField, Min(0f)] float swingAcceleration = 300f;
        [SerializeField, Min(0f)] float swingDamping = .4f;
        [Header("도약")]
        [SerializeField, Min(0f)] float releaseUpwardSpeed = 6f;
        [SerializeField, Min(0f)] float horizontalSpeedLimit = 8f;
        [Header("기존 인간대포 봉 외형")]
        [SerializeField] GameObject regularVisual;
        [SerializeField] GameObject switchVisual;

        public const int Capacity = 2;
        public float HoldSeconds => Mathf.Max(.1f, holdSeconds);
        public float GrabRadius => GameSettings.Base.Interaction.Distance;
        public float HangLength => Mathf.Max(.2f, hangLength);
        public float PassengerSeparation => Mathf.Max(.2f, passengerSeparation);
        public float SwingLength => Mathf.Max(.1f, swingLength);
        public float MaxSwingAngle => Mathf.Clamp(maxSwingAngle, 1f, 85f);
        public float MaxSwingSpeed => Mathf.Max(1f, maxSwingSpeed);
        public float SwingAcceleration => Mathf.Max(0f, swingAcceleration);
        public float SwingDamping => Mathf.Max(0f, swingDamping);
        public float ReleaseUpwardSpeed => Mathf.Max(0f, releaseUpwardSpeed);
        public float HorizontalSpeedLimit => Mathf.Max(0f, horizontalSpeedLimit);
        public float AirControlSpeed => GameSettings.Base.AirControl.Speed;
        public GameObject Visual(bool isSwitch) => isSwitch ? switchVisual : regularVisual;
    }
}
