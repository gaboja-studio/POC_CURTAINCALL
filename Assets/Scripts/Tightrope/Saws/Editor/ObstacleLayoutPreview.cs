using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws.EditorTools
{
    /// <summary>
    /// 장애물 배치 미리보기 상태(시간 슬라이더)와 계산. 창(<see cref="ObstacleLayoutWindow"/>)과 씬 뷰 그리기(<see cref="ObstacleLayoutSceneView"/>)가 함께 쓴다.
    /// 가로 톱날은 실행 코드와 같은 계산(<see cref="ObstacleLayout.GetHorizontalSideAt"/>), 수직 톱날은 "가상 선두가 묘기 시작부터 줄 위 걷기 속도로 걷는다"고 가정한다.
    /// </summary>
    public static class ObstacleLayoutPreview
    {
        const string PrefsPrefix = "CurtainCall.ObstacleLayout.";

        /// <summary>미리보기를 켰는지. 끄면 톱날을 출발 쪽 끝(대기 위치)에 그린다.</summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefsPrefix + "Preview", true);
            set => EditorPrefs.SetBool(PrefsPrefix + "Preview", value);
        }

        /// <summary>미리보는 시각(게임 경과, 초).</summary>
        public static float Time { get; set; }

        /// <summary>가정: 묘기가 시작되는 시각(게임 경과, 초). 처음 플레이어가 줄에 오르는 때.</summary>
        public static float PerformanceStartedAt
        {
            get => EditorPrefs.GetFloat(PrefsPrefix + "PerformanceStart", 3f);
            set => EditorPrefs.SetFloat(PrefsPrefix + "PerformanceStart", Mathf.Max(0f, value));
        }

        /// <summary>씬 뷰를 다시 그린다.</summary>
        public static void Repaint() => SceneView.RepaintAll();

        public static SawSettings Saws => GameSettings.Tightrope.Saws;

        /// <summary>가상 선두의 걷기 속도(m/s) = 외줄 세팅의 줄 위 이동 속도.</summary>
        public static float WalkSpeed => GameSettings.Tightrope.RopeMovement.MoveSpeed;

        /// <summary>그 시각 가상 선두의 거리(m). 묘기 시작 전이면 NaN.</summary>
        public static float GetVirtualLeader(float time) =>
            time < PerformanceStartedAt ? float.NaN : WalkSpeed * (time - PerformanceStartedAt);

        /// <summary>미리보기 수직 톱날 한 번의 등장.</summary>
        public struct VerticalAppearance
        {
            public int serial;          // 1부터
            public float spawnedAt;     // 게임 경과(초)
            public float spawnDistance; // m
            public float removedAt;     // 게임 경과(초)
            public float contactAt;     // 가상 선두와 만나는 시각. 안 만나면 NaN
        }

        /// <summary>
        /// 그 시각의 수직 톱날 상태. 나와 있으면 true와 등장 정보. <paramref name="note"/>는 안 나오는 이유(없으면 null).
        /// 실행 규칙(7-1~7-3)과 같은 순서: 첫 등장 → 사라짐(끝 거리) → 다음 등장까지 → 최대 횟수. 가상 선두 앞 n m가 생성 한계를 넘으면 더는 안 나온다.
        /// </summary>
        public static bool TryGetVertical(ObstacleLayout layout, float time, out VerticalAppearance appearance, out string note)
        {
            appearance = default;
            note = null;
            var rule = layout.vertical;
            var saws = Saws;
            if (rule == null || !rule.enabled)
            {
                note = "수직 톱날 꺼짐";
                return false;
            }

            float due = PerformanceStartedAt + rule.firstDelay;
            for (int serial = 1; rule.maxCount <= 0 || serial <= rule.maxCount; serial++)
            {
                if (time < due)
                {
                    note = $"#{serial} 등장 대기: {due:0.0}초에 나옴";
                    return false;
                }
                float leader = GetVirtualLeader(due);
                float spawnDistance = layout.GetVerticalSpawnDistance(leader, saws);
                if (float.IsNaN(spawnDistance))
                {
                    note = $"#{serial}: {due:0.0}초에 가상 선두({leader:0.0}m) 앞 {rule.minLeadDistance:0.#}m가 생성 한계 {saws.verticalSpawnDistance:0.#}m를 넘어 안 나옴";
                    return false;
                }
                float travel = Mathf.Max(0f, spawnDistance - saws.verticalEndDistance) / saws.verticalSpeed;
                float removedAt = due + saws.verticalDropDuration + travel;
                float landedAt = due + saws.verticalDropDuration;
                float leaderAtLand = WalkSpeed * (landedAt - PerformanceStartedAt);
                float closing = saws.verticalSpeed + WalkSpeed;
                float contactAt = closing <= 0f ? float.NaN : landedAt + Mathf.Max(0f, spawnDistance - leaderAtLand) / closing;
                if (contactAt > removedAt) contactAt = float.NaN;

                if (time < removedAt)
                {
                    appearance = new VerticalAppearance
                    {
                        serial = serial,
                        spawnedAt = due,
                        spawnDistance = spawnDistance,
                        removedAt = removedAt,
                        contactAt = contactAt,
                    };
                    return true;
                }
                due = removedAt + rule.nextDelay;
                if (serial > 1000) break; // 안전장치
            }
            note = "최대 등장 횟수를 모두 씀";
            return false;
        }
    }
}
