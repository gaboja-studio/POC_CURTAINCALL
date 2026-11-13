using System;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapePursuerBoss
    {
        [Serializable]
        public sealed class SeverablePart
        {
            public string label;
            public BoxCollider hitbox;
            public Renderer[] meshes = Array.Empty<Renderer>();
        }
        // Left/right foot, left/right hand, left/right leg, left/right arm.
        [SerializeField] SeverablePart[] parts = Array.Empty<SeverablePart>();
        public int PartCount => parts.Length;
        public bool IsPart(Collider collider) => Array.Exists(parts, part => part.hitbox == collider);
        public BoxCollider Hitbox(int index) => index >= 0 && index < parts.Length ? parts[index].hitbox : null;
        public static bool Exposed(byte mask, int index) => index >= 0 && index < 8 && (mask & (1 << index)) == 0
            && (index < 4 || (mask & (1 << (index - 4))) != 0);

        public bool TryHitSaw(Vector3 from, Vector3 to, Vector3 normal, float radius, float thickness, out Vector3 contact)
        {
            contact = to;
            if (!CombatStarted || Defeated || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return false;
            int selected = -1; float first = float.PositiveInfinity;
            for (int i = 0; i < parts.Length && i < 8; i++)
            {
                if ((state.Mask & (1 << i)) != 0 || !parts[i].hitbox) continue;
                if (IntersectPart(parts[i].hitbox, from, to, normal, radius, thickness, out float t) && t < first)
                { first = t; selected = i; }
            }
            if (selected < 0) return false;
            contact = Vector3.Lerp(from, to, first);
            // A hand/foot is severed before the attached arm/leg. Markers make the next cut explicit.
            if (!Exposed(state.Mask, selected) || state.Phase == Phase.Leaping || state.Phase == Phase.LeapWarning) return true;
            state.Mask |= (byte)(1 << selected); state.HitPoint = contact;
            chase.CancelBossAttack();
            Enter(state.Mask == byte.MaxValue ? Phase.Dying : Phase.Hit, EscapeRouteTimer.Current.Clock);
            return true;
        }

        internal static bool IntersectPart(BoxCollider box, Vector3 from, Vector3 to, Vector3 normal,
            float radius, float thickness, out float enter)
        {
            enter = 0;
            if (!box || !float.IsFinite(from.sqrMagnitude) || !float.IsFinite(to.sqrMagnitude)
                || !float.IsFinite(normal.sqrMagnitude) || normal.sqrMagnitude < .001f
                || !float.IsFinite(radius) || !float.IsFinite(thickness) || radius < 0 || thickness < 0) return false;
            var tr = box.transform;
            var a = tr.InverseTransformPoint(from) - box.center;
            var delta = tr.InverseTransformVector(to - from);
            var scale = tr.lossyScale;
            float leave = 1;
            normal.Normalize();
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(scale[axis]) < .0001f) return false;
                Vector3 unit = axis == 0 ? tr.right : axis == 1 ? tr.up : tr.forward;
                float d = Mathf.Abs(Vector3.Dot(unit, normal));
                float margin = (radius * Mathf.Sqrt(Mathf.Max(0, 1-d*d)) + thickness * d) / Mathf.Abs(scale[axis]);
                float half = box.size[axis] * .5f + margin;
                if (Mathf.Abs(delta[axis]) < .00001f) { if (Mathf.Abs(a[axis]) > half) return false; continue; }
                float x = (-half - a[axis]) / delta[axis], y = (half - a[axis]) / delta[axis];
                if (x > y) (x, y) = (y, x);
                enter = Mathf.Max(enter, x); leave = Mathf.Min(leave, y);
                if (enter > leave) return false;
            }
            return true;
        }
    }
}
