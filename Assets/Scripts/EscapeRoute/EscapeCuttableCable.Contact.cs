using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeCuttableCable
    {
        public bool TryIntersect(Vector3 from, Vector3 to, float projectileRadius, out float along01)
        {
            along01 = 1;
            if (cut || !Finite(from) || !Finite(to) || !float.IsFinite(projectileRadius) || projectileRadius < 0) return false;
            EnsurePath(); bool found = false;
            for (int i = 0; i < path.Length - 1; i++)
                if (SweepCapsule(from, to, transform.TransformPoint(path[i]), transform.TransformPoint(path[i + 1]),
                    projectileRadius + WorldRadius, out float t) && (!found || t < along01))
                { found = true; along01 = t; }
            return found;
        }

        public bool TryIntersectDisk(Vector3 from, Vector3 to, Vector3 diskNormal, float projectileRadius,
            float halfThickness, out float along01) => TryIntersectDisk(from, to, diskNormal, projectileRadius, halfThickness, out along01, out _);

        /// <summary>Continuous thin-cylinder versus sampled cable tube. A spherical broad phase cannot approve a cut.</summary>
        public bool TryIntersectDisk(Vector3 from, Vector3 to, Vector3 diskNormal, float projectileRadius,
            float halfThickness, out float along01, out Vector3 hitPoint)
        {
            along01 = 1; hitPoint = default;
            if (cut || !Finite(from) || !Finite(to) || !Finite(diskNormal) || diskNormal.sqrMagnitude < .00001f
                || !float.IsFinite(projectileRadius) || projectileRadius <= 0 || !float.IsFinite(halfThickness) || halfThickness < 0) return false;
            EnsurePath(); diskNormal.Normalize(); bool found = false;
            float wireRadius = WorldRadius, outer = Mathf.Sqrt(projectileRadius * projectileRadius + halfThickness * halfThickness) + wireRadius;
            Vector3 travel = to - from;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = transform.TransformPoint(path[i]), b = transform.TransformPoint(path[i + 1]);
                if (!SweepCapsule(from, to, a, b, outer, out float broad) || found && broad > along01) continue;
                float normalA = Vector3.Dot(a - from, diskNormal), normalB = Vector3.Dot(b - from, diskNormal);
                float normalTravel = Vector3.Dot(travel, diskNormal), thickness = halfThickness + wireRadius;
                if (Mathf.Min(normalA, normalB) - Mathf.Max(0, normalTravel) > thickness
                    || Mathf.Max(normalA, normalB) - Mathf.Min(0, normalTravel) < -thickness) continue;
                // Squared distance between convex shapes is convex along a translation. Minimize continuously,
                // then bisect its first zero crossing; no distance stepping that can tunnel past a thin strand.
                float lower = broad, upper = found ? along01 : 1;
                for (int iteration = 0; iteration < 24; iteration++)
                {
                    float m1 = Mathf.Lerp(lower, upper, 1f / 3), m2 = Mathf.Lerp(lower, upper, 2f / 3);
                    if (DistanceAt(m1, out _) <= DistanceAt(m2, out _)) upper = m2; else lower = m1;
                }
                float minimum = (lower + upper) * .5f;
                float best = DistanceAt(minimum, out _), endDistance = DistanceAt(found ? along01 : 1, out _);
                if (endDistance < best) { minimum = found ? along01 : 1; best = endDistance; }
                float tolerance = wireRadius * wireRadius + .0000005f;
                if (best > tolerance) continue;
                lower = 0; upper = minimum;
                if (DistanceAt(0, out _) <= tolerance) upper = 0;
                else for (int iteration = 0; iteration < 24; iteration++)
                {
                    float middle = (lower + upper) * .5f;
                    if (DistanceAt(middle, out _) <= tolerance) upper = middle; else lower = middle;
                }
                DistanceAt(upper, out Vector3 point);
                if (!found || upper < along01) { found = true; along01 = upper; hitPoint = point; }

                float DistanceAt(float time, out Vector3 point)
                {
                    Vector3 center = from + travel * time;
                    Vector3 segment = b - a;
                    float lo = 0, hi = 1, along;
                    // The derivative of squared distance to a convex cylinder is monotone on a line.
                    // Endpoint tests resolve most short authored segments without an iterative search.
                    if (Derivative(a - center) >= 0) along = 0;
                    else if (Derivative(b - center) <= 0) along = 1;
                    else
                    {
                        for (int iteration = 0; iteration < 16; iteration++)
                        {
                            float middle = (lo + hi) * .5f;
                            if (Derivative(Vector3.Lerp(a, b, middle) - center) >= 0) hi = middle; else lo = middle;
                        }
                        along = (lo + hi) * .5f;
                    }
                    point = Vector3.Lerp(a, b, along);
                    return ToDisk(point - center);
                    float Derivative(Vector3 offset)
                    {
                        float axial = Vector3.Dot(offset, diskNormal);
                        Vector3 radial = offset - diskNormal * axial;
                        float radialLength = radial.magnitude;
                        float value = Mathf.Max(0, Mathf.Abs(axial) - halfThickness) * Mathf.Sign(axial) * Vector3.Dot(segment, diskNormal);
                        if (radialLength > projectileRadius)
                            value += (1 - projectileRadius / radialLength) * Vector3.Dot(segment, radial);
                        return value;
                    }
                }
                float ToDisk(Vector3 offset)
                {
                    float axial = Vector3.Dot(offset, diskNormal);
                    float axialExcess = Mathf.Max(0, Mathf.Abs(axial) - halfThickness);
                    float radialExcess = Mathf.Max(0, (offset - diskNormal * axial).magnitude - projectileRadius);
                    return axialExcess * axialExcess + radialExcess * radialExcess;
                }
            }
            return found;
        }

        static bool SweepCapsule(Vector3 from, Vector3 to, Vector3 a, Vector3 b, float radius, out float along)
        {
            along = 1;
            if ((from - Vector3.Lerp(a, b, ClosestAlong(from, a, b))).sqrMagnitude <= radius * radius) { along = 0; return true; }
            Vector3 delta = to - from; float length = delta.magnitude;
            if (length < .000001f) return false;
            Vector3 direction = delta / length, axis = b - a, relative = from - a;
            float aa = axis.sqrMagnitude, ar = Vector3.Dot(axis, direction), ao = Vector3.Dot(axis, relative);
            float qa = aa - ar * ar, qb = aa * Vector3.Dot(direction, relative) - ao * ar;
            float qc = aa * relative.sqrMagnitude - ao * ao - radius * radius * aa;
            float best = float.PositiveInfinity, discriminant = qb * qb - qa * qc;
            if (qa > .0000001f && discriminant >= 0)
            {
                float t = (-qb - Mathf.Sqrt(discriminant)) / qa, y = ao + t * ar;
                if (t >= 0 && y >= 0 && y <= aa) best = t;
            }
            Sphere(a); Sphere(b);
            if (best > length) return false;
            along = Mathf.Clamp01(best / length); return true;
            void Sphere(Vector3 center)
            {
                Vector3 offset = from - center; float projection = Vector3.Dot(direction, offset);
                float determinant = projection * projection - offset.sqrMagnitude + radius * radius;
                if (determinant < 0) return;
                float t = -projection - Mathf.Sqrt(determinant);
                if (t >= 0) best = Mathf.Min(best, t);
            }
        }
    }
}
