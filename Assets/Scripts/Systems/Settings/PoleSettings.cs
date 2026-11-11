using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>모든 씬의 일반 봉·스위치 봉이 함께 사용하는 조작/운동 설정.</summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Pole Settings", fileName = "PoleSettings")]
    public sealed class PoleSettings : ScriptableObject
    {
        [Header("공통 인원·조작")]
        [InspectorName("최대 매달림 인원"), Tooltip("봉 하나에 위아래로 매달릴 수 있는 총인원(명, 1~4). 일반 봉과 스위치 봉에 공통 적용한다. 기본 2명. 인원을 늘리면 봉 아래 여유 공간도 필요하다. 플레이를 멈춘 상태에서 바꾼 뒤 다시 실행한다. 이미 생성된 봉은 코스만 재시작해도 인원이 바뀌지 않는다.")]
        [SerializeField, Range(1, 4)] int capacity = 2;
        [InspectorName("맨 위 사람만 반동 조작"), Tooltip("켜면 봉을 직접 잡은 맨 위 사람만 앞뒤 반동을 만든다. 끄면 매달린 사람들의 입력을 평균한다. 반대 방향 입력은 서로 상쇄된다. 기본 켬.")]
        [SerializeField] bool topRiderOnlySwing = true;
        [InspectorName("아래 사람이 있으면 놓기 제한"), Tooltip("켜면 자기 아래 사람이 모두 놓아야 도약할 수 있다. 끄면 중간이나 위 사람이 놓을 때 그 아래 사람도 함께 떨어진다. 시간 만료·사망으로 지지자를 잃으면 이 설정과 무관하게 아래 사람도 떨어진다. 기본 켬.")]
        [SerializeField] bool preventReleaseWithPassengers = true;
        [Header("매달리기")]
        [InspectorName("매달림 제한 시간"), Tooltip("맨 위 사람이 봉을 잡은 순간부터 놓칠 때까지의 시간(초). 아래 사람은 맨 위 사람의 남은 시간을 공유한다. 기본 30초. 이미 잡은 봉은 다음 탑승부터 적용한다.")]
        [SerializeField, Min(.1f)] float holdSeconds = 30f;
        [InspectorName("봉과 맨 위 사람 발 사이"), Tooltip("봉에서 맨 위 사람의 발까지 내려오는 거리(m). 커지면 몸이 더 아래에 매달린다. 기본 1.5m.")]
        [SerializeField, Min(.2f)] float hangLength = 1.5f;
        [InspectorName("매달린 사람 사이 간격"), Tooltip("위아래로 매달린 사람들의 발 사이 수직 간격(m). 기본 1.45m. 인원을 늘리면 이 간격만큼 사슬이 길어진다.")]
        [SerializeField, Min(.2f)] float passengerSeparation = 1.45f;
        [Header("앞뒤 운동 — 외형 케이블 길이와 독립")]
        [InspectorName("앞뒤 운동 속도 배율"), Tooltip("봉과 사람이 왕복하는 전체 물리 시간의 배율. 기본 1.7. 크게 하면 왕복과 반동 축적·감쇠가 함께 빨라지고, 도약에 이어받는 반동 속도도 같은 배율로 커진다. 추가 상승 속도는 그대로이며 수평 속도는 도약 상한을 넘지 않는다. 모든 봉·원격 화면에 공통 적용한다.")]
        [SerializeField, Range(.5f, 2f)] float swingMotionSpeed = 1.7f;
        [InspectorName("반동 계산용 진자 길이"), Tooltip("봉의 흔들림 이동 범위와 관성을 계산하는 길이(m). 천장 앵커·중립 위치는 바꾸지 않는다. 길어지면 같은 각도에서 이동 폭과 도약 관성이 커진다. 기본 2m, 최대 각도 85도에서 앞뒤 이동 폭 약 4m.")]
        [SerializeField, Min(.1f)] float swingLength = 2f;
        [InspectorName("최대 흔들림 각도"), Tooltip("중립 위치에서 앞뒤로 기울 수 있는 최대 각도(도). 기본 앞뒤 각각 85도. 1~85도 범위. 이 각도에 해당하는 총 반동 에너지도 제한하므로 계속 입력해도 숨은 반동이 무한히 쌓이지 않는다.")]
        [SerializeField, Range(1f, 85f)] float maxSwingAngle = 85f;
        [InspectorName("최대 흔들림 속도"), Tooltip("운동 속도 배율을 적용하기 전 회전 속도 상한(도/초). 기본 180도/초이며 배율 1.7이면 최대 306도/초. 최대 각도에 해당하는 에너지 상한도 함께 적용한다. 너무 낮추면 최대 각도에 도달하지 못한다.")]
        [SerializeField, Min(1f)] float maxSwingSpeed = 180f;
        [InspectorName("반동 입력 가속도"), Tooltip("입력 방향으로 움직이는 구간에서 보태는 힘(도/초², 운동 속도 배율 적용 전). W/S를 유지해도 매 왕복마다 힘이 쌓이고, 돌아오는 구간은 중력으로 움직인다. 클수록 적은 왕복으로 최대 반동에 도달한다. 기본 100으로 약 2~4회 왕복을 목표로 하며 입력 방식에 따라 달라진다.")]
        [SerializeField, Min(0f)] float swingAcceleration = 100f;
        [InspectorName("흔들림 감쇠"), Tooltip("마찰에 의해 반동이 줄어드는 정도(1/초, 운동 속도 배율 적용 전). 입력을 멈춰도 바로 정지하지 않고 왕복 폭과 도약 힘이 함께 서서히 줄어든다. 클수록 빨리 멈추며 0이면 감쇠하지 않는다. 기본 0.12.")]
        [SerializeField, Min(0f)] float swingDamping = .12f;
        [Header("도약")]
        [InspectorName("도약 추가 상승 속도"), Tooltip("손을 놓을 때의 위쪽 도약 속도(m/s). 기본 6m/s. 현재 반동 에너지는 수평 도약 힘으로 바꾸므로 끝점에서도 축적한 반동으로 멀리 뛴다. 마지막 반동 입력 방향으로 도약하며, 입력 이력이 없으면 움직이는 방향을 따른다. 반동이 없는 봉은 수평 추진 없이 위로만 뛴다.")]
        [SerializeField, Min(0f)] float releaseUpwardSpeed = 6f;
        [InspectorName("도약 수평 속도 상한"), Tooltip("현재 반동 에너지에서 계산한 수평 도약 속도의 상한(m/s). 기본 6m/s. 반동이 적으면 이보다 느리며 운반 레일 속도는 더하지 않는다.")]
        [SerializeField, Min(0f)] float horizontalSpeedLimit = 6f;
        [Header("기존 인간대포 봉 외형")]
        [InspectorName("일반 봉 외형"), Tooltip("일반 봉에 공통으로 표시할 기존 외형 프리팹. 조작·반동 수치는 바꾸지 않는다. 변경 후 코스를 다시 생성한다.")]
        [SerializeField] GameObject regularVisual;
        [InspectorName("일반 봉 너비"), Tooltip("모든 씬의 일반 봉 가로 길이(m). 기본 1.8m. 봉 길이와 양쪽 줄 사이 간격만 함께 조정하며, 봉 두께·줄 높이·잡기 거리·매달림 위치·반동·도약은 바꾸지 않는다. 씬이나 부모 오브젝트의 크기와 무관하게 적용한다.")]
        [SerializeField, Min(.1f)] float regularBarWidth = 1.8f;
        [InspectorName("스위치 봉 외형"), Tooltip("잡고 있는 동안 장치를 작동시키는 봉의 공통 외형 프리팹. 변경 후 코스를 다시 생성한다.")]
        [SerializeField] GameObject switchVisual;
        [InspectorName("스위치 봉 너비"), Tooltip("모든 씬의 스위치 봉 가로 길이(m). 기본 1.8m. 봉 길이와 양쪽 줄 사이 간격만 함께 조정하며, 봉 두께·줄 높이·잡기 거리·매달림 위치·반동·도약은 바꾸지 않는다. 씬이나 부모 오브젝트의 크기와 무관하게 적용한다.")]
        [SerializeField, Min(.1f)] float switchBarWidth = 1.8f;

        public int Capacity => Mathf.Clamp(capacity, 1, 4);
        public bool TopRiderOnlySwing => topRiderOnlySwing;
        public bool PreventReleaseWithPassengers => preventReleaseWithPassengers;
        public bool CanDrive(int seat) => seat >= 0 && (!topRiderOnlySwing || seat == 0);
        public float HoldSeconds => Mathf.Max(.1f, holdSeconds);
        public float GrabRadius => GameSettings.Base.Interaction.Distance;
        public float HangLength => Mathf.Max(.2f, hangLength);
        public float PassengerSeparation => Mathf.Max(.2f, passengerSeparation);
        public float SwingMotionSpeed => float.IsFinite(swingMotionSpeed) ? Mathf.Clamp(swingMotionSpeed, .5f, 2f) : 1.7f;
        public float SwingLength => Mathf.Max(.1f, swingLength);
        public float MaxSwingAngle => Mathf.Clamp(maxSwingAngle, 1f, 85f);
        public float MaxSwingSpeed => Mathf.Max(1f, maxSwingSpeed);
        public float SwingAcceleration => Mathf.Max(0f, swingAcceleration);
        public float SwingDamping => Mathf.Max(0f, swingDamping);
        public float ReleaseUpwardSpeed => Mathf.Max(0f, releaseUpwardSpeed);
        public float HorizontalSpeedLimit => Mathf.Max(0f, horizontalSpeedLimit);
        public float AirControlSpeed => GameSettings.Base.AirControl.Speed;
        public GameObject Visual(bool isSwitch) => isSwitch ? switchVisual : regularVisual;
        public float BarWidth(bool isSwitch)
        {
            float value = isSwitch ? switchBarWidth : regularBarWidth;
            return float.IsFinite(value) ? Mathf.Max(.1f, value) : 1.8f;
        }
    }
}
