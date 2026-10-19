using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public static class LandingWaveMath
    {
        public static float Strength(float distance, float range, float maximum)
        {
            if (!float.IsFinite(distance) || !float.IsFinite(range) || !float.IsFinite(maximum)
                || range <= 0f || maximum <= 0f || Mathf.Abs(distance) >= range) return 0f;
            return Mathf.Clamp(maximum, 0f, 15f) * (1f - Mathf.Abs(distance) / range);
        }

        public static bool SharesSupport(IReadOnlyList<Vector2> spans, float source, float target)
        {
            if (spans == null || !float.IsFinite(source) || !float.IsFinite(target)) return false;
            foreach (var span in spans)
                if (source >= span.x && source < span.y && target >= span.x && target < span.y) return true;
            return false;
        }
    }
}
