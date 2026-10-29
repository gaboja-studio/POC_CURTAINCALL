using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        /// <summary>기존 씬 줄의 양끝 근처인지 확인한다. 공중의 같은 진행축도 허용하되 옆줄 밖은 제외한다.</summary>
        internal bool IsWithinCannonPoleExit(Vector3 position, float bodyRadius, float endDistance)
        {
            if (!useSceneGeometry || !float.IsFinite(endDistance) || endDistance <= 0f
                || !TryGetRopePoint(position, out int lane, out float along) || !IsLaneUsable(lane, along)) return false;
            float length = Layout.GetLaneEnd(lane);
            if (HasIndependentRopes)
            {
                if (!IsSceneLineActive(lane) || !SceneLine(lane, out _, out _, out length)) return false;
            }
            if (Mathf.Min(along, length - along) > endDistance) return false;
            var point = GetLanePosition(lane, along);
            float sideDistance = Vector3.ProjectOnPlane(position - point, Vector3.up).magnitude;
            return sideDistance <= Shape.RopeWalkWidth * .5f + Mathf.Max(0f, bodyRadius);
        }
    }
}
