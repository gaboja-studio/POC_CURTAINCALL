using System;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>Host-side hazard contact against the standing or crawling player capsule.</summary>
    public static class CannonHazardGeometry
    {
        /// <summary>수직 플레이어 캡슐과 축 정렬 낙하물/불꽃 체적. 경고만 표시하는 시간에는 호출하지 않는다.</summary>
        public static bool BoxTouches(Vector3 center, Vector3 size, Vector3 feet, float height, float radius)
        {
            if (!Finite(center) || !Finite(size) || !Finite(feet) || !Dimension(height) || !Dimension(radius)
                || size.x <= 0 || size.y <= 0 || size.z <= 0) return false;
            double dx = Math.Max(0, Math.Abs((double)feet.x-center.x)-size.x*.5);
            double dz = Math.Max(0, Math.Abs((double)feet.z-center.z)-size.z*.5);
            double bottom = (double)feet.y+radius, top = (double)feet.y+Math.Max(radius,height-radius);
            double dy = Math.Max(0, Math.Max(center.y-size.y*.5-top, bottom-center.y-size.y*.5));
            return dx*dx+dy*dy+dz*dz <= (double)radius*radius;
        }

        public static bool SphereTouches(Vector3 center, float hazardRadius, Vector3 feet,
            float bodyHeight, float bodyRadius) =>
            SegmentTouches(center, center, hazardRadius, feet, bodyHeight, bodyRadius);

        public static bool SegmentTouches(Vector3 start, Vector3 end, float hazardRadius,
            Vector3 feet, float bodyHeight, float bodyRadius)
        {
            if (!Finite(start) || !Finite(end) || !Finite(feet) || !Dimension(hazardRadius) ||
                !Dimension(bodyHeight) || !Dimension(bodyRadius)) return false;
            var bottom = new Point(feet.x, (double)feet.y + bodyRadius, feet.z);
            var top = new Point(feet.x, (double)feet.y + Math.Max(bodyRadius,
                (double)bodyHeight - bodyRadius), feet.z);
            double radius = (double)hazardRadius + bodyRadius;
            return DistanceSquared(new Point(start), new Point(end), bottom, top) <= radius * radius;
        }

        static double DistanceSquared(Point p, Point q, Point a, Point b)
        {
            Point first = q - p, second = b - a, offset = p - a;
            double firstLength = Dot(first, first), secondLength = Dot(second, second);
            double towardSecond = Dot(second, offset), s, t;
            if (firstLength == 0 && secondLength == 0) return Dot(offset, offset);
            if (firstLength == 0)
            {
                s = 0;
                t = Clamp(towardSecond / secondLength);
            }
            else
            {
                double towardFirst = Dot(first, offset);
                if (secondLength == 0)
                {
                    t = 0;
                    s = Clamp(-towardFirst / firstLength);
                }
                else
                {
                    double alignment = Dot(first, second);
                    double denominator = firstLength * secondLength - alignment * alignment;
                    s = denominator > 0 ? Clamp((alignment * towardSecond -
                        towardFirst * secondLength) / denominator) : 0;
                    t = (alignment * s + towardSecond) / secondLength;
                    if (t < 0) { t = 0; s = Clamp(-towardFirst / firstLength); }
                    else if (t > 1) { t = 1; s = Clamp((alignment - towardFirst) / firstLength); }
                }
            }
            Point separation = offset + first * s - second * t;
            return Dot(separation, separation);
        }

        static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
        static bool Dimension(float value) => value >= 0 && Finite(value);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static double Dot(Point a, Point b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        // Double arithmetic avoids overflow even when finite float coordinates are very large.
        readonly struct Point
        {
            public readonly double X, Y, Z;
            public Point(double x, double y, double z) { X = x; Y = y; Z = z; }
            public Point(Vector3 value) : this(value.x, value.y, value.z) { }
            public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static Point operator *(Point a, double scale) => new Point(a.X * scale, a.Y * scale, a.Z * scale);
        }
    }
}
