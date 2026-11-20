using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Damage;
using CurtainCall.EscapeRoute;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>운반자가 공중에서 죽으면 트램펄린이 그 자리에서 바로 아래 지면까지 떨어진다(2026-11-05 사용자 요청).
    /// 낙사하면 시작 지점에 가장 가까운 생존자 발밑 지면으로, 대포 안이거나 발밑 지면이 없으면 2m 뒤로 옮긴다.</summary>
    public sealed partial class HumanCannonTrampoline
    {
        const float DropMinHeight = 0.05f;
        // 원격 화면은 운반자 위치 공유 지연만큼 수평으로 어긋날 수 있다. 이 안이면 확정 지점 위에서 떨어뜨린다.
        const float DropHorizontalTolerance = 2f;
        const float DropProbeDistance = 60f;
        const float SurvivorBehindDistance = 2f;
        const int SurvivorBehindSteps = 2;

        readonly RaycastHit[] dropHits = new RaycastHit[16];
        bool dropping;
        Vector3 dropTarget;
        float dropSpeed;

        /// <summary>호스트가 공유하는 위치. 떨어지는 중이면 도착할 지면 위치를 보낸다.</summary>
        Vector3 SharedPosition => dropping ? dropTarget : transform.position;

        /// <summary>낙사 때 옮길 곳: 시작 지점에 가장 가까운 생존자 발밑 지면. 공중 생존자도 그 아래 지면을 쓴다.
        /// 발밑에 둘 수 없으면 2m와 4m 뒤의 지면을 확인한다.</summary>
        bool TryFindSurvivorGround(out Vector3 ground)
        {
            ground = default;
            NetworkPlayer survivor = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in NetworkPlayer.All)
            {
                if (candidate == null || !candidate.IsSpawned || candidate.State != PlayerState.Normal) continue;
                Vector3 feet = candidate.transform.position;
                if (!float.IsFinite(feet.x) || !float.IsFinite(feet.y) || !float.IsFinite(feet.z)) continue;
                float distance = new Vector2(feet.x - initialPosition.x, feet.z - initialPosition.z).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                survivor = candidate;
            }
            if (survivor == null) return false;
            Vector3 position = survivor.transform.position;
            var cannon = HumanCannonSystem.Current;
            bool seated = cannon != null && cannon.IsSeated(survivor.OwnerClientId);
            if (!seated && TryFindPlacement(position, initialRotation, survivor, out ground)) return true;
            Vector3 forward = Vector3.ProjectOnPlane(survivor.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            // 생존자의 2m, 4m 뒤에서 트램펄린을 놓을 수 있는 지면을 찾는다.
            Vector3 behind = position;
            for (int step = 0; step < SurvivorBehindSteps; step++)
            {
                behind -= forward.normalized * SurvivorBehindDistance;
                if (TryFindPlacement(behind, initialRotation, survivor, out ground)) return true;
            }
            return false;
        }

        internal static bool IsPursuerCollider(Collider collider) => collider.GetComponentInParent<PursuitActor>() != null
            || collider.GetComponentInParent<EscapeUnderstageChase>() != null;

        /// <summary>주어진 위치 바로 아래 가장 가까운 지면. 플레이어·자기 자신·트리거·벽면은 제외한다.</summary>
        bool TryFindGround(Vector3 from, out Vector3 ground, bool avoidRope = false)
        {
            ground = from;
            Vector3 origin = from + Vector3.up * DropMinHeight;
            float distance = HumanCannonRouteSystem.Current != null
                ? Mathf.Max(0f, origin.y - HumanCannonRouteSystem.Current.FallHeight) : DropProbeDistance;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, dropHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var hits = count == dropHits.Length
                ? Physics.RaycastAll(origin, Vector3.down, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) : dropHits;
            if (hits != dropHits) count = hits.Length;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= nearest || hit.normal.y < 0.7f || hit.transform.IsChildOf(transform)
                    || hit.collider.GetComponentInParent<NetworkPlayer>() != null
                    || DismemberedLimb.Contains(hit.collider) || IsPursuerCollider(hit.collider)) continue;
                if (avoidRope && TightropeCourse.IsRopeSurface(hit.collider)) continue;
                if (Physics.GetIgnoreLayerCollision(gameObject.layer, hit.collider.gameObject.layer)) continue;
                nearest = hit.distance;
                ground = hit.point;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        bool TryFindPlacement(Vector3 from, Quaternion rotation, NetworkPlayer carrier, out Vector3 ground, bool avoidRope = false)
            => TryFindGround(from, out ground, avoidRope) && CanSettleAt(ground, rotation, carrier);

        bool TryFindDropPath(Vector3 from, Quaternion rotation, NetworkPlayer carrier, out Vector3 ground, bool avoidRope = false)
            => TryFindPlacement(from, rotation, carrier, out ground, avoidRope)
                && HumanCannonCarry.CanMoveFromPose(transform, carryBody, from, ground, rotation, carrier);

        bool TryDropHere(NetworkPlayer carrier, out Vector3 ground)
            => TryFindDropPath(transform.position, transform.rotation, carrier, out ground);

        bool TryManualDrop(Vector3 position, Quaternion rotation, NetworkPlayer player, out Vector3 ground, out Vector3 origin)
        {
            origin = position;
            if (TryFindDropPath(position, rotation, player, out ground)) return true;
            bool side = IsWaitingRoom(gameObject.scene.name)
                ? TryFindWaitingRoomDrop(position, rotation, player, out ground)
                : TryFindNearbySidePlacement(position, rotation, player, out ground, false, UsesTraversalCarryClearance(gameObject.scene.name));
            if (side) origin = new Vector3(ground.x, position.y, ground.z);
            return side;
        }

        HumanCannonOutline placementOutline;
        Material placementOutlineMaterial;
        float nextPlacementPreview;

        void PresentPlacementOutline(NetworkPlayer carrier)
        {
            if (!Application.isPlaying) return;
            if (!AllowsPlacementOutline(gameObject.scene) || carrier == null || !carrier.IsLocal || carrier.State != PlayerState.Normal)
            {
                ClearPlacementOutline();
                return;
            }
            if (Time.unscaledTime < nextPlacementPreview) return;
            nextPlacementPreview = Time.unscaledTime + .08f;
            if (!placementOutline)
            {
                placementOutline = GetComponentInChildren<HumanCannonOutline>(true);
                if (!placementOutline)
                {
                    var shader = Resources.Load<Shader>("TightropeOutline");
                    if (!shader) return;
                    placementOutlineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                    placementOutline = HumanCannonOutline.AttachToHierarchy(gameObject, placementOutlineMaterial);
                }
            }
            bool fits = carryBody && !HasRopeSupport(carrier)
                && TryManualDrop(transform.position, transform.rotation, carrier, out _, out _);
            placementOutline.SetColorOverride(fits ? new Color(.08f, 1f, .25f) : new Color(1f, .12f, .13f));
        }

        static bool AllowsPlacementOutline(UnityEngine.SceneManagement.Scene scene)
        {
            var escape = EscapeCampaignContent.Current;
            return escape == null || escape.gameObject.scene != scene;
        }

        void ClearPlacementOutline()
        {
            nextPlacementPreview = 0f;
            if (placementOutline) placementOutline.SetColorOverride(null);
            if (!placementOutlineMaterial) return;
            if (placementOutline)
            {
                placementOutline.Configure(null);
                Destroy(placementOutline);
            }
            Destroy(placementOutlineMaterial);
            placementOutlineMaterial = null;
            placementOutline = null;
        }

        bool CanSettleAt(Vector3 ground, Quaternion rotation, NetworkPlayer carrier)
        {
            if (!HumanCannonCarry.CanMove(transform, carryBody, ground, rotation, carrier, sweep: false)) return false;
            // Waiting-room actors use navigation and temporary probes, so a physical overlap alone can miss them.
            var pursuit = PursuitDirector.Current;
            if (pursuit == null) return true;
            foreach (var actor in pursuit.Actors)
            {
                if (actor == null || !actor.isActiveAndEnabled || actor.IsDead) continue;
                if (OverlapsPursuer(actor, ground, rotation)) return false;
            }
            return true;
        }

        bool OverlapsPursuer(PursuitActor actor, Vector3 ground, Quaternion rotation)
        {
            var feet = actor.transform.position;
            const float bodyRadius = .28f; // PursuitActor.Initialize uses this navigation radius.
            float height = actor.IsCrouched ? PursuitActor.CrouchBodyHeight : 1.8f * (actor.Placement?.SizeScale ?? 1f);
            float centerY = ground.y + surfaceHeight * Mathf.Abs(transform.lossyScale.y) * .5f;
            feet.y = Mathf.Clamp(centerY, feet.y + bodyRadius, feet.y + Mathf.Max(bodyRadius, height - bodyRadius));
            var nearest = Physics.ClosestPoint(feet, carryBody, ground, rotation);
            return (nearest - feet).sqrMagnitude < bodyRadius * bodyRadius;
        }

        bool TryFindNearbyPlacement(Vector3 position, Quaternion rotation, NetworkPlayer carrier, out Vector3 ground, bool avoidRope = false)
            => TryFindPlacement(position, rotation, carrier, out ground, avoidRope)
                || TryFindNearbySidePlacement(position, rotation, carrier, out ground, avoidRope);

        bool TryRestoreInitialPlacement(NetworkPlayer carrier, out Vector3 ground, bool avoidRope = false)
        {
            ground = initialPosition;
            return CanSettleAt(initialPosition, initialRotation, carrier)
                || TryFindNearbyPlacement(initialPosition, initialRotation, carrier, out ground, avoidRope);
        }

        bool TryFindNearbySidePlacement(Vector3 position, Quaternion rotation, NetworkPlayer carrier, out Vector3 ground, bool avoidRope, bool fullWidth = false)
        {
            float reach = GameSettings.Trampoline.GrabDistance;
            for (int step = 1; step <= 2; step++)
            {
                float offset = reach * step / 2f;
                Vector3 right = rotation * Vector3.right * offset;
                Vector3 forward = rotation * Vector3.forward * offset;
                if (TryFindReachablePlacement(position, position - forward, rotation, carrier, out ground, avoidRope, fullWidth)
                    || TryFindReachablePlacement(position, position + right, rotation, carrier, out ground, avoidRope, fullWidth)
                    || TryFindReachablePlacement(position, position - right, rotation, carrier, out ground, avoidRope, fullWidth)
                    || TryFindReachablePlacement(position, position + forward, rotation, carrier, out ground, avoidRope, fullWidth)) return true;
            }
            ground = default;
            return false;
        }

        bool TryFindReachablePlacement(Vector3 from, Vector3 candidate, Quaternion rotation, NetworkPlayer carrier,
            out Vector3 ground, bool avoidRope, bool fullWidth)
            => TryFindDropPath(candidate, rotation, fullWidth ? null : carrier, out ground, avoidRope)
                && (!avoidRope || candidate.y - ground.y <= GameSettings.Trampoline.GrabDistance)
                && HumanCannonCarry.CanMoveFromPose(transform, fullWidth ? carryBody : MoveCheckBody, from, candidate, rotation, carrier);

        bool TryFindWaitingRoomDrop(Vector3 position, Quaternion rotation, NetworkPlayer carrier, out Vector3 ground)
        {
            float reach = GameSettings.Trampoline.GrabDistance;
            // Search near the carried pose first. The narrow carry probe may slide past a corner,
            // but the lowered trampoline still needs room for its full body.
            for (int ring = 1; ring <= 4; ring++)
            {
                float distance = reach * ring / 4f;
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4f;
                    var offset = rotation * new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * distance;
                    var candidate = position + offset;
                    if (TryFindDropPath(candidate, rotation, null, out ground, avoidRope: true)
                        && candidate.y - ground.y <= reach
                        && HumanCannonCarry.CanMoveFromPose(transform, MoveCheckBody, position, candidate, rotation, carrier))
                        return true;
                }
            }
            ground = default;
            return false;
        }

        bool TryParkBelow(Vector3 from, NetworkPlayer carrier, float reach, out Vector3 ground) =>
            TryFindDropPath(from, transform.rotation, carrier, out ground, avoidRope: true) && from.y - ground.y <= reach;

        void ReleaseAt(ulong actor, uint revision, Vector3 from, Quaternion rotation, Vector3 ground)
        {
            if (!ownership.TryApply(CannonAction.Release, actor, revision)) return;
            transform.SetPositionAndRotation(from, rotation);
            StartDrop(ground);
            RefreshCarryBody(null);
            ResetPreviousPose();
            PublishOwnership();
        }

        void StartDrop(Vector3 target)
        {
            Vector3 position = transform.position;
            if (position.y - target.y <= DropMinHeight)
            {
                StopDrop();
                transform.position = target;
                return;
            }
            transform.position = new Vector3(target.x, position.y, target.z);
            dropTarget = target;
            dropSpeed = 0f;
            dropping = true;
        }

        void StopDrop() => dropping = false;

        /// <summary>원격 화면: 확정 지점이 현재 위치 바로 아래면 순간이동 대신 떨어뜨린다.</summary>
        bool TryStartRemoteDrop(Vector3 target, Quaternion rotation)
        {
            Vector3 position = transform.position;
            Vector2 horizontal = new Vector2(target.x - position.x, target.z - position.z);
            if (target.y >= position.y - DropMinHeight || horizontal.sqrMagnitude > DropHorizontalTolerance * DropHorizontalTolerance
                || !HumanCannonCarry.CanMove(transform, carryBody, target, rotation, null, true))
                return false;
            StartDrop(target);
            return true;
        }

        void TickDrop()
        {
            if (!dropping) return;
            dropSpeed += Physics.gravity.magnitude * Time.deltaTime;
            Vector3 position = transform.position;
            position.y = Mathf.Max(dropTarget.y, position.y - dropSpeed * Time.deltaTime);
            transform.position = position;
            if (position.y <= dropTarget.y)
            {
                // A player or pursuer may have entered the landing space after the host approved the drop.
                var rotation = transform.rotation;
                if (CanSettleAt(dropTarget, rotation, null))
                    dropping = false;
                else if (session != null && session.IsHost && TryFindNearbyPlacement(dropTarget, rotation, null, out var ground))
                {
                    transform.position = ground;
                    StopDrop();
                    ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
                    PublishOwnership();
                }
                if (!dropping) RefreshCarryBody(null);
            }
            // 떨어지는 이동을 반동 sweep으로 판정하지 않는다.
            ResetPreviousPose();
        }
    }
}
