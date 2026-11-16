using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed partial class ExitSawTests
    {
        [Test, Combinatorial, Category("EscapeFullCombinationAudit")]
        public void FiringHonoursBodyAndActionStateCombinations(
            [Values(false, true)] bool local, [Values(0, 1, 2, 3)] int lostCount,
            [Values("Walk", "Sprint", "Crouch", "Jump", "CrouchedJump", "ControlLocked", "BodyDisabled", "SimulationPaused")] string action,
            [Values("Normal", "Fallen", "Retired", "Arrived", "EscapeCaptured")] string state)
        {
            Fixture(true); var player = Player(41); Own(41);
            SetCables(Strand(ShotStart + Vector3.forward * 20));
            Set(player, "IsOwner", local);
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var effects = player.gameObject.AddComponent(T("CurtainCall.Damage.BodyDamageEffects"));
            var condition = player.GetComponent(T("CurtainCall.Player.PlayerCondition"));
            var partType = T("CurtainCall.Player.BodyPart");
            object parts = Enum.ToObject(partType, 0);
            var next = condition.GetType().GetMethod("NextFixedCut", Static);
            for (int i = 0; i < lostCount; i++)
                parts = Enum.ToObject(partType, Convert.ToInt32(parts) | Convert.ToInt32(next.Invoke(null, new[] { parts })));
            Call(condition, "SetLostParts", parts); Set(Get(player, "lostParts"), "Value", parts);
            Call(effects, "Awake"); Call(effects, "Update");
            Set(mover, "Simulated", local && action != "SimulationPaused");
            Set(mover, "ControlEnabled", action != "ControlLocked");
            Set(mover, "IsCrouched", action is "Crouch" or "CrouchedJump");
            Set(mover, "IsSprinting", action == "Sprint");
            bool airborne = action is "Jump" or "CrouchedJump";
            Set(mover, "IsAirborne", airborne); Call(mover, "ApplyBodySize", true);
            ((CharacterController)Get(mover, "Body")).enabled = action != "BodyDisabled";
            Set(Get(player, "state"), "Value", Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), state));
            if (airborne) { player.transform.position += Vector3.up; device.position += Vector3.up; }
            Physics.SyncTransforms();
            bool expected = state == "Normal" && action != "BodyDisabled"
                && !(local && action is "ControlLocked" or "SimulationPaused");
            for (int click = 0; click < 2; click++)
                Assert.That(Call(saw, "TryFire", player), Is.EqualTo(expected), "Each click must obey the same current authoritative eligibility.");
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(expected ? 2 : 0));
        }

        [TestCase(false), TestCase(true), Category("EscapeNovember16Followup")]
        public void HiddenSuppressedLauncherReleasesCarrierAndKeepsFiredSaws(bool crewScaled)
        {
            Fixture(true); Player(41); Own(41);
            Set(saw, "scaleWithCrew", crewScaled); Set(saw, "projectileLifetime", 0f);
            SetCables(Strand(ShotStart + Vector3.forward * 20));
            Call(saw, "SpawnProjectile");
            Set(saw, "Suppressed", true); device.gameObject.SetActive(false);
            Call(saw, "LateUpdate");
            Assert.That(Get(saw, "Carrier"), Is.EqualTo(ulong.MaxValue), "A hidden device cannot strand its carrier in the carry state.");
            Assert.That(Get(saw, "ProjectileSessionReady"), Is.True, "Already fired blades must keep moving after their launcher disappears.");
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1));
        }

        [TestCase("Fallen"), TestCase("Retired"), TestCase("Arrived"), TestCase("EscapeCaptured"), TestCase("Disconnected")]
        [Category("EscapeNovember16Followup")]
        public void UnavailableCarrierReleasesMachineWithoutUndoingCutsOrFlyingShots(string reason)
        {
            Fixture(true); var player = Player(41); Own(41); Set(saw, "projectileLifetime", 0f);
            var strand = Strand(ShotStart + Vector3.forward * 20); SetCables(strand);
            Call(saw, "CutAlong", ShotStart, ShotStart + Vector3.forward * 22, Vector3.up, null);
            Call(saw, "SpawnProjectile");
            if (reason == "Disconnected") Set(player, "IsSpawned", false);
            else Set(Get(player, "state"), "Value", Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), reason));
            Call(saw, "HostStep", 0f);
            Assert.That(Get(saw, "Carrier"), Is.EqualTo(ulong.MaxValue));
            Assert.That(Get(saw, "DoorOpen"), Is.True);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(1));
            var successor = Player(42); successor.transform.position += Vector3.right * 3;
            Own(42);
            Assert.That(Call(saw, "TryFire", successor), Is.True);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(2));
        }

        [Test, Category("EscapeNovember16Followup")]
        public void FullVolleyRejectsOverflowWithoutDeletingFlyingBladesAndFiresAgainAfterImpact()
        {
            Fixture(true); var player = Player(41); Own(41); Set(saw, "projectileLifetime", 0f);
            SetCables(Strand(ShotStart + Vector3.right * 20));
            for (int i = 0; i < 64; i++) Assert.That(Call(saw, "TryFire", player), Is.True);
            Assert.That(Call(saw, "TryFire", player), Is.False);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.EqualTo(64));
            var wall = Root("Volley impact wall", true).AddComponent<BoxCollider>();
            wall.transform.position = ShotStart + Vector3.forward * 4; wall.size = new Vector3(6, 6, .05f);
            Physics.SyncTransforms(); Call(saw, "HostProjectileStep", 1f, player);
            Assert.That(Get(saw, "ActiveProjectileCount"), Is.Zero);
            Assert.That(Call(saw, "TryFire", player), Is.True);
        }

        [Test, Category("EscapeNovember16Recovery")]
        public void MovingShotSnapshotsAdvanceRevisionEvenWhenNoCableOrOwnerChanges()
        {
            Fixture(true); Own(41); Set(saw,"projectileLifetime",0f);
            SetCables(Strand(ShotStart + Vector3.right*10));
            ((IList)Get(saw,"shots")).Add(Shot(1,1,ShotStart,Vector3.forward));
            uint before=(uint)Get(saw,"stateRevision");
            Set(saw,"nextPublish",-1d);
            Call(saw,"HostProjectileStep",.1f,null);
            Assert.That((uint)Get(saw,"stateRevision"),Is.GreaterThan(before),
                "Predicted client obstruction must be recoverable from a newer authoritative snapshot.");
        }
        [Test] public void UnlimitedShotTravelsBeyondOldRangeAndStopsAtADistantThinWall()
        {
            Fixture(true);Own(41);Set(saw,"projectileLifetime",0f);Set(saw,"projectileSpeed",36f);
            var before=Strand(ShotStart+Vector3.forward*100);var after=Strand(ShotStart+Vector3.forward*140);SetCables(before,after);
            var wall=Root("Distant thin asset",true).AddComponent<BoxCollider>();wall.transform.position=ShotStart+Vector3.forward*120;wall.size=new Vector3(6,6,.05f);
            var entries=(IList)Get(saw,"shots");var shot=Shot(1,1,ShotStart,Vector3.forward);entries.Add(shot);Physics.SyncTransforms();
            Call(saw,"StepProjectiles",2f,true);Assert.That(entries.Count,Is.EqualTo(1));Assert.That(((Vector3)Get(shot,"Position")-ShotStart).z,Is.EqualTo(72).Within(.01f));
            Call(saw,"StepProjectiles",2f,true);Assert.That(Get(before,"IsCut"),Is.True);Assert.That(Get(after,"IsCut"),Is.False);Assert.That(entries.Count,Is.Zero);
        }
        [Test] public void UnlimitedShotSurvivesDropAndOwnershipChangeButCourseResetClearsIt()
        {
            Fixture(true);var player=Player(41);Own(41);Set(saw,"projectileLifetime",0f);SetCables(Strand(ShotStart+Vector3.right*10));
            var entries=(IList)Get(saw,"shots");var shot=Shot(1,1,ShotStart,Vector3.forward,120);entries.Add(shot);
            Call(saw,"HandleAction",41UL,(byte)2,1U,true);Assert.That(Get(saw,"Carrier"),Is.EqualTo(ulong.MaxValue));Assert.That(entries.Count,Is.EqualTo(1));
            Own(42);Call(saw,"StepProjectiles",1f,false);Assert.That(entries.Count,Is.EqualTo(1));Assert.That((float)Get(shot,"Age"),Is.EqualTo(121));
            Call(saw,"ResetProjectileCourse");Assert.That(entries.Count,Is.Zero);
        }
        [Test] public void UnlimitedSnapshotAcceptsOldInFlightShotsWithoutACarrierAndStillRejectsInvalidAge()
        {
            Fixture();Set(saw,"projectileLifetime",0f);SetCables(Strand(ShotStart+Vector3.right*10));var points=new Vector3[32];
            var old=Shot(1,1,ShotStart,Vector3.forward,120);
            Assert.That(ProjectileSnapshot(5,2,ulong.MaxValue,0,points,ShotArray(old)),Is.True);
            Call(saw,"StepProjectiles",1f,false);Assert.That(Get(saw,"ActiveProjectileCount"),Is.EqualTo(1));
            Assert.That(ProjectileSnapshot(6,2,ulong.MaxValue,0,points,ShotArray(Shot(2,1,ShotStart,Vector3.forward,float.PositiveInfinity))),Is.False);
            Assert.That(ProjectileSnapshot(6,2,ulong.MaxValue,0,points,ShotArray(Shot(2,3,ShotStart,Vector3.forward,1))),Is.False);
        }
        [Test] public void ProjectilePresentationStripsParticleEffectsWithoutRemovingBladeGeometryOrCableSparks()
        {
            Fixture();var visual=Root("Legacy projectile template",true);var effect=new GameObject("Projectile Sparks");effect.transform.SetParent(visual.transform);effect.AddComponent<ParticleSystem>();
            var mesh=new GameObject("Actual saw blade",typeof(MeshFilter),typeof(MeshRenderer));mesh.transform.SetParent(visual.transform);
            var cut=Root("Cable cut sparks",true).AddComponent<ParticleSystem>();
            T(SawType).GetMethod("StripProjectileEffects",Static).Invoke(null,new object[]{visual});
            Assert.That(visual.GetComponentsInChildren<ParticleSystem>(true),Is.Empty);Assert.That(visual.GetComponentsInChildren<MeshFilter>(true).Length,Is.EqualTo(1));Assert.That(cut,Is.Not.Null);
        }
        [Test] public void ClientPredictionStopsAtItsWallWithoutApplyingCableCuts()
        {
            Fixture();Set(saw,"projectileLifetime",0f);Set(saw,"projectileSpeed",36f);
            var strand=Strand(ShotStart+Vector3.forward*2);SetCables(strand);
            var wall=Root("Client visible wall",true).AddComponent<BoxCollider>();wall.transform.position=ShotStart+Vector3.forward*4;wall.size=new Vector3(6,6,.05f);
            var entries=(IList)Get(saw,"shots");entries.Add(Shot(1,0,ShotStart,Vector3.forward));Physics.SyncTransforms();
            Call(saw,"StepProjectiles",1f,false);
            Assert.That(entries.Count,Is.Zero);Assert.That(Get(strand,"IsCut"),Is.False,"Only the host confirms a cut.");
        }
    }
}
