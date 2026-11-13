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
        [SerializeField, Range(1, 4)] int dummyInputRows = 2;

        public int DummyInputRows => Mathf.Clamp(dummyInputRows, 1, 4);

        [Header("탈출문 연출")]
        [Tooltip("양쪽 여닫이문의 최대 열림 각도(도).")]
        [SerializeField, Range(90f, 120f)] float exitDoorOpenAngle = 105f;
        [Tooltip("탈출문이 닫힘에서 완전히 열리기까지 걸리는 시간(초).")]
        [SerializeField, Min(.1f)] float exitDoorOpenSeconds = 4f;

        public float ExitDoorOpenAngle => Mathf.Clamp(exitDoorOpenAngle, 90f, 120f);
        public float ExitDoorOpenSeconds => Mathf.Max(.1f, exitDoorOpenSeconds);

        [Header("탈출로 5m 추격자")]
        [SerializeField] PursuerTuning pursuer = new();
        public PursuerTuning Pursuer => pursuer ??= new();

        [System.Serializable]
        public sealed class PursuerTuning
        {
            [Min(.1f)] public float walkSpeed = 2.7f;
            [Min(.1f)] public float crouchSpeed = 1.7f;
            [Min(.1f)] public float runSpeed = 5.2f;
            [Min(0)] public float firstWalkMetres = 6f;
            [Min(.1f)] public float finaleRunSpeed = 5.5f;
            [Min(0)] public float finalChaseLeadSeconds = 4f;
            [Min(.1f)] public float runStartGap = 10f;
            [Min(.1f)] public float runStopGap = 6f;
            [Min(.1f)] public float runBurstSeconds = 1.8f;
            [Min(.1f)] public float runRestSeconds = 3f;
            [Min(.1f)] public float contactRange = 3.15f;
            [Min(.1f)] public float armRange = 18f;
            [Min(.1f)] public float hallWarning = .85f;
            [Min(.1f)] public float stationWarning = 3.5f;
            [Min(.1f)] public float wireWarning = 2.5f;
            [Min(.05f)] public float armStrikeSeconds = .42f;
            [Min(.05f)] public float armRetractSeconds = .65f;
            [Min(.05f)] public float missRecoverySeconds = 1.15f;
            [Min(.05f)] public float armRadius = .22f;
            [Min(.05f)] public float severSeconds = .7f;
            [Min(.05f)] public float holdSeconds = 1.05f;
            [Min(.05f)] public float throwSwingSeconds = .48f;
            [Min(.05f)] public float throwFlightSeconds = 1.2f;
            [Header("마지막 추격자 전투")]
            [Min(.1f)] public float bossMoveSpeed = 5.8f;
            [Range(0, .12f)] public float bossSlowPerCut = .07f;
            [Min(.1f)] public float bossLeapWarning = .8f;
            [Min(.1f)] public float bossLeapSeconds = 1.45f;
            [Min(.1f)] public float bossLeapHeight = 8f;
            [Min(.1f)] public float bossLandingRecovery = 1.1f;
            [Min(.1f)] public float bossDashDistance = 3.2f;
            [Min(.1f)] public float bossDashSeconds = .3f;
            [Min(.1f)] public float bossDashCooldown = 3.2f;
            [Min(.1f)] public float bossDashWarning = .2f;
            [Min(.1f)] public float bossAttackWarning = 1.1f;
            [Min(.1f)] public float bossHitRecovery = .65f;
        }
    }
}
