using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>1단계의 운반 트램폴린. 소유/반동은 호스트, 운반 이동은 기존 플레이어 위치 공유를 사용한다.</summary>
    [DefaultExecutionOrder(-30)]
    public sealed partial class HumanCannonTrampoline : MonoBehaviour
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
        [Tooltip("CC 원형 본체와 손잡이 외곽으로 접촉을 검사한다. 구형 더미는 기존 bounds를 사용한다."), SerializeField] bool usePropOutline;
        [Tooltip("협동 구간 전용: 다른 단원이 손잡이를 잡고 있을 때만 반동한다. 기본값은 바닥에 놓아도 반동."), SerializeField] bool requireCarrierForBounce;
        [Tooltip("운송 시 검사할 프레임의 좌우/앞뒤 반폭. 0이면 기존 반동 면 크기를 사용한다.")]
        [SerializeField] Vector2 loadHalfSize;
        [Tooltip("손잡이 잡기 거리(m). 공용 상호작용 거리 배율 적용.")]
        [Min(0.1f), SerializeField] float grabDistance = 4f;
        [Tooltip("수직 반동 속도(m/s). 자동 전진은 멈추고 WASD 조절은 유지한다. 다리 부상으로 감소하지 않는다.")]
        [Min(0.1f), SerializeField] float upwardSpeed = 12f;
        [Tooltip("땅에서 점프해 첫 반동을 시작한 플레이어의 WASD 수평 조절 속도(m/s). 트램폴린 운반 이동도 같은 기본 속도를 쓴다.")]
        [Min(0f), SerializeField] float groundBounceAirControlSpeed = 3f;
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
        bool carrierRecoveryPending;
        Renderer[] handleRenderers;
        MaterialPropertyBlock[] idleHandleProperties;
        MaterialPropertyBlock heldHandleTint;
        bool? handleShownHeld;
        public static HumanCannonTrampoline Current { get; private set; }
        public ulong Carrier => ownership.Occupant;
        public Transform Handle => handle != null ? handle : transform;
        public float CarryHandleHeight => carryHandleHeight;
        public Vector3 Surface => transform.position + Vector3.up * surfaceHeight;
        public Vector2 HalfSize => halfSize;
        public bool CircularSurface => circularSurface;
        public bool UsePropOutline => usePropOutline;
        public bool RequireCarrierForBounce => requireCarrierForBounce;
        bool BounceReady => !requireCarrierForBounce || Carrier != CannonSeatState.Nobody
            && FindPlayer(Carrier) is NetworkPlayer carrier && carrier.IsSpawned && carrier.State == PlayerState.Normal;
        public Vector2 LoadHalfSize => loadHalfSize.x > 0f && loadHalfSize.y > 0f ? loadHalfSize : halfSize;
        public float UpwardSpeed => upwardSpeed;
        public float CarryMoveSpeed => groundBounceAirControlSpeed;
        public int LocalBounceCount { get; private set; }
        public static event System.Action<ulong> ServerBounced;

        void OnEnable()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError("[HumanCannon] 1단계 테스트에는 트램폴린을 하나만 둡니다.", this);
                enabled = false;
                return;
            }
            Current = this;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            contactBoundsReady = false;
            ResetPreviousPose();
            PresentHandleOwnership();
            NetworkPlayer.Restarted += HandleRestart;
        }

        void Update()
        {
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += HandleConnection;
                HandleConnection(session.ConnectionState);
            }
            if (session == null || !session.IsHost) return;
            bool changed = false;
            if (ownership.IsOccupied)
            {
                var carrier = FindPlayer(Carrier);
                if (carrier != null && carrier.State == PlayerState.Normal) return;
                carrierRecoveryPending = carrier != null
                    && (carrier.State == PlayerState.Fallen || carrier.State == PlayerState.Retired);
                ownership.TryApply(CannonAction.Release, Carrier, ownership.Revision);
                changed = true;
            }
            if (carrierRecoveryPending && TryRecoverNearSurvivor())
            {
                carrierRecoveryPending = false;
                // 재등장 위치도 새 revision으로 확정해 오래된 잡기/놓기 요청을 무효화한다.
                ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
                changed = true;
            }
            if (!changed) return;
            // 순간이동 전후를 반동 sweep으로 연결하지 않고, 모든 화면에 같은 위치를 공유한다.
            ResetPreviousPose();
            PublishOwnership();
        }

        bool TryRecoverNearSurvivor()
        {
            NetworkPlayer survivor = null;
            int count = 0;
            foreach (var candidate in NetworkPlayer.All)
            {
                if (candidate == null || !candidate.IsSpawned || candidate.State != PlayerState.Normal
                    || !Finite(candidate.transform.position)) continue;
                if (Random.Range(0, ++count) == 0) survivor = candidate;
            }
            // 전멸 때는 부활시키지 않는다. 정상 참가자가 생기거나 회차가 초기화될 때까지 기다린다.
            if (survivor == null) return false;
            Vector3 forward = Vector3.ProjectOnPlane(survivor.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            // 자동 소유권 이전 없이 손잡이를 생존자 옆 1m에 둔다. 공중 생존자도 다시 F로 잡을 수 있다.
            Vector3 handlePosition = CarryHandlePoint(survivor.transform.position) + Vector3.Cross(Vector3.up, forward);
            Vector3 handleOffset = Vector3.Scale(transform.InverseTransformPoint(Handle.position), transform.lossyScale);
            if (!TrampolineCarryPose.TryCalculate(handlePosition, forward, handleOffset, out var position, out var rotation))
            {
                rotation = Quaternion.LookRotation(forward);
                position = handlePosition - rotation * handleOffset;
            }
            transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        void LateUpdate()
        {
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
            // 운반자는 3인칭 카메라의 수평 시선을 바라보며 옆걸음/뒷걸음한다.
            // 다른 화면에서는 그 운반자가 공유한 몸 방향을 사용한다. 관찰자의 카메라는 사용하지 않는다.
            Vector3 handleOffset = Vector3.Scale(transform.InverseTransformPoint(Handle.position), transform.lossyScale);
            Vector3 facing = alignToCarrier || !carrier.IsLocal ? carrier.transform.forward
                : Vector3.ProjectOnPlane(transform.position - Handle.position, Vector3.up);
            var view = CameraViewDirector.Current;
            // 봇은 마우스 시선 없이 직접 이동 입력을 만들므로 기존 운반 방향을 유지한다.
            if (carrier.IsLocal && !HumanCannonBotWorker.IsWorkerProcess && !InputGate.IsBlocked
                && view != null && view.LocalPlayer == carrier && view.IsFreeThirdPerson)
                facing = view.ViewForward;
            if (TrampolineCarryPose.TryCalculate(CarryHandlePoint(carrier.transform.position), facing,
                handleOffset, out var position, out var rotation))
            {
                transform.SetPositionAndRotation(position, rotation);
                carrier.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(facing, Vector3.up));
            }
        }

        // 호스트 최초 잡기/원격 추종/로컬 추종/사망 후 복구에서 같은 손 높이를 사용한다.
        Vector3 CarryHandlePoint(Vector3 feet) => feet + Vector3.up * Mathf.Max(0f, carryHandleHeight);

        void ResetPreviousPose()
        {
            previousSurface = Surface;
            previousRotation = transform.rotation;
        }

        public bool CanGrab(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal || ownership.IsOccupied) return false;
            var cannon = HumanCannonSystem.Current;
            if (cannon == null || cannon.IsSeated(player.OwnerClientId)) return false;
            if (HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0) return false;
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            float radius = grabDistance * reach;
            return PlayerInteraction.InRange(player, usePropOutline ? Handle.position + transform.up * (.6f * transform.lossyScale.y) : Surface, radius);
        }

        internal void RequestGrab()
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
        internal void RequestDrop() => SendRequest(TrampolineAction.Drop, -1, 0, 0, transform.position);
        public void ServerReleaseForInspection(ulong actor)
        {
            if (session == null || !session.IsHost || Carrier != actor) return;
            HandleRequest(actor, TrampolineAction.Drop, ownership.Revision, -1, 0, 0, transform.position, transform.rotation);
        }

        internal bool TryContact(Vector3 from, Vector3 to, float radius, float height, out Vector3 contact)
        {
            contact = default;
            if (!BounceReady) return false;
            bool hit = usePropOutline
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
            if (usePropOutline)
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
                carrierRecoveryPending = false;
                HumanCannonSystem.Current.StartCarrying(actor);
                FollowCarrier(player, true);
            }
            else if (action == TrampolineAction.Drop)
            {
                if (!Finite(rotation) || !ownership.TryApply(CannonAction.Release, actor, revision)) return;
                // 로컬 장치 위치를 사용하되 마지막 동기화 위치에서 비정상적으로 먼 요청은 무시한다.
                float turnSpan = Mathf.Max(4f, 2f * Vector3.Distance(transform.position, Handle.position) + 1f);
                if ((position - transform.position).sqrMagnitude <= turnSpan * turnSpan)
                    transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
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
                && cannon.ApplyRebound(player.OwnerClientId, station, launch, segment, contact, upwardSpeed);
            if (accepted)
            {
                ServerBounced?.Invoke(player.OwnerClientId);
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
            carrierRecoveryPending = false;
            // 아무도 잡지 않았어도 revision을 올려 초기화 이전의 잡기 요청을 무효화한다.
            ownership.Receive(ownership.Revision + 1, CannonSeatState.Nobody);
            if (resetPoseOnRestart) transform.SetPositionAndRotation(initialPosition, initialRotation);
            ResetPreviousPose();
            PublishOwnership();
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
