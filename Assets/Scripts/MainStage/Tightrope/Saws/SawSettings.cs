using System;
using UnityEngine;

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

        public HorizontalSawEntry(float distance, SawStartSide startSide)
        {
            this.distance = distance;
            this.startSide = startSide;
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
        public float verticalSpawnDistance = 93f;

        [Tooltip("제거 거리(m). 기획 5.")]
        public float verticalEndDistance = 5f;

        [Tooltip("시작점 쪽으로 내려오는 속도(m/s). 기획 0.8 고정(#7).")]
        [Min(0.01f)] public float verticalSpeed = 0.8f;

        [Tooltip("충돌 지름(m). 기획 1.0 — 상단 1.1m라 점프로 못 넘는다.")]
        [Min(0.01f)] public float verticalColliderDiameter = 1f;

        [Tooltip("보이는 지름(m). 기획 1.2.")]
        [Min(0.01f)] public float verticalVisualDiameter = 1.2f;

        [Tooltip("중심 높이(m, 외줄 윗면 기준). 기획 0.6.")]
        public float verticalCenterHeight = 0.6f;

        [Tooltip("두께(m). 임시값.")]
        [Min(0.01f)] public float verticalThickness = 0.1f;

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

        [Tooltip("눌린 뒤 톱날이 내려가는 시간(초). 기획 0.1~0.3, 기본 0.2(CC-64).")]
        [Range(0.1f, 0.3f)] public float verticalLowerDuration = 0.2f;

        [Tooltip("내려가는 깊이(m). 충돌체 상단(1.1)보다 깊어야 지나갈 수 있다. 임시값.")]
        [Min(0f)] public float verticalLowerDepth = 1.5f;

        [Header("부위 판정")]
        [Tooltip("허리 높이(m, 발바닥 기준). 닿은 곳이 이보다 낮으면 다리, 높으면 팔. 임시값(캡슐 높이 1.5 기준).")]
        [Min(0f)] public float waistHeight = 0.85f;

        [Tooltip("같은 톱날·같은 플레이어가 다시 판정되기까지 최소 시간(초). 닿아 있는 동안에는 다시 판정하지 않는다. 임시값.")]
        [Min(0f)] public float hitCooldown = 1f;

        [Header("겉모습")]
        [Tooltip("톱날 회전 속도(도/초). 겉모습만.")]
        public float spinSpeed = 720f;

        /// <summary>가로 톱날 수.</summary>
        public int HorizontalCount => horizontalSaws?.Length ?? 0;

        /// <summary>가로 톱날 위치(m).</summary>
        public float GetHorizontalDistance(int index) => horizontalSaws[index].distance;

        /// <summary>가로 톱날 속도(m/s). 첫 톱날 거리 → 마지막 톱날 거리 사이를 직선으로 잇는다.</summary>
        public float GetHorizontalSpeed(int index)
        {
            int count = HorizontalCount;
            if (count <= 1) return horizontalFirstSpeed;
            float first = horizontalSaws[0].distance, last = horizontalSaws[count - 1].distance;
            float t = Mathf.Approximately(first, last) ? 0f : Mathf.InverseLerp(first, last, horizontalSaws[index].distance);
            return Mathf.Lerp(horizontalFirstSpeed, horizontalLastSpeed, t);
        }

        /// <summary>가로 톱날이 왼쪽 끝(좌→우)에서 출발하는지.</summary>
        public bool StartsLeft(int index) => horizontalSaws[index].startSide == SawStartSide.Left;

        /// <summary>가로 톱날의 작동 기록·위치 계산을 새로 만든다(작동 전, 출발 쪽 끝에서 대기).</summary>
        public HorizontalSawTrack CreateTrack(int index) =>
            new(GetHorizontalSpeed(index), horizontalTravelHalfWidth, StartsLeft(index));

        /// <summary>원문 규칙: 게임 시작 때 가로 전체 작동, 묘기 시작 60초 뒤 수직 톱날(랜덤 줄), 제거 20초 뒤 끝없이 반복.</summary>
        public static ObstacleStep[] CreateDefaultSteps() => new[]
        {
            new ObstacleStep("가로 전체", StepTrigger.GameStart, 0f, StepAction.StartHorizontal) { firstSaw = 1, lastSaw = 22 },
            new ObstacleStep("수직", StepTrigger.PerformanceStart, 60f, StepAction.SpawnVertical) { repeat = -1, repeatDelay = 20f },
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

        /// <summary>가로 톱날 중심 높이(m, 외줄 윗면 기준).</summary>
        public float HorizontalCenterHeight => horizontalAttackTop - horizontalThickness * 0.5f;

        /// <summary>수직 톱날 진행 거리(m). <paramref name="sinceSpawn"/> = 생성 후 지난 시간.</summary>
        public float GetVerticalDistance(float sinceSpawn) => verticalSpawnDistance - verticalSpeed * Mathf.Max(0f, sinceSpawn);

        /// <summary>수직 톱날이 끝 거리에 닿았는지.</summary>
        public bool HasVerticalReachedEnd(float sinceSpawn) => GetVerticalDistance(sinceSpawn) <= verticalEndDistance;

        /// <summary>발판을 밟은 뒤 내려간 깊이(m, 0 이상). <paramref name="sinceLower"/>가 음수면 0.</summary>
        public float GetLowerOffset(float sinceLower) =>
            sinceLower < 0f ? 0f : verticalLowerDepth * Mathf.Clamp01(sinceLower / verticalLowerDuration);

        /// <summary>다 내려갔는지.</summary>
        public bool HasLowered(float sinceLower) => sinceLower >= verticalLowerDuration;
    }
}
