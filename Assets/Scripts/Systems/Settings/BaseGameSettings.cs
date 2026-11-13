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
        [Header("공통 운반 장치")]
        [SerializeField] CarryDeviceSettings carryDevices = new();
        [Header("캐릭터")]
        [SerializeField] CharacterSettings character = new();
        [Header("입력")]
        [SerializeField] InputSettings input = new();
        [Header("공통 상호작용")]
        [SerializeField] InteractionSettings interaction = new();
        [Header("공통 공중 조절")]
        [SerializeField] AirControlSettings airControl = new();
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
        public CarryDeviceSettings CarryDevices => carryDevices;
        public CharacterSettings Character => character;
        public InputSettings Input => input;
        public InteractionSettings Interaction => interaction;
        public AirControlSettings AirControl => airControl;
        public BalanceSettings Balance => balance;
        public HudLayoutSettings HudLayout => hudLayout;
        public PresentationSettings Presentation => presentation;
        public SyncSettings Sync => sync;
        public BodyDamageSettings BodyDamage => bodyDamage;
        public SpaceTransitionSettings SpaceTransition => spaceTransition;
        public VoiceChatSettings VoiceChat => voiceChat;

        void OnValidate() => balance.Validate();

        [Serializable]
        public sealed class CarryDeviceSettings
        {
            [Tooltip("휴대 톱날 발사기 외형과 충돌체의 공통 크기 배율. 기본 0.7(원본의 70%). 손잡이 위치도 함께 줄어듭니다.")]
            [SerializeField, Range(.3f, 1.5f)] float sawScale = .7f;
            [Tooltip("휴대 톱날 발사기를 들고 움직이는 기본 속도(m/s). 부상 배율은 별도 적용됩니다.")]
            [SerializeField, Min(0)] float sawCarrySpeed = 3f;
            [Tooltip("사용 키를 한 번 누르면 톱날 한 발을 발사합니다. 다음 발사까지 필요한 시간(초). 계속 누르고 있어도 연속 발사하지 않습니다.")]
            [SerializeField, Min(.1f)] float sawShotInterval = 1.15f;
            [Tooltip("켜면 톱날 발사대를 들고 있는 팀원 또는 그 팀원이 포함된 목마에 새로 올라탈 수 없습니다. 모든 씬의 목마 대상 선택과 호스트 승인에 공통 적용됩니다.")]
            [SerializeField] bool blockPiggybackOnSawCarrier = true;
            [Tooltip("운반 장치의 충돌 검사에서 접촉 떨림을 줄이는 여유(m). 트램펄린과 톱날 발사기에 공통 적용합니다.")]
            [SerializeField, Range(.001f, .05f)] float collisionSkin = .02f;
            public float SawScale => Mathf.Clamp(sawScale, .3f, 1.5f);
            public float SawCarrySpeed => Mathf.Max(0, sawCarrySpeed);
            public float SawShotInterval => Mathf.Max(.1f, sawShotInterval);
            public bool BlockPiggybackOnSawCarrier => blockPiggybackOnSawCarrier;
            public float CollisionSkin => Mathf.Clamp(collisionSkin, .001f, .05f);
        }

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

                [Tooltip("이 시간대에 탐색 통로의 문을 잠그고 탐색 구역 전원을 차단합니다. 플레이 중 변경도 즉시 반영됩니다.")]
                [SerializeField, InspectorName("탐색 잠금")] bool explorationLocked;

                public RestTimeLimitSettings() { }
                public RestTimeLimitSettings(bool unlimited, float seconds = 300f)
                { this.unlimited = unlimited; this.seconds = seconds; }

                public bool Unlimited => unlimited;
                public bool ExplorationLocked => explorationLocked;
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
                    nightMaintenance = new RestTimeLimitSettings(false);
                    timeLimitsMigrated = true;
                }
                morningRest ??= new RestTimeLimitSettings(false, restDuration);
                noonRest ??= new RestTimeLimitSettings(false, restDuration);
                guideRest ??= new RestTimeLimitSettings(false, guideRestDuration);
                nightMaintenance ??= new RestTimeLimitSettings(false);
            }

            public void OnBeforeSerialize() => EnsureTimeLimits();
            public void OnAfterDeserialize() => EnsureTimeLimits();

            [Tooltip("소집 경보 시점(초). 쉬는 시간이 이만큼 남으면 경보를 울린다. 기획 1분 = 60.")]
            [SerializeField, Min(0f)] float recallAlarmLeadTime = 60f;

            [Tooltip("공연 실패 확정 뒤 묘기 화면을 비추는 시간(초). 이 시간이 지나야 다음 시간대·공간으로 넘어간다. 0이면 바로 넘어간다. 2026-10-19 PM 2초.")]
            [SerializeField, Min(0f)] float failedPerformanceHold = 2f;

            [Tooltip("공연(외줄·인간대포 전 난이도) 첫 생존 도착 뒤 정산까지 기다리는 여유 시간(초). 그동안 상단에 남은 초를 알리고, 도착한 생존자를 모두 완주로 정산한다. 0이면 바로 정산. 2026-11-13 사용자 5초.")]
            [SerializeField, Min(0f)] float finishGraceSeconds = 5f;

            [Tooltip("공연 중 생존자가 1명만 남았을 때 그 플레이어에게 띄우는 공연 포기 선택의 대기 시간(초). 선택이 없으면 계속하기로 처리한다. 2026-11-13 사용자 5초.")]
            [SerializeField, Min(1f)] float forfeitPromptSeconds = 5f;

            public int DayCount => dayCount;
            public int PerformancesPerDay => performancesPerDay;
            public RestTimeLimitSettings MorningRest { get { EnsureTimeLimits(); return morningRest; } }
            public RestTimeLimitSettings NoonRest { get { EnsureTimeLimits(); return noonRest; } }
            public RestTimeLimitSettings GuideRest { get { EnsureTimeLimits(); return guideRest; } }
            public RestTimeLimitSettings NightMaintenance { get { EnsureTimeLimits(); return nightMaintenance; } }
            public float RestDuration => NoonRest.Seconds;
            public float GuideRestDuration => GuideRest.Seconds;

            public bool IsExplorationLocked(CurtainCall.Campaign.CampaignSnapshot state) => state.Phase switch
            {
                CurtainCall.Campaign.CampaignPhase.Night => NightMaintenance.ExplorationLocked,
                CurtainCall.Campaign.CampaignPhase.RestTime => TimeLimit(state.Rest).ExplorationLocked,
                _ => false,
            };

            public RestTimeLimitSettings TimeLimit(CurtainCall.Campaign.RestKind rest) => rest switch
            {
                CurtainCall.Campaign.RestKind.Morning => MorningRest,
                CurtainCall.Campaign.RestKind.Guide => GuideRest,
                _ => NoonRest,
            };
            public float RecallAlarmLeadTime => recallAlarmLeadTime;
            public float FailedPerformanceHold => failedPerformanceHold;
            public float FinishGraceSeconds => float.IsFinite(finishGraceSeconds) ? Mathf.Max(0f, finishGraceSeconds) : 5f;
            public float ForfeitPromptSeconds => float.IsFinite(forfeitPromptSeconds) ? Mathf.Max(1f, forfeitPromptSeconds) : 5f;

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
            public float JumpHeight => jumpHeight;
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

        [Serializable]
        public sealed class InteractionSettings
        {
            [SerializeField, Min(.1f), Tooltip("일반 물체 잡기·탑승·버튼·도구 접촉의 공통 거리(m). 플레이어 몸 중심과 대상의 실제 상호작용 지점 사이 3차원 거리이며 벽/층으로 가려지면 닿지 않는다. 팔 부상 배율은 별도 적용. 기본 2.5m.")]
            float distance = 2.5f;
            [InspectorName("구조 대상 먼저 선택"), Tooltip("F를 처음 누를 때 구조할 동료를 일반 물건보다 먼저 선택한다. 끄면 구조 대상도 일반 물건과 함께 가까운 순서로 선택한다. 이미 잡거나 사용 중인 대상은 항상 우선하며 F 한 번에는 하나만 반응한다. 기본 켬.")]
            [SerializeField] bool prioritizeRescue = true;
            public float Distance => Mathf.Max(.1f, distance);
            public bool PrioritizeRescue => prioritizeRescue;
        }

        [Serializable]
        public sealed class AirControlSettings
        {
            [SerializeField, Min(0f), Tooltip("일반 점프·낙하의 수평 이동 속도 및 봉·대포·트램펄린 비행 중 입력으로 더하는 수평 조절 속도(m/s). 공중에는 Shift 달리기 배율을 적용하지 않으며, 기물 발사 속도와는 별개다.")]
            float speed = 4f;
            [Tooltip("봉·대포·트램펄린 비행의 수평 조절이 입력 방향을 따라가는 반응 시간(초). 작을수록 빠르게 방향을 바꾼다. 기본 0.1초.")]
            [SerializeField, Min(.001f)] float responseSeconds = .1f;
            [SerializeField, Min(0f), Tooltip("봉 도약 직후 반동을 주던 W/S가 공중 조절로 겹치지 않는 유예.")]
            float pumpReleaseGrace = .15f;
            public float Speed => Mathf.Max(0f, speed);
            public float ResponseSeconds => Mathf.Max(.001f, responseSeconds);
            public float PumpReleaseGrace => Mathf.Max(0f, pumpReleaseGrace);
        }

        /// <summary>입력 판정.</summary>
        [Serializable]
        public sealed class InputSettings
        {
            [Tooltip("착지 직전 점프 입력을 받아두는 공통 시간(초). 일반 바닥과 외줄에 동일하게 적용한다.")]
            [SerializeField, Min(0f)] float jumpInputBuffer = .15f;
            public float JumpInputBuffer => Mathf.Max(0f, jumpInputBuffer);
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
            [Header("이동 애니메이션")]
            [InspectorName("걷기 모션 기준 속도"), Tooltip("걷기 애니메이션을 1배속으로 재생할 실제 이동 속도(m/s). 실제 이동이 느려지면 발동작도 함께 느려진다. 게임 이동 속도는 바꾸지 않는다.")]
            [SerializeField, Min(.01f)] float walkAnimationSpeed = 3f;
            [InspectorName("달리기 모션 기준 속도"), Tooltip("달리기 애니메이션의 1배속 기준(m/s). 이동·부상 설정 변경에 맞춰 발동작 속도만 조정한다.")]
            [SerializeField, Min(.01f)] float runAnimationSpeed = 6f;
            [InspectorName("앉아 걷기 모션 기준 속도"), Tooltip("앉아 걷기 애니메이션의 1배속 기준(m/s). 충돌 높이와 이동 속도에는 영향이 없다.")]
            [SerializeField, Min(.01f)] float crouchAnimationSpeed = 1.5f;
            [InspectorName("외줄 걷기 모션 기준 속도"), Tooltip("외줄 걷기 애니메이션의 1배속 기준(m/s). 뒤로 이동하면 발동작도 뒤로 재생한다. 줄 이동·균형 규칙은 바꾸지 않는다.")]
            [SerializeField, Min(.01f)] float ropeAnimationSpeed = .5f;
            [InspectorName("기어가기 모션 기준 속도"), Tooltip("부상 후 기어가기 애니메이션의 1배속 기준(m/s). 남은 팔의 동작 속도를 실제 이동량에 맞춘다.")]
            [SerializeField, Min(.01f)] float crawlAnimationSpeed = 1.5f;
            public float LocomotionAnimationSpeed(string motion) => Mathf.Max(.01f, motion switch
            {
                "Run" => runAnimationSpeed, "CrouchWalk" => crouchAnimationSpeed,
                "Tightrope" => ropeAnimationSpeed, "Crawl" => crawlAnimationSpeed,
                _ when motion != null && motion.StartsWith("ItemCrawl") => crawlAnimationSpeed,
                _ => walkAnimationSpeed,
            });

            [Serializable]
            public sealed class InteractionAnimationPolicy
            {
                [Tooltip("애니메이션 중 이동과 점프 허용")]
                [SerializeField] bool allowMovement;
                [Tooltip("재생 중 다음 상호작용을 즉시 허용")]
                [SerializeField] bool allowInterrupt;
                public bool AllowMovement => allowMovement;
                public bool AllowInterrupt => allowInterrupt;
            }

            [Header("상호작용 애니메이션")]
            [SerializeField] InteractionAnimationPolicy strike = new();
            [SerializeField] InteractionAnimationPolicy manipulate = new();
            [SerializeField] InteractionAnimationPolicy pull = new();
            [SerializeField] InteractionAnimationPolicy key = new();
            [Tooltip("바닥 또는 배송구의 기물 줍기")]
            [SerializeField] InteractionAnimationPolicy pickup = new();
            public InteractionAnimationPolicy InteractionPolicy(int kind) => kind switch
            {
                0 => strike, 1 => manipulate, 2 => pull, 3 => key, 4 => pickup, _ => manipulate
            };
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
            [Header("공통 위험물·낙하")]
            [InspectorName("위험물 피해 간격"), Tooltip("한 플레이어가 일반 위험물로 다시 다칠 수 있는 최소 간격(초). 서로 다른 톱·일반 화염도 이 간격을 공유한다. 즉사형 위험물에는 적용하지 않는다. 기본 1초.")]
            [SerializeField, Min(0f)] float hazardHitCooldown = 1f;
            [InspectorName("일반 위험물 절단 개수"), Tooltip("톱·일반 화염 등에 한 번 다칠 때 잃는 사지 수(개, 1~4). 정해진 절단 순서대로 적용하며 남은 사지를 모두 잃으면 사망한다. 즉사형 화염은 별도다. 기본 1개.")]
            [SerializeField, Range(1, 4)] int hazardCutCount = 1;
            [InspectorName("부상 낙하 높이"), Tooltip("낙하 최고점에서 착지한 발 위치까지의 높이 차이가 이 값 이상이면 낙하 부상(m). 일반 바닥·외줄에서 바닥으로 추락·대포 착지에 공통 적용. 신발 반동이 먼저 성공하면 피해를 막는다. 기본 5m. 사망 낙하 높이 미만에서 사지 손실을 적용한다.")]
            [SerializeField, Min(0f)] float fallInjuryHeight = 5f;
            [InspectorName("낙하 부상 절단 개수"), Tooltip("부상 높이 이상·사망 높이 미만으로 착지할 때 잃는 사지 수(개, 1~4). 이미 다친 상태에서는 남은 사지를 모두 잃어 사망할 수 있다. 기본 2개.")]
            [SerializeField, Range(1, 4)] int fallInjuryCutCount = 2;
            [InspectorName("사망 낙하 높이"), Tooltip("이 높이 이상 떨어져 착지하면 사망(m). 실제 적용값은 부상 낙하 높이보다 낮아지지 않는다. 신발이나 트램펄린 반동이 먼저 성공하면 피해를 막는다. 기본 15m. 월드 Y 좌표가 아니라 낙하 최고점과 착지점의 높이 차이다.")]
            [SerializeField, Min(0f)] float fallDeathHeight = 15f;
            [InspectorName("월드 Y 즉사 기준"), Tooltip("플레이어 발 위치의 절대 월드 Y 좌표가 이 값 이하이면 착지를 기다리지 않고 사망 판정한다(m). 기본 -15. 낙하거리 기준(부상 5m·사망 15m)과 별개이며, 외줄·안전 착지대·반동 장비의 착지 보호로 막지 않는다. 사망 후 튜토리얼 복귀 등 기존 공통 복구 정책은 유지한다.")]
            [SerializeField] float worldDeathY = -15f;
            [SerializeField, InspectorName("외줄 착지 낙하 피해 면제")]
            [Tooltip("실제로 외줄 위에 정상 착지하면 높이에 관계없이 낙하 부상·사망 없이 줄을 탄다. 모든 씬 공통. 줄에서 떨어져 아래 바닥에 착지한 경우에는 일반 낙하 높이 기준을 적용한다. 기본 켬.")]
            bool safeRopeLanding = true;
            [SerializeField, InspectorName("안전 착지대 낙하 피해 면제")]
            [Tooltip("안전 착지대(HumanCannonLandingZone)의 실제 윗면에 착지하면 높이와 관계없이 낙하 피해를 받지 않는다. 대포·봉·일반 낙하 모두 공통이며 기존 부상을 회복하지 않는다. 일반 바닥·착지대 밖·비활성 착지대와 직접 위험물은 보호하지 않는다. 기본 켬.")]
            bool safeLandingZones = true;
            public float HazardHitCooldown => Mathf.Max(0f, hazardHitCooldown);
            public int HazardCutCount => Mathf.Clamp(hazardCutCount, 1, 4);
            public int FallInjuryCutCount => Mathf.Clamp(fallInjuryCutCount, 1, 4);
            public float FallInjuryHeight => Mathf.Max(0f, fallInjuryHeight);
            public float FallDeathHeight => Mathf.Max(FallInjuryHeight, fallDeathHeight);
            public float WorldDeathY => float.IsFinite(worldDeathY) ? worldDeathY : -15f;
            public bool SafeRopeLanding => safeRopeLanding;
            public bool SafeLandingZones => safeLandingZones;

            [Tooltip("[폐기 예정] 외줄 묘기 재시작 때 잃은 부위 처리(프로토타입 시험용). 기획상 재시작이 없어 외줄 재시작이 사라지면 지운다. 생존자 유지 = 살아 있던 사람은 그대로·사망자만 복구, 모두 복구 = 전원 온전한 몸.")]
            [SerializeField] BodyRestartRecovery restartRecovery = BodyRestartRecovery.KeepSurvivors;

            [Header("모든 씬 공통 부상 이동·매달리기")]
            [Tooltip("다리 하나를 잃었을 때 이동 속도 배율. 기본 0.9.")]
            [SerializeField, Range(0f, 1f)] float exploreOneLegMoveSpeed = .9f;

            [Tooltip("팔을 잃은 플레이어의 매달림 시간 배율. 기본 0.5 (봉 30초 → 15초).")]
            [SerializeField, Range(.1f, 1f)] float injuredHangTimeMultiplier = .5f;

            [Header("두 다리를 잃었을 때 (기어가기)")]
            [Tooltip("기어갈 때 이동 속도 배율. 기본 0.65.")]
            [SerializeField, Range(0f, 1f)] float crawlSpeedMultiplier = .65f;

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
            [Tooltip("절단 순간 잘린 부위에서 혈흔이 튀는 연출과 부상에 따른 피 묻은 의상을 게임 전체에서 쓸지. 끄면 의상은 처음 상태를 유지하고, 모든 플레이어에게 보이지 않고 게임 설정 화면의 '혈흔 연출 표시' 항목도 사라진다.")]
            [SerializeField] bool bloodEffectEnabled = true;

            [Tooltip("혈흔 방울 양 배율(×). 파티클 프리팹(Resources/Effects/Blood/BloodSpurt)의 방출 수에 곱한다.")]
            [SerializeField, Range(0.1f, 3f)] float bloodAmount = 1f;

            [Tooltip("잘린 부위에서 피가 뿜어지는 시간(초). 처음 한 번 크게 튀고 이 시간 동안 약하게 이어진다.")]
            [SerializeField, Range(0.1f, 3f)] float bloodSpurtDuration = 0.8f;

            public BodyRestartRecovery RestartRecovery => restartRecovery;
            public float ExploreOneLegMoveSpeed => exploreOneLegMoveSpeed;
            public float MoveMultiplier(int lostLegs) => lostLegs <= 0 ? 1f : lostLegs == 1 ? exploreOneLegMoveSpeed : crawlSpeedMultiplier;
            public float ReachMultiplier(int lostArms) => 1f;
            public float JumpHeightForLostLegs(int lostLegs) => 1f;
            public float HangSeconds(float baseSeconds, int lostArms) =>
                lostArms <= 0 ? baseSeconds : baseSeconds * Mathf.Clamp(injuredHangTimeMultiplier, .1f, 1f);
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

            [Tooltip("공연 출발점 배치 확인이 늦어질 때 추락 사망을 보류할 최대 시간(초). 배치가 확인되면 즉시 보호가 해제된다.")]
            [SerializeField, Min(0f)] float performancePlacementGraceSeconds = 8f;


            [Tooltip("실제 플레이·Dev 시작·탐색 대기실. 변경 후 런을 다시 시작하세요.")]
            [SerializeField] WaitingRoomSceneVariant waitingRoomVariant = WaitingRoomSceneVariant.Integrated;
            [HideInInspector, SerializeField] string waitingRoomScene = "WaitingRoomBase";

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
            public float PerformancePlacementGraceSeconds => Mathf.Max(0f, performancePlacementGraceSeconds);
            public WaitingRoomSceneVariant WaitingRoomVariant => waitingRoomVariant;
            public string WaitingRoomScene => WaitingRoomScenes.Resolve(waitingRoomScene, waitingRoomVariant);
            public bool KeepWaitingRoomOnExit => keepWaitingRoomOnExit;
            public string ExplorationScene => WaitingRoomScenes.Resolve(explorationScene, waitingRoomVariant);
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
