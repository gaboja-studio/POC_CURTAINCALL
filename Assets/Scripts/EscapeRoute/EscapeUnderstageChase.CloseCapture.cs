using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        SkinnedMeshRenderer closeCaptureModel;
        bool searchedCaptureModel;
        Bounds closeCaptureBounds;
        readonly Collider[] closeCaptureOverlaps = new Collider[32];
        readonly RaycastHit[] closeCaptureHits = new RaycastHit[32];

        // Use the authored torso, not the animated hand/extended arm bounds. Cache
        // its local envelope so a grab animation cannot enlarge the contact area.
        void FindCloseCaptureModel()
        {
            if (searchedCaptureModel) return;
            searchedCaptureModel = true;
            if (!doll) return;
            foreach (var skin in doll.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name == "Body")
                { closeCaptureModel = skin; closeCaptureBounds = skin.localBounds; break; }
        }

        bool CloseCaptureCandidate(NetworkPlayer player, out Vector3 contact)
        {
            contact = default;
            if (!player || !player.IsSpawned || player.State != PlayerState.Normal
                || WaitingForConveyor(player.OwnerClientId) || !player.TryGetComponent(out PlayerMover mover)) return false;
            var shell = body ? body : GetComponent<CapsuleCollider>();
            if (!shell || !shell.enabled) return false;
            Vector3 feet = PlayerFeet(player);
            if (!float.IsFinite(feet.sqrMagnitude) || InsideRefuge(feet)) return false;
            var scale = shell.transform.lossyScale;
            Vector3 center = shell.transform.TransformPoint(shell.center);
            float radius = shell.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, shell.height * Mathf.Abs(scale.y) * .5f);
            float height = mover.BodyHeight, playerRadius = mover.BodyRadius;
            float margin = Mathf.Max(0, Tuning.closeCaptureDistance);
            if (!float.IsFinite(height + playerRadius + margin) || height <= 0
                || feet.y + height < center.y - halfHeight || feet.y > center.y + halfHeight) return false;

            FindCloseCaptureModel();
            if (closeCaptureModel)
            {
                var frame = closeCaptureModel.transform;
                // One circle around the actor, with the same reach on every side.
                // Model width/depth and scale size it; facing never changes it.
                float width = Vector3.ProjectOnPlane(frame.TransformVector(Vector3.right * closeCaptureBounds.extents.x), Vector3.up).magnitude;
                float depth = Vector3.ProjectOnPlane(frame.TransformVector(Vector3.forward * closeCaptureBounds.extents.z), Vector3.up).magnitude;
                float offset = Vector3.ProjectOnPlane(frame.TransformPoint(closeCaptureBounds.center) - center, Vector3.up).magnitude;
                radius = Mathf.Max(radius, Mathf.Max(width, depth) + offset);
            }
            var planar = Vector3.ProjectOnPlane(feet - center, Vector3.up);
            float reach = radius + margin + playerRadius;
            if (planar.sqrMagnitude > reach * reach) return false;

            contact = feet + Vector3.up * (height * .5f);
            Vector3 origin = new Vector3(center.x,
                Mathf.Clamp(contact.y, center.y - halfHeight + .1f, center.y + halfHeight - .1f), center.z);
            // A tall torso can overlap the height of a different storey. The feet
            // connection must also be open, so the model cannot grab through its floor.
            Vector3 baseOrigin = new Vector3(center.x, center.y - halfHeight + .1f, center.z);
            return ClearCloseCapture(origin, contact) && ClearCloseCapture(baseOrigin, feet + Vector3.up * .1f);
        }

        bool CloseCaptureObstacle(Collider hit) => hit && !hit.transform.IsChildOf(transform)
            && !hit.GetComponentInParent<NetworkPlayer>();

        bool ClearCloseCapture(Vector3 origin, Vector3 contact)
        {
            // The small sweep checks a real barrier, even for a crawling amputee.
            // Check the origin too: SphereCast alone misses a wall enclosing it.
            const float skin = .04f;
            int overlaps = Physics.OverlapSphereNonAlloc(origin, skin, closeCaptureOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlaps == closeCaptureOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++) if (CloseCaptureObstacle(closeCaptureOverlaps[i])) return false;
            Vector3 delta = contact - origin;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = Physics.SphereCastNonAlloc(origin, skin, delta.normalized, closeCaptureHits,
                delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == closeCaptureHits.Length) return false;
            for (int i = 0; i < count; i++) if (CloseCaptureObstacle(closeCaptureHits[i].collider)) return false;
            return true;
        }

        bool TryCloseCapture(double now, ref Vector3 watch)
        {
            if (!CanUseAttackHand || HoldingRunner || jumping || encounterJump || returningJump
                || !CanAttackFromRoute && !(boss && boss.InBattle)) return false;
            NetworkPlayer nearest = null;
            Vector3 contact = default;
            float best = float.PositiveInfinity;
            foreach (var player in NetworkPlayer.All)
            {
                if (!CloseCaptureCandidate(player, out var point)) continue;
                float distance = (point - transform.position).sqrMagnitude;
                if (distance < best) { best = distance; nearest = player; contact = point; }
            }
            if (!nearest || !nearest.ServerBeginEscapeCapture()) return false;
            victim = nearest;
            Vector3 capturedFeet = PlayerFeet(nearest);
            ReleaseDevices(nearest.OwnerClientId);
            watch = Vector3.ProjectOnPlane(contact - transform.position, Vector3.up);
            attack = new AttackCue { Phase = AttackPhase.Grab, Actor = nearest.OwnerClientId,
                Reach = Reach.Hall, Started = now, Origin = contact, Release = contact,
                Aim = capturedFeet };
            if (fullRoute) fullRoute.MarkPressured(nearest);
            previousExtension = 0;
            nextSend = 0;
            return true;
        }
    }
}
