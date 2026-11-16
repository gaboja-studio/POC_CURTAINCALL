using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        // Search far to near, facing the authored disposal bay first. The old near-bay
        // early return silently capped throws even when the entire chasm was clear.
        bool TryFindThrow(Vector3 release, Vector3 preferred, out Vector3 drop)
        {
            Vector3 direction = Vector3.ProjectOnPlane(preferred - release, Vector3.up).normalized;
            if (direction.sqrMagnitude < .01f) direction = transform.forward;
            float height = Mathf.Min(preferred.y, transform.position.y - 8f);
            float fall = height - release.y;
            float range = Mathf.Max(1f, Tuning.torsoThrowRange);
            float horizontal = Mathf.Sqrt(Mathf.Max(0, range * range - fall * fall));
            foreach (float fraction in ThrowRadii)
                for (int i = 0; i < 24; i++)
                {
                    // Alternate either side of the bay before considering the opposite side.
                    float angle = ((i + 1) / 2) * 15f * (i % 2 == 0 ? -1f : 1f);
                    drop = release + Quaternion.AngleAxis(angle, Vector3.up) * direction * (horizontal * fraction);
                    drop.y = height;
                    if (ClearThrow(release, drop)) return true;
                }
            drop = preferred;
            return ClearThrow(release, drop);
        }

        static readonly float[] ThrowRadii = { 1f, .85f, .65f, .45f, .25f };
        readonly Collider[] throwOverlaps = new Collider[32];

        bool ThrowObstacle(Collider obstacle) => !obstacle.transform.IsChildOf(transform)
            && !obstacle.GetComponentInParent<NetworkPlayer>();

        // Use the same arc, body centre and radius as the actual in-flight collision sweep.
        // A clear straight hand ray is not enough: the raised arc can hit a beam overhead.
        bool ClearThrowArc(Vector3 release, Vector3 drop)
        {
            float range = Tuning.torsoThrowRange;
            if ((drop - release).sqrMagnitude > range * range + .01f) return false;
            Vector3 previous = release + Vector3.up * .8f;
            int initial = Physics.OverlapSphereNonAlloc(previous, .55f, throwOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (initial == throwOverlaps.Length) return false;
            for (int i = 0; i < initial; i++) if (ThrowObstacle(throwOverlaps[i])) return false;
            for (int i = 1; i <= 32; i++)
            {
                Vector3 next = ThrowPosition(release, drop, i / 32f) + Vector3.up * .8f;
                Vector3 delta = next - previous;
                int hits = Physics.SphereCastNonAlloc(previous, .55f, delta.normalized, reachHits, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (hits == reachHits.Length) return false;
                for (int h = 0; h < hits; h++) if (ThrowObstacle(reachHits[h].collider)) return false;
                previous = next;
            }
            int overlaps = Physics.OverlapSphereNonAlloc(drop + Vector3.up * .8f, .55f, throwOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlaps == throwOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++) if (ThrowObstacle(throwOverlaps[i])) return false;
            return true;
        }

        bool ClearThrow(Vector3 release, Vector3 drop)
        {
            if (drop.y > transform.position.y - 4f || !ClearThrowArc(release, drop)) return false;
            int supports = Physics.RaycastNonAlloc(drop + Vector3.up, Vector3.down, reachHits, 5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (supports == reachHits.Length) return false;
            for (int i = 0; i < supports; i++) if (ThrowObstacle(reachHits[i].collider)) return false;
            return true;
        }
    }
}
