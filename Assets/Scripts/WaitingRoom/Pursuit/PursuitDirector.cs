using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>호스트 AI/최종 포획 확정, 원격 피해자는 자신의 화면에서 접촉을 보고한다. 배치 순서는 씬에서 고정한다.</summary>
    public sealed partial class PursuitDirector : MonoBehaviour
    {
        const string Message = "CurtainCall.WaitingRoom.Pursuit.v4";
        const string ReadyMessage = "CurtainCall.WaitingRoom.Pursuit.v4.ready";
        readonly HashSet<ulong> readyClients = new();
        bool receivedSnapshot;
        float readyAt;
        /// <summary>마지막으로 보낸 스냅샷 크기(바이트). 측정 도구용.</summary>
        public int LastSnapshotBytes { get; private set; }
        /// <summary>측정용: 현재 상태를 이전 방식(JSON 문자열)과 새 방식(바이너리)으로 직렬화한 크기.</summary>
        public (int legacyJson, int binary) MeasureSnapshot()
        {
            var s = BuildSnapshot();
            int legacy = FastBufferWriter.GetWriteSize(JsonUtility.ToJson(s));
            using var writer = new FastBufferWriter(PursuitSnapshotCodec.MaxSize(s), Allocator.Temp);
            PursuitSnapshotCodec.Write(writer, s);
            return (legacy, writer.Length);
        }
        public PursuitRoom[] Rooms;
        public GameObject[] Mannequins;
        public Unity.Behavior.BehaviorGraph BehaviourGraph;
        public GameObject CaptureBloodPrefab;
        public Sprite CaptureBloodSprite;
        public IReadOnlyList<PursuitActor> Actors => actors;
        public bool IsInitialized { get; private set; }
        public double InitializationMs { get; private set; }
        public double InitializationWorkMs { get; private set; }
        public double MaxSpawnMs { get; private set; }
        public int InitializationFrames { get; private set; }
        readonly struct SpawnRequest
        {
            public readonly GameObject Prefab;
            public readonly Transform Point;
            public readonly PursuitRoom Room;
            public readonly PursuitPlacement Placement;
            public SpawnRequest(GameObject prefab, Transform point, PursuitRoom room, PursuitPlacement placement)
            { Prefab = prefab; Point = point; Room = room; Placement = placement; }
        }
        readonly List<SpawnRequest> pendingSpawns = new();
        int nextSpawn;
        double initializationStartedAt;
        static readonly Unity.Profiling.ProfilerMarker SpawnMarker = new("Pursuit.SpawnAndInitialize");
        public float Elapsed { get; private set; } = -1;
        public static PursuitDirector Current { get; private set; }
        readonly List<PursuitActor> actors = new();
        readonly Dictionary<ulong, Victim> victims = new();
        readonly List<GameObject> candidates = new();
        CampaignRun run;
        CustomMessagingManager messaging;
        NetworkManager manager;
        int stage = -1, generation, day, performance;
        float sendAt;
        internal void ActorStateChanged() => sendAt = 0;
        bool active;
        [Serializable] public sealed class ActorState { public Vector3 position, look; public bool hasLook, hasTarget; public Quaternion rotation; public int motion, behaviour, captures; public ulong victim = ulong.MaxValue, target = ulong.MaxValue; public float capture; }
        [Serializable] public sealed class VictimState { public ulong id; public float elapsed; public bool retired; public int room; public Vector3 anchor; public Quaternion upright; }
        [Serializable] public sealed class Snapshot { public int generation, day, performance, stage, poseEpoch, lightRoom = -1; public CampaignPhase phase = CampaignPhase.RestTime; public float lightAt = -1; public bool stageForced, debugInvulnerable, debugIgnorePlayers; public string announcement; public float elapsed; public ActorState[] actors; public VictimState[] victims; }
        sealed class Victim { public GameObject Object; public PursuitVictimPose Pose; public float Elapsed; public bool Retired; public PursuitRoom Room; public Vector3 Anchor; public Quaternion Upright; }
        public static bool IsCaptured(GameObject player) => Current != null && Current.victims.Values.Any(v => v.Object == player);
        public static bool IsCaptured(NetworkPlayer player) => player != null && IsCaptured(player.gameObject);
        public static bool IsCapturePending(NetworkPlayer player) => player != null && Current != null
            && Current.victims.Values.Any(v => v.Object == player.gameObject && !v.Retired);
        public static bool IsPursuitPhase(CampaignPhase phase) => phase == CampaignPhase.RestTime || phase == CampaignPhase.Night;
        public static Vector3 ArrivalLocal(BoxCollider area, int index, int count)
        {
            if (area == null) return Vector3.zero;
            var p = area.center;
            p.x += Mathf.Clamp((index - (count - 1) * .5f) * .6f, -area.size.x * .35f, area.size.x * .35f);
            p.y -= area.size.y * .5f - .1f;
            return p;
        }

        void Awake()
        {
            Current = this;
            initializationStartedAt = Time.realtimeSinceStartupAsDouble;
            if (Rooms == null || Rooms.Length == 0) Rooms = GetComponentsInChildren<PursuitRoom>();
            int index = 0;
            foreach (var room in Rooms) {
                if (room != null && room.UsePlacements) {
                    foreach (var placement in room.Placements ?? Array.Empty<PursuitPlacement>()) {
                        // Awake can run before a placement's OnEnable; isActiveAndEnabled is not stable yet.
                        if (placement == null || !placement.enabled || !placement.gameObject.activeInHierarchy || placement.Prefab == null) continue;
                        pendingSpawns.Add(new SpawnRequest(placement.Prefab, placement.transform, room, placement));
                    }
                    continue;
                }
                if (room == null || room.SpawnPoints == null || room.SpawnPoints.Length == 0) continue;
                for (int n = 0; n < Mathf.Clamp(room.Count, 0, 32); n++) {
                    if (Mannequins == null || Mannequins.Length == 0) continue;
                    var prefab = Mannequins[index++ % Mannequins.Length];
                    var point = room.SpawnPoints[n % room.SpawnPoints.Length];
                    if (prefab == null || point == null) continue;
                    pendingSpawns.Add(new SpawnRequest(prefab, point, room, null));
                }
            }
            if (GetComponent<PursuitFeedback>() == null) gameObject.AddComponent<PursuitFeedback>();
            if (GetComponent<PursuitLighting>() == null) gameObject.AddComponent<PursuitLighting>();
            if (Application.isPlaying && GetComponent<PursuitEyeFeedback>() == null) gameObject.AddComponent<PursuitEyeFeedback>();
            IsInitialized = pendingSpawns.Count == 0;
        }
        void InitializeNext()
        {
            // Keep serialized order identical on host and replicas. Bound work by a 2ms budget
            // and four actors per frame, then let the final actor's Start settle.
            if (nextSpawn < pendingSpawns.Count) {
                double frameStarted = Time.realtimeSinceStartupAsDouble;
                int count = 0;
                do {
                    var request = pendingSpawns[nextSpawn++];
                    double started = Time.realtimeSinceStartupAsDouble;
                    using (SpawnMarker.Auto()) {
                        if (request.Prefab != null && request.Point != null && request.Room != null)
                            Spawn(request.Prefab, request.Point, request.Room, request.Placement);
                    }
                    double ms = (Time.realtimeSinceStartupAsDouble - started) * 1000;
                    InitializationWorkMs += ms; MaxSpawnMs = Math.Max(MaxSpawnMs, ms);
                } while (++count < 4 && nextSpawn < pendingSpawns.Count
                    && (Time.realtimeSinceStartupAsDouble - frameStarted) * 1000 < 2);
                InitializationFrames++;
                return;
            }
            pendingSpawns.Clear();
            InitializationMs = (Time.realtimeSinceStartupAsDouble - initializationStartedAt) * 1000;
            IsInitialized = true;
        }
        void Spawn(GameObject prefab, Transform point, PursuitRoom room, PursuitPlacement placement)
        {
            var go = Instantiate(prefab, point.position, point.rotation, transform);
            if (placement != null) go.transform.localScale *= placement.SizeScale;
            go.name = $"Pursuer_{actors.Count:00}_{room.name}";
            var actor = go.GetComponent<PursuitActor>() ?? go.AddComponent<PursuitActor>();
            actor.Initialize(this, room, placement); actors.Add(actor);
        }
        void Update()
        {
            if (!IsInitialized) { InitializeNext(); return; }
            Bind();
            SendReady();
            var nextRun = CampaignRun.Instance;
            if (run != nextRun) {
                if (run != null) run.StepChanged -= StepChanged;
                run = nextRun;
                if (run != null) run.StepChanged += StepChanged;
            }
            if (run == null) return;
            var s = run.State;
            if (!IsPursuitPhase(s.Phase)) { if (active) StepChanged(s); return; }
            if (!run.HasAuthority) {
                foreach (var v in victims.Values) { v.Elapsed += Time.deltaTime; Present(v); }
                return;
            }
            if (!CurtainCall.Dev.DevMode.Enabled && (debugInvulnerable || debugIgnorePlayers)) ResetDebugProtection();
            if (s.Generation != generation || s.Day != day || s.PerformanceIndex != performance) ResetStep(s);
            CollectCandidates();
            if (!active && s.Timer == RestTimerState.Pursuit) { active = true; Elapsed = 0; }
            if (active) Elapsed += Time.deltaTime;
            if (active) SampleFootsteps();
            bool warning = s.Timer == RestTimerState.Running && s.TimeRemaining <= GameSettings.WaitingRoom.Pursuit.WarningSeconds;
            foreach (var actor in actors) actor.ServerTick(Elapsed, warning);
            if (active) {
                foreach (var victim in victims.Values) {
                    victim.Elapsed += Time.deltaTime;
                    Present(victim);
                }
                Announce();
                TickLighting();
                int living = candidates.Count(IsAvailable);
                bool pending = victims.Values.Any(v => !v.Retired && v.Object != null);
                // Final morning fails as soon as everyone is secured. Earlier days wait for the sequence.
                bool sleeping = candidates.Any(IsSleeping);
                if (living == 0 && !sleeping && victims.Count > 0 && (s.IsFinalMorning || !pending)) run.NotifyAllPursuitCaught();
            }
            TickPoseBroadcast();
            if (Time.unscaledTime >= sendAt) { sendAt = Time.unscaledTime + .25f; Broadcast(); }
        }
        /// <summary>클라이언트: 이 씬의 수신기가 준비되면 첫 스냅샷을 받을 때까지 1초마다 준비를 알린다.</summary>
        void SendReady()
        {
            if (messaging == null || manager == null || manager.IsServer || receivedSnapshot || Time.unscaledTime < readyAt) return;
            readyAt = Time.unscaledTime + 1;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteByteSafe(PursuitSnapshotCodec.Version);
            messaging.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        }
        void ReceiveReady(ulong sender, FastBufferReader reader)
        {
            if (sender == NetworkManager.ServerClientId) return;
            if (!reader.TryBeginRead(1)) return;
            reader.ReadByte(out byte version);
            if (version == PursuitSnapshotCodec.Version) readyClients.Add(sender);
        }
        void ResetStep(CampaignSnapshot s)
        {
            // Timer edits preserve test protection; a new visit/run restores normal gameplay.
            if (!IsPursuitPhase(s.Phase) || s.Generation != generation || s.Day != day || s.PerformanceIndex != performance)
                ResetDebugProtection();
            foreach (var victim in victims.Values) if (victim.Pose != null) Destroy(victim.Pose);
            victims.Clear(); active = false; Elapsed = -1; stage = -1; forcedStage = -1;
            footsteps.Clear();
            ResetLighting();
            ResetContacts();
            generation = s.Generation; day = s.Day; performance = s.PerformanceIndex;
            poseEpoch++; sendAt = 0;
            foreach (var actor in actors) actor.ResetActor();
        }
        void StepChanged(CampaignSnapshot s)
        {
            if (IsPursuitPhase(s.Phase)) { ResetStep(s); return; }
            if (run != null && run.HasAuthority && s.Generation == generation && (s.Phase == CampaignPhase.Performance || s.Phase == CampaignPhase.Escape)) {
                foreach (var victim in victims.Values) {
                    if (victim.Object == null) continue;
                    var player = victim.Object.GetComponent<NetworkPlayer>();
                    if (player != null && player.IsSpawned) player.ServerReleaseFromPursuit();
                }
            }
            ResetStep(s);
        }
        void CollectCandidates()
        {
            candidates.Clear();
            if (NetworkPlayer.All.Count > 0) {
                foreach (var p in NetworkPlayer.All)
                    if (p != null && p.IsSpawned && p.Slot != NetworkPlayer.NoSlot && !p.IsApplyingInitialCondition) candidates.Add(p.gameObject);
            } else {
                foreach (var p in FindObjectsByType<PlayerMover>(FindObjectsSortMode.None))
                    if (p.isActiveAndEnabled) candidates.Add(p.gameObject);
            }
        }
        public bool IsAvailable(GameObject p)
        {
            if (p == null || !p.activeInHierarchy || IsCaptured(p) || IsSleeping(p)) return false;
            var network = p.GetComponent<NetworkPlayer>();
            return network == null || !network.IsSpawned || network.State == PlayerState.Normal;
        }
        bool IsSleeping(GameObject p)
        {
            if (p == null || run == null || run.State.Phase != CampaignPhase.Night) return false;
            var player = p.GetComponent<NetworkPlayer>();
            return player != null && player.IsSpawned ? run.IsPlayerSleeping(player.OwnerClientId) : run.IsLocalReady;
        }
        public GameObject NearestVisible(PursuitActor actor, PursuitStage rule)
        {
            if (!PlayerRecognitionEnabled || actor == null || !actor.IsActivated) return null;
            if (rule.AlwaysNearest) return NearestByPath(actor, rule);
            GameObject closest = null; float distance = float.PositiveInfinity;
            foreach (var player in candidates) {
                if (player == null) continue;
                float d = (player.transform.position - actor.transform.position).sqrMagnitude;
                if (d >= distance) continue;
                if (!HasVisualContact(actor, player, rule)) continue;
                distance = d; closest = player;
            }
            return closest;
        }
        public bool HasVisualContact(PursuitActor actor, GameObject player, PursuitStage rule)
        {
            if (!PlayerRecognitionEnabled || actor == null || !actor.IsActivated || !IsAvailable(player)
                || (!actor.Room.RoamAcrossRooms && !actor.Room.Contains(player.transform.position))) return false;
            var delta = player.transform.position - actor.transform.position;
            if (delta.sqrMagnitude > rule.SightRadius * rule.SightRadius
                || Vector3.Angle(actor.transform.forward, delta) > rule.FieldOfView * .5f) return false;
            return rule.SeeThroughWalls || ClearLine(actor.transform.position + Vector3.up * 1.5f,
                player.transform.position + Vector3.up, player.transform);
        }
        readonly List<(float straight, GameObject player)> nearestOrder = new();
        // 최인접 추격은 벽 너머 직선 거리가 아니라 실제로 달려갈 NavMesh 경로 거리로 고른다.
        // 직선 거리는 경로 길이의 하한이므로 가까운 순으로 보고, 이미 찾은 경로보다 직선이 먼 대상은 계산하지 않는다.
        GameObject NearestByPath(PursuitActor actor, PursuitStage rule)
        {
            nearestOrder.Clear();
            foreach (var player in candidates) {
                if (!IsAvailable(player) || (!actor.Room.RoamAcrossRooms && !actor.Room.Contains(player.transform.position))) continue;
                if (!actor.WithinPatrolLeash(player.transform.position, rule) && !HasVisualContact(actor, player, rule)) continue;
                nearestOrder.Add((Vector3.Distance(player.transform.position, actor.transform.position), player));
            }
            nearestOrder.Sort((a, b) => a.straight.CompareTo(b.straight));
            GameObject closest = null; float distance = float.PositiveInfinity;
            foreach (var (straight, player) in nearestOrder) {
                if (straight >= distance) break;
                float d = actor.PathCost(player);
                if (d >= distance) continue;
                distance = d; closest = player;
            }
            return closest;
        }
        readonly RaycastHit[] sightHits = new RaycastHit[32];
        public bool ClearLine(Vector3 from, Vector3 to, Transform target)
        {
            var direction = to - from;
            float distance = direction.magnitude;
            int count = Physics.RaycastNonAlloc(from, direction, sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            // A full buffer may omit an occluder: use the complete result in dense geometry.
            var hits = count == sightHits.Length
                ? Physics.RaycastAll(from, direction, distance, ~0, QueryTriggerInteraction.Ignore) : sightHits;
            if (hits != sightHits) count = hits.Length;
            for (int i = 0; i < count; i++) {
                var hit = hits[i];
                if (hit.transform == target || hit.transform.IsChildOf(target) || hit.transform.GetComponentInParent<PursuitActor>() != null) continue;
                return false;
            }
            return true;
        }
        public void BeginCapture(PursuitActor actor, GameObject player)
        {
            // Host/offline victims have no network presentation delay. A remote victim can only
            // enter through a validated owner contact, even when the host's old position is close.
            if (RequiresOwnerContact(player)) return;
            CommitCapture(actor, player);
        }
        void CommitCapture(PursuitActor actor, GameObject player, Vector3? contactPosition = null)
        {
            if (run == null || !run.HasAuthority || !active || actor == null || actor.Victim != null || !actor.IsActivated || CaptureSuppressed || !PlayerRecognitionEnabled || !IsAvailable(player)) return;
            var contactPoint = contactPosition ?? player.transform.position;
            var room = Rooms.Where(r => r != null && r.Contains(contactPoint)).OrderBy(r => r.Area.bounds.size.sqrMagnitude).FirstOrDefault() ?? actor.Room;
            var visual = player.GetComponentInChildren<HumanoidModel>();
            var v = new Victim { Object = player, Room = room,
                Anchor = contactPoint + (visual != null ? visual.transform.position - player.transform.position : Vector3.zero),
                Upright = visual != null ? visual.transform.rotation : player.transform.rotation };
            victims[Id(player)] = v;
            actor.Capture(player, 0);
            Present(v);
        }
        public void AdvanceCapture(PursuitActor actor)
        {
            if (actor.Victim == null || !victims.TryGetValue(Id(actor.Victim), out var v)) return;
            var rules = GameSettings.WaitingRoom.Pursuit;
            var network = v.Object.GetComponent<NetworkPlayer>();
            var condition = v.Object.GetComponent<PlayerCondition>();
            float elapsed = actor.CaptureElapsed;
            int cuts = Mathf.Clamp(Mathf.FloorToInt((elapsed - rules.KnockdownSeconds) / Mathf.Max(.1f, rules.CutInterval)) + 1, 0, 3);
            for (int i = 0; i < cuts; i++) {
                var part = PlayerCondition.CutOrder[i];
                if (network != null && network.IsSpawned) {
                    if ((network.LostParts & part) == 0) network.ServerCutPart(part);
                } else if (condition != null && (condition.LostParts & part) == 0) condition.LosePart(part);
            }
            if (elapsed < rules.KnockdownSeconds + rules.CutInterval * 3) return;
            if (network != null && network.IsSpawned) network.ServerRetireFromPursuit();
            else condition?.SetLostParts(PlayerCondition.OneArmParts);
            v.Retired = true; Present(v); actor.FinishCapture();
        }
        static ulong Id(GameObject player) {
            var p = player != null ? player.GetComponent<NetworkPlayer>() : null;
            return p != null && p.IsSpawned ? p.OwnerClientId : ulong.MaxValue;
        }
        GameObject Resolve(ulong id)
        {
            if (id == ulong.MaxValue) return candidates.FirstOrDefault();
            foreach (var p in NetworkPlayer.All) if (p != null && p.IsSpawned && p.OwnerClientId == id) return p.gameObject;
            return null;
        }
        void Present(Victim v)
        {
            if (v.Object == null) return;
            if (v.Pose == null) {
                v.Pose = v.Object.GetComponent<PursuitVictimPose>();
                if (v.Pose == null) v.Pose = v.Object.AddComponent<PursuitVictimPose>();
            }
            v.Pose.Elapsed = v.Elapsed; v.Pose.Hidden = v.Retired;
            v.Pose.SetCaptureFrame(v.Anchor, v.Upright);
            if (!v.Retired && v.Room != null) v.Room.Flicker(Time.time + .2f);
        }
        void Announce()
        {
            int next = CurrentStageIndex;
            if (next == stage) return;
            stage = next;
            var text = PursuitTexts.StageAnnouncement(next);
            if (!string.IsNullOrEmpty(text)) WaitingRoomItemBrokenNotice.ShowMessage(PursuitTexts.AlertTitle, text);
        }
        void Bind()
        {
            var nm = NetworkManager.Singleton;
            var next = nm != null && nm.IsListening ? nm.CustomMessagingManager : null;
            if (next == messaging) return;
            Unbind();
            manager = nm; messaging = next;
            if (messaging == null) return;
            if (manager.IsServer) { messaging.RegisterNamedMessageHandler(ReadyMessage, ReceiveReady); messaging.RegisterNamedMessageHandler(ContactMessage, ReceiveContact); }
            else { messaging.RegisterNamedMessageHandler(Message, Receive); messaging.RegisterNamedMessageHandler(PoseMessage, ReceivePoses); receivedSnapshot = false; readyAt = 0; }
        }
        void Unbind()
        {
            if (messaging != null && manager != null) {
                if (manager.IsServer) { messaging.UnregisterNamedMessageHandler(ReadyMessage); messaging.UnregisterNamedMessageHandler(ContactMessage); }
                else { messaging.UnregisterNamedMessageHandler(Message); messaging.UnregisterNamedMessageHandler(PoseMessage); }
            }
            readyClients.Clear();
            ResetContacts();
        }
        Snapshot BuildSnapshot() => new Snapshot { generation = generation, day = day, performance = performance, phase = run != null ? run.State.Phase : CampaignPhase.RestTime, elapsed = Elapsed, stage = stage,
            poseEpoch = poseEpoch, debugInvulnerable = debugInvulnerable, debugIgnorePlayers = debugIgnorePlayers,
            stageForced = StageIsForced, lightRoom = RoomFlickerIndex, lightAt = RoomFlickerAt, announcement = active ? PursuitTexts.StageAnnouncement(CurrentStageIndex) : "",
            actors = actors.Select(a => new ActorState { position = a.transform.position, rotation = a.transform.rotation, look = a.LookPoint, hasLook = a.HasLook,
                motion = (int)a.State, behaviour = (int)a.Behaviour, victim = a.Victim != null ? Id(a.Victim) : ulong.MaxValue, capture = a.CaptureElapsed,
                captures = a.GetComponent<PursuitAppearance>() != null ? a.GetComponent<PursuitAppearance>().CaptureCount : 0,
                hasTarget = a.ChasedPlayer != null, target = a.ChasedPlayer != null ? Id(a.ChasedPlayer) : ulong.MaxValue }).ToArray(),
            victims = victims.Where(v => v.Value.Object != null).Select(v => new VictimState { id = v.Key, elapsed = v.Value.Elapsed,
                retired = v.Value.Retired, room = Array.IndexOf(Rooms, v.Value.Room), anchor = v.Value.Anchor, upright = v.Value.Upright }).ToArray() };
        void Broadcast()
        {
            if (!IsInitialized || messaging == null || manager == null || !manager.IsServer) return;
            // 씬을 다 불러와 수신 준비를 알린 클라이언트에게만 보낸다. 로딩으로 멈춘 클라이언트에 쌓이지 않게 한다.
            readyClients.RemoveWhere(id => !manager.ConnectedClients.ContainsKey(id));
            if (readyClients.Count == 0) return;
            var s = BuildSnapshot();
            using var writer = new FastBufferWriter(PursuitSnapshotCodec.MaxSize(s), Allocator.Temp);
            PursuitSnapshotCodec.Write(writer, s);
            LastSnapshotBytes = writer.Length;
            // 위치는 별도 비신뢰성 패킷으로 보간한다. 포획·단계·치트·리셋 상태는 신뢰성 경로로 유지한다.
            var delivery = NetworkDelivery.ReliableFragmentedSequenced;
            foreach (var id in readyClients) messaging.SendNamedMessage(Message, id, writer, delivery);
        }
        void Receive(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || run == null) return;
            var s = PursuitSnapshotCodec.Read(reader);
            if (s == null) return;
            receivedSnapshot = true;
            // 알림 문구는 같은 문구 표·설정 에셋에서 읽어 전송량을 줄인다.
            s.announcement = s.elapsed >= 0 ? PursuitTexts.StageAnnouncement(s.stage) : "";
            ApplySnapshot(s);
        }
        void ApplySnapshot(Snapshot s)
        {
            if (!IsInitialized || run == null || s == null) return;
            var c = run.State;
            if (!IsPursuitPhase(c.Phase) || s.phase != c.Phase || s.generation != c.Generation || s.day != c.Day || s.performance != c.PerformanceIndex
                || s.actors == null || s.actors.Length != actors.Count || s.victims == null) return;
            if (s.elapsed < 0 && active) ResetStep(c);
            if (poseEpoch != s.poseEpoch) { ResetContacts(); foreach (var actor in actors) actor.ResetActor(); }
            poseEpoch = s.poseEpoch;
            foreach (var id in victims.Keys.Where(id => !s.victims.Any(v => v.id == id)).ToArray()) {
                if (victims[id].Pose != null) Destroy(victims[id].Pose);
                victims.Remove(id);
            }
            Elapsed = s.elapsed; active = Elapsed >= 0;
            forcedStage = s.stageForced ? s.stage : -1;
            debugInvulnerable = s.debugInvulnerable; debugIgnorePlayers = s.debugIgnorePlayers;
            ReceiveLighting(s.lightRoom, s.lightAt);
            generation = s.generation; day = s.day; performance = s.performance;
            for (int i = 0; i < actors.Count; i++) {
                var a = s.actors[i];
                actors[i].Receive(a.position, a.rotation, (PursuitMotion)a.motion, a.motion >= (int)PursuitMotion.Grab ? Resolve(a.victim) : null, a.capture, a.look, a.hasLook, a.hasTarget ? Resolve(a.target) : null);
                actors[i].ReceiveBehaviour((PursuitBehaviour)a.behaviour);
                actors[i].GetComponent<PursuitAppearance>()?.SetCaptureCount(a.captures);
            }
            foreach (var state in s.victims) {
                if (!victims.TryGetValue(state.id, out var v)) victims[state.id] = v = new Victim();
                v.Object = Resolve(state.id); v.Elapsed = state.elapsed; v.Retired = state.retired;
                v.Anchor = state.anchor; v.Upright = state.upright;
                v.Room = state.room >= 0 && state.room < Rooms.Length ? Rooms[state.room] : null;
                Present(v);
            }
            if (active && stage != s.stage) {
                stage = s.stage;
                if (!string.IsNullOrEmpty(s.announcement)) WaitingRoomItemBrokenNotice.ShowMessage(PursuitTexts.AlertTitle, s.announcement);
            }
            if (!active) stage = -1;
        }
        [ContextMenu("개발/오프라인 추격 시작")]
        public void DebugStart()
        {
            var campaign = CampaignRun.EnsureExists();
            if (!campaign.State.IsRunning) campaign.StartOfflineRun();
            if (CurtainCall.Dev.DevMode.Enabled) campaign.DebugStartAt(new RunStartPoint(2, RunStartPhase.Morning, 0, "Tightrope"));
            else {
                if (campaign.State.Phase == CampaignPhase.Intro) campaign.EndIntro();
                if (campaign.State.Phase == CampaignPhase.Performance) campaign.EndPerformance();
            }
            campaign.DebugSetRemaining(.05f);
        }
        void OnDestroy()
        {
            if (run != null) run.StepChanged -= StepChanged;
            Unbind();
            foreach (var v in victims.Values) if (v.Pose != null) Destroy(v.Pose);
            if (Current == this) Current = null;
        }
    }
}
