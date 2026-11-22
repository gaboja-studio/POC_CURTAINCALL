using System.Collections.Generic;
using CurtainCall.Damage;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeExitSaw
    {
        CharacterController ignoredCarrierBody;
        readonly List<Collider> carriedColliders = new();
        readonly List<bool> previousIgnoredPairs = new();
        readonly HashSet<Collider> ignoredRopes = new();
        TightropeCourse ignoredCourse;

        // Retain the previous yaw if the turn clips a post. Then resolve the translation along
        // the contact plane, still checking the whole sweep against real colliders.
        bool TryContactTranslation(Vector3 hand, NetworkPlayer carrier, bool allowSlide, out Vector3 position)
        {
            var root = CarryRoot;
            position = root.position + hand - Handle.position;
            if (HumanCannonCarry.CheckMove(root, loadCollider, position, root.rotation, carrier,
                true, 0f, null, true, out var obstruction)) return true;
            if (!allowSlide) return false;
            var normal = Vector3.ProjectOnPlane(obstruction, Vector3.up).normalized;
            var delta = position - root.position;
            float into = Vector3.Dot(delta, normal);
            if (into >= 0f) return false;
            position -= normal * into;
            return HumanCannonCarry.CanMove(root, loadCollider, position, root.rotation, carrier,
                true, 0f, null, allowExistingOverlap: true);
        }

        bool TryFollowAlongContact(NetworkPlayer carrier, out Vector3 position, out Quaternion rotation, out Quaternion carrierRotation)
        {
            rotation = CarryRoot.rotation;
            carrierRotation = carrier.transform.rotation;
            var hand = carrier.transform.position + Vector3.up * Mathf.Max(0, carryHandleHeight);
            bool canCorrect = carrier.IsLocal && carrier.TryGetComponent(out PlayerMover mover)
                && mover.Simulated && mover.Body && mover.Body.enabled;
            if (!TryContactTranslation(hand, carrier, canCorrect, out position)) return false;

            var correction = Vector3.ProjectOnPlane(Handle.position + position - CarryRoot.position - hand, Vector3.up);
            if (canCorrect && correction.sqrMagnitude > .000001f)
            {
                carrier.GetComponent<PlayerMover>().Body.Move(correction);
                // The player's own capsule may have hit a second wall during correction.
                hand = carrier.transform.position + Vector3.up * Mathf.Max(0, carryHandleHeight);
                if (!TryContactTranslation(hand, carrier, false, out position)) return false;
            }
            var facing = Vector3.ProjectOnPlane(CarryRoot.position - Handle.position, Vector3.up);
            if (facing.sqrMagnitude > .000001f) carrierRotation = Quaternion.LookRotation(facing);
            return true;
        }

        bool HasSupport(Vector3 feet, float maxFootGap)
        {
            var origin = feet + Vector3.up * .15f;
            float distance = .15f + maxFootGap;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, floorHits, distance, ~0, QueryTriggerInteraction.Ignore);
            var hits = count == floorHits.Length
                ? Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore) : floorHits;
            if (hits != floorHits) count = hits.Length;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.normal.y > .5f && IsSawSupport(hit.collider)) return true;
            }
            return false;
        }

        bool IsSawSupport(Collider collider) => collider && (!OfficialEscapeScene || collider.gameObject.scene == gameObject.scene)
            && !collider.transform.IsChildOf(CarryRoot)
            && !collider.GetComponentInParent<NetworkPlayer>() && !collider.GetComponentInParent<EscapeExitSaw>()
            && !DismemberedLimb.Contains(collider) && !collider.GetComponentInParent<EscapePursuerDebris>();

        bool HasFootprintSupport(PlayerMover mover, Vector3 feet, float maxFootGap)
        {
            if (HasSupport(feet, maxFootGap)) return true;
            // A narrow panel seam can leave the centre ray unsupported while the footprint still touches.
            var scene = OfficialEscapeScene ? gameObject.scene : default;
            return WalkableSupport.TryGetSupport(mover, feet, scene, out var support, IsSawSupport)
                && Mathf.Abs(feet.y - support.point.y) <= maxFootGap && IsSawSupport(support.collider);
        }

        bool CanLeaveAtCurrentPose(NetworkPlayer carrier)
        {
            if (!Grounded(carrier, requireReportedGrounded: carrier.IsLocal)
                || loadCollider && !PoseFits(CarryRoot.position, CarryRoot.rotation, null)) return false;
            if (!ProjectileMode) return true;
            var dropFeet = loadCollider ? loadCollider.transform.TransformPoint(loadCollider.center) : CarryRoot.position;
            dropFeet.y = RouteFeet(carrier).y;
            return HasFootprintSupport(null, dropFeet, .25f);
        }

        void RecoverCarryPose()
        {
            var position = recoveryAnchor != null ? recoveryAnchor.position : initialPosition;
            var rotation = recoveryAnchor != null ? recoveryAnchor.rotation : initialRotation;
            if (loadCollider != null && CarryRoot.gameObject.activeInHierarchy && !PoseFits(position, rotation, null))
            {
                // Authored recovery anchors normally work. Search only if a player or another solid
                // load is occupying the anchor, and never recover onto loose debris or over a drop.
                var right = rotation * Vector3.right;
                var forward = rotation * Vector3.forward;
                for (int ring = 1; ring <= 2; ring++)
                {
                    float offset = ring * .8f;
                    for (int side = 0; side < 4; side++)
                    {
                        var direction = side == 0 ? right : side == 1 ? -right : side == 2 ? forward : -forward;
                        var candidate = position + Vector3.ProjectOnPlane(direction, Vector3.up).normalized * offset;
                        if (!HasSupport(candidate, .25f) || !PoseFits(candidate, rotation, null)) continue;
                        CarryRoot.SetPositionAndRotation(candidate, rotation);
                        return;
                    }
                }
            }
            CarryRoot.SetPositionAndRotation(position, rotation);
        }

        // Only the current bearer ignores their own held machine. Its colliders still block the world and other players.
        void RefreshCarrierCollision(NetworkPlayer carrier)
        {
            var body = carrier != null && carrier.TryGetComponent(out PlayerMover mover) ? mover.Body : null;
            if (body != ignoredCarrierBody)
            {
                RestoreCarrierCollision();
                if (body == null) return;
                ignoredCarrierBody = body;
                CarryRoot.GetComponentsInChildren(true, carriedColliders);
                for (int i = carriedColliders.Count - 1; i >= 0; i--)
                    if (carriedColliders[i] == body || carriedColliders[i].isTrigger) carriedColliders.RemoveAt(i);
                foreach (var collider in carriedColliders) previousIgnoredPairs.Add(Physics.GetIgnoreCollision(body, collider));
            }
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) return;
            var course = TightropeCourse.Current;
            if (!GameSettings.Base.CarryDevices.BlockRopeWithSaw || course == null || !course.IsTraversalActive) course = null;
            if (ignoredCourse != course) RestoreRopeCollision();
            if (course != null)
            {
                ignoredCourse = course;
                course.IgnoreRopeCollision(body, ignoredRopes);
            }
            // CharacterController re-enabling clears IgnoreCollision, including respawn and inspection transitions.
            foreach (var collider in carriedColliders)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(body, collider, true);
        }

        void RestoreCarrierCollision()
        {
            RestoreRopeCollision();
            if (ignoredCarrierBody != null)
                for (int i = 0; i < carriedColliders.Count; i++)
                    if (carriedColliders[i] != null)
                        Physics.IgnoreCollision(ignoredCarrierBody, carriedColliders[i], previousIgnoredPairs[i]);
            ignoredCarrierBody = null;
            carriedColliders.Clear(); previousIgnoredPairs.Clear();
        }

        void RestoreRopeCollision()
        {
            if (ignoredCarrierBody != null)
                foreach (var rope in ignoredRopes)
                    if (rope != null) Physics.IgnoreCollision(ignoredCarrierBody, rope, false);
            ignoredRopes.Clear();
            ignoredCourse = null;
        }
    }
}
