using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>톱날 원판(판정용). 법선이 위(+Y)면 눕힌 가로 톱날, 수평이면 세운 수직 톱날.</summary>
    public readonly struct SawDisc
    {
        public readonly Vector3 Center;
        public readonly Vector3 Normal;
        public readonly float Radius;
        public readonly float HalfThickness;

        public SawDisc(Vector3 center, Vector3 normal, float radius, float halfThickness)
        {
            Center = center;
            Normal = normal.normalized;
            Radius = radius;
            HalfThickness = halfThickness;
        }
    }

    /// <summary>플레이어 몸통(판정용). 발바닥 중심에서 위로 선 원기둥으로 본다(캡슐 끝 둥근 부분은 무시, 조금 넉넉한 판정).</summary>
    public readonly struct BodyCapsule
    {
        public readonly Vector3 Feet;
        public readonly float Height;
        public readonly float Radius;

        public BodyCapsule(Vector3 feet, float height, float radius)
        {
            Feet = feet;
            Height = height;
            Radius = radius;
        }
    }

    /// <summary>
    /// 톱날 계산(MonoBehaviour와 분리, 모든 화면이 같은 값을 낸다).
    /// 충돌은 물리 엔진 대신 원판과 몸통 사이 거리로 판정한다 — 톱날은 플레이어와만 닿고 톱날끼리·외줄은 통과한다(규칙 #8).
    /// </summary>
    public static class SawMath
    {
        /// <summary>
        /// 제자리 좌우 왕복하는 중심의 옆 좌표(코스 가운데 0, 오른쪽 +). 끝에서 즉시 반전한다.
        /// 왼쪽 시작이면 -반폭에서 오른쪽으로, 오른쪽 시작이면 +반폭에서 왼쪽으로 출발한다.
        /// </summary>
        public static float PingPongSide(float speed, float halfWidth, bool startsLeft, float elapsed)
        {
            if (halfWidth <= 0f) return 0f;
            float side = Mathf.PingPong(speed * Mathf.Max(0f, elapsed), halfWidth * 2f) - halfWidth;
            return startsLeft ? side : -side;
        }

        /// <summary>원판과 몸통이 닿았는지. 닿았으면 닿은 곳(원판 위 점)을 돌려준다.</summary>
        public static bool TryGetContact(in SawDisc disc, in BodyCapsule body, out Vector3 contact)
        {
            return Mathf.Abs(Vector3.Dot(disc.Normal, Vector3.up)) > 0.99f
                ? TryGetFlatContact(disc, body, out contact)
                : TryGetUprightContact(disc, body, out contact);
        }

        /// <summary>눕힌 원판(법선 = 위).</summary>
        static bool TryGetFlatContact(in SawDisc disc, in BodyCapsule body, out Vector3 contact)
        {
            contact = default;
            Vector2 axis = new(body.Feet.x, body.Feet.z);
            Vector2 center = new(disc.Center.x, disc.Center.z);
            Vector2 toAxis = axis - center;
            float planarDistance = toAxis.magnitude;

            float bottom = disc.Center.y - disc.HalfThickness, top = disc.Center.y + disc.HalfThickness;
            float low = body.Feet.y, high = body.Feet.y + body.Height;
            if (high < bottom || low > top) return false; // 높이가 겹치지 않음(점프로 넘음)
            if (planarDistance - disc.Radius > body.Radius) return false;

            Vector2 point = planarDistance > disc.Radius ? center + toAxis / planarDistance * disc.Radius : axis;
            float y = (Mathf.Max(bottom, low) + Mathf.Min(top, high)) * 0.5f;
            contact = new Vector3(point.x, y, point.y);
            return true;
        }

        /// <summary>세운 원판(법선 = 수평).</summary>
        static bool TryGetUprightContact(in SawDisc disc, in BodyCapsule body, out Vector3 contact)
        {
            contact = default;
            Vector3 normal = Vector3.ProjectOnPlane(disc.Normal, Vector3.up).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, normal); // 원판 면 안의 수평 방향

            Vector3 offset = body.Feet - disc.Center;
            float across = Vector3.Dot(offset, normal);  // 원판 면에서 떨어진 거리
            float along = Vector3.Dot(offset, tangent);  // 원판 면 안의 수평 위치
            float low = offset.y, high = offset.y + body.Height;

            // 몸통 높이 안에서 원판이 가장 넓은 높이(중심에 가장 가까운 높이)로 수평 거리를 잰다
            float spanLow = Mathf.Max(low, -disc.Radius), spanHigh = Mathf.Min(high, disc.Radius);
            if (spanLow > spanHigh) return false; // 높이가 겹치지 않음
            float widestY = Mathf.Clamp(0f, spanLow, spanHigh);
            float widestHalf = Mathf.Sqrt(Mathf.Max(0f, disc.Radius * disc.Radius - widestY * widestY));
            float alongGap = Mathf.Max(0f, Mathf.Abs(along) - widestHalf);
            float acrossGap = Mathf.Max(0f, Mathf.Abs(across) - disc.HalfThickness);
            if (alongGap * alongGap + acrossGap * acrossGap > body.Radius * body.Radius) return false;

            float x = Mathf.Clamp(along, -widestHalf, widestHalf);
            float y = widestY;
            float axisHalf = Mathf.Sqrt(Mathf.Max(0f, disc.Radius * disc.Radius - along * along));
            if (Mathf.Abs(along) <= disc.Radius && Mathf.Max(low, -axisHalf) <= Mathf.Min(high, axisHalf))
                y = (Mathf.Max(low, -axisHalf) + Mathf.Min(high, axisHalf)) * 0.5f; // 몸통 축이 원판을 지나간다: 겹친 높이의 가운데
            contact = disc.Center + tangent * x + Vector3.up * y + normal * Mathf.Clamp(across, -disc.HalfThickness, disc.HalfThickness);
            return true;
        }

        /// <summary>
        /// 닿은 곳으로 잘릴 부위를 정한다(규칙 #9). 높이: 허리 아래 다리, 위 팔. 좌우: 몸 중심에서 코스 오른쪽(<paramref name="right"/>)이 +.
        /// 가운데(0)는 오른쪽으로 본다. 이미 잃은 부위 처리(같은 종류의 남은 쪽)는 020 신체 손상(<see cref="PlayerCondition.ResolveCut"/>)이 한다.
        /// </summary>
        public static BodyPart DecidePart(Vector3 contact, in BodyCapsule body, Vector3 right, float waistHeight)
        {
            float height = contact.y - body.Feet.y;
            Vector3 offset = contact - body.Feet;
            offset.y = 0f;
            bool isRight = Vector3.Dot(offset, right) >= 0f;
            bool isArm = height >= waistHeight;

            return isArm ? (isRight ? BodyPart.RightArm : BodyPart.LeftArm) : (isRight ? BodyPart.RightLeg : BodyPart.LeftLeg);
        }
    }
}
