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
        static readonly System.Collections.Generic.List<EscapeExitSaw> devices = new();
        public static System.Collections.Generic.IReadOnlyList<EscapeExitSaw> Devices => devices;
        [SerializeField] BoxCollider loadCollider;
        [SerializeField] GameObject intactCable, severedCable;
        [SerializeField] Renderer indicator;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] AudioSource motor;
        [SerializeField, Min(.1f)] float grabDistance = 2.5f;
        [SerializeField, Min(0)] float carryHandleHeight = 1.05f;
        [SerializeField, Min(.1f)] float carryMoveSpeed = 3f;
        [SerializeField, Min(.05f)] float bladeRadius = .5f;
        [SerializeField, Min(.01f)] float bladeHalfThickness = .12f;
        [SerializeField, Min(.01f)] float cableRadius = .08f;
        [SerializeField] Vector3 bladeAxis = Vector3.forward;
        [SerializeField, Min(.1f)] float cutSeconds = 3f;
        [SerializeField, Min(.1f)] float doorLift = 8.5f;
        [SerializeField, Min(.1f)] float doorSpeed = 5f;
        CannonSeatState ownership = new();
        readonly Collider[] overlaps = new Collider[48];
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
        public Transform CableSocket => cableSocket;
        public Vector3 CarryFocus => blade != null ? blade.position : CarryRoot.position + Vector3.up;
        public Vector2 CarryHalfSize => loadCollider != null
            ? new Vector2(loadCollider.bounds.extents.x, loadCollider.bounds.extents.z) : new Vector2(.7f, .7f);
        public float CarryMoveSpeed => carryMoveSpeed;
        public string CarryName => "휴대 절단기";
        public float CutProgress01 => ProjectileMode ? RequiredCableCount > 0 ? CutCableCount / (float)RequiredCableCount : 0
            : Mathf.Clamp01(cutProgress / Mathf.Max(.1f, cutSeconds));
        public bool DoorOpen => ProjectileMode ? RequiredMask != 0 && (cutMask & RequiredMask) == RequiredMask : cutProgress >= cutSeconds;
        public bool IsCutting => cutting && !DoorOpen;
        public string Guidance => DoorOpen ? "배선 절단 완료 · 열린 출구로 이동하세요"
            : carryBlocked ? "장치가 설비에 걸렸습니다 · 한 걸음 물러나 방향을 바꾸세요"
            : ProjectileMode ? $"배선 {CutCableCount}/{RequiredCableCount} 절단 · 색 전선을 조준하면 {shotInterval:0.#}초마다 톱날 발사"
            : IsCutting ? $"배선 절단 중 {Mathf.CeilToInt(CutProgress01 * 100)}% · 날을 계속 맞대세요"
            : $"출구 옆 노란 배선에 회전 날을 {cutSeconds:0.#}초 동안 맞대세요";
        bool Ready => isActiveAndEnabled && synchronized && !resetBarrier.Waiting && session != null
            && session.GameState == GameSessionState.Playing && CarryRoot.gameObject.activeInHierarchy;

        void OnEnable()
        {
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
            if (blade != null) initialBladeRotation = blade.localRotation;
            InitializeProjectilePresentation();
            initialized = true;
        }
        // AddComponent 직후 필드를 연결하는 편집 도구도 첫 런타임 프레임에는 완성된 배치를 기준으로 한다.
        void Start() { initialized = false; InitializePose(); }
        void Update()
        {
            Attach();
            if (!Ready && messaging != null && session != null && !session.IsHost && Time.unscaledTimeAsDouble >= nextSync)
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
            return PlayerInteraction.InRange(player, Handle.position, grabDistance * reach)
                && HumanCannonCarry.TryFollowPose(player, CarryRoot, Handle, carryHandleHeight, true, out var position, out var rotation, out _)
                && PoseFits(position, rotation, player);
        }
        public void RequestGrab() { if (CanGrab(NetworkPlayer.Local)) RequestAction(1); }
        public void RequestDrop() { if (NetworkPlayer.Local != null && Carrier == NetworkPlayer.Local.OwnerClientId) RequestAction(2); }
        public void ServerReleaseForInspection(ulong actor)
        {
            if (session == null || !session.IsHost || Carrier != actor) return;
            ownership.TryApply(CannonAction.Release, actor, ownership.Revision);
            Recover(); Changed();
        }
        bool Grounded(NetworkPlayer player)
        {
            if (!Alive(player) || player.IsAirborne || inspection != null && inspection.IsInspecting(player.OwnerClientId)
                || !player.TryGetComponent(out PlayerMover mover) || mover.IsAirborne || !mover.Body.enabled
                || player.IsLocal && !mover.Simulated) return false;
            var cannon = HumanCannonSystem.Current;
            if (cannon != null && (cannon.IsFlying(player.OwnerClientId) || cannon.IsSeated(player.OwnerClientId))) return false;
            int count = Physics.RaycastNonAlloc(player.transform.position + Vector3.up * .15f, Vector3.down, floorHits, .4f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = floorHits[i];
                if (hit.normal.y > .5f && hit.collider != null && !hit.collider.transform.IsChildOf(CarryRoot)
                    && hit.collider.GetComponentInParent<NetworkPlayer>() == null) return true;
            }
            return false;
        }
        bool FollowCarrier(NetworkPlayer carrier, bool align)
        {
            if (!HumanCannonCarry.TryFollowPose(carrier, CarryRoot, Handle, carryHandleHeight, align,
                out var position, out var rotation, out var carrierRotation)) return false;
            carryBlocked = !PoseFits(position, rotation, carrier);
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
            if (loadCollider == null) return true;
            Vector3 center = CarryRoot.InverseTransformPoint(loadCollider.transform.TransformPoint(loadCollider.center));
            Vector3 half = Vector3.Scale(loadCollider.size * .5f, loadCollider.transform.lossyScale);
            half = new Vector3(Mathf.Max(.01f, Mathf.Abs(half.x) - .02f), Mathf.Max(.01f, Mathf.Abs(half.y) - .02f), Mathf.Max(.01f, Mathf.Abs(half.z) - .02f));
            Vector3 at = position + rotation * Vector3.Scale(center, CarryRoot.lossyScale);
            Quaternion relative = Quaternion.Inverse(CarryRoot.rotation) * loadCollider.transform.rotation;
            int count = Physics.OverlapBoxNonAlloc(at, half, overlaps, rotation * relative, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = overlaps[i];
                if (hit == null || hit.transform.IsChildOf(CarryRoot)
                    || carrier != null && hit.transform.IsChildOf(carrier.transform)) continue;
                return false;
            }
            return true;
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
            ClearShots();
            CarryRoot.SetPositionAndRotation(recoveryAnchor != null ? recoveryAnchor.position : initialPosition,
                recoveryAnchor != null ? recoveryAnchor.rotation : initialRotation);
            if (!DoorOpen) cutProgress = 0;
            cutting = carryBlocked = false;
        }
        void Present(float deltaTime, bool snapDoor = false)
        {
            if (doorPanel != null)
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
            DisposeProjectiles();
            NetworkPlayer.Restarted -= Restart; HumanCannonCarry.Unregister(this); Detach();
            devices.Remove(this);
            if (Current == this) Current = devices.Count > 0 ? devices[0] : null;
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (motor != null) motor.Stop();
        }
    }
}
