using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Cameras;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using NUnit.Framework;
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
        static void Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, Private).Invoke(instance, args);
        static void SetCurrent<T>(T instance) => typeof(T).GetProperty("Current").SetValue(null, instance);

        static object CreateChannel(EscapeSceneTransfer transfer)
        {
            var field = typeof(EscapeSceneTransfer).GetField("channel", Private);
            var channel = System.Activator.CreateInstance(field.FieldType, "Escape.EditorTest", null, null, null, null, null);
            field.SetValue(transfer, channel);
            return channel;
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
                CheckRegistry<HumanCannonStation>(frame, frame.GetComponentInChildren<HumanCannonSystem>(true), "stations", 10);
                CheckRegistry<HumanCannonPole>(frame, frame.GetComponentInChildren<HumanCannonPoleSystem>(true), "poles", 36);
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
            }
        }

        [TestCase(CampaignPhase.RestTime)]
        [TestCase(CampaignPhase.Night)]
        public void PendingDepartureSurvivesUntilTheEscapePhaseArrives(CampaignPhase precedingPhase)
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
                var snapshot = typeof(EscapeSceneTransfer).GetField("snapshot", Private).GetValue(transfer);
                snapshot.GetType().GetField("generation").SetValue(snapshot, 5);
                snapshot.GetType().GetField("phase").SetValue(snapshot, 1);

                Set(run, "state", new CampaignSnapshot { Generation = 5, Phase = precedingPhase });
                Call(transfer, "Update");
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
