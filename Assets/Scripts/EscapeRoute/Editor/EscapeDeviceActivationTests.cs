using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Cameras;
using CurtainCall.Damage;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Survival;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools.Tests
{
    [Category("CommonProgressionRegression")]
    public sealed class EscapeDeviceActivationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

        static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);
        static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, Private).Invoke(instance, args);
        static void SetCurrent<T>(T instance) => typeof(T).GetProperty("Current").SetValue(null, instance);

        [Test]
        public void LiveEscapeLauncherWaitsForCampaignMembershipWithoutBlockingOutgoingScene()
        {
            var previous = EscapeCampaignContent.Current;
            var previousTransfer = EscapeSceneTransfer.Current;
            var root = new GameObject("Escape launcher transfer gate");
            root.SetActive(false);
            var departure = EditorSceneManager.NewPreviewScene();
            var oldRoot = new GameObject("Waiting room launcher");
            oldRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(oldRoot, departure);
            try
            {
                var content = root.AddComponent<EscapeCampaignContent>();
                SetCurrent(content);
                SetCurrent<EscapeSceneTransfer>(null);
                var launcher = root.AddComponent<EscapeExitSaw>();
                var oldLauncher = oldRoot.AddComponent<EscapeExitSaw>();
                var ready = typeof(EscapeExitSaw).GetMethod("CanUseCampaignLauncher", Private);
                Assert.That(ready.Invoke(launcher, new object[] { 3UL }), Is.True);
                typeof(EscapeCampaignContent).GetProperty("IsCampaign").SetValue(content, true);
                Assert.That(ready.Invoke(launcher, new object[] { 3UL }), Is.False);
                Assert.That(ready.Invoke(oldLauncher, new object[] { 3UL }), Is.True);
            }
            finally
            {
                SetCurrent(previous);
                SetCurrent(previousTransfer);
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(departure);
            }
        }

        [TestCase(false, null, false)]
        [TestCase(true, null, true)]
        [TestCase(false, PlayerState.Normal, false)]
        [TestCase(false, PlayerState.EscapeCaptured, false)]
        [TestCase(true, PlayerState.Normal, true)]
        [TestCase(true, PlayerState.EscapeCaptured, true)]
        [TestCase(true, PlayerState.Arrived, false)]
        [TestCase(true, PlayerState.Fallen, false)]
        [TestCase(true, PlayerState.Retired, false)]
        public void LiveEscapeCountsOnlyConnectedUnresolvedParticipants(bool connected, PlayerState? state, bool pending)
        {
            var method = typeof(EscapeCampaignContent).GetMethod("IsUnresolvedParticipant", Static);
            Assert.That(method.Invoke(null, new object[] { connected, state }), Is.EqualTo(pending));
        }

        [TestCase(CampaignPhase.RestTime, RestTimerState.WaitingForExit, false)]
        [TestCase(CampaignPhase.Night, RestTimerState.WaitingForExit, false)]
        [TestCase(CampaignPhase.RestTime, RestTimerState.Stopped, true)]
        [TestCase(CampaignPhase.Night, RestTimerState.Stopped, true)]
        [TestCase(CampaignPhase.Performance, RestTimerState.Stopped, false)]
        public void NormalEscapeDepartureFollowsCampaignAdmission(CampaignPhase phase, RestTimerState timer, bool allowed)
        {
            var state = new CampaignSnapshot { Phase = phase, Timer = timer };
            var admission = typeof(RunState).GetMethod("CanStartEscape", Static);
            Assert.That(admission.Invoke(null, new object[] { state }), Is.EqualTo(allowed));
        }

        [TestCase(3UL, true, true)]
        [TestCase(2UL, true, false)]
        [TestCase(3UL, false, false)]
        public void NormalEscapePlacementResolvesOnlyTheCurrentSpawnOfTheCapturedOwner(ulong owner, bool spawned, bool allowed)
        {
            var root = new GameObject("Transfer avatar replacement");
            root.SetActive(false);
            var original = new GameObject("Old avatar"); original.SetActive(false);
            try
            {
                var transfer = root.AddComponent<EscapeSceneTransfer>();
                var oldPlayer = original.AddComponent<NetworkPlayer>();
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(oldPlayer, 3UL);
                Set(transfer, "localPlaced", true);
                Set(transfer, "placedPlayer", oldPlayer);
                var oldMover = original.GetComponent<PlayerMover>();
                Object.DestroyImmediate(original);

                var replacement = root.AddComponent<NetworkPlayer>();
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(replacement, owner);
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(replacement, spawned);
                Set(transfer, "localCaptured", true);
                Set(transfer, "capturedOwner", 3UL);
                Assert.That(Call(transfer, "IsLocalPlacementCurrent", replacement), Is.False);
                var resolve = typeof(EscapeSceneTransfer).GetMethod("TryResolveCapturedMover", Private);
                object[] args = { replacement, null };
                Assert.That(resolve.Invoke(transfer, args), Is.EqualTo(allowed));
                if (allowed)
                {
                    Assert.That(args[1], Is.SameAs(root.GetComponent<PlayerMover>()));
                    Assert.That((PlayerMover)args[1] == oldMover, Is.False);
                    Set(transfer, "placedPlayer", replacement);
                    Assert.That(Call(transfer, "IsLocalPlacementCurrent", replacement), Is.True);
                }
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(replacement, false);
            }
            finally
            {
                if (original != null) Object.DestroyImmediate(original);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(2)]
        [TestCase(4)]
        public void PartialLoadCannotMovePlayersOrAcceptPlacementAcksBeforeApproval(int count)
        {
            var root = new GameObject("Partial escape load");
            root.SetActive(false);
            try
            {
                var transfer = root.AddComponent<EscapeSceneTransfer>();
                var arrival = root.AddComponent<EscapeSceneArrival>();
                var type = typeof(EscapeSceneTransfer).GetNestedType("Snapshot", BindingFlags.NonPublic);
                object Snapshot(int generation, bool approved)
                {
                    return JsonUtility.FromJson(
                        "{\"phase\":1,\"generation\":" + generation
                        + ",\"placementAuthorized\":" + (approved ? "true" : "false")
                        + ",\"participants\":[" + string.Join(",", Enumerable.Range(1, count))
                        + "],\"positions\":[" + string.Join(",", Enumerable.Repeat("{}", count))
                        + "],\"rotations\":[" + string.Join(",", Enumerable.Repeat("{\"w\":1}", count)) + "]}", type);
                }

                bool Apply(object snapshot) => (bool)typeof(EscapeSceneTransfer).GetMethod("ApplySnapshot", Private)
                    .Invoke(transfer, new[] { snapshot });
                var placed = (HashSet<ulong>)typeof(EscapeSceneTransfer).GetField("placed", Private).GetValue(transfer);
                var captured = (HashSet<ulong>)typeof(EscapeSceneTransfer).GetField("captured", Private).GetValue(transfer);

                Assert.That(Apply(Snapshot(2, false)), Is.True);
                Set(transfer, "localPlaced", true);
                Set(transfer, "nextSnapshot", 10.5f);
                Assert.That(Call(transfer, "ShouldRepeatPendingSnapshot", 10.49f, true), Is.False);
                Assert.That(Call(transfer, "ShouldRepeatPendingSnapshot", 10.5f, false), Is.False);
                Assert.That(Call(transfer, "ShouldRepeatPendingSnapshot", 10.5f, true), Is.True);
                Assert.That(transfer.PlaceLocal(arrival), Is.False);
                Call(transfer, "Request", 2UL, "{\"generation\":2,\"kind\":\"captured\"}");
                Call(transfer, "Request", 2UL, "{\"generation\":2,\"kind\":\"placed\"}");
                Assert.That(captured, Does.Contain(2UL));
                Assert.That(placed, Is.Empty);

                Assert.That(Apply(Snapshot(2, true)), Is.True);
                Assert.That(Apply(Snapshot(2, false)), Is.False, "A delayed failure state cannot withdraw approval.");
                Assert.That(transfer.PlaceLocal(arrival), Is.False, "A cached placement cannot replace the current avatar.");
                Call(transfer, "Request", 2UL, "{\"generation\":1,\"kind\":\"placed\"}");
                Assert.That(placed, Is.Empty);
                Call(transfer, "Request", 2UL, "{\"generation\":2,\"kind\":\"placed\"}");
                Assert.That(placed, Does.Contain(2UL));

                Assert.That(Apply(Snapshot(3, false)), Is.True);
                Assert.That(Apply(Snapshot(2, true)), Is.False, "A previous round cannot authorize the current transfer.");
                Assert.That(transfer.PlaceLocal(arrival), Is.False);
                Assert.That(placed, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test, Category("EscapeFullCombinationAudit")]
        public void AuthoredStaticPolesClearBothPassengersAndRetainSupportedInjuredAccess()
        {
            const string path = "Assets/Scenes/Levels/escape_route.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var frame = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Single(t => t.name == "EscapeCampaignFrame");
                var poles = frame.GetComponentsInChildren<HumanCannonPole>(true).Where(p => p.isActiveAndEnabled && p.Loop == null).ToArray();
                Assert.That(poles.Length, Is.GreaterThan(0), "An empty scene cannot pass the geometry audit.");
                Physics.SyncTransforms();
                var audit = typeof(EscapeLabyrinthAudit).GetMethod("AuditPoleBodyClearance", Static);
                var report = new EscapeLabyrinthAudit.Report();
                foreach (var pole in poles) audit.Invoke(null, new object[] { pole.transform, frame, report });
                Assert.That(report.failures, Is.Empty, string.Join("\n", report.failures));
                var supports = new Dictionary<string, string>
                {
                    ["A04A_01_CavernExit"] = "A03_CannonLandingWire",
                    ["A04A_02_DeepTransfer"] = "A04A_UpperWire",
                    ["A04A_03_PumpEntry"] = "A04A_LowerWire",
                    ["A06_WireExitSwing"] = "A06_UpperReturnWire",
                    ["A06_HelperAscentClutch"] = "유지봉 측면 정비턱",
                    ["A07C_HelperBridgeClutch"] = "동료 역할 교대 정비대",
                    ["A07C_ReturnBridgeClutch"] = "복귀 고정 후 마지막 질주면"
                };
                foreach (var entry in supports)
                {
                    var pole = poles.Single(p => p.name == entry.Key);
                    foreach (bool injured in new[] { false, true })
                        Assert.That(HasSupportedPoleAccess(pole, entry.Value, injured), Is.True,
                            $"{entry.Key}: support {entry.Value}, injured={injured}");
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test, Category("EscapeFullCombinationAudit")]
        public void AuthoredSwingReleasesReachTheirNextPlatforms()
        {
            const string path = "Assets/Scenes/Levels/escape_route.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var poles = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonPole>()).ToLookup(p => p.name);
                var supports = roots.SelectMany(r => r.GetComponentsInChildren<Collider>()).Where(c => c.enabled && !c.isTrigger)
                    .ToLookup(c => c.name);
                var cushions = roots.SelectMany(r => r.GetComponentsInChildren<HumanCannonLandingZone>()).ToArray();
                var links = new (string pole, string[] targets)[]
                {
                    ("A04A_03_PumpEntry", new[] { "대포 포획 준비대" }),
                    ("Labyrinth Inspection swing 03", new[] { "02 inspection relay apron" }),
                    ("Labyrinth lower inspection swing 7", new[] { "Inspection receiving island" }),
                    ("Labyrinth processing swing 2", new[] { "Processing receiving island" }),
                    ("Labyrinth Return north swing", new[] { "Return north receiving balcony" }),
                    ("Labyrinth Return direct swing", new[] { "Return direct cable departure" }),
                    ("A06_FlameExitSwing", new[] { "코어 끝 짧은 대포대" }),
                    ("A06_WireExitSwing", new[] { "반출동 착지 측면 이음", "A07 접합 발판" }),
                    ("Labyrinth Extraction swing 03", new[] { "06 extraction turn island" }),
                    ("Labyrinth Extraction swing 04", new[] { "06 extraction south join", "06 flush western return" }),
                    ("Labyrinth Transfer swing 03", new[] { "06 second transfer pier" })
                };
                Physics.SyncTransforms();
                var failures = new List<string>();
                foreach (var (name, names) in links)
                {
                    var targets = names.SelectMany(n => supports[n]).ToHashSet();
                    var pole = poles[name].SingleOrDefault();
                    if (!pole || targets.Count == 0)
                    {
                        failures.Add($"{name}: saved pole or platform missing ({string.Join(", ", names)}).");
                        continue;
                    }
                    foreach (int fps in new[] { 30, 60, 120 })
                        for (int seat = 0; seat < pole.RiderCapacity; seat++)
                        {
                            string result = MeasureSwingLanding(pole, targets, cushions, seat, fps, out bool reachable);
                            TestContext.WriteLine(result);
                            if (!reachable) failures.Add(result);
                        }
                }
                Assert.That(failures, Is.Empty, string.Join("\n", failures));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static string MeasureSwingLanding(HumanCannonPole pole, HashSet<Collider> targets,
            HumanCannonLandingZone[] cushions, int seat, int fps, out bool reachable)
        {
            var character = GameSettings.Base.Character;
            float dt = 1f / fps;
            float nearest = float.PositiveInfinity;
            var origin = pole.HangingPosition(0, seat);
            var target = targets.OrderBy(c => (c.bounds.ClosestPoint(origin) - origin).sqrMagnitude).First();
            string prefix = $"{pole.name} -> {target.name} | {fps} Hz seat {seat + 1}";
            string closest = "";
            var successes = new List<(float time, float angle, Vector3 launch, float flight, Vector3 point, string name, float drop, bool cushioned)>();
            Vector3 side = Vector3.Cross(Vector3.up, pole.Forward);
            var air = new CannonAirControl();
            for (int strategy = 0; strategy < 3 && successes.Count == 0; strategy++)
            {
                float angle = 0f, speed = 0f;
                for (int tick = 1; tick <= fps * 8; tick++)
                {
                    float drive = strategy == 0 || speed >= 0 ? 1f : -1f;
                    pole.Step(ref angle, ref speed, drive, dt, character.Gravity);
                    if (drive < 0 || angle < -5f || tick % Mathf.Max(1, fps / 30) != 0) continue;
                    Vector3 launch = pole.ReleaseVelocity(angle, speed, seat, drive);
                    Vector3 start = pole.HangingPosition(angle, seat);
                    if (Vector3.Dot(launch, pole.Forward) < 1f) continue;
                    Vector3 feet = start, velocity = launch;
                    float peak = start.y;
                    Vector3 lower = Vector3.up * (character.CapsuleRadius + .015f);
                    Vector3 upper = Vector3.up * (character.CapsuleHeight - character.CapsuleRadius + .015f);
                    float steering = strategy == 0 ? 0f : strategy == 1
                        ? Mathf.Clamp(Vector3.Dot(target.bounds.center - start, side), -1, 1) : 1f;
                    air.BeginPoleFlight(drive);
                    for (int step = 1; step <= fps * 3; step++)
                    {
                        air.SetInput(new Vector2(steering, 0), step);
                        var correction = air.ConsumePoleVelocity(step, false, pole.Forward, pole.AirControlSpeed,
                            velocity, GameSettings.Poles.HorizontalSpeedLimit, dt);
                        Vector3 delta = BallisticMotion.Step(ref velocity, correction, character.Gravity, dt);
                        var hit = Physics.CapsuleCastAll(feet + lower, feet + upper, character.CapsuleRadius,
                            delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            .Where(h => h.distance > 0 && h.collider.gameObject.scene == pole.gameObject.scene
                                && !h.collider.transform.IsChildOf(pole.transform)).OrderBy(h => h.distance).FirstOrDefault();
                        feet += hit.collider ? delta.normalized * hit.distance : delta;
                        peak = Mathf.Max(peak, feet.y);
                        var top = target.bounds.ClosestPoint(feet);
                        top.y = target.bounds.max.y;
                        float gap = Vector3.Distance(feet, top);
                        if (gap < nearest)
                        {
                            nearest = gap;
                            closest = $"({angle:F1}°, {launch.magnitude:F2} m/s, {hit.collider?.name ?? "air"}, feet {feet:F2}, platform {top:F2})";
                        }
                        if (!hit.collider) continue;
                        if (targets.Contains(hit.collider) && hit.normal.y > .9f && velocity.y < 0
                            && hit.collider.Raycast(new Ray(feet + Vector3.up * .1f, Vector3.down), out var support, .2f))
                        {
                            // Require unobstructed foot support and the same fall threshold or
                            // authored landing-zone exemption used by the common game rules.
                            float drop = peak - support.point.y;
                            bool cushioned = GameSettings.Base.BodyDamage.SafeLandingZones
                                && cushions.Any(zone => zone.SupportsLanding(support.point));
                            if (cushioned || CommonDamageRules.Fall(drop) == FallOutcome.Safe)
                                successes.Add((tick * dt, angle, launch, step * dt, support.point, hit.collider.name, drop, cushioned));
                        }
                        if (hit.normal.y < -.9f && velocity.y > 0)
                        {
                            velocity.y = 0f;
                            continue;
                        }
                        break;
                    }
                }
            }
            reachable = successes.Count > 0;
            float edgeDistance = Vector3.Distance(Vector3.ProjectOnPlane(target.bounds.ClosestPoint(origin) - origin, Vector3.up), Vector3.zero);
            if (!reachable) return $"{prefix}: FAIL edge {edgeDistance:F2} m, closest {nearest:F2} m {closest}";
            var first = successes[0];
            float travelled = (first.point - pole.HangingPosition(first.angle, seat)).magnitude;
            return $"{prefix}: {successes.Count} release samples, edge {edgeDistance:F2} m; "
                + $"first {first.time:F2} s, {first.angle:F1}°, {first.launch:F2} m/s, "
                + $"flight {first.flight:F2} s / {travelled:F2} m to {first.name} {first.point:F2}; "
                + $"drop {first.drop:F2} m{(first.cushioned ? " (landing zone)" : "")}";
        }

        static bool HasSupportedPoleAccess(HumanCannonPole pole, string support, bool injured)
        {
            var settings = CurtainCall.Settings.GameSettings.Base;
            float height = injured ? settings.BodyDamage.CrawlCapsuleHeight : settings.Character.CapsuleHeight;
            float radius = Mathf.Min(settings.Character.CapsuleRadius, height * .5f);
            float reach = CurtainCall.Settings.GameSettings.Poles.GrabRadius * (injured ? settings.BodyDamage.ReachMultiplier(1) : 1f);
            var target = pole.BarPosition(0);
            for (float x = -3; x <= 3; x += .5f)
                for (float z = -3; z <= 3; z += .5f)
                    foreach (var hit in Physics.RaycastAll(target + new Vector3(x, 1, z), Vector3.down, 7,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.gameObject.scene != pole.gameObject.scene || hit.collider.name != support || hit.normal.y < .9f) continue;
                        var feet = hit.point + Vector3.up * .03f; var center = feet + Vector3.up * height * .5f;
                        if (Vector3.Distance(center, target) > reach) continue;
                        if (Physics.OverlapCapsule(feet + Vector3.up * radius, feet + Vector3.up * (height - radius), radius,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(c => c.gameObject.scene == pole.gameObject.scene && !c.transform.IsChildOf(pole.transform))) continue;
                        if (PlayerInteraction.ClearPathFrom(center, target, pole.transform, null, true)) return true;
                    }
            return false;
        }

        [TestCase(3), TestCase(4), Category("EscapeFullCombinationAudit")]
        public void IndependentClientReplicasKeepTheExitOpenWhenSpareLauncherPacketsArriveLate(int crew)
        {
            var roots = new List<GameObject>();
            var previous = EscapeExitSaw.Current;
            GameObject Root(string name)
            {
                var go = new GameObject(name); go.SetActive(false); roots.Add(go); return go;
            }
            var devices = new EscapeExitSaw[crew + 1, 4];
            try
            {
                for (int replica = 0; replica <= crew; replica++)
                {
                    // No cable, door, transform or ownership object is shared between clients.
                    var cableRoot = Root("Replica cables " + replica);
                    var cables = new EscapeCuttableCable[24];
                    for (int i = 0; i < cables.Length; i++)
                    {
                        var child = new GameObject("Strand " + i); child.transform.SetParent(cableRoot.transform);
                        cables[i] = child.AddComponent<EscapeCuttableCable>();
                    }
                    var session = Root("Replica session " + replica).AddComponent<NetworkSessionManager>();
                    typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session,
                        replica == 0 ? SessionConnectionState.Host : SessionConnectionState.Client);
                    var door = Root("Replica door " + replica).transform;
                    for (int device = 0; device < 4; device++)
                    {
                        var saw = Root($"Replica {replica} launcher {device}").AddComponent<EscapeExitSaw>();
                        devices[replica, device] = saw;
                        Set(saw, "cables", cables); Set(saw, "doorPanel", door);
                        Set(saw, "session", session); Set(saw, "deviceId", device);
                        Set(saw, "projectileLifetime", 0f);
                        Call(saw, "InitializePose"); Call(saw, "OnEnable");
                    }
                }
                byte[] Packet(EscapeExitSaw source, uint revision, uint mask)
                {
                    Set(source, "stateRevision", revision); Set(source, "cutMask", mask);
                    using var writer = new FastBufferWriter(4096, Allocator.Temp);
                    Call(source, "WriteState", writer, 0); return writer.ToArray();
                }
                void Receive(EscapeExitSaw target, byte[] packet)
                {
                    using var reader = new FastBufferReader(packet, Allocator.Temp);
                    Call(target, "ReadState", NetworkManager.ServerClientId, reader);
                }
                var stale = new byte[4][]; var opened = new byte[4][];
                for (int i = 0; i < 4; i++)
                {
                    stale[i] = Packet(devices[0, i], 1, 0);
                    opened[i] = Packet(devices[0, i], 2, (1u << 24) - 1);
                }
                for (int replica = 1; replica <= crew; replica++)
                {
                    int first = (replica - 1) % 4;
                    Receive(devices[replica, first], opened[first]);
                    for (int other = 1; other <= crew; other++)
                        Assert.That(devices[other, 0].DoorOpen, Is.EqualTo(other <= replica),
                            "A host/client in the same editor must not supply another client's missing packet.");
                    for (int step = 1; step < 4; step++)
                    {
                        int device = (first + step) % 4;
                        Receive(devices[replica, device], stale[device]);
                        Assert.That(devices[replica, device].DoorOpen, Is.True, "A late spare launcher cannot close the shared door.");
                    }
                    for (int device = 3; device >= 0; device--)
                    {
                        Receive(devices[replica, device], opened[device]);
                        Receive(devices[replica, device], stale[device]);
                        Assert.That(devices[replica, device].CutCableCount, Is.EqualTo(24));
                        Assert.That(devices[replica, device].DoorOpen, Is.True);
                    }
                }
            }
            finally
            {
                foreach (var device in devices) if (device != null) Call(device, "OnDisable");
                for (int i = roots.Count - 1; i >= 0; i--) Object.DestroyImmediate(roots[i]);
                SetCurrent(previous);
            }
        }

        [TestCase(3), TestCase(4), Category("EscapeFullCombinationAudit")]
        public void EveryTeamVerdictCombinationWaitsForUnresolvedMembersAndEndsExactlyOnce(int crew)
        {
            // Eight distinct host observations per member, crossed with both door states.
            // This is authoritative state coverage, not live network or traversal coverage.
            var all = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
            var savedPlayers = all.ToArray(); all.Clear();
            var previousRun = CampaignRun.Instance;
            var previousTransfer = EscapeSceneTransfer.Current;
            var root = new GameObject("Escape verdict combinations"); root.SetActive(false);
            var floor = new GameObject("Matrix arrival"); floor.transform.position = new Vector3(24000, 3000, 24000);
            var area = floor.AddComponent<BoxCollider>(); area.isTrigger = true; area.size = Vector3.one * 4;
            var spawned = typeof(NetworkBehaviour).GetProperty("IsSpawned");
            var server = typeof(NetworkBehaviour).GetProperty("IsServer");
            var actors = new List<NetworkPlayer>();
            try
            {
                var run = root.AddComponent<CampaignRun>(); Set(run, "offlineRun", true);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
                var transfer = root.AddComponent<EscapeSceneTransfer>(); SetCurrent(transfer);
                var snap = typeof(EscapeSceneTransfer).GetField("snapshot", Private).GetValue(transfer);
                var ids = Enumerable.Range(0, crew).Select(i => (ulong)(23000 + i)).ToArray();
                snap.GetType().GetField("phase").SetValue(snap, 2);
                snap.GetType().GetField("participants").SetValue(snap, ids);
                var channel = CreateChannel(transfer);
                var netRoot = new GameObject("Matrix roster"); netRoot.SetActive(false); netRoot.transform.SetParent(root.transform);
                var session = netRoot.AddComponent<NetworkSessionManager>();
                var manager = netRoot.GetComponent<NetworkManager>(); Set(session, "_networkManager", manager);
                typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
                channel.GetType().GetProperty("Session").SetValue(channel, session);
                var connected = (IDictionary<ulong, NetworkClient>)manager.ConnectedClients;
                var content = root.AddComponent<EscapeCampaignContent>();
                typeof(EscapeCampaignContent).GetProperty("IsCampaign").SetValue(content, true);
                Set(content, "content", floor); Set(content, "arrivalArea", area); Set(content, "run", run);
                var saw = root.AddComponent<EscapeExitSaw>(); Set(content, "exitSaw", saw);
                var arrived = (HashSet<ulong>)typeof(EscapeCampaignContent).GetField("arrived", Private).GetValue(content);
                var states = new List<NetworkVariable<PlayerState>>();
                for (int i = 0; i < crew; i++)
                {
                    var go = new GameObject("Matrix actor " + i); go.transform.SetParent(root.transform);
                    var actor = go.AddComponent<NetworkPlayer>();
                    server.SetValue(actor, true); typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(actor, ids[i]);
                    actors.Add(actor); all.Add(actor);
                    states.Add((NetworkVariable<PlayerState>)typeof(NetworkPlayer).GetField("state", Private).GetValue(actor));
                }
                int combinations = 1 << (crew * 3);
                for (int door = 0; door < 2; door++)
                    for (int combination = 0; combination < combinations; combination++)
                    {
                        var host = new RunState(); host.DebugStartAt(new RunStartPoint(3, RunStartPhase.Escape, 0, CampaignSnapshot.TightropeId));
                        Set(run, "host", host); Set(run, "state", host.Snapshot);
                        Set(content, "reported", false); arrived.Clear(); connected.Clear();
                        Set(saw, "cutProgress", door == 1 ? 100f : 0f);
                        bool pending = false, success = false;
                        for (int i = 0; i < crew; i++)
                        {
                            int kind = (combination >> (i * 3)) & 7;
                            // 0 outside, 1 inside, 2 arrived, 3 fallen, 4 retired,
                            // 5 captured, 6 disconnected, 7 connected but avatar pending.
                            // A disconnected avatar may still be present at the exit until NGO despawns it.
                            spawned.SetValue(actors[i], kind != 7);
                            if (kind != 6) connected.Add(ids[i], new NetworkClient());
                            states[i].Value = kind == 2 ? PlayerState.Arrived : kind == 3 ? PlayerState.Fallen
                                : kind == 4 ? PlayerState.Retired : kind == 5 ? PlayerState.EscapeCaptured : PlayerState.Normal;
                            actors[i].transform.position = floor.transform.position + (kind is 1 or 6 ? Vector3.zero : Vector3.forward * 10);
                            pending |= kind == 0 || kind == 1 && door == 0 || kind == 5 || kind == 7;
                            success |= kind == 2 || kind == 1 && door == 1;
                        }
                        Call(content, "Update");
                        string label = $"crew={crew}, door={door}, base8={System.Convert.ToString(combination, 8)}";
                        Assert.That(run.State.Phase, Is.EqualTo(pending ? CampaignPhase.Escape : CampaignPhase.Ending), label);
                        if (!pending) Assert.That(run.State.LastEnd, Is.EqualTo(success ? RunEndReason.EscapeSucceeded : RunEndReason.EscapeAllRetired), label);
                        var phase = run.State.Phase; var end = run.State.LastEnd;
                        Call(content, "Update");
                        Assert.That(run.State.Phase, Is.EqualTo(phase), "Repeated verdict: " + label);
                        Assert.That(run.State.LastEnd, Is.EqualTo(end), "Repeated verdict: " + label);
                    }
            }
            finally
            {
                foreach (var actor in actors) { spawned.SetValue(actor, false); server.SetValue(actor, false); }
                all.Clear(); all.AddRange(savedPlayers);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun); SetCurrent(previousTransfer);
                Object.DestroyImmediate(root); Object.DestroyImmediate(floor);
            }
        }

        [TestCase(0), TestCase(24), TestCase(64), Category("EscapeNovember16Followup")]
        public void FullSawPacketsRoundTripAndRejectTruncationOrNonHostSenders(int count)
        {
            var sourceRoot = new GameObject("Saw packet source"); sourceRoot.SetActive(false);
            var targetRoot = new GameObject("Saw packet target"); targetRoot.SetActive(false);
            var netRoot = new GameObject("Saw packet client"); netRoot.SetActive(false);
            try
            {
                var source = sourceRoot.AddComponent<EscapeExitSaw>();
                var target = targetRoot.AddComponent<EscapeExitSaw>();
                foreach (var saw in new[] { source, target })
                { Set(saw, "fireWithoutCables", true); Set(saw, "projectileLifetime", 0f); }
                var ownership = new CannonSeatState(); ownership.Receive(3, 42);
                Set(source, "ownership", ownership); Set(source, "stateRevision", 7u);
                for (int i = 0; i < count; i++) Call(source, "SpawnProjectile");
                var client = netRoot.AddComponent<NetworkSessionManager>();
                typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(client, SessionConnectionState.Client);
                Set(target, "session", client);
                using var writer = new FastBufferWriter(4096, Allocator.Temp);
                Call(source, "WriteState", writer, 0);
                Assert.That(writer.Length, Is.EqualTo(66 + count * 36));
                using (var unauthorized = new FastBufferReader(writer, Allocator.Temp))
                    Call(target, "ReadState", 42UL, unauthorized);
                Assert.That(target.Carrier, Is.EqualTo(ulong.MaxValue));
                var bytes = writer.ToArray(); System.Array.Resize(ref bytes, bytes.Length - 1);
                using (var truncated = new FastBufferReader(bytes, Allocator.Temp))
                    Call(target, "ReadState", NetworkManager.ServerClientId, truncated);
                Assert.That(target.Carrier, Is.EqualTo(ulong.MaxValue));
                using (var valid = new FastBufferReader(writer, Allocator.Temp))
                    Call(target, "ReadState", NetworkManager.ServerClientId, valid);
                Assert.That(target.Carrier, Is.EqualTo(42UL));
                Assert.That(target.ActiveProjectileCount, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(sourceRoot); Object.DestroyImmediate(targetRoot); Object.DestroyImmediate(netRoot); }
        }

        [TestCase(1UL), TestCase(2UL), TestCase(3UL)]
        public void GuestPickupSurvivesFacingDisagreementAndRequestBeforeReplicatedPosition(ulong guestId)
        {
            var roots = new List<GameObject>();
            var origin = new Vector3(14800, 60, 14800);
            var previousCannon = HumanCannonSystem.Current;
            var previousSaw = EscapeExitSaw.Current;
            var roster = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
            var previousRoster = roster.ToArray();
            var spawned = typeof(NetworkPlayer).GetProperty("IsSpawned");
            var owner = typeof(NetworkPlayer).GetProperty("IsOwner");
            GameObject Root(string name, Vector3 point)
            {
                var go = new GameObject(name); go.SetActive(false);
                go.transform.position = point; roots.Add(go); return go;
            }
            NetworkSessionManager Session(SessionConnectionState role)
            {
                var result = Root(role.ToString(), origin).AddComponent<NetworkSessionManager>();
                typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(result, role);
                typeof(NetworkSessionManager).GetProperty("GameState").SetValue(result, GameSessionState.Playing);
                return result;
            }
            EscapeExitSaw Launcher(NetworkSessionManager session)
            {
                var go = Root("Launcher replica", origin + Vector3.forward * 1.1f);
                var result = go.AddComponent<EscapeExitSaw>();
                var handle = new GameObject("Handle").transform;
                handle.SetParent(go.transform, false); handle.localPosition = new Vector3(0, 1.05f, -1.1f);
                var load = go.AddComponent<BoxCollider>(); load.center = Vector3.up * .75f; load.size = Vector3.one * .75f;
                Set(result, "handle", handle); Set(result, "loadCollider", load); Set(result, "fireWithoutCables", true);
                go.SetActive(true); Set(result, "session", session); HumanCannonCarry.Register(result); return result;
            }
            void Snapshot(EscapeExitSaw source, EscapeExitSaw target)
            {
                using var writer = new FastBufferWriter(256, Allocator.Temp);
                Call(source, "WriteState", writer, 0);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                Call(target, "ReadState", NetworkManager.ServerClientId, reader);
            }
            NetworkPlayer guest = null;
            try
            {
                roster.Clear();
                var floor = Root("Floor", origin + Vector3.down * .25f);
                floor.AddComponent<BoxCollider>().size = new Vector3(20, .5f, 20); floor.SetActive(true);
                var wall = Root("Wall behind pickup", origin + new Vector3(0, .8f, -1.1f));
                wall.AddComponent<BoxCollider>().size = new Vector3(2, 1.6f, .5f); wall.SetActive(true);
                var cannon = Root("Cannon carry controls", origin).AddComponent<HumanCannonSystem>();
                Call(cannon, "ResetSeats"); SetCurrent(cannon);
                var host = Launcher(Session(SessionConnectionState.Host)); Set(host, "synchronized", true);
                var client = Launcher(Session(SessionConnectionState.Client));
                // The replicas in this test share a scene. Each role only sees its own load collider.
                host.GetComponent<BoxCollider>().enabled = false;
                host.Suppressed = true;
                var actor = Root("Participant", origin); guest = actor.AddComponent<NetworkPlayer>();
                typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(guest, guestId);
                var mover = actor.GetComponent<PlayerMover>();
                Call(mover, "Awake"); Set(guest, "mover", mover);
                mover.FreeMovement = true;
                spawned.SetValue(guest, true); owner.SetValue(guest, true); actor.SetActive(true); roster.Add(guest);
                actor.transform.rotation = Quaternion.Euler(0, 180, 0);
                Physics.SyncTransforms();
                Assert.That(client.CanGrab(guest), Is.False, "Client cannot take a launcher before receiving host state.");
                Snapshot(host, client);
                Assert.That(HumanCannonCarry.TryFollowPose(guest, client.CarryRoot, client.Handle, 1.05f, true,
                    out var oldPosition, out var oldRotation, out _), Is.True);
                Assert.That(HumanCannonCarry.CanMove(client.CarryRoot, client.GetComponent<BoxCollider>(), oldPosition,
                    oldRotation, guest, false), Is.False, "The prior camera/avatar-facing pose overlaps the wall.");
                Assert.That(client.CanGrab(guest), Is.True, "The handle-aligned pose fits at the same pickup point.");
                Assert.That(HumanCannonCarry.FindGrabbable(guest), Is.SameAs(client), "F can now select the synchronized launcher.");

                owner.SetValue(guest, false);
                actor.transform.position = origin + Vector3.right * 7f;
                host.Suppressed = false;
                client.GetComponent<BoxCollider>().enabled = false; host.GetComponent<BoxCollider>().enabled = true;
                Physics.SyncTransforms();
                using var request = new FastBufferWriter(9, Allocator.Temp);
                request.WriteValueSafe((byte)1); request.WriteValueSafe(0u); request.WriteValueSafe(0);
                using var input = new FastBufferReader(request, Allocator.Temp);
                Call(host, "ReadRequest", guestId, input);
                Assert.That(host.Carrier, Is.EqualTo(CannonSeatState.Nobody));
                actor.transform.position = origin; Physics.SyncTransforms();
                Call(host, "TickPendingGrabs", Time.unscaledTimeAsDouble);
                Assert.That(host.Carrier, Is.EqualTo(guestId));
                Snapshot(host, client);
                Assert.That(client.Carrier, Is.EqualTo(guestId));
            }
            finally
            {
                if (guest != null) spawned.SetValue(guest, false);
                roster.Clear(); roster.AddRange(previousRoster);
                foreach (var go in roots)
                {
                    if (go.TryGetComponent(out EscapeExitSaw launcher)) HumanCannonCarry.Unregister(launcher);
                    Object.DestroyImmediate(go);
                }
                SetCurrent(previousCannon); SetCurrent(previousSaw); Physics.SyncTransforms();
            }
        }

        [Test, Category("EscapeNovember16Recovery")]
        public void AdditiveSawChannelsDoNotCollideWithTheDepartureScene()
        {
            var departure = EditorSceneManager.NewPreviewScene();
            var destination = EditorSceneManager.NewPreviewScene();
            departure.name = "WaitingRoom"; destination.name = "escape_route";
            try
            {
                var one = new GameObject("Departure saw"); one.SetActive(false);
                var two = new GameObject("Escape saw"); two.SetActive(false);
                SceneManager.MoveGameObjectToScene(one, departure);
                SceneManager.MoveGameObjectToScene(two, destination);
                var source = one.AddComponent<EscapeExitSaw>();
                var target = two.AddComponent<EscapeExitSaw>();
                foreach (string property in new[] { "RequestName", "StateName" })
                {
                    var p = typeof(EscapeExitSaw).GetProperty(property, Private);
                    Assert.That(p.GetValue(source), Is.Not.EqualTo(p.GetValue(target)), property);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(destination); EditorSceneManager.ClosePreviewScene(departure); }
        }

        [Test, Category("EscapeNovember16Recovery")]
        public void DisabledDepartureRunCannotVetoEscapeFallDamageThroughACapturedDelegate()
        {
            var root = new GameObject("Departed externally managed rope"); root.SetActive(false);
            var run = root.AddComponent<TightropeRun>();
            var actorRoot = new GameObject("Escape fall actor"); actorRoot.SetActive(false);
            var actor = actorRoot.AddComponent<NetworkPlayer>();
            var previous = NetworkPlayer.ServerCanChangeState;
            try
            {
                Set(run, "externallyManaged", true);
                Set(run, "externalStarted", false);
                var guard = (System.Func<NetworkPlayer, PlayerState, bool>)System.Delegate.CreateDelegate(
                    typeof(System.Func<NetworkPlayer, PlayerState, bool>), run,
                    typeof(TightropeRun).GetMethod("CanChangeState", Private));
                // Escape/inspection can retain this delegate after the old course disables itself.
                NetworkPlayer.ServerCanChangeState = (player, state) => guard(player, state);
                Call(run, "OnDisable");
                Assert.That(NetworkPlayer.ServerCanChangeState(actor, PlayerState.Fallen), Is.True);
            }
            finally { NetworkPlayer.ServerCanChangeState = previous; Object.DestroyImmediate(actorRoot); Object.DestroyImmediate(root); }
        }

        static object CreateChannel(EscapeSceneTransfer transfer)
        {
            var field = typeof(EscapeSceneTransfer).GetField("channel", Private);
            var channel = System.Activator.CreateInstance(field.FieldType, "Escape.EditorTest", null, null, null, null, null);
            field.SetValue(transfer, channel);
            return channel;
        }

        [TestCase(PlayerState.Fallen), TestCase(PlayerState.Retired), TestCase(PlayerState.Arrived)]
        [Category("EscapeNovember16Followup")]
        public void DisabledEscapeGuardDoesNotBlockTheNextScene(PlayerState next)
        {
            var root = new GameObject("Inactive outgoing escape"); root.SetActive(false);
            var content = root.AddComponent<EscapeCampaignContent>();
            try
            {
                var guard = typeof(EscapeCampaignContent).GetMethod("AllowState", Private);
                Assert.That(guard.Invoke(content, new object[] { null, next }), Is.True,
                    "A delegate retained by another scene must not apply an inactive escape scene's restrictions.");
                Set(content, "previousStateRule", new System.Func<NetworkPlayer, PlayerState, bool>((_, _) => false));
                Assert.That(guard.Invoke(content, new object[] { null, next }), Is.False,
                    "The still-current shared guard must remain authoritative.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(3)] [TestCase(4)]
        [Category("EscapeNovember16Recovery"), Category("EscapeNovember16Followup")]
        public void OneArrivalThenLastTeammateFallEndsEscapeDespiteDisconnectAndLostLocalArrivalCache(int count)
        {
            var all = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
            var previousRun = CampaignRun.Instance;
            var previousTransfer = EscapeSceneTransfer.Current;
            var previousGuard = NetworkPlayer.ServerCanChangeState;
            var previousPlace = SurvivalRules.ServerPlace;
            var root = new GameObject("Escape team verdict"); root.SetActive(false);
            var room = new GameObject("Final arrival fixture");
            room.transform.position = new Vector3(15500, 2300, 15500);
            var area = room.AddComponent<BoxCollider>(); area.isTrigger = true; area.size = Vector3.one * 4;
            var players = new List<NetworkPlayer>();
            var spawned = typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned");
            var server = typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsServer");
            var owner = typeof(Unity.Netcode.NetworkBehaviour).GetProperty("OwnerClientId");
            void State(NetworkPlayer player, PlayerState next)
            {
                var variable = typeof(NetworkPlayer).GetField("state", Private).GetValue(player);
                variable.GetType().GetProperty("Value").SetValue(variable, next);
            }
            try
            {
                NetworkPlayer.ServerCanChangeState = null; SurvivalRules.ServerPlace = SurvivalPlace.EscapeRoute;
                var host = new RunState();
                host.DebugStartAt(new RunStartPoint(3, RunStartPhase.Escape, 0, CampaignSnapshot.TightropeId));
                var run = root.AddComponent<CampaignRun>();
                Set(run, "host", host); Set(run, "state", host.Snapshot); Set(run, "offlineRun", true);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
                var transfer = root.AddComponent<EscapeSceneTransfer>();
                var snapshot = typeof(EscapeSceneTransfer).GetField("snapshot", Private).GetValue(transfer);
                snapshot.GetType().GetField("phase").SetValue(snapshot, 2);
                var ids = Enumerable.Range(0, count).Select(i => (ulong)(19000 + i)).ToArray();
                snapshot.GetType().GetField("participants").SetValue(snapshot, ids);
                SetCurrent(transfer);
                var channel = CreateChannel(transfer);
                var networkRoot = new GameObject("Connected escape roster"); networkRoot.SetActive(false);
                networkRoot.transform.SetParent(root.transform, false);
                var session = networkRoot.AddComponent<NetworkSessionManager>();
                var manager = networkRoot.GetComponent<Unity.Netcode.NetworkManager>();
                Set(session, "_networkManager", manager);
                typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
                channel.GetType().GetProperty("Session").SetValue(channel, session);
                var connected = (IDictionary<ulong, Unity.Netcode.NetworkClient>)manager.ConnectedClients;
                foreach (ulong id in ids.Take(count - 1)) connected.Add(id, new Unity.Netcode.NetworkClient());
                var content = root.AddComponent<EscapeCampaignContent>();
                typeof(EscapeCampaignContent).GetProperty("IsCampaign").SetValue(content, true);
                Set(content, "content", room); Set(content, "arrivalArea", area); Set(content, "run", run);
                var saw = root.AddComponent<EscapeExitSaw>(); Set(saw, "cutProgress", 100f); Set(content, "exitSaw", saw);
                for (int i = 0; i < count - 1; i++) // last roster member disconnected
                {
                    var actor = new GameObject("Verdict actor " + i); actor.transform.SetParent(root.transform, false);
                    actor.transform.position = room.transform.position + (i == 0 ? Vector3.zero : Vector3.forward * 10);
                    var player = actor.AddComponent<NetworkPlayer>();
                    spawned.SetValue(player, true); server.SetValue(player, true); owner.SetValue(player, ids[i]);
                    State(player, i < 2 ? PlayerState.Normal : PlayerState.Retired);
                    players.Add(player); all.Add(player);
                }
                Call(content, "Update");
                Assert.That(players[0].State, Is.EqualTo(PlayerState.Arrived));
                Assert.That(run.State.Phase, Is.EqualTo(CampaignPhase.Escape), "The remaining living teammate keeps playing.");
                spawned.SetValue(players[1], false);
                Call(content, "Update");
                Assert.That(run.State.Phase, Is.EqualTo(CampaignPhase.Escape),
                    "A connected participant whose avatar is temporarily unavailable has not disconnected or died.");
                spawned.SetValue(players[1], true);
                ((HashSet<ulong>)typeof(EscapeCampaignContent).GetField("arrived", Private).GetValue(content)).Clear();
                var falling = players[1];
                falling.transform.position = new Vector3(0, CurtainCall.Settings.GameSettings.Base.BodyDamage.WorldDeathY - 1, 0);
                // Exercise the host verdict here; EditMode has no NGO transport to deliver an RPC.
                Assert.That(CurtainCall.Damage.CommonDamageRules.IsBelowWorldDeathY(falling.transform.position.y), Is.True);
                var accept = typeof(NetworkPlayer).GetMethod("AcceptWorldDeath", Private);
                Assert.That(accept.Invoke(falling, new object[] { falling.transform.position, 0, 0 }), Is.True);
                Assert.That(falling.State, Is.EqualTo(PlayerState.Retired));
                Call(content, "Update");
                Assert.That(run.State.LastEnd, Is.EqualTo(RunEndReason.EscapeSucceeded));
                Assert.That(EscapeEndingPresentation.IsSuccessfulEnding(run.State), Is.True);
            }
            finally
            {
                NetworkPlayer.ServerCanChangeState = previousGuard; SurvivalRules.ServerPlace = previousPlace;
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun); SetCurrent(previousTransfer);
                foreach (var p in players) { all.Remove(p); spawned.SetValue(p, false); server.SetValue(p, false); }
                Object.DestroyImmediate(root); Object.DestroyImmediate(room);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SoleGuestCanOpenThePhysicalGateAndFinishTheEscape(bool thirdPlayer, bool thirdDisconnected)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Levels/escape_route.unity", OpenSceneMode.Additive);
            var fixture = new GameObject("Guest-only escape verdict");
            fixture.SetActive(false);
            var all = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", Static).GetValue(null);
            var actors = new List<NetworkPlayer>();
            var previousRun = CampaignRun.Instance;
            var previousTransfer = EscapeSceneTransfer.Current;
            var previousGuard = NetworkPlayer.ServerCanChangeState;
            var previousPlace = SurvivalRules.ServerPlace;
            var spawned = typeof(NetworkBehaviour).GetProperty("IsSpawned");
            var server = typeof(NetworkBehaviour).GetProperty("IsServer");
            var owner = typeof(NetworkBehaviour).GetProperty("OwnerClientId");
            try
            {
                var frame = scene.GetRootGameObjects().Single().transform;
                var content = frame.GetComponentInChildren<EscapeCampaignContent>(true);
                var saw = (EscapeExitSaw)new SerializedObject(content).FindProperty("exitSaw").objectReferenceValue;
                var boss = saw.FinaleBoss;
                var hostRun = new RunState();
                hostRun.DebugStartAt(new RunStartPoint(3, RunStartPhase.Escape, 0, CampaignSnapshot.TightropeId));
                var run = fixture.AddComponent<CampaignRun>();
                Set(run, "host", hostRun); Set(run, "state", hostRun.Snapshot); Set(run, "offlineRun", true);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
                NetworkPlayer.ServerCanChangeState = null;
                SurvivalRules.ServerPlace = SurvivalPlace.EscapeRoute;
                var transfer = fixture.AddComponent<EscapeSceneTransfer>();
                ulong[] ids = thirdPlayer ? new[] { 0UL, 8201UL, 8202UL } : new[] { 0UL, 8201UL };
                var snapshot = typeof(EscapeSceneTransfer).GetField("snapshot", Private).GetValue(transfer);
                snapshot.GetType().GetField("phase").SetValue(snapshot, 2);
                snapshot.GetType().GetField("participants").SetValue(snapshot, ids);
                SetCurrent(transfer);
                var channel = CreateChannel(transfer);
                var session = fixture.AddComponent<NetworkSessionManager>();
                var manager = fixture.GetComponent<NetworkManager>();
                Set(session, "_networkManager", manager);
                typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
                channel.GetType().GetProperty("Session").SetValue(channel, session);
                var connected = (IDictionary<ulong, NetworkClient>)manager.ConnectedClients;
                foreach (ulong id in ids)
                {
                    var root = new GameObject("Escape actor " + id);
                    root.transform.SetParent(fixture.transform, false);
                    var actor = root.AddComponent<NetworkPlayer>();
                    spawned.SetValue(actor, true); server.SetValue(actor, true); owner.SetValue(actor, id);
                    var state = typeof(NetworkPlayer).GetField("state", Private).GetValue(actor);
                    state.GetType().GetProperty("Value").SetValue(state, id == ids[1] || thirdDisconnected && id == ids[2]
                        ? PlayerState.Normal : PlayerState.Retired);
                    if (!thirdDisconnected || id != ids[2]) connected.Add(id, new NetworkClient());
                    actors.Add(actor);
                    all.Add(actor);
                }
                var guest = actors[1];
                typeof(EscapeCampaignContent).GetProperty("IsCampaign").SetValue(content, true);
                Set(content, "run", run);
                Call(saw, "InitializePose");
                var arrival = (BoxCollider)new SerializedObject(content).FindProperty("arrivalArea").objectReferenceValue;
                guest.transform.position = arrival.transform.TransformPoint(arrival.center);
                Physics.SyncTransforms();

                int extras = ids.Length;
                Assert.That(EscapeFinaleFlowTests.Apply(boss, 10, 1, EscapePursuerBoss.Phase.Battle, count: (byte)extras), Is.True);
                Call(boss, "EnsureReinforcements", extras);
                var pack = (System.Collections.IEnumerable)typeof(EscapePursuerBoss).GetField("reinforcements", Private).GetValue(boss);
                var reinforcements = pack.Cast<EscapePursuerBoss>().ToArray();
                Assert.That(reinforcements.Length, Is.EqualTo(extras));
                var required = typeof(EscapeExitSaw).GetProperty("RequiredMask", Private);
                Set(saw, "cutMask", required.GetValue(saw));
                Assert.That(EscapeFinaleFlowTests.Apply(boss, 10, 2, EscapePursuerBoss.Phase.Dead, byte.MaxValue, (byte)extras), Is.True);
                foreach (var enemy in reinforcements.Take(extras - 1))
                    Assert.That(EscapeFinaleFlowTests.Apply(enemy, 10, 1, EscapePursuerBoss.Phase.Dead, byte.MaxValue), Is.True);
                Call(content, "Update");
                Assert.That(guest.State, Is.EqualTo(PlayerState.Normal));
                Assert.That(run.State.Phase, Is.EqualTo(CampaignPhase.Escape));

                Assert.That(EscapeFinaleFlowTests.Apply(reinforcements.Last(), 10, 1, EscapePursuerBoss.Phase.Dead, byte.MaxValue), Is.True);
                Call(saw, "Present", 0f, true);
                Physics.SyncTransforms();
                var from = frame.TransformPoint(new Vector3(398, 0, 78));
                var step = frame.TransformPoint(new Vector3(403.9f, 0, 78)) - from;
                Assert.That(Physics.CapsuleCastAll(from + Vector3.up * .4f, from + Vector3.up * 1.2f, .30f,
                    step.normalized, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.Empty);
                Call(content, "Update");
                Assert.That(guest.State, Is.EqualTo(PlayerState.Arrived));
                Assert.That(EscapeEndingPresentation.IsSuccessfulEnding(run.State), Is.True);
                Assert.That(content.HasArrived(0UL), Is.False);
                Assert.That(content.HasArrived(ids[1]), Is.True);
            }
            finally
            {
                foreach (var actor in actors) { all.Remove(actor); spawned.SetValue(actor, false); server.SetValue(actor, false); }
                NetworkPlayer.ServerCanChangeState = previousGuard;
                SurvivalRules.ServerPlace = previousPlace;
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
                SetCurrent(previousTransfer);
                Object.DestroyImmediate(fixture);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AdditiveDepartureHandsOffDeviceRootsAndDoesNotRecreateWaitingRoomCannon(bool practiceLaneOpen, bool unloadBeforeArrival)
        {
            var departure = EditorSceneManager.NewPreviewScene();
            var escape = EditorSceneManager.NewPreviewScene();
            var devicesField = typeof(WaitingRoomStageDevices).GetField("instance", Static);
            var previousDevices = devicesField.GetValue(null);
            var previousPractice = PracticeRoomFreePlay.Current;
            var previousCannon = HumanCannonSystem.Current;
            var previousTrampoline = HumanCannonTrampoline.Current;
            var created = new List<GameObject>();

            GameObject Make(string name, Scene scene)
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                created.Add(root);
                return root;
            }

            PracticeRoomFreePlay practice = null;
            EscapeCampaignContent content = null;
            try
            {
                var floor = Make("Departure floor and camera", departure);
                var departureLight = floor.AddComponent<UvFlashlight>();
                var practiceRoot = Make("PracticeFreePlay", departure);
                var practicePoles = practiceRoot.AddComponent<HumanCannonPoleSystem>();
                practice = practiceRoot.AddComponent<PracticeRoomFreePlay>();
                var practiceRopes = Make("RopesAndSwings", departure);
                practiceRopes.transform.SetParent(practiceRoot.transform, false);
                practiceRopes.AddComponent<TightropeCourse>();
                var practiceCannonLane = Make("CannonLane", departure);
                practiceCannonLane.transform.SetParent(practiceRoot.transform, false);
                practice.Configure(practiceRopes, practiceCannonLane, System.Array.Empty<HumanCannonStation>(), null,
                    System.Array.Empty<HumanCannonPole>(), System.Array.Empty<HumanCannonPole>());
                Call(practice, "OnEnable");
                practiceRopes.SetActive(true);
                practiceCannonLane.SetActive(practiceLaneOpen);

                var waitingCannon = Make("WaitingRoomCannonDevices", departure);
                var sourceCannon = waitingCannon.AddComponent<HumanCannonSystem>();
                SetCurrent(sourceCannon);
                var trampoline = practiceLaneOpen ? Make("WaitingRoomCarryTrampoline", departure) : null;
                SetCurrent(trampoline != null ? trampoline.AddComponent<HumanCannonTrampoline>() : null);
                var devices = Make("WaitingRoomStageDevices", departure).AddComponent<WaitingRoomStageDevices>();
                devicesField.SetValue(null, devices);
                Set(devices, "room", floor.transform);
                Set(devices, "systemRoot", waitingCannon);
                Set(devices, "trampoline", trampoline);
                Set(devices, "systemHasCannons", practiceLaneOpen);

                var target = Make("Escape devices", escape);
                target.SetActive(false);
                var targetCannon = target.AddComponent<HumanCannonSystem>();
                var targetPoles = target.AddComponent<HumanCannonPoleSystem>();
                var targetTrampoline = target.AddComponent<HumanCannonTrampoline>();
                var targetRopes = target.AddComponent<TightropeCourse>();
                content = Make("Escape frame", escape).AddComponent<EscapeCampaignContent>();
                Set(content, "departureScene", departure);
                Set(content, "content", target);
                Set(content, "activeRoots", new[] { target });

                Call(content, "SetActive", true);
                Assert.That(waitingCannon.activeSelf, Is.False, "The waiting-room cannon must release its channel before the escape systems enable.");
                Assert.That(trampoline != null && trampoline.activeSelf, Is.False, "The optional waiting-room trampoline must release its channel as well.");
                Assert.That(practicePoles.isActiveAndEnabled || practiceRopes.activeInHierarchy, Is.False);
                Assert.That(floor.activeInHierarchy, Is.True, "The source scene must keep its floor while clients report arrival.");
                Assert.That(departureLight.enabled, Is.False);
                var routeLight = content.GetComponent<UvFlashlight>();
                Assert.That(routeLight.isActiveAndEnabled, Is.True);
                Assert.That(targetCannon.isActiveAndEnabled && targetPoles.isActiveAndEnabled &&
                    targetTrampoline.isActiveAndEnabled && targetRopes.isActiveAndEnabled, Is.True);
                // EditMode does not run ordinary MonoBehaviour scene hooks. Reflect the registrations
                // their OnEnable/OnDisable callbacks complete synchronously inside SetActive in play.
                Call(practice, "OnDisable");
                // The scenery coroutine has not reached the escape trampoline yet, and this peer has no cached prop.
                SetCurrent<HumanCannonSystem>(null);
                SetCurrent<HumanCannonTrampoline>(null);
                Set(devices, "systemRoot", null);
                Set(devices, "trampoline", null);
                Call(devices, "Update");
                Assert.That(departure.GetRootGameObjects().Count(go => go.name == "WaitingRoomCannonDevices"), Is.EqualTo(1),
                    "A delayed destination registration must not reopen the departure device factory.");
                Set(devices, "systemRoot", waitingCannon);
                Set(devices, "trampoline", trampoline);
                SetCurrent(targetCannon);
                SetCurrent(targetTrampoline);
                Call(devices, "Update");
                Call(content, "SetActive", true);
                Assert.That(content.GetComponents<UvFlashlight>().Length, Is.EqualTo(1));
                Assert.That(departure.GetRootGameObjects().Any(go => go.name == "WaitingRoomCannonDevices" && go.activeInHierarchy), Is.False);

                if (unloadBeforeArrival) Call(content, "OnDestroy");
                else Call(content, "SetActive", false);
                Assert.That(target.activeSelf, Is.False);
                Assert.That(routeLight.enabled, Is.False);
                Assert.That(departureLight.isActiveAndEnabled, Is.True);
                Assert.That(practiceRoot.activeSelf, Is.True, "A cancelled transfer must restore the still-loaded practice course.");
                Call(practice, "OnEnable");
                SetCurrent(sourceCannon != null && sourceCannon.isActiveAndEnabled ? sourceCannon : null);
                SetCurrent<HumanCannonTrampoline>(null);
                Call(devices, "Update");
                var rebuilt = departure.GetRootGameObjects().Single(go => go.name == "WaitingRoomCannonDevices");
                Assert.That(rebuilt.GetComponent<HumanCannonSystem>().isActiveAndEnabled, Is.True);
            }
            finally
            {
                if (content != null) Call(content, "SetActive", false);
                if (practice != null) Call(practice, "OnDisable");
                SetCurrent(previousPractice);
                SetCurrent(previousCannon);
                SetCurrent(previousTrampoline);
                devicesField.SetValue(null, previousDevices);
                foreach (var root in created) if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(escape);
                EditorSceneManager.ClosePreviewScene(departure);
            }
        }

        [Test]
        public void AuthoredEscapeDevicesActivateOnlyWithTheirCampaignRoot()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Levels/escape_route.unity");
            try
            {
                var frame = scene.GetRootGameObjects().Single();
                var content = frame.GetComponent<EscapeCampaignContent>();
                var props = new SerializedObject(content);
                var roots = props.FindProperty("activeRoots");
                var rootObjects = Enumerable.Range(0, roots.arraySize).Select(i => roots.GetArrayElementAtIndex(i).objectReferenceValue as GameObject)
                    .Append(props.FindProperty("content").objectReferenceValue as GameObject).Where(go => go != null).ToArray();
                var disabled = props.FindProperty("campaignDisabledBehaviours");
                var disabledBehaviours = Enumerable.Range(0, disabled.arraySize).Select(i => disabled.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
                foreach (var type in new[] { typeof(HumanCannonSystem), typeof(HumanCannonPoleSystem), typeof(HumanCannonTrampoline),
                    typeof(TightropeCourse), typeof(CourseControlSwitcher), typeof(TightropeHangSystem), typeof(TightropeCatchSystem),
                    typeof(HumanCannonHazardCourse), typeof(EscapeCooperativeRopes), typeof(EscapeCooperativeLocks),
                    typeof(EscapeCooperativeSpan), typeof(EscapeExitSaw) })
                {
                    var devices = frame.GetComponentsInChildren(type, true).OfType<Behaviour>().ToArray();
                    Assert.That(devices, Is.Not.Empty, type.Name);
                    foreach (var device in devices)
                    {
                        Assert.That(rootObjects.Any(root => device.transform.IsChildOf(root.transform)), Is.True, type.Name);
                        Assert.That(disabledBehaviours.Contains(device), Is.False, type.Name);
                        Assert.That(device.enabled, Is.True, type.Name);
                        if (device is HumanCannonSystem)
                            Assert.That(device.transform.IsChildOf(frame.transform.Find("EscapeBackground")), Is.False,
                                "The cannon input owner must activate before the deferred scenery reveal.");
                    }
                }
                CheckRegistry<HumanCannonStation>(frame, frame.GetComponentInChildren<HumanCannonSystem>(true), "stations", 7);
                CheckRegistry<HumanCannonPole>(frame, frame.GetComponentInChildren<HumanCannonPoleSystem>(true), "poles", 37);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TransferDoesNotOverwriteMovementRestoredByTheDepartureDevice()
        {
            var root = new GameObject("Transfer movement");
            root.SetActive(false);
            var localPlayer = typeof(NetworkPlayer).GetProperty("Local");
            var previousPlayer = NetworkPlayer.Local;
            var previousTransfer = EscapeSceneTransfer.Current;
            var gateFields = new[] { "rearming", "checkedFrame", "blockedFrame" }
                .Select(name => typeof(InputGate).GetField(name, Static)).ToArray();
            var gateBefore = gateFields.Select(field => field.GetValue(null)).ToArray();
            var player = root.AddComponent<NetworkPlayer>();
            var transfer = root.AddComponent<EscapeSceneTransfer>();
            try
            {
                localPlayer.SetValue(null, player);
                SetCurrent(transfer);
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                CreateChannel(transfer);
                var snapshot = typeof(EscapeSceneTransfer).GetField("snapshot", Private).GetValue(transfer);
                snapshot.GetType().GetField("participants").SetValue(snapshot, new[] { player.OwnerClientId });
                transfer.SetSource(root.transform);
                var mover = root.GetComponent<PlayerMover>();
                mover.Simulated = false;

                Call(transfer, "CaptureLocal");
                Assert.That(EscapeSceneTransfer.HoldingForDeparture, Is.True);
                Assert.That(mover.Simulated, Is.False);
                mover.Simulated = true; // The old cannon or rope gives movement back during the overlap.
                transfer.Cancel();
                Assert.That(EscapeSceneTransfer.HoldingForDeparture, Is.False);
                Assert.That(mover.Simulated, Is.True);

                Call(transfer, "CaptureLocal");
                Assert.That(mover.Simulated, Is.True);
            }
            finally
            {
                transfer.Cancel();
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                localPlayer.SetValue(null, previousPlayer);
                SetCurrent(previousTransfer);
                Object.DestroyImmediate(root);
                for (int i = 0; i < gateFields.Length; i++) gateFields[i].SetValue(null, gateBefore[i]);
            }
        }

        [TestCase(CampaignPhase.RestTime)]
        [TestCase(CampaignPhase.Night)]
        public void DeparturePacketsCannotStartAnUnconfirmedEscape(CampaignPhase precedingPhase)
        {
            var root = new GameObject("Pending escape transfer");
            root.SetActive(false);
            var previous = CampaignRun.Instance;
            var runProperty = typeof(CampaignRun).GetProperty("Instance");
            var run = root.AddComponent<CampaignRun>();
            var transfer = root.AddComponent<EscapeSceneTransfer>();
            var session = root.AddComponent<NetworkSessionManager>();
            try
            {
                runProperty.SetValue(null, run);
                var channel = CreateChannel(transfer);
                channel.GetType().GetProperty("Session").SetValue(channel, session);
                const string snapshot = "{\"generation\":5,\"phase\":1,\"participants\":[1],\"positions\":[{}],\"rotations\":[{\"w\":1}]}";

                Set(run, "state", new CampaignSnapshot { Generation = 5, Phase = precedingPhase });
                Call(transfer, "Receive", snapshot);
                Call(transfer, "Update");
                Assert.That(transfer.HasDeparture, Is.False, "The host has not committed the escape phase yet.");

                Set(run, "state", new CampaignSnapshot { Generation = 5, Phase = CampaignPhase.Escape });
                Call(transfer, "Receive", snapshot);
                Assert.That(transfer.Pending, Is.True);

                Set(run, "state", new CampaignSnapshot { Generation = 5, Phase = CampaignPhase.Performance });
                Call(transfer, "Update");
                Assert.That(transfer.HasDeparture, Is.False);
            }
            finally
            {
                runProperty.SetValue(null, previous);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(PlayerState.Normal, false)]
        [TestCase(PlayerState.Fallen, true)]
        [TestCase(PlayerState.Retired, true)]
        public void DepartureCameraIsRequiredOnlyForPlayersWhoStillHaveToRun(PlayerState state, bool expected)
        {
            var root = new GameObject("Escape arrival without a local camera");
            var platform = new GameObject("Escape assembly floor");
            root.SetActive(false);
            var director = CameraViewDirector.Current;
            var previousContent = EscapeCampaignContent.Current;
            var previousPlayer = NetworkPlayer.Local;
            var local = typeof(NetworkPlayer).GetProperty("Local");
            var spawned = typeof(NetworkPlayer).GetProperty("IsSpawned");
            var player = root.AddComponent<NetworkPlayer>();
            try
            {
                SetCurrent<CameraViewDirector>(null);
                platform.transform.position = new Vector3(9200, 400, 9200);
                var floor = platform.AddComponent<BoxCollider>();
                floor.size = new Vector3(8, .25f, 8);
                floor.center = Vector3.down * .125f;
                var area = platform.AddComponent<BoxCollider>();
                area.isTrigger = true;
                area.size = new Vector3(8, 3, 8);
                area.center = Vector3.up * 1.5f;
                var arrival = root.AddComponent<EscapeSceneArrival>();
                Set(arrival, "assemblyArea", area);
                var content = root.AddComponent<EscapeCampaignContent>();
                Set(content, "content", platform);
                SetCurrent(content);

                var transfer = root.AddComponent<EscapeSceneTransfer>();
                CreateChannel(transfer);
                var snapshot = typeof(EscapeSceneTransfer).GetField("snapshot", Private).GetValue(transfer);
                snapshot.GetType().GetField("phase").SetValue(snapshot, 1);
                snapshot.GetType().GetField("placementAuthorized").SetValue(snapshot, true);
                snapshot.GetType().GetField("participants").SetValue(snapshot, new[] { player.OwnerClientId });
                snapshot.GetType().GetField("positions").SetValue(snapshot, new[] { Vector3.zero });
                snapshot.GetType().GetField("rotations").SetValue(snapshot, new[] { Quaternion.identity });
                Set(transfer, "localCaptured", true);
                Set(transfer, "capturedOwner", player.OwnerClientId);
                Set(transfer, "localRotation", Quaternion.identity);
                local.SetValue(null, player);
                spawned.SetValue(player, true);
                ((NetworkVariable<PlayerState>)typeof(NetworkPlayer).GetField("state", Private).GetValue(player)).Value = state;
                var mover = player.GetComponent<PlayerMover>();
                Set(mover, "controller", mover.GetComponent<CharacterController>());
                Physics.SyncTransforms();

                Assert.That(transfer.PlaceLocal(arrival), Is.EqualTo(expected));
                Assert.That(root.transform.position, Is.EqualTo(expected ? platform.transform.position : Vector3.zero));
                Assert.That(player.State, Is.EqualTo(state));
            }
            finally
            {
                spawned.SetValue(player, false);
                local.SetValue(null, previousPlayer);
                SetCurrent(previousContent);
                SetCurrent(director);
                Object.DestroyImmediate(platform);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ArrivalRetriesItsViewAndUsesTheActualDepartureScene()
        {
            var source = EditorSceneManager.NewPreviewScene();
            var destination = EditorSceneManager.NewPreviewScene();
            var shared = EditorSceneManager.NewPreviewScene();
            var previousDirector = CameraViewDirector.Current;
            var root = new GameObject("View transfer");
            var sourceRig = new GameObject("Source rig");
            var sharedRig = new GameObject("Shared rig");
            var lateCamera = new GameObject("Late main camera") { tag = "MainCamera" };
            try
            {
                SceneManager.MoveGameObjectToScene(sourceRig, source);
                SceneManager.MoveGameObjectToScene(sharedRig, shared);
                var camera = lateCamera.AddComponent<Camera>();
                camera.transform.rotation = Quaternion.Euler(12, 50, 0);
                var director = sourceRig.AddComponent<CameraViewDirector>();
                var transfer = root.AddComponent<EscapeSceneTransfer>();
                transfer.SetSource(sourceRig.transform);
                SetCurrent<CameraViewDirector>(null);
                var getView = typeof(EscapeSceneTransfer).GetMethod("TryGetArrivalView", Private);
                Assert.That(getView.Invoke(transfer, null), Is.False);
                SetCurrent(director);
                Assert.That(getView.Invoke(transfer, null), Is.True);
                var view = (Camera)typeof(EscapeSceneTransfer).GetField("sourceCamera", Private).GetValue(transfer);
                var look = (Quaternion)typeof(EscapeSceneTransfer).GetField("localLook", Private).GetValue(transfer);
                Assert.That(Quaternion.Angle(look, Quaternion.Inverse(sourceRig.transform.rotation) * view.transform.rotation), Is.LessThan(.01f));

                var moveCamera = typeof(EscapeSceneTransfer).GetMethod("MoveCamera", Static);
                moveCamera.Invoke(null, new object[] { sourceRig.transform, source, destination });
                moveCamera.Invoke(null, new object[] { sharedRig.transform, source, destination });
                Assert.That(sourceRig.scene, Is.EqualTo(destination));
                Assert.That(sharedRig.scene, Is.EqualTo(shared));
            }
            finally
            {
                SetCurrent(previousDirector);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(lateCamera);
                EditorSceneManager.ClosePreviewScene(source);
                EditorSceneManager.ClosePreviewScene(destination);
                EditorSceneManager.ClosePreviewScene(shared);
            }
        }

        [Test]
        public void InvalidArrivalPoseFallsBackToHostThenToSupportedAssemblySlot()
        {
            var root = new GameObject("Arrival floor fixture");
            root.transform.position = new Vector3(9300, 3100, 7000);
            var area = root.AddComponent<BoxCollider>();
            area.size = new Vector3(8, 3, 6);
            area.center = Vector3.up * 1.5f;
            area.isTrigger = true;
            var floor = new GameObject("Assembly floor");
            floor.transform.SetParent(root.transform, false);
            var support = floor.AddComponent<BoxCollider>();
            support.size = new Vector3(6, .25f, 4);
            support.center = Vector3.down * .125f;
            var resolve = typeof(EscapeSceneTransfer).GetMethod("TryResolveArrivalPose", Static);
            var preferred = new Pose(new Vector3(0, .05f, 1), Quaternion.Euler(0, 30, 0));
            var host = new Pose(new Vector3(0, .05f, -1), Quaternion.Euler(0, 60, 0));
            var unsupported = new Pose(new Vector3(0, .05f, 2.5f), Quaternion.identity);
            (bool supported, Pose pose) Resolve(Pose localPose, Pose hostPose)
            {
                object[] args = { root.scene, area, localPose, hostPose, 2, 4, null };
                bool found = (bool)resolve.Invoke(null, args);
                return (found, (Pose)args[6]);
            }
            try
            {
                Assert.That(Resolve(preferred, host).pose.position, Is.EqualTo(root.transform.TransformPoint(preferred.position)));
                Assert.That(Resolve(unsupported, host).pose.position, Is.EqualTo(root.transform.TransformPoint(host.position)));
                var fallback = Resolve(unsupported, unsupported);
                Assert.That(fallback.supported, Is.True);
                Assert.That((fallback.pose.position - root.transform.TransformPoint(new Vector3(.3f, .1f, 0))).magnitude, Is.LessThan(.001f));
                support.enabled = false;
                Assert.That(Resolve(unsupported, host).supported, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void TraversalRopeKeepsExplorationToolsAvailableDuringTheRun(bool authoredTraversal, bool toolsBlocked)
        {
            var root = new GameObject("Rope tool classification");
            root.SetActive(false);
            var scene = EditorSceneManager.NewPreviewScene();
            SceneManager.MoveGameObjectToScene(root, scene);
            scene.name = "EditorRopeToolClassification";
            var run = root.AddComponent<TightropeRun>();
            var course = root.GetComponent<TightropeCourse>();
            var current = typeof(TightropeRun).GetField("current", Static);
            var previous = current.GetValue(null);
            try
            {
                Set(course, "useSceneGeometry", authoredTraversal);
                Set(run, "course", course);
                typeof(TightropeRun).GetProperty("State").SetValue(run, TightropeRunState.Running);
                current.SetValue(null, run);
                Assert.That(ExplorationTargets.ToolsBlocked, Is.EqualTo(toolsBlocked));
            }
            finally
            {
                current.SetValue(null, previous);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void SpringReboundIsOfferedForDamagingSupportedLandingsOnly(bool highFall, bool safePlatform, bool reboundExpected)
        {
            var root = new GameObject("Spring fall actor");
            root.SetActive(false);
            var floor = new GameObject("Spring test platform");
            var actor = root.AddComponent<NetworkPlayer>();
            var body = root.GetComponent<PlayerMover>();
            var server = typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsServer");
            var feet = new Vector3(9350, 3100, 7000);
            root.transform.position = feet;
            floor.transform.position = feet - Vector3.up * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(4, 1, 4);
            var zone = safePlatform ? floor.AddComponent<HumanCannonLandingZone>() : null;
            var previousGuard = NetworkPlayer.ServerCanChangeState;
            try
            {
                if (safePlatform) Call(zone, "OnEnable");
                Physics.SyncTransforms();
                Assert.That(WalkableSupport.Contains(body, feet), Is.True);
                Set(actor, "mover", body);
                server.SetValue(actor, true);
                NetworkPlayer.ServerCanChangeState = null;
                float height = highFall ? CurtainCall.Settings.GameSettings.Base.BodyDamage.FallInjuryHeight + .1f : .2f;
                bool offered = false;
                bool landed = false;
                bool bounced = actor.CompleteFall(height, feet, () => { offered = true; return true; }, () => landed = true);
                Assert.That(offered && bounced, Is.EqualTo(reboundExpected));
                Assert.That(landed, Is.EqualTo(!reboundExpected));
                Assert.That(actor.State, Is.EqualTo(PlayerState.Normal));
            }
            finally
            {
                NetworkPlayer.ServerCanChangeState = previousGuard;
                server.SetValue(actor, false);
                if (zone != null) Call(zone, "OnDisable");
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(floor);
            }
        }

        [TestCase("Grab")]
        [TestCase("Throw")]
        public void StalledCaptureRetiresTheHeldPlayerAndClearsAttackState(string phase)
        {
            var root = new GameObject("Stalled capture");
            root.SetActive(false);
            var player = root.AddComponent<NetworkPlayer>();
            var chase = root.AddComponent<EscapeUnderstageChase>();
            var spawned = typeof(NetworkPlayer).GetProperty("IsSpawned");
            var server = typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsServer");
            try
            {
                spawned.SetValue(player, true);
                server.SetValue(player, true);
                var playerState = typeof(NetworkPlayer).GetField("state", Private).GetValue(player);
                playerState.GetType().GetProperty("Value").SetValue(playerState, PlayerState.EscapeCaptured);
                var field = typeof(EscapeUnderstageChase).GetField("attack", Private);
                var attack = field.GetValue(chase);
                var attackPhase = attack.GetType().GetField("Phase");
                attackPhase.SetValue(attack, System.Enum.Parse(attackPhase.FieldType, phase));
                attack.GetType().GetField("Started").SetValue(attack, 100d);
                field.SetValue(chase, attack);
                Set(chase, "victim", player);

                Call(chase, "FinishStalledCapture", 100.5d);
                Assert.That(player.State, Is.EqualTo(PlayerState.EscapeCaptured));
                Call(chase, "FinishStalledCapture", 500d);
                Assert.That(player.State, Is.EqualTo(PlayerState.Retired));
                Assert.That(attackPhase.GetValue(field.GetValue(chase)).ToString(), Is.EqualTo("None"));
            }
            finally
            {
                spawned.SetValue(player, false);
                server.SetValue(player, false);
                Object.DestroyImmediate(root);
            }
        }

        static void CheckRegistry<T>(GameObject frame, Component system, string field, int count) where T : Behaviour
        {
            var registered = new SerializedObject(system).FindProperty(field);
            var members = Enumerable.Range(0, registered.arraySize)
                .Select(i => registered.GetArrayElementAtIndex(i).objectReferenceValue as T).ToArray();
            Assert.That(members.Length, Is.EqualTo(count), field);
            Assert.That(members.All(device => device != null && device.enabled && device.gameObject.activeInHierarchy), Is.True, field);
            Assert.That(members.Distinct().Count(), Is.EqualTo(count), field);
            Assert.That(members, Is.EquivalentTo(frame.GetComponentsInChildren<T>()), field);
        }
    }
}
