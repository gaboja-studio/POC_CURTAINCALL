using System;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed partial class ExitSawTests
    {
        [TestCase(true)] [TestCase(false)]
        [Category("SawCarrierPiggybackRegression"), Category("CommonProgressionRegression")]
        public void SawCarrierMountRuleUsesSharedSettingAndHostRechecksOwnership(bool blocked)
        {
            Fixture(true); var climber = Player(41); var carrier = Player(42);
            climber.transform.position += Vector3.right;
            var system = Root("Saw carrier piggyback", true).AddComponent(T("CurtainCall.Tightrope.PiggybackSystem"));
            var setting = Get(T("CurtainCall.Settings.GameSettings").GetProperty("Base", Static).GetValue(null), "CarryDevices");
            var previous = Get(setting, "blockPiggybackOnSawCarrier");
            var suppressed = T("CurtainCall.Tightrope.PiggybackSystem").GetProperty("Suppressed", Static);
            var wasSuppressed = suppressed.GetValue(null);
            try
            {
                Set(setting, "blockPiggybackOnSawCarrier", blocked); suppressed.SetValue(null, false); Physics.SyncTransforms();
                Assert.AreSame(carrier, Call(system, "FindMountTarget", climber), "빈손 팀원은 탑승 가능");
                Own(42); // 클라이언트 후보 탐색 후 호스트 요청 도착 전에 상대가 발사대를 잡는다.
                if (blocked)
                {
                    Assert.IsNull(Call(system, "FindMountTarget", climber));
                    StringAssert.StartsWith("거부:", (string)Call(system, "ServerTryLink", 41UL));
                    Assert.IsFalse(((System.Collections.IDictionary)Get(system, "belowOf")).Contains(41UL));
                }
                else Assert.AreSame(carrier, Call(system, "FindMountTarget", climber), "인스펙터 설정을 판정에 사용");
                var ownership = Get(saw, "ownership");
                Call(ownership, "TryApply", Enum.Parse(T("CurtainCall.HumanCannon.CannonAction"), "Release"), 42UL, (uint)Get(ownership, "Revision"));
                Assert.AreSame(carrier, Call(system, "FindMountTarget", climber), "발사대를 내려놓으면 다시 탑승 가능");
                StringAssert.StartsWith("연결:", (string)Call(system, "ServerTryLink", 41UL));
                Own(42);
                Assert.That(Call(system, "BlocksNewPassenger", climber), Is.EqualTo(blocked), "목마 위층을 대상으로 해도 아래 운반자의 공통 제한을 확인");
            }
            finally { Set(setting, "blockPiggybackOnSawCarrier", previous); suppressed.SetValue(null, wasSuppressed); }
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [Category("EscapeSharedCableRegression"), Category("CommonProgressionRegression")]
        public void SharedCablePresentationSurvivesDelayedOtherLauncherSnapshotsAndResetsByCourse(int crew)
        {
            Fixture(true); Call(saw, "OnEnable");
            var cable = Strand(ShotStart + Vector3.forward * 3); SetCables(cable);
            var peers = new Component[crew]; peers[0] = saw;
            for (int i = 1; i < crew; i++)
            {
                peers[i] = Root("Other launcher " + i).AddComponent(T(SawType));
                Set(peers[i], "carryRoot", Root("Other body " + i, true).transform);
                Set(peers[i], "cables", Get(saw, "cables")); Call(peers[i], "OnEnable");
            }
            var points = new Vector3[32]; points[0] = cable.transform.position;
            bool Receive(Component target, uint revision, uint mask, uint course)
            {
                var body = (Transform)Get(target, "CarryRoot");
                return (bool)Call(target, "ApplySnapshot", revision, 0u, ulong.MaxValue, body.position, body.rotation,
                    0f, (byte)0, 0, mask, points, ShotArray(), 0f, course);
            }
            Assert.IsTrue(Receive(peers[0], 1, 1, 0));
            Assert.IsTrue((bool)Get(cable, "IsCut"));
            for (int i = 1; i < crew; i++)
            {
                Assert.IsTrue(Receive(peers[i], 1, 0, 0));
                Assert.IsTrue((bool)Get(cable, "IsCut"), "다른 발사대의 늦은 미절단 상태가 절단된 배선을 되살리면 안 된다.");
            }
            Assert.IsTrue(Receive(peers[0], 2, 0, 1));
            Assert.IsFalse((bool)Get(cable, "IsCut"), "새 코스에서는 배선을 복구한다.");
            for (int i = 1; i < crew; i++)
            {
                Assert.IsTrue(Receive(peers[i], 2, 1, 0));
                Assert.IsFalse((bool)Get(cable, "IsCut"), "이전 코스의 지연 절단은 새 코스에 적용하지 않는다.");
                Assert.IsTrue(Receive(peers[i], 3, 0, 1));
            }
            Assert.IsTrue(Receive(peers[0], 3, 1, 1));
            Assert.IsTrue((bool)Get(cable, "IsCut"), "새 코스의 새 절단은 표시한다.");
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ConnectedCrewGetsOneLauncherEachAndRejoiningRestoresTheSameDevice(int crew)
        {
            Fixture(); Set(saw, "scaleWithCrew", true);
            var originalPosition = device.position;
            for (int slot = 0; slot < 4; slot++)
            {
                Set(saw, "deviceId", slot); Call(saw, "RefreshCrewAvailability", crew);
                Assert.That(device.gameObject.activeSelf, Is.EqualTo(slot < crew));
                Call(saw, "RefreshCrewAvailability", 4);
                Assert.IsTrue(device.gameObject.activeSelf);
                Assert.That(device.position, Is.EqualTo(originalPosition));
            }
        }

        [Test]
        public void CrewDisconnectNeverHidesAnOccupiedMachineAndDropReleasesTheSpare()
        {
            Fixture(); Set(saw, "scaleWithCrew", true); Set(saw, "deviceId", 3); Own(41);
            Call(saw, "RefreshCrewAvailability", 1); Assert.IsTrue(device.gameObject.activeSelf);
            var ownership = Get(saw, "ownership");
            Assert.That(Call(ownership, "TryApply", Enum.Parse(T("CurtainCall.HumanCannon.CannonAction"), "Release"),
                41UL, (uint)Get(ownership, "Revision")), Is.True);
            Call(saw, "RefreshCrewAvailability", 1); Assert.IsFalse(device.gameObject.activeSelf);
        }

        [Test]
        public void HiddenSpareStillReceivesSharedCutsAndKeepsItsOwnEmptyCarrier()
        {
            Fixture(true); Call(saw, "OnEnable"); Own(41);
            var cable = Strand(ShotStart + Vector3.forward * 3); SetCables(cable);
            var spare = Root("Hidden crew spare").AddComponent(T(SawType));
            var spareBody = Root("Spare physical machine", true).transform;
            Set(spare, "carryRoot", spareBody); Set(spare, "scaleWithCrew", true); Set(spare, "deviceId", 3);
            Set(spare, "cables", Get(saw, "cables")); Call(spare, "OnEnable");
            Call(spare, "RefreshCrewAvailability", 1); Assert.IsFalse(spareBody.gameObject.activeSelf);
            Call(saw, "CutAlong", ShotStart, ShotStart + Vector3.forward * 5, Vector3.up, null);
            Assert.That(Get(spare, "DoorOpen"), Is.True); Assert.That(Get(spare, "Carrier"), Is.EqualTo(ulong.MaxValue));
            Call(spare, "RefreshCrewAvailability", 4);
            Assert.IsTrue(spareBody.gameObject.activeSelf); Assert.That(Get(spare, "CutCableCount"), Is.EqualTo(1));
            Call(spare, "OnDisable");
        }

        [Test]
        public void HidingSparePreservesAlreadyFiredUnlimitedSawsUntilTheyHitScenery()
        {
            Fixture(true); SetCables(Strand(ShotStart + Vector3.forward * 100));
            Set(saw, "scaleWithCrew", true); Set(saw, "deviceId", 3); Set(saw, "projectileLifetime", 0f);
            Call(saw, "SpawnProjectile"); Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1));
            Call(saw, "RefreshCrewAvailability", 1);
            Assert.IsFalse(device.gameObject.activeSelf); Assert.That(Get(saw, "Ready"), Is.False);
            Assert.That(Get(saw, "ProjectileSessionReady"), Is.True);
            var live = (System.Collections.IList)Get(saw, "shots"); var before = (Vector3)Get(live[0], "Position");
            Call(saw, "HostProjectileStep", .2f, null);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1));
            Assert.That(Vector3.Distance((Vector3)Get(live[0], "Position"), before), Is.GreaterThan(1f));
        }

        [Test]
        public void OtherScenesRetainAuthoredLauncherCountUntilExplicitlyEnabled()
        {
            Fixture(); Set(saw, "deviceId", 3); Call(saw, "RefreshCrewAvailability", 1);
            Assert.IsTrue(device.gameObject.activeSelf);
        }
    }
}
