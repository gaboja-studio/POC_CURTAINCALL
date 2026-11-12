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
        [Header("추락 매달림·동료 끌어올리기 (M2)")]
        [SerializeField] HangSettings hang = new();
        [Header("공연 단계 추가 배치 (2단계부터)")]
        [Tooltip("대기실 스케줄에서 고른 외줄 단계에만 1단계 위에 더 놓는 기물. 첫 칸이 2단계, 둘째 칸이 3단계다. 1단계(위 코스·톱날 칸)를 고치면 모든 단계에 같이 반영된다.")]
        [SerializeField] StageSettings[] laterStages = { new(), new() };

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
        public HangSettings Hang => hang ??= new HangSettings();

        /// <summary>공연 단계 수(대기실 스케줄의 1~3단계).</summary>
        public const int StageCount = 3;
        public static int ClampStage(int stage) => Mathf.Clamp(stage, 1, StageCount);

        /// <summary>그 단계의 코스 값. 지금은 모든 단계가 1단계 코스를 쓴다(추가 배치는 기물만).</summary>
        public CourseSettings CourseFor(int stage) => course;

        [NonSerialized] Tightrope.Saws.SawSettings[] stageSaws;

        /// <summary>그 단계의 톱날 값 = 1단계 톱날 + 그 단계 추가 톱날(+ 정면 톱날). 추가가 없으면 1단계 값 그대로.</summary>
        public Tightrope.Saws.SawSettings SawsFor(int stage)
        {
            var later = LaterStage(stage);
            var extra = later?.extraHorizontalSaws;
            bool incoming = later != null && later.incomingSaw;
            if ((extra == null || extra.Length == 0) && !incoming) return saws;
            stageSaws ??= new Tightrope.Saws.SawSettings[StageCount];
            ref var cached = ref stageSaws[ClampStage(stage) - 1];
            if (cached != null) return cached;
            cached = saws.WithExtraHorizontalSaws(extra);
            if (incoming) cached = cached.WithIncomingSaw(IncomingSawSpawnDistance(later),
                later.incomingSawSpeed, Mathf.Max(0f, later.incomingSawRepeatDelay), later.incomingSawScale, later.incomingSawThickness, later.incomingSawStartDelay,
                later.incomingSawIdleTime, later.incomingSawSpinUpTime, later.incomingSawStopsAtTrapeze);
            return cached;
        }

        /// <summary>
        /// 정면 톱날 생성 거리(m): 도착 끝에서 앞으로 offset만큼. 그 자리가 불에 타 줄이 사라지는 구간(마지막 구간)이면
        /// 그 구간이 시작되기 전, 줄이 사라지지 않는 곳으로 당긴다(톱날 반지름만큼 더 앞, 2026-11-12).
        /// </summary>
        float IncomingSawSpawnDistance(StageSettings later)
        {
            float spawn = course.FinishDistance - Mathf.Max(0f, later.incomingSawFinishOffset);
            float radius = saws.verticalColliderDiameter * Mathf.Max(.1f, later.incomingSawScale) * .5f;
            var segments = fire?.segments;
            for (bool moved = true; moved && segments != null;)
            {
                moved = false;
                foreach (var segment in segments)
                    if (spawn + radius > segment.from && spawn - radius < segment.to)
                    {
                        spawn = segment.from - radius;
                        moved = true;
                    }
            }
            return Mathf.Max(0f, spawn);
        }

        /// <summary>그 단계의 추격 불 속도(m/s). 2·3단계 값이 0보다 작으면 1단계(불 칸의 chaseSpeed).</summary>
        public float FireChaseSpeedFor(int stage)
        {
            float own = LaterStage(stage)?.fireChaseSpeed ?? -1f;
            return own >= 0f ? own : fire.chaseSpeed;
        }

        StageSettings LaterStage(int stage)
        {
            int index = ClampStage(stage) - 2;
            return index >= 0 && laterStages != null && index < laterStages.Length ? laterStages[index] : null;
        }

        void OnValidate()
        {
            stageSaws = null;
            Changed?.Invoke();
        }

        /// <summary>2단계부터 그 단계에만 1단계 위에 더 놓는 기물.</summary>
        [Serializable]
        public sealed class StageSettings
        {
            [Tooltip("이 단계에만 나오는 가로 톱날(위치·출발 쪽·수평). 톱날 밀도와 상관없이 항상 나오고, 1단계 목록의 마지막 톱날을 작동시키는 장애물 순서와 함께 작동한다. 속도는 1단계 첫·마지막 톱날 거리 비율을 따른다.")]
            public Tightrope.Saws.HorizontalSawEntry[] extraHorizontalSaws = Array.Empty<Tightrope.Saws.HorizontalSawEntry>();

            [Tooltip("이 단계의 추격 불 속도(m/s). 0보다 작으면 1단계(불 칸의 chaseSpeed)와 같다.")]
            public float fireChaseSpeed = -1f;

            [Tooltip("켜면 이 단계에만 정면 톱날이 나온다. 도착 쪽 외줄 끝 앞에서 생겨 진행 중인 플레이어의 줄을 따라 출발점(0m)까지 똑바로 온다. 지름·속도·높이는 톱날 칸의 수직 톱날 값, 발판 없음 — 점프로 못 넘고 옆줄로만 피한다(2026-11-12).")]
            public bool incomingSaw;

            [Tooltip("정면 톱날 생성 위치: 도착 쪽 외줄 끝에서 시작점 쪽으로 이 거리(m). 그 자리가 불에 타 줄이 사라지는 구간이면 그 구간 앞(줄이 남는 곳)으로 당겨진다.")]
            [Min(0f)] public float incomingSawFinishOffset = 5f;

            [Tooltip("정면 톱날 속도(m/s). 0 이하면 톱날 칸의 수직 톱날 속도.")]
            public float incomingSawSpeed = -1f;

            [Tooltip("정면 톱날이 출발점에 닿아 사라진 뒤 다음 톱날이 나오기까지(초).")]
            [Min(0f)] public float incomingSawRepeatDelay = 20f;

            [Tooltip("정면 톱날 크기 배율(외형·판정 지름·두께). 중심 높이도 같이 올라 바닥은 외줄 위에 그대로 놓인다.")]
            [Min(0.1f)] public float incomingSawScale = 1f;

            [Tooltip("정면 톱날 두께(m, 외형·판정 함께). 0 이하면 톱날 칸 두께 × 크기 배율.")]
            public float incomingSawThickness = -1f;

            [Tooltip("누군가 처음 외줄에 오른 뒤 첫 정면 톱날이 나오기까지(초). 0이면 즉시.")]
            [Min(0f)] public float incomingSawStartDelay = 0f;

            [Tooltip("정면 톱날이 나온 뒤 회전 없이 놓여 있는 시간(초).")]
            [Min(0f)] public float incomingSawIdleTime = 0f;

            [Tooltip("제자리에서 회전과 불꽃이 0에서 최대까지 커지는 시간(초). 최대가 되면 출발점 쪽으로 온다.")]
            [Min(0f)] public float incomingSawSpinUpTime = 0f;

            [Tooltip("켜면 누군가 공중그네 첫 봉(목마로만 닿는 높은 봉)을 잡은 뒤로 이 단계 공연 동안(판을 다시 해도) 정면 톱날이 더 나오지 않는다. 이미 나온 톱날은 끝까지 간다.")]
            public bool incomingSawStopsAtTrapeze;
        }

        /// <summary>
        /// M2(2026-10-27) 균형 추락 매달림: 추락 게이지가 넘치면 줄을 잡고 버티고, 근처 동료가 F로 끌어올린다.
        /// 근거: Harness/Project/Decisions/tightrope-course-rules.md #1-1. 호스트 값으로 판정한다.
        /// </summary>
        [Serializable]
        public sealed class HangSettings
        {
            [Tooltip("매달려 버티는 시간(초). 다 지나면 손을 놓고 일반 낙하. 기획 7.")]
            [SerializeField, Min(.5f)] float holdSeconds = 7f;
            [Tooltip("동료가 F를 누른 뒤 끌어올리는 시간(초). 그동안 버티는 시간이 멈춘다. 기획 2.")]
            [SerializeField, Min(.1f)] float pullSeconds = 2f;
            [Tooltip("구조할 때 줄 윗면과 동료 발 사이에 허용하는 높이 차이(m). 기본 0.75. 공중에서는 구조 불가.")]
            [SerializeField, Min(.1f)] float rescueHeightTolerance = .75f;
            [Tooltip("추락 시작 위치에서 붙잡을 줄까지의 실제 거리(m). 생성/배치 줄에 같은 값을 사용한다.")]
            [SerializeField, Min(.1f)] float catchDistance = 1.5f;
            [Tooltip("같은 줄의 다른 매달린 사람과 이 거리(m) 안이면 그 줄은 이미 찬 것으로 본다(한 줄에 한 명). 임시 2.")]
            [SerializeField, Min(.1f)] float laneOccupyDistance = 2f;
            [Tooltip("매달린 몸의 발이 줄 윗면보다 내려가는 거리(m). 보이는 자세용.")]
            [SerializeField, Min(.5f)] float hangDrop = 1.9f;

            public float HoldSeconds => Mathf.Max(.5f, holdSeconds);
            public float PullSeconds => Mathf.Max(.1f, pullSeconds);
            public float RescueDistance => GameSettings.Base.Interaction.Distance;
            public float RescueHeightTolerance => Mathf.Max(.1f, rescueHeightTolerance);
            public float CatchDistance => Mathf.Max(.1f, catchDistance);
            public float LaneOccupyDistance => Mathf.Max(.1f, laneOccupyDistance);
            public float HangDrop => Mathf.Max(.5f, hangDrop);
        }

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


            [Header("모양")]
            [Tooltip("기획 초기 테스트: 중간 줄을 끊고 회전봉으로 건너는 구간을 만든다. 짧은 테스트 코스에는 적용하지 않는다.")]
            [SerializeField] bool swingGapEnabled = true;
            [Tooltip("끊긴 구간을 해제봉 교대 관문으로 사용한다. 켜면 모든 줄이 끊기며 출발·도착 봉으로 연결한다.")]
            [SerializeField] bool relayGapEnabled = true;
            [Tooltip("양쪽에서 통로를 유지하는 데 필요한 봉 인원. 첫 관문 1, 심화 관문 2.")]
            [SerializeField, Range(1, 2)] int relayRequiredHolders = 1;
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



            [Tooltip("출발 위치(m, 외줄 시작점 기준). 음수면 시작 플랫폼 위. -1.")]
            [SerializeField] float spawnDistance = -1f;

            [Header("추락")]
            [Tooltip("코스 아래 안전망 깊이(m). 떨어진 캐릭터를 받는다. 0이면 만들지 않는다. 6.")]
            [SerializeField, Min(0f)] float safetyNetDepth = 6f;


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
            public float StartPlatformLength => startPlatformLength;
            public float FinishPlatformLength => finishPlatformLength;
            public float SideMargin => sideMargin;
            public float PlatformThickness => platformThickness;
            public float RopeThickness => ropeThickness;
            public float RopeWalkWidth => GameSettings.Tightrope.RopeMovement.SupportWidth;
            public float RopeHeightTolerance => GameSettings.Tightrope.RopeMovement.SupportHeightTolerance;
            public float SpawnDistance => spawnDistance;
            public float SafetyNetDepth => safetyNetDepth;
            public float FallDepth => GameSettings.Tightrope.RopeMovement.FallReleaseDepth;
            public CourseSection[] Sections => sections;
            public bool SwingGapEnabled => swingGapEnabled;
            public bool RelayGapEnabled => relayGapEnabled;
            public int RelayRequiredHolders => Mathf.Clamp(relayRequiredHolders, 1, 2);
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

        /// <summary>모든 외줄의 이동·점프·착지 공통값. 코스 배치와 분리한다.</summary>
        [Serializable]
        public sealed class RopeMovementSettings
        {
            [Tooltip("켜면 보이는 외줄의 굵기로 지지 폭을 맞춘다. 생성 코스는 줄 굵기, 배치된 줄은 자체 메시/콜라이더 폭을 사용한다. 모든 씬 공통.")]
            [SerializeField] bool matchVisualSupportWidth = true;
            public bool MatchVisualSupportWidth => matchVisualSupportWidth;
            [Tooltip("외줄 외형에 지지 폭 맞추기를 끈 경우 사용하는 공통 지지 폭(m).")]
            [SerializeField, Min(.01f)] float supportWidth = .55f;
            [Tooltip("발과 줄 윗면의 높이 허용(m). 모든 씬 공통.")]
            [SerializeField, Min(.01f)] float supportHeightTolerance = .3f;
            [Tooltip("줄 아래 일반 낙하 전환 깊이(m). 착지 피해 높이와 별개.")]
            [SerializeField, Min(.1f)] float fallReleaseDepth = 1f;
            public float SupportWidth => Mathf.Max(.01f, supportWidth);
            public float SupportHeightTolerance => Mathf.Max(.01f, supportHeightTolerance);
            public float FallReleaseDepth => Mathf.Max(.1f, fallReleaseDepth);

            [Tooltip("모든 외줄의 제자리 점프 높이(m). 탈출로 기준 1.5m.")]
            [SerializeField, Min(0f)] float jumpHeight = 1.5f;

            [InspectorName("외줄 점프 중력 배율"), Tooltip("외줄 점프 중 적용할 기본 중력의 배율(배). 점프 높이를 유지하며 값이 커질수록 빠르게 뛰고 내려온다. 일반 바닥 점프에는 적용하지 않는다. 기본 1배.")]
            [SerializeField, Min(.1f)] float jumpGravityMultiplier = 1f;

            [Tooltip("같은 높이의 옆줄 이동 최대 수평 거리(m). 높이가 다른 줄은 대상에서 제외한다.")]
            [SerializeField, Min(0f)] float laneJumpReach = 3f;

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



            [Header("동료 착지 파동 (초기 테스트값)")]
            [Tooltip("동료가 줄에 착지했을 때 흔들림이 전달되는 최대 거리(m). 0이면 전달하지 않는다. 기본 6m.")]
            [SerializeField, Min(0f)] float landingWaveRange = 6f;
            [Tooltip("동료 착지 흔들림이 줄을 따라 전달되는 속도(m/s). 클수록 먼 동료에게 빨리 도착한다. 기본 10m/s.")]
            [SerializeField, Min(0.1f)] float landingWaveSpeed = 10f;
            [Tooltip("동료 착지 파동이 균형에 더하는 충격량. 클수록 중심을 크게 흔든다. 0~15, 기본 6.")]
            [SerializeField, Range(0f, 15f)] float landingWaveShock = 6f;
            [Tooltip("동료 착지 후 흔들림 전달을 시작하기까지 기다리는 시간(초). 기본 0.2초. 실제 적용 범위 0~2초.")]
            [SerializeField, Min(0f)] float landingWaveDelay = .2f;

            public float LandingWaveRange => Mathf.Clamp(landingWaveRange, 0f, 100f);
            public float LandingWaveSpeed => Mathf.Clamp(landingWaveSpeed, .1f, 100f);
            public float LandingWaveShock => Mathf.Clamp(landingWaveShock, 0f, 15f);
            public float LandingWaveDelay => Mathf.Clamp(landingWaveDelay, 0f, 2f);

            public float LandingShock => landingShock;
            public float MoveSpeed => GameSettings.Base.Character.FreeMoveSpeed;
            public float JumpHeight => Mathf.Max(0f, jumpHeight);
            public float LaneJumpReach => Mathf.Max(0f, laneJumpReach);
            public float JumpGravityMultiplier => Mathf.Max(.1f, jumpGravityMultiplier);
            public float JumpInputBuffer => GameSettings.Base.Input.JumpInputBuffer;
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


            [Tooltip("잃은 다리 하나당 자연 흔들림 증가(+). 1 + 이 값 × 잃은 다리 수. 임시값 0.25(하나 ×1.25, 둘 ×1.5).")]
            [SerializeField, Min(0f)] float swayPerLostLeg = 0.25f;

            [Tooltip("잃은 팔 하나당 자연 흔들림 증가(+). 1 + 이 값 × 잃은 팔 수. 다리 쪽 배율과 곱한다. 임시값 0.1.")]
            [SerializeField, Min(0f)] float swayPerLostArm = 0.1f;

            [Tooltip("두 다리를 잃었을 때 제자리 점프 착지 충격 배율(×). 임시값 1.5(10 → 15).")]
            [SerializeField, Min(1f)] float leglessLandingShock = 1.5f;

            [Tooltip("두 다리를 잃었을 때 옆줄 이동 착지 충격 배율(×). 임시값 1.5(35 → 52.5).")]
            [SerializeField, Min(1f)] float leglessLaneLandingShock = 1.5f;


            public float SwayPerLostLeg => swayPerLostLeg;
            public float SwayPerLostArm => swayPerLostArm;
            public float LeglessLandingShock => leglessLandingShock;
            public float LeglessLaneLandingShock => leglessLaneLandingShock;
        }
    }
}
