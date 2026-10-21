using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>
    /// 외줄 묘기 세팅: 코스·진행·줄 위 동작·손잡기·목마·카메라·신체 손상 불이익·톱날(+ 보상 연결은 부모).
    /// 에셋: Assets/Resources/Settings/GameSettings/Tricks/TightropeSettings.asset. <see cref="GameSettings.Tightrope"/>로 읽는다.
    /// 코스 모양은 코스를 만들 때 읽는다(대기 중 수정은 바로 다시 만들고, 게임 중 수정은 다음 대기 때). 불은 회차 시작에 호스트가 복사하고, 나머지는 쓸 때마다 읽는다.
    /// 톱날 칸은 012(`SawSettings`, 장애물 순서 포함), 불 칸은 014(`FireSettings`, 2026-10-05 승인된 초기 실험값).
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Tricks/Tightrope", fileName = "TightropeSettings")]
    public sealed class TightropeSettings : TrickSettings
    {
        [Header("코스 (만들 때 적용)")]
        [SerializeField] CourseSettings course = new();
        [Header("진행")]
        [SerializeField] RunSettings run = new();
        [Header("줄 위 동작")]
        [SerializeField] RopeMovementSettings ropeMovement = new();
        [Header("손잡기")]
        [SerializeField] HandholdSettings handhold = new();
        [Header("목마")]
        [SerializeField] PiggybackSettings piggyback = new();
        [Header("카메라")]
        [SerializeField] CameraSettings camera = new();
        [Header("신체 손상 불이익 (외줄)")]
        [SerializeField] BodyPenaltySettings bodyPenalty = new();
        [Header("톱날 (장애물 순서·가로·수직·발판·부위 판정)")]
        [SerializeField] Tightrope.Saws.SawSettings saws = new();
        [Header("불 (회차 시작에 적용·플레이 중 변경은 다음 회차)")]
        [SerializeField] Tightrope.Fire.FireSettings fire = new();

        /// <summary>인스펙터에서 값을 바꿨을 때(에디터). 코스가 다시 만들지 판단하는 데 쓴다.</summary>
        public event Action Changed;

        public CourseSettings Course => course;
        public RunSettings Run => run;
        public RopeMovementSettings RopeMovement => ropeMovement;
        public HandholdSettings Handhold => handhold;
        public PiggybackSettings Piggyback => piggyback;
        public CameraSettings Camera => camera;
        public BodyPenaltySettings BodyPenalty => bodyPenalty;
        public Tightrope.Saws.SawSettings Saws => saws;
        public Tightrope.Fire.FireSettings Fire => fire;

        void OnValidate() => Changed?.Invoke();

        /// <summary>
        /// 코스 모양. 온라인에서는 호스트 값을 그대로 복사해 쓰므로(코스 값 공유) JSON으로 주고받을 수 있게 칸만 둔다.
        /// </summary>
        [Serializable]
        public sealed class CourseSettings
        {
            [Header("줄")]
            [Tooltip("줄 수(개). 기획 4.")]
            [SerializeField, Min(1)] int laneCount = 4;

            [Tooltip("줄 중심 간격(m). 옆줄 점프 거리도 이 값이 된다. 기획 3.")]
            [SerializeField, Min(0.1f)] float laneSpacing = 3f;

            [Tooltip("줄마다 끝나는 거리(m, 왼쪽 줄부터). 비우거나 0 이하·도착 거리 이상이면 도착까지 간다. 기획: 모든 줄이 도착까지.")]
            [SerializeField] float[] laneEndDistances = Array.Empty<float>();

            [Tooltip("코스 길이 = 도착 판정선(m, 외줄 시작점에서). 기획 100.")]
            [SerializeField, Min(1f)] float finishDistance = 100f;

            [Tooltip("옆줄 이동 가능 구간(m). x = 시작, y = 끝. 구간 밖에서는 옆줄 이동을 막는다. 2026-10-03 PM: 0~100 전 구간.")]
            [SerializeField] Vector2[] laneChangeZones = { new(0f, 100f) };

            [Header("모양")]
            [Tooltip("기획 초기 테스트: 중간 줄을 끊고 회전봉으로 건너는 구간을 만든다. 짧은 테스트 코스에는 적용하지 않는다.")]
            [SerializeField] bool swingGapEnabled = true;
            [Tooltip("끊긴 구간을 해제봉 교대 관문으로 사용한다. 켜면 모든 줄이 끊기며 출발·도착 봉으로 연결한다.")]
            [SerializeField] bool relayGapEnabled = true;
            [Tooltip("양쪽에서 통로를 유지하는 데 필요한 봉 인원. 첫 관문 1, 심화 관문 2.")]
            [SerializeField, Range(1, 2)] int relayRequiredHolders = 1;
            [Tooltip("해제봉 최대 유지 시간(초). 첫 교대 관문 초기값 30. 놓기 예고는 인간대포 봉의 값을 사용한다.")]
            [SerializeField, Range(8f, 120f)] float relayHoldSeconds = 30f;
            [Tooltip("줄을 끊고 개별 봉을 둘 줄 번호(1부터). 나머지 줄은 이어진다. 초기 배치: 2·5번 줄.")]
            [SerializeField] int[] swingGapLanes = { 2, 5 };
            [Tooltip("끊어진 구간 시작 거리(m). 초기 테스트 128.")]
            [SerializeField, Min(10f)] float swingGapStart = 128f;
            [Tooltip("끊어진 줄 길이(m). 초기 테스트 8.")]
            [SerializeField, Range(4f, 12f)] float swingGapLength = 8f;

            [Header("공중 2인 봉 교대 구간")]
            [SerializeField] bool trapezeEnabled = true;
            [SerializeField, Min(10f)] float trapezeStart = 110f;
            [SerializeField, Range(8f, 24f)] float trapezeLength = 16f;
            [Tooltip("공중 봉 세 개를 배치할 줄 번호(1부터). 구간의 모든 줄을 끊어 봉으로 건너도록 한다.")]
            [SerializeField, Min(1)] int trapezeLane = 3;

            [Tooltip("시작(대기) 플랫폼 길이(m). 기획 6.")]
            [SerializeField, Min(0.5f)] float startPlatformLength = 6f;

            [Tooltip("도착(세리머니) 플랫폼 길이(m). 기획 6.")]
            [SerializeField, Min(0f)] float finishPlatformLength = 6f;

            [Tooltip("양쪽 끝 줄 중심에서 맵 경계까지 여유(m). 기획 2(유효 폭 13m).")]
            [SerializeField, Min(0f)] float sideMargin = 2f;

            [Tooltip("플랫폼 두께(m). 윗면이 줄 높이와 같다.")]
            [SerializeField, Min(0.05f)] float platformThickness = 0.5f;

            [Tooltip("줄 굵기(m, 보이는 지름). 기획 0.2.")]
            [SerializeField, Min(0.01f)] float ropeThickness = 0.2f;

            [Tooltip("줄 위를 걸을 수 있는 폭(m, 충돌 폭). 보이는 굵기보다 넓어야 캐릭터가 서 있는다. 0.4.")]
            [SerializeField, Min(0.01f)] float ropeWalkWidth = 0.4f;

            [Tooltip("발 높이가 줄 윗면에서 이 거리(m) 안이면 줄 위로 본다. 0.3.")]
            [SerializeField, Min(0.01f)] float ropeHeightTolerance = 0.3f;

            [Tooltip("출발 위치(m, 외줄 시작점 기준). 음수면 시작 플랫폼 위. -1.")]
            [SerializeField] float spawnDistance = -1f;

            [Header("추락")]
            [Tooltip("코스 아래 안전망 깊이(m). 떨어진 캐릭터를 받는다. 0이면 만들지 않는다. 6.")]
            [SerializeField, Min(0f)] float safetyNetDepth = 6f;

            [Tooltip("캐릭터가 줄 높이보다 이만큼(m) 내려가면 추락 처리. 기획 값이 없어 테스트 값 1.")]
            [SerializeField, Min(0.1f)] float fallDepth = 1f;

            [Tooltip("진행 구간의 끝 비율·이름·줄 색. 충돌면은 색 경계에서 끊기지 않는다.")]
            [SerializeField] CourseSection[] sections =
            {
                new(.22727273f, "균형과 점프", new Color(.18f, .52f, 1f)),
                new(.40909091f, "줄바꿈", new Color(.12f, .82f, .7f)),
                new(.59090909f, "첫 협동 관문", new Color(.65f, .4f, 1f)),
                new(.77272727f, "동료와 합류", new Color(.95f, .38f, .68f)),
                new(.95454545f, "마지막 줄 선택", new Color(.65f, .85f, .25f)),
                new(1f, "한 줄 피날레", new Color(.95f, .95f, 1f))
            };

            public int LaneCount => laneCount;
            public float LaneSpacing => laneSpacing;
            public float[] LaneEndDistances => laneEndDistances;
            public float FinishDistance => finishDistance;
            public Vector2[] LaneChangeZones => laneChangeZones;
            public float StartPlatformLength => startPlatformLength;
            public float FinishPlatformLength => finishPlatformLength;
            public float SideMargin => sideMargin;
            public float PlatformThickness => platformThickness;
            public float RopeThickness => ropeThickness;
            public float RopeWalkWidth => ropeWalkWidth;
            public float RopeHeightTolerance => ropeHeightTolerance;
            public float SpawnDistance => spawnDistance;
            public float SafetyNetDepth => safetyNetDepth;
            public float FallDepth => fallDepth;
            public CourseSection[] Sections => sections;
            public bool SwingGapEnabled => swingGapEnabled;
            public bool RelayGapEnabled => relayGapEnabled;
            public int RelayRequiredHolders => Mathf.Clamp(relayRequiredHolders, 1, 2);
            public float RelayHoldSeconds => Mathf.Clamp(relayHoldSeconds, 8f, 120f);
            public bool TrapezeEnabled => trapezeEnabled;
            public float TrapezeStart => trapezeStart;
            public float TrapezeLength => Mathf.Clamp(trapezeLength, 8f, 24f);
            public int TrapezeLane => Mathf.Clamp(trapezeLane, 1, LaneCount);
            public int[] SwingGapLanes => swingGapLanes;
            public bool HasSwingGapLane(int lane) => lane >= 0 && lane < laneCount
                && swingGapLanes != null && Array.IndexOf(swingGapLanes, lane + 1) >= 0;
            public float SwingGapStart => swingGapStart;
            public float SwingGapLength => Mathf.Clamp(swingGapLength, 4f, 12f);

            /// <summary>값을 JSON으로(코스 값 공유용).</summary>
            public string ToJson() => JsonUtility.ToJson(this);

            /// <summary>JSON에서 만든다. 실패하면 null.</summary>
            public static CourseSettings FromJson(string json)
            {
                if (string.IsNullOrEmpty(json)) return null;
                try { return JsonUtility.FromJson<CourseSettings>(json); }
                catch (ArgumentException) { return null; }
            }

            /// <summary>지금 값을 복사한다. 코스가 만들 때의 값을 붙잡아 두는 데 쓴다.</summary>
            public CourseSettings Clone() => FromJson(ToJson());
        }

        [Serializable]
        public struct CourseSection
        {
            [Range(0f, 1f)] public float endFraction;
            public string label;
            public Color color;
            public CourseSection(float endFraction, string label, Color color)
            { this.endFraction = endFraction; this.label = label; this.color = color; }
        }

        /// <summary>묘기 진행(호스트 판정).</summary>
        [Serializable]
        public sealed class RunSettings
        {
            [Tooltip("제한시간(초). 게임 시작부터 잰다. 기획 300(5분).")]
            [SerializeField, Min(1f)] float timeLimit = 300f;

            [Tooltip("실패 후 묘기 재시작까지 기다리는 시간(초). 기획 값이 없어 테스트 값 3.")]
            [SerializeField, Min(0f)] float restartDelay = 3f;

            public float TimeLimit => timeLimit;
            public float RestartDelay => restartDelay;
        }

        /// <summary>균형·착지 조정값. 인간대포 공통 이동·중력을 사용하며 점프 높이만 외줄 배율을 적용한다.</summary>
        [Serializable]
        public sealed class RopeMovementSettings
        {
            [Tooltip("공통 점프 높이에 곱하는 외줄 높이 배율. 2026-10-19 사용자 요청: 0.8m → 1.0m.")]
            [SerializeField, Min(1f)] float jumpHeightMultiplier = 1.25f;
            [Tooltip("착지 직전 점프 입력을 기억하는 시간(초). 0이면 입력 여유를 끈다. 옆줄 방향은 누른 순간 기준.")]
            [SerializeField, Range(0f, 0.3f)] float jumpInputBuffer = 0.15f;

            [Tooltip("외줄의 자연 흔들림 배율(×). 걷기·제자리 흔들림에 적용하고 부상·목마·옆줄 착지 배율과 곱한다. A/D 보정 힘과 추락 기준은 바꾸지 않는다.")]
            [SerializeField, Min(0f)] float naturalSwayMultiplier = 1.5f;

            [Header("동적인 줄 흔들림 (초기 조정값)")]
            [Tooltip("서 있을 때에도 강약과 방향이 변하는 추가 흔들림(초당). 0이면 끈다.")]
            [SerializeField, Min(0f)] float dynamicIdleSway = 6f;
            [Tooltip("걷는 동안 추가 흔들림(초당). A/D 보정과 별도로 실제 균형에 반영한다.")]
            [SerializeField, Min(0f)] float dynamicMovingSway = 12f;
            [Tooltip("큰 흔들림의 초당 왕복 횟수. 더 빠르고 작은 흔들림도 함께 겹친다.")]
            [SerializeField, Range(.1f, 2f)] float dynamicSwayFrequency = .55f;
            [Tooltip("동일한 연결 줄 위 동료의 하중이 전달되는 앞뒤 거리(m). 가까울수록 강하다.")]
            [SerializeField, Min(.1f)] float sharedSwayRange = 12f;
            [Tooltip("가까운 동료 한 명당 자연·동적 흔들림에 더하는 배율. 총 증가는 최대 75%.")]
            [SerializeField, Range(0f, .75f)] float sharedSwayPerPlayer = .25f;

            public float DynamicIdleSway => Mathf.Max(0f, dynamicIdleSway);
            public float DynamicMovingSway => Mathf.Max(0f, dynamicMovingSway);
            public float DynamicSwayFrequency => Mathf.Clamp(dynamicSwayFrequency, .1f, 2f);
            public float SharedSwayRange => Mathf.Max(.1f, sharedSwayRange);
            public float SharedSwayPerPlayer => Mathf.Clamp(sharedSwayPerPlayer, 0f, .75f);

            [Tooltip("착지 충격. 현재 기울어진 쪽으로 밀며 목마 배율을 곱한다. 추락 위험 시간은 직접 더하지 않는다. 기획 10.")]
            [SerializeField, Min(0f)] float landingShock = 10f;

            [Tooltip("이 시간(초)보다 짧게 떠 있었으면 착지 충격을 주지 않는다(작은 턱 무시). 0.15.")]
            [SerializeField, Min(0f)] float minAirTimeForShock = 0.15f;

            [Tooltip("옆줄 점프 착지 충격. 현재 기울어진 쪽으로 밀며 중앙에서는 점프 방향을 쓴다. 기획 35.")]
            [SerializeField, Min(0f)] float laneLandingShock = 35f;

            [Tooltip("옆줄에 착지할 때 더하는 추락 게이지 비율(0~1). 0.05면 전체 바의 5%. 충격 완충·목마 배율과 무관하게 적용한다.")]
            [SerializeField, Range(0f, 1f)] float laneLandingDangerRatio = .05f;

            [Tooltip("옆줄 점프 착지 후 자연 흔들림 배율(×). 고도화 초기값 1.5(이전 1.75).")]
            [SerializeField, Min(1f)] float laneSwayBoost = 1.5f;

            [Tooltip("옆줄 점프 착지 후 흔들림이 커지는 시간(초). 반복해도 쌓이지 않고 시간만 갱신. 고도화 초기값 1.5(이전 3).")]
            [SerializeField, Min(0f)] float laneSwayBoostDuration = 1.5f;

            [Header("공중그네 첫 봉 잡기")]
            [Tooltip("첫 높은 봉과 플레이어 발 높이의 허용 차이(m). 짧아진 목마 높이·점프에도 잡을 여유를 준다. 팔 부상은 수평 잡기 거리에 적용한다.")]
            [SerializeField, Range(.1f, 1.2f)] float trapezeEntryHeightTolerance = 1.2f;
            [Tooltip("첫 높은 봉의 수평 잡기 거리(m). 팔 부상의 상호작용 거리 배율을 곱한다.")]
            [SerializeField, Min(.1f)] float trapezeEntryGrabDistance = 1.05f;

            public float TrapezeEntryHeightTolerance => Mathf.Clamp(trapezeEntryHeightTolerance, .1f, 1.2f);
            public float TrapezeEntryGrabDistance => Mathf.Max(.1f, trapezeEntryGrabDistance);

            [Header("동료 착지 파동 (초기 테스트값)")]
            [SerializeField, Min(0f)] float landingWaveRange = 6f;
            [SerializeField, Min(0.1f)] float landingWaveSpeed = 10f;
            [SerializeField, Range(0f, 15f)] float landingWaveShock = 6f;
            [SerializeField, Min(0f)] float landingWaveDelay = .2f;

            public float LandingWaveRange => Mathf.Clamp(landingWaveRange, 0f, 100f);
            public float LandingWaveSpeed => Mathf.Clamp(landingWaveSpeed, .1f, 100f);
            public float LandingWaveShock => Mathf.Clamp(landingWaveShock, 0f, 15f);
            public float LandingWaveDelay => Mathf.Clamp(landingWaveDelay, 0f, 2f);

            public float LandingShock => landingShock;
            public float MoveSpeed => GameSettings.Base.Character.FreeMoveSpeed;
            public float JumpHeight => GameSettings.Base.Character.JumpHeight * Mathf.Max(1f, jumpHeightMultiplier);
            public float JumpGravityMultiplier => 1f;
            public float JumpInputBuffer => jumpInputBuffer;
            public float NaturalSwayMultiplier => Mathf.Max(0f, naturalSwayMultiplier);
            public float MinAirTimeForShock => minAirTimeForShock;
            public float LaneLandingShock => laneLandingShock;
            public float LaneLandingDangerRatio => Mathf.Clamp01(laneLandingDangerRatio);
            public float LaneSwayBoost => laneSwayBoost;
            public float LaneSwayBoostDuration => laneSwayBoostDuration;
        }

        /// <summary>손잡기(협동 옆줄 이동): 옆줄 착지 지점이 같은 줄 동료 가까이면 착지 충격·흔들림을 줄인다.</summary>
        [Serializable]
        public sealed class HandholdSettings
        {
            [Tooltip("옆줄 착지 지점과 같은 줄 동료의 앞뒤 거리(m, 몸 중심끼리)가 이 안이면 손잡기. 기획 1.0.")]
            [SerializeField, Min(0f)] float distance = 1f;

            [Tooltip("손잡기 때 옆줄 착지 충격·흔들림 증가 감소율(0~1). 기획 0.5(50%).")]
            [SerializeField, Range(0f, 1f)] float reduction = 0.5f;

            [Tooltip("F로 동료를 받을 준비를 유지하는 시간(초). 이동·점프하면 해제한다.")]
            [SerializeField, Min(0.1f)] float readySeconds = 3f;
            [SerializeField, Min(0f)] float activeDistance = 1.25f;
            [SerializeField, Range(0f, 1f)] float activeReduction = 0.9f;

            [Header("봉 양쪽 교대 버팀 (고도화 초기값)")]
            [Tooltip("봉 앞에서 F로 버티는 동료는 착지 충격을 완충하고, 양쪽 발판의 준비는 주변 톱 정지를 요청한다.")]
            [SerializeField] bool relayBraceEnabled = true;
            [Tooltip("끊어진 줄 시작점에서 뒤쪽 버팀 표시까지 거리(m). 봉 잡기 위치와 겹치지 않게 둔다.")]
            [SerializeField, Min(2f)] float relaySourceOffset = 3f;
            [Tooltip("버팀 표시의 앞뒤 위치 허용 거리(m).")]
            [SerializeField, Range(.2f, 1f)] float relayPadRadius = .6f;

            public float Distance => distance;
            public float Reduction => reduction;
            public float ReadySeconds => Mathf.Max(.1f, readySeconds);
            public float ActiveDistance => Mathf.Max(0f, activeDistance);
            public float ActiveReduction => Mathf.Clamp01(activeReduction);
            public bool RelayBraceEnabled => relayBraceEnabled;
            public float RelaySourceOffset => Mathf.Max(2f, relaySourceOffset);
            public float RelayPadRadius => Mathf.Clamp(relayPadRadius, .2f, 1f);
        }

        /// <summary>목마(여러 층으로 쌓기).</summary>
        [Serializable]
        public sealed class PiggybackSettings
        {
            [Tooltip("목마 전체 인원별 흔들림 배율(×, 1인~4인 순서). 자연 흔들림과 충격에 곱한다. 기획 1 / 1.3 / 1.6 / 2.")]
            [SerializeField] float[] stackMultipliers = { 1f, 1.3f, 1.6f, 2f };

            [Tooltip("위층 균형 전달: 초당 (내 위층 균형값 합 × 이 값)만큼 같은 방향으로 민다. 기획 0.2.")]
            [SerializeField, Min(0f)] float upperTransfer = 0.2f;

            [Tooltip("목마 연결 거리(m, 수평). F 짧게를 누른 사람과 상대(목마면 맨 아래 사람) 사이가 이 안이어야 올라간다. 호스트 값으로 판정. 기획 미정(임시 1.5).")]
            [SerializeField, Min(0f)] float linkDistance = 1.5f;

            [Tooltip("위층이 떨어질 때 바로 아래층이 받는 반동 충격(균형 ±, 랜덤 방향, 목마 배율 적용). 호스트 값으로 보낸다. 기획 미정(임시 10, 착지 충격과 같음).")]
            [SerializeField, Min(0f)] float fallShock = 10f;

            [Tooltip("목마 맨 위 점프의 높이 배율(×). 부상 배율을 적용한 기존 높이에 곱하며, 제자리·앞뒤 분리·옆줄 분리 점프에 적용한다.")]
            [SerializeField, Min(0f)] float jumpHeightMultiplier = 1.5f;

            [Tooltip("맨 위 W + SB 앞으로 분리 점프의 수평 속도(m/s). 높이는 목마 점프 배율을 적용한다. 기획 미정(임시 2.2).")]
            [SerializeField, Min(0f)] float forwardJumpSpeed = 2.2f;

            [Tooltip("맨 위 S + SB 뒤로 분리 점프의 수평 속도(m/s). 높이는 목마 점프 배율을 적용한다. 기획 미정(임시 0.7).")]
            [SerializeField, Min(0f)] float backwardJumpSpeed = 0.7f;

            public float UpperTransfer => upperTransfer;
            public float LinkDistance => linkDistance;
            public float FallShock => fallShock;
            public float JumpHeightMultiplier => Mathf.Max(0f, jumpHeightMultiplier);
            public float ForwardJumpSpeed => forwardJumpSpeed;
            public float BackwardJumpSpeed => backwardJumpSpeed;

            /// <summary>목마 전체 인원의 배율. 칸이 비어 있으면 1, 인원이 칸보다 많으면 마지막 칸.</summary>
            public float GetStackMultiplier(int stackSize) =>
                stackMultipliers == null || stackMultipliers.Length == 0
                    ? 1f
                    : stackMultipliers[Mathf.Clamp(stackSize, 1, stackMultipliers.Length) - 1];
        }

        /// <summary>
        /// 외줄 카메라 구도와 가림 처리(Integrations/Active/Integration-tightrope-prototype/camera-review-20261004.md, 2026-10-04 PM).
        /// 카메라 리그(PlayerFollowCamera)의 구도·가림 처리 컴포넌트가 매 프레임 읽는다.
        /// </summary>
        [Serializable]
        public sealed class CameraSettings
        {
            [Tooltip("카메라 높이(m, 내 캐릭터 발 기준 위). 이전 2.6. 기획 미정(임시 3.75).")]
            [SerializeField, Min(0f)] float height = 3.75f;

            [Tooltip("카메라 거리(m, 코스 뒤쪽).")]
            [SerializeField, Min(0.5f)] float distance = 4.5f;

            [Tooltip("바라보는 점 높이(m, 내 캐릭터 발 기준).")]
            [SerializeField] float lookHeight = 1.2f;

            [Tooltip("바라보는 점을 내 캐릭터 앞쪽으로 옮기는 거리(m). 앞 장애물이 화면 가운데로 온다. 기획 미정(임시 1.75).")]
            [SerializeField] float lookAhead = 1.75f;

            [Tooltip("함께 화면에 담을 가까운 동료의 수평 거리(m).")]
            [SerializeField, Min(0f)] float nearbyRadius = 6f;

            [Tooltip("앞쪽 톱날을 미리 화면에 담는 거리(m).")]
            [SerializeField, Min(0f)] float obstacleLookAhead = 8f;

            [Tooltip("목마·동료·장애물을 담기 위해 카메라가 멀어질 수 있는 최대 거리(m).")]
            [SerializeField, Min(0.5f)] float maximumDistance = 12f;

            [Tooltip("화면 가장자리와 대상 사이의 여유(m).")]
            [SerializeField, Min(0f)] float framingPadding = 0.5f;

            [Tooltip("앞 장애물을 볼 때 추가로 앞을 바라보는 최대 거리(m).")]
            [SerializeField, Min(0f)] float contextLookAhead = 2f;

            [Tooltip("상황에 따라 구도가 넓어지고 돌아오는 완충 시간(초).")]
            [SerializeField, Min(0.01f)] float framingSmoothTime = 0.45f;

            [Tooltip("카메라와 내 캐릭터 사이를 가리는 것(다른 플레이어·장애물)의 투명도(0 투명 ~ 1 불투명). 기획 미정(임시 0.35).")]
            [SerializeField, Range(0f, 1f)] float occluderAlpha = 0.35f;

            [Tooltip("가림 판정 굵기(m, 반지름). 카메라→내 캐릭터 선에서 이 안에 걸리면 흐리게 한다.")]
            [SerializeField, Min(0f)] float occluderRadius = 0.4f;

            [Tooltip("흐려지고 돌아오는 빠르기(초당 투명도 변화).")]
            [SerializeField, Min(0.1f)] float fadeSpeed = 6f;

            public float Height => height;
            public float Distance => distance;
            public float LookHeight => lookHeight;
            public float LookAhead => lookAhead;
            public float NearbyRadius => nearbyRadius;
            public float ObstacleLookAhead => obstacleLookAhead;
            public float MaximumDistance => maximumDistance;
            public float FramingPadding => framingPadding;
            public float ContextLookAhead => contextLookAhead;
            public float FramingSmoothTime => framingSmoothTime;
            public float OccluderAlpha => occluderAlpha;
            public float OccluderRadius => occluderRadius;
            public float FadeSpeed => fadeSpeed;
        }

        /// <summary>
        /// 팔·다리를 잃었을 때 외줄에서 받는 불이익(다친 본인만). 규칙: Harness/Project/Decisions/body-damage.md
        /// 두 다리를 잃었을 때의 기어가기(속도·캡슐)는 게임 공통이라 게임 기본 세팅에 있다. 모두 2026-10-04 임시값.
        /// </summary>
        [Serializable]
        public sealed class BodyPenaltySettings
        {
            [Tooltip("다리 하나만 잃었을 때 이동 속도 배율(×). 두 다리를 잃으면 기어가기 속도(게임 기본 세팅)를 쓴다. 임시값 0.8.")]
            [SerializeField, Range(0f, 1f)] float oneLegMoveSpeed = 0.8f;

            [Tooltip("외줄에서 다리를 잃어도 유지할 최소 이동 속도 배율. 공통 기어가기 감속을 포함한 최종 값이다.")]
            [SerializeField, Range(0.01f, 1f)] float minimumInjuredMoveSpeed = 0.8f;

            [Tooltip("잃은 다리 하나당 자연 흔들림 증가(+). 1 + 이 값 × 잃은 다리 수. 임시값 0.25(하나 ×1.25, 둘 ×1.5).")]
            [SerializeField, Min(0f)] float swayPerLostLeg = 0.25f;

            [Tooltip("잃은 팔 하나당 자연 흔들림 증가(+). 1 + 이 값 × 잃은 팔 수. 다리 쪽 배율과 곱한다. 임시값 0.1.")]
            [SerializeField, Min(0f)] float swayPerLostArm = 0.1f;

            [Tooltip("두 다리를 잃었을 때 제자리 점프 착지 충격 배율(×). 임시값 1.5(10 → 15).")]
            [SerializeField, Min(1f)] float leglessLandingShock = 1.5f;

            [Tooltip("두 다리를 잃었을 때 옆줄 이동 착지 충격 배율(×). 임시값 1.5(35 → 52.5).")]
            [SerializeField, Min(1f)] float leglessLaneLandingShock = 1.5f;

            [Tooltip("잃은 팔 하나당 상호작용(목마 올라타기) 가능 거리 감소(−). 1 − 이 값 × 잃은 팔 수. 임시값 0.25(하나 ×0.75, 둘 ×0.5). 목마(006)가 이 거리를 쓴다.")]
            [SerializeField, Range(0f, 0.5f)] float reachLossPerArm = 0.25f;

            public float OneLegMoveSpeed => oneLegMoveSpeed;
            public float MinimumInjuredMoveSpeed => minimumInjuredMoveSpeed;
            public float SwayPerLostLeg => swayPerLostLeg;
            public float SwayPerLostArm => swayPerLostArm;
            public float LeglessLandingShock => leglessLandingShock;
            public float LeglessLaneLandingShock => leglessLaneLandingShock;
            public float ReachLossPerArm => reachLossPerArm;
        }
    }
}
