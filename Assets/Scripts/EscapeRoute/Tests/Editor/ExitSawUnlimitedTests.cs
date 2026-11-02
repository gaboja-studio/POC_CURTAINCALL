using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed partial class ExitSawTests
    {
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
