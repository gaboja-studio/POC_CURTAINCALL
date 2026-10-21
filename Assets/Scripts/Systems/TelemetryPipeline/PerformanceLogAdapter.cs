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
        PerformanceRewards rewards;
        TightropeResults results;
        TightropeSaws saws;
        TightropeFire fire;
        HumanCannonSystem cannons;
        HumanCannonCourseSections cannonSections;
        HumanCannonHazardCourse cannonHazards;
        PiggybackSystem piggyback;
        string attempt, trick;
        string sharedPerformance;
        float began, sectionAt, nextProgress;
        int section, maximumSection, difficulty = 1;
        double maximumDistance;
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
                section = maximumSection = 0; maximumDistance = 0; sectionAt = began; launches.Clear(); below.Clear(); poleOccupants.Clear(); transportRevisions.Clear();
                difficulty = nextTrick == "CANNON" && HumanCannonHazardCourse.Current != null ? HumanCannonHazardCourse.Current.Difficulty : 1;
                log.SetAttempt(attempt);
                foreach (var hooks in players.Values) hooks.BeginAttempt();
                var local = NetworkPlayer.Local;
                log.Emit("PERFORMANCE_STARTED", new() { ["trickType"] = trick, ["difficulty"] = difficulty,
                    ["retryIndex"] = trick == "TIGHTROPE" ? Math.Max(1, run != null ? run.Round : 1) : 1,
                    ["playerCount"] = Math.Clamp(NetworkPlayer.All.Count, 1, 4), ["startingLostCount"] = local != null ? DetailLogIds.Lost(local.LostParts) : 0 }, Host ? "HOST" : "LOCAL");
            }
            if (attempt == null || ended) return;
            log.SetAttempt(attempt);
            ObserveSection();
            var localPlayer = NetworkPlayer.Local; var course = TightropeCourse.Current;
            if (Host) foreach (var player in NetworkPlayer.All) maximumSection = Math.Max(maximumSection, Section(player));
            if (trick == "TIGHTROPE" && localPlayer != null && localPlayer.State == PlayerState.Normal && course != null)
                maximumDistance = Math.Max(maximumDistance, Math.Clamp(course.GetDistance(localPlayer.transform.position), 0, course.FinishDistance));
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
        void ObserveSection()
        {
            var local = NetworkPlayer.Local; int current = Section(local);
            if (local == null || local.State != PlayerState.Normal || current == 0 || current == section) return;
            CloseSection(current > section ? "PASSED" : "RETREATED");
            section = current; maximumSection = Math.Max(maximumSection, current); sectionAt = Time.unscaledTime;
            log.Emit("SECTION_ENTERED", new() { ["sectionId"] = section, ["lostCount"] = DetailLogIds.Lost(local.LostParts) });
        }
        void CloseSection(string reason)
        {
            if (section > 0) log.Emit("SECTION_EXITED", new() { ["sectionId"] = section, ["durationMs"] = (Time.unscaledTime - sectionAt) * 1000d, ["reason"] = reason });
            section = 0;
        }
        void Progress()
        {
            var local = NetworkPlayer.Local; var course = TightropeCourse.Current;
            if (attempt == null || trick != "TIGHTROPE" || local == null || course == null) return;
            log.Emit("PERFORMANCE_PROGRESS", new() { ["trickType"] = trick, ["difficulty"] = difficulty, ["maxDistance"] = maximumDistance });
        }
        void RewardResult(PerformanceResult result)
        {
            if (result == null || sharedPerformance != result.Performance.Id) return;
            var local = NetworkPlayer.Local;
            var person = local != null ? result.Participants.FirstOrDefault(p => p.ClientId == local.OwnerClientId) : null;
            if (person?.MaximumDistance != null) log.Emit("PERFORMANCE_PROGRESS", new() { ["trickType"] = trick, ["difficulty"] = difficulty,
                ["maxDistance"] = person.MaximumDistance.Value, ["source"] = "HOST_RESULT" });
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
            Progress(); CloseSection("PERFORMANCE_ENDED");
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
            log.Emit("PERFORMANCE_LEFT", new() { ["trickType"] = trick, ["sectionId"] = section > 0 ? section : null, ["reason"] = reason });
            foreach (ulong actor in launches.Keys.ToArray()) EndFlight(actor, reason);
            CloseSection("PERFORMANCE_ENDED"); ended = true;
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
                    ["gimmickId"] = DetailLogIds.ObjectId(piggyback, "IN_"), ["participants"] = new[] { log.PlayerCode(player.OwnerClientId), log.PlayerCode(lower.OwnerClientId) },
                    ["height"] = piggyback.GetFloor(player) }, "HOST");
            }
        }
        void Bounced(ulong actor)
        {
            if (!Host || attempt == null || ended || trick != "CANNON") return;
            var player = Find(actor); if (player == null) return;
            var trampoline = HumanCannonTrampoline.Current;
            var ids = new List<string> { log.PlayerCode(actor) };
            if (trampoline != null && trampoline.Carrier != CannonSeatState.Nobody && trampoline.Carrier != actor) ids.Add(log.PlayerCode(trampoline.Carrier));
            log.EmitFor(player, "COOP_ACTIVATED", new() { ["trickType"] = trick, ["gimmickType"] = "TRAMPOLINE", ["gimmickId"] = DetailLogIds.ObjectId(trampoline, "IN_"), ["participants"] = ids }, "HOST");
        }
        void Handhold(NetworkPlayer actor, NetworkPlayer target)
        {
            if (!Host || attempt == null || ended || trick != "TIGHTROPE" || actor == null || target == null) return;
            log.EmitFor(actor, "COOP_ACTIVATED", new() { ["trickType"] = trick, ["gimmickType"] = "HANDHOLD",
                ["gimmickId"] = DetailLogIds.ObjectId(piggyback, "IN_"), ["participants"] = new[] { log.PlayerCode(actor.OwnerClientId), log.PlayerCode(target.OwnerClientId) },
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
                ["gimmickId"] = DetailLogIds.ObjectId(source, "IN_"), ["participants"] = new[] { log.PlayerCode(actor) },
                ["source"] = "HOST_STATE_TRANSITION" }, "HOST");
        }
        void Cannon(int index, CannonAction action, ulong actor)
        {
            if (attempt == null || ended || trick != "CANNON" || !Host) return;
            if (action == CannonAction.Reset) { foreach (ulong id in launches.Keys.ToArray()) EndFlight(id, "RESET"); return; }
            if (action != CannonAction.Launch || index < 0 || index >= cannons.StationCount) return;
            var player = Find(actor); var station = cannons.Station(index);
            if (player == null || station == null) return;
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
            void State(PlayerState before, PlayerState after) { if (after == PlayerState.Fallen && before != after && owner.attempt != null && !owner.ended && !deathEmitted) death = Snapshot(); }
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
                    foreach (var cut in cuts) owner.log.EmitFor(player, "LIMB_SEVERED", new() { ["sectionId"] = cut.Section > 0 ? cut.Section : null, ["position"] = DetailLogIds.Position(cut.Position),
                        ["part"] = cut.Part.ToString(), ["afterLostCount"] = cut.Lost, ["obstacleId"] = cut.Obstacle, ["cause"] = cut.Cause }, "HOST");
                    if (death != null) deathEmitted = owner.log.EmitFor(player, "PLAYER_DIED", new() { ["sectionId"] = death.Section > 0 ? death.Section : null, ["position"] = DetailLogIds.Position(death.Position),
                        ["cause"] = death.Cause, ["lostCount"] = death.Lost, ["obstacleId"] = death.Obstacle }, "HOST");
                }
                if (death != null && player.IsLocal) { FinishJump(false); owner.CloseSection("DIED"); }
                if (death != null && !owner.Host) deathEmitted = true;
                cuts.Clear(); death = null;
            }
            sealed class DamageObservation { public int Frame, Lost, Section; public BodyPart Part; public Vector3 Position; public string Cause, Obstacle; }
            public void Dispose() { if (player != null) player.StateChanged -= State; if (condition != null) condition.Changed -= Body; if (balance != null) balance.Fell -= Fell; if (mover != null) mover.AirborneChanged -= Airborne; }
        }
    }
}
