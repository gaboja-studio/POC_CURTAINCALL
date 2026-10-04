using System;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>
    /// 톱날 장애물 조정값 묶음. 규칙: Harness/Project/Decisions/tightrope-course-rules.md #6~#9 (2026-10-03).
    /// 거리는 외줄 시작점 0m 기준 진행 거리, 높이는 외줄 윗면 기준(m). 기획에 없는 값은 "임시값"으로 표시한다.
    /// 값은 외줄 세팅 에셋(<see cref="CurtainCall.Settings.TightropeSettings.Saws"/>, Assets/Resources/GameSettings/Tricks/TightropeSettings.asset)의 톱날 칸에 있다(2026-10-05).
    /// 계산 도우미가 있어 클래스는 톱날 폴더에 둔다. 톱날 위치·시간(레벨 디자인)은 장애물 배치 파일(<see cref="ObstacleLayout"/>, 2026-10-05)에 있고, 여기는 공통 규격만 둔다.
    /// </summary>
    [Serializable]
    public sealed class SawSettings
    {
        [Header("가로 톱날")]
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
        [Tooltip("생성 한계 거리(m). 수직 톱날은 그 줄 선두 앞(배치 파일의 '선두 앞 최소 거리')에 생기되 이보다 멀리는 생기지 않는다. 기획 93(7-1·7-2).")]
        public float verticalSpawnDistance = 93f;

        [Tooltip("등장 낙하 높이(m, 외줄 윗면 기준). 생성 지점의 카메라 화면 위(목마 4층까지 고려)에서 생겨 내려온다. 임시값 8(7-3, 2026-10-05 PM).")]
        [Min(0f)] public float verticalDropHeight = 8f;

        [Tooltip("등장 낙하 시간(초). 내려오는 동안은 앞으로 움직이지 않고, 다 내려온 뒤 시작점 쪽으로 움직인다. 0이면 바로 줄 위에 나타남. 임시값 0.8.")]
        [Min(0f)] public float verticalDropDuration = 0.8f;

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

        /// <summary>가로 톱날 중심 높이(m, 외줄 윗면 기준).</summary>
        public float HorizontalCenterHeight => horizontalAttackTop - horizontalThickness * 0.5f;

        /// <summary>수직 톱날 진행 거리(m). <paramref name="spawnDistance"/> = 생긴 거리, <paramref name="sinceSpawn"/> = 생성 후 지난 시간(낙하 시간 동안은 제자리).</summary>
        public float GetVerticalDistance(float spawnDistance, float sinceSpawn) =>
            spawnDistance - verticalSpeed * Mathf.Max(0f, sinceSpawn - verticalDropDuration);

        /// <summary>등장 낙하 중 줄 위로 남은 높이(m, 0 이상). 점점 빨라지며 떨어진다.</summary>
        public float GetVerticalDropOffset(float sinceSpawn)
        {
            if (verticalDropDuration <= 0f) return 0f;
            float t = Mathf.Clamp01(sinceSpawn / verticalDropDuration);
            return verticalDropHeight * (1f - t * t);
        }

        /// <summary>다 내려와 줄 위에 섰는지(발판은 이때부터 보이고 눌린다).</summary>
        public bool HasVerticalLanded(float sinceSpawn) => sinceSpawn >= verticalDropDuration;

        /// <summary>수직 톱날이 끝 거리에 닿았는지.</summary>
        public bool HasVerticalReachedEnd(float spawnDistance, float sinceSpawn) => GetVerticalDistance(spawnDistance, sinceSpawn) <= verticalEndDistance;

        /// <summary>발판을 밟은 뒤 내려간 깊이(m, 0 이상). <paramref name="sinceLower"/>가 음수면 0.</summary>
        public float GetLowerOffset(float sinceLower) =>
            sinceLower < 0f ? 0f : verticalLowerDepth * Mathf.Clamp01(sinceLower / verticalLowerDuration);

        /// <summary>다 내려갔는지.</summary>
        public bool HasLowered(float sinceLower) => sinceLower >= verticalLowerDuration;
    }
}
