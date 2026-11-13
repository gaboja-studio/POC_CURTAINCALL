using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.SpaceLink;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Owner positions and host world snapshots on the NGO server clock; never drives gameplay.</summary>
    sealed class SpatialLogAdapter : IDisposable
    {
        readonly TelemetryRuntime log;
        [Serializable] sealed class MapIndex { public MapEntry[] maps; }
        [Serializable] sealed class MapEntry { public string id, scene, sectionName; public int sectionId; public float floorY,minX,maxX,minZ,maxZ,minY,maxY; }
        readonly MapEntry[] mapEntries;
        readonly Dictionary<PursuitActor, string> pursuitStates = new();
        readonly Dictionary<(int, string), bool> clueAvailability = new();
        readonly Dictionary<string, bool> discoveries = new();
        readonly Dictionary<string, string> worldStates = new();
        readonly Dictionary<string, Vector3> worldPositions = new();
        readonly List<Collider> geometry = new();
        readonly List<MonoBehaviour> objects = new();
        PursuitRoom[] rooms = Array.Empty<PursuitRoom>();
        float nextPlayer, nextWorld, nextCache;
        Vector3 lastPosition;
        bool hasPosition, captured, dead, arrived, chased;
        string scope;
        (bool active, int stage, bool completed, bool practice)? rehearsal;
        (bool active, bool ending, bool mastered, int checkpoint, bool arrived)? cannonRehearsal;
        readonly Dictionary<string, string> ownerStates = new();
        readonly Dictionary<string, int> previousSizes = new();
        int bounceCount;
        HumanCannonPoleLoop[] conveyors = Array.Empty<HumanCannonPoleLoop>();
        EscapeCooperativeSpan[] bridges = Array.Empty<EscapeCooperativeSpan>();
        TightropeCourse observedCourse;
        readonly Dictionary<int,List<Vector2>> recordedSpans = new();
        readonly Dictionary<int,bool> relayConnections = new();
        int geometryIndex;
        bool escapeStarted;
        CampaignPerformanceEntry performanceEntry;
        HumanCannonHazardCourse hazards;
        BodyPart lastLost;
        bool bodySeen;
        int lastEscapeSection;
        readonly HashSet<int> visitedSections = new();
        bool Host => NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.IsHost;
        string Scene => TelemetryRuntime.SceneType(log.CurrentSceneName);
        string Scope => log.CurrentRunId + "/" + log.CurrentSceneName + "/"
            + (Scene == "MAINSTAGE" ? log.CurrentAttemptId : Scene == "WAITINGROOM" ? log.CurrentBreakId ?? "REHEARSAL" : "ESCAPE");

        public SpatialLogAdapter(TelemetryRuntime runtime)
        {
            log = runtime;
            var manifest = Resources.Load<TextAsset>("TelemetrySpatialMaps");
            mapEntries = manifest != null ? JsonUtility.FromJson<MapIndex>(manifest.text).maps : Array.Empty<MapEntry>();
            ClueInspectable.InspectionObserved += Inspected;
            ClueInspectable.AvailabilityObserved += Availability;
            ShopReach.ClueFocused += Focused;
            ExplorationState.TelemetryAttempted += Explored;
            ExplorationState.RewardDropped += RewardDrop;
            PerformanceRewards.AudienceReacted += Audience;
            ScheduleBoard.SelectionChanged += Schedule;
            CampaignRun.TrickDiscovered += Unlocked;
            EscapeExitSaw.ShotObserved += Shot;
            TightropeHangSystem.ServerRescueSucceeded += Rescue;

            NetworkSessionManager.PeerDeparted += Departed;
        }

        public void Enrich(LogEvent e, NetworkPlayer player)
        {
            if (Scene == "EXITROUTE" && !e.eventType.StartsWith("PERFORMANCE_", StringComparison.Ordinal)) { e.sceneType = "EXITROUTE"; e.breakId = null; }
            if (e.sceneType is not ("WAITINGROOM" or "MAINSTAGE" or "EXITROUTE")) return;
            var manager = NetworkSessionManager.Instance?.NetworkManager;
            e.payload["spatialVersion"] = 1;
            e.payload.TryAdd("spatialScope", Scope);
            e.payload["clockSource"] = manager != null && manager.IsListening ? "SERVER" : "LOCAL";
            e.payload["timelineMs"] = manager != null && manager.IsListening ? manager.ServerTime.Time * 1000d : Time.realtimeSinceStartupAsDouble * 1000d;
            e.payload["clockId"] = manager != null && manager.IsListening ? log.CurrentRoomId ?? "unresolved" : log.FallbackPlayerId;
            if (e.eventType == "SPATIAL_SAMPLE" && e.sceneType == "WAITINGROOM" && player != null)
            {
                var room = ContainingRoom(player.transform.position);
                e.payload["roomName"] = room != null ? room.name : null;
                e.payload["headingDeg"] = player.transform.eulerAngles.y;
            }
            if (Ready(player) && !e.payload.ContainsKey("position")
                && e.eventType is not ("SCENE_ENTERED" or "SCENE_EXITED" or "SPATIAL_GEOMETRY"))
                e.payload["position"] = DetailLogIds.Position(player.transform.position);
            if (e.payload.TryGetValue("objectPosition",out var objectPoint) && objectPoint!=null && player!=null)
                e.payload.TryAdd("objectFloorY", e.payload.GetValueOrDefault("floorY") ?? Floor(player.transform.position));
            if (!e.payload.ContainsKey("floorY") && player != null) e.payload["floorY"] = Floor(player.transform.position);
            if (e.payload.TryGetValue("floorY", out var height)) {
                var map = mapEntries.Where(m=>m.scene==log.CurrentSceneName && m.scene!="Tightrope" && m.sectionId==0).OrderBy(m=>Math.Abs(m.floorY-Convert.ToSingle(height))).FirstOrDefault();
                if (map != null) { e.payload["mapId"] = map.id; e.payload["floorY"] = map.floorY; }
            }
            if (log.CurrentSceneName == "Tightrope") e.payload["mapId"] = "Tightrope_base_021";
            e.payload["sceneName"] = log.CurrentSceneName;
        }

        PursuitRoom ContainingRoom(Vector3 position) => rooms.Where(r => r != null && r.Area != null && r.Contains(position))
            .OrderBy(r => r.Area.bounds.size.sqrMagnitude).FirstOrDefault();

        float Floor(Vector3 position)
        {
            if (Scene != "WAITINGROOM" || rooms.Length == 0) return 0;
            var room = ContainingRoom(position);
            if (room == null) room = rooms.Where(r => r != null && r.Area != null)
                .OrderBy(r => Mathf.Abs(FloorOf(r) - position.y)).FirstOrDefault();
            return room != null ? FloorOf(room) : 0;
        }

        // Same reference as the pursuit designer: median patrol height, then spawn height, then volume bottom.
        static float FloorOf(PursuitRoom room)
        {
            var heights = (room.PatrolPoints ?? Array.Empty<Transform>()).Where(t => t != null).Select(t => t.position.y).OrderBy(y => y).ToArray();
            if (heights.Length == 0) heights = (room.SpawnPoints ?? Array.Empty<Transform>()).Where(t => t != null).Select(t => t.position.y).OrderBy(y => y).ToArray();
            return (float)Math.Round(heights.Length > 0 ? heights[heights.Length / 2] : room.Area.bounds.min.y, 1);
        }

        public void Tick()
        {
            if (log.CurrentRunId == null || Scene is not ("WAITINGROOM" or "MAINSTAGE" or "EXITROUTE")) return;
            if (scope != Scope)
            {
                if (scope != null) log.ReleaseSpatialBoundary();
                scope = Scope; hasPosition = captured = dead = arrived = chased = escapeStarted = false;
                pursuitStates.Clear(); discoveries.Clear(); clueAvailability.Clear(); worldStates.Clear(); worldPositions.Clear(); geometry.Clear(); objects.Clear();
                BindCourse(null);
                geometryIndex = 0; nextCache = nextPlayer = nextWorld = 0; rehearsal = null; cannonRehearsal = null;
                bodySeen = false; lastEscapeSection = -1; visitedSections.Clear(); ownerStates.Clear(); previousSizes.Clear(); bounceCount=0;
            }
            if (Time.unscaledTime >= nextCache)
            {
                bool initial = nextCache == 0;
                nextCache = Time.unscaledTime + 5;
                Cache();
                // Disabled UV clues are absent from the normal active-object cache.
                if (initial && Scene == "WAITINGROOM")
                    foreach (var clue in UnityEngine.Object.FindObjectsByType<ClueInspectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        Availability(clue, clue.isActiveAndEnabled && clue.Revealed, "CACHE_OBSERVATION");
            }
            var player = NetworkPlayer.Local;
            if (Ready(player))
            {
                if (Scene is "MAINSTAGE" or "EXITROUTE") ObserveMechanisms(player);
                bool nowCaptured = PursuitDirector.IsCaptured(player);
                bool nowDead = player.State is PlayerState.Retired or PlayerState.Fallen;
                bool nowArrived = player.State == PlayerState.Arrived;
                bool nowChased = PursuitDirector.Current != null && PursuitDirector.Current.Actors.Any(a => a != null && a.ChasedPlayer == player.gameObject);
                if (nowChased != chased)
                    log.Emit("PURSUIT_PLAYER_STATE", new() { ["state"] = nowChased ? "CHASE_STARTED" : nowCaptured ? "CAPTURED" : "TARGET_RELEASED",
                        ["position"] = DetailLogIds.Position(player.transform.position) });
                chased = nowChased;
                if (nowCaptured && !captured) Point("PURSUIT_CAPTURED", player, "CONFIRMED_CAPTURE");
                if (nowDead && !dead)
                {
                    // PerformanceLogAdapter emits the detailed death. Pin the track endpoint without counting a second death.
                    if (Scene == "MAINSTAGE") Point("SPATIAL_SAMPLE", player, "DEATH_POSITION");
                    else Point("SPATIAL_DIED", player, nowCaptured ? "PURSUIT" : player.LastDeathOrigin.ToString());
                }
                if (Scene == "EXITROUTE" && nowArrived && !arrived && !Host)
                {
                    Point("SPATIAL_SAMPLE", player, "ARRIVAL_POSITION");
                    log.SubmitEscapeArrival();
                }
                // EscapeCampaignContent records actual entry before it closes the run.
                captured = nowCaptured; dead = nowDead; arrived = nowArrived;
                if (Scene == "EXITROUTE") ObserveEscapePlayer(player);
                if (!nowDead && !nowCaptured && !nowArrived && Time.unscaledTime >= nextPlayer)
                {
                    nextPlayer = Time.unscaledTime + (Scene == "MAINSTAGE" ? 5 : 1);
                    if (!hasPosition || Scene == "MAINSTAGE" || (player.transform.position - lastPosition).sqrMagnitude >= .25f)
                    {
                        if (Point("SPATIAL_SAMPLE", player, hasPosition ? "MOVE" : "FIRST_OBSERVATION"))
                        { lastPosition = player.transform.position; hasPosition = true; }
                    }
                }
            }
            if(Scene=="EXITROUTE") ObserveEscape();
            ObserveRehearsal();
            if (!Host) return;
            ObservePursuit();
            // Spread map serialization over frames instead of a scene-entry spike.
            for (int i = 0; i < 4 && geometryIndex < geometry.Count; i++)
            {
                var box = geometry[geometryIndex++];
                if (box != null) Geometry(box);
            }
            if (Time.unscaledTime >= nextWorld)
            {
                nextWorld = Time.unscaledTime + (Scene == "MAINSTAGE" ? 5 : 1);
                foreach (var item in objects) if (item != null) ObserveObject(item);
                if (Scene=="MAINSTAGE" && TightropeSawsForScene() is {} saws) {
                    for(int i=0;i<saws.HorizontalCount;i++)if(saws.TryGetHorizontalPosition(i,out var at)) SawSample("HORIZONTAL_"+i,at);
                    if(saws.TryGetVerticalPosition(out var pos))SawSample("VERTICAL_"+saws.VerticalSerial,pos);
                }
                if (PursuitDirector.Current != null)
                    foreach (var actor in PursuitDirector.Current.Actors) if (actor != null && actor.IsActivated) Actor(actor, "PURSUIT_SAMPLE");
            }
        }

        CurtainCall.Tightrope.Saws.TightropeSaws TightropeSawsForScene() => log.CurrentSceneName=="Tightrope"?CurtainCall.Tightrope.Saws.TightropeSaws.Current:null;
        void SawSample(string key,Vector3 at)=> log.Emit("WORLD_SAMPLE", new(){["entityId"]="SAW_"+key,["entityKind"]="OBSTACLE",["position"]=DetailLogIds.Position(at),["state"]="ACTIVE",["label"]=key,["floorY"]=0},"HOST");

        bool Point(string type, NetworkPlayer player, string reason) => log.EmitFor(player, type, new()
        {
            ["position"] = DetailLogIds.Position(player.transform.position), ["reason"] = reason,
            ["spatialScope"] = scope ?? Scope,
            ["entityKind"] = "PLAYER", ["obstacleId"]=type=="SPATIAL_DIED"?player.LastDeathObstacle:null, ["lostCount"]=DetailLogIds.Lost(player.LostParts), ["bodyResult"]=player.LostParts.ToString(), ["state"] = player.State.ToString(), ["collectionSource"] = "OWNER"
        });

        bool Ready(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.IsApplyingInitialCondition) return false;
            if (Scene == "WAITINGROOM") return WaitingRoomSpawn.LastPlacedPlayer == player &&
                WaitingRoomSpawn.LastPlacedScene == SceneManager.GetActiveScene().handle;
            if (Scene == "MAINSTAGE") return log.CurrentAttemptId != null && (performanceEntry == null || performanceEntry.LocalPlaced);
            return Scene == "EXITROUTE" && EscapeCampaignContent.IsTraversalActive
                && (EscapeSceneTransfer.Current == null || EscapeSceneTransfer.Current.Ready);
        }

        void Cache()
        {
            var scene = SceneManager.GetActiveScene();
            performanceEntry = UnityEngine.Object.FindAnyObjectByType<CampaignPerformanceEntry>();
            var nextHazards = HumanCannonHazardCourse.Current;
            if (hazards != nextHazards) {
                if (hazards != null) hazards.ContactResolved -= EscapeContact;
                hazards = nextHazards;
                if (hazards != null) hazards.ContactResolved += EscapeContact;
            }
            conveyors=UnityEngine.Object.FindObjectsByType<HumanCannonPoleLoop>(FindObjectsSortMode.None);
            bridges=UnityEngine.Object.FindObjectsByType<EscapeCooperativeSpan>(FindObjectsSortMode.None);
            rooms = UnityEngine.Object.FindObjectsByType<PursuitRoom>(FindObjectsSortMode.None).Where(r => r.gameObject.scene == scene).ToArray();
            if (Host && scene.name=="Tightrope" && log.CurrentAttemptId != null && observedCourse == null)
                BindCourse(TightropeCourse.Current);
            objects.Clear();
            foreach (var item in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (item.gameObject.scene != scene) continue;
                if (item is ClueInspectable clue) Availability(clue, clue.isActiveAndEnabled && clue.Revealed, "CACHE_OBSERVATION");
                if (item is IExplorationTarget || item is PerformancePropDiscovery || WorldKind(item) != null) objects.Add(item);
            }
            // Static stages already ship a measured map. Do not enumerate every collider or retransmit its outlines.
            if (!Host || geometry.Count > 0 || scene.name=="Tightrope" || mapEntries.Any(m=>m.scene==scene.name)) return;
            foreach (var room in rooms) if (room != null && room.Area != null) geometry.Add(room.Area);
            foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.gameObject.scene != scene || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != null) continue;
                if (collider.GetComponentInParent<NetworkPlayer>() != null || collider.GetComponentInParent<PursuitActor>() != null) continue;
                string name = collider.name.ToLowerInvariant();
                if (name.Contains("wall") || name.Contains("column") || name.Contains("floor") || name.Contains("deck") || name.Contains("platform") || name.Contains("stair")) geometry.Add(collider);
            }
        }

        void BindCourse(TightropeCourse next)
        {
            if (observedCourse != null) {
                observedCourse.Rebuilt -= CourseGeometry;
                observedCourse.SegmentsChanged -= CourseGeometry;
                observedCourse.LaneBlockedChanged -= LaneChanged;
            }
            observedCourse = next;
            recordedSpans.Clear();
            relayConnections.Clear();
            if (next == null) return;
            next.Rebuilt += CourseGeometry;
            next.SegmentsChanged += CourseGeometry;
            next.LaneBlockedChanged += LaneChanged;
            CourseGeometry();
        }

        void LaneChanged(int lane, bool blocked) => CourseGeometry();

        void CourseGeometry()
        {
            var course = observedCourse;
            if (course == null || !Host || scope != Scope || log.CurrentAttemptId == null) return;
            var relay=course.GetComponent<TightropeRelayGate>();
            for (int lane = 0; lane < course.LaneCount; lane++) {
                var spans = course.GetUsableSpans(lane);
                bool connected=relay!=null && relay.IsLaneConnected(lane);
                bool known=recordedSpans.TryGetValue(lane,out var prior);
                if (known && prior.SequenceEqual(spans) && relayConnections[lane]==connected) continue;
                string state=!known?"INITIAL":relayConnections[lane]==connected?"SUPPORT_CHANGED":connected?"CONNECTED":"DISCONNECTED";
                var a=course.GetLanePosition(lane,0); var b=course.GetLanePosition(lane,course.Layout.GetLaneEnd(lane));
                if (log.Emit("SPATIAL_GEOMETRY", new() { ["entityId"]="ROPE_"+lane, ["entityKind"]="STRUCTURE", ["label"]="외줄 "+(lane+1),
                    ["position"]=DetailLogIds.Position((a+b)*.5f), ["size"]=DetailLogIds.Position(new Vector3(Mathf.Abs(a.x-b.x)+.2f,.2f,Mathf.Abs(a.z-b.z)+.2f)),
                    ["lane"]=lane, ["state"]=state, ["connected"]=connected, ["ropeSpans"]=spans.Select(s=>new[]{DetailLogIds.Position(course.GetLanePosition(lane,s.x)),DetailLogIds.Position(course.GetLanePosition(lane,s.y))}).ToArray(), ["floorY"]=0 }, "HOST")) {
                    recordedSpans[lane] = spans; relayConnections[lane] = connected;
                }
            }
        }

        static string WorldKind(MonoBehaviour item)
        {
            if (item is EscapeExitSaw) return "OBJECT";
            string name = item.GetType().Name;
            if (item is HumanCannonHazardCourse || item is CurtainCall.Tightrope.Saws.TightropeSaws) return null;
            if (name.Contains("Saw") || name.Contains("Fire") || name.Contains("Hazard") || name.Contains("Mace") || name.Contains("Blade")) return "OBSTACLE";
            if (item is IExplorationTarget || item is PerformancePropDiscovery || name is "HumanCannonStation" or "HumanCannonPole" or "HumanCannonTrampoline" or "TightropeSwingPole" or "TightropeTrapeze" or "EscapeCuttableCable" or "EscapeCooperativeLocks" or "EscapeCooperativeRopes") return "OBJECT";
            return null;
        }

        void Geometry(Collider collider)
        {
            var b = collider.bounds;
            var room = rooms.FirstOrDefault(r => r != null && r.Area == collider);
            var payload = new Dictionary<string, object> {
                ["entityId"] = DetailLogIds.ObjectId(collider, "MAP_"), ["entityKind"] = room != null ? "ROOM" : "STRUCTURE", ["label"] = room != null ? room.name : collider.name,
                ["position"] = DetailLogIds.Position(b.center), ["size"] = DetailLogIds.Position(b.size), ["floorY"] = room != null ? FloorOf(room) : Floor(b.center),
                ["minY"] = b.min.y, ["maxY"] = b.max.y
            };
            if (collider is BoxCollider box)
            {
                var c = box.center; var e = box.size * .5f;
                payload["corners"] = new[] { DetailLogIds.Position(box.transform.TransformPoint(c + new Vector3(-e.x, 0, -e.z))),
                    DetailLogIds.Position(box.transform.TransformPoint(c + new Vector3(-e.x, 0, e.z))),
                    DetailLogIds.Position(box.transform.TransformPoint(c + new Vector3(e.x, 0, e.z))),
                    DetailLogIds.Position(box.transform.TransformPoint(c + new Vector3(e.x, 0, -e.z))) };
            }
            log.Emit("SPATIAL_GEOMETRY", payload, "HOST");
        }

        void ObservePursuit()
        {
            var director = PursuitDirector.Current;
            if (director == null) return;
            foreach (var actor in director.Actors)
            {
                if (actor == null || !actor.IsActivated) continue;
                string state = actor.Behaviour + "/" + actor.State + "/" + (actor.ChasedPlayer != null ? actor.ChasedPlayer.GetComponent<NetworkPlayer>()?.OwnerClientId.ToString() : "none");
                if (!pursuitStates.TryGetValue(actor, out var previous) || previous != state)
                    if (Actor(actor, "PURSUIT_STATE")) pursuitStates[actor] = state;
            }
        }

        bool Actor(PursuitActor actor, string type)
        {
            var target = actor.ChasedPlayer != null ? actor.ChasedPlayer.GetComponent<NetworkPlayer>() : null;
            return log.Emit(type, new() { ["entityId"] = DetailLogIds.ObjectId(actor, "PU_"), ["entityKind"] = "PURSUER",
                ["position"] = DetailLogIds.Position(actor.transform.position), ["floorY"] = Floor(actor.transform.position),
                ["state"] = actor.State.ToString(), ["behaviour"] = actor.Behaviour.ToString(),
                ["targetClientId"] = target != null ? target.OwnerClientId.ToString() : null, ["label"] = actor.name }, "HOST");
        }

        void ObserveObject(MonoBehaviour item)
        {
            string id = DetailLogIds.ObjectId(item, WorldKind(item)=="OBSTACLE"?"OB_":"IN_");
            if (item is IExplorationTarget target && ExplorationState.Instance != null)
            {
                bool open = ExplorationState.Instance.IsOpen(target.TargetId);
                if (discoveries.TryGetValue(id, out bool before) && !before && open)
                    log.Emit("OBJECT_DISCOVERED", new() { ["entityId"] = id, ["targetId"] = target.TargetId, ["position"] = DetailLogIds.Position(item.transform.position), ["entityKind"] = "OBJECT" }, "HOST");
                discoveries[id] = open;
            }
            string kind = WorldKind(item);
            if (kind == null) return;
            var at = item.transform.position;
            if(item is HumanCannonHazard h && hazards!=null){
                if(!hazards.IsSynchronized)return;
                int index=0;while(index<hazards.HazardCount && hazards.Hazard(index)!=h)index++;
                if(index==hazards.HazardCount||!hazards.IsHazardEnabled(index))return;
                at=h.Position(hazards.Elapsed,h.IsPoleSaw?1f:CannonDifficulty.HazardSpeed(hazards.Difficulty));
            }
            string state = !item.isActiveAndEnabled ? "INACTIVE" : item is EscapePursuitFire fire ? (fire.IsArmed ? "ARMED" : "IDLE")
                : item is EscapeCuttableCable cable ? (cable.IsCut ? "CUT" : "INTACT")
                : item is EscapeExitSaw saw ? (saw.DoorOpen ? "DOOR_OPEN" : "CLOSED") + "/" + saw.Carrier : "ACTIVE";
            if (worldStates.TryGetValue(id, out var last) && state != last)
                log.Emit("WORLD_STATE_CHANGED", new() { ["entityId"] = id, ["entityKind"] = kind, ["state"] = state, ["previousState"] = last, ["floorY"]=Floor(at), ["position"] = DetailLogIds.Position(at) }, "HOST");
            bool changed = !worldStates.TryGetValue(id, out var previous) || previous != state;
            worldStates[id] = state;
            if (Scene != "MAINSTAGE" && !changed && worldPositions.TryGetValue(id, out var position) && (position - at).sqrMagnitude < .0025f) return;
            worldPositions[id] = at;
            log.Emit("WORLD_SAMPLE", new() { ["entityId"] = id, ["entityKind"] = kind, ["position"] = DetailLogIds.Position(at),
                ["floorY"] = Floor(at), ["state"] = state, ["label"] = item.GetType().Name }, "HOST");
        }

        void ObserveRehearsal()
        {
            if (Scene != "WAITINGROOM") return;
            var value = (active: WaitingRoomRehearsal.IsActive, stage: (int)WaitingRoomRehearsal.CurrentStage,
                completed: WaitingRoomRehearsal.IsCompleted, practice: false);
            if (value != rehearsal && (value.active || rehearsal != null))
                log.Emit("REHEARSAL_STATE", new() { ["recordKind"] = "SNAPSHOT", ["state"] = $"{value.active}/{value.stage}/{value.completed}/{value.practice}",
                    ["stage"] = value.stage, ["active"] = value.active, ["completed"] = value.completed, ["practice"] = value.practice, ["trickId"] = "TIGHTROPE" });
            rehearsal = value;
            var player = NetworkPlayer.Local;
            if (player == null || !player.IsSpawned) return;
            var cannon = (active: WaitingRoomCannonRehearsal.IsActive, ending: WaitingRoomCannonRehearsal.IsEnding,
                mastered: WaitingRoomCannonRehearsal.HasMastered, checkpoint: WaitingRoomCannonRehearsal.Checkpoint(player.OwnerClientId),
                arrived: WaitingRoomCannonRehearsal.HasArrived(player.OwnerClientId));
            if (cannon != cannonRehearsal && (cannon.active || cannonRehearsal?.active == true))
                log.EmitFor(player, "REHEARSAL_STATE", new() { ["recordKind"] = "SNAPSHOT", ["trickId"] = "CANNON",
                    ["state"] = !cannon.active ? "ENDED" : cannon.ending ? "ENDING" : cannon.arrived ? "ARRIVED" : cannon.checkpoint > 0 ? "CHECKPOINT" : "STARTED",
                    ["stage"] = cannon.checkpoint, ["active"] = cannon.active, ["completed"] = cannon.mastered, ["practice"] = true, ["arrived"] = cannon.arrived });
            cannonRehearsal = cannon;
        }

        void ObserveEscape()
        {
            var timer = EscapeRouteTimer.Current;
            if (Scene != "EXITROUTE" || timer == null) return;
            if (!escapeStarted && timer.HasStarted && Ready(NetworkPlayer.Local)) escapeStarted = log.Emit("ESCAPE_STARTED", new() { ["durationMs"] = 0, ["timeLimitEnabled"] = false, ["startedAtServerMs"] = timer.StartedAt * 1000 });
        }

        void Departed(ulong client)
        {
            if(!Host || log.CurrentRunId==null || Scene is not ("MAINSTAGE" or "EXITROUTE")) return;
            var p=NetworkPlayer.All.FirstOrDefault(x=>x!=null && x.OwnerClientId==client);
            var data=new Dictionary<string,object>{["subjectClientId"]=client.ToString(),["subjectId"]=log.PlayerCode(client),["reason"]="DISCONNECTED"};
            if(p==null){data["position"]=null;data["positionSource"]="DESPAWNED";}
            log.EmitFor(p,"PARTICIPANT_LEFT",data,"HOST");
        }
        void Rescue(ulong actor,ulong target)=>Pair(actor,target,"RESCUE");

        void Pair(ulong actor,ulong target,string kind) {
            var p=NetworkPlayer.All.FirstOrDefault(x=>x!=null&&x.OwnerClientId==actor);
            if(Host&&p!=null)TelemetryRuntime.Mechanism(p,p,kind,"SUCCEEDED:"+target,p.transform.position,true);
        }
        bool CollectingWaitingRoom => log.CurrentRunId != null && Scene == "WAITINGROOM";

        void RewardDrop(string target, string item, int uid, Vector3 at)
        {
            if (!CollectingWaitingRoom) return;
            log.Emit("WORLD_STATE_CHANGED", new() { ["entityId"] = "DROP_" + uid, ["entityKind"] = "OBJECT",
                ["state"] = "REWARD_DROPPED", ["targetId"] = target, ["label"] = item,
                ["position"] = DetailLogIds.Position(at), ["floorY"] = Floor(at) }, "HOST");
        }

        static string TargetCode(IExplorationTarget target, Component item) =>
            target == null ? "UNKNOWN_TARGET" : target.TargetId is { Length: > 0 and <= 128 } id ? id : DetailLogIds.ObjectId(item, "IN_");

        void Focused(ClueInspectable clue) => Inspected(clue, "FOCUSED", "SHOP_REACH", null);

        void Inspected(ClueInspectable item, string state, string reason, double? durationMs) =>
            EmitClue(item, state, reason, durationMs);

        bool EmitClue(ClueInspectable item, string state, string reason, double? durationMs)
        {
            if (!CollectingWaitingRoom || item == null || item.gameObject.scene.name != log.CurrentSceneName) return false;
            var at = item.transform.position;
            var payload = new Dictionary<string, object> {
                ["entityId"] = DetailLogIds.ObjectId(item, "IN_"), ["targetId"] = TargetCode(item, item),
                ["state"] = state, ["reason"] = reason, ["hasSecret"] = item.HasSecret, ["secretSeen"] = item.SecretSeen,
                ["objectFloorY"] = Floor(at), ["objectPosition"] = DetailLogIds.Position(at)
            };
            if (durationMs.HasValue) payload["durationMs"] = durationMs.Value;
            return log.Emit("CLUE_INSPECTED", payload, "LOCAL");
        }

        // Availability is local: UV content can be active on one screen only.
        void Availability(ClueInspectable item, bool available, string reason)
        {
            if (!CollectingWaitingRoom || item == null || item.gameObject.scene.name != log.CurrentSceneName || scope != Scope) return;
            var key = (item.GetInstanceID(), item.TargetId);
            if (clueAvailability.TryGetValue(key, out bool previous) && available == previous) return;
            if (EmitClue(item, available ? "AVAILABLE" : "UNAVAILABLE", reason, null))
                clueAvailability[key] = available;
        }

        void Explored(ulong client, IExplorationTarget target, bool accepted, bool opened, int hits, string reason)
        {
            var player = NetworkPlayer.All.FirstOrDefault(p => p != null && p.OwnerClientId == client);
            if (player == null || !CollectingWaitingRoom) return;
            var item = target as Component;
            log.EmitFor(player, "EXPLORATION_INTERACTION", new() {
                ["objectPosition"] = item != null ? DetailLogIds.Position(item.transform.position) : null,
                ["objectFloorY"] = item != null ? Floor(item.transform.position) : null, ["targetId"] = TargetCode(target, item),
                ["label"] = item != null ? item.GetType().Name : null, ["accepted"] = accepted, ["opened"] = opened, ["hits"] = hits, ["reason"] = reason
            }, "HOST");
        }

        internal static void RehearsalAction(string trick, string state, int round, int stage, bool active,
            bool completed, bool practice, ulong? actor, bool host, string reason = null, bool? vote = null, string guideId = null)
        {
            var runtime = TelemetryRuntime.Instance;
            if (runtime == null || runtime.CurrentRunId == null || TelemetryRuntime.SceneType(runtime.CurrentSceneName) != "WAITINGROOM") return;
            var player = actor.HasValue ? NetworkPlayer.All.FirstOrDefault(p => p != null && p.IsSpawned && p.OwnerClientId == actor.Value) : null;
            if (actor.HasValue && player == null || !host && (player == null || !player.IsLocal)) return;
            var data = new Dictionary<string, object> {
                ["recordKind"] = "ACTION", ["subjectKind"] = actor.HasValue ? "PLAYER" : "TEAM",
                ["trickId"] = trick, ["state"] = state, ["round"] = round, ["stage"] = stage,
                ["active"] = active, ["completed"] = completed, ["practice"] = practice
            };
            if (player == null) data["position"] = null;
            else data["position"] = DetailLogIds.Position(player.transform.position);
            if (reason != null) data["reason"] = reason;
            if (vote.HasValue) data["vote"] = vote.Value;
            if (guideId != null) data["guideId"] = guideId;
            runtime.EmitFor(player, "REHEARSAL_STATE", data, host ? "HOST" : "LOCAL");
        }

        void Audience(string participant, int delta)
        {
            var shared = PerformanceRewards.Instance?.State;
            if (shared == null) return;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null) continue;
                // Reward identity is resolved by its existing participant roster, never guessed from a player slot.
                var person = shared.Segments.FirstOrDefault(p => p.ParticipantId == participant);
                if (person == null || person.ClientId != player.OwnerClientId) continue;
                log.EmitFor(player, "AUDIENCE_REACTION", new() { ["reaction"] = delta > 0 ? "CHEER" : delta < 0 ? "JEER" : "INTEREST_RESET", ["delta"] = delta, ["personalAudience"] = person.Amount, ["teamAudience"] = shared.Segments.Sum(p => p.Amount) }, "HOST");
                break;
            }
        }

        void Schedule(int day, int slot, string trick, int tier) => log.Emit("SCHEDULE_CHANGED", new() { ["day"] = day, ["slot"] = slot, ["trickId"] = trick, ["tier"] = tier }, "HOST");
        void Shot(EscapeExitSaw device, ulong client, string shot, string state, Vector3 position, Vector3 direction, string result)
        {
            if (!Host || Scene != "EXITROUTE" || state == "ENDED") return;
            var player = NetworkPlayer.All.FirstOrDefault(p => p != null && p.OwnerClientId == client);
            var id = DetailLogIds.ObjectId(device, "IN_");
            log.EmitFor(player, "ESCAPE_CUTTER", new() { ["deviceId"] = id, ["shotId"] = id + ":" + shot, ["state"] = state,
                ["position"] = DetailLogIds.Position(position), ["entityKind"] = "OBJECT", ["entityId"] = id + ":" + shot, ["cutCableCount"] = device.CutCableCount, ["objectPosition"] = DetailLogIds.Position(position), ["direction"] = DetailLogIds.Position(direction), ["result"] = result,
                ["remainingCables"] = Math.Max(0, device.RequiredCableCount - device.CutCableCount) }, "HOST");
        }
        void ObserveMechanisms(NetworkPlayer p)
        {
            ulong client = p.OwnerClientId;
            void Track(Component source, string kind, string value, int size = 0)
            {
                if (source == null) return;
                string key = kind + ":" + source.GetInstanceID();
                bool sizeChanged = size > 0 && previousSizes.TryGetValue(key, out var priorSize) && priorSize != size;
                if (size > 0) previousSizes[key] = size;
                if (ownerStates.TryGetValue(key, out var previous) && previous == value)
                {
                    if (sizeChanged && value != null) TelemetryRuntime.Mechanism(p, source, kind, "SIZE_CHANGED:" + value, p.transform.position, stackSize:size);
                    return;
                }
                ownerStates[key] = value;
                if (previous == null && value == null) return;
                if (previous != null) TelemetryRuntime.Mechanism(p, source, kind, "RELEASED:"+previous, p.transform.position, stackSize:size);
                if (value != null) TelemetryRuntime.Mechanism(p, source, kind, value, p.transform.position, stackSize:size);
            }
            var pig = PiggybackSystem.Current;
            if (pig != null) {
                var lower=pig.GetBelow(p);var upper=pig.GetAbove(p);
                Track(pig,"PIGGYBACK",lower != null ? "RIDING:"+lower.OwnerClientId : null,pig.GetStackSize(p));
                Track(pig,"SUPPORT",upper != null ? "SUPPORTING:"+upper.OwnerClientId : null,pig.GetStackSize(p));
            }
            var hang=TightropeHangSystem.Current;
            if(hang!=null){ Track(hang,"HANG",hang.IsHanging(p)?"HANGING":null); Track(hang,"RESCUE",hang.IsRescuing(p)?"PULLING":null); }
            var swing=TightropeSwingPole.Current;
            if(swing!=null)Track(swing,"SWING",swing.IsHeld(p)?"HOLDING":null);
            var trap=TightropeTrapeze.Current;
            if(trap!=null)Track(trap,"TRAPEZE",trap.IsHeld(p)?(trap.IsLower(p)?"HOLDING_FEET:":"HOLDING_BAR:")+trap.HeldPole(p):null);
            var trampoline=HumanCannonTrampoline.Current;
            if(trampoline!=null){
                Track(trampoline,"TRAMPOLINE_ROLE",trampoline.Carrier==CannonSeatState.Nobody?null:trampoline.Carrier==client?"CARRIER":"FLYER");
                int bounce=trampoline.LocalBounceCount;
                if(bounce>bounceCount)
                    TelemetryRuntime.Mechanism(p,trampoline,"TRAMPOLINE","BOUNCED",p.transform.position);
                bounceCount=bounce;
            }
            HumanCannonPoleLoop activeConveyor = null;
            var poles=HumanCannonPoleSystem.Current;
            if(poles!=null)for(int i=0;i<poles.PoleCount;i++){
                bool holding=false;for(int s=0;s<PoleGateState.MaxRiders;s++)holding|=poles.Occupant(i,s)==client;
                var pole=poles.Pole(i);
                Track(pole,"POLE",holding?"HOLDING":null);
                if(holding && pole!=null && Scene=="EXITROUTE") foreach(var loop in conveyors) if(loop!=null && loop.Contains(pole.transform)) { activeConveyor=loop; break; }
            }
            foreach(var loop in conveyors) if(loop!=null) Track(loop,"CONVEYOR",loop==activeConveyor?"RIDING":null);
            foreach(var bridge in bridges)if(bridge!=null)Track(bridge,"DRAWBRIDGE",bridge.SupportsFeet(p.transform.position)?"CROSSING":null);
            var routes=HumanCannonRouteSystem.Current;
            if(routes!=null)for(int i=0;i<routes.TransportCount;i++){
                var st=routes.TransportState(i);
                Track(routes.Transport(i),i==1?"LIFT":"PLATFORM",st.Rider==client&&st.Phase==DeparturePhase.Moving?"RIDING":null);
            }
            var system=HumanCannonSystem.Current;
            if(system!=null)for(int i=0;i<system.StationCount;i++){
                var station=system.Station(i); if(station==null)continue;
                // The replicated Board action emits at the station seat.
                if(Host){
                    string key="cannonReady:"+i, value=station.ReadyToUse?"READY":"BUSY";
                    if(ownerStates.TryGetValue(key,out var old)&&old!=value&&value=="READY") TelemetryRuntime.Mechanism(null,station,"CANNON","READY",station.transform.position,true);
                    ownerStates[key]=value;
                }
            }
            var ropes=EscapeCooperativeRopes.Current;
            if(ropes!=null)Track(ropes,"ROPE_CONTROL",ropes.WorkerOwner==client?"HOLDING":null);
            if(Scene=="EXITROUTE") {
                string value=p.IsAirborne?"AIR":"GROUND";
                if(ownerStates.TryGetValue("feet",out var prev)&&prev=="AIR"&&value=="GROUND"&&p.State==PlayerState.Normal)
                    TelemetryRuntime.Mechanism(p,p,"LANDING","LANDED",p.transform.position);
                ownerStates["feet"]=value;
            }
        }

        void ObserveEscapePlayer(NetworkPlayer player)
        {
            if (bodySeen && player.LostParts != lastLost)
                log.Emit("ESCAPE_INJURY", new() { ["beforeParts"] = lastLost.ToString(), ["afterParts"] = player.LostParts.ToString(),
                    ["beforeLostCount"] = DetailLogIds.Lost(lastLost), ["afterLostCount"] = DetailLogIds.Lost(player.LostParts) });
            bodySeen = true; lastLost = player.LostParts;
            var position=player.transform.position;
            var region=mapEntries.Where(m=>m.scene==log.CurrentSceneName&&m.sectionId>0&&position.x>=m.minX&&position.x<=m.maxX&&position.z>=m.minZ&&position.z<=m.maxZ&&position.y>=m.minY&&position.y<=m.maxY)
                .OrderBy(m=>(m.maxX-m.minX)*(m.maxZ-m.minZ)).FirstOrDefault();
            if(region==null)return;
            int section = region.sectionId;
            if (section != lastEscapeSection) {
                if (!visitedSections.Contains(section) && log.Emit("ESCAPE_SECTION", new() { ["sectionId"] = section, ["sectionName"] = region.sectionName, ["previousSectionId"] = lastEscapeSection,
                    ["remainingMs"] = null, ["elapsedMs"] = EscapeRouteTimer.Current != null ? EscapeRouteTimer.Current.ElapsedSeconds * 1000 : null })) visitedSections.Add(section);
                lastEscapeSection = section;
            }
        }
        void EscapeContact(NetworkPlayer player, HumanCannonHazard hazard, BodyPart cut, Vector3 position, bool evaluated)
        {
            if (!Host || Scene != "EXITROUTE" || player == null || hazard == null) return;
            log.EmitFor(player, "ESCAPE_HAZARD_CONTACT", new() { ["position"] = DetailLogIds.Position(position),
                ["obstacleId"] = DetailLogIds.ObjectId(hazard, "OB_"), ["hazardType"] = hazard.Kind.ToString(), ["part"] = cut.ToString(),
                ["lostCount"] = DetailLogIds.Lost(player.LostParts), ["bodyResult"] = player.LostParts.ToString(), ["cutApplied"] = cut != BodyPart.None, ["evaluated"] = evaluated }, "HOST");
        }
        void Unlocked(string trick, ulong client)
        {
            var player = NetworkPlayer.All.FirstOrDefault(p => p != null && p.OwnerClientId == client);
            var prop = objects.OfType<PerformancePropDiscovery>().FirstOrDefault(p => p != null && p.TrickId == trick);
            var payload = new Dictionary<string, object> { ["trickId"] = trick, ["entityId"] = prop != null ? DetailLogIds.ObjectId(prop, "IN_") : "TRICK_" + trick };
            if(prop!=null) foreach(var go in prop.MapProps) if(go!=null)log.EmitFor(player,"OBJECT_DISCOVERED",new(){["entityKind"]="OBJECT",["entityId"]=DetailLogIds.ObjectId(go.transform,"IN_"),["targetId"]=go.name,["trickId"]=trick,["position"]=DetailLogIds.Position(go.transform.position),["floorY"]=Floor(go.transform.position)},"HOST");
            if (player != null) payload["position"] = DetailLogIds.Position(player.transform.position);
            else if (prop != null) payload["position"] = DetailLogIds.Position(prop.transform.position);
            else return;
            log.EmitFor(player, "TRICK_UNLOCKED", payload, "HOST");
        }

        public void Leaving(string reason)
        {
            BindCourse(null);
            var player = NetworkPlayer.Local;
            if (scope != null && player != null && player.IsSpawned) Point("SPATIAL_BOUNDARY", player, reason);
            scope = null;
        }
        public void Dispose()
        {
            BindCourse(null);
            ClueInspectable.InspectionObserved -= Inspected;
            ClueInspectable.AvailabilityObserved -= Availability;
            ShopReach.ClueFocused -= Focused;
            ExplorationState.TelemetryAttempted -= Explored;
            ExplorationState.RewardDropped -= RewardDrop;
            PerformanceRewards.AudienceReacted -= Audience;
            ScheduleBoard.SelectionChanged -= Schedule;
            CampaignRun.TrickDiscovered -= Unlocked;
            EscapeExitSaw.ShotObserved -= Shot;
            TightropeHangSystem.ServerRescueSucceeded -= Rescue;

            NetworkSessionManager.PeerDeparted -= Departed;
            if (hazards != null) hazards.ContactResolved -= EscapeContact;
        }
    }
}
