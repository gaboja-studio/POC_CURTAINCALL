using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>Resolve an intruding door/prop horizontally, without crossing another wall or changing floors.</summary>
    public static class PursuitSeparation
    {
        static readonly Collider[] overlaps = new Collider[64];
        static readonly RaycastHit[] sweeps = new RaycastHit[64];
        static readonly List<Collider> intruders = new();
        static readonly Vector3[] directions = {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(1,0,1).normalized, new Vector3(-1,0,1).normalized,
            new Vector3(1,0,-1).normalized, new Vector3(-1,0,-1).normalized
        };

        static bool Solid(Collider c, Transform self) => c != null && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy
            && !c.transform.IsChildOf(self) && c.GetComponentInParent<PursuitActor>() == null
            && c.GetComponentInParent<PlayerMover>() == null && !CurtainCall.Damage.DismemberedLimb.Contains(c);
        static bool Moving(Collider c) => c.attachedRigidbody != null || c.GetComponentInParent<PursuitDoorObstacle>() != null
            || c.GetComponentInParent<ExplorationSlide>() != null || c.GetComponentInParent<CurtainCall.HumanCannon.HumanCannonTrampoline>() != null
            || c.GetComponentInParent<CurtainCall.EscapeRoute.EscapeExitSaw>() != null;

        static void Ends(CapsuleCollider body, Vector3 feet, out Vector3 a, out Vector3 b)
        {
            var center = feet + body.center;
            float half = Mathf.Max(0, body.height * .5f - body.radius);
            a = center - Vector3.up * half; b = center + Vector3.up * half;
        }
        static bool Penetrates(CapsuleCollider body, Vector3 feet, Collider c, out Vector3 direction, out float depth)
            => Physics.ComputePenetration(body, feet, Quaternion.identity, c, c.transform.position, c.transform.rotation, out direction, out depth) && depth > .003f;

        public static bool Clear(CapsuleCollider body, Vector3 feet, Transform self)
        {
            bool enabled = body.enabled;
            try { body.enabled = true; return ClearEnabled(body, feet, self); }
            finally { body.enabled = enabled; }
        }
        static bool ClearEnabled(CapsuleCollider body, Vector3 feet, Transform self)
        {
            Ends(body, feet, out var a, out var b);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, body.radius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (n == overlaps.Length) return false;
            for (int i = 0; i < n; i++) if (Solid(overlaps[i], self) && Penetrates(body, feet, overlaps[i], out _, out _)) return false;
            return true;
        }
        static bool Passage(CapsuleCollider body, Vector3 from, Vector3 to, Transform self)
        {
            var delta = to - from;
            Ends(body, from, out var a, out var b);
            int n = Physics.CapsuleCastNonAlloc(a, b, body.radius, delta.normalized, sweeps, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (n == sweeps.Length) return false;
            for (int i = 0; i < n; i++) if (Solid(sweeps[i].collider, self) && !intruders.Contains(sweeps[i].collider)) return false;
            return true;
        }
        static bool Grounded(Vector3 feet, Transform self)
        {
            foreach (var hit in Physics.RaycastAll(feet + Vector3.up * .15f, Vector3.down, .45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (Solid(hit.collider, self) && hit.normal.y > .6f) return true;
            return false;
        }

        public static bool TryResolve(CapsuleCollider body, Vector3 feet, Transform self, float limit, bool movingOnly, out Vector3 result)
        {
            // PhysX ignores disabled collider geometry in ComputePenetration. Enable
            // only during this synchronous query; never leave a gameplay collider.
            bool enabled = body.enabled;
            try { body.enabled = true; return ResolveEnabled(body, feet, self, limit, movingOnly, out result); }
            finally { body.enabled = enabled; }
        }
        static bool ResolveEnabled(CapsuleCollider body, Vector3 feet, Transform self, float limit, bool movingOnly, out Vector3 result)
        {
            result = feet; intruders.Clear();
            Ends(body, feet, out var a, out var b);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, body.radius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (n == overlaps.Length) return false;
            Vector3 push = Vector3.zero;
            for (int i = 0; i < n; i++) {
                var c = overlaps[i];
                if (!Solid(c, self) || !Penetrates(body, feet, c, out var direction, out var depth)) continue;
                intruders.Add(c);
                if (!movingOnly || Moving(c)) {
                    var flat = Vector3.ProjectOnPlane(direction, Vector3.up);
                    if (flat.sqrMagnitude > .1f) push += flat.normalized * (depth / flat.magnitude + .025f);
                }
            }
            if (intruders.Count == 0 || movingOnly && !intruders.Exists(Moving)) return false;
            bool onGround = Grounded(feet, self);
            bool Valid(Vector3 p) => (p - feet).sqrMagnitude <= limit * limit && Clear(body, p, self)
                && (!onGround || Grounded(p, self)) && Passage(body, feet, p, self);
            if (push.sqrMagnitude > .0001f && Valid(feet + push)) { result = feet + push; return true; }
            // Closing shutters can have a vertical minimum translation. Search the
            // closest horizontal exit instead of pushing a pursuer through the floor.
            for (float distance = .1f; distance <= limit + .001f; distance += .1f)
                foreach (var direction in directions) {
                    var candidate = feet + direction * distance;
                    if (!Valid(candidate)) continue;
                    result = candidate; return true;
                }
            return false; // A fully sealed space must never teleport through a wall.
        }
    }
}
