using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>
    /// 톱날 장애물 조정값 묶음. 규칙: Harness/Project/Decisions/tightrope-course-rules.md #6~#9 (2026-10-03).
    /// 거리는 외줄 시작점 0m 기준 진행 거리, 높이는 외줄 윗면 기준(m). 기획에 없는 값은 "임시값"으로 표시한다.
    /// 값은 외줄 세팅 에셋(<see cref="CurtainCall.Settings.TightropeSettings.Saws"/>, Assets/Resources/Settings/GameSettings/Tricks/TightropeSettings.asset)의 톱날 칸에 있다(2026-10-05).
    /// 계산 도우미가 있어 클래스는 톱날 폴더에 둔다.
    /// </summary>
    /// <summary>가로 톱날이 처음 출발하는 쪽.</summary>
    public enum SawStartSide
    {
        /// <summary>왼쪽 끝에서 오른쪽으로(원문 "좌측 톱날 좌→우").</summary>
        Left,
        /// <summary>오른쪽 끝에서 왼쪽으로(원문 "우측 톱날 우→좌").</summary>
        Right,
    }

    /// <summary>가로 톱날 하나의 배치.</summary>
    [Serializable]
    public struct HorizontalSawEntry
    {
        [Tooltip("중심 위치(m, 외줄 시작점 기준).")]
        public float distance;

        [Tooltip("처음 출발하는 쪽. 작동 전에는 이쪽 끝(맵 밖)에서 기다린다. 언제 작동할지는 장애물 순서(steps)가 정한다.")]
        public SawStartSide startSide;

        [Tooltip("켜면 공통 사선 각도 대신 코스를 똑바로 가로지른다. 시작점에서 보아 오른쪽 끝 위치에 맞춰 왼쪽을 올린 수평 레일(2026-11-11 3번 톱날).")]
        public bool level;

        [Tooltip("켜면 공통 사선과 반대 방향으로 기운다(시작점에서 보아 왼쪽 끝이 먼 쪽). 같은 위치의 일반 톱날과 겹치면 X자가 된다. 수평이면 쓰지 않는다.")]
        public bool mirrored;

        [Tooltip("거리 비율로 정한 속도에 더하는 값(m/s). 0이면 기본 속도.")]
        public float extraSpeed;

        public HorizontalSawEntry(float distance, SawStartSide startSide)
        {
            this.distance = distance;
            this.startSide = startSide;
            level = false;
            mirrored = false;
            extraSpeed = 0f;
        }
    }

    [Serializable]
    public sealed class SawSettings
    {
        [Header("장애물 순서")]
        [Tooltip("장애물 순서(레벨 디자인). 단계마다 시작 조건·지연·할 일(가로 톱날 범위 작동/정지, 수직 톱날 출현)·반복을 정한다. 기본값은 원문 규칙(게임 시작 때 가로 전체 작동, 묘기 시작 60초 뒤 수직 톱날, 제거 20초 뒤 반복). 2026-10-03 PM.")]
        public ObstacleStep[] steps = CreateDefaultSteps();

        [Header("가로 톱날")]
        [Tooltip("가로 톱날 목록(위치·출발 쪽). 번호는 위에서부터 1번. 원문 5장 22개, 출발 쪽은 원문에 배정이 없어 번갈아(1번 왼쪽) 임시값.")]
        public HorizontalSawEntry[] horizontalSaws = CreateDefaultHorizontalSaws();

        [Tooltip("첫 톱날 속도(m/s). 기획 3.0(#6).")]
        [Min(0.01f)] public float horizontalFirstSpeed = 3f;

        [Tooltip("마지막 톱날 속도(m/s). 사이 톱날은 거리에 따라 직선으로 늘어난다. 기획 4.5(#6).")]
        [Min(0.01f)] public float horizontalLastSpeed = 4.5f;

        [Tooltip("톱날 중심이 왕복하는 반폭(m, 코스 가운데 기준). 맵 경계(6.5) 밖 3m = 9.5.")]
        [Min(0f)] public float horizontalTravelHalfWidth = 9.5f;

        [Tooltip("충돌 지름(m). 기획 1.0.")]
        [Min(0.01f)] public float horizontalColliderDiameter = 1f;

        [Tooltip("보이는 지름(m). 기획 1.2.")]
        [Min(0.01f)] public float horizontalVisualDiameter = 1.2f;

        [Tooltip("공격 판정 윗면 높이(m, 외줄 윗면 기준). 기획 0.2 — 점프(0.8)로 넘는다.")]
        public float horizontalAttackTop = 0.2f;

        [Tooltip("두께(m). 판정은 윗면에서 이만큼 아래까지. 임시값.")]
        [Min(0.01f)] public float horizontalThickness = 0.1f;

        [Header("수직 톱날")]
        [Tooltip("생성 거리(m). 기획 93.")]
        public float verticalSpawnDistance = 75f;

        [Tooltip("제거 거리(m). 기획 5.")]
        public float verticalEndDistance = 5f;

        [Tooltip("시작점 쪽으로 내려오는 속도(m/s). 기획 0.8 고정(#7).")]
        [Min(0.01f)] public float verticalSpeed = 0.8f;

        [Tooltip("나온 뒤 회전 없이 제자리에 놓여 있는 시간(초). 0이면 바로 회전 시작(2026-11-12 3단계 정면 톱날).")]
        [Min(0f)] public float verticalIdleTime;

        [Tooltip("제자리에서 회전·불꽃이 0에서 최대까지 커지는 시간(초). 끝나면 출발점 쪽으로 움직인다. 0이면 바로 출발.")]
        [Min(0f)] public float verticalSpinUpTime;

        [Tooltip("켜면 누군가 공중그네 첫 봉(목마로만 닿는 높은 봉)을 잡은 뒤로 그 단계 공연 동안(판을 다시 해도) 수직 톱날이 더 나오지 않는다. 이미 나온 톱날은 끝까지 간다(2026-11-12 3단계).")]
        public bool verticalStopsAtTrapeze;

        [Tooltip("첫 봉으로 멈출 때 이미 나와 있던 톱날이 외줄에서 떨어지는 중력(m/s²). 진행 속도를 유지한 채 포물선으로 떨어진다(발판 없는 정면 톱날만).")]
        [Min(0.1f)] public float verticalFallGravity = 9.81f;

        [Tooltip("떨어지는 톱날 중심이 외줄 윗면에서 이만큼(m) 아래로 내려가면 사라진다.")]
        [Min(0f)] public float verticalFallRemoveDepth = 15f;

        [Tooltip("충돌 지름(m). 기획 1.0 — 상단 1.1m라 점프로 못 넘는다.")]
        [Min(0.01f)] public float verticalColliderDiameter = 1f;

        [Tooltip("보이는 지름(m). 기획 1.2.")]
        [Min(0.01f)] public float verticalVisualDiameter = 1.2f;

        [Tooltip("중심 높이(m, 외줄 윗면 기준). 기획 0.6.")]
        public float verticalCenterHeight = 0.6f;

        [Tooltip("두께(m). 임시값.")]
        [Min(0.01f)] public float verticalThickness = 0.1f;

        [Tooltip("켜면 사선으로 옆줄을 오가지 않고 생성된 줄을 따라 똑바로 온다(3단계 정면 톱날, 2026-11-12).")]
        public bool verticalStraight;

        [Tooltip("켜면 톱날 뒤 발판이 없다. 밟아서 내릴 수 없고 옆줄로만 피한다(3단계 정면 톱날, 2026-11-12).")]
        public bool verticalNoPlate;

        [Tooltip("켜면 줄을 정하지 않은 출현 단계가 진행자 줄이 아니라 쓸 수 있는 모든 줄 중 하나를 랜덤으로 고른다(3단계 정면 톱날, 2026-11-12).")]
        public bool verticalAnyLane;

        [Tooltip("수직 톱날 외형(프리팹) 배율. 1이면 기존 수직 톱날 외형 크기. 판정 지름·두께·높이는 위 값을 따른다(3단계 정면 톱날 2배, 2026-11-12).")]
        [Min(0.01f)] public float verticalLookScale = 1f;

        [Header("수직 톱날 불꽃 (외줄에 닿는 하단에서 뒤쪽·위로)")]
        [Tooltip("불꽃 크기 배율. 가로 톱날 불꽃 크기 × 톱날 크기 비율(수직 외형 지름 ÷ 가로 톱날 지름) × 이 값.")]
        [Min(0f)] public float verticalSparkSize = 3f;

        [Tooltip("불꽃 양 배율. 가로 톱날 불꽃(이동 1m당 9개) × 이 값. 0이면 불꽃 없음.")]
        [Min(0f)] public float verticalSparkAmount = 1.5f;

        [Tooltip("튀는 속도 배율. 가로 톱날 불꽃 속도(3~7m/s) × 톱날 크기 비율 × 이 값.")]
        [Min(0f)] public float verticalSparkSpeed = 1f;

        [Tooltip("퍼지는 각도(도). 클수록 넓게 흩어진다.")]
        [Range(0f, 89f)] public float verticalSparkSpread = 30f;

        [Tooltip("위로 튀는 정도. 0이면 톱날 뒤쪽 수평, 클수록 위로 솟는다.")]
        [Min(0f)] public float verticalSparkLift = .8f;

        [Header("목마 협동 톱 관문")]
        [Tooltip("켜면 일반 정면 톱 출현 단계 대신 선두의 줄로 다가오는 큰 원형 톱을 사용한다.")]
        public bool cooperativeGates;
        [Tooltip("관문이 멈춰 팀을 기다리는 진행 거리(m). 오름차순으로 배치한다.")]
        public float[] cooperativeGateDistances = { 170f, 320f };
        [Min(0f)] public float cooperativeGateTriggerLead = 24f;
        [Min(0f)] public float cooperativeGateApproachDistance = 12f;
        [Min(0.1f)] public float cooperativeGateApproachSpeed = 3f;
        [Min(0.1f)] public float cooperativeGateHeight = 2.1f;
        [Min(0.05f)] public float cooperativeGateThickness = 0.2f;
        [Min(0.1f)] public float cooperativeGatePlateOffset = 2.2f;
        [Min(0.1f)] public float cooperativeGatePlateLength = 1.6f;
        [Tooltip("최후반 자동 개방의 상승·하강 시간(초).")]
        [FormerlySerializedAs("cooperativeGateLowerDuration")]
        [Min(0.1f)] public float cooperativeGateOpenDuration = 0.3f;
        [Header("버튼으로 톱을 뒤로 넘기기 (초기 테스트값)")]
        [Min(0.5f)] public float cooperativeGateHopDuration = 1.2f;
        [Min(3f)] public float cooperativeGateHopDistance = 6f;
        [Tooltip("이전 에셋과 편집기 도구의 호환값. 실제 톱 홉 높이는 기본 캡슐 높이 × cooperativeGateHopHeightMultiplier를 쓴다.")]
        [Min(0f)] public float cooperativeGateHopHeight = 5f;
        [Tooltip("톱 홉의 정점 높이 = 기본 캐릭터 캡슐 높이 × 이 값. 남은 목마를 자동으로 보호하지 않는다.")]
        [Min(0.1f)] public float cooperativeGateHopHeightMultiplier = 1.5f;
        [Header("최후반 협동 도약 (2명부터)")]
        [Tooltip("최후반 관문마다 출발선의 2~4인 목마를 확인한다. 접속 인원과 무관하게 2명으로 통과 가능하며, 3·4층이 떠나도 2층의 발판 작동은 유지된다.")]
        public bool finaleFourStackRequired;
        [Range(0.5f, 1f)] public float finaleStartFraction = 0.8f;
        [Tooltip("3·4층은 발판 뒤쪽 끝과 몸 반경을 지난 뒤 이 거리만큼 더 앞에 착지한다.")]
        [Min(0.5f)] public float cooperativeGateUpperLandingOffset = 2f;
        [Header("최후반 단독 통과 (초기 테스트값)")]
        [Tooltip("기존 최후반 자동 개방을 유지할지. 협동 도약 코스에서는 끈다.")]
        public bool finaleSoloAutoOpen = true;
        [Tooltip("마지막 관문이 이 코스 비율 이후에 있을 때만 주기적으로 열린다. 중반 협동 관문에는 적용하지 않는다.")]
        [Range(0.5f, 1f)] public float finaleSoloStartFraction = 0.9f;
        [Min(0.1f)] public float finaleGateClosedSeconds = 4f;
        [Tooltip("완전히 열린 상태의 유지 시간. 다리 부상 상태의 보행도 통과할 수 있게 둔다.")]
        [Min(0.1f)] public float finaleGateOpenSeconds = 4f;
        [Tooltip("목마 앞점프를 도와주는 출발선 앞뒤 허용 거리(m). 실제 두 사람 연결이 있어야 한다.")]
        [Min(0.05f)] public float cooperativeGateLaunchTolerance = 0.35f;
        [Tooltip("부상으로 목마가 낮아져도 관문 위로 확보하는 최소 점프 정점 여유(m).")]
        [Min(0.1f)] public float cooperativeGateJumpClearance = 0.6f;

        [Header("발판(수직 톱날 뒤, 임시 형태)")]
        [Tooltip("톱날 중심에서 발판 중심까지 거리(m, 톱날 뒤 = 도착 쪽). 임시값.")]
        [Min(0f)] public float plateOffset = 1.5f;

        [Tooltip("발판 길이(m, 진행 방향). 임시값.")]
        [Min(0.05f)] public float plateLength = 0.8f;

        [Tooltip("발판 폭(m). 임시값.")]
        [Min(0.05f)] public float plateWidth = 0.8f;

        [Tooltip("발이 외줄 윗면에서 이 높이(m) 안이면 밟은 것으로 본다. 임시값.")]
        [Min(0f)] public float plateHeightTolerance = 0.3f;

        [Tooltip("이 시간(초) 동안 계속 밟아야 눌린다. 0이면 밟는 즉시. 임시값(Open: 누가·몇 초).")]
        [Min(0f)] public float plateHoldTime = 0f;

        [Tooltip("협동 발판을 밟은 뒤 주변 가로 톱을 내리고 멈추는 시간(초).")]
        [Min(0f)] public float teamPassageDuration = 6f;

        [Tooltip("협동 발판 앞뒤로 안전하게 열리는 구간의 반길이(m).")]
        [Min(0f)] public float teamPassageHalfLength = 6f;

        [Tooltip("협동 통로가 끝나기 전 톱이 서서히 올라오며 재가동을 예고하는 시간(초).")]
        [Min(0.1f)] public float teamPassageWarning = 1f;

        [Tooltip("눌린 뒤 톱날이 내려가는 시간(초). 기획 0.1~0.3, 기본 0.2(CC-64).")]
        [Range(0.1f, 0.3f)] public float verticalLowerDuration = 0.2f;

        [Tooltip("내려가는 깊이(m). 충돌체 상단(1.1)보다 깊어야 지나갈 수 있다. 임시값.")]
        [Min(0f)] public float verticalLowerDepth = 1.5f;

        [Header("부위 판정")]
        [Tooltip("허리 높이(m, 발바닥 기준). 닿은 곳이 이보다 낮으면 다리, 높으면 팔. 임시값(캡슐 높이 1.5 기준).")]
        [Min(0f)] public float waistHeight = 0.85f;


        [Header("겉모습")]
        [Tooltip("기본 가로 톱날 원판 색. 낮은 가로 톱날은 점프로 피한다.")]
        public Color horizontalColor = new(0.05f, 0.8f, 0.85f);

        [Tooltip("기본 정면 톱날 원판 색. 정면 톱날은 옆줄로 피한다. 별도 모델의 원본 재질은 보존한다.")]
        public Color verticalColor = new(1f, 0.38f, 0.08f);

        [Tooltip("톱날 회전 속도(도/초). 겉모습만.")]
        public float spinSpeed = 720f;

        [Tooltip("수직 톱날 회전 속도(도/초). 0 이하면 위 spinSpeed. 겉모습만(2026-11-12 정면 톱날 1초 1바퀴 = 360).")]
        public float verticalSpinSpeed = -1f;

        /// <summary>수직 톱날 회전 속도(도/초).</summary>
        public float VerticalSpinSpeed => verticalSpinSpeed > 0f ? verticalSpinSpeed : spinSpeed;

        /// <summary>가로 톱날 수(단계 추가 톱날 포함).</summary>
        public int HorizontalCount => horizontalSaws?.Length ?? 0;

        [NonSerialized] int baseHorizontalCount = -1;

        /// <summary>1단계 목록의 가로 톱날 수. 단계 추가 톱날은 이 뒤 번호에 붙는다.</summary>
        public int BaseHorizontalCount => baseHorizontalCount >= 0 ? Mathf.Min(baseHorizontalCount, HorizontalCount) : HorizontalCount;

        /// <summary>단계에서 추가한 톱날인지(톱날 밀도와 상관없이 항상 나온다).</summary>
        public bool IsStageExtra(int index) => index >= BaseHorizontalCount && index < HorizontalCount;

        /// <summary>
        /// 1단계 값에 단계 추가 톱날을 뒤에 붙인 복사본. 1단계 목록 마지막 톱날까지 작동시키는 장애물 순서는 추가 톱날까지 넓힌다.
        /// </summary>
        public SawSettings WithExtraHorizontalSaws(HorizontalSawEntry[] extra)
        {
            if (extra == null || extra.Length == 0) return this;
            var copy = Copy();
            int baseCount = HorizontalCount, total = baseCount + extra.Length;
            copy.horizontalSaws = new HorizontalSawEntry[total];
            if (baseCount > 0) Array.Copy(horizontalSaws, copy.horizontalSaws, baseCount);
            Array.Copy(extra, 0, copy.horizontalSaws, baseCount, extra.Length);
            copy.baseHorizontalCount = baseCount;
            if (copy.steps != null)
                foreach (var step in copy.steps)
                    if (step != null && step.action is StepAction.StartHorizontal or StepAction.StopHorizontal && step.lastSaw >= baseCount)
                        step.lastSaw = total;
            return copy;
        }

        /// <summary>
        /// 단계 정면 톱날을 켠 복사본(2026-11-12 3단계). <paramref name="spawnDistance"/>에서 생겨 출발점(0m)까지 진행 중인 플레이어의 줄을 따라
        /// 똑바로 오고, 발판이 없어 옆줄로만 피한다. 장애물 순서에 수직 톱날 단계가 없으면 누군가 줄에 오르는 즉시 모든 줄 중 랜덤 한 곳에 내고
        /// 사라지면 <paramref name="repeatDelay"/>초 뒤 반복하는 단계를 더한다. <paramref name="speed"/>가 0보다 크면 그 속도(m/s)로 온다.
        /// </summary>
        public SawSettings WithIncomingSaw(float spawnDistance, float speed = -1f, float repeatDelay = 20f, float scale = 1f, float thickness = -1f, float startDelay = 0f,
            float idleTime = 0f, float spinUpTime = 0f, bool stopsAtTrapeze = false)
        {
            var copy = Copy();
            copy.baseHorizontalCount = baseHorizontalCount; // 직렬화하지 않는 값이라 복사본에 옮긴다
            copy.verticalSpawnDistance = Mathf.Max(0f, spawnDistance);
            copy.verticalEndDistance = 0f;
            copy.verticalStraight = true;
            copy.verticalNoPlate = true;
            copy.verticalAnyLane = true;
            if (speed > 0f) copy.verticalSpeed = speed;
            if (scale > 0f && !Mathf.Approximately(scale, 1f))
            {
                // 크기를 키워도 바닥은 지금처럼 외줄 윗면에 닿도록 중심 높이도 같은 배율로 올린다.
                copy.verticalColliderDiameter *= scale;
                copy.verticalVisualDiameter *= scale;
                copy.verticalThickness *= scale;
                copy.verticalCenterHeight *= scale;
                copy.verticalLookScale *= scale;
            }
            if (thickness > 0f) copy.verticalThickness = thickness;
            copy.verticalIdleTime = Mathf.Max(0f, idleTime);
            copy.verticalSpinUpTime = Mathf.Max(0f, spinUpTime);
            copy.verticalStopsAtTrapeze = stopsAtTrapeze;
            var steps = copy.steps ?? Array.Empty<ObstacleStep>();
            if (Array.Exists(steps, step => step != null && step.action == StepAction.SpawnVertical)) return copy;
            var vertical = new ObstacleStep("정면 톱날", StepTrigger.PerformanceStart, Mathf.Max(0f, startDelay), StepAction.SpawnVertical) { repeat = -1, repeatDelay = Mathf.Max(0f, repeatDelay) };
            copy.steps = new ObstacleStep[steps.Length + 1];
            Array.Copy(steps, copy.steps, steps.Length);
            copy.steps[^1] = vertical;
            return copy;
        }

        SawSettings Copy() => JsonUtility.FromJson<SawSettings>(JsonUtility.ToJson(this));

        /// <summary>가로 톱날 위치(m).</summary>
        public float GetHorizontalDistance(int index) => horizontalSaws[index].distance;

        /// <summary>가로 톱날 속도(m/s). 첫 톱날 거리 → 마지막 톱날 거리 사이를 직선으로 잇는다.</summary>
        public float GetHorizontalSpeed(int index)
        {
            int count = BaseHorizontalCount;
            if (count <= 1) return Mathf.Max(.01f, horizontalFirstSpeed + horizontalSaws[index].extraSpeed);
            float first = horizontalSaws[0].distance, last = horizontalSaws[count - 1].distance;
            float t = Mathf.Approximately(first, last) ? 0f : Mathf.InverseLerp(first, last, horizontalSaws[index].distance);
            return Mathf.Max(.01f, Mathf.Lerp(horizontalFirstSpeed, horizontalLastSpeed, t) + horizontalSaws[index].extraSpeed);
        }

        /// <summary>가로 톱날이 사선 대신 수평 레일을 쓰는지.</summary>
        public bool IsLevel(int index) => horizontalSaws[index].level;

        /// <summary>가로 톱날이 공통 사선과 반대 방향으로 기우는지.</summary>
        public bool IsMirrored(int index) => horizontalSaws[index].mirrored;

        /// <summary>가로 톱날이 왼쪽 끝(좌→우)에서 출발하는지.</summary>
        public bool StartsLeft(int index) => horizontalSaws[index].startSide == SawStartSide.Left;

        /// <summary>가로 톱날의 작동 기록·위치 계산을 새로 만든다(작동 전, 출발 쪽 끝에서 대기).</summary>
        public HorizontalSawTrack CreateTrack(int index) =>
            new(GetHorizontalSpeed(index), horizontalTravelHalfWidth, StartsLeft(index));

        /// <summary>원문 규칙: 게임 시작 때 가로 전체 작동, 묘기 시작 60초 뒤 수직 톱날(랜덤 줄), 제거 20초 뒤 끝없이 반복.</summary>
        public static ObstacleStep[] CreateDefaultSteps() => new[]
        {
            new ObstacleStep("가로 전체", StepTrigger.GameStart, 0f, StepAction.StartHorizontal) { firstSaw = 1, lastSaw = 22 },
            new ObstacleStep("수직", StepTrigger.PerformanceStart, 10f, StepAction.SpawnVertical) { repeat = -1, repeatDelay = 20f },
        };

        /// <summary>원문 5장 위치 22개, 출발 쪽은 번갈아(1번 왼쪽).</summary>
        public static HorizontalSawEntry[] CreateDefaultHorizontalSaws()
        {
            float[] distances =
            {
                5.6f, 8.8f, 12.0f, 15.2f, 18.4f,
                22.5f, 27.5f, 32.5f, 37.5f, 42.5f, 47.5f, 52.5f, 57.5f, 62.5f, 67.5f,
                71.8f, 75.4f, 78.9f, 82.5f, 86.1f, 89.6f, 93.2f,
            };
            var saws = new HorizontalSawEntry[distances.Length];
            for (int i = 0; i < saws.Length; i++)
                saws[i] = new HorizontalSawEntry(distances[i], i % 2 == 0 ? SawStartSide.Left : SawStartSide.Right);
            return saws;
        }

        /// <summary>초반 점프 간격을 넓히고 후반에 촘촘해지는 20개 배치. 실제 코스 길이에 비례하며 기본 설정을 자동 교체하지 않는다.</summary>
        public static HorizontalSawEntry[] CreateLearningHorizontalSaws(float finishDistance)
        {
            float[] template = { 12f, 21f, 30f, 40f, 48f, 56f, 64f, 72f, 80f, 88f,
                96f, 104f, 112f, 118f, 124f, 130f, 135f, 139f, 143f, 147f };
            var saws = new HorizontalSawEntry[template.Length];
            float scale = Mathf.Max(0f, finishDistance) / 150f;
            for (int i = 0; i < saws.Length; i++)
                saws[i] = new HorizontalSawEntry(template[i] * scale,
                    i % 2 == 0 ? SawStartSide.Left : SawStartSide.Right);
            return saws;
        }

        /// <summary>가로 톱날 중심 높이(m, 외줄 윗면 기준).</summary>
        public float HorizontalCenterHeight => horizontalAttackTop - horizontalThickness * 0.5f;

        /// <summary>수직 톱날 진행 거리(m). <paramref name="sinceSpawn"/> = 생성 후 지난 시간.</summary>
        public float GetVerticalDistance(float sinceSpawn) => verticalSpawnDistance - verticalSpeed * Mathf.Max(0f, sinceSpawn - VerticalWindup);

        /// <summary>나온 뒤 출발하기까지(놓임 + 회전 가속) 초.</summary>
        public float VerticalWindup => Mathf.Max(0f, verticalIdleTime) + Mathf.Max(0f, verticalSpinUpTime);

        /// <summary>회전·불꽃 세기 0~1. 놓여 있는 동안 0, 회전 가속 동안 차오르고, 출발할 때 1.</summary>
        public float GetVerticalSpinProgress(float sinceSpawn)
        {
            float t = sinceSpawn - Mathf.Max(0f, verticalIdleTime);
            if (t <= 0f) return 0f;
            return verticalSpinUpTime <= 0f ? 1f : Mathf.Clamp01(t / verticalSpinUpTime);
        }

        /// <summary>수직 톱날 회전 각도(도). 놓여 있는 동안 0, 가속 동안 일정하게 빨라져 출발 때 <paramref name="degreesPerSecond"/>에 닿는다.</summary>
        public float GetVerticalSpinAngle(float sinceSpawn, float degreesPerSecond)
        {
            float t = sinceSpawn - Mathf.Max(0f, verticalIdleTime), ramp = Mathf.Max(0f, verticalSpinUpTime);
            if (t <= 0f) return 0f;
            if (t < ramp) return degreesPerSecond * t * t / (2f * ramp);
            return degreesPerSecond * (ramp * .5f + t - ramp);
        }

        /// <summary>수직 톱날이 끝 거리에 닿았는지.</summary>
        public bool HasVerticalReachedEnd(float sinceSpawn) => GetVerticalDistance(sinceSpawn) <= verticalEndDistance;

        /// <summary>발판을 밟은 뒤 내려간 깊이(m, 0 이상). <paramref name="sinceLower"/>가 음수면 0.</summary>
        public float GetLowerOffset(float sinceLower) =>
            sinceLower < 0f ? 0f
            : verticalNoPlate ? .5f * verticalFallGravity * sinceLower * sinceLower // 발판 없는 정면 톱날: 외줄에서 떨어짐
            : verticalLowerDepth * Mathf.Clamp01(sinceLower / verticalLowerDuration);

        /// <summary>다 내려갔는지.</summary>
        public bool HasLowered(float sinceLower) => verticalNoPlate
            ? verticalCenterHeight - GetLowerOffset(sinceLower) <= -verticalFallRemoveDepth
            : sinceLower >= verticalLowerDuration;
    }
}
