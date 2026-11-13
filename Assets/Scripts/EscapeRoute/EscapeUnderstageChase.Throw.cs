using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        // The authored bay is preferred. Runtime checks also account for moving facility machinery.
        bool TryFindThrow(Vector3 release, Vector3 preferred, out Vector3 drop)
        {
            drop = preferred;
            if (ClearThrow(release, drop)) return true;
            foreach (float radius in ThrowRadii)
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI / 12f;
                    drop = transform.position + new Vector3(Mathf.Cos(angle) * radius, -8f, Mathf.Sin(angle) * radius);
                    if (ClearThrow(release, drop)) return true;
                }
            return false;
        }

        static readonly float[] ThrowRadii = { 7f, 10f, 14f, 20f };
        readonly Collider[] throwOverlaps = new Collider[32];

        bool ThrowObstacle(Collider obstacle) => !obstacle.transform.IsChildOf(transform)
            && !obstacle.GetComponentInParent<NetworkPlayer>();

        bool ClearThrow(Vector3 release, Vector3 drop)
        {
            if (drop.y > transform.position.y - 4f || (drop - release).sqrMagnitude > 35f * 35f) return false;
            Vector3 previous = release + Vector3.up * .7f;
            for (int i = 1; i <= 32; i++)
            {
                Vector3 next = ThrowPosition(release, drop, i / 32f) + Vector3.up * .7f;
                Vector3 delta = next - previous;
                int hits = Physics.SphereCastNonAlloc(previous, .5f, delta.normalized, reachHits, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (hits == reachHits.Length) return false;
                for (int h = 0; h < hits; h++) if (ThrowObstacle(reachHits[h].collider)) return false;
                previous = next;
            }
            int overlaps = Physics.OverlapSphereNonAlloc(drop + Vector3.up * .7f, .6f, throwOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlaps == throwOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++) if (ThrowObstacle(throwOverlaps[i])) return false;
            int supports = Physics.RaycastNonAlloc(drop + Vector3.up, Vector3.down, reachHits, 5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (supports == reachHits.Length) return false;
            for (int i = 0; i < supports; i++) if (ThrowObstacle(reachHits[i].collider)) return false;
            return true;
        }
    }
}
