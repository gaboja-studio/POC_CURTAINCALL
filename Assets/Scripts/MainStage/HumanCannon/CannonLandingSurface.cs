using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>지정한 수평 박스의 윗면에 발이 놓였는지 검사한다. 옆면/아래/공중 통과는 제외.</summary>
    public static class CannonLandingSurface
    {
        public static bool Contains(Vector3 localFeet, Vector3 center, Vector3 size, float heightTolerance)
        {
            if (!Finite(localFeet) || !Finite(center) || !Finite(size) || !float.IsFinite(heightTolerance)
                || size.x <= 0f || size.y <= 0f || size.z <= 0f || heightTolerance < 0f) return false;
            Vector3 offset = localFeet - center;
            Vector3 half = size * 0.5f;
            return Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.z) <= half.z
                && Mathf.Abs(offset.y - half.y) <= heightTolerance;
        }

        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
