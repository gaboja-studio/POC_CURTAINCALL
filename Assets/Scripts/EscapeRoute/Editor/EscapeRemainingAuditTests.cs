using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.EditorTools.Tests;
using CurtainCall.UI.EscapeResultsKit;
using NUnit.Framework;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools.Tests
{
    [Category("CommonProgressionRegression"), Category("EscapeRemainingAudit")]
    public sealed class EscapeRemainingAuditTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
        static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
        static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, F).Invoke(o, args);

        [TestCase(true)]
        [TestCase(false)]
        public void DeviceReconciliationAudit_AuthoredEscapeSawOnlyUsesItsSceneFloor(bool sameScene)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Levels/escape_route.unity");
            var support = new GameObject("Floor at the launcher's feet");
            try
            {
                var cutter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeExitSaw>(true))
                    .First(s => s.ProjectileMode);
                if (sameScene) SceneManager.MoveGameObjectToScene(support, scene);
                var floor = support.AddComponent<BoxCollider>();
                Assert.That(typeof(EscapeExitSaw).GetMethod("IsSawSupport", F).Invoke(cutter, new object[] { floor }),
                    Is.EqualTo(sameScene));
            }
            finally
            {
                Object.DestroyImmediate(support);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void OfficialPathAudit_MountedRunnerOutsideArrivalVolumeSharesOnlyALiveCarrierArrival(bool inside, bool expected)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Levels/escape_route.unity");
            var stackRoot = new GameObject("Escape arrival stack");
            stackRoot.SetActive(false);
            var stack = stackRoot.AddComponent<PiggybackSystem>();
            var current = typeof(PiggybackSystem).GetProperty("Current");
            var previous = current.GetValue(null);
            bool suppressed = PiggybackSystem.Suppressed;
            var players = (List<NetworkPlayer>)NetworkPlayer.All;
            var before = players.ToArray();
            players.Clear();
            try
            {
                current.SetValue(null, stack);
                PiggybackSystem.Suppressed = false;
                stackRoot.SetActive(true);
                var content = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<EscapeCampaignContent>(true)).Single();
                var gate = (BoxCollider)Get(content, "arrivalArea");
                Assert.That(gate.gameObject.activeInHierarchy && gate.enabled, Is.True);
                var center = gate.transform.TransformPoint(gate.center);
                var carrier = AddPlayer(200UL, center + (inside ? Vector3.zero : gate.transform.right * gate.size.x));
                var rider = AddPlayer(201UL, center + Vector3.up * (gate.size.y + 1f));
                ((Dictionary<ulong, ulong>)Get(stack, "belowOf")).Add(rider.OwnerClientId, carrier.OwnerClientId);
                Assert.That(EscapeEntrance.Contains(gate, rider.transform.position), Is.False);
                var result = typeof(EscapeCampaignContent).GetMethod("IsAtArrival", F).Invoke(content, new object[] { rider });
                Assert.That(result, Is.EqualTo(expected));

                NetworkPlayer AddPlayer(ulong id, Vector3 position)
                {
                    var node = new GameObject("Stack runner");
                    node.SetActive(false);
                    node.transform.SetParent(stackRoot.transform);
                    node.transform.position = position;
                    var player = node.AddComponent<NetworkPlayer>();
                    typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, id);
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                    players.Add(player);
                    return player;
                }
            }
            finally
            {
                foreach (var player in players)
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                players.Clear();
                players.AddRange(before);
                Object.DestroyImmediate(stackRoot);
                current.SetValue(null, previous);
                PiggybackSystem.Suppressed = suppressed;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void StartCampaignAudit_OfficialBuildUsesTheLobbyAndTheActualPerformanceAndEscapeScenes()
        {
            var enabled = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray();
            Assert.That(enabled[0], Is.EqualTo("Assets/Scenes/InGame/Start.unity"));
            var transition = GameSettings.Base.SpaceTransition;
            string SceneFor(CurtainCall.SpaceLink.SpaceId place, string trick = null)
                => CurtainCall.SpaceLink.SpaceRoute.SceneName(place, transition, trick);
            Assert.That(enabled.Any(path => path.EndsWith("/" + transition.HubScene + ".unity")), Is.True);
            Assert.That(enabled.Any(path => path.EndsWith("/" + transition.WaitingRoomScene + ".unity")), Is.True);
            Assert.That(SceneFor(CurtainCall.SpaceLink.SpaceId.EscapeRoute), Is.EqualTo("escape_route"));
            Assert.That(enabled, Does.Contain("Assets/Scenes/Levels/escape_route.unity"));
            foreach (var trick in new[] { CampaignSnapshot.HumanCannonId, CampaignSnapshot.TightropeId })
                Assert.That(enabled.Any(path => path.EndsWith("/" + SceneFor(CurtainCall.SpaceLink.SpaceId.Performance, trick) + ".unity")), Is.True);
        }

        [TestCase(true, "placed", true)]
        [TestCase(true, "captured", true)]
        [TestCase(false, "placed", false)]
        [TestCase(true, "other", false)]
        public void StartCampaignAudit_PeersCanRecoverMissedEscapeCompletion(bool complete, string kind, bool reply)
        {
            var method = typeof(EscapeSceneTransfer).GetMethod("ShouldReplyWithCompletion", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method.Invoke(null, new object[] { complete, kind }), Is.EqualTo(reply));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void StartCampaignAudit_DisconnectedPeerDoesNotBlockLoadOrSourceUnload(bool connected, bool clear)
        {
            var method = typeof(CurtainCall.SpaceLink.SpaceLinkDirector).GetMethod("RelevantSceneTimeouts",
                BindingFlags.Static | BindingFlags.NonPublic);
            var present = connected ? new[] { 0UL, 8UL } : new[] { 0UL };
            var remaining = (IReadOnlyList<ulong>)method.Invoke(null, new object[] { new[] { 8UL }, present });
            Assert.That(remaining.Count == 0, Is.EqualTo(clear));
        }

        [TestCase(-1, false, false)]
        [TestCase(0, true, false)]
        [TestCase(0, false, true)]
        public void StartCampaignAudit_OnlyAssignedSettledAvatarsCanBePlaced(int slot, bool applying, bool valid)
        {
            var root = new GameObject("Campaign avatar readiness");
            root.SetActive(false);
            try
            {
                var player = root.AddComponent<NetworkPlayer>();
                const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                var variable = Get(player, "slot");
                Set(variable, "m_InternalValue", slot);
                typeof(NetworkPlayer).GetProperty("IsApplyingInitialCondition").SetValue(player, applying);
                var escape = typeof(EscapeSceneTransfer).GetMethod("HasStableAvatar", statics);
                var performance = typeof(CurtainCall.SpaceLink.CampaignPerformanceEntry).GetMethod("CanPlaceAvatar", statics);
                Assert.That(escape.Invoke(null, new object[] { player }), Is.EqualTo(valid));
                Assert.That(performance.Invoke(null, new object[] { player }), Is.EqualTo(valid));
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(3, 1, true)]
        [TestCase(4, 1, false)]
        [TestCase(3, 2, false)]
        public void StartCampaignAudit_ReusedPerformanceSceneRequiresTheCurrentDayAndAttempt(int day, int index, bool current)
        {
            var prior = new CampaignSnapshot
            {
                Phase = CampaignPhase.Performance, Generation = 2, Day = 3, PerformanceIndex = 1,
                TrickId = CampaignSnapshot.HumanCannonId
            };
            var incoming = prior;
            incoming.Day = day; incoming.PerformanceIndex = index;
            const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
            var sameStep = typeof(CurtainCall.SpaceLink.CampaignPerformanceEntry).GetMethod("MatchesPerformanceStep", statics);
            Assert.That(sameStep.Invoke(null, new object[] { prior, incoming }), Is.EqualTo(current));
            var settlement = new CurtainCall.Results.PerformanceIdentity(Guid.NewGuid().ToString("N"), 2, 3, 1, prior.TrickId);
            var sameSettlement = typeof(HumanCannonDestination).GetMethod("MatchesCampaignPerformance", statics);
            Assert.That(sameSettlement.Invoke(null, new object[] { settlement, incoming }), Is.EqualTo(current));
        }

        [Test]
        public void StartCampaignAudit_AnOlderUnassignedAvatarDoesNotHideTheRealParticipant()
        {
            var first = new GameObject("Placeholder network avatar"); first.SetActive(false);
            var second = new GameObject("Final network avatar"); second.SetActive(false);
            var roster = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int initial = roster.Count;
            try
            {
                NetworkPlayer Add(GameObject root, int slot)
                {
                    var player = root.AddComponent<NetworkPlayer>();
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                    typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, 862UL);
                    Set(Get(player, "slot"), "m_InternalValue", slot);
                    roster.Add(player);
                    return player;
                }
                Add(first, -1);
                var stable = Add(second, 0);
                const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
                var escape = typeof(EscapeSceneTransfer).GetMethod("FindSpawnedParticipant", statics);
                var rewards = typeof(CurtainCall.Reward.PerformanceRewards).GetMethod("FindPlayer", statics);
                Assert.That(escape.Invoke(null, new object[] { 862UL }), Is.SameAs(stable));
                Assert.That(rewards.Invoke(null, new object[] { 862UL, true }), Is.SameAs(stable));
            }
            finally
            {
                for (int i = roster.Count - 1; i >= initial; i--)
                {
                    typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(roster[i], false);
                    roster.RemoveAt(i);
                }
                Object.DestroyImmediate(first); Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void StartCampaignAudit_OverlappingSceneUsesThePerformanceSpawnRegistry()
        {
            var actor = new GameObject("Cannon entrant"); actor.SetActive(false);
            var course = new GameObject("Active cannon start points");
            var overlap = new GameObject("Source room still loaded");
            var current = typeof(PlayerSpawnPoints).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = current.GetValue(null);
            try
            {
                var player = actor.AddComponent<NetworkPlayer>();
                Set(Get(player, "slot"), "m_InternalValue", 0);
                var entry = actor.AddComponent<CurtainCall.SpaceLink.CampaignPerformanceEntry>();
                var authored = course.AddComponent<PlayerSpawnPoints>();
                var other = overlap.AddComponent<PlayerSpawnPoints>();
                course.transform.position = new Vector3(210, 27, -10);
                overlap.transform.position = new Vector3(-500, 20, 0);
                Set(authored, "points", new[] { course.transform });
                Set(other, "points", new[] { overlap.transform });
                current.SetValue(null, other);
                Set(entry, "sceneSpawns", authored);
                var args = new object[] { player, actor.GetComponent<PlayerMover>(), Vector3.zero, Quaternion.identity };
                typeof(CurtainCall.SpaceLink.CampaignPerformanceEntry).GetMethod("ResolveStartPose", F).Invoke(entry, args);
                Assert.That(args[2], Is.EqualTo(course.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(actor); Object.DestroyImmediate(overlap); Object.DestroyImmediate(course);
                current.SetValue(null, previous);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StartCampaignAudit_AnInterruptedPlacementRestoresTheOriginalControlFlag(bool wasEnabled)
        {
            var root = new GameObject("Placement with control already owned");
            root.SetActive(false);
            try
            {
                var entry = root.AddComponent<CurtainCall.SpaceLink.CampaignPerformanceEntry>();
                var player = root.AddComponent<NetworkPlayer>();
                var mover = root.GetComponent<PlayerMover>();
                mover.SetControlEnabled(false);
                Set(entry, "mover", mover);
                Set(entry, "placed", player);
                Set(entry, "controlBeforeHold", wasEnabled);
                Set(entry, "controlHeld", true);
                Call(entry, "ReleaseControl");
                Assert.That(mover.ControlEnabled, Is.EqualTo(wasEnabled));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void EndingAvailability_TransitionStopsWaitingWhenItsContentIsGone()
        {
            using var gate = new InputGateTestScope();
            var root = new GameObject("Ending transition fixture");
            var previous = CampaignRun.Instance;
            try
            {
                var runObject = new GameObject("Campaign fixture");
                runObject.SetActive(false);
                runObject.transform.SetParent(root.transform, false);
                var run = runObject.AddComponent<CampaignRun>();
                var campaign = new RunState();
                campaign.DebugStartAt(new RunStartPoint(3, RunStartPhase.Escape, 0, CampaignSnapshot.TightropeId));
                campaign.EndEscape(true);
                Set(run, "state", campaign.Snapshot);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);

                var geometry = new GameObject("Escape route");
                geometry.transform.SetParent(root.transform, false);
                var content = root.AddComponent<EscapeCampaignContent>();
                Set(content, "content", geometry);
                var ending = root.AddComponent<EscapeEndingPresentation>();
                Set(ending, "content", content);
                var transition = ending.HoldBeforeTransition();
                Assert.That(transition.MoveNext(), Is.True);
                geometry.SetActive(false);
                Assert.That(transition.MoveNext(), Is.False);
                Assert.That(InputGate.IsRequested, Is.False);
            }
            finally
            {
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previous);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("day")]
        [TestCase("outcome")]
        [TestCase("early")]
        public void EndingAvailability_OnlyTheCurrentEndingCanFreezeTheGuestView(string mismatch)
        {
            WithEnding((results, run, state) =>
            {
                string packet = ResultPacket(state, true, includePerson: true);
                if (mismatch == "day")
                {
                    var wrongDay = state;
                    wrongDay.Day++;
                    packet = ResultPacket(wrongDay, true, includePerson: true);
                }
                else if (mismatch == "outcome")
                    packet = ResultPacket(state, false, includePerson: true);
                else
                {
                    var playing = state;
                    playing.Phase = CampaignPhase.Escape;
                    Set(run, "state", playing);
                }
                Call(results, "Receive", packet);
                Assert.That(results.HasSnapshot, Is.False);
                Set(run, "state", state);
                Call(results, "Receive", ResultPacket(state, true, includePerson: true));
                Assert.That(results.HasSnapshot, Is.True);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EndingAvailability_EmptyRosterStillOpensTheResultScreen(bool authority)
        {
            WithEnding((results, _, state) =>
            {
                Call(results, "Receive", ResultPacket(state, true, includePerson: false));
                Assert.That(results.HasSnapshot, Is.True);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/EscapeResultsKit/EscapeResultsCanvas.prefab");
                Set(results, "prefab", prefab);
                results.Show(authority);
                Assert.That(results.View, Is.Not.Null);
                Assert.That(results.View.Visible, Is.True);
                Assert.That(results.View.ReturnButton.interactable, Is.EqualTo(authority));
            });
        }

        static string ResultPacket(CampaignSnapshot state, bool success, bool includePerson)
        {
            var type = typeof(EscapeEndingResults).GetNestedType("Snapshot", BindingFlags.NonPublic);
            var packet = Activator.CreateInstance(type);
            type.GetField("generation").SetValue(packet, state.Generation);
            type.GetField("day").SetValue(packet, state.Day);
            type.GetField("success").SetValue(packet, success);
            var people = includePerson ? new[] { new EscapeEndingResults.Person { client = 71, slot = 0, escaped = success } }
                : Array.Empty<EscapeEndingResults.Person>();
            type.GetField("people").SetValue(packet, people);
            return JsonUtility.ToJson(packet);
        }

        static void WithEnding(Action<EscapeEndingResults, CampaignRun, CampaignSnapshot> check)
        {
            var root = new GameObject("Results state fixture");
            root.SetActive(false);
            var previous = CampaignRun.Instance;
            try
            {
                var campaign = new RunState();
                campaign.DebugStartAt(new RunStartPoint(3, RunStartPhase.Escape, 0, CampaignSnapshot.TightropeId));
                campaign.EndEscape(true);
                var run = root.AddComponent<CampaignRun>();
                Set(run, "state", campaign.Snapshot);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
                check(root.AddComponent<EscapeEndingResults>(), run, campaign.Snapshot);
            }
            finally
            {
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previous);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EndingHandoff_ReleasesItsInputAndCameraBeforeSceneUnload(bool disconnect)
        {
            using var gate = new InputGateTestScope();
            var previousRun = CampaignRun.Instance;
            var root = new GameObject("Loaded escape scene");
            root.SetActive(false);
            var geometry = new GameObject("Escape content");
            geometry.transform.SetParent(root.transform, false);
            try
            {
                var host = new RunState();
                host.DebugStartAt(new RunStartPoint(3, RunStartPhase.Escape, 0, CampaignSnapshot.TightropeId));
                Assert.That(host.EndEscape(true), Is.True);
                var run = root.AddComponent<CampaignRun>();
                Set(run, "host", host);
                Set(run, "state", host.Snapshot);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);

                var content = root.AddComponent<EscapeCampaignContent>();
                Set(content, "content", geometry);
                var presentation = root.AddComponent<EscapeEndingPresentation>();
                Set(presentation, "content", content);
                var camera = root.AddComponent<CinemachineCamera>();
                Set(presentation, "endingCamera", camera);
                Call(presentation, "ResetPresentation");
                Call(presentation, "LateUpdate");
                Assert.That(presentation.IsPresenting, Is.True);
                Assert.That(InputGate.IsRequested, Is.True);
                Assert.That(camera.enabled, Is.True);

                Set(presentation, "returnRequested", true);
                Call(presentation, "LateUpdate");
                Assert.That(InputGate.IsRequested, Is.True, "The ending still owns input until the handoff begins.");

                if (disconnect)
                {
                    Call(run, "HandleConnection", SessionConnectionState.Offline);
                    Assert.That(run.State.Phase, Is.EqualTo(CampaignPhase.None));
                }
                else
                    geometry.SetActive(false);

                Call(presentation, "LateUpdate");
                Assert.That(presentation.IsPresenting, Is.False);
                Assert.That(InputGate.IsRequested, Is.False);
                Assert.That(camera.enabled, Is.False);
            }
            finally
            {
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previousRun);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SavedMovingRailsClearBothBodiesAndReleaseOverSupportedLanding()
        {
            const string path = "Assets/Scenes/Levels/escape_route.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var loops = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonPoleLoop>())
                .Where(l => l.isActiveAndEnabled).ToArray();
            var saved = loops.Select(l => (l.Carriage.position, l.Carriage.rotation)).ToArray();
            try
            {
                Assert.That(loops.Length, Is.EqualTo(6));
                var poles = loops.Select(l => l.GetComponentInChildren<HumanCannonPole>()).ToArray();
                double period = loops.Max(l => l.Length / (double)(float)Get(l, "metersPerSecond"));
                var character = GameSettings.Base.Character;
                var heights = new[] { character.CapsuleHeight, GameSettings.Base.BodyDamage.CrawlCapsuleHeight };
                int samples = 0;
                for (double time = 0; time < period; time += .1)
                {
                    foreach (var loop in loops)
                    {
                        loop.EvaluatePose(time, out var p, out var r, out _, out _);
                        loop.Carriage.SetPositionAndRotation(p, r);
                    }
                    Physics.SyncTransforms();
                    foreach (var pole in poles)
                        for (int seat = 0; seat < pole.RiderCapacity; seat++)
                            foreach (float height in heights)
                                for (float angle = -GameSettings.Poles.MaxSwingAngle; angle <= GameSettings.Poles.MaxSwingAngle; angle += 15)
                                {
                                    samples++;
                                    var feet = pole.HangingPosition(angle, seat);
                                    float radius = Mathf.Min(character.CapsuleRadius, height * .5f);
                                    var hit = Physics.OverlapCapsule(feet + Vector3.up * (radius + .01f),
                                        feet + Vector3.up * (height - radius + .01f), radius, Physics.DefaultRaycastLayers,
                                        QueryTriggerInteraction.Ignore).FirstOrDefault(c => c.gameObject.scene == scene && !c.transform.IsChildOf(pole.transform));
                                    if (hit != null) Assert.Fail($"{pole.name}: time {time:F2}, seat {seat}, height {height}, angle {angle} clips {hit.name}");
                                }
                }
                foreach (var pole in poles)
                {
                    Assert.That(pole.Loop.TryGetRecycleBoundary(0, period + 1, out var terminal), Is.True);
                    pole.Loop.EvaluatePose(terminal, out var p, out var r, out _, out _);
                    pole.Loop.Carriage.SetPositionAndRotation(p, r);
                    Physics.SyncTransforms();
                    for (int seat = 0; seat < pole.RiderCapacity; seat++)
                        foreach (float angle in new[] { -GameSettings.Poles.MaxSwingAngle, 0, GameSettings.Poles.MaxSwingAngle })
                        {
                            var feet = pole.HangingPosition(angle, seat);
                            var support = Physics.RaycastAll(feet + Vector3.up * .02f, Vector3.down,
                                GameSettings.Base.BodyDamage.FallInjuryHeight, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                .Where(h => h.collider.gameObject.scene == scene && !h.collider.transform.IsChildOf(pole.transform) && h.normal.y > .9f)
                                .OrderBy(h => h.distance).ToArray();
                            Assert.That(support, Is.Not.Empty, $"{pole.name}, seat {seat}, angle {angle}: no safe terminal landing");
                        }
                }
                TestContext.WriteLine($"Moving passenger capsule samples: {samples}; terminal support samples: 36.");
            }
            finally
            {
                for (int i = 0; i < loops.Length; i++) loops[i].Carriage.SetPositionAndRotation(saved[i].position, saved[i].rotation);
                Physics.SyncTransforms();
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SavedEndingReferencesSupportFourRowsAndLobbyReturn()
        {
            const string path = "Assets/Scenes/Levels/escape_route.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeEndingPresentation>(true)).Single();
                foreach (string field in new[] { "content", "exitSaw", "arrivalArea", "endingCamera" })
                    Assert.That((Object)Get(owner, field), Is.Not.Null, field);
                var results = owner.GetComponent<EscapeEndingResults>();
                Assert.That(results, Is.Not.Null);
                var prefab = (GameObject)Get(results, "prefab");
                Assert.That(prefab, Is.Not.Null);
                var view = prefab.GetComponentInChildren<EscapeResultsView>(true);
                Assert.That(view, Is.Not.Null);
                foreach (Object reference in new Object[] { view.Group, view.PlayersContent, view.StatisticsContent,
                    view.PlayersTab, view.StatisticsTab, view.ReturnButton, view.ReturnLabel, view.Summary })
                    Assert.That(reference, Is.Not.Null);
                Assert.That(view.PlayerRows.Length, Is.GreaterThanOrEqualTo(4));
                foreach (var row in view.PlayerRows.Take(4))
                    foreach (Object reference in new Object[] { row.Root, row.Name, row.Outcome, row.OutcomeLabel })
                        Assert.That(reference, Is.Not.Null);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EndingPacketRemainsUsableWhileAnAvatarHasNoSlot(bool disconnected)
        {
            var root = new GameObject("Ending while the crew changes"); root.SetActive(false);
            var previous = CampaignRun.Instance;
            var all = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var saved = all.ToArray(); all.Clear();
            var actors = new List<NetworkPlayer>();
            try
            {
                var host = new RunState();
                host.DebugStartAt(new RunStartPoint(3, RunStartPhase.Escape, 0, CampaignSnapshot.TightropeId));
                host.EndEscape(true);
                var run = root.AddComponent<CampaignRun>(); Set(run, "host", host); Set(run, "state", host.Snapshot);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);

                var results = root.AddComponent<EscapeEndingResults>(); Call(results, "Awake");
                Get(results, "channel").GetType().GetProperty("Session").SetValue(Get(results, "channel"), root.AddComponent<NetworkSessionManager>());
                MakeAvatar(720, 0, PlayerState.Arrived);
                MakeAvatar(721, disconnected ? NetworkPlayer.NoSlot : 1, disconnected ? PlayerState.Normal : PlayerState.Retired);
                MakeAvatar(799, NetworkPlayer.NoSlot, PlayerState.Normal);
                var observed = (Dictionary<ulong, EscapeEndingResults.Person>)Get(results, "observed");
                observed[721] = new EscapeEndingResults.Person { client = 721, slot = 1, items = new[] { "saved-item" } };
                results.Freeze();

                var guestRoot = new GameObject("Guest ending receiver"); guestRoot.transform.SetParent(root.transform, false);
                var guest = guestRoot.AddComponent<EscapeEndingResults>();
                string packet = JsonUtility.ToJson(Get(results, "snapshot"));
                string stale = packet.Replace("\"generation\":" + run.State.Generation, "\"generation\":" + (run.State.Generation - 1));
                Call(guest, "Receive", stale);
                Assert.That(guest.HasSnapshot, Is.False);
                var retry = typeof(EscapeEndingResults).GetMethod("ShouldRequestEndingSnapshot", F);
                Set(guest, "nextEndingSync", 100.5d);
                Assert.That(retry.Invoke(guest, new object[] { run.State, true, 100.49d }), Is.False);
                Assert.That(retry.Invoke(guest, new object[] { run.State, true, 100.5d }), Is.True);
                Assert.That(retry.Invoke(guest, new object[] { run.State, false, 100.5d }), Is.False);
                Call(guest, "Receive", packet);
                Assert.That(guest.HasSnapshot, Is.True, "A changing crew must not cause the guest to reject the whole ending.");
                Assert.That(retry.Invoke(guest, new object[] { run.State, true, 101d }), Is.False);
                Assert.That(guest.People.Select(person => person.client), Is.EqualTo(new[] { 720UL, 721UL }));
                Assert.That(guest.People[1].items, Is.EqualTo(new[] { "saved-item" }));
                Assert.That(guest.Arrivals, Is.EquivalentTo(new[] { 720UL }));
                Set(guest, "prefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/EscapeResultsKit/EscapeResultsCanvas.prefab"));
                guest.Show(false);
                Assert.That(guest.View, Is.Not.Null, "The validated packet must let the guest open the results view.");
            }
            finally
            {
                foreach (var actor in actors) typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, false);
                all.Clear(); all.AddRange(saved);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previous);
                Object.DestroyImmediate(root);
            }

            void MakeAvatar(ulong id, int slot, PlayerState state)
            {
                var node = new GameObject("Ending avatar " + id); node.transform.SetParent(root.transform, false);
                var actor = node.AddComponent<NetworkPlayer>();
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(actor, true);
                typeof(NetworkBehaviour).GetProperty("OwnerClientId").SetValue(actor, id);
                ((NetworkVariable<int>)Get(actor, "slot")).Value = slot;
                ((NetworkVariable<PlayerState>)Get(actor, "state")).Value = state;
                all.Add(actor); actors.Add(actor);
            }
        }

        [TestCase(3), TestCase(4)]
        public void EndingRosterRetainsArrivalWhenTheActorDisconnectsBeforeTheResultsObserveIt(int crew)
        {
            var root = new GameObject("Ending disconnect ordering"); root.SetActive(false);
            var previous = CampaignRun.Instance;
            var all = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var saved = all.ToArray(); all.Clear();
            try
            {
                var host = new RunState(); host.DebugStartAt(new RunStartPoint(3, RunStartPhase.Escape, 0, CampaignSnapshot.TightropeId));
                host.EndEscape(true);
                var run = root.AddComponent<CampaignRun>(); Set(run, "host", host); Set(run, "state", host.Snapshot);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, run);
                var content = root.AddComponent<EscapeCampaignContent>();
                ((HashSet<ulong>)Get(content, "arrived")).Add(700);
                var owner = root.AddComponent<EscapeEndingPresentation>(); Set(owner, "content", content);
                var results = root.AddComponent<EscapeEndingResults>(); Call(results, "Awake");
                var session = root.AddComponent<NetworkSessionManager>();
                Get(results, "channel").GetType().GetProperty("Session").SetValue(Get(results, "channel"), session);
                var observed = (Dictionary<ulong, EscapeEndingResults.Person>)Get(results, "observed");
                for (int i = 0; i < crew; i++) observed[(ulong)(700 + i)] = new EscapeEndingResults.Person { client = (ulong)(700 + i), slot = i, escaped = false };
                results.Freeze();
                Assert.That(results.People.Count, Is.EqualTo(crew));
                Assert.That(results.People.Single(p => p.client == 700).escaped, Is.True,
                    "The authoritative exit-volume arrival must survive a disconnect before the result observer's next Update.");
                Assert.That(results.Arrivals, Does.Contain(700UL));
                var clientRoot = new GameObject("Independent ending receiver"); clientRoot.SetActive(false); clientRoot.transform.SetParent(root.transform);
                var client = clientRoot.AddComponent<EscapeEndingResults>();
                string packet = JsonUtility.ToJson(Get(results, "snapshot"));
                string stale = packet.Replace("\"generation\":" + run.State.Generation, "\"generation\":" + (run.State.Generation + 1));
                Call(client, "Receive", stale); Assert.That(client.HasSnapshot, Is.False);
                Call(client, "Receive", packet);
                Assert.That(client.Arrivals, Does.Contain(700UL));
                Call(client, "Receive", packet.Replace("\"escaped\":true", "\"escaped\":false"));
                Assert.That(client.Arrivals, Does.Contain(700UL), "Repeated sync cannot rewrite a frozen ending.");
                Call(run, "HandleConnection", SessionConnectionState.Offline);
                Assert.That(run.State.Phase, Is.EqualTo(CampaignPhase.None), "Host loss must clear the ending's campaign hold.");
            }
            finally
            {
                all.Clear(); all.AddRange(saved);
                typeof(CampaignRun).GetProperty("Instance").SetValue(null, previous);
                Object.DestroyImmediate(root);
            }
        }
    }
}
