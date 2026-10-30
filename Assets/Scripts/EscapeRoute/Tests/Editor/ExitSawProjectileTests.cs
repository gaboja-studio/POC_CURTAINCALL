using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed partial class ExitSawTests
    {
        const string CableType = "CurtainCall.EscapeRoute.EscapeCuttableCable";
        Component Strand(Vector3 center, bool required = true)
        {
            var root = Root("Projectile cable"); root.transform.position = center;
            var strand = root.AddComponent(T(CableType));
            Set(strand, "localPoints", new[] { Vector3.down, Vector3.up }); Set(strand, "curveSubdivisions", 1);
            Set(strand, "radius", .05f); Set(strand, "requiredForExit", required);
            root.SetActive(true); Call(strand, "Rebuild"); return strand;
        }
        void SetCables(params Component[] strands)
        {
            Array array = Array.CreateInstance(T(CableType), strands.Length);
            for (int i = 0; i < strands.Length; i++) array.SetValue(strands[i], i);
            Set(saw, "cables", array);
        }
        object Shot(uint id, uint epoch, Vector3 position, Vector3 direction, float age = 0)
        {
            var type = T(SawType).GetNestedType("Shot", BindingFlags.NonPublic);
            object shot = Activator.CreateInstance(type, true);
            Set(shot, "Id", id); Set(shot, "Epoch", epoch); Set(shot, "Position", position); Set(shot, "Direction", direction); Set(shot, "Age", age);
            return shot;
        }
        Array ShotArray(params object[] values)
        {
            var result = Array.CreateInstance(T(SawType).GetNestedType("Shot", BindingFlags.NonPublic), values.Length);
            for (int i = 0; i < values.Length; i++) result.SetValue(values[i], i);
            return result;
        }
        bool ProjectileSnapshot(uint version, uint owner, ulong actor, uint mask, Vector3[] points, Array entries,
            uint course = 0, int token = 0)
            => (bool)Call(saw, "ApplySnapshot", version, owner, actor, device.position, device.rotation, 0f, (byte)0,
                token, mask, points, entries, 2f, course);
        Vector3 ShotStart => Origin + Vector3.up * 1.45f;

        [Test]
        public void AutomaticShotWaitsTwoSecondsAndNeverCatchesUpWithABurst()
        {
            Fixture();
            Assert.That(Call(saw, "AdvanceShotClock", 0f, true), Is.False);
            Assert.That(Call(saw, "AdvanceShotClock", 1.99f, true), Is.False);
            Assert.That(Call(saw, "AdvanceShotClock", .02f, true), Is.True);
            Assert.That(Call(saw, "AdvanceShotClock", 0f, true), Is.False);
            Assert.That(Call(saw, "AdvanceShotClock", 20f, true), Is.True);
            Assert.That(Call(saw, "AdvanceShotClock", 0f, true), Is.False);
            Call(saw, "AdvanceShotClock", 1f, true); Call(saw, "AdvanceShotClock", 0f, false);
            Assert.That(Call(saw, "AdvanceShotClock", 0f, true), Is.False);
            Assert.That(Call(saw, "AdvanceShotClock", 1.99f, true), Is.False, "공중/막힘에서 돌아와도 새 안전 간격을 센다.");
        }
        [Test]
        public void HostRequiresAGroundedCarrierAndRestartsIntervalAfterAirborne()
        {
            Fixture(true); var player = Player(41);
            SetCables(Strand(ShotStart + Vector3.forward * 3)); Physics.SyncTransforms();
            Call(saw, "HostStep", 3f); Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0));
            Own(41); Call(saw, "HostStep", 0f);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover")); Set(mover, "IsAirborne", true);
            Call(saw, "HostStep", 3f); Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0));
            Set(mover, "IsAirborne", false); Call(saw, "HostStep", 0f); Call(saw, "HostStep", 1.99f);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0));
            Call(saw, "HostStep", .02f); Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1));
        }
        [Test]
        public void ProjectileCutsMultipleStrandsAlongPathButStopsBeforeWall()
        {
            Fixture(true); Own(41);
            var first = Strand(ShotStart + Vector3.forward * 2);
            var second = Strand(ShotStart + Vector3.forward * 3);
            var behind = Strand(ShotStart + Vector3.forward * 5);
            SetCables(first, second, behind);
            var wall = Root("Projectile obstruction", true).AddComponent<BoxCollider>();
            wall.transform.position = ShotStart + Vector3.forward * 4; wall.size = new Vector3(4, 4, .2f);
            ((IList)Get(saw, "shots")).Add(Shot(1, 1, ShotStart, Vector3.forward)); Physics.SyncTransforms();
            Call(saw, "StepProjectiles", .5f, true);
            Assert.That(Get(first, "IsCut"), Is.True); Assert.That(Get(second, "IsCut"), Is.True);
            Assert.That(Get(behind, "IsCut"), Is.False); Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0));
            Assert.That(Get(saw, "DoorOpen"), Is.False);
        }
        [Test]
        public void EdgeFirstProjectileCannotCutStrandBesideItsThinSide()
        {
            Fixture(true); Own(41);
            var centered = Strand(ShotStart + Vector3.forward * 3);
            var beside = Strand(ShotStart + Vector3.forward * 3 + Vector3.up * 1.4f);
            SetCables(centered, beside);
            ((IList)Get(saw, "shots")).Add(Shot(1, 1, ShotStart, Vector3.forward)); Physics.SyncTransforms();
            Call(saw, "StepProjectiles", .25f, true);
            Assert.That(Get(centered, "IsCut"), Is.True); Assert.That(Get(beside, "IsCut"), Is.False);
        }
        [Test]
        public void IndependentLaunchersShareCableCutsWithoutSharingTheirCarrier()
        {
            Fixture(true); Call(saw, "OnEnable"); Own(41);
            var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var second = Root("Second independent saw").AddComponent(T(SawType));
            Set(second, "deviceId", 1); Set(second, "cables", Get(saw, "cables")); second.gameObject.SetActive(true); Call(second, "OnEnable");
            Call(saw, "CutAlong", ShotStart, ShotStart + Vector3.forward * 5, Vector3.up);
            Assert.That(Get(saw, "CutCableCount"), Is.EqualTo(1));
            Assert.That(Get(second, "CutCableCount"), Is.EqualTo(1));
            Assert.That(Get(second, "DoorOpen"), Is.True);
            Assert.That(Get(second, "Carrier"), Is.EqualTo(ulong.MaxValue));
            Assert.That(Get(saw, "Carrier"), Is.EqualTo(41UL));
            Assert.That(Get(saw, "RequestName"), Is.Not.EqualTo(Get(second, "RequestName")));
            Call(second, "OnDisable");
        }
        [Test]
        public void OldOwnerProjectileCannotCutAndManualDropClearsFlyingBlades()
        {
            Fixture(true); Player(41); Own(41);
            var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var entries = (IList)Get(saw, "shots"); entries.Add(Shot(1, 0, ShotStart, Vector3.forward));
            Call(saw, "StepProjectiles", .4f, true);
            Assert.That(Get(strand, "IsCut"), Is.False); Assert.That(entries.Count, Is.Zero);
            entries.Add(Shot(2, 1, ShotStart, Vector3.forward));
            Call(saw, "HandleAction", 41UL, (byte)2, 1U, true);
            Assert.That(entries.Count, Is.Zero); Assert.That(Get(saw, "Carrier"), Is.EqualTo(ulong.MaxValue));
        }
        [Test]
        public void EveryRequiredWireMustBeCutButDecorativeStrandDoesNotLockExit()
        {
            Fixture(); var a = Strand(ShotStart + Vector3.forward * 2); var b = Strand(ShotStart + Vector3.forward * 4);
            var optional = Strand(ShotStart + Vector3.right * 4, false); SetCables(a, b, optional);
            var points = new Vector3[32]; points[0] = a.transform.position; points[1] = b.transform.position;
            Assert.That(ProjectileSnapshot(1, 0, ulong.MaxValue, 1, points, ShotArray()), Is.True);
            Assert.That(Get(saw, "DoorOpen"), Is.False); Assert.That(Get(saw, "RequiredCableCount"), Is.EqualTo(2));
            Assert.That(ProjectileSnapshot(2, 0, ulong.MaxValue, 3, points, ShotArray()), Is.True);
            Assert.That(Get(saw, "DoorOpen"), Is.True); Assert.That(Get(saw, "CutCableCount"), Is.EqualTo(2));
            Assert.That(Get(optional, "IsCut"), Is.False);
        }
        [Test]
        public void CutMaskPersistsAcrossDropAndRegrabAndOnlyNewCourseCanRestoreWires()
        {
            Fixture(); var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var points = new Vector3[32]; points[0] = strand.transform.position;
            Assert.That(ProjectileSnapshot(5, 1, 41, 1, points, ShotArray()), Is.True);
            Assert.That(ProjectileSnapshot(6, 2, ulong.MaxValue, 0, points, ShotArray()), Is.False);
            Assert.That(ProjectileSnapshot(6, 2, ulong.MaxValue, 1, points, ShotArray()), Is.True);
            Assert.That(ProjectileSnapshot(7, 3, 42, 0, points, ShotArray()), Is.False);
            Assert.That(ProjectileSnapshot(7, 3, 42, 1, points, ShotArray()), Is.True);
            Assert.That(ProjectileSnapshot(8, 4, ulong.MaxValue, 0, points, ShotArray(), course: 1), Is.True);
            Assert.That(Get(strand, "IsCut"), Is.False);
            Assert.That(ProjectileSnapshot(9, 4, ulong.MaxValue, 1, points, ShotArray(), course: 0), Is.False);
        }
        [Test]
        public void LateJoinRestoresActualCutPointAndFlyingBladeWithoutLaunchReplay()
        {
            Fixture(); var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var points = new Vector3[32]; points[0] = strand.transform.position + Vector3.up * .4f;
            Assert.That(ProjectileSnapshot(5, 1, 41, 1, points, ShotArray(Shot(1, 1, ShotStart, Vector3.forward, .2f))), Is.True);
            Assert.That(Vector3.Distance((Vector3)Get(strand, "CutPoint"), points[0]), Is.LessThan(.005f));
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1)); Assert.That(Get(saw, "recoilTime"), Is.EqualTo(0f));
            Assert.That(ProjectileSnapshot(6, 1, 41, 1, points, ShotArray(Shot(2, 1, ShotStart, Vector3.forward, .1f))), Is.True);
            Assert.That((float)Get(saw, "recoilTime"), Is.GreaterThan(0));
        }
        [Test]
        public void InvalidShotEpochDuplicateIdsAndUnconfiguredCutBitsAreAtomic()
        {
            Fixture(); var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var points = new Vector3[32]; points[0] = strand.transform.position;
            Assert.That(ProjectileSnapshot(5, 1, 41, 0, points, ShotArray()), Is.True);
            Assert.That(ProjectileSnapshot(99, 1, 41, 0, points, ShotArray(Shot(1, 0, ShotStart, Vector3.forward))), Is.False);
            Assert.That(ProjectileSnapshot(99, 1, 41, 0, points, ShotArray(Shot(1, 1, ShotStart, Vector3.forward), Shot(1, 1, ShotStart, Vector3.forward))), Is.False);
            Assert.That(ProjectileSnapshot(99, 1, 41, 2, points, ShotArray()), Is.False);
            var tooMany = new object[9]; for (int i = 0; i < tooMany.Length; i++) tooMany[i] = Shot((uint)i + 1, 1, ShotStart, Vector3.forward);
            Assert.That(ProjectileSnapshot(99, 1, 41, 0, points, ShotArray(tooMany)), Is.False);
            Assert.That(Get(saw, "stateRevision"), Is.EqualTo(5U)); Assert.That(Get(strand, "IsCut"), Is.False);
        }
        [Test]
        public void ResetBarrierRejectsStaleProjectilesAndMalformedReplyDoesNotReleaseIt()
        {
            Fixture(); var strand = Strand(ShotStart + Vector3.forward * 3); SetCables(strand);
            var points = new Vector3[32];
            ProjectileSnapshot(5, 1, 41, 0, points, ShotArray());
            int token = (int)Call(Get(saw, "resetBarrier"), "Begin"); Set(saw, "synchronized", false);
            Assert.That(ProjectileSnapshot(6, 1, 41, 0, points, ShotArray(Shot(1, 1, ShotStart, Vector3.forward))), Is.False);
            Assert.That(ProjectileSnapshot(6, 2, ulong.MaxValue, 0, points, ShotArray(Shot(1, 1, ShotStart, Vector3.forward)), course: 1, token: token), Is.False);
            Assert.That(Get(Get(saw, "resetBarrier"), "Waiting"), Is.True);
            Assert.That(ProjectileSnapshot(6, 2, ulong.MaxValue, 0, points, ShotArray(), course: 1, token: token), Is.True);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(0)); Assert.That(Get(saw, "recoilTime"), Is.EqualTo(0f));
        }
    }
}
