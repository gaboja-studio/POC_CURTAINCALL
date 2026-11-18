using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("November11CommonFix")]
    public sealed partial class ExitSawTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const string SawType = "CurtainCall.EscapeRoute.EscapeExitSaw";
        const string PlayerType = "CurtainCall.Network.PlayerSync.NetworkPlayer";
        static readonly Vector3 Origin = new(14700, 40, 14700);
        readonly List<GameObject> roots = new();
        readonly List<(Type type, string name, object value)> saved = new();
        IList players, devices;
        object[] previousPlayers, previousDevices;
        Component saw;
        Transform device, blade, cable, door;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target) ?? target.GetType().GetField(name, Flags)?.GetValue(target);
        static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Flags);
            if (property != null) property.SetValue(target, value); else target.GetType().GetField(name, Flags).SetValue(target, value);
        }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        void Isolate(string typeName, string name = "Current")
        {
            var type = T(typeName); var property = type.GetProperty(name, Static);
            saved.Add((type, name, property.GetValue(null))); property.SetValue(null, null);
        }
        GameObject Root(string name, bool active = false)
        {
            var root = new GameObject(name) { hideFlags = HideFlags.DontSave };
            root.SetActive(active); root.transform.position = Origin; roots.Add(root); return root;
        }
        Component Player(ulong id)
        {
            var root = Root("Saw actor " + id);
            var player = root.AddComponent(T(PlayerType));
            Call(root.GetComponent(T("CurtainCall.Player.PlayerMover")), "Awake");
            Set(player, "IsSpawned", true); Set(player, "OwnerClientId", id); root.SetActive(true); players.Add(player); return player;
        }
        Component Host()
        {
            var host = Root("Saw offline session").AddComponent(T("CurtainCall.Network.Session.NetworkSessionManager"));
            Set(host, "ConnectionState", Enum.Parse(T("CurtainCall.Network.Session.SessionConnectionState"), "Host"));
            Set(host, "GameState", Enum.Parse(T("CurtainCall.Network.Session.GameSessionState"), "Playing")); return host;
        }
        void Fixture(bool ready = false)
        {
            saw = Root("Saw controller").AddComponent(T(SawType));
            device = Root("Movable saw", true).transform; device.position += Vector3.forward * 1.1f;
            var handle = new GameObject("Handle").transform; handle.SetParent(device, false); handle.localPosition = new Vector3(0, 1.05f, -1.1f);
            blade = new GameObject("Blade").transform; blade.SetParent(device, false); blade.localPosition = new Vector3(0, 1.45f, .65f);
            cable = Root("Fixed cable", true).transform; cable.position = blade.position;
            door = Root("Physical door", true).transform; door.position += new Vector3(5, 4, 0); door.gameObject.AddComponent<BoxCollider>();
            Set(saw, "carryRoot", device); Set(saw, "handle", handle); Set(saw, "blade", blade); Set(saw, "cableSocket", cable); Set(saw, "doorPanel", door);
            Call(saw, "InitializePose");
            if (!ready) return;
            var floor = Root("Actual support", true).AddComponent<BoxCollider>(); floor.center = new Vector3(0, -.25f, 0); floor.size = new Vector3(10, .5f, 10);
            var cannon = Root("Original cannon input").AddComponent(T("CurtainCall.HumanCannon.HumanCannonSystem")); Call(cannon, "ResetSeats");
            cannon.GetType().GetProperty("Current", Static).SetValue(null, cannon);
            saw.gameObject.SetActive(true); Set(saw, "session", Host()); Set(saw, "synchronized", true);
            T("CurtainCall.HumanCannon.HumanCannonCarry").GetMethod("Register", Static).Invoke(null, new object[] { saw });
            T(SawType).GetProperty("Current", Static).SetValue(null, saw); Physics.SyncTransforms();
        }
        void Own(ulong actor)
        {
            var ownership = Get(saw, "ownership");
            Assert.That(Call(ownership, "TryApply", Enum.Parse(T("CurtainCall.HumanCannon.CannonAction"), "Board"), actor, (uint)Get(ownership, "Revision")), Is.True);
        }
        [Test]
        public void HingedExitOpensBothLeavesOutwardAndClosesOnResetWithoutLiftingTheDoor()
        {
            Fixture();
            var left = Root("Left exit hinge", true).transform; left.SetParent(door, false);
            var right = Root("Right exit hinge", true).transform; right.SetParent(door, false);
            Set(saw, "leftDoorHinge", left); Set(saw, "rightDoorHinge", right);
            Set(saw, "initialized", false); Call(saw, "InitializePose");
            var original = door.position;
            var settings = T("CurtainCall.Settings.GameSettings").GetProperty("EscapeRoute", Static).GetValue(null);
            float angle = (float)Get(settings, "ExitDoorOpenAngle"), seconds = (float)Get(settings, "ExitDoorOpenSeconds");
            Set(saw, "cutProgress", Get(saw, "cutSeconds"));
            Call(saw, "Present", seconds * .5f, false);
            Assert.That(Quaternion.Angle(left.localRotation, Quaternion.Euler(0, -angle * .5f, 0)), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(right.localRotation, Quaternion.Euler(0, angle * .5f, 0)), Is.LessThan(.01f));
            Call(saw, "Present", seconds, false);
            Assert.That(Quaternion.Angle(left.localRotation, Quaternion.Euler(0, -angle, 0)), Is.LessThan(.01f));
            Assert.That(door.position, Is.EqualTo(original));
            Set(saw, "cutProgress", 0f); Call(saw, "Present", 0f, true);
            Assert.That(Quaternion.Angle(left.localRotation, Quaternion.identity), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(right.localRotation, Quaternion.identity), Is.LessThan(.01f));
            Set(saw, "cutProgress", Get(saw, "cutSeconds")); Call(saw, "Present", 0f, true);
            Assert.That(Quaternion.Angle(right.localRotation, Quaternion.Euler(0, angle, 0)), Is.LessThan(.01f));
        }

        bool Snapshot(uint version, uint own, ulong actor, float progress, byte active = 0, int token = 0)
            => (bool)Call(saw, "ApplyState", version, own, actor, Origin + Vector3.forward * 1.1f, Quaternion.identity, progress, active, token);
        [Test]
        public void ExitObjectiveVisitSurvivesJoinAndLaterSnapshots()
        {
            Fixture();
            Assert.IsTrue(Snapshot(4,0,ulong.MaxValue,0,0x80));
            Assert.AreEqual(true,Get(saw,"TeamReachedExitZone"));
            Assert.IsFalse(Snapshot(5,0,ulong.MaxValue,0));
            Assert.IsTrue(Snapshot(5,0,ulong.MaxValue,3,0x80));
            Assert.AreEqual(true,Get(saw,"DoorOpen"));
        }
        void CutFor(float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) Call(saw, "TickCut", .05f, true); }

        [SetUp]
        public void SetUp()
        {
            Isolate(SawType); Isolate("CurtainCall.HumanCannon.HumanCannonSystem"); Isolate("CurtainCall.HumanCannon.HumanCannonTrampoline");
            Isolate("CurtainCall.HumanCannon.HumanCannonPoleSystem"); Isolate("CurtainCall.Tightrope.PiggybackSystem");
            players = (IList)T(PlayerType).GetField("all", Static).GetValue(null); previousPlayers = players.Cast<object>().ToArray(); players.Clear();
            devices = (IList)T("CurtainCall.HumanCannon.HumanCannonCarry").GetField("devices", Static).GetValue(null);
            previousDevices = devices.Cast<object>().ToArray(); devices.Clear();
        }
        [TearDown]
        public void TearDown()
        {
            if (saw != null) Call(saw, "OnDisable");
            foreach (var player in players.Cast<object>().ToArray()) Set(player, "IsSpawned", false);
            players.Clear(); devices.Clear();
            for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]); roots.Clear();
            foreach (var item in saved) item.type.GetProperty(item.name, Static).SetValue(null, item.value); saved.Clear();
            foreach (var player in previousPlayers) players.Add(player); foreach (var load in previousDevices) devices.Add(load);
            saw = null; Physics.SyncTransforms();
        }

        [Test]
        public void SawContactUsesThinDiskRatherThanSphericalProximity()
        {
            var method = T(SawType).GetMethod("BladeTouches", Static);
            bool Touch(Vector3 point) => (bool)method.Invoke(null, new object[] { Vector3.zero, Vector3.forward, .5f, .1f, point, .05f });
            Assert.That(Touch(new Vector3(.3f, .3f, .1f)), Is.True);
            Assert.That(Touch(new Vector3(0, 0, .3f)), Is.False, "날 평면 뒤의 가까운 전선은 접촉이 아니다.");
            Assert.That(Touch(new Vector3(.6f, 0, 0)), Is.False);
            Assert.That(Touch(new Vector3(float.NaN, 0, 0)), Is.False);
        }

        [Test, Category("EscapeSawCameraRegression"), Category("CommonProgressionRegression")]
        public void SpectatorIgnoresWatchedCarriersSawButStillCollidesWithWallsAndOtherLoads()
        {
            Fixture(true); var spectator = Player(41); var carrier = Player(42); Own(42);
            var load = device.gameObject.AddComponent<BoxCollider>();
            var wall = Root("Solid wall", true).AddComponent<BoxCollider>();
            var localProperty = T(PlayerType).GetProperty("Local", Static);
            var targetProperty = T("CurtainCall.Cameras.CameraSpectator").GetProperty("Target", Static);
            var spectatingProperty = T("CurtainCall.Cameras.CameraSpectator").GetProperty("IsSpectating", Static);
            var previousLocal = localProperty.GetValue(null); var previousTarget = targetProperty.GetValue(null);
            var previousSpectating = spectatingProperty.GetValue(null);
            var blocks = T("CurtainCall.Cameras.CameraViewDirector").GetMethod("BlocksCamera", Static);
            bool Blocks(Collider shape) => (bool)blocks.Invoke(null, new object[] { shape });
            try
            {
                localProperty.SetValue(null, spectator); targetProperty.SetValue(null, carrier); spectatingProperty.SetValue(null, true);
                Assert.That(Blocks(load), Is.False, "관전 대상이 든 톱이 관전 카메라를 압축하면 안 된다.");
                Assert.That(Blocks(wall), Is.True);
                targetProperty.SetValue(null, spectator);
                Assert.That(Blocks(load), Is.True, "다른 사람이 든 기물까지 모두 무시하지 않는다.");
                spectatingProperty.SetValue(null, false); localProperty.SetValue(null, carrier);
                Assert.That(Blocks(load), Is.False, "본인 운반 시 기존 제외 유지");
            }
            finally
            {
                localProperty.SetValue(null, previousLocal); targetProperty.SetValue(null, previousTarget);
                spectatingProperty.SetValue(null, previousSpectating);
            }
        }
        [Test]
        public void RotatedBladeContactFollowsItsWorldAxis()
        {
            var method = T(SawType).GetMethod("BladeTouches", Static);
            Assert.That(method.Invoke(null, new object[] { Origin, Vector3.right, .5f, .1f, Origin + new Vector3(.1f, .3f, .3f), .05f }), Is.True);
            Assert.That(method.Invoke(null, new object[] { Origin, Vector3.right, .5f, .1f, Origin + new Vector3(.3f, 0, 0), .05f }), Is.False);
        }
        [Test]
        public void CuttingRequiresContinuousContactAndCannotCompleteFromOneLargeFrame()
        {
            Fixture(); Call(saw, "TickCut", 90f, true); Assert.That((float)Get(saw, "cutProgress"), Is.EqualTo(.1f).Within(.001));
            CutFor(1); Call(saw, "TickCut", .05f, false); Assert.That(Get(saw, "CutProgress01"), Is.EqualTo(0f));
            CutFor(2.9f); Assert.That(Get(saw, "DoorOpen"), Is.False); CutFor(.2f); Assert.That(Get(saw, "DoorOpen"), Is.True);
            Call(saw, "TickCut", .05f, false); Assert.That(Get(saw, "DoorOpen"), Is.True, "절단 완료 후에는 날을 떼어도 문이 열린다.");
        }
        [Test]
        public void UncarriedBladeAtCableCannotCut()
        {
            Fixture(true); Call(saw, "HostStep", .1f);
            Assert.That(Get(saw, "CutProgress01"), Is.EqualTo(0f)); Assert.That(Get(saw, "IsCutting"), Is.False);
        }
        [Test]
        public void GroundedCarrierCutsButAirborneAndMissingSupportResetProgress()
        {
            Fixture(true); var player = Player(41); Own(41); Physics.SyncTransforms();
            Call(saw, "HostStep", .1f); Assert.That((float)Get(saw, "CutProgress01"), Is.GreaterThan(0));
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover")); Set(mover, "IsAirborne", true);
            Call(saw, "HostStep", .1f); Assert.That(Get(saw, "CutProgress01"), Is.EqualTo(0f));
            Set(mover, "IsAirborne", false); player.transform.position += Vector3.up * 2; Physics.SyncTransforms();
            Call(saw, "HostStep", .1f); Assert.That(Get(saw, "CutProgress01"), Is.EqualTo(0f));
        }
        [Test]
        public void OnlyCarrierCanDropAndOldRequestCannotDropNewOwnership()
        {
            Fixture(true); Player(41); Player(42); Own(41); Physics.SyncTransforms();
            Call(saw, "HandleAction", 42UL, (byte)2, 1U, true); Assert.That(Get(saw, "Carrier"), Is.EqualTo(41UL));
            Call(saw, "HandleAction", 41UL, (byte)2, 0U, true); Assert.That(Get(saw, "Carrier"), Is.EqualTo(41UL));
            Call(saw, "HandleAction", 41UL, (byte)2, 1U, true); Assert.That(Get(saw, "Carrier"), Is.EqualTo(ulong.MaxValue));
        }
        [Test]
        public void MissingCarrierAndInspectionRecoverWithoutOpeningDoor()
        {
            Fixture(true); Own(41); device.position += Vector3.forward * 3;
            Call(saw, "HostStep", .1f); Assert.That(Get(saw, "Carrier"), Is.EqualTo(ulong.MaxValue));
            Assert.That(device.position, Is.EqualTo(Origin + Vector3.forward * 1.1f)); Assert.That(Get(saw, "DoorOpen"), Is.False);
            Player(42); Own(42); device.position += Vector3.right * 2; Call(saw, "ServerReleaseForInspection", 42UL);
            Assert.That(Get(saw, "Carrier"), Is.EqualTo(ulong.MaxValue)); Assert.That(device.position, Is.EqualTo(Origin + Vector3.forward * 1.1f));
        }
        [Test]
        [Category("FinalEscape164")]
        public void ClientStillActiveAcceptsNewSnapshotAfterHostTemporarilyDisablesLauncher()
        {
            Fixture(true);
            var receiver = Root("Client replica").AddComponent(T(SawType));
            var network = Root("Retained network manager").AddComponent(T("Unity.Netcode.NetworkManager"));
            var transport = Activator.CreateInstance(T("Unity.Netcode.CustomMessagingManager"), Flags, null,
                new object[] { network }, null);
            Set(saw, "messaging", transport);
            Set(saw, "stateRevision", 12u);
            Set(saw, "courseEpoch", 4u);
            Own(42);
            uint receivedRevision = (uint)Get(Get(saw, "ownership"), "Revision");
            Vector3 point = device.position;
            var emptyShots = Array.CreateInstance(saw.GetType().GetNestedType("Shot", BindingFlags.NonPublic), 0);
            bool Apply(uint revision, uint order, ulong carrier, uint course)
                => (bool)Call(receiver, "ApplySnapshot", revision, order, carrier, point, Quaternion.identity,
                    0f, (byte)0, 0, 0u, new Vector3[32], emptyShots, 0f, course);
            Assert.IsTrue(Apply(12u, receivedRevision, 42UL, 4u));
            Call(saw, "OnDisable");
            Assert.AreSame(transport, Get(saw, "suspendedHostMessaging"));
            Assert.IsTrue(Apply((uint)Get(saw, "stateRevision"), (uint)Get(Get(saw, "ownership"), "Revision"),
                (ulong)Get(saw, "Carrier"), (uint)Get(saw, "courseEpoch")));
            Assert.AreEqual(ulong.MaxValue, Get(receiver, "Carrier"));
            Assert.IsFalse(Apply(12u, receivedRevision, 42UL, 4u), "A late snapshot cannot restore the old carrier after activation.");
        }

        [Test]
        public void LateJoinRestoresCutDoorAndRejectsOlderOrConflictingSnapshots()
        {
            Fixture(); Assert.That(Snapshot(10, 2, ulong.MaxValue, 3), Is.True); Assert.That(Get(saw, "DoorOpen"), Is.True);
            Assert.That(door.position.y, Is.EqualTo(Origin.y + 4 + 8.5f));
            Assert.That(Snapshot(9, 1, 41, 0), Is.False); Assert.That(Snapshot(10, 2, ulong.MaxValue, 0), Is.False);
            Assert.That(Snapshot(11, 2, 41, 3), Is.False, "동일한 소유권 revision의 소유자를 바꿀 수 없다.");
        }
        [Test]
        public void RestartBarrierIgnoresDelayedCutAndInvalidReplyCannotConsumeToken()
        {
            Fixture(); Snapshot(10, 2, ulong.MaxValue, 3);
            int token = (int)Call(Get(saw, "resetBarrier"), "Begin"); Set(saw, "synchronized", false);
            Assert.That(Snapshot(11, 3, ulong.MaxValue, 3), Is.False);
            Assert.That(Snapshot(11, 3, ulong.MaxValue, float.NaN, token: token), Is.False);
            Assert.That(Get(Get(saw, "resetBarrier"), "Waiting"), Is.True);
            Assert.That(Snapshot(11, 3, ulong.MaxValue, 0, token: token), Is.True);
            Assert.That(Get(saw, "DoorOpen"), Is.False);
        }
        [Test]
        public void DoorRemainsPhysicalWhileLiftingAndResetReturnsItToClosedPose()
        {
            Fixture(); Set(saw, "cutProgress", 3f);
            Vector3 closed = door.position; Call(saw, "Present", .1f, false);
            Assert.That(door.position.y, Is.EqualTo(closed.y + .5f).Within(.001)); Assert.That(door.GetComponent<BoxCollider>().enabled, Is.True);
            Set(saw, "cutProgress", 0f); Call(saw, "Present", 0f, true); Assert.That(door.position, Is.EqualTo(closed));
        }
        [TestCase(1UL), TestCase(2UL), TestCase(3UL)]
        public void HostGrantsLauncherToLandedGuestWhileJumpSnapshotIsStillAirborne(ulong guestId)
        {
            Fixture(true);
            Set(saw, "fireWithoutCables", true);
            var guest = Player(guestId);
            Set(guest, "IsOwner", false);
            var mover = guest.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Set(mover, "Simulated", false);
            Set(Get(guest, "jump"), "Value", Enum.Parse(T("CurtainCall.Player.JumpKind"), "Fall"));
            var load = device.gameObject.AddComponent<BoxCollider>();
            load.center = Vector3.up * .8f;
            load.size = new Vector3(.5f, 1.2f, .5f);
            Set(saw, "loadCollider", load);

            guest.transform.position = Origin + Vector3.up * .4f;
            Physics.SyncTransforms();
            Call(saw, "HandleAction", guestId, (byte)1, 0u, false);
            Assert.That(Get(saw, "Carrier"), Is.EqualTo(ulong.MaxValue),
                "Away from the floor, the same request must be rejected.");

            guest.transform.position = Origin;
            Physics.SyncTransforms();
            Assert.That(Get(guest, "IsAirborne"), Is.True);
            Assert.That(Call(saw, "Grounded", guest, true), Is.False, "The prior gate rejects this landed pose.");
            Call(saw, "HandleAction", guestId, (byte)1, 0u, false);
            Assert.That(Get(saw, "Carrier"), Is.EqualTo(guestId),
                "A late jump snapshot must not veto a guest whose feet already touch the floor.");
        }

        [TestCase(false, false), TestCase(true, false), TestCase(true, true)]
        public void PlatformInputKeepsThePromptedLauncherUntilKeyUp(bool anotherDeviceMovesCloser, bool targetUnavailable)
        {
            Fixture(true);
            Set(saw, "fireWithoutCables", true);
            Isolate(PlayerType, "Local");
            var actor = Player(2);
            Set(actor, "mover", actor.GetComponent(T("CurtainCall.Player.PlayerMover")));
            Set(actor, "IsOwner", true);
            T(PlayerType).GetProperty("Local", Static).SetValue(null, actor);
            var pilot = actor.gameObject.AddComponent(T("CurtainCall.HumanCannon.HumanCannonPilot"));
            Set(pilot, "system", T("CurtainCall.HumanCannon.HumanCannonSystem").GetProperty("Current", Static).GetValue(null));
            Set(pilot, "player", actor);
            var scheme = ScriptableObject.CreateInstance(T("CurtainCall.Player.PlatformControlScheme"));
            try
            {
                var context = Activator.CreateInstance(T("CurtainCall.Player.PlayerControlContext"), new object[] { actor.gameObject });
                var frame = Activator.CreateInstance(T("CurtainCall.Player.PlayerInputFrame"));
                var reader = actor.GetComponent(T("CurtainCall.Player.PlayerInputReader"))
                    ?? actor.gameObject.AddComponent(T("CurtainCall.Player.PlayerInputReader"));
                var select = T("CurtainCall.Player.CommonInteractionSelection").GetMethod("Selected", Static);
                Set(frame, "InteractHeld", true);
                Set(reader, "Current", frame);
                Assert.That(select.Invoke(null, new object[] { actor }), Is.SameAs(saw));

                if (anotherDeviceMovesCloser)
                {
                    var second = Root("Other available launcher").AddComponent(T(SawType));
                    var secondBody = Root("Other launcher body", true).transform;
                    secondBody.position = device.position;
                    var grip = new GameObject("Other grip").transform;
                    grip.SetParent(secondBody, false);
                    grip.localPosition = ((Transform)Get(saw, "Handle")).localPosition;
                    Set(second, "carryRoot", secondBody); Set(second, "handle", grip);
                    Set(second, "fireWithoutCables", true);
                    second.gameObject.SetActive(true);
                    Set(second, "session", Get(saw, "session")); Set(second, "synchronized", true);
                    var carry = T("CurtainCall.HumanCannon.HumanCannonCarry");
                    carry.GetMethod("Register", Static).Invoke(null, new object[] { second });
                    device.position += Vector3.right * .35f;
                    Physics.SyncTransforms();
                    Assert.That(carry.GetMethod("FindGrabbable", Static).Invoke(null, new object[] { actor }), Is.SameAs(second));
                    Assert.That(select.Invoke(null, new object[] { actor }), Is.SameAs(saw));
                }

                if (targetUnavailable) Set(saw, "Suppressed", true);
                // Advance the gesture to the release frame inside this synchronous EditMode test.
                var gestures = (IDictionary)select.DeclaringType.GetField("gestures", Static).GetValue(null);
                Set(gestures[reader.GetInstanceID()], "Frame", -1);
                Set(frame, "InteractHeld", false);
                Set(frame, "InteractTapped", true);
                Set(reader, "Current", frame);
                Call(scheme, "Tick", context, frame);
                if (targetUnavailable)
                {
                    Assert.That(devices.Cast<object>().Any(d => (ulong)Get(d, "Carrier") == 2UL), Is.False,
                        "Losing the selected launcher must not redirect this tap to another device.");
                    return;
                }
                Assert.That(Get(saw, "Carrier"), Is.EqualTo(2UL));
                Set(frame, "InteractTapped", false);
                Set(frame, "UseDevicePressed", true);
                Call(scheme, "Tick", context, frame);
                Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(scheme); }
        }

        [Test, Combinatorial]
        public void GuestWireRequestsGrabFireAndDropAtDestination(
            [Values(1UL, 2UL, 3UL)] ulong guestId,
            [Values("Normal", "Fallen", "Retired", "EscapeCaptured", "Arrived")] string hostState,
            [Values(false, true)] bool hostPreviouslyCarried)
        {
            Fixture(true);
            Set(saw, "fireWithoutCables", true);
            var host = saw;
            var guest = Player(guestId);
            Set(guest, "IsOwner", false);
            var localHost = Player(0);
            var stateType = T("CurtainCall.Network.PlayerSync.PlayerState");
            Isolate(PlayerType, "Local");
            T(PlayerType).GetProperty("Local", Static).SetValue(null, localHost);
            Set(localHost, "mover", localHost.GetComponent(T("CurtainCall.Player.PlayerMover")));
            Set(localHost, "IsOwner", true);
            var competitor = Player(guestId == 1 ? 2UL : 1UL);
            if (hostPreviouslyCarried) Own(0);
            Set(Get(localHost, "state"), "Value", Enum.Parse(stateType, hostState));
            var spectator = "CurtainCall.Cameras.CameraSpectator";
            Isolate(spectator, "IsSpectating"); Isolate(spectator, "Target");
            if (hostState != "Normal")
            {
                T(spectator).GetProperty("IsSpectating", Static).SetValue(null, true);
                T(spectator).GetProperty("Target", Static).SetValue(null, hostPreviouslyCarried ? competitor : guest);
            }
            if (hostPreviouslyCarried && hostState == "Normal")
                Call(host, "HandleAction", 0UL, (byte)2, 1u, true);
            else Call(host, "ReleaseUnavailableCarrier");
            Assert.That(Get(host, "Carrier"), Is.EqualTo(ulong.MaxValue));
            var replica = Root("Guest launcher").AddComponent(T(SawType));
            Set(replica, "fireWithoutCables", true);
            var client = Host();
            Set(client, "ConnectionState", Enum.Parse(T("CurtainCall.Network.Session.SessionConnectionState"), "Client"));
            Set(replica, "session", client);
            var allocator = Enum.Parse(T("Unity.Collections.Allocator"), "Temp");
            var writerType = T("Unity.Netcode.FastBufferWriter");
            var constructor = T("Unity.Netcode.FastBufferReader").GetConstructors().Single(c => c.GetParameters()[0].ParameterType == writerType);
            void Deliver(Component target, string receiver, ulong sender, Action<object> write)
            {
                using var writer = (IDisposable)Activator.CreateInstance(writerType, 4096, allocator, -1);
                write(writer);
                using var reader = (IDisposable)constructor.Invoke(new object[] { writer, allocator, -1, 0, allocator });
                Call(target, receiver, sender, reader);
            }
            void Sync() => Deliver(replica, "ReadState", 0, w => Call(host, "WriteState", w, 0));
            void Request(byte action) => Deliver(host, "ReadRequest", guestId, w => Call(replica, "WriteRequest", w, action, 0));

            Physics.SyncTransforms();
            Sync();
            Assert.That(Get(replica, "synchronized"), Is.True, "Initial host packet must enable guest interaction.");
            Request(1);
            // A second press saw the same empty ownership revision before the first grant arrived.
            Deliver(host, "ReadRequest", (ulong)Get(competitor, "OwnerClientId"), w => Call(replica, "WriteRequest", w, (byte)1, 0));
            Sync();
            Assert.That(Get(replica, "Carrier"), Is.EqualTo(guestId));
            if (hostPreviouslyCarried) Call(host, "HandleAction", 0UL, (byte)2, 1u, true);
            Assert.That(Get(host, "Carrier"), Is.EqualTo(guestId));
            Request(3);
            Sync();
            Assert.That(Get(replica, "ActiveProjectileCount"), Is.EqualTo(1));
            guest.transform.position += Vector3.right * 3;
            Physics.SyncTransforms();
            Set(Get(guest, "jump"), "Value", Enum.Parse(T("CurtainCall.Player.JumpKind"), "Fall"));
            Request(2);
            Sync();
            Assert.That(Get(replica, "Carrier"), Is.EqualTo(ulong.MaxValue));
            Assert.That(replica.transform.position.x, Is.EqualTo(guest.transform.position.x).Within(.05f));
        }

        [Test]
        public void RemoteGrabUsesCapsuleSupportAcrossAGapBetweenFloorPanels()
        {
            Fixture(true);
            var guest = Player(2);
            Set(saw, "fireWithoutCables", true);
            var floor = roots.Single(root => root.name == "Actual support");
            floor.transform.position += Vector3.right * 5.04f;
            var other = Root("Left floor panel", true).AddComponent<BoxCollider>();
            other.center = Vector3.down * .25f;
            other.size = new Vector3(10f, .5f, 10f);
            other.transform.position -= Vector3.right * 5.04f;
            Physics.SyncTransforms();
            Assert.That(Call(saw, "HasSupport", Origin, .25f), Is.False);
            Assert.That(Call(saw, "CanGrab", guest), Is.True);
            guest.transform.position += Vector3.up * .5f;
            Physics.SyncTransforms();
            Assert.That(Call(saw, "CanGrab", guest), Is.False);
        }

        [Test]
        public void CarriedBodyRejectsSceneryOverlapButIgnoresOwnLoadCollider()
        {
            Fixture(true); var player = Player(41);
            var load = device.gameObject.AddComponent<BoxCollider>(); load.center = new Vector3(0, .8f, 0); load.size = new Vector3(1, 1.4f, 1);
            Set(saw, "loadCollider", load); Physics.SyncTransforms();
            Assert.That(Call(saw, "PoseFits", device.position, device.rotation, player), Is.True);
            var wall = Root("Load wall", true).AddComponent<BoxCollider>(); wall.transform.position = device.position + Vector3.up * .8f;
            wall.size = new Vector3(.2f, 2, 2); Physics.SyncTransforms();
            Assert.That(Call(saw, "PoseFits", device.position, device.rotation, player), Is.False);
        }

        [TestCase(1UL), TestCase(2UL), TestCase(3UL)]
        [Category("CommonProgressionRegression")]
        public void GuestCanPickUpLauncherBesideRailButNotThroughAClosedWall(ulong guestId)
        {
            Fixture(true); Set(saw, "fireWithoutCables", true);
            var guest = Player(guestId); Set(guest, "IsOwner", false);
            var load = device.gameObject.AddComponent<BoxCollider>();
            load.center = Vector3.up * .8f; load.size = new Vector3(1f, 1.4f, 1f);
            Set(saw, "loadCollider", load);
            var rail = Root("Rail intersects launcher body, not handle reach", true).AddComponent<BoxCollider>();
            rail.transform.position = device.position + Vector3.up * .8f;
            rail.size = new Vector3(3f, .15f, .15f);
            Physics.SyncTransforms();
            Assert.That(Call(saw, "PoseFits", device.position, device.rotation, guest), Is.False);
            Assert.That(Call(saw, "CanGrab", guest), Is.True);

            var blocker = Root("Closed wall between actor and handle", true).AddComponent<BoxCollider>();
            blocker.transform.position = Origin + new Vector3(.3f, 1f, 0);
            blocker.size = new Vector3(.1f, 2f, 3f);
            guest.transform.position = Origin + Vector3.right * .7f;
            Physics.SyncTransforms();
            Assert.That(Call(saw, "CanGrab", guest), Is.False, "Pickup must still obey the common wall/line-of-sight rule.");
            blocker.enabled = false; guest.transform.position = Origin; Physics.SyncTransforms();
            Call(saw, "HandleAction", guestId, (byte)1, 0u, false);
            Assert.That(Get(saw, "Carrier"), Is.EqualTo(guestId));
        }

        [Test, Category("CommonProgressionRegression")]
        public void RemoteLauncherFollowingDoesNotOverwriteTheOwnersReplicatedRotation()
        {
            Fixture(true); Set(saw, "fireWithoutCables", true);
            var guest = Player(1); Set(guest, "IsOwner", false); Own(1);
            guest.transform.rotation = Quaternion.Euler(0, 71f, 0);
            var expected = guest.transform.rotation;
            Assert.That(Call(saw, "FollowCarrier", guest, false), Is.True);
            Assert.That(Quaternion.Angle(guest.transform.rotation, expected), Is.LessThan(.001f));
        }

        Component CarriedSawBeforeIntrusion(out BoxCollider load)
        {
            Fixture(true); var carrier = Player(41); Own(41);
            Set(carrier, "IsOwner", false); // Keep the follow pose independent of a live local camera.
            load = device.gameObject.AddComponent<BoxCollider>();
            load.center = new Vector3(0, .8f, 0); load.size = new Vector3(1, 1.4f, 1);
            Set(saw, "loadCollider", load); Physics.SyncTransforms();
            Assert.That(Call(saw, "FollowCarrier", carrier, true), Is.True);
            return carrier;
        }

        CapsuleCollider IntrudeIntoCarriedSaw()
        {
            var obstacle = Root("Moving body entering the carried saw", true).AddComponent<CapsuleCollider>();
            obstacle.radius = .6f; obstacle.height = 2.8f;
            obstacle.transform.position = device.position + new Vector3(.9f, .8f, 0);
            Physics.SyncTransforms();
            return obstacle;
        }

        static float CarryPenetration(Collider load, Collider obstacle)
        {
            Assert.That(Physics.ComputePenetration(load, load.transform.position, load.transform.rotation,
                obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth), Is.True);
            return depth;
        }

        [Test, Category("CommonProgressionRegression")]
        public void FollowingSawCanLeaveAnIntrudingBodyInTwoCentimetreSteps()
        {
            var carrier = CarriedSawBeforeIntrusion(out var load);
            var obstacle = IntrudeIntoCarriedSaw();
            float beforeDepth = CarryPenetration(load, obstacle);
            Assert.That(beforeDepth, Is.GreaterThan(.1f));
            for (int step = 0; step < 3; step++)
            {
                Vector3 before = device.position;
                carrier.transform.position += Vector3.left * .02f; Physics.SyncTransforms();
                Assert.That(Call(saw, "FollowCarrier", carrier, false), Is.True,
                    "A partial exit must move before the load has fully cleared the intruding body.");
                Assert.That(Get(saw, "carryBlocked"), Is.False);
                Assert.That(Vector3.Distance(device.position, before + Vector3.left * .02f), Is.LessThan(.002f));
                Assert.That(Get(saw, "lastSafeFeet"), Is.EqualTo(carrier.transform.position));
                Physics.SyncTransforms();
                float afterDepth = CarryPenetration(load, obstacle);
                Assert.That(afterDepth, Is.LessThan(beforeDepth - .005f));
                beforeDepth = afterDepth;
            }
        }

        [Test, Category("CommonProgressionRegression")]
        public void FollowingSawStillRejectsDeeperOverlapWithTheIntrudingBody()
        {
            var carrier = CarriedSawBeforeIntrusion(out var load);
            var obstacle = IntrudeIntoCarriedSaw();
            float beforeDepth = CarryPenetration(load, obstacle);
            Vector3 before = device.position, safeFeet = carrier.transform.position;
            carrier.transform.position += Vector3.right * .02f; Physics.SyncTransforms();
            Assert.That(Call(saw, "FollowCarrier", carrier, false), Is.False);
            Assert.That(Get(saw, "carryBlocked"), Is.True);
            Assert.That(device.position, Is.EqualTo(before));
            Assert.That(Get(saw, "lastSafeFeet"), Is.EqualTo(safeFeet));
            Assert.That(CarryPenetration(load, obstacle), Is.EqualTo(beforeDepth).Within(.001f));
        }

        [Test, Category("CommonProgressionRegression")]
        public void FollowingSawCannotCrossAnotherWallWhileLeavingAnExistingOverlap()
        {
            var carrier = CarriedSawBeforeIntrusion(out var load);
            IntrudeIntoCarriedSaw();
            var wall = Root("Separate wall along the escape path", true).AddComponent<BoxCollider>();
            wall.transform.position = device.position + new Vector3(-1.3f, .8f, 0);
            wall.size = new Vector3(.2f, 2, 3); Physics.SyncTransforms();
            Assert.That(Physics.ComputePenetration(load, device.position, device.rotation,
                wall, wall.transform.position, wall.transform.rotation, out _, out _), Is.False);
            Vector3 before = device.position;
            carrier.transform.position += Vector3.left * 2; Physics.SyncTransforms();
            Assert.That(Call(saw, "PoseFits", before + Vector3.left * 2, device.rotation, carrier), Is.True,
                "The clear endpoint must not hide the separate wall between the two poses.");
            Assert.That(Call(saw, "FollowCarrier", carrier, false), Is.False);
            Assert.That(Get(saw, "carryBlocked"), Is.True);
            Assert.That(device.position, Is.EqualTo(before));
        }

        [Test, Category("CommonProgressionRegression")]
        public void InitialSawAlignmentStillRejectsAnExistingOverlap()
        {
            var carrier = CarriedSawBeforeIntrusion(out _);
            IntrudeIntoCarriedSaw();
            Vector3 before = device.position;
            Assert.That(Call(saw, "PoseFits", before, device.rotation, carrier), Is.False);
            Assert.That(Call(saw, "FollowCarrier", carrier, true), Is.False);
            Assert.That(Get(saw, "carryBlocked"), Is.True);
            Assert.That(device.position, Is.EqualTo(before));
        }
    }
}
