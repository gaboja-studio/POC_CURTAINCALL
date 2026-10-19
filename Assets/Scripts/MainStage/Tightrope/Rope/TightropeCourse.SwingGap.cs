using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        public bool HasSwingGap
        {
            get
            {
                if (HasRelayGap) return false;
                if (!Shape.SwingGapEnabled || !SwingPoleMath.HasGap(FinishDistance, Shape.SwingGapStart, Shape.SwingGapLength)) return false;
                for (int i = 0; i < LaneCount; i++) if (Shape.HasSwingGapLane(i)) return true;
                return false;
            }
        }
        public bool HasSwingGapOnLane(int lane) => HasSwingGap && Shape.HasSwingGapLane(lane);
        public float SwingGapFrom => Shape.SwingGapStart;
        public float SwingGapTo => Shape.SwingGapStart + Shape.SwingGapLength;
        public bool IsSwingGap(float distance) => HasSwingGap && SwingPoleMath.Contains(distance, SwingGapFrom, Shape.SwingGapLength);
        public bool IsSwingGapOnLane(int lane, float distance) => HasSwingGapOnLane(lane) && IsSwingGap(distance);

        public bool HasRelayGap => Shape.SwingGapEnabled && Shape.RelayGapEnabled && LaneCount >= 3
            && SwingPoleMath.HasGap(FinishDistance, Shape.SwingGapStart, Shape.SwingGapLength);
        public bool IsRelayGapClosed(int lane) => HasRelayGap
            && (GetComponent<TightropeRelayGate>() is not { } gate || !gate.IsLaneConnected(lane));
        public bool IsRelayGapClosed(int lane, float distance) => IsRelayGapClosed(lane)
            && SwingPoleMath.Contains(distance, SwingGapFrom, Shape.SwingGapLength);

        public bool HasTrapezeGap => Shape.TrapezeEnabled && LaneCount >= 1 && float.IsFinite(Shape.TrapezeStart)
            && Shape.TrapezeStart >= 10f && Shape.TrapezeStart + Shape.TrapezeLength + 10f < FinishDistance
            && (!Shape.SwingGapEnabled || Shape.TrapezeStart > SwingGapTo + 10f);
        public float TrapezeGapFrom => Shape.TrapezeStart;
        public float TrapezeGapTo => Shape.TrapezeStart + Shape.TrapezeLength;
        public bool IsTrapezeGapOnLane(int lane) => HasTrapezeGap && lane >= 0 && lane < LaneCount;
        public bool IsTrapezeGapOnLane(int lane, float distance) => IsTrapezeGapOnLane(lane)
            && SwingPoleMath.Contains(distance, TrapezeGapFrom, Shape.TrapezeLength);

        /// <summary>호스트가 공유한 해제봉 상태에 맞춰 메시와 실제 지지면을 함께 갱신한다.</summary>
        public void RefreshRelayGeometry()
        {
            for (int lane = 0; lane < LaneCount; lane++) RefreshLaneGeometry(lane);
            SegmentsChanged?.Invoke();
        }

    }
}
