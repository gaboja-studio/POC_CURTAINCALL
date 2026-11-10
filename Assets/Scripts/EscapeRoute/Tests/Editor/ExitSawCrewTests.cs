using System;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed partial class ExitSawTests
    {
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
