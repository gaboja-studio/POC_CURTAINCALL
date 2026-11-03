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
    /// <summary>호스트만 AI/포획을 판정. 방·배치 순서는 씬에서 고정하며 상태를 늦은 입장자에게도 계속 전송한다.</summary>
    public sealed partial class PursuitDirector : MonoBehaviour
    {
        const string Message = "CurtainCall.WaitingRoom.Pursuit.v2";
        public PursuitRoom[] Rooms;
        public GameObject[] Mannequins;
        public Unity.Behavior.BehaviorGraph BehaviourGraph;
        public GameObject CaptureBloodPrefab;
        public Sprite CaptureBloodSprite;
        public IReadOnlyList<PursuitActor> Actors => actors;
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
        [Serializable] sealed class ActorState { public Vector3 position, look; public bool hasLook, hasTarget; public Quaternion rotation; public int motion, behaviour, captures; public ulong victim = ulong.MaxValue, target = ulong.MaxValue; public float capture; }
        [Serializable] sealed class VictimState { public ulong id; public float elapsed; public bool retired; public int room; public Vector3 anchor; public Quaternion upright; }
        [Serializable] sealed class Snapshot { public int generation, day, performance, stage, poseEpoch; public bool stageForced, debugInvulnerable, debugIgnorePlayers; public string announcement; public float elapsed; public ActorState[] actors; public VictimState[] victims; }
        sealed class Victim { public GameObject Object; public PursuitVictimPose Pose; public float Elapsed; public bool Retired; public PursuitRoom Room; public Vector3 Anchor; public Quaternion Upright; }
        public static bool IsCaptured(GameObject player) => Current != null && Current.victims.Values.Any(v => v.Object == player);
        public static bool IsCaptured(NetworkPlayer player) => player != null && IsCaptured(player.gameObject);
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
            if (Rooms == null || Rooms.Length == 0) Rooms = GetComponentsInChildren<PursuitRoom>();
            int index = 0;
            foreach (var room in Rooms) {
                if (room != null && room.UsePlacements) {
                    foreach (var placement in room.Placements ?? Array.Empty<PursuitPlacement>()) {
                        // Awake can run before a placement's OnEnable; isActiveAndEnabled is not stable yet.
                        if (placement == null || !placement.enabled || !placement.gameObject.activeInHierarchy || placement.Prefab == null) continue;
                        Spawn(placement.Prefab, placement.transform, room, placement);
                    }
                    continue;
                }
                if (room == null || room.SpawnPoints == null || room.SpawnPoints.Length == 0) continue;
                for (int n = 0; n < Mathf.Clamp(room.Count, 0, 32); n++) {
                    if (Mannequins == null || Mannequins.Length == 0) continue;
                    var prefab = Mannequins[index++ % Mannequins.Length];
                    var point = room.SpawnPoints[n % room.SpawnPoints.Length];
                    if (prefab == null || point == null) continue;
                    Spawn(prefab, point, room, null);
                }
            }
            if (GetComponent<PursuitFeedback>() == null) gameObject.AddComponent<PursuitFeedback>();
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
            Bind();
            var nextRun = CampaignRun.Instance;
            if (run != nextRun) {
                if (run != null) run.StepChanged -= StepChanged;
                run = nextRun;
                if (run != null) run.StepChanged += StepChanged;
            }
            if (run == null) return;
            var s = run.State;
            if (s.Phase != CampaignPhase.RestTime) { if (active) StepChanged(s); return; }
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
                int living = candidates.Count(IsAvailable);
                bool pending = victims.Values.Any(v => !v.Retired && v.Object != null);
                // Final morning fails as soon as everyone is secured. Earlier days wait for the sequence.
                if (living == 0 && victims.Count > 0 && (s.IsFinalMorning || !pending)) run.NotifyAllPursuitCaught();
            }
            TickPoseBroadcast();
            if (Time.unscaledTime >= sendAt) { sendAt = Time.unscaledTime + .25f; Broadcast(); }
        }
        void ResetStep(CampaignSnapshot s)
        {
            // Timer edits preserve test protection; a new visit/run restores normal gameplay.
            if (s.Phase != CampaignPhase.RestTime || s.Generation != generation || s.Day != day || s.PerformanceIndex != performance)
                ResetDebugProtection();
            foreach (var victim in victims.Values) if (victim.Pose != null) Destroy(victim.Pose);
            victims.Clear(); active = false; Elapsed = -1; stage = -1; forcedStage = -1;
            footsteps.Clear();
            generation = s.Generation; day = s.Day; performance = s.PerformanceIndex;
            poseEpoch++; sendAt = 0;
            foreach (var actor in actors) actor.ResetActor();
        }
        void StepChanged(CampaignSnapshot s)
        {
            if (s.Phase == CampaignPhase.RestTime) { ResetStep(s); return; }
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
            if (p == null || !p.activeInHierarchy || IsCaptured(p)) return false;
            var network = p.GetComponent<NetworkPlayer>();
            return network == null || !network.IsSpawned || network.State == PlayerState.Normal;
        }
        public GameObject NearestVisible(PursuitActor actor, PursuitStage rule)
        {
            if (!PlayerRecognitionEnabled || actor == null || !actor.IsActivated) return null;
            GameObject closest = null; float distance = float.PositiveInfinity;
            foreach (var player in candidates) {
                if (!IsAvailable(player) || (!actor.Room.RoamAcrossRooms && !actor.Room.Contains(player.transform.position))) continue;
                if (!actor.WithinPatrolLeash(player.transform.position, rule)) continue;
                var delta = player.transform.position - actor.transform.position;
                float d = delta.sqrMagnitude;
                if (d >= distance) continue;
                if (!rule.AlwaysNearest && (d > rule.SightRadius * rule.SightRadius || Vector3.Angle(actor.transform.forward, delta) > rule.FieldOfView * .5f)) continue;
                if (!rule.SeeThroughWalls && !rule.AlwaysNearest && !ClearLine(actor.transform.position + Vector3.up * 1.5f, player.transform.position + Vector3.up, player.transform)) continue;
                distance = d; closest = player;
            }
            return closest;
        }
        public bool ClearLine(Vector3 from, Vector3 to, Transform target)
        {
            var hits = Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits) {
                if (hit.transform == target || hit.transform.IsChildOf(target) || hit.transform.GetComponentInParent<PursuitActor>() != null) continue;
                return false;
            }
            return true;
        }
        public void BeginCapture(PursuitActor actor, GameObject player)
        {
            if (run == null || !run.HasAuthority || !active || actor == null || !actor.IsActivated || CaptureSuppressed || !PlayerRecognitionEnabled || !IsAvailable(player)) return;
            var room = Rooms.Where(r => r != null && r.Contains(player.transform.position)).OrderBy(r => r.Area.bounds.size.sqrMagnitude).FirstOrDefault() ?? actor.Room;
            var visual = player.GetComponentInChildren<HumanoidModel>();
            var v = new Victim { Object = player, Room = room, Anchor = visual != null ? visual.transform.position : player.transform.position,
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
            var text = CurrentStageRules.Announcement;
            if (!string.IsNullOrEmpty(text)) WaitingRoomItemBrokenNotice.ShowMessage("추격 경보", text);
        }
        void Bind()
        {
            var nm = NetworkManager.Singleton;
            var next = nm != null && nm.IsListening ? nm.CustomMessagingManager : null;
            if (next == messaging) return;
            if (messaging != null && manager != null && !manager.IsServer) { messaging.UnregisterNamedMessageHandler(Message); messaging.UnregisterNamedMessageHandler(PoseMessage); }
            manager = nm; messaging = next;
            if (messaging != null && !manager.IsServer) { messaging.RegisterNamedMessageHandler(Message, Receive); messaging.RegisterNamedMessageHandler(PoseMessage, ReceivePoses); }
        }
        void Broadcast()
        {
            if (messaging == null || manager == null || !manager.IsServer) return;
            var s = new Snapshot { generation = generation, day = day, performance = performance, elapsed = Elapsed, stage = stage, poseEpoch = poseEpoch,
                stageForced = StageIsForced, debugInvulnerable = debugInvulnerable, debugIgnorePlayers = debugIgnorePlayers,
                announcement = active ? CurrentStageRules.Announcement : "",
                actors = actors.Select(a => new ActorState { position = a.transform.position, rotation = a.transform.rotation, look = a.LookPoint, hasLook = a.HasLook,
                    motion = (int)a.State, behaviour = (int)a.Behaviour, victim = a.Victim != null ? Id(a.Victim) : ulong.MaxValue, capture = a.CaptureElapsed,
                    captures = a.GetComponent<PursuitAppearance>() != null ? a.GetComponent<PursuitAppearance>().CaptureCount : 0,
                    hasTarget = a.ChasedPlayer != null, target = a.ChasedPlayer != null ? Id(a.ChasedPlayer) : ulong.MaxValue }).ToArray(),
                victims = victims.Where(v => v.Value.Object != null).Select(v => new VictimState { id = v.Key, elapsed = v.Value.Elapsed,
                    retired = v.Value.Retired, room = Array.IndexOf(Rooms, v.Value.Room), anchor = v.Value.Anchor, upright = v.Value.Upright }).ToArray() };
            string json = JsonUtility.ToJson(s);
            using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(json), Allocator.Temp);
            writer.WriteValueSafe(json);
            foreach (var id in manager.ConnectedClientsIds) if (id != NetworkManager.ServerClientId)
                messaging.SendNamedMessage(Message, id, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
        void Receive(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || run == null) return;
            reader.ReadValueSafe(out string json);
            var s = JsonUtility.FromJson<Snapshot>(json);
            ApplySnapshot(s);
        }
        void ApplySnapshot(Snapshot s)
        {
            var c = run.State;
            if (s == null || c.Phase != CampaignPhase.RestTime || s.generation != c.Generation || s.day != c.Day || s.performance != c.PerformanceIndex
                || s.actors == null || s.actors.Length != actors.Count || s.victims == null) return;
            if (s.elapsed < 0 && active) ResetStep(c);
            if (poseEpoch != s.poseEpoch) foreach (var actor in actors) actor.ResetActor();
            poseEpoch = s.poseEpoch;
            foreach (var id in victims.Keys.Where(id => !s.victims.Any(v => v.id == id)).ToArray()) {
                if (victims[id].Pose != null) Destroy(victims[id].Pose);
                victims.Remove(id);
            }
            Elapsed = s.elapsed; active = Elapsed >= 0;
            forcedStage = s.stageForced ? s.stage : -1;
            debugInvulnerable = s.debugInvulnerable; debugIgnorePlayers = s.debugIgnorePlayers;
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
                if (!string.IsNullOrEmpty(s.announcement)) WaitingRoomItemBrokenNotice.ShowMessage("추격 경보", s.announcement);
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
            if (messaging != null && manager != null && !manager.IsServer) { messaging.UnregisterNamedMessageHandler(Message); messaging.UnregisterNamedMessageHandler(PoseMessage); }
            foreach (var v in victims.Values) if (v.Pose != null) Destroy(v.Pose);
            if (Current == this) Current = null;
        }
    }
}
