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
    }
}
