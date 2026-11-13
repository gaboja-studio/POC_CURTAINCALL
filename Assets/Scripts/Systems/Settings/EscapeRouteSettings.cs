using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 탈출로 세팅(세팅 5묶음 중 "탈출로"): 탈출로 구간·추격·리타이어·엔딩 등 탈출로 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset. <see cref="GameSettings.EscapeRoute"/>로 읽는다.
    /// 탈출문 연출과 더미 공간 Task(CC-119)의 입력 행 수를 포함한다. 실제 참가 인원·집계는 정의하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Escape Route Settings", fileName = "EscapeRouteSettings")]
    public sealed class EscapeRouteSettings : ScriptableObject
    {
        [Header("더미 검증 전용 — 실제 참가 인원 아님")]
        [Tooltip("더미 공간 검증 화면에 보이는 입력 줄 수(1~4). 실제 참가 인원과 무관하다.")]
        [SerializeField, Range(1, 4)] int dummyInputRows = 2;

        public int DummyInputRows => Mathf.Clamp(dummyInputRows, 1, 4);

        [Header("탈출문 연출")]
        [Tooltip("양쪽 여닫이문의 최대 열림 각도(도).")]
        [SerializeField, Range(90f, 120f)] float exitDoorOpenAngle = 105f;
        [Tooltip("탈출문이 닫힘에서 완전히 열리기까지 걸리는 시간(초).")]
        [SerializeField, Min(.1f)] float exitDoorOpenSeconds = 4f;

        public float ExitDoorOpenAngle => Mathf.Clamp(exitDoorOpenAngle, 90f, 120f);
        public float ExitDoorOpenSeconds => Mathf.Max(.1f, exitDoorOpenSeconds);

        public enum EndingTimeOfDay { Night, Day }

        [Header("탈출 엔딩 연출 배경")]
        [Tooltip("탈출 엔딩(갈대밭 질주) 연출의 시간대. 밤은 달빛·짙은 남색 안개, 낮은 햇빛·하늘색 안개로 표시합니다.")]
        [SerializeField] EndingTimeOfDay endingTimeOfDay = EndingTimeOfDay.Night;
        [Tooltip("밤 배경의 조명·안개 값.")]
        [SerializeField] EndingLighting endingNight = EndingLighting.NightDefault();
        [Tooltip("낮 배경의 조명·안개 값.")]
        [SerializeField] EndingLighting endingDay = EndingLighting.DayDefault();

        public EndingTimeOfDay EndingTime => endingTimeOfDay;
        public EndingLighting EndingLightingFor(EndingTimeOfDay time) => time == EndingTimeOfDay.Day
            ? endingDay ??= EndingLighting.DayDefault()
            : endingNight ??= EndingLighting.NightDefault();

        [System.Serializable]
        public sealed class EndingLighting
        {
            [Tooltip("하늘(카메라 배경)과 안개 색.")]
            public Color skyColor = new(.027f, .043f, .075f);
            [Tooltip("안개 농도. 높을수록 멀리 있는 배경이 하늘색에 묻힙니다.")]
            [Min(0)] public float fogDensity = .004f;
            [Tooltip("그늘진 면에도 들어오는 주변광 색.")]
            public Color ambientColor = new(.25f, .30f, .43f);
            [Tooltip("달빛/햇빛 색.")]
            public Color lightColor = new(.52f, .66f, 1f);
            [Tooltip("달빛/햇빛 밝기.")]
            [Min(0)] public float lightIntensity = 1.3f;

            public static EndingLighting NightDefault() => new();
            public static EndingLighting DayDefault() => new()
            {
                skyColor = new Color(.62f, .78f, .93f),
                fogDensity = .0025f,
                ambientColor = new Color(.62f, .64f, .62f),
                lightColor = new Color(1f, .95f, .84f),
                lightIntensity = 1.6f
            };
        }

        [Header("심부 어둠 · 탈출로 루트 기준 높이")]
        [Tooltip("탈출로 아래 깊은 구멍을 어둡게 보이게 하는 높이 범위와 강도.")]
        [SerializeField] ShaftFadeTuning shaftFade = new();
        public ShaftFadeTuning ShaftFade => shaftFade ??= new();

        [System.Serializable]
        public sealed class ShaftFadeTuning
        {
            [Tooltip("탈출로 루트보다 이 높이(m) 아래부터 완전히 어둡게 표시합니다. 맵의 월드 위치를 옮겨도 같은 구간에 적용됩니다.")]
            [SerializeField] float bottom = -73f;
            [Tooltip("탈출로 루트 기준 이 높이(m) 위에는 심부 어둠을 적용하지 않습니다. 아래로 내려갈수록 어두워집니다.")]
            [SerializeField] float top = -29f;
            [Tooltip("심부 어둠 강도. 0은 끄기, 1은 기존의 검은 심부 표현입니다. 충돌·낙사 판정에는 영향을 주지 않습니다.")]
            [SerializeField, Range(0f, 1f)] float strength = 1f;
            public float Bottom => bottom;
            public float Top => Mathf.Max(bottom + .01f, top);
            public float Strength => Mathf.Clamp01(strength);
        }

        [Header("탈출로 5m 추격자")]
        [Tooltip("탈출로 무대 아래 추격자의 이동 속도·달리기 리듬·팔 공격·포획·마지막 전투 값.")]
        [SerializeField] PursuerTuning pursuer = new();
        public PursuerTuning Pursuer => pursuer ??= new();

        [System.Serializable]
        public sealed class PursuerTuning
        {
            [Tooltip("추격자가 평소 걷는 속도(m/s).")]
            [Min(.1f)] public float walkSpeed = 2.7f;
            [Tooltip("낮은 통로에서 몸을 숙이고 이동하는 속도(m/s).")]
            [Min(.1f)] public float crouchSpeed = 1.7f;
            [Tooltip("추격자가 달려들 때의 속도(m/s).")]
            [Min(.1f)] public float runSpeed = 5.2f;
            [Tooltip("등장 뒤 처음 이만큼(m) 걸은 다음부터 달리기를 시작할 수 있다.")]
            [Min(0)] public float firstWalkMetres = 6f;
            [Tooltip("탈출로 마지막 질주 구간에서 추격자가 달리는 속도(m/s).")]
            [Min(.1f)] public float finaleRunSpeed = 5.5f;
            [Tooltip("마지막 질주가 시작된 뒤 추격자가 뒤쫓기 시작할 때까지 주는 먼저 출발 시간(초).")]
            [Min(0)] public float finalChaseLeadSeconds = 4f;
            [Tooltip("가장 뒤의 플레이어와 이 거리(m) 이상 벌어지면 달리기를 시작한다.")]
            [Min(.1f)] public float runStartGap = 10f;
            [Tooltip("달리다가 이 거리(m)까지 따라붙으면 달리기를 멈추고 걷는다.")]
            [Min(.1f)] public float runStopGap = 6f;
            [Tooltip("한 번 달릴 때 최대로 달리는 시간(초).")]
            [Min(.1f)] public float runBurstSeconds = 1.8f;
            [Tooltip("달리기를 멈춘 뒤 다시 달릴 수 있을 때까지 쉬는 시간(초).")]
            [Min(.1f)] public float runRestSeconds = 3f;
            [Tooltip("복도에서 팔을 뻗어 잡을 수 있는 수평 거리(m).")]
            [Min(.1f)] public float contactRange = 3.15f;
            [Tooltip("승강장·와이어 구간에서 팔을 길게 뻗어 닿는 최대 거리(m).")]
            [Min(.1f)] public float armRange = 18f;
            [Tooltip("복도에서 팔 공격 전에 예고하는 시간(초). 이 시간 안에 피하면 맞지 않는다.")]
            [Min(.1f)] public float hallWarning = .85f;
            [Tooltip("승강장(대피소 앞)에서 팔 공격 전 예고 시간(초).")]
            [Min(.1f)] public float stationWarning = 3.5f;
            [Tooltip("와이어 구간에서 팔 공격 전 예고 시간(초).")]
            [Min(.1f)] public float wireWarning = 2.5f;
            [Tooltip("예고가 끝나고 팔이 목표까지 뻗어 나가는 시간(초).")]
            [Min(.05f)] public float armStrikeSeconds = .42f;
            [Tooltip("공격 뒤 팔을 다시 거둬들이는 시간(초).")]
            [Min(.05f)] public float armRetractSeconds = .65f;
            [Tooltip("팔 공격이 빗나간 뒤 다음 행동까지 멈춰 있는 시간(초).")]
            [Min(.05f)] public float missRecoverySeconds = 1.15f;
            [Tooltip("뻗은 팔의 맞음 판정 굵기(반지름, m).")]
            [Min(.05f)] public float armRadius = .22f;
            [Tooltip("잡은 뒤 팔다리를 자르기까지 걸리는 시간(초).")]
            [Min(.05f)] public float severSeconds = .7f;
            [Tooltip("잡은 플레이어를 붙잡고 있는 전체 시간(초). 절단 시간보다 짧으면 절단 시간을 쓴다.")]
            [Min(.05f)] public float holdSeconds = 1.05f;
            [Tooltip("잡은 플레이어를 던지기 위해 휘두르는 시간(초).")]
            [Min(.05f)] public float throwSwingSeconds = .48f;
            [Tooltip("던져진 플레이어가 날아가는 시간(초).")]
            [Min(.05f)] public float throwFlightSeconds = 1.2f;
            [Header("마지막 추격자 전투")]
            [Tooltip("마지막 추격자 전투에서 추격자가 움직이는 기본 속도(m/s).")]
            [Min(.1f)] public float bossMoveSpeed = 5.8f;
            [Tooltip("마지막 추격자의 신체를 하나 자를 때마다 느려지는 비율(0.07 = 7%).")]
            [Range(0, .12f)] public float bossSlowPerCut = .07f;
            [Tooltip("도약 공격 전에 착지 지점을 노려보며 예고하는 시간(초).")]
            [Min(.1f)] public float bossLeapWarning = .8f;
            [Tooltip("도약해서 착지할 때까지 걸리는 시간(초).")]
            [Min(.1f)] public float bossLeapSeconds = 1.45f;
            [Tooltip("도약할 때 가장 높이 뜨는 높이(m).")]
            [Min(.1f)] public float bossLeapHeight = 8f;
            [Tooltip("착지한 뒤 다시 움직이기까지 멈춰 있는 시간(초). 공격할 틈이다.")]
            [Min(.1f)] public float bossLandingRecovery = 1.1f;
            [Tooltip("돌진 한 번에 움직이는 거리(m).")]
            [Min(.1f)] public float bossDashDistance = 3.2f;
            [Tooltip("돌진에 걸리는 시간(초).")]
            [Min(.1f)] public float bossDashSeconds = .3f;
            [Tooltip("돌진 뒤 다음 돌진까지 기다리는 시간(초).")]
            [Min(.1f)] public float bossDashCooldown = 3.2f;
            [Tooltip("돌진 직전 예고 시간(초).")]
            [Min(.1f)] public float bossDashWarning = .2f;
            [Tooltip("마지막 전투 중 팔 공격 전 예고 시간(초). 일반 구간 예고 대신 쓴다.")]
            [Min(.1f)] public float bossAttackWarning = 1.1f;
            [Tooltip("공격을 맞고 다시 움직이기까지 경직되는 시간(초).")]
            [Min(.1f)] public float bossHitRecovery = .65f;
        }
    }
}
