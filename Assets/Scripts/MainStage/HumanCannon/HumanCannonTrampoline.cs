using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>1단계의 운반 트램폴린. 소유/반동은 호스트, 운반 이동은 기존 플레이어 위치 공유를 사용한다.</summary>
    [DefaultExecutionOrder(-30)]
    public sealed partial class HumanCannonTrampoline : MonoBehaviour, IHumanCannonCarry
    {
        [Tooltip("외곽 손잡이. 플레이어 발 위치 기준으로 잡기 거리를 검사한다.")]
        [SerializeField] Transform handle;
        [Tooltip("운반 중 손잡이를 붙일 발 위 높이(월드 m). 0이면 기존 발 위치 기준을 유지한다."), Min(0f), SerializeField] float carryHandleHeight;
        [Tooltip("중앙 반동 면의 높이(m, 장치 원점 기준).")]
        [Min(0.1f), SerializeField] float surfaceHeight = 0.5f;
        [Tooltip("중앙 반동 면의 로컬 좌우/앞뒤 반폭(m). 운반 회전을 따라 판정한다.")]
        [SerializeField] Vector2 halfSize = new Vector2(2f, 2f);
        [Tooltip("원형 천의 반동 판정. 기존 씬은 기본값인 사각형을 유지한다.")]
        [SerializeField] bool circularSurface;
        [Tooltip("운송 시 검사할 프레임의 좌우/앞뒤 반폭. 0이면 기존 반동 면 크기를 사용한다.")]
        [SerializeField] Vector2 loadHalfSize;
        [Tooltip("테스트용 모두 재시작 시 트램폴린도 최초 위치로 돌린다.")]
        [SerializeField] bool resetPoseOnRestart = true;

        CannonSeatState ownership = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        Vector3 initialPosition;
        Quaternion initialRotation;
        Vector3 previousSurface;
        Quaternion previousRotation;
        Bounds contactBounds;
        bool contactBoundsReady;
        bool localGrabPending;
        uint localGrabRevision;
        double localGrabUntil;
        Renderer[] handleRenderers;
        MaterialPropertyBlock[] idleHandleProperties;
        MaterialPropertyBlock heldHandleTint;
        bool? handleShownHeld;
        Collider carryBody;
        bool originalCarryBodyTrigger;
        CharacterController ignoredCarrierBody;
        bool previousCarrierIgnored;
        Vector3 lastSafeCarrierFeet;
        ulong collisionCarrier = CannonSeatState.Nobody;

        void RefreshCarryBody(NetworkPlayer carrier)
        {
            CurtainCall.Settings.GamePhysicsLayers.AssignHierarchy(transform, CurtainCall.Settings.GamePhysicsLayers.Trampoline);
            if (carryBody == null)
            {
                carryBody = GetComponent<Collider>();
                if (carryBody == null)
                {
                    var shape = CurtainCall.Settings.GameSettings.Trampoline.CarryCollisionMesh;
                    if (shape != null)
                    {
                        var mesh = gameObject.AddComponent<MeshCollider>();
                        mesh.sharedMesh = shape;
                        mesh.convex = true;
                        carryBody = mesh;
                    }
                    else carryBody = gameObject.AddComponent<BoxCollider>();
                }
                originalCarryBodyTrigger = carryBody.isTrigger;
            }
            if (carryBody is MeshCollider commonMesh && CurtainCall.Settings.GameSettings.Trampoline.CarryCollisionMesh is { } sharedShape
                && commonMesh.sharedMesh != sharedShape)
            {
                commonMesh.sharedMesh = sharedShape;
                commonMesh.convex = true;
            }
            if (carryBody is BoxCollider box)
            {
                box.center = new Vector3(0, surfaceHeight * .5f, 0);
                box.size = new Vector3(LoadHalfSize.x * 2f, surfaceHeight, LoadHalfSize.y * 2f);
            }
            // The core is checked explicitly; the rim must not physically push other bodies while carried.
            bool trigger = originalCarryBodyTrigger || CarryCheckDiameter > 0f && (ownership.IsOccupied || dropping);
            if (carryBody.isTrigger != trigger) carryBody.isTrigger = trigger;
            var next = gameObject.scene.IsValid() && carrier != null && carrier.gameObject.scene.IsValid()
                && carrier.TryGetComponent(out PlayerMover mover) ? mover.Body : null;
            if (next != ignoredCarrierBody)
            {
                if (ignoredCarrierBody != null) Physics.IgnoreCollision(ignoredCarrierBody, carryBody, previousCarrierIgnored);
                ignoredCarrierBody = next;
                if (next != null) previousCarrierIgnored = Physics.GetIgnoreCollision(next, carryBody);
            }
            if (next != null && next.enabled && next.gameObject.activeInHierarchy) Physics.IgnoreCollision(next, carryBody, true);
        }
        float CarryCheckDiameter => UsesTraversalCarryClearance(gameObject.scene.name)
            ? CurtainCall.Settings.GameSettings.WaitingRoom.TrampolineCarryCheckDiameter : 0f;

        static bool UsesTraversalCarryClearance(string sceneName) =>
            sceneName != null && (sceneName.StartsWith("WaitingRoom", System.StringComparison.Ordinal)
                || sceneName.StartsWith("escape_route", System.StringComparison.OrdinalIgnoreCase));

        BoxCollider carryCheckBody;
        internal bool IsCarryCheckBody(Collider body) => carryCheckBody != null && body == carryCheckBody;

        Collider MoveCheckBody
        {
            get
            {
                float diameter = CarryCheckDiameter;
                if (diameter <= 0f || carryBody == null) return carryBody;
                if (carryCheckBody == null)
                {
                    var holder = new GameObject("TrampolineCarryCheck") { layer = gameObject.layer, hideFlags = HideFlags.DontSave };
                    holder.transform.SetParent(transform, false);
                    carryCheckBody = holder.AddComponent<BoxCollider>();
                    carryCheckBody.isTrigger = true;
                }
                var scale = transform.lossyScale;
                carryCheckBody.center = new Vector3(0, surfaceHeight * .5f, 0);
                carryCheckBody.size = new Vector3(diameter / Mathf.Max(.0001f, Mathf.Abs(scale.x)), surfaceHeight, diameter / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
                return carryCheckBody;
            }
        }
        public static HumanCannonTrampoline Current { get; private set; }
        public ulong Carrier => ownership.Occupant;
        public Transform Handle => handle != null ? handle : transform;
        public Vector3 GrabPoint => UsePropOutline ? Handle.position + transform.up * (CurtainCall.Settings.GameSettings.Trampoline.GrabPointHeight * transform.lossyScale.y) : Surface;
        public float CarryHandleHeight => carryHandleHeight;
        public Vector3 Surface => transform.position + Vector3.up * surfaceHeight;
        public Vector2 HalfSize => halfSize;
        public bool CircularSurface => circularSurface;
        public bool UsePropOutline => CurtainCall.Settings.GameSettings.Trampoline.UsePropOutline;
        public bool RequireCarrierForBounce => CurtainCall.Settings.GameSettings.Trampoline.RequireCarrierForBounce;
        /// <summary>지금 위에 떨어지면 반동하는지(운반자 필요 설정이면 운반 중일 때만).</summary>
        public bool BounceReady => !dropping && (!CurtainCall.Settings.GameSettings.Trampoline.RequireCarrierForBounce || Carrier != CannonSeatState.Nobody
            && FindPlayer(Carrier) is NetworkPlayer carrier && carrier.IsSpawned && carrier.State == PlayerState.Normal);
        public Vector2 LoadHalfSize => loadHalfSize.x > 0f && loadHalfSize.y > 0f ? loadHalfSize : halfSize;
        public float UpwardSpeed => CurtainCall.Settings.GameSettings.Trampoline.UpwardSpeed;
        public float CarryMoveSpeed => CurtainCall.Settings.GameSettings.Trampoline.CarryMoveSpeed;
        public Transform CarryRoot => transform;
        public Vector3 CarryFocus => Surface;
        public Vector2 CarryHalfSize => halfSize;
        public string CarryName => "트램펄린";
        public int LocalBounceCount { get; private set; }
        public static event System.Action<ulong> ServerBounced;

        void OnEnable()
        {
            RefreshCarryBody(null);
            if (Current != null && Current != this)
            {
                Debug.LogError("[HumanCannon] 1단계 테스트에는 트램폴린을 하나만 둡니다.", this);
                enabled = false;
                return;
            }
            Current = this;
            HumanCannonCarry.Register(this);
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            contactBoundsReady = false;
            ResetPreviousPose();
            PresentHandleOwnership();
            NetworkPlayer.Restarted += HandleRestart;
        }

        void OnTransformChildrenChanged() => CurtainCall.Settings.GamePhysicsLayers.AssignHierarchy(transform, CurtainCall.Settings.GamePhysicsLayers.Trampoline);

        void Update()
        {
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += HandleConnection;
                HandleConnection(session.ConnectionState);
            }
            if (session != null && messaging == null && session.ConnectionState is SessionConnectionState.Host or SessionConnectionState.Client)
                HandleConnection(session.ConnectionState);
            if (session != null && session.ConnectionState == SessionConnectionState.Client) RetryOwnershipSync();
            TickDrop();
            if (session == null || !session.IsHost) return;
            if (!ownership.IsOccupied) return;
            var carrier = FindPlayer(Carrier);
            if (carrier == null || carrier.State != PlayerState.Normal)
                ReleaseCarrierToGround(carrier);
            else if (HasRopeSupport(carrier))
                ServerDropForRope(carrier.OwnerClientId);
        }

        internal static bool IsCarriedBy(NetworkPlayer player) => player != null
            && HumanCannonCarry.Held(player.OwnerClientId) is HumanCannonTrampoline;

        static bool HasRopeSupport(NetworkPlayer player)
        {
            if (player == null || !player.TryGetComponent(out PlayerMover mover) || (player.IsLocal
                ? !mover.Simulated || mover.IsAirborne : player.IsAirborne)) return false;
            var course = TightropeCourse.Current;
            return course != null && course.IsTraversalActive && course.TryGetSupportingRope(mover, out _);
        }

        internal static bool WaitingForRopeDrop(PlayerMover mover)
        {
            if (Current == null || !Current.ownership.IsOccupied || mover == null
                || !mover.TryGetComponent(out NetworkPlayer player) || player.State != PlayerState.Normal) return false;
            var stack = PiggybackSystem.Current;
            var carrier = stack != null && stack.GetSupportingPlayer(player) != null ? stack.GetCarrier(player) : player;
            return carrier != null && Current.Carrier == carrier.OwnerClientId && HasRopeSupport(carrier);
        }

        void ReleaseCarrierToGround(NetworkPlayer carrier)
        {
            if (carryBody == null) RefreshCarryBody(carrier);
            // 들고 낙사하면 시작 지점에 가장 가까운 생존자 발밑 지면으로 옮긴다(2026-11-05 사용자 요청).
            // 그 밖의 사망은 생존자에게 옮기지 않고 사망 지점 바로 아래 지면에 떨어뜨린다(2026-11-05 사용자 요청).
            bool fellOff = carrier != null
                && (HumanCannonRouteSystem.IsBelowCourse(carrier.transform.position) || HumanCannonRouteSystem.IsBelowCourse(transform.position));
            Vector3 dropOrigin = transform.position;
            Quaternion rotation = transform.rotation;
            Vector3 ground = default;
            bool nearby = !fellOff && (TryDropHere(carrier, out ground)
                || TryFindNearbySidePlacement(dropOrigin, rotation, carrier, out ground, avoidRope: false));
            if (!nearby)
            {
                rotation = initialRotation;
                if (!TryFindSurvivorGround(out ground)
                    && !TryRestoreInitialPlacement(carrier, out ground)) return;
            }
            ReleaseAt(Carrier, ownership.Revision, nearby ? dropOrigin : ground, rotation, ground);
        }

        void LateUpdate()
        {
            if (!ownership.IsOccupied) { RefreshCarryBody(null); collisionCarrier = CannonSeatState.Nobody; }
            PresentHandleOwnership();
            previousSurface = Surface;
            previousRotation = transform.rotation;
            if (!ownership.IsOccupied) return;
            var carrier = FindPlayer(Carrier);
            if (carrier != null && carrier.State == PlayerState.Normal)
                FollowCarrier(carrier);
        }

        void PresentHandleOwnership()
        {
            // 호스트 확정 소유 상태를 표시하므로 원격 운반자도 같은 색으로 보인다.
            if (handle == null) return;
            if (handleRenderers == null)
            {
                handleRenderers = handle.GetComponentsInChildren<Renderer>(true);
                // 제작 FBX의 메시 원점과 운반용 손잡이 앵커는 다르다. 별도 앵커여도 보이는 손잡이를 표시한다.
                if (handleRenderers.Length == 0)
                    handleRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.enabled && r.name == handle.name);
                idleHandleProperties = new MaterialPropertyBlock[handleRenderers.Length];
                for (int i = 0; i < handleRenderers.Length; i++)
                {
                    idleHandleProperties[i] = new MaterialPropertyBlock();
                    handleRenderers[i].GetPropertyBlock(idleHandleProperties[i]);
                }
                heldHandleTint ??= new MaterialPropertyBlock();
            }
            bool held = ownership.IsOccupied;
            if (handleShownHeld == held) return;
            for (int i = 0; i < handleRenderers.Length; i++)
            {
                var renderer = handleRenderers[i];
                if (renderer == null) continue;
                if (held)
                {
                    renderer.GetPropertyBlock(heldHandleTint);
                    heldHandleTint.SetColor("_BaseColor", Color.green);
                    heldHandleTint.SetColor("_Color", Color.green);
                    renderer.SetPropertyBlock(heldHandleTint);
                }
                else renderer.SetPropertyBlock(idleHandleProperties[i]);
            }
            handleShownHeld = held;
        }

        void RestoreHandleAppearance()
        {
            if (handleRenderers != null)
                for (int i = 0; i < handleRenderers.Length; i++)
                    if (handleRenderers[i] != null) handleRenderers[i].SetPropertyBlock(idleHandleProperties[i]);
            handleRenderers = null;
            idleHandleProperties = null;
            handleShownHeld = null;
        }

        void FollowCarrier(NetworkPlayer carrier, bool alignToCarrier = false)
        {
            bool riding = HumanCannonRouteSystem.Current?.RiddenTransport(carrier.OwnerClientId) != null;
            UpdateCarryPose(carrier, alignToCarrier || riding, riding);
        }

        internal void FollowTransportArrival(NetworkPlayer carrier)
        {
            if (carrier != null && Carrier == carrier.OwnerClientId) UpdateCarryPose(carrier, true, true);
        }

        void UpdateCarryPose(NetworkPlayer carrier, bool alignToCarrier, bool riding)
        {
            // A local rope catch can precede the host's ownership update.
            if (TightropeHangSystem.Current?.IsHanging(carrier) == true || HasRopeSupport(carrier)) return;
            RefreshCarryBody(carrier);
            if (collisionCarrier != Carrier || alignToCarrier) { collisionCarrier = Carrier; lastSafeCarrierFeet = carrier.transform.position; }
            // 운반자는 3인칭 카메라의 수평 시선을 바라보며 옆걸음/뒷걸음한다.
            // 다른 화면에서는 그 운반자가 공유한 몸 방향을 사용한다. 관찰자의 카메라는 사용하지 않는다.
            if (HumanCannonCarry.TryFollowPose(carrier, transform, Handle, carryHandleHeight, alignToCarrier,
                out var position, out var rotation, out var carrierRotation))
            {
                carrier.TryGetComponent(out PlayerMover mover);
                float floorClearance = !alignToCarrier && mover != null && mover.Body != null
                    ? mover.Body.skinWidth * Mathf.Abs(mover.Body.transform.lossyScale.y) : 0f;
                bool fits = riding || HumanCannonCarry.CanMove(transform, MoveCheckBody, position, rotation, carrier, !alignToCarrier,
                    floorClearance, null, allowExistingOverlap: !alignToCarrier);
                // A remote sample can omit the turn around a column, so its straight path is not authoritative.
                bool catchUp = !fits && !alignToCarrier && !carrier.IsLocal &&
                    HumanCannonCarry.CanMove(transform, MoveCheckBody, position, rotation, carrier, false, floorClearance, null);
                if (!fits && !catchUp && (alignToCarrier || !TryTranslateWithoutTurning(carrier, mover, floorClearance,
                    out position, out rotation, out carrierRotation)))
                {
                    if (carrier.IsLocal && mover != null && mover.Simulated && mover.Body != null && mover.Body.enabled)
                        mover.Body.Move(Vector3.ProjectOnPlane(lastSafeCarrierFeet - carrier.transform.position, Vector3.up));
                    return;
                }
                transform.SetPositionAndRotation(position, rotation);
                if (catchUp) ResetPreviousPose();
                if (carrier.IsLocal) carrier.transform.rotation = carrierRotation;
                lastSafeCarrierFeet = carrier.transform.position;
            }
        }

        bool TryTranslateWithoutTurning(NetworkPlayer carrier, PlayerMover mover, float floorClearance, out Vector3 position,
            out Quaternion rotation, out Quaternion carrierRotation)
        {
            rotation = transform.rotation;
            var toCenter = Vector3.ProjectOnPlane(transform.position - Handle.position, Vector3.up);
            carrierRotation = toCenter.sqrMagnitude > .0001f ? Quaternion.LookRotation(toCenter) : carrier.transform.rotation;
            Vector3 handleOffset = transform.position - Handle.position + Vector3.up * Mathf.Max(0f, carryHandleHeight);
            position = carrier.transform.position + handleOffset;
            if (HumanCannonCarry.CheckMove(transform, MoveCheckBody, position, rotation, carrier, true, floorClearance, null,
                allowExistingOverlap: true, out var obstruction)) return true;
            if (HumanCannonCarry.TryCarryOverStep(transform, MoveCheckBody, position, carrier, floorClearance,
                CurtainCall.Settings.GameSettings.Trampoline.MaxCarryStepLift, out var raised))
            { position = raised; return true; }
            if (!carrier.IsLocal || mover == null || !mover.Simulated || mover.Body == null || !mover.Body.enabled) return false;

            Vector3 wallNormal = Vector3.ProjectOnPlane(obstruction, Vector3.up).normalized;
            if (wallNormal == Vector3.zero || Mathf.Abs(obstruction.y) > .5f) return false;
            Vector3 travel = carrier.transform.position - lastSafeCarrierFeet;
            Vector3 slide = Vector3.ProjectOnPlane(travel, wallNormal);
            if (slide.sqrMagnitude < .000001f || Vector3.Dot(travel, wallNormal) >= 0f) return false;
            Vector3 slideFeet = lastSafeCarrierFeet + slide;
            if (!HumanCannonCarry.CanMove(transform, MoveCheckBody, slideFeet + handleOffset, rotation, carrier, true,
                floorClearance, null, allowExistingOverlap: true)) return false;

            mover.Body.Move(slideFeet - carrier.transform.position);
            position = carrier.transform.position + handleOffset;
            return HumanCannonCarry.CanMove(transform, MoveCheckBody, position, rotation, carrier, true, floorClearance, null,
                allowExistingOverlap: true);
        }

        void ResetPreviousPose()
        {
            previousSurface = Surface;
            previousRotation = transform.rotation;
        }

        internal System.Func<bool> PracticeGrabAllowed { get; set; }

        public bool CanGrab(NetworkPlayer player)
        {
            if (!CanAcquire(player) || dropping) return false;
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            float radius = CurtainCall.Settings.GameSettings.Trampoline.GrabDistance * reach;
            return PlayerInteraction.InRange(player, GrabPoint, radius, transform)
                && HumanCannonCarry.TryFollowPose(player, transform, Handle, carryHandleHeight, true, out var position, out var rotation, out _)
                && HumanCannonCarry.CanMove(transform, MoveCheckBody, position, rotation, player, false);
        }

        // Automatic rehearsal returns skip reach, but never skip the carrier's eligibility.
        bool CanAcquire(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal || ownership.IsOccupied
                || session != null && session.ConnectionState == SessionConnectionState.Client && !hasOwnershipSnapshot
                || HumanCannonSystem.Current == null || !HumanCannonSystem.Current.IsPracticeReady(player.OwnerClientId)
                || HumanCannonCarry.HasOccupiedHands(player)
                || CurtainCall.WaitingRoom.Pursuit.PursuitDirector.IsCaptured(player)
                || PiggybackSystem.Current?.GetBelow(player) != null || HasRopeSupport(player)
                || PracticeGrabAllowed?.Invoke() == false) return false;
            var course = TightropeCourse.Current;
            return course == null || !course.IsTraversalActive || !course.UsesRopeRules(player);
        }

        public void RequestUse() { } // 반동은 접촉으로 동작하며 사용 키는 소비하지 않는다.

        public void RequestGrab()
        {
            var player = NetworkPlayer.Local;
            if (!CanGrab(player) || GrabPending(player)) return;
            localGrabPending = true;
            localGrabRevision = ownership.Revision;
            var condition = player.GetComponent<PlayerCondition>();
            // 호스트의 외팔 대기와 왕복 지연 동안 같은 플레이어의 반동 요청을 보내지 않는다.
            localGrabUntil = Time.timeAsDouble + 1.5d
                + HumanCannonSystem.Current.Injuries.InteractionSeconds(condition != null && condition.IsOneArm);
            SendRequest(TrampolineAction.Grab, -1, 0, 0, transform.position);
        }

        internal bool GrabPending(NetworkPlayer player)
        {
            if (player == null) return false;
            if (player.IsLocal && localGrabPending)
            {
                if (localGrabRevision != ownership.Revision || Time.timeAsDouble > localGrabUntil || !CanGrab(player))
                    localGrabPending = false;
                else return true;
            }
            return session != null && session.IsHost && HumanCannonSystem.Current != null
                && HumanCannonSystem.Current.HasPendingTrampolineGrab(player, ownership.Revision);
        }
        public void RequestDrop() => SendRequest(TrampolineAction.Drop, -1, 0, 0, transform.position);
        public void ServerReleaseForInspection(ulong actor)
        {
            if (session == null || !session.IsHost || Carrier != actor) return;
            var carrier = FindPlayer(actor);
            if (HasRopeSupport(carrier))
            {
                ServerDropForRope(actor);
                return;
            }
            RefreshCarryBody(carrier);
            var position = transform.position;
            var rotation = transform.rotation;
            // Relocation detaches at the old pose before its carrier teleports.
            if (CanSettleAt(position, rotation, carrier))
                ReleaseAt(actor, ownership.Revision, position, rotation, position);
            else
                ReleaseCarrierToGround(carrier);
        }

        internal bool ServerRestoreRehearsalCarrier(NetworkPlayer player)
        {
            if (session == null || !session.IsHost || !CanAcquire(player)) return false;
            RefreshCarryBody(player);
            if (!HumanCannonCarry.TryFollowPose(player, transform, Handle, carryHandleHeight, true,
                out var position, out var rotation, out _) || !HumanCannonCarry.CanMove(transform, MoveCheckBody, position, rotation, player, false)
                || !ownership.TryApply(CannonAction.Board, player.OwnerClientId, ownership.Revision)) return false;
            StopDrop();
            HumanCannonSystem.Current.StartCarrying(player.OwnerClientId);
            FollowCarrier(player, true);
            ResetPreviousPose();
            PublishOwnership();
            return true;
        }

        internal void ServerDropForRope(ulong actor)
        {
            if (session == null || !session.IsHost || Carrier != actor) return;
            var carrier = FindPlayer(actor);
            RefreshCarryBody(carrier);
            var position = transform.position;
            var rotation = transform.rotation;
            if (TryParkBelow(position, carrier, CurtainCall.Settings.GameSettings.Trampoline.GrabDistance, out var ground))
                ReleaseAt(actor, ownership.Revision, position, rotation, ground);
            else if (TryFindNearbySidePlacement(position, rotation, carrier, out ground, avoidRope: true))
                ReleaseAt(actor, ownership.Revision, ground, rotation, ground);
            else if (TryParkBelow(position, carrier, DropProbeDistance, out ground))
                ReleaseAt(actor, ownership.Revision, position, rotation, ground);
            else if (TryRestoreInitialPlacement(carrier, out ground, avoidRope: true))
                ReleaseAt(actor, ownership.Revision, ground, initialRotation, ground);
        }

        internal bool TryContact(Vector3 from, Vector3 to, float radius, float height, out Vector3 contact)
        {
            contact = default;
            if (!BounceReady) return false;
            bool hit = UsePropOutline
                ? TrampolineOutline.TryTouch(from, to, previousSurface, Surface, previousRotation, transform.rotation, ContactBounds(), radius, height, out contact)
                : TrampolineContact.TryTouch(from, to, previousSurface, Surface, previousRotation, transform.rotation, ContactBounds(), radius, height, out contact);
            return hit && !BounceBlocked(contact);
        }

        Bounds ContactBounds()
        {
            if (contactBoundsReady) return contactBounds;
            var size = LoadHalfSize;
            contactBounds = new Bounds(new Vector3(0f, -surfaceHeight * .5f, 0f),
                new Vector3(size.x * 2f, surfaceHeight, size.y * 2f));
            Quaternion inverse = Quaternion.Inverse(transform.rotation);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.name.StartsWith("__")) continue;
                var bounds = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    contactBounds.Encapsulate(inverse * (renderer.transform.TransformPoint(corner) - Surface));
                }
            }
            contactBoundsReady = true;
            return contactBounds;
        }

        bool NearSurface(NetworkPlayer player, Vector3 contact)
        {
            Vector3 relative = Quaternion.Inverse(transform.rotation) * (contact - Surface);
            var bounds = ContactBounds();
            if (UsePropOutline)
                return TrampolineOutline.Contains(relative, bounds, 1f) && (player.transform.position - contact).sqrMagnitude <= 25f;
            bounds.Expand(new Vector3(2f, 4f, 2f));
            return bounds.Contains(relative) && (player.transform.position - contact).sqrMagnitude <= 25f;
        }

        // 과승 실패 중에는 운반 트램폴린도 함께 떨어진다. 지연된 반동 요청에도 같은 판정을 사용한다.
        static bool BounceBlocked(Vector3 contact) => HumanCannonRouteSystem.Current != null
            && HumanCannonRouteSystem.Current.BlocksTrampolineBounce(contact);

        internal void RequestBounce(int station, uint launch, uint segment, Vector3 contact) =>
            SendRequest(TrampolineAction.Bounce, station, launch, segment, contact);

        void HandleRequest(ulong actor, TrampolineAction action, uint revision, int station, uint launch,
            uint segment, Vector3 position, Quaternion rotation, bool interactionReady = false)
        {
            if (session == null || !session.IsHost) return;
            if (action == TrampolineAction.Sync) { SendOwnership(actor); return; }
            var player = FindPlayer(actor);
            if (player == null || player.State != PlayerState.Normal || !Finite(position)) return;
            if (action == TrampolineAction.GroundBounce)
            {
                HandleGroundBounce(player, revision, launch, position);
                return;
            }
            if (action == TrampolineAction.Bounce)
            {
                HandleBounce(player, station, launch, segment, position);
                return;
            }
            if (action == TrampolineAction.Grab)
            {
                if (!CanGrab(player) || ownership.Revision != revision) return;
                if (!interactionReady && HumanCannonSystem.Current.DelayInjuredInteraction(player, -1, revision,
                    () => this != null && isActiveAndEnabled && player != null
                        && ownership.Revision == revision && CanGrab(player),
                    () => HandleRequest(actor, action, revision, station, launch, segment, position, rotation, true))) return;
                if (!CanGrab(player) || !ownership.TryApply(CannonAction.Board, actor, revision)) return;
                StopDrop();
                HumanCannonSystem.Current.StartCarrying(actor);
                FollowCarrier(player, true);
            }
            else if (action == TrampolineAction.Drop)
            {
                if (Carrier == actor && ownership.Revision == revision && HasRopeSupport(player))
                {
                    ServerDropForRope(actor);
                    return;
                }
                if (!Finite(rotation) || Carrier != actor || ownership.Revision != revision) return;
                RefreshCarryBody(player);
                // 로컬 장치 위치를 사용하되 마지막 동기화 위치에서 비정상적으로 먼 요청은 무시한다.
                float turnSpan = Mathf.Max(4f, 2f * Vector3.Distance(transform.position, Handle.position) + 1f);
                rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
                if ((position - transform.position).sqrMagnitude > turnSpan * turnSpan
                    || !HumanCannonCarry.CanMove(transform, MoveCheckBody, position, rotation, player,
                        player.IsLocal, 0f, null, allowExistingOverlap: player.IsLocal))
                {
                    position = transform.position;
                    rotation = transform.rotation;
                }
                if (TryFindDropPath(position, rotation, player, out var ground))
                    ReleaseAt(actor, revision, position, rotation, ground);
                else if (UsesTraversalCarryClearance(gameObject.scene.name))
                {
                    if (TryFindNearbySidePlacement(position, rotation, player, out ground, avoidRope: false, fullWidth: true))
                        ReleaseAt(actor, revision, new Vector3(ground.x, position.y, ground.z), rotation, ground);
                }
                else ReleaseCarrierToGround(player);
                return;
            }
            else return;
            ResetPreviousPose();
            PublishOwnership();
        }

        void HandleBounce(NetworkPlayer player, int station, uint launch, uint segment, Vector3 contact)
        {
            var cannon = HumanCannonSystem.Current;
            // 위치는 소유자가 계산한다. 지연 허용 폭 안의 접촉과 현재 비행 식별자를 함께 검사한다.
            bool nearSurface = NearSurface(player, contact);
            bool accepted = BounceReady && !GrabPending(player) && !BounceBlocked(contact) && Carrier != player.OwnerClientId && nearSurface && cannon != null
                && cannon.ApplyRebound(player.OwnerClientId, station, launch, segment, contact, CurtainCall.Settings.GameSettings.Trampoline.UpwardSpeed, fromTrampoline: true);
            if (accepted)
            {
                ServerBounced?.Invoke(player.OwnerClientId);
                CurtainCall.WaitingRoom.Pursuit.PursuitStimuli.Emit(CurtainCall.WaitingRoom.Pursuit.PursuitNoiseKind.TrampolineBounce, contact, transform);
                if (player.IsLocal) LocalBounceCount++;
                foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                    if (client != NetworkManager.ServerClientId)
                        SendBounce(client, true, player.OwnerClientId, station, launch, segment, contact);
            }
            else if (player.IsLocal) cannon?.RejectRebound(station, launch, segment);
            else SendBounce(player.OwnerClientId, false, player.OwnerClientId, station, launch, segment, contact);
        }

        void HandleRestart(NetworkPlayer player)
        {
            localGrabPending = false;
            if (player.IsLocal) LocalBounceCount = 0;
            if (session == null || !session.IsHost) return;
            StopDrop();
            // 아무도 잡지 않았어도 revision을 올려 초기화 이전의 잡기 요청을 무효화한다.
            ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
            if (resetPoseOnRestart) transform.SetPositionAndRotation(initialPosition, initialRotation);
            ResetPreviousPose();
            PublishOwnership();
        }

        /// <summary>The rehearsal can recover its only shared prop when its carrier falls.</summary>
        public bool ResetPracticePose()
        {
            if (HumanCannonSystem.Current == null || !HumanCannonSystem.Current.PracticeMode || session == null || !session.IsHost) return false;
            StopDrop();
            ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            ResetPreviousPose(); PublishOwnership();
            return true;
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        static bool Finite(Quaternion value) => float.IsFinite(value.x) && float.IsFinite(value.y)
            && float.IsFinite(value.z) && float.IsFinite(value.w) && Quaternion.Dot(value, value) > 0.0001f;

        static NetworkPlayer FindPlayer(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id) return player;
            return null;
        }

        void OnDisable()
        {
            if (carryBody != null && ignoredCarrierBody != null) Physics.IgnoreCollision(ignoredCarrierBody, carryBody, previousCarrierIgnored);
            if (carryBody != null) carryBody.isTrigger = originalCarryBodyTrigger;
            ignoredCarrierBody = null;
            collisionCarrier = CannonSeatState.Nobody;
            HumanCannonCarry.Unregister(this);
            localGrabPending = false;
            RestoreHandleAppearance();
            NetworkPlayer.Restarted -= HandleRestart;
            Unregister();
            if (session != null) session.ConnectionStateChanged -= HandleConnection;
            session = null;
            if (Current == this) Current = null;
        }
    }
}
