using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>원본 운반 조작을 쓰는 출구 절단기. 호스트의 접지·날/케이블 접촉만 문 잠금을 해제한다.</summary>
    [DefaultExecutionOrder(-30)]
    public sealed partial class EscapeExitSaw : MonoBehaviour, IHumanCannonCarry
    {
        [SerializeField] Transform carryRoot, handle, blade, cableSocket, doorPanel, recoveryAnchor;
        [SerializeField, Min(0)] int deviceId;
        [Tooltip("현재 접속 인원당 한 대만 제공한다. 들고 있는 장치는 인원 감소로 숨기지 않는다.")]
        [SerializeField] bool scaleWithCrew;
        static readonly System.Collections.Generic.List<EscapeExitSaw> devices = new();
        public static System.Collections.Generic.IReadOnlyList<EscapeExitSaw> Devices => devices;
        [SerializeField] BoxCollider loadCollider;
        [SerializeField] GameObject intactCable, severedCable;
        [SerializeField] Renderer indicator;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] AudioSource motor;
        [SerializeField, Min(0)] float carryHandleHeight = 1.05f;
        Vector3 authoredCarryScale;
        bool hasAuthoredCarryScale;
        [SerializeField, Min(.05f)] float bladeRadius = .5f;
        [SerializeField, Min(.01f)] float bladeHalfThickness = .12f;
        [SerializeField, Min(.01f)] float cableRadius = .08f;
        [SerializeField] Vector3 bladeAxis = Vector3.forward;
        [SerializeField, Min(.1f)] float cutSeconds = 3f;
        [SerializeField, Min(.1f)] float doorLift = 8.5f;
        [SerializeField, Min(.1f)] float doorSpeed = 5f;
        [SerializeField] Transform leftDoorHinge, rightDoorHinge;
        Quaternion closedLeftDoorRotation, closedRightDoorRotation;
        float doorAngle;
        CannonSeatState ownership = new();
        readonly RaycastHit[] floorHits = new RaycastHit[24];
        readonly RouteResetBarrier resetBarrier = new();
        NetworkSessionManager session;
        EscapeInspectionSession inspection;
        Vector3 initialPosition, closedDoorPosition, lastSafeFeet;
        Quaternion initialRotation, initialBladeRotation;
        MaterialPropertyBlock tint;
        float cutProgress, bladeAngle;
        bool cutting, synchronized, initialized, carryBlocked;
        uint stateRevision;
        double nextPublish, nextSync;
        int resetFrame = -1;
        public static EscapeExitSaw Current { get; private set; }
        public ulong Carrier => ownership.Occupant;
        public Transform CarryRoot => carryRoot != null ? carryRoot : transform;
        public Transform Handle => handle != null ? handle : CarryRoot;
        public Vector3 GrabPoint => Handle.position;
        public Transform CableSocket => cableSocket;
        public Vector3 CarryFocus => blade != null ? blade.position : CarryRoot.position + Vector3.up;
        public Vector2 CarryHalfSize => loadCollider != null
            ? new Vector2(loadCollider.bounds.extents.x, loadCollider.bounds.extents.z) : new Vector2(.7f, .7f);
        public float CarryMoveSpeed => CurtainCall.Settings.GameSettings.Base.CarryDevices.SawCarrySpeed;
        public string CarryName => "휴대 절단기";
        public float CutProgress01 => ProjectileMode ? RequiredCableCount > 0 ? CutCableCount / (float)RequiredCableCount : 0
            : Mathf.Clamp01(cutProgress / Mathf.Max(.1f, cutSeconds));
        public bool DoorOpen
        {
            get
            {
                if (!ProjectileMode) return cutProgress >= cutSeconds;
                if (cutMask == 0) return false;
                uint required = RequiredMask;
                return required != 0 && (cutMask & required) == required;
            }
        }
        public bool IsCutting => cutting && !DoorOpen;
        [Tooltip("배선 안내 위치 기준 팀 임무 구역. 마지막 통로의 절단기부터 출구까지 포함합니다.")]
        [SerializeField] Bounds objectiveZone = new(new Vector3(-3,2,0), new Vector3(34,10,14));
        public bool TeamReachedExitZone { get; private set; }
        void WatchObjectiveZone()
        {
            if (deviceId != 0 || TeamReachedExitZone || !Ready || !session.IsHost || !cableSocket
                || !EscapeCampaignContent.IsTraversalActive) return;
            foreach (var member in NetworkPlayer.All)
            {
                if (!Alive(member) || member.Slot == NetworkPlayer.NoSlot || member.IsApplyingInitialCondition
                    || (inspection && inspection.IsInspecting(member.OwnerClientId))
                    || !objectiveZone.Contains(cableSocket.InverseTransformPoint(member.transform.position))) continue;
                TeamReachedExitZone = true;
                Changed();
                break;
            }
        }
        public string Guidance => DoorOpen ? "배선 절단 완료 · 열린 출구로 이동하세요"
            : carryBlocked ? "장치가 설비에 걸렸습니다 · 한 걸음 물러나 방향을 바꾸세요"
            : ProjectileMode && RequiredCableCount == 0 ? $"{UseKey} · 톱날 한 발 발사 · 배선 절단용(추격자 공격 불가)"
            : ProjectileMode ? $"배선 {CutCableCount}/{RequiredCableCount} 절단 · 색 전선을 조준하고 {UseKey}로 발사"
            : IsCutting ? $"배선 절단 중 {Mathf.CeilToInt(CutProgress01 * 100)}% · 날을 계속 맞대세요"
            : $"출구 옆 노란 배선에 회전 날을 {cutSeconds:0.#}초 동안 맞대세요";
        bool SessionReady => isActiveAndEnabled && synchronized && !resetBarrier.Waiting && session != null
            && session.GameState == GameSessionState.Playing;
        bool Ready => SessionReady && CarryRoot.gameObject.activeInHierarchy;
        bool ProjectileSessionReady => SessionReady && (CarryRoot.gameObject.activeInHierarchy || scaleWithCrew);
        string UseKey => NetworkPlayer.Local != null && NetworkPlayer.Local.TryGetComponent(out PlayerInputReader input)
            ? input.BindingLabel("UseDevice", "V") : "V";

        void ApplyCommonSize()
        {
            if (!hasAuthoredCarryScale) { authoredCarryScale = CarryRoot.localScale; hasAuthoredCarryScale = true; }
            CarryRoot.localScale = authoredCarryScale * CurtainCall.Settings.GameSettings.Base.CarryDevices.SawScale;
        }

        void OnEnable()
        {
            ApplyCommonSize();
            if (!devices.Contains(this)) devices.Add(this);
            if (Current == null) Current = this;
            InitializePose();
            inspection = FindFirstObjectByType<EscapeInspectionSession>();
            HumanCannonCarry.Register(this);
            NetworkPlayer.Restarted += Restart;
            Present(0, true);
        }
        void InitializePose()
        {
            if (initialized) return;
            initialPosition = CarryRoot.position; initialRotation = CarryRoot.rotation;
            if (doorPanel != null) closedDoorPosition = doorPanel.position;
            if (leftDoorHinge != null) closedLeftDoorRotation = leftDoorHinge.localRotation;
            if (rightDoorHinge != null) closedRightDoorRotation = rightDoorHinge.localRotation;
            if (blade != null) initialBladeRotation = blade.localRotation;
            InitializeProjectilePresentation();
            initialized = true;
        }
        // AddComponent 직후 필드를 연결하는 편집 도구도 첫 런타임 프레임에는 완성된 배치를 기준으로 한다.
        void Start() { initialized = false; InitializePose(); }
        void Update()
        {
            ApplyCommonSize();
            Attach();
            WatchObjectiveZone();
            if (scaleWithCrew && session != null) RefreshCrewAvailability(session.PlayerCount);
            RefreshCarrierCollision(Ready ? FindPlayer(Carrier) : null);
            if (!Ready && CrewAvailable && messaging != null && session != null && !session.IsHost && Time.unscaledTimeAsDouble >= nextSync)
            { nextSync = Time.unscaledTimeAsDouble + .5; SendRequest(0, resetBarrier.Waiting ? resetBarrier.Token : 0); }
        }
        void LateUpdate()
        {
            if (Ready)
            {
                var carrier = FindPlayer(Carrier);
                if (Alive(carrier)) FollowCarrier(carrier, false);
                if (session.IsHost && resetFrame != Time.frameCount) HostStep(Time.deltaTime);
                else if (!session.IsHost && ProjectileMode) StepProjectiles(Time.deltaTime, false);
            }
            else if (ProjectileSessionReady && ProjectileMode)
            {
                // Hiding an unoccupied spare must not freeze saws it has already fired.
                if (session.IsHost && resetFrame != Time.frameCount) HostProjectileStep(Time.deltaTime, null);
                else if (!session.IsHost) StepProjectiles(Time.deltaTime, false);
            }
            Present(Time.deltaTime);
            PresentProjectiles(Time.deltaTime);
        }

        public bool CanGrab(NetworkPlayer player)
        {
            if (!Ready || ownership.IsOccupied || !Grounded(player) || HumanCannonCarry.Held(player.OwnerClientId) != null) return false;
            var cannon = HumanCannonSystem.Current;
            if (cannon == null || cannon.IsSeated(player.OwnerClientId) || cannon.IsFlying(player.OwnerClientId)) return false;
            if (HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0
                || PiggybackSystem.Current != null && PiggybackSystem.Current.IsInStack(player)) return false;
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            return PlayerInteraction.InRange(player, GrabPoint, CurtainCall.Settings.GameSettings.Base.Interaction.Distance * reach, transform)
                && HumanCannonCarry.TryFollowPose(player, CarryRoot, Handle, carryHandleHeight, true, out var position, out var rotation, out _)
                && PoseFits(position, rotation, player);
        }
        public void RequestGrab() { if (CanGrab(NetworkPlayer.Local)) RequestAction(1); }
        public void RequestDrop() { if (NetworkPlayer.Local != null && Carrier == NetworkPlayer.Local.OwnerClientId) RequestAction(2); }
        public void RequestUse() { if (NetworkPlayer.Local != null && Carrier == NetworkPlayer.Local.OwnerClientId) RequestAction(3); }
        public void ServerReleaseForInspection(ulong actor)
        {
            if (session == null || !session.IsHost || Carrier != actor) return;
            ownership.TryApply(CannonAction.Release, actor, ownership.Revision);
            Recover(); Changed();
        }
        bool Grounded(NetworkPlayer player, bool requireReportedGrounded = true)
        {
            if (!Alive(player) || inspection != null && inspection.IsInspecting(player.OwnerClientId)
                || !player.TryGetComponent(out PlayerMover mover) || !mover.Body.enabled
                || player.IsLocal && !mover.Simulated) return false;
            bool reportedAirborne = player.IsAirborne || mover.IsAirborne;
            if (reportedAirborne && (requireReportedGrounded || player.IsLocal)) return false;
            var cannon = HumanCannonSystem.Current;
            if (cannon != null && (cannon.IsFlying(player.OwnerClientId) || cannon.IsSeated(player.OwnerClientId))) return false;
            float maxFootGap = reportedAirborne ? mover.Body.skinWidth * Mathf.Abs(mover.transform.lossyScale.y) : .25f;
            int count = Physics.RaycastNonAlloc(player.transform.position + Vector3.up * .15f, Vector3.down, floorHits, .4f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = floorHits[i];
                if (hit.normal.y > .5f && hit.distance <= .15f + maxFootGap && hit.collider != null && !hit.collider.transform.IsChildOf(CarryRoot)
                    && hit.collider.GetComponentInParent<NetworkPlayer>() == null) return true;
            }
            return false;
        }
        bool FollowCarrier(NetworkPlayer carrier, bool align)
        {
            RefreshCarrierCollision(carrier);
            if (!HumanCannonCarry.TryFollowPose(carrier, CarryRoot, Handle, carryHandleHeight, align,
                out var position, out var rotation, out var carrierRotation)) return false;
            carryBlocked = !HumanCannonCarry.CanMove(CarryRoot, loadCollider, position, rotation, carrier, !align);
            if (carryBlocked)
            {
                // 원본 CharacterController 충돌로 운반자의 마지막 유효 위치까지 되돌린다.
                if (carrier.IsLocal && carrier.TryGetComponent(out PlayerMover mover) && mover.Simulated && mover.Body.enabled)
                    mover.Body.Move(lastSafeFeet - carrier.transform.position);
                return false;
            }
            CarryRoot.SetPositionAndRotation(position, rotation);
            carrier.transform.rotation = carrierRotation;
            lastSafeFeet = carrier.transform.position;
            return true;
        }
        bool PoseFits(Vector3 position, Quaternion rotation, NetworkPlayer carrier)
        {
            return HumanCannonCarry.CanMove(CarryRoot, loadCollider, position, rotation, carrier, false);
        }
        void HostStep(float deltaTime)
        {
            var carrier = FindPlayer(Carrier);
            if (ownership.IsOccupied && (!Alive(carrier) || inspection != null && inspection.IsInspecting(Carrier)))
            {
                ownership.TryApply(CannonAction.Release, Carrier, ownership.Revision);
                Recover(); Changed();
            }
            if (ProjectileMode) { HostProjectileStep(deltaTime, carrier); return; }
            bool contact = !carryBlocked && !DoorOpen && ownership.IsOccupied && Grounded(carrier)
                && blade != null && blade.gameObject.activeInHierarchy && cableSocket != null && cableSocket.gameObject.activeInHierarchy
                && Vector3.Distance(Handle.position, carrier.transform.position + Vector3.up * carryHandleHeight) < .3f
                && BladeTouches(blade.position, blade.TransformDirection(bladeAxis), bladeRadius, bladeHalfThickness, cableSocket.position, cableRadius);
            bool wasOpen = DoorOpen, wasCutting = cutting;
            float before = cutProgress;
            TickCut(deltaTime, contact);
            if (before != cutProgress || wasCutting != cutting) stateRevision++;
            if (wasOpen != DoorOpen || wasCutting != cutting || Time.unscaledTimeAsDouble >= nextPublish)
            { nextPublish = Time.unscaledTimeAsDouble + .1; Publish(); }
        }
        void TickCut(float deltaTime, bool contact)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0) return;
            if (DoorOpen) { cutting = false; return; }
            cutting = contact;
            cutProgress = contact ? Mathf.Min(cutSeconds, cutProgress + Mathf.Min(deltaTime, .1f)) : 0;
            if (DoorOpen) cutting = false;
        }
        internal static bool BladeTouches(Vector3 center, Vector3 normal, float radius, float halfThickness, Vector3 cable, float cableThickness)
        {
            if (!Finite(center) || !Finite(normal) || !Finite(cable) || normal.sqrMagnitude < .001f
                || !float.IsFinite(radius) || !float.IsFinite(halfThickness) || !float.IsFinite(cableThickness)
                || radius <= 0 || halfThickness <= 0 || cableThickness < 0) return false;
            normal.Normalize(); Vector3 offset = cable - center;
            float depth = Vector3.Dot(offset, normal);
            return Mathf.Abs(depth) <= halfThickness + cableThickness
                && (offset - normal * depth).sqrMagnitude <= (radius + cableThickness) * (radius + cableThickness);
        }
        void Recover()
        {
            CarrierChanged();
            CarryRoot.SetPositionAndRotation(recoveryAnchor != null ? recoveryAnchor.position : initialPosition,
                recoveryAnchor != null ? recoveryAnchor.rotation : initialRotation);
            if (!DoorOpen) cutProgress = 0;
            cutting = carryBlocked = false;
        }
        void Present(float deltaTime, bool snapDoor = false)
        {
            RefreshCarrierCollision(Ready ? FindPlayer(Carrier) : null);
            if (leftDoorHinge != null && rightDoorHinge != null)
            {
                var settings = CurtainCall.Settings.GameSettings.EscapeRoute;
                float target = DoorOpen ? settings.ExitDoorOpenAngle : 0f;
                doorAngle = snapDoor ? target : Mathf.MoveTowards(doorAngle, target,
                    settings.ExitDoorOpenAngle / settings.ExitDoorOpenSeconds * Mathf.Max(0, deltaTime));
                leftDoorHinge.localRotation = closedLeftDoorRotation * Quaternion.Euler(0, -doorAngle, 0);
                rightDoorHinge.localRotation = closedRightDoorRotation * Quaternion.Euler(0, doorAngle, 0);
            }
            else if (doorPanel != null)
            {
                Vector3 target = closedDoorPosition + (DoorOpen ? Vector3.up * doorLift : Vector3.zero);
                if (snapDoor) doorPanel.position = target;
                else if (deltaTime > 0) doorPanel.position = Vector3.MoveTowards(doorPanel.position, target, doorSpeed * deltaTime);
            }
            bool running = Ready && Carrier != CannonSeatState.Nobody && (ProjectileMode || !DoorOpen);
            if (blade != null && running)
            { bladeAngle = Mathf.Repeat(bladeAngle + 1100f * deltaTime, 360); blade.localRotation = initialBladeRotation * Quaternion.AngleAxis(bladeAngle, bladeAxis); }
            if (!ProjectileMode && intactCable != null) intactCable.SetActive(!DoorOpen);
            if (!ProjectileMode && severedCable != null) severedCable.SetActive(DoorOpen);
            bool contactSparks = !ProjectileMode && IsCutting;
            if (sparks != null && sparks.isPlaying != contactSparks)
            { if (contactSparks) sparks.Play(); else sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            if (motor != null && motor.isPlaying != running) { if (running) motor.Play(); else motor.Stop(); }
            if (indicator != null)
            {
                tint ??= new MaterialPropertyBlock(); Color color = DoorOpen ? Color.green : IsCutting ? Color.yellow : Color.red;
                tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); indicator.SetPropertyBlock(tint);
            }
        }
        static bool Alive(NetworkPlayer player) => player != null && player.IsSpawned && player.State == PlayerState.Normal;
        static NetworkPlayer FindPlayer(ulong actor)
        {
            if (actor != CannonSeatState.Nobody) foreach (var player in NetworkPlayer.All)
                if (Alive(player) && player.OwnerClientId == actor) return player;
            return null;
        }
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        static bool Finite(Quaternion value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z)
            && float.IsFinite(value.w) && Quaternion.Dot(value, value) > .001f;
        void OnDisable()
        {
            RestoreCarrierCollision();
            DisposeProjectiles();
            NetworkPlayer.Restarted -= Restart; HumanCannonCarry.Unregister(this); Detach();
            devices.Remove(this);
            if (Current == this) Current = devices.Count > 0 ? devices[0] : null;
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (motor != null) motor.Stop();
        }
    }
}
