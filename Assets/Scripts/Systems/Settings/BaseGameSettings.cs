using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>쉬는 시간 타이머가 언제부터 흐르는지. 결정: Harness/Project/Decisions/run-rules.md #1</summary>
    public enum RestTimerMode
    {
        /// <summary>A안: 쉬는 시간이 시작되자마자 흐른다. 대기실도 복귀 지점.</summary>
        FromRestStart = 0,
        /// <summary>B안(기본): 대기실에서 백스테이지로 처음 나온 순간부터 흐른다. 복귀는 무대 입구 통로만.</summary>
        FromLeavingWaitingRoom = 1,
    }

    /// <summary>
    /// 공통 세팅(세팅 5묶음 중 "공통"): 세션·런 진행·기물 소지·캐릭터·입력·균형 공통·연출·동기화. 묘기·공간과 상관없이 쓰는 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/BaseGameSettings.asset. <see cref="GameSettings.Base"/>로 읽는다.
    /// 기본값은 2026-10-03 프리팹 값(옮기기 전 값)과 같다. 런 진행·기물 소지는 2026-10-14 PM 결정값.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Base Game Settings", fileName = "BaseGameSettings")]
    public sealed class BaseGameSettings : ScriptableObject
    {
        [Header("세션")]
        [SerializeField] SessionSettings session = new();
        [Header("런 진행")]
        [SerializeField] RunSettings run = new();
        [Header("기물 소지")]
        [SerializeField] ItemCarrySettings itemCarry = new();
        [Header("캐릭터")]
        [SerializeField] CharacterSettings character = new();
        [Header("입력")]
        [SerializeField] InputSettings input = new();
        [Header("균형 공통")]
        [SerializeField] BalanceSettings balance = new();
        [Header("연출")]
        [SerializeField] PresentationSettings presentation = new();
        [Header("동기화")]
        [SerializeField] SyncSettings sync = new();
        [Header("신체 손상")]
        [SerializeField] BodyDamageSettings bodyDamage = new();

        public SessionSettings Session => session;
        public RunSettings Run => run;
        public ItemCarrySettings ItemCarry => itemCarry;
        public CharacterSettings Character => character;
        public InputSettings Input => input;
        public BalanceSettings Balance => balance;
        public PresentationSettings Presentation => presentation;
        public SyncSettings Sync => sync;
        public BodyDamageSettings BodyDamage => bodyDamage;

        void OnValidate() => balance.Validate();

        /// <summary>세션(방) 인원.</summary>
        [Serializable]
        public sealed class SessionSettings
        {
            [Tooltip("방 정원(명). 이 인원이 차면 더 들어오지 못한다(자동 시작은 하지 않음). 방을 만들 때 적용된다.")]
            [SerializeField, Min(1)] int maxPlayers = 4;

            [Tooltip("호스트가 게임을 시작할 수 있는 최소 인원(명, 호스트 포함). 정원보다 크면 정원을 쓴다. 기획 2(2026-10-14 PM). 1인 테스트는 개발자 모드로 한다.")]
            [SerializeField, Min(1)] int minPlayers = 2;

            public int MaxPlayers => maxPlayers;
            public int MinPlayers => Mathf.Min(minPlayers, maxPlayers);
        }

        /// <summary>런(1~N일차 + 마지막 날 다음 날 아침 쉬는 시간) 진행. 기획: 게임 흐름 2-2·2-4, 결정: run-rules.md</summary>
        [Serializable]
        public sealed class RunSettings
        {
            [Tooltip("공연하는 날 수(일). 이 다음 날 아침 쉬는 시간이 마지막 탈출 기회이고, 그것이 끝나면 런 실패. 기획 3.")]
            [SerializeField, Min(1)] int dayCount = 3;

            [Tooltip("하루 공연 횟수(회). 공연 사이마다 쉬는 시간, 마지막 공연 뒤 밤 정비. 기획 2.")]
            [SerializeField, Min(1)] int performancesPerDay = 2;

            [Tooltip("1일차 공연 순서(묘기 이름, 고정). 2일차부터는 밤 정비 때 정하는 세트리스트를 쓴다. 기획 줄타기 → 인간 대포.")]
            [SerializeField] string[] firstDayTricks = { "Tightrope", "HumanCannon" };

            [Tooltip("쉬는 시간 타이머 방식. B안 = 대기실에서 백스테이지로 나온 순간부터(기본), A안 = 쉬는 시간 시작부터. A/B 테스트용(2026-10-14 PM).")]
            [SerializeField] RestTimerMode restTimerMode = RestTimerMode.FromLeavingWaitingRoom;

            [Tooltip("쉬는 시간 길이(초). 아침·공연 사이 공통. 기획 5분 = 300.")]
            [SerializeField, Min(1f)] float restDuration = 300f;

            [Tooltip("가이드 쉬는 시간 길이(초). 1일차 첫 공연 뒤 쉬는 시간. 기획 미정(플레이테스트로 결정) — 임시 300.")]
            [SerializeField, Min(1f)] float guideRestDuration = 300f;

            [Tooltip("소집 경보 시점(초). 쉬는 시간이 이만큼 남으면 경보를 울린다. 기획 1분 = 60.")]
            [SerializeField, Min(0f)] float recallAlarmLeadTime = 60f;

            public int DayCount => dayCount;
            public int PerformancesPerDay => performancesPerDay;
            public RestTimerMode RestTimerMode => restTimerMode;
            public float RestDuration => restDuration;
            public float GuideRestDuration => guideRestDuration;
            public float RecallAlarmLeadTime => recallAlarmLeadTime;

            /// <summary>1일차 <paramref name="index"/>번째(0부터) 공연 묘기. 없으면 빈 문자열.</summary>
            public string FirstDayTrick(int index) =>
                firstDayTricks != null && index >= 0 && index < firstDayTricks.Length ? firstDayTricks[index] : "";
        }

        /// <summary>한 사람이 지닐 수 있는 기물 개수. 결정: injury-rules.md #8 (부상 기획 기본값)</summary>
        [Serializable]
        public sealed class ItemCarrySettings
        {
            [Tooltip("팔이 2개일 때 지닐 수 있는 기물(개). 부상 기획 2.")]
            [SerializeField, Min(0)] int twoArmSlots = 2;

            [Tooltip("팔이 1개일 때 지닐 수 있는 기물(개). 부상 기획 1.")]
            [SerializeField, Min(0)] int oneArmSlots = 1;

            [Tooltip("배낭이 있을 때 더 지닐 수 있는 기물(개). 부상 기획 +2.")]
            [SerializeField, Min(0)] int backpackBonus = 2;

            public int TwoArmSlots => twoArmSlots;
            public int OneArmSlots => oneArmSlots;
            public int BackpackBonus => backpackBonus;
        }

        /// <summary>캐릭터 몸과 일반 이동(플랫폼).</summary>
        [Serializable]
        public sealed class CharacterSettings
        {
            [Tooltip("플랫폼 위 이동 속도(m/s). 줄 위 속도는 외줄 세팅. 2026-10-03 PM 테스트 값 3.")]
            [SerializeField, Min(0f)] float freeMoveSpeed = 3f;

            [Tooltip("플랫폼 위에서 이동 방향으로 몸을 돌리는 속도(도/초).")]
            [SerializeField, Min(0f)] float turnSpeed = 720f;

            [Tooltip("중력 크기(m/s²). 기획 기준 9.81. 점프 높이·공중 시간도 이 값으로 계산한다.")]
            [SerializeField, Min(0.1f)] float gravity = 9.81f;

            [Tooltip("몸통(충돌 캡슐) 높이(m). 2026-10-03 기준 1.5. ⚠ 모델 맞춤 키(1.73)와 다르다 — 모델 작업(017)에서 확정.")]
            [SerializeField, Min(0.1f)] float capsuleHeight = 1.5f;

            [Tooltip("몸통(충돌 캡슐) 지름(m). 2026-10-03 기준 0.6. 높이보다 클 수 없다.")]
            [SerializeField, Min(0.05f)] float capsuleDiameter = 0.6f;

            [Tooltip("모델을 맞출 캐릭터 키(m). 기획 1.73. ⚠ 캡슐 높이(1.5)와 다르다 — 모델 작업(017)에서 확정. 모델을 끼울 때 적용된다.")]
            [SerializeField, Min(0.1f)] float modelHeight = 1.73f;

            [Tooltip("몸통으로 래그돌 같은 물체를 밀 때 주는 속도(m/s). 0이면 밀지 않는다.")]
            [SerializeField, Min(0f)] float pushSpeed = 2f;

            [Tooltip("다른 플레이어와 닿을 때 남기는 틈(m).")]
            [SerializeField, Min(0f)] float playerGap = 0.05f;

            public float FreeMoveSpeed => freeMoveSpeed;
            public float TurnSpeed => turnSpeed;
            public float Gravity => gravity;
            public float CapsuleHeight => capsuleHeight;
            public float CapsuleRadius => Mathf.Min(capsuleDiameter, capsuleHeight) * 0.5f;
            public float ModelHeight => modelHeight;
            public float PushSpeed => pushSpeed;
            public float PlayerGap => playerGap;
        }

        /// <summary>입력 판정.</summary>
        [Serializable]
        public sealed class InputSettings
        {
            [Tooltip("상호작용 키를 이 시간(초) 이상 누르면 '길게'(목마 해제 등). 기획 값이 없어 튜닝값 0.5.")]
            [SerializeField, Min(0.05f)] float longPressTime = 0.5f;

            public float LongPressTime => longPressTime;
        }

        /// <summary>균형 게이지 공통 규칙. 규칙: Harness/Project/Decisions/tightrope-balance.md</summary>
        [Serializable]
        public sealed class BalanceSettings
        {
            [Tooltip("중앙에서 이 값(±, 게이지 ±100 기준)까지 초록, 넘으면 빨강. 기획 40.")]
            [SerializeField, Range(0f, 100f)] float greenLimit = 40f;

            [Tooltip("빨강에 이 시간(초) 이상 머물면 추락. 기획 확정 2초.")]
            [SerializeField, Min(0.05f)] float fallTime = 2f;

            [Tooltip("균형 보정(A/D)을 끝까지 눌렀을 때 바늘이 움직이는 양(초당). 기획 60.")]
            [SerializeField, Min(0f)] float correctionSpeed = 60f;

            [Tooltip("걷는 중 자연 흔들림(초당). 기획 15.")]
            [SerializeField, Min(0f)] float movingSway = 15f;

            [Tooltip("서 있을 때 자연 흔들림(초당). 기획 5.")]
            [SerializeField, Min(0f)] float idleSway = 5f;

            [Tooltip("흔들림 방향이 바뀌는 간격(초). x~y 사이에서 랜덤. 기획 1~2초.")]
            [SerializeField] Vector2 swayDirectionInterval = new(1f, 2f);

            [Tooltip("기울기 가속: 초당 (현재 균형값 × 이 값)만큼 바깥으로 민다. 많이 기울수록 빨리 넘어간다. 기획 0.5.")]
            [SerializeField, Min(0f)] float tiltAcceleration = 0.5f;

            public float GreenLimit => greenLimit;
            public float FallTime => fallTime;
            public float CorrectionSpeed => correctionSpeed;
            public float MovingSway => movingSway;
            public float IdleSway => idleSway;
            public Vector2 SwayDirectionInterval => swayDirectionInterval;
            public float TiltAcceleration => tiltAcceleration;

            internal void Validate()
            {
                if (swayDirectionInterval.x < 0.05f) swayDirectionInterval.x = 0.05f;
                if (swayDirectionInterval.y < swayDirectionInterval.x) swayDirectionInterval.y = swayDirectionInterval.x;
            }
        }

        /// <summary>겉모습 연출(판정과 무관).</summary>
        [Serializable]
        public sealed class PresentationSettings
        {
            [Tooltip("균형이 끝(±100)일 때 몸 기울기(도).")]
            [SerializeField, Range(0f, 90f)] float maxTiltAngle = 25f;

            [Tooltip("몸 기울기가 목표를 따라가는 빠르기. 클수록 바로, 작을수록 부드럽게.")]
            [SerializeField, Min(0.1f)] float tiltFollowSharpness = 10f;

            [Tooltip("추락해 래그돌로 무너질 때 기울어 있던 쪽으로 밀어 주는 속도(m/s). 0이면 그냥 무너진다.")]
            [SerializeField, Min(0f)] float ragdollTiltPush = 1.5f;

            public float MaxTiltAngle => maxTiltAngle;
            public float TiltFollowSharpness => tiltFollowSharpness;
            public float RagdollTiltPush => ragdollTiltPush;
        }

        /// <summary>온라인 동기화.</summary>
        [Serializable]
        public sealed class SyncSettings
        {
            [Tooltip("균형 값이 이만큼(게이지 ±100 기준) 바뀌어야 다른 화면에 다시 보낸다. 작을수록 기울기가 촘촘히 맞고 전송이 늘어난다.")]
            [SerializeField, Min(0f)] float balanceSendThreshold = 0.5f;

            public float BalanceSendThreshold => balanceSendThreshold;
        }

        /// <summary>묘기 재시작 때 잃은 부위를 어떻게 할지(프로토타입 시험용).</summary>
        public enum BodyRestartRecovery
        {
            /// <summary>살아 있던 사람은 잃은 부위 그대로, 사망한 사람만 온전한 몸으로(정식 규칙과 같음).</summary>
            KeepSurvivors = 0,
            /// <summary>모두 온전한 몸으로.</summary>
            RestoreAll = 1,
        }

        /// <summary>신체 손상(팔·다리). 규칙: Harness/Project/Decisions/body-damage.md</summary>
        [Serializable]
        public sealed class BodyDamageSettings
        {
            [Tooltip("묘기 재시작 때 잃은 부위 처리(프로토타입 시험용). 생존자 유지 = 살아 있던 사람은 그대로·사망자만 복구(정식 규칙), 모두 복구 = 전원 온전한 몸. 정식 규칙을 바꾸는 값이 아니다.")]
            [SerializeField] BodyRestartRecovery restartRecovery = BodyRestartRecovery.KeepSurvivors;

            [Header("두 다리를 잃었을 때 (기어가기)")]
            [Tooltip("기어갈 때 이동 속도 배율(×). 걷기 속도에 곱한다. 임시값 0.5(줄 위 0.5m/s → 0.25m/s).")]
            [SerializeField, Range(0f, 1f)] float crawlSpeedMultiplier = 0.5f;

            [Tooltip("기어갈 때 제자리 점프를 할 수 있는지. 기획상 금지 규칙 없음 — 기본 가능(2026-10-04 PM).")]
            [SerializeField] bool crawlCanJump = true;

            [Tooltip("기어갈 때 옆줄 이동(점프)을 할 수 있는지. 기획상 금지 규칙 없음 — 기본 가능(2026-10-04 PM).")]
            [SerializeField] bool crawlCanLaneJump = true;

            [Tooltip("기어갈 때 충돌 몸통(캡슐) 높이(m). 서 있을 때는 게임 기본 세팅 · 캐릭터의 캡슐 높이. 임시값 0.9.")]
            [SerializeField, Min(0.1f)] float crawlCapsuleHeight = 0.9f;

            [Header("잘린 부위 연출 (판정과 무관)")]
            [Tooltip("잘린 팔·다리가 몸 바깥쪽으로 날아가는 속도(m/s). 임시값 3.")]
            [SerializeField, Min(0f)] float limbFlingSpeed = 3f;

            [Tooltip("잘린 팔·다리가 위로 튀는 속도(m/s). 임시값 2.5.")]
            [SerializeField, Min(0f)] float limbFlingUpSpeed = 2.5f;

            [Tooltip("잘린 팔·다리가 도는 속도(라디안/초, 랜덤 방향). 임시값 8.")]
            [SerializeField, Min(0f)] float limbSpin = 8f;

            [Tooltip("잘린 부위에 Rigidbody가 없을 때 붙이는 질량(kg). 임시값 1.")]
            [SerializeField, Min(0.01f)] float limbMass = 1f;

            [Tooltip("잘린 부위가 사라지기까지 시간(초). 0이면 묘기 재시작까지 남는다.")]
            [SerializeField, Min(0f)] float limbLifetime = 0f;

            public BodyRestartRecovery RestartRecovery => restartRecovery;
            public float CrawlSpeedMultiplier => crawlSpeedMultiplier;
            public bool CrawlCanJump => crawlCanJump;
            public bool CrawlCanLaneJump => crawlCanLaneJump;
            public float CrawlCapsuleHeight => crawlCapsuleHeight;
            public float LimbFlingSpeed => limbFlingSpeed;
            public float LimbFlingUpSpeed => limbFlingUpSpeed;
            public float LimbSpin => limbSpin;
            public float LimbMass => limbMass;
            public float LimbLifetime => limbLifetime;
        }
    }
}
