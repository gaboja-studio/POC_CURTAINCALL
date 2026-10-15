using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>이동하는 수평 받침면과 플레이어 발 위치 사이의 하향 교차를 검사한다.</summary>
    public static class TrampolineContact
    {
        public static bool TryCross(Vector3 from, Vector3 to, Vector3 previousSurface, Vector3 surface,
            Vector2 halfSize, float bodyRadius, out Vector3 contact) =>
            TryCross(from, to, previousSurface, surface, Quaternion.identity, Quaternion.identity,
                halfSize, bodyRadius, out contact);

        public static bool TryCross(Vector3 from, Vector3 to, Vector3 previousSurface, Vector3 surface,
            Quaternion previousRotation, Quaternion rotation,
            Vector2 halfSize, float bodyRadius, out Vector3 contact)
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
            float reachX = halfSize.x + bodyRadius;
            float reachZ = halfSize.y + bodyRadius;
            if (!Finite(feetAtContact) || !Finite(surfaceAtContact) || !Finite(reachX) || !Finite(reachZ)
                || !Finite(localContact) || Mathf.Abs(localContact.x) > reachX
                || Mathf.Abs(localContact.z) > reachZ) return false;

            contact = feetAtContact;
            contact.y = surfaceAtContact.y;
            return true;
        }

        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z)
            && Finite(value.w) && Quaternion.Dot(value, value) > 0.0001f;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
