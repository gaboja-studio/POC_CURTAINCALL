using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>장치 전체와 몸통의 이동 접촉 및 기존 받침면의 하향 교차를 검사한다.</summary>
    public static class TrampolineContact
    {
        /// <summary>장치 전체 외형과 몸통의 이동 접촉. 상승/하강/옆 진입을 같은 범위로 검사한다.</summary>
        public static bool TryTouch(Vector3 from, Vector3 to, Vector3 previousSurface, Vector3 surface,
            Quaternion previousRotation, Quaternion rotation, Bounds device, float bodyRadius, float bodyHeight, out Vector3 contact)
        {
            contact = Vector3.zero;
            if (!Finite(from) || !Finite(to) || !Finite(previousSurface) || !Finite(surface)
                || !Finite(previousRotation) || !Finite(rotation) || !Finite(device.center) || !Finite(device.size)
                || device.size.x <= 0f || device.size.y <= 0f || device.size.z <= 0f
                || !Finite(bodyRadius) || bodyRadius < 0f || !Finite(bodyHeight) || bodyHeight < 0f) return false;
            float halfHeight = Mathf.Max(bodyRadius, bodyHeight * .5f);
            Quaternion before = Quaternion.Inverse(previousRotation), after = Quaternion.Inverse(rotation);
            Vector3 start = before * (from + Vector3.up * halfHeight - previousSurface);
            Vector3 end = after * (to + Vector3.up * halfHeight - surface);
            Vector3 axis = Vector3.Max(Abs(before * Vector3.up), Abs(after * Vector3.up));
            var expanded = device;
            expanded.Expand(2f * (Vector3.one * bodyRadius + axis * (halfHeight - bodyRadius)));
            Vector3 motion = end - start;
            float fraction = 0f;
            if (!expanded.Contains(start))
            {
                float length = motion.magnitude;
                if (length <= .000001f || !expanded.IntersectRay(new Ray(start, motion / length), out float distance)
                    || distance > length) return false;
                fraction = Mathf.Clamp01(distance / length);
            }
            Vector3 hit = Vector3.Lerp(start, end, fraction);
            // 어느 부위로 닿아도 외형 최상단에서 출발시켜 다음 프레임의 연속 겹침 반동을 막는다.
            Vector3 landing = new(Mathf.Clamp(hit.x, device.min.x, device.max.x), device.max.y,
                Mathf.Clamp(hit.z, device.min.z, device.max.z));
            contact = surface + rotation * landing;
            return Finite(contact);
        }

        static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        public static bool TryCross(Vector3 from, Vector3 to, Vector3 previousSurface, Vector3 surface,
            Vector2 halfSize, float bodyRadius, out Vector3 contact) =>
            TryCross(from, to, previousSurface, surface, Quaternion.identity, Quaternion.identity,
                halfSize, bodyRadius, out contact);

        public static bool TryCross(Vector3 from, Vector3 to, Vector3 previousSurface, Vector3 surface,
            Quaternion previousRotation, Quaternion rotation,
            Vector2 halfSize, float bodyRadius, out Vector3 contact)
            => TryCross(from, to, previousSurface, surface, previousRotation, rotation,
                halfSize, bodyRadius, false, out contact);

        public static bool TryCross(Vector3 from, Vector3 to, Vector3 previousSurface, Vector3 surface,
            Quaternion previousRotation, Quaternion rotation,
            Vector2 halfSize, float bodyRadius, bool circular, out Vector3 contact)
        {
            contact = Vector3.zero;
            if (!Finite(from) || !Finite(to) || !Finite(previousSurface) || !Finite(surface)
                || !Finite(halfSize.x) || !Finite(halfSize.y) || halfSize.x <= 0f || halfSize.y <= 0f
                || !Finite(bodyRadius) || bodyRadius < 0f
                || !Finite(previousRotation) || !Finite(rotation)) return false;

            float before = from.y - previousSurface.y;
            float after = to.y - surface.y;
            if (!Finite(before) || !Finite(after) || before < 0f || after > 0f) return false;
            float descent = before - after;
            if (!Finite(descent) || descent <= 0f) return false; // 면 위에 계속 머무는 것은 새 진입이 아니다.

            float fraction = before / descent;
            Vector3 feetAtContact = Vector3.LerpUnclamped(from, to, fraction);
            Vector3 surfaceAtContact = Vector3.LerpUnclamped(previousSurface, surface, fraction);
            Quaternion rotationAtContact = Quaternion.Slerp(previousRotation, rotation, fraction);
            Vector3 localContact = Quaternion.Inverse(rotationAtContact) * (feetAtContact - surfaceAtContact);
            if (!Finite(feetAtContact) || !Finite(surfaceAtContact) || !Finite(localContact)
                || !Contains(new Vector2(localContact.x, localContact.z), halfSize, bodyRadius, circular)) return false;

            contact = feetAtContact;
            contact.y = surfaceAtContact.y;
            return true;
        }

        /// <summary>Circle uses the smaller half-size as its radius; callers use the same shape for host validation.</summary>
        public static bool Contains(Vector2 point, Vector2 halfSize, float margin, bool circular)
        {
            if (!Finite(point.x) || !Finite(point.y) || !Finite(halfSize.x) || !Finite(halfSize.y)
                || halfSize.x <= 0f || halfSize.y <= 0f || !Finite(margin) || margin < 0f) return false;
            if (circular)
            {
                double reach = (double)Mathf.Min(halfSize.x, halfSize.y) + margin;
                return (double)point.x * point.x + (double)point.y * point.y <= reach * reach;
            }
            float reachX = halfSize.x + margin, reachZ = halfSize.y + margin;
            return Finite(reachX) && Finite(reachZ) && Mathf.Abs(point.x) <= reachX && Mathf.Abs(point.y) <= reachZ;
        }

        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z)
            && Finite(value.w) && Quaternion.Dot(value, value) > 0.0001f;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
