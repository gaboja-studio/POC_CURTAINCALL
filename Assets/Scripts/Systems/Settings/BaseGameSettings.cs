using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace CurtainCall.Settings
{
    /// <summary>장소(공간 씬)를 바꾸는 방식. 결정: Task-20261013-032 plan.md (2026-10-16 PM)</summary>
    public enum SpaceTransitionMode
    {
        /// <summary>통째 교체(기본): 다음 공간 씬만 남기고 이전 씬을 모두 닫는다. 모두 같은 공간으로 함께 이동한다.</summary>
        Single = 0,
        /// <summary>추가 로드: 시작 씬(코어)은 남겨 두고 공간 씬만 붙였다 뗀다.</summary>
        Additive = 1,
    }

    /// <summary>
    /// 공통 세팅(세팅 5묶음 중 "공통"): 세션·런 진행·기물 소지·캐릭터·입력·균형 공통·연출·동기화·음성 채팅. 묘기·공간과 상관없이 쓰는 값.
    /// 에셋: Assets/Resources/Settings/GameSettings/BaseGameSettings.asset. <see cref="GameSettings.Base"/>로 읽는다.
    /// 기본값은 2026-10-03 프리팹 값(옮기기 전 값)과 같다. 런 진행·기물 소지는 2026-10-14 PM 결정값.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Base Game Settings", fileName = "BaseGameSettings")]
    public sealed class BaseGameSettings : ScriptableObject
    {
        [Header("프레임 제한")]
        [SerializeField] FrameRateSettings frameRate = new();

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
        [Header("HUD 배치")]
        [SerializeField] HudLayoutSettings hudLayout = new();
        [Header("연출")]
        [SerializeField] PresentationSettings presentation = new();
        [Header("동기화")]
        [SerializeField] SyncSettings sync = new();
        [Header("신체 손상")]
        [SerializeField] BodyDamageSettings bodyDamage = new();
        [Header("장소 전환")]
        [SerializeField] SpaceTransitionSettings spaceTransition = new();
        [Header("음성 채팅")]
        [SerializeField] VoiceChatSettings voiceChat = new();

        public FrameRateSettings FrameRate => frameRate;
        public SessionSettings Session => session;
        public RunSettings Run => run;
        public ItemCarrySettings ItemCarry => itemCarry;
        public CharacterSettings Character => character;
        public InputSettings Input => input;
        public BalanceSettings Balance => balance;
        public HudLayoutSettings HudLayout => hudLayout;
        public PresentationSettings Presentation => presentation;
        public SyncSettings Sync => sync;
        public BodyDamageSettings BodyDamage => bodyDamage;
        public SpaceTransitionSettings SpaceTransition => spaceTransition;
        public VoiceChatSettings VoiceChat => voiceChat;

        void OnValidate() => balance.Validate();

        public enum BalanceHudPosition
        {
            [InspectorName("1안 · 하단 중앙")] BottomCenter = 0,
            [InspectorName("2안 · 상단 중앙")] TopCenter = 1,
        }

        [Serializable]
        public sealed class HudLayoutSettings
        {
            [InspectorName("균형/추락 게이지 위치")]
            [Tooltip("1안은 기존 인벤토리 자리인 하단 중앙, 2안은 상단 중앙입니다. 플레이 중에도 즉시 반영되며, 상단 팝업은 게이지 아래로 이동합니다.")]
            [SerializeField] BalanceHudPosition balancePosition = BalanceHudPosition.BottomCenter;

            public BalanceHudPosition BalancePosition => balancePosition;
        }

        [Serializable]
        public sealed class FrameRateSettings
        {
            [InspectorName("FPS 제한 사용")]
            [Tooltip("켜면 최대 FPS를 제한합니다. 끄면 FPS 제한을 해제합니다. 플레이 중 변경도 반영되며, 빌드에는 빌드 당시 값이 포함됩니다. VSync는 실행 중 꺼집니다.")]
            [SerializeField] bool limitEnabled = true;

            [InspectorName("최대 FPS")]
            [Tooltip("초당 최대 프레임 수(FPS). 기본 60. 제한을 켰을 때만 적용됩니다.")]
            [SerializeField, Min(1)] int maxFps = 60;

            public bool LimitEnabled => limitEnabled;
            public int MaxFps => Mathf.Max(1, maxFps);
            public int TargetFrameRate => limitEnabled ? MaxFps : -1;
        }

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
        public sealed class RunSettings : ISerializationCallbackReceiver
        {
            [Tooltip("공연하는 날 수(일). 이 다음 날 아침 쉬는 시간이 마지막 탈출 기회이고, 그것이 끝나면 런 실패. 기획 3.")]
            [SerializeField, Min(1)] int dayCount = 3;

            [Tooltip("하루 공연 횟수(회). 공연 사이마다 쉬는 시간, 마지막 공연 뒤 밤 정비. 기획 2.")]
            [SerializeField, Min(1)] int performancesPerDay = 2;

            [Tooltip("1일차 공연 순서(묘기 이름, 고정). 2일차부터는 밤 정비 때 정하는 세트리스트를 쓴다. M2: 외줄타기 → 외줄타기(2026-10-29 사용자, Task-079).")]
            [SerializeField] string[] firstDayTricks = { "Tightrope", "Tightrope" };

            [Tooltip("2일차부터 스케줄표로 정하지 않은 공연에 쓰는 묘기. M2: 스케줄표 편성 보류 동안 외줄타기 고정(Task-079).")]
            [SerializeField] string defaultTrick = "Tightrope";

            [Tooltip("쉬는 시간 길이(초). 아침·공연 사이 공통. 시작하자마자 흐른다(M2, 안전한 곳 없음). 기획 5분 = 300.")]
            [SerializeField, HideInInspector, Min(1f)] float restDuration = 300f;

            [Tooltip("가이드 쉬는 시간 길이(초). 1일차 첫 공연 뒤 쉬는 시간. 기획 미정(플레이테스트로 결정) — 임시 300.")]
            [SerializeField, HideInInspector, Min(1f)] float guideRestDuration = 300f;

            [Header("시간대별 실제 제한시간 · 다음 단계 진입부터 적용")]
            [SerializeField, InspectorName("아침 쉬는 시간")] RestTimeLimitSettings morningRest;
            [SerializeField, InspectorName("낮 쉬는 시간")] RestTimeLimitSettings noonRest;
            [SerializeField, InspectorName("첫날 가이드 쉬는 시간")] RestTimeLimitSettings guideRest;
            [SerializeField, InspectorName("밤 정비")] RestTimeLimitSettings nightMaintenance;
            [SerializeField, HideInInspector] bool timeLimitsMigrated;

            /// <summary>단계 진입 때 적용하는 실제 제한시간. 무제한이어도 초 값은 유한하게 보관한다.</summary>
            [Serializable]
            public sealed class RestTimeLimitSettings
            {
                [SerializeField, InspectorName("제한 없음")] bool unlimited;
                [Tooltip("제한 없음이 꺼져 있을 때 사용하는 실제 초. 하루 시계의 시작·끝 시각과 별개입니다.")]
                [SerializeField, Min(1f), InspectorName("제한시간(초)")] float seconds = 300f;

                public RestTimeLimitSettings() { }
                public RestTimeLimitSettings(bool unlimited, float seconds = 300f)
                { this.unlimited = unlimited; this.seconds = seconds; }

                public bool Unlimited => unlimited;
                public float Seconds => float.IsNaN(seconds) || float.IsInfinity(seconds) ? 300f : Mathf.Max(1f, seconds);
            }

            // 기존 에셋은 Unity가 비어 있는 인라인 객체를 만들기 전에 저장한 공통/가이드 값을 이관한다.
            void EnsureTimeLimits()
            {
                if (!timeLimitsMigrated)
                {
                    morningRest = new RestTimeLimitSettings(false, restDuration);
                    noonRest = new RestTimeLimitSettings(false, restDuration);
                    guideRest = new RestTimeLimitSettings(false, guideRestDuration);
                    nightMaintenance = new RestTimeLimitSettings(true);
                    timeLimitsMigrated = true;
                }
                morningRest ??= new RestTimeLimitSettings(false, restDuration);
                noonRest ??= new RestTimeLimitSettings(false, restDuration);
                guideRest ??= new RestTimeLimitSettings(false, guideRestDuration);
                nightMaintenance ??= new RestTimeLimitSettings(true);
            }

            public void OnBeforeSerialize() => EnsureTimeLimits();
            public void OnAfterDeserialize() => EnsureTimeLimits();

            [Tooltip("소집 경보 시점(초). 쉬는 시간이 이만큼 남으면 경보를 울린다. 기획 1분 = 60.")]
            [SerializeField, Min(0f)] float recallAlarmLeadTime = 60f;

            [Tooltip("공연 실패 확정 뒤 묘기 화면을 비추는 시간(초). 이 시간이 지나야 다음 시간대·공간으로 넘어간다. 0이면 바로 넘어간다. 2026-10-19 PM 2초.")]
            [SerializeField, Min(0f)] float failedPerformanceHold = 2f;

            public int DayCount => dayCount;
            public int PerformancesPerDay => performancesPerDay;
            public RestTimeLimitSettings MorningRest { get { EnsureTimeLimits(); return morningRest; } }
            public RestTimeLimitSettings NoonRest { get { EnsureTimeLimits(); return noonRest; } }
            public RestTimeLimitSettings GuideRest { get { EnsureTimeLimits(); return guideRest; } }
            public RestTimeLimitSettings NightMaintenance { get { EnsureTimeLimits(); return nightMaintenance; } }
            public float RestDuration => NoonRest.Seconds;
            public float GuideRestDuration => GuideRest.Seconds;

            public RestTimeLimitSettings TimeLimit(CurtainCall.Campaign.RestKind rest) => rest switch
            {
                CurtainCall.Campaign.RestKind.Morning => MorningRest,
                CurtainCall.Campaign.RestKind.Guide => GuideRest,
                _ => NoonRest,
            };
            public float RecallAlarmLeadTime => recallAlarmLeadTime;
            public float FailedPerformanceHold => failedPerformanceHold;

            /// <summary>1일차 <paramref name="index"/>번째(0부터) 공연 묘기. 없으면 빈 문자열.</summary>
            public string FirstDayTrick(int index) =>
                firstDayTricks != null && index >= 0 && index < firstDayTricks.Length ? firstDayTricks[index] : "";

            /// <summary>2일차부터 편성되지 않은 공연의 묘기.</summary>
            public string DefaultTrick => string.IsNullOrEmpty(defaultTrick) ? "Tightrope" : defaultTrick;
        }

        /// <summary>팔 개수와 관계없는 기물 소지 개수. 결정: injury-rules.md #8 (2026-10-28)</summary>
        [Serializable]
        public sealed class ItemCarrySettings
        {
            [Tooltip("신체 상태와 관계없이 지닐 수 있는 기물(개). 기본 2. 배낭·외팔 슬롯 감소 없음.")]
            [FormerlySerializedAs("twoArmSlots")]
            [SerializeField, Min(0)] int baseSlots = 2;

            public int BaseSlots => Mathf.Max(0, baseSlots);
        }

        /// <summary>캐릭터 몸과 일반 이동(플랫폼).</summary>
        [Serializable]
        public sealed class CharacterSettings
        {
            [Tooltip("플랫폼 위 이동 속도(m/s, 8방향 일반 이동). 2026-10-03 PM 테스트 값 3.")]
            [SerializeField, Min(0f)] float freeMoveSpeed = 3f;

            [Tooltip("코스 방향 걷기 속도(m/s, 앞뒤로만 걷는 줄타기 이동). 외줄 세팅에서 옮김(값 그대로, 2026-10-14 PM). 기획 0.5.")]
            [SerializeField, Min(0f)] float courseMoveSpeed = 0.5f;

            [Tooltip("점프 높이(m, 발 기준 최고점). 공중 시간은 이 값과 중력으로 정해진다. 다리를 잃으면 신체 손상 · 점프 높이 배율을 곱한다. 외줄 세팅에서 옮김(값 그대로, 2026-10-14 PM). 0.8.")]
            [SerializeField, Min(0f)] float jumpHeight = 0.8f;

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

            [Tooltip("달리기(LShift 홀드) 이동 속도 배율(×). 플랫폼 걷기 속도에 곱한다. 2026-10-19 PM 1.5. 플레이 중 바꾸면 바로 반영.")]
            [SerializeField, Range(1f, 5f)] float sprintSpeedMultiplier = 1.5f;

            [Tooltip("앉기(LCtrl 홀드) 이동 속도 배율(×). 플랫폼 걷기 속도에 곱한다. 2026-10-19 PM 0.5. 플레이 중 바꾸면 바로 반영.")]
            [SerializeField, Range(0.05f, 1f)] float crouchSpeedMultiplier = 0.5f;

            [Tooltip("앉았을 때 높이 비율(0~1, 몸통 캡슐 높이 기준). 발은 고정하고 겉모습·충돌 높이·1인칭 눈높이를 함께 낮춘다. 2026-10-19 PM 0.6. 플레이 중 바꾸면 바로 반영.")]
            [SerializeField, Range(0.2f, 1f)] float crouchHeightRatio = 0.6f;

            [Header("플레이어 자리 색상")]
            [InspectorName("플레이어 1 색상")]
            [Tooltip("첫 번째 자리(0번) 플레이어 색상. 자리가 정해지지 않았거나 범위를 벗어나면 이 색상을 쓴다. 기본 빨강.")]
            [SerializeField] Color playerColorRed = Color.red;

            [InspectorName("플레이어 2 색상")]
            [Tooltip("두 번째 자리(1번) 플레이어 색상. 기본 파랑.")]
            [SerializeField] Color playerColorBlue = Color.blue;

            [InspectorName("플레이어 3 색상")]
            [Tooltip("세 번째 자리(2번) 플레이어 색상. 기본 노랑.")]
            [SerializeField] Color playerColorYellow = Color.yellow;

            [InspectorName("플레이어 4 색상")]
            [Tooltip("네 번째 자리(3번) 플레이어 색상. 기본 초록.")]
            [SerializeField] Color playerColorGreen = Color.green;

            /// <summary>자리 번호(0부터)의 표시 색상. 미배정·범위 밖은 첫 번째 색상을 쓴다.</summary>
            public Color GetPlayerColor(int slot) => slot switch
            {
                1 => playerColorBlue,
                2 => playerColorYellow,
                3 => playerColorGreen,
                _ => playerColorRed,
            };

            public float FreeMoveSpeed => freeMoveSpeed;
            public float CourseMoveSpeed => courseMoveSpeed;
            public float JumpHeight => CharacterJumpHeightOverrides.Resolve(jumpHeight);
            public float TurnSpeed => turnSpeed;
            public float Gravity => gravity;
            public float CapsuleHeight => capsuleHeight;
            public float CapsuleRadius => Mathf.Min(capsuleDiameter, capsuleHeight) * 0.5f;
            public float ModelHeight => modelHeight;
            public float PushSpeed => pushSpeed;
            public float PlayerGap => playerGap;
            public float SprintSpeedMultiplier => sprintSpeedMultiplier;
            public float CrouchSpeedMultiplier => crouchSpeedMultiplier;
            public float CrouchHeightRatio => crouchHeightRatio;
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
            [Tooltip("중앙에서 이 값(±, 게이지 ±100 기준)까지 초록. 외줄 고도화 초기값 30.")]
            [SerializeField, Range(0f, 100f)] float greenLimit = 30f;

            [Tooltip("초록 바깥부터 이 값(±)까지 노란 경고 구간. 노랑은 느리게, 빨강은 정상 속도로 추락 게이지가 쌓인다. 초기값 50.")]
            [SerializeField, Range(0f, 100f)] float warningLimit = 50f;

            [Tooltip("빨강에 이 시간(초) 이상 머물면 추락. 기획 확정 2초.")]
            [SerializeField, Min(0.05f)] float fallTime = 2f;
            [Tooltip("노란 구간 추락 게이지 누적 속도(빨강=1). 초기 테스트 1/3. 초록에서는 회복 속도에 따라 서서히 줄인다.")]
            [SerializeField, Range(0f, 1f)] float warningFallRate = 1f / 3f;

            [Tooltip("줄 위 초록 구간에서 초당 회복하는 추락 바의 비율(0~1). 0.25 = 초당 25%, 50% 위험은 2초에 회복. 공중에서는 회복하지 않는다.")]
            [SerializeField, Range(0f, 1f)] float greenRecoveryRate = .25f;

            [Tooltip("균형 보정(A/D)을 끝까지 눌렀을 때 바늘이 움직이는 양(초당). 외줄 고도화 초기값 85(이전 60).")]
            [SerializeField, Min(0f)] float correctionSpeed = 85f;

            [Tooltip("걷는 중 자연 흔들림(초당). 기획 15.")]
            [SerializeField, Min(0f)] float movingSway = 15f;

            [Tooltip("서 있을 때 자연 흔들림(초당). 기획 5.")]
            [SerializeField, Min(0f)] float idleSway = 5f;

            [Tooltip("흔들림 방향이 바뀌는 간격(초). x~y 사이에서 랜덤. 기획 1~2초.")]
            [SerializeField] Vector2 swayDirectionInterval = new(1f, 2f);

            [Tooltip("기울기 가속: 초당 (현재 균형값 × 이 값)만큼 바깥으로 민다. 많이 기울수록 빨리 넘어간다. 기획 0.5.")]
            [SerializeField, Min(0f)] float tiltAcceleration = 0.5f;

            public float GreenLimit => Mathf.Clamp(greenLimit, 0f, 100f);
            public float WarningLimit => Mathf.Clamp(warningLimit, GreenLimit, 100f);
            public float FallTime => fallTime;
            public float WarningFallRate => Mathf.Clamp01(warningFallRate);
            public float GreenRecoveryRate => Mathf.Clamp01(greenRecoveryRate);
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

            [Tooltip("줄에 매달린 외형의 추가 흔들림 최대 각도(도). 0이면 추가 흔들림 없음. 판정·버티기 시간에는 영향 없음.")]
            [SerializeField, Range(0f, 45f)] float ropeHangSwayAngle = 18f;

            [Tooltip("봉·그네·리프트에 매달린 외형의 추가 흔들림 최대 각도(도). 장치 자체 운동과 별개.")]
            [SerializeField, Range(0f, 45f)] float poleHangSwayAngle = 12f;

            public float RopeHangSwayAngle => Mathf.Clamp(ropeHangSwayAngle, 0f, 45f);
            public float PoleHangSwayAngle => Mathf.Clamp(poleHangSwayAngle, 0f, 45f);
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

        /// <summary>
        /// 묘기 재시작 때 잃은 부위를 어떻게 할지(프로토타입 시험용).
        /// 폐기 예정(2026-10-14): 기획상 묘기 재시작이 없고 사망자는 다음 시간대에 외팔 상태로 부활한다(injury-rules.md #4).
        /// 외줄 실패 후 재시작(<c>TightropeRun</c>)이 사라질 때(4단계) 함께 지운다.
        /// </summary>
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
            [Tooltip("[폐기 예정] 외줄 묘기 재시작 때 잃은 부위 처리(프로토타입 시험용). 기획상 재시작이 없어 외줄 재시작이 사라지면 지운다. 생존자 유지 = 살아 있던 사람은 그대로·사망자만 복구, 모두 복구 = 전원 온전한 몸.")]
            [SerializeField] BodyRestartRecovery restartRecovery = BodyRestartRecovery.KeepSurvivors;

            [Header("다리를 잃었을 때 점프 높이 (탐색·공연 공통)")]
            [Tooltip("다리 1개를 잃었을 때 점프 높이 배율(×). 캐릭터 점프 높이에 곱한다. 부상 기획 5-4(6), 수치 미정 — 임시값 0.75.")]
            [SerializeField, Range(0f, 1f)] float oneLegJumpHeight = 0.75f;

            [Tooltip("두 다리를 잃었을 때(기어가기) 점프 높이 배율(×). 기어갈 때 점프 허용은 아래 '기어갈 때 제자리 점프'. 임시값 0.5.")]
            [SerializeField, Range(0f, 1f)] float twoLegJumpHeight = 0.5f;

            [Header("탐색 중 부상 패널티 (대기실·백스테이지·탈출로 묘기 밖)")]
            [Tooltip("다리 1개를 잃었을 때 이동 속도 배율(×). 두 다리는 기어가기 배율을 쓴다. 수치는 대기실 담당 미정 — 임시값 0.8.")]
            [SerializeField, Range(0f, 1f)] float exploreOneLegMoveSpeed = 0.8f;

            [Tooltip("팔 1개를 잃었을 때 손 닿는 거리 배율(×). 수치는 대기실 담당 미정 — 임시값 0.8.")]
            [SerializeField, Range(0f, 1f)] float exploreOneArmReach = 0.8f;

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

            [Header("혈흔 연출 (판정과 무관, Task-090)")]
            [Tooltip("절단 순간 잘린 부위에서 혈흔이 튀는 연출을 게임 전체에서 쓸지. 끄면 모든 플레이어에게 보이지 않고 게임 설정 화면의 '혈흔 연출 표시' 항목도 사라진다.")]
            [SerializeField] bool bloodEffectEnabled = true;

            [Tooltip("혈흔 방울 양 배율(×). 파티클 프리팹(Resources/Effects/Blood/BloodSpurt)의 방출 수에 곱한다.")]
            [SerializeField, Range(0.1f, 3f)] float bloodAmount = 1f;

            [Tooltip("잘린 부위에서 피가 뿜어지는 시간(초). 처음 한 번 크게 튀고 이 시간 동안 약하게 이어진다.")]
            [SerializeField, Range(0.1f, 3f)] float bloodSpurtDuration = 0.8f;

            public BodyRestartRecovery RestartRecovery => restartRecovery;
            public float OneLegJumpHeight => oneLegJumpHeight;
            public float TwoLegJumpHeight => twoLegJumpHeight;
            public float ExploreOneLegMoveSpeed => exploreOneLegMoveSpeed;
            public float ExploreOneArmReach => exploreOneArmReach;

            /// <summary>잃은 다리 개수(0~2)에 맞는 점프 높이 배율.</summary>
            public float JumpHeightForLostLegs(int lostLegs) =>
                lostLegs <= 0 ? 1f : lostLegs == 1 ? oneLegJumpHeight : twoLegJumpHeight;
            public float CrawlSpeedMultiplier => crawlSpeedMultiplier;
            public bool CrawlCanJump => crawlCanJump;
            public bool CrawlCanLaneJump => crawlCanLaneJump;
            public float CrawlCapsuleHeight => crawlCapsuleHeight;
            public float LimbFlingSpeed => limbFlingSpeed;
            public bool BloodEffectEnabled => bloodEffectEnabled;
            public float BloodAmount => bloodAmount;
            public float BloodSpurtDuration => bloodSpurtDuration;
            public float LimbFlingUpSpeed => limbFlingUpSpeed;
            public float LimbSpin => limbSpin;
            public float LimbMass => limbMass;
            public float LimbLifetime => limbLifetime;
        }

        /// <summary>장소 전환(공간 씬 바꾸기). 씬 이름은 빌드 목록에 있어야 한다(접속 중 전환은 NGO가 빌드 목록으로 찾는다).</summary>
        [Serializable]
        public sealed class SpaceTransitionSettings
        {
            [Tooltip("공간 씬을 바꾸는 방식. 통째 교체 = 다음 공간 씬만 남김(기본). 추가 로드 = 시작 씬을 남기고 공간 씬만 붙였다 뗌. 다음 전환부터 적용된다.")]
            [SerializeField] SpaceTransitionMode mode = SpaceTransitionMode.Single;

            [Tooltip("시작 씬(접속·런 시작, 도입·엔딩 때 돌아오는 곳). 추가 로드에서는 계속 떠 있는 코어 씬.")]
            [SerializeField] string hubScene = "Lobby";

            [Tooltip("묘기별 씬이 지정되지 않은 공연 시간대의 공간 씬.")]
            [SerializeField] string performanceScene = "Tightrope";

            [Tooltip("줄타기 공연 씬. 비어 있으면 기본 공연 씬을 쓴다.")]
            [SerializeField] string tightropeScene = "Tightrope";

            [Tooltip("인간 대포 공연 씬. 비어 있으면 기본 공연 씬을 쓴다.")]
            [SerializeField] string humanCannonScene = "HumanCannon";

            [Tooltip("쉬는 시간 시작·밤 정비의 공간 씬(대기실 로비).")]
            [SerializeField] string waitingRoomScene = "WaitingRoomBase";

            [Tooltip("퇴장 시 같은 씬에서 탐색한다. 실제 대기실은 켜고, 별도 탐색 씬을 쓰는 더미 검증은 끈다.")]
            [SerializeField] bool keepWaitingRoomOnExit = true;

            [Tooltip("퇴장 시 같은 씬 유지가 꺼져 있을 때 이동할 공간 씬(백스테이지 탐색).")]
            [SerializeField] string explorationScene = "WaitingRoomBase";

            [Tooltip("탈출 개시 뒤의 공간 씬(탈출로).")]
            [SerializeField] string escapeRouteScene = "DummyEscapeRoute";

            public SpaceTransitionMode Mode => mode;
            public string HubScene => hubScene;
            public string PerformanceScene => performanceScene;
            public string TightropeScene => tightropeScene;
            public string HumanCannonScene => humanCannonScene;
            public string WaitingRoomScene => waitingRoomScene;
            public bool KeepWaitingRoomOnExit => keepWaitingRoomOnExit;
            public string ExplorationScene => explorationScene;
            public string EscapeRouteScene => escapeRouteScene;
        }

        /// <summary>
        /// 음성 채팅. Start 흐름의 확정 씬에서만. 생존자 = 거리 감쇠, 사망자(추락·리타이어) = 거리 무관. 결정: Harness/Project/Decisions/voice-chat.md
        /// 거리·감쇠 값은 채널에 들어갈 때 적용된다(플레이 중 바꾸면 다음 입장부터).
        /// </summary>
        [Serializable]
        public sealed class VoiceChatSettings
        {
            [Tooltip("음성 채팅 사용. 끄면 음성 로그인·채널 입장을 하지 않는다.")]
            [SerializeField] bool enabled = true;

            [Tooltip("LAN(직접 IP) 접속에서도 음성을 쓴다(기본 켬). Vivox 서버를 쓰므로 인터넷이 필요하다. 끄면 LAN 방에서는 음성 없음.")]
            [SerializeField] bool useOnLan = true;

            [Tooltip("음성을 쓰는 씬 이름. Start 씬부터 실행해 이어진 흐름에서, 지금 씬이 이 목록에 있을 때만 동작한다. 백스테이지·탈출로 씬이 확정되면 추가.")]
            [SerializeField] string[] scenes = { "Start", "Lobby", "WaitingRoomBase", "Tightrope", "HumanCannon" };

            [Tooltip("생존자: 이 거리(m) 안에서는 최대 음량. 정수.")]
            [SerializeField, Min(1)] int conversationalDistance = 2;

            [Tooltip("생존자: 이 거리(m) 밖은 들리지 않는다. 정수, 대화 거리보다 커야 한다.")]
            [SerializeField, Min(2)] int audibleDistance = 20;

            [Tooltip("생존자: 대화 거리~들리는 거리 사이에서 줄어드는 모양. 직선 = 일정하게 줄어 들리는 거리에서 0(기본). 반비례 = 처음에 빨리 줄고 멀수록 천천히. 지수 = 매우 빨리 줄어듦.")]
            [SerializeField] VoiceFadeModel fadeModel = VoiceFadeModel.Linear;

            [Tooltip("생존자: 줄어드는 세기(배). 1 = 보통, 2 = 두 배 빨리 작아짐, 0.5 = 절반.")]
            [SerializeField, Range(0.1f, 4f)] float fadeIntensity = 1f;

            [Tooltip("말하는/듣는 위치 높이(캐릭터 발에서 m).")]
            [SerializeField, Min(0f)] float mouthHeight = 1.55f;

            [Tooltip("내 마이크 끄기/켜기 키(pressed). 기획 미정 임시값 — 조작키 합의 필요(control-keys.md).")]
            [SerializeField] Key muteKey = Key.M;

            public bool Enabled => enabled;
            public bool UseOnLan => useOnLan;
            public IReadOnlyList<string> Scenes => scenes ?? Array.Empty<string>();
            public int ConversationalDistance => Mathf.Max(1, conversationalDistance);
            public int AudibleDistance => Mathf.Max(audibleDistance, ConversationalDistance + 1);
            public VoiceFadeModel FadeModel => fadeModel;
            public float FadeIntensity => fadeIntensity;
            public float MouthHeight => mouthHeight;
            public Key MuteKey => muteKey;
        }
    }

    /// <summary>생존자 음성이 거리에 따라 줄어드는 모양(Vivox AudioFadeModel과 같은 숫자).</summary>
    public enum VoiceFadeModel
    {
        /// <summary>반비례: 대화 거리를 벗어나면 빨리 줄고 멀수록 천천히.</summary>
        Inverse = 1,
        /// <summary>직선: 들리는 거리에서 0이 되도록 일정하게 줄어든다.</summary>
        Linear = 2,
        /// <summary>지수: 대화 거리를 벗어나면 매우 빨리 줄어든다.</summary>
        Exponential = 3,
    }
}
