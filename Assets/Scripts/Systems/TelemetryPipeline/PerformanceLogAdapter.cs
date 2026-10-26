using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Results;
using CurtainCall.Reward;
using CurtainCall.SpaceLink;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Fire;
using CurtainCall.Tightrope.Results;
using CurtainCall.Tightrope.Saws;
using UnityEngine;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Reads confirmed public results and damage events; never decides a gameplay outcome.</summary>
    sealed class PerformanceLogAdapter : IDisposable
    {
        readonly TelemetryRuntime log;
        readonly Dictionary<NetworkPlayer, PlayerHooks> players = new();
        readonly HashSet<string> confirmed = new();
        readonly Dictionary<ulong, string> launches = new();
        readonly Dictionary<int, ulong> poleOccupants = new();
        readonly Dictionary<int, uint> transportRevisions = new();
        readonly Dictionary<ulong, ParticipantObservation> participants = new();
        string rosterSignature;
        readonly HashSet<NetworkPlayer> seenPlaced = new();
        string placementKey;
        PerformanceRewards rewards;
        TightropeResults results;
        TightropeSaws saws;
        TightropeFire fire;
        HumanCannonSystem cannons;
        HumanCannonCourseSections cannonSections;
        HumanCannonHazardCourse cannonHazards;
        CampaignPerformanceEntry entry;
        PiggybackSystem piggyback;
        string attempt, trick;
        string sharedPerformance;
        float began, nextProgress;
        int maximumSection, difficulty = 1;
        bool ended;
        readonly Dictionary<ulong, ulong> below = new();
        public PerformanceLogAdapter(TelemetryRuntime runtime)
        {
            log = runtime;
            HumanCannonTrampoline.ServerBounced += Bounced;
        }
        bool Host => NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.IsHost;
        public void Tick()
        {
            Bind();
            ObservePlacement();
            foreach (var hooks in players.Values.ToArray()) hooks.Flush();
            var shared = rewards?.State;
            var run = TightropeRun.Current;
            string next = null, nextTrick = null;
            if (shared?.IsOpen == true && shared.Performance != null && log.CurrentRunId != null)
            {
                nextTrick = shared.Performance.TrickId == "Tightrope" ? "TIGHTROPE" : shared.Performance.TrickId == "HumanCannon" ? "CANNON" : null;
                if (nextTrick == "TIGHTROPE" && results?.IsAttemptOpen == true && Guid.TryParseExact(results.AttemptId, "N", out var id)) next = id.ToString("D");
                if (nextTrick == "CANNON") next = DetailLogIds.Uuid("performance:" + shared.Performance.Id);
            }
            else if (run != null && !run.ExternallyManaged && run.State == TightropeRunState.Running && results?.IsAttemptOpen == true)
            {
                next = Guid.ParseExact(results.AttemptId, "N").ToString("D"); nextTrick = "TIGHTROPE";
                if (log.CurrentRunId == null) log.SetContext(log.CurrentRoomId, TelemetryRuntime.SharedRunId(results.SessionId, 1), null);
            }
            if (next != null && nextTrick != null && next != attempt)
            {
                if (attempt != null && !ended) Leave("ATTEMPT_REPLACED");
                attempt = next; trick = nextTrick; ended = false; began = Time.unscaledTime;
                sharedPerformance = shared?.IsOpen == true ? shared.Performance?.Id : null;
                maximumSection = 0; participants.Clear(); rosterSignature = null; launches.Clear(); below.Clear(); poleOccupants.Clear(); transportRevisions.Clear();
                difficulty = nextTrick == "CANNON" && HumanCannonHazardCourse.Current != null ? HumanCannonHazardCourse.Current.Difficulty : 1;
                log.SetAttempt(attempt);
                foreach (var hooks in players.Values) hooks.BeginAttempt();
            }
            if (attempt == null || ended) return;
            log.SetAttempt(attempt);
            if (Host) { ObserveRoster(shared); ObservePlayers(); }
            if (Host && trick == "CANNON" && cannons != null)
            {
                foreach (ulong actor in launches.Keys.ToArray())
                    if (!cannons.IsFlying(actor)) EndFlight(actor, Find(actor)?.State == PlayerState.Fallen ? "FALL" : "FLIGHT_STOPPED");
                CannonCooperation();
            }
            if (Time.unscaledTime >= nextProgress) { nextProgress = Time.unscaledTime + 5; Progress(); }
        }
        void Bind()
        {
            var current = PerformanceRewards.Instance;
            if (rewards != current) { if (rewards != null) rewards.ResultConfirmed -= RewardResult; rewards = current; if (rewards != null) rewards.ResultConfirmed += RewardResult; }
            var r = TightropeRun.Current != null ? TightropeRun.Current.Results : null;
            if (results != r) { if (results != null) results.ResultConfirmed -= TightropeResult; results = r; if (results != null) results.ResultConfirmed += TightropeResult; }
            if (saws != TightropeSaws.Current) { if (saws != null) saws.Hit -= Saw; saws = TightropeSaws.Current; if (saws != null) saws.Hit += Saw; }
            if (fire != TightropeFire.Current) { if (fire != null) fire.Hit -= Fire; fire = TightropeFire.Current; if (fire != null) fire.Hit += Fire; }
            if (cannons != HumanCannonSystem.Current)
            {
                if (cannons != null) { cannons.StateChanged -= Cannon; cannons.LandingConfirmed -= Landed; }
                cannons = HumanCannonSystem.Current;
                if (cannons != null) { cannons.StateChanged += Cannon; cannons.LandingConfirmed += Landed; }
            }
            if (cannonSections == null && cannons != null) cannonSections = UnityEngine.Object.FindAnyObjectByType<HumanCannonCourseSections>();
            if (entry == null) entry = UnityEngine.Object.FindAnyObjectByType<CampaignPerformanceEntry>();
            if (cannonHazards != HumanCannonHazardCourse.Current)
            {
                if (cannonHazards != null) { cannonHazards.ContactDetected -= CannonContact; cannonHazards.ContactResolved -= CannonContactResolved; }
                cannonHazards = HumanCannonHazardCourse.Current;
                if (cannonHazards != null) { cannonHazards.ContactDetected += CannonContact; cannonHazards.ContactResolved += CannonContactResolved; }
            }
            if (piggyback != PiggybackSystem.Current)
            {
                if (piggyback != null) { piggyback.LinksChanged -= Links; piggyback.HandholdConfirmed -= Handhold; }
                piggyback = PiggybackSystem.Current;
                if (piggyback != null) { piggyback.LinksChanged += Links; piggyback.HandholdConfirmed += Handhold; }
            }
            foreach (var player in NetworkPlayer.All)
                if (player != null && !players.ContainsKey(player)) players.Add(player, new PlayerHooks(this, player));
            foreach (var player in players.Keys.ToArray())
                if (player == null || !NetworkPlayer.All.Contains(player)) { players[player].Dispose(); players.Remove(player); }
        }
        void CannonContact(NetworkPlayer player, HumanCannonHazard hazard, Vector3 position)
        {
            if (player != null && hazard != null && players.TryGetValue(player, out var hooks))
                hooks.Hit("CANNON_" + hazard.Kind.ToString().ToUpperInvariant(), DetailLogIds.ObjectId(hazard, "OB_"), position);
        }
        void CannonContactResolved(NetworkPlayer player, HumanCannonHazard hazard, BodyPart cut, Vector3 position, bool evaluated)
        {
            // Settlement may already have closed a fatal attempt in ServerCut. Its context is still valid.
            if (!Host || trick != "CANNON" || attempt == null || player == null || hazard == null) return;
            int at = cannonSections != null ? (int)cannonSections.SectionAt(position) + 1 : 0;
            log.EmitFor(player, "CANNON_HAZARD_CONTACT", new() { ["sectionId"] = at > 0 ? at : null,
                ["position"] = DetailLogIds.Position(position), ["obstacleId"] = DetailLogIds.ObjectId(hazard, "OB_"),
                ["hazardType"] = hazard.Kind.ToString(), ["cutApplied"] = cut != BodyPart.None, ["part"] = cut.ToString(),
                ["lostCount"] = DetailLogIds.Lost(player.LostParts),
                ["reason"] = !evaluated ? "FRAME_LIMIT" : cut == BodyPart.None ? "NO_CUT" : "CUT" }, "HOST");
        }
        int Section(NetworkPlayer player)
        {
            if (player != null && trick == "CANNON" && cannonSections != null) return (int)cannonSections.SectionAt(player.transform.position) + 1;
            var course = TightropeCourse.Current;
            if (trick != "TIGHTROPE" || course == null || player == null) return 0;
            int index = course.GetSectionIndex(course.GetDistance(player.transform.position));
            return index < 0 ? 0 : index + 1;
        }
        void ObserveRoster(RewardSnapshot shared)
        {
            // The official roster survives missing/late character objects and client log relays.
            if (shared != null && sharedPerformance != null && shared.Performance?.Id == sharedPerformance)
                foreach (var person in shared.Segments)
                    AddParticipant(person.ClientId, person.Slot, person.StartingLostCount);
            else if (results != null)
                foreach (var person in results.Participants)
                    AddParticipant(person.ClientId, person.Slot, DetailLogIds.Lost(person.StartingLostParts));
            if (participants.Count == 0) return;
            foreach (var person in participants.Values)
            {
                var player = Find(person.Client);
                if (player != null && player.Slot is >= 0 and <= 3) person.Slot = player.Slot;
            }
            var people = participants.Values.OrderBy(p => p.Client).Select(p => new {
                playerId = log.PlayerCode(p.Client), networkClientId = p.Client.ToString(), playerNumber = p.Slot }).ToArray();
            string signature = LogJson.Serialize(people);
            if (signature != rosterSignature && log.Emit("PERFORMANCE_ROSTER", new() {
                ["trickType"] = trick, ["difficulty"] = difficulty, ["playerCount"] = people.Length,
                ["participants"] = people, ["collectionSource"] = "HOST_ROSTER" }, "HOST"))
            { rosterSignature = signature; log.Flush(); }
        }
        void AddParticipant(ulong client, int slot, int lost)
        {
            if (!participants.ContainsKey(client)) participants.Add(client, new ParticipantObservation {
                Client = client, Slot = slot is >= 0 and <= 3 ? slot : null, StartingLost = lost });
        }
        bool Ready(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.Slot is < 0 or > 3 || player.IsApplyingInitialCondition
                || TelemetryRuntime.SceneType(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) != "MAINSTAGE") return false;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (trick == "TIGHTROPE" ? scene != "Tightrope" : scene is not ("HummanCannon" or "HumanCannon")) return false;
            var run = TightropeRun.Current;
            if (trick == "TIGHTROPE" && run != null && !run.ExternallyManaged) return run.IsParticipantReady(player);
            if (entry != null && player.IsLocal) return entry.IsCurrentPerformance && entry.LocalPlaced;
            return seenPlaced.Contains(player);
        }
        void ObservePlacement()
        {
            if (!Host) return;
            var state = CampaignRun.Instance != null ? CampaignRun.Instance.State : default;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string key = log.CurrentRunId + ":" + state.Generation + ":" + state.Day + ":" + state.PerformanceIndex + ":" + scene;
            if (key != placementKey) { placementKey = key; seenPlaced.Clear(); }
            if (TelemetryRuntime.SceneType(scene) != "MAINSTAGE") return;
            // Managed performances can open before the remote owner's placement arrives.
            // Observe the actual spawn pose, never treat an old waiting-room position as the start.
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.Slot is < 0 or > 3 || player.State != PlayerState.Normal
                    || !player.TryGetComponent(out PlayerMover mover)) continue;
                PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out var position, out _);
                if (Vector3.Distance(player.transform.position, position) <= 1.5f) seenPlaced.Add(player);
            }
        }
        bool StartPlayer(NetworkPlayer player, bool gameplayObserved = false)
        {
            if (!Host || player == null || !participants.TryGetValue(player.OwnerClientId, out var person)) return false;
            if (person.Started) return true;
            bool ready = Ready(player);
            if (!ready && !gameplayObserved || player.Slot is < 0 or > 3) return false;
            var run = TightropeRun.Current;
            var payload = new Dictionary<string, object> { ["trickType"] = trick, ["difficulty"] = difficulty,
                ["retryIndex"] = trick == "TIGHTROPE" ? Math.Max(1, run != null ? run.Round : 1) : 1,
                ["playerCount"] = participants.Count, ["startingLostCount"] = person.StartingLost,
                ["collectionSource"] = "HOST_PLAYER", ["startObservation"] = ready ? "READY" : "FIRST_GAMEPLAY_EVENT" };
            // A late first observation proves participation, not the original spawn coordinates/time.
            if (!ready) { payload["position"] = null; payload["positionSource"] = "START_NOT_OBSERVED"; }
            person.Started = log.EmitFor(player, "PERFORMANCE_STARTED", payload, "HOST");
            if (person.Started) log.Flush();
            return person.Started;
        }
        void ObservePlayers()
        {
            foreach (var person in participants.Values)
            {
                var player = Find(person.Client);
                if (!StartPlayer(player)) continue;
                if (player.State == PlayerState.Arrived) { Arrived(player); continue; }
                if (player.State != PlayerState.Normal) continue;
                int current = Section(player);
                if (current > 0 && current != person.Section)
                {
                    if (!CloseSection(player, current > person.Section ? "PASSED" : "RETREATED")) continue;
                    if (log.EmitFor(player, "SECTION_ENTERED", new() { ["sectionId"] = current,
                        ["lostCount"] = DetailLogIds.Lost(player.LostParts), ["collectionSource"] = "HOST_PLAYER" }, "HOST"))
                    { person.Section = current; person.SectionAt = Time.unscaledTime; }
                }
                maximumSection = Math.Max(maximumSection, current);
                var course = TightropeCourse.Current;
                if (trick == "TIGHTROPE" && course != null)
                    person.MaxDistance = Math.Max(person.MaxDistance, Math.Clamp(course.GetDistance(player.transform.position), 0, course.FinishDistance));
            }
        }
        bool CloseSection(NetworkPlayer player, string reason)
        {
            if (player == null || !participants.TryGetValue(player.OwnerClientId, out var person) || person.Section == 0) return true;
            if (!log.EmitFor(player, "SECTION_EXITED", new() { ["sectionId"] = person.Section,
                ["durationMs"] = (Time.unscaledTime - person.SectionAt) * 1000d, ["reason"] = reason,
                ["collectionSource"] = "HOST_PLAYER" }, "HOST")) return false;
            person.Section = 0; return true;
        }
        void Arrived(NetworkPlayer player)
        {
            if (!StartPlayer(player, true) || !participants.TryGetValue(player.OwnerClientId, out var person) || person.Arrived) return;
            int at = Section(player);
            person.Arrived = log.EmitFor(player, "PLAYER_ARRIVED", new() { ["sectionId"] = at > 0 ? at : null,
                ["position"] = DetailLogIds.Position(player.transform.position), ["lostCount"] = DetailLogIds.Lost(player.LostParts),
                ["collectionSource"] = "HOST_PLAYER" }, "HOST");
            if (person.Arrived) CloseSection(player, "ARRIVED");
        }
        void Progress()
        {
            if (!Host || attempt == null || trick != "TIGHTROPE") return;
            foreach (var person in participants.Values)
            {
                var player = Find(person.Client);
                if (person.Started && player != null) log.EmitFor(player, "PERFORMANCE_PROGRESS", new() {
                    ["trickType"] = trick, ["difficulty"] = difficulty, ["maxDistance"] = person.MaxDistance,
                    ["collectionSource"] = "HOST_PLAYER" }, "HOST");
            }
        }
        void RewardResult(PerformanceResult result)
        {
            if (result == null || sharedPerformance != result.Performance.Id) return;
            if (Host) foreach (var person in result.Participants)
            {
                var player = Find(person.ClientId);
                if (player == null) continue;
                if (person.Judgement is "Arrived" or "DirectFinish") Arrived(player);
                if (person.MaximumDistance.HasValue) log.EmitFor(player, "PERFORMANCE_PROGRESS", new() {
                    ["trickType"] = trick, ["difficulty"] = difficulty, ["maxDistance"] = person.MaximumDistance.Value,
                    ["source"] = "HOST_RESULT", ["collectionSource"] = "HOST_PLAYER" }, "HOST");
            }
            int arrivals = result.Participants.Count(p => p.Judgement == "Arrived" || p.Judgement == "DirectFinish");
            Terminal(result.Performance.Id, result.Succeeded, result.ElapsedSeconds.HasValue ? result.ElapsedSeconds.Value * 1000 : (Time.unscaledTime - began) * 1000d,
                result.Participants.Count(p => !p.Dead && p.Connected), arrivals, result.EndReason, result.ElapsedSeconds.HasValue ? "HOST_RESULT" : "OBSERVER_INTERVAL");
        }
        void TightropeResult(TightropeResult result)
        {
            if (result == null || TightropeRun.Current?.ExternallyManaged == true) return;
            Terminal(result.AttemptId, result.Succeeded, result.Elapsed * 1000d,
                result.Participants.Count(p => p.Connected && p.Outcome != ParticipantOutcome.Fallen),
                result.Participants.Count(p => p.Outcome == ParticipantOutcome.DirectFinish), result.Reason.ToString(), "HOST_RESULT");
        }
        void Terminal(string resultId, bool success, double duration, int survivors, int arrivals, string reason, string source)
        {
            if (attempt == null || ended || !confirmed.Add(resultId)) return;
            foreach (var hooks in players.Values) hooks.Flush();
            foreach (ulong actor in launches.Keys.ToArray()) EndFlight(actor, "PERFORMANCE_ENDED");
            Progress(); foreach (var person in participants.Values) CloseSection(Find(person.Client), "PERFORMANCE_ENDED");
            var payload = new Dictionary<string, object> { ["trickType"] = trick, ["difficulty"] = difficulty, ["durationMs"] = duration,
                ["durationSource"] = source, ["maxSection"] = maximumSection };
            if (success) { payload["survivors"] = survivors; payload["arrivals"] = arrivals; }
            else payload["reason"] = reason;
            if (log.Emit(success ? "PERFORMANCE_SUCCEEDED" : "PERFORMANCE_FAILED", payload, Host ? "HOST" : "OBSERVED")) ended = true;
            log.Flush();
        }
        public void Leave(string reason)
        {
            if (attempt == null || ended) return;
            foreach (var hooks in players.Values) hooks.Flush();
            Progress();
            if (Host) foreach (var person in participants.Values)
            {
                var player = Find(person.Client);
                if (player != null) log.EmitFor(player, "PERFORMANCE_LEFT", new() { ["trickType"] = trick,
                    ["sectionId"] = person.Section > 0 ? person.Section : null, ["reason"] = reason,
                    ["collectionSource"] = "HOST_PLAYER" }, "HOST");
            }
            foreach (ulong actor in launches.Keys.ToArray()) EndFlight(actor, reason);
            foreach (var person in participants.Values) CloseSection(Find(person.Client), "PERFORMANCE_ENDED"); ended = true;
        }
        public void SceneLeaving() { Leave("SCENE_CHANGED"); attempt = null; log.SetAttempt(null); }
        void Saw(SawHit hit) { if (hit.Player != null && players.TryGetValue(hit.Player, out var p)) p.Hit("SAW_" + hit.Kind.ToString().ToUpperInvariant(), "OB_" + DurableOutbox.Sha256("tightrope:saw:" + hit.Kind + ":" + hit.Index).Substring(0, 12)); }
        void Fire(FireHit hit) { if (players.TryGetValue(hit.Player, out var p)) p.Hit("FIRE", "OB_" + DurableOutbox.Sha256("tightrope:fire:" + hit.Kind + ":" + hit.Segment).Substring(0, 12)); }
        void Links()
        {
            if (!Host || attempt == null || ended || piggyback == null) return;
            foreach (var player in NetworkPlayer.All)
            {
                var lower = piggyback.GetBelow(player); ulong value = lower != null ? lower.OwnerClientId : ulong.MaxValue;
                if (below.TryGetValue(player.OwnerClientId, out ulong old) && old == value) continue;
                below[player.OwnerClientId] = value;
                if (lower == null) continue;
                log.EmitFor(player, "COOP_ACTIVATED", new() { ["trickType"] = "TIGHTROPE", ["gimmickType"] = "PIGGYBACK",
                    ["gimmickId"] = DetailLogIds.ObjectId(piggyback, "IN_"), ["participants"] = new[] { log.ParticipantCode(player.OwnerClientId), log.ParticipantCode(lower.OwnerClientId) },
                    ["height"] = piggyback.GetFloor(player) }, "HOST");
            }
        }
        void Bounced(ulong actor)
        {
            if (!Host || attempt == null || ended || trick != "CANNON") return;
            var player = Find(actor); if (player == null) return;
            var trampoline = HumanCannonTrampoline.Current;
            var ids = new List<string> { log.ParticipantCode(actor) };
            if (trampoline != null && trampoline.Carrier != CannonSeatState.Nobody && trampoline.Carrier != actor) ids.Add(log.ParticipantCode(trampoline.Carrier));
            log.EmitFor(player, "COOP_ACTIVATED", new() { ["trickType"] = trick, ["gimmickType"] = "TRAMPOLINE", ["gimmickId"] = DetailLogIds.ObjectId(trampoline, "IN_"), ["participants"] = ids }, "HOST");
        }
        void Handhold(NetworkPlayer actor, NetworkPlayer target)
        {
            if (!Host || attempt == null || ended || trick != "TIGHTROPE" || actor == null || target == null) return;
            log.EmitFor(actor, "COOP_ACTIVATED", new() { ["trickType"] = trick, ["gimmickType"] = "HANDHOLD",
                ["gimmickId"] = DetailLogIds.ObjectId(piggyback, "IN_"), ["participants"] = new[] { log.ParticipantCode(actor.OwnerClientId), log.ParticipantCode(target.OwnerClientId) },
                ["source"] = "HOST_LANE_LANDING" }, "HOST");
        }
        void CannonCooperation()
        {
            var poles = HumanCannonPoleSystem.Current;
            if (poles != null)
                for (int i = 0; i < poles.PoleCount; i++)
                {
                    ulong occupant = poles.State(i).Occupant;
                    if (poleOccupants.TryGetValue(i, out ulong before) && before == occupant) continue;
                    poleOccupants[i] = occupant;
                    if (occupant != CannonSeatState.Nobody) Cooperation(occupant, "POLE", poles.Pole(i));
                }
            var routes = HumanCannonRouteSystem.Current;
            if (routes != null)
                for (int i = 0; i < routes.TransportCount; i++)
                {
                    var state = routes.TransportState(i);
                    if (state.Phase != DeparturePhase.Moving || state.Rider == CannonSeatState.Nobody
                        || transportRevisions.TryGetValue(i, out uint revision) && revision == state.Revision) continue;
                    transportRevisions[i] = state.Revision;
                    Cooperation(state.Rider, i == 1 ? "LIFT" : "PLATFORM", routes.Transport(i));
                }
        }
        void Cooperation(ulong actor, string type, Component source)
        {
            var player = Find(actor); if (player == null || source == null) return;
            log.EmitFor(player, "COOP_ACTIVATED", new() { ["trickType"] = "CANNON", ["gimmickType"] = type,
                ["gimmickId"] = DetailLogIds.ObjectId(source, "IN_"), ["participants"] = new[] { log.ParticipantCode(actor) },
                ["source"] = "HOST_STATE_TRANSITION" }, "HOST");
        }
        void Cannon(int index, CannonAction action, ulong actor)
        {
            if (attempt == null || ended || trick != "CANNON" || !Host) return;
            if (action == CannonAction.Reset) { foreach (ulong id in launches.Keys.ToArray()) EndFlight(id, "RESET"); return; }
            if (action != CannonAction.Launch || index < 0 || index >= cannons.StationCount) return;
            var player = Find(actor); var station = cannons.Station(index);
            if (player == null || station == null) return;
            StartPlayer(player, true);
            if (launches.ContainsKey(actor)) EndFlight(actor, "RELAUNCHED");
            string launch = Guid.NewGuid().ToString("D"); Vector3 velocity = station.LaunchVelocity;
            int firingSection = Section(player);
            if (log.EmitFor(player, "CANNON_FIRED", new() { ["launchId"] = launch, ["sectionId"] = firingSection > 0 ? firingSection : null,
                ["cannonId"] = DetailLogIds.ObjectId(station, "IN_"), ["direction"] = new { x = velocity.normalized.x, y = velocity.normalized.y, z = velocity.normalized.z },
                ["power"] = velocity.magnitude, ["lostCount"] = DetailLogIds.Lost(player.LostParts), ["sectionSource"] = firingSection > 0 ? "COURSE_BOUNDARIES" : "UNAVAILABLE" }, "HOST")) launches[actor] = launch;
        }
        void Landed(ulong actor, int station, uint revision, uint segment, Vector3 position, bool safe)
        {
            if (!Host || trick != "CANNON") return;
            var player = Find(actor);
            if (!safe && player != null && players.TryGetValue(player, out var hooks)) hooks.Hit("UNSAFE_LANDING", null);
            EndFlight(actor, safe ? "SAFE_LANDING" : "UNSAFE_LANDING", safe, position, station, revision, segment);
        }
        void EndFlight(ulong actor, string reason, bool? safeLanding = null, Vector3? contact = null, int? station = null, uint? revision = null, uint? segment = null)
        {
            if (!launches.TryGetValue(actor, out string launch)) return;
            var player = Find(actor);
            var position = contact ?? (player != null ? player.transform.position : Vector3.zero);
            int landingSection = cannonSections != null ? (int)cannonSections.SectionAt(position) + 1 : 0;
            if (player != null) log.EmitFor(player, "CANNON_FLIGHT_ENDED", new() { ["launchId"] = launch, ["sectionId"] = landingSection > 0 ? landingSection : null,
                ["position"] = DetailLogIds.Position(position), ["reachedTarget"] = safeLanding ?? (player.State == PlayerState.Arrived ? true : null),
                ["reason"] = reason, ["terminationSource"] = safeLanding.HasValue ? "HOST_LANDING_JUDGEMENT" : "PUBLIC_FLIGHT_STATE",
                ["stationIndex"] = station, ["launchRevision"] = revision, ["flightSegment"] = segment,
                ["targetDefinition"] = "SAFE_LANDING_ZONE" }, "HOST");
            launches.Remove(actor);
        }
        static NetworkPlayer Find(ulong actor) => NetworkPlayer.All.FirstOrDefault(p => p != null && p.OwnerClientId == actor);
        sealed class ParticipantObservation
        {
            public ulong Client;
            public int? Slot;
            public int StartingLost, Section;
            public float SectionAt;
            public double MaxDistance;
            public bool Started, Arrived;
        }
        public void Dispose()
        {
            if (rewards != null) rewards.ResultConfirmed -= RewardResult;
            if (results != null) results.ResultConfirmed -= TightropeResult;
            if (saws != null) saws.Hit -= Saw; if (fire != null) fire.Hit -= Fire;
            if (cannons != null) { cannons.StateChanged -= Cannon; cannons.LandingConfirmed -= Landed; }
            if (piggyback != null) { piggyback.LinksChanged -= Links; piggyback.HandholdConfirmed -= Handhold; }
            HumanCannonTrampoline.ServerBounced -= Bounced;
            if (cannonHazards != null) { cannonHazards.ContactDetected -= CannonContact; cannonHazards.ContactResolved -= CannonContactResolved; }
            foreach (var hooks in players.Values) hooks.Dispose(); players.Clear();
        }
        sealed class PlayerHooks : IDisposable
        {
            readonly PerformanceLogAdapter owner;
            readonly NetworkPlayer player;
            readonly PlayerCondition condition;
            readonly PlayerBalance balance;
            readonly PlayerMover mover;
            int originRope, targetRope, jumpSection;
            string jump;
            readonly List<DamageObservation> cuts = new();
            DamageObservation death;
            string cause = "UNKNOWN", obstacle;
            int hitFrame = -1;
            bool deadAtStart, deathEmitted;
            Vector3? contactPosition;
            public void BeginAttempt() { deadAtStart = player.State == PlayerState.Fallen; deathEmitted = false; cuts.Clear(); death = null; hitFrame = -1; }
            public PlayerHooks(PerformanceLogAdapter adapter, NetworkPlayer target)
            {
                owner = adapter; player = target; condition = player.GetComponent<PlayerCondition>(); balance = player.GetComponent<PlayerBalance>(); mover = player.GetComponent<PlayerMover>();
                BeginAttempt();
                player.StateChanged += State; if (condition != null) condition.Changed += Body;
                if (balance != null) balance.Fell += Fell;
                if (mover != null && player.IsLocal) mover.AirborneChanged += Airborne;
            }
            public void Hit(string reason, string id, Vector3? position = null)
            {
                cause = reason; obstacle = id; contactPosition = position; hitFrame = Time.frameCount;
                if (cuts.Count > 0 && cuts[^1].Frame == hitFrame) { cuts[^1].Cause = reason; cuts[^1].Obstacle = id; if (position.HasValue) cuts[^1].Position = position.Value; }
                if (death != null && death.Frame == hitFrame) { death.Cause = reason; death.Obstacle = id; if (position.HasValue) death.Position = position.Value; }
            }
            void Fell()
            {
                // PlayerBalance also mirrors a network death/ragdoll. That is not evidence
                // of a balance failure in cannon, nor may it overwrite a confirmed obstacle.
                if (owner.trick != "TIGHTROPE") return;
                if (hitFrame != Time.frameCount || obstacle == null) Hit("OUT_OF_BALANCE", null);
                var course = TightropeCourse.Current;
                if (!player.IsLocal || owner.attempt == null || owner.ended || course == null) return;
                if (course.TryGetRopePoint(player.transform.position, out int rope, out float distance) && owner.Section(player) > 0)
                    owner.log.Emit("TIGHTROPE_FELL", new() { ["sectionId"] = owner.Section(player), ["ropeNumber"] = rope + 1,
                        ["distance"] = Math.Max(0, distance), ["lostCount"] = DetailLogIds.Lost(player.LostParts), ["handhold"] = null,
                        ["piggyback"] = owner.piggyback != null && owner.piggyback.IsInStack(player) });
            }
            void Airborne(bool airborne)
            {
                var course = TightropeCourse.Current;
                if (owner.attempt == null || owner.ended || owner.trick != "TIGHTROPE" || course == null) { jump = null; return; }
                if (airborne && mover.CurrentJump is JumpKind.InPlace or JumpKind.Lane)
                {
                    if (!course.TryGetRopePoint(player.transform.position, out int rope, out _)) return;
                    originRope = rope + 1; int target = mover.CurrentJump == JumpKind.Lane ? course.FindLandingLane(player.transform.position, mover.LaneJumpDirection) : rope;
                    targetRope = target + 1; jumpSection = owner.Section(player); jump = mover.CurrentJump.ToString();
                }
                else if (!airborne) FinishJump(player.State == PlayerState.Normal);
            }
            void FinishJump(bool landed)
            {
                if (jump == null) return;
                if (jumpSection > 0 && targetRope > 0) owner.log.Emit("JUMP_RESULT", new() { ["sectionId"] = jumpSection, ["jumpType"] = jump,
                    ["originRope"] = originRope, ["targetRope"] = targetRope, ["landed"] = landed });
                jump = null;
            }
            DamageObservation Snapshot() => new() { Frame = Time.frameCount, Lost = DetailLogIds.Lost(player.LostParts),
                Position = Time.frameCount == hitFrame && contactPosition.HasValue ? contactPosition.Value : player.transform.position,
                Section = owner.Section(player), Cause = Time.frameCount == hitFrame ? cause : "UNKNOWN", Obstacle = Time.frameCount == hitFrame ? obstacle : null };
            void State(PlayerState before, PlayerState after)
            {
                if (owner.attempt == null || owner.ended) return;
                if (after == PlayerState.Fallen && before != after && !deathEmitted) death = Snapshot();
                if (after == PlayerState.Arrived && before != after && owner.Host) owner.Arrived(player);
            }
            void Body(BodyPart before, BodyPart after)
            {
                if (player.IsApplyingInitialCondition) return;
                if (player.IsLocal) owner.log.Emit("INJURY_CHANGED", new() { ["beforeLostCount"] = DetailLogIds.Lost(before), ["afterLostCount"] = DetailLogIds.Lost(after) });
                if (owner.attempt == null || owner.ended) return;
                foreach (BodyPart part in PlayerCondition.CutOrder)
                    if (((after & ~before) & part) != 0) { var observation = Snapshot(); observation.Part = part; observation.Lost = DetailLogIds.Lost(after); cuts.Add(observation); }
            }
            public void Flush()
            {
                if (player == null || owner.attempt == null) { cuts.Clear(); death = null; return; }
                // Host settlement may synchronously run before our StateChanged subscriber.
                // Observe the already-confirmed state once, without inventing a pre-existing death.
                if (!owner.ended && !deadAtStart && !deathEmitted && death == null && player.State == PlayerState.Fallen) death = Snapshot();
                if (owner.Host)
                {
                    if (death != null || cuts.Count > 0) owner.StartPlayer(player, true);
                    foreach (var cut in cuts) owner.log.EmitFor(player, "LIMB_SEVERED", new() { ["sectionId"] = cut.Section > 0 ? cut.Section : null, ["position"] = DetailLogIds.Position(cut.Position),
                        ["part"] = cut.Part.ToString(), ["afterLostCount"] = cut.Lost, ["obstacleId"] = cut.Obstacle, ["cause"] = cut.Cause }, "HOST");
                    if (death != null) deathEmitted = owner.log.EmitFor(player, "PLAYER_DIED", new() { ["sectionId"] = death.Section > 0 ? death.Section : null, ["position"] = DetailLogIds.Position(death.Position),
                        ["cause"] = death.Cause, ["lostCount"] = death.Lost, ["obstacleId"] = death.Obstacle }, "HOST");
                }
                if (death != null) { if (player.IsLocal) FinishJump(false); if (owner.Host) owner.CloseSection(player, "DIED"); }
                if (death != null && !owner.Host) deathEmitted = true;
                cuts.Clear(); death = null;
            }
            sealed class DamageObservation { public int Frame, Lost, Section; public BodyPart Part; public Vector3 Position; public string Cause, Obstacle; }
            public void Dispose() { if (player != null) player.StateChanged -= State; if (condition != null) condition.Changed -= Body; if (balance != null) balance.Fell -= Fell; if (mover != null) mover.AirborneChanged -= Airborne; }
        }
    }
}
