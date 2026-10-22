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
        public Vector3 RelayPoleGround(int station) => GetLanePosition(RelayPoleLane(station),
            station < 2 ? SwingGapFrom - 2.5f : SwingGapTo + 3f);
        public Vector3 TrapezeRestingFeet(int index)
        {
            float along = TrapezeGapFrom + (index == 0 ? -1f : Shape.TrapezeLength * (index == 1 ? .38f : .74f));
            float height = index == 0 ? 1.5f : index == 1 ? 2.1f : .6f;
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
                for (int i = 0; i < 3; i++)
                    DrawPole(TrapezeRestingFeet(i) + Vector3.up * 1.5f, i == 2 ? Color.green : Color.cyan,
                        i == 0 ? "1 · 가까이서 F 잡기" : i == 1 ? "2 · 동료 몸 잡고 교대" : "3 · 낮은 복귀 봉");
#endif
        }

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
