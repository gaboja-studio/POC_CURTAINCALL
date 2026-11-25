using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        // 런타임 봉과 같은 좌표를 사용한다. 미리보기는 지지면이나 상호작용을 생성하지 않는다.
        public int RelayPoleLane(int station)
        {
            int transit = (LaneCount - 1) / 2;
            return Mathf.Clamp(station % 2 == 0 ? (transit == 1 ? 0 : 1)
                : Mathf.Max(transit + 1, LaneCount - 2), 0, LaneCount - 1);
        }
        /// <summary>도착 해제봉은 빈칸 끝에서 이만큼 뒤에 선다.</summary>
        public const float RelayFarPoleOffset = 2f;
        public Vector3 RelayPoleGround(int station) => GetLanePosition(RelayPoleLane(station),
            station < 2 ? SwingGapFrom - 2.5f : SwingGapTo + RelayFarPoleOffset);
        /// <summary>공연 공중그네가 쓰는 자리(<see cref="TrapezeRestingFeet"/> 번호). 2026-11-11 사용자 요청으로 가운데 교대 봉(1)을 뺐다.</summary>
        public static readonly int[] TrapezePerformancePoles = { 0, 2 };
        /// <summary>낮은 복귀 봉(2)은 출발 봉에서 줄 방향으로 이만큼 떨어진다(2026-11-11 사용자 요청, 이전 빈칸 길이의 74%).</summary>
        public const float TrapezeLowPoleSpacing = 11.5f;
        public Vector3 TrapezeRestingFeet(int index)
        {
            const float startOffset = -1f;
            float along = TrapezeGapFrom + (index == 0 ? startOffset
                : index == 1 ? Shape.TrapezeLength * .38f : startOffset + TrapezeLowPoleSpacing);
            // 첫 협동 봉은 줄 위 5m(발 3.5m + 공통 매달림 1.5m).
            // 혼자 점프한 몸 중심+잡기 범위를 넘고 목마 윗사람의 점프로 접근한다.
            float height = index == 0 ? 3.5f : index == 1 ? 2.1f : .6f;
            return GetLanePosition(Shape.TrapezeLane - 1, along) + Vector3.up * height;
        }

        void DrawPolePreview()
        {
#if UNITY_EDITOR
            if (HasRelayGap)
                for (int i = 0; i < 4; i++)
                    DrawPole(RelayPoleGround(i) + Vector3.up * 2.2f, i < 2 ? Color.cyan : Color.green,
                        i < 2 ? "출발 해제봉 · F 유지" : "반대편 해제봉 · 남은 동료 연결");
            if (HasTrapezeGap)
                for (int i = 0; i < TrapezePerformancePoles.Length; i++)
                {
                    bool last = i == TrapezePerformancePoles.Length - 1;
                    DrawPole(TrapezeRestingFeet(TrapezePerformancePoles[i]) + Vector3.up * 1.5f, last ? Color.green : Color.cyan,
                        i == 0 ? "1 · 목마로만 F 잡기" : last ? $"{i + 1} · 낮은 복귀 봉" : $"{i + 1} · 동료 몸 잡고 교대");
                    if (TrapezePerformancePoles[i] == 0) DrawPiggybackZone(TrapezeRestingFeet(0) + Vector3.up * CurtainCall.Settings.GameSettings.Poles.HangLength);
                }
#endif
        }

#if UNITY_EDITOR
        /// <summary>런타임에 생기는 높은 출발 봉의 바닥 잡기 상자(GameSettings › 봉 설정 › 기본 바닥 잡기 영역)와 기준 높이.</summary>
        void DrawPiggybackZone(Vector3 bar)
        {
            var zone = CurtainCall.Settings.GameSettings.Poles.PiggybackGrabZone;
            var yaw = Quaternion.LookRotation(Forward, Vector3.up);
            var previous = Gizmos.matrix;
            Gizmos.color = new Color(1f, .82f, .2f, 1f);
            Gizmos.matrix = Matrix4x4.TRS(bar, yaw, Vector3.one);
            Gizmos.DrawWireCube(zone.center, zone.SafeSize);
            Gizmos.matrix = previous;
            float rope = bar.y - CurtainCall.Settings.GameSettings.Poles.HangLength - 3.5f;
            void Reach(float height, string label)
            {
                bool inside = CurtainCall.HumanCannon.PoleReachReference.Reaches(zone, bar, yaw, rope, height);
                Vector3 point = bar + yaw * new Vector3(zone.center.x, 0f, zone.center.z);
                point.y = rope + height;
                Gizmos.color = inside ? new Color(.35f, 1f, .45f) : new Color(1f, .35f, .3f);
                Gizmos.DrawLine(point - Right * .6f, point + Right * .6f);
                UnityEditor.Handles.Label(point + Right * .6f, $"{label} · {(inside ? "닿음" : "안 닿음")}");
            }
            Reach(CurtainCall.HumanCannon.PoleReachReference.SoloJump, "혼자 점프");
            Reach(CurtainCall.HumanCannon.PoleReachReference.PiggybackStand, "목마 위");
            Reach(CurtainCall.HumanCannon.PoleReachReference.PiggybackJump, "목마 점프");
            UnityEditor.Handles.Label(bar + Vector3.up * (zone.center.y + zone.SafeSize.y * .5f + .3f), "목마 전용 판정 상자 · 봉 설정에서 조절");
        }
#endif

#if UNITY_EDITOR
        void DrawPole(Vector3 grip, Color color, string label)
        {
            Gizmos.color = color;
            Vector3 side = Right * .65f;
            Gizmos.DrawLine(grip - side, grip + side);
            Gizmos.DrawWireSphere(grip, .1f);
            Gizmos.DrawLine(grip - side, grip - side + Vector3.up * 1.5f);
            Gizmos.DrawLine(grip + side, grip + side + Vector3.up * 1.5f);
            UnityEditor.Handles.Label(grip + Vector3.up * 1.6f, label);
        }
#endif
    }
}
