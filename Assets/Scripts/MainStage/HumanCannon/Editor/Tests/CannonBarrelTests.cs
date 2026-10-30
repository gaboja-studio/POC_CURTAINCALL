using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonBarrelTests
    {
        static CannonBarrelPhase Phase(CannonBarrelCycle cycle, double time) => cycle.Sample(time, 1.2f, .35f, 2.8f, out _);

        [TestCase(4f)] [TestCase(5f)] [TestCase(20f)]
        public void ConfiguredCooldownControlsOnlyInactiveWait(float duration)
        {
            var cycle = new CannonBarrelCycle(); cycle.Fire(10);
            float rest = duration - 4;
            Assert.AreEqual(CannonBarrelPhase.FireDisable, cycle.Sample(11, 1.2f, rest, 2.8f, out _));
            Assert.AreEqual(CannonBarrelPhase.Recover, cycle.Sample(10 + duration - 2.7, 1.2f, rest, 2.8f, out _));
            Assert.AreEqual(CannonBarrelPhase.Recover, cycle.Sample(10 + duration - .01, 1.2f, rest, 2.8f, out _));
            Assert.AreEqual(CannonBarrelPhase.Active, cycle.Sample(10 + duration + .01, 1.2f, rest, 2.8f, out _));
        }

        [Test]
        public void ReenableSkipsLongRestAndLaterShotsStillUseFullCooldown()
        {
            var cycle = new CannonBarrelCycle(); cycle.Fire(10); cycle.SetAvailable(false, 12);
            Assert.AreEqual(CannonBarrelPhase.Inactive, cycle.Sample(15, 1.2f, 16, 2.8f, out _));
            cycle.SetAvailable(true, 15);
            Assert.AreEqual(CannonBarrelPhase.Recover, cycle.Sample(15.01, 1.2f, 16, 2.8f, out _));
            Assert.AreEqual(CannonBarrelPhase.Active, cycle.Sample(17.81, 1.2f, 16, 2.8f, out _));
            var joined = new CannonBarrelCycle(); joined.Restore(cycle.FiredAt, cycle.UnlockedAt);
            Assert.AreEqual(CannonBarrelPhase.Recover, joined.Sample(16, 1.2f, 16, 2.8f, out _));
            Assert.AreEqual(CannonBarrelPhase.Active, joined.Sample(17.81, 1.2f, 16, 2.8f, out _));
            cycle.Fire(18);
            Assert.AreEqual(CannonBarrelPhase.Inactive, cycle.Sample(30, 1.2f, 16, 2.8f, out _));
            Assert.AreEqual(CannonBarrelPhase.Active, cycle.Sample(38.01, 1.2f, 16, 2.8f, out _));
        }

        [Test]
        public void SharedSettingsDefaultToFiveAndClampFourThroughTwenty()
        {
            var settings = ScriptableObject.CreateInstance<CurtainCall.Settings.HumanCannonSettings>();
            try
            {
                Assert.AreEqual(5, settings.CannonCooldown);
                var data = new SerializedObject(settings); var field = data.FindProperty("cannonCooldown");
                field.floatValue = 1; data.ApplyModifiedPropertiesWithoutUndo(); Assert.AreEqual(4, settings.CannonCooldown);
                field.floatValue = 30; data.ApplyModifiedPropertiesWithoutUndo(); Assert.AreEqual(20, settings.CannonCooldown);
            }
            finally { Object.DestroyImmediate(settings); }
        }


        [Test]
        public void InitiallyDisabledStationHasInactivePoseWithoutAnEnableDisableRoundTrip()
        {
            var go = new GameObject("Initially disabled cannon"); go.SetActive(false);
            try
            {
                var station = go.AddComponent<HumanCannonStation>(); station.enabled = false;
                Assert.AreEqual(CannonBarrelPhase.Inactive, station.SampleBarrel(out _));
                Assert.IsFalse(station.ReadyToUse);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShotFinishesLowerAndRestBeforeFourStepRecoveryAllowsBoarding()
        {
            var cycle = new CannonBarrelCycle();
            Assert.AreEqual(CannonBarrelPhase.Active, Phase(cycle, 0));
            cycle.Fire(10);
            Assert.AreEqual(CannonBarrelPhase.FireDisable, Phase(cycle, 10.6));
            Assert.AreEqual(CannonBarrelPhase.Inactive, Phase(cycle, 11.4));
            Assert.AreEqual(CannonBarrelPhase.Recover, Phase(cycle, 12));
            Assert.AreEqual(CannonBarrelPhase.Recover, Phase(cycle, 14.34));
            Assert.AreEqual(CannonBarrelPhase.Active, Phase(cycle, 14.36));
        }

        [Test]
        public void LateJoinSeeksCurrentPhaseAndResetNeverReplaysPreviousShot()
        {
            var cycle = new CannonBarrelCycle(); cycle.Restore(10);
            Assert.AreEqual(CannonBarrelPhase.Recover, cycle.Sample(12.95, 1.2f, .35f, 2.8f, out float t));
            Assert.That(t, Is.EqualTo(.5f).Within(.001f));
            Assert.AreEqual(CannonBarrelPhase.Active, Phase(cycle, 50));
            cycle.Reset(); Assert.AreEqual(CannonBarrelPhase.Active, Phase(cycle, 11));
            Assert.AreEqual(-1, cycle.FiredAt);
        }

        [Test]
        public void ExternalInterlockCannotBeOverwrittenByCooldownCompletion()
        {
            var cycle = new CannonBarrelCycle(); cycle.Fire(10); cycle.SetAvailable(false, 10.1);
            Assert.AreEqual(CannonBarrelPhase.FireDisable, Phase(cycle, 10.5));
            Assert.AreEqual(CannonBarrelPhase.Inactive, Phase(cycle, 20));
            cycle.SetAvailable(true, 20);
            Assert.AreEqual(CannonBarrelPhase.Recover, Phase(cycle, 20));
            cycle.SetAvailable(true, 21); // Repeated machinery updates must not restart the lift.
            Assert.AreEqual(CannonBarrelPhase.Active, Phase(cycle, 22.81));
            cycle.SetAvailable(false, 23); cycle.Reset();
            Assert.AreEqual(CannonBarrelPhase.Inactive, Phase(cycle, 30));
        }

        [Test]
        public void UnlockDuringShotSkipsRestButWaitsForLoweringToFinish()
        {
            var cycle = new CannonBarrelCycle(); cycle.Fire(10);
            cycle.SetAvailable(false, 10.1); cycle.SetAvailable(true, 10.2);
            Assert.AreEqual(CannonBarrelPhase.FireDisable, Phase(cycle, 10.8));
            Assert.AreEqual(CannonBarrelPhase.Recover, Phase(cycle, 11.4));
            Assert.AreEqual(CannonBarrelPhase.Recover, Phase(cycle, 11.56));
        }

        [Test]
        public void AuthoredClipsOnlyMoveBarrelAndJoinAtMatchingEndpoints()
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CannonBarrelArtBuilder.PrefabPath));
            try
            {
                var view = root.GetComponent<CannonBarrelPresentation>();
                var chassis = root.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Chassis");
                var barrel = root.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Barrel");
                var fixedMatrix = chassis.transform.localToWorldMatrix;
                foreach (var f in new[] { chassis, barrel })
                {
                    Assert.AreEqual(1, f.sharedMesh.subMeshCount);
                    Assert.AreEqual(1, f.GetComponent<Renderer>().sharedMaterials.Length);
                    Assert.IsNotNull(f.GetComponent<Renderer>().sharedMaterial);
                }
                foreach (string name in new[] { "Active", "Inactive", "FireDisable", "Recover" })
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CannonBarrelArtBuilder.Art + "/Animations/CC_Cannon_" + name + ".anim");
                    Assert.IsNotNull(clip); Assert.Greater(clip.length, .9f);
                    Assert.IsTrue(AnimationUtility.GetCurveBindings(clip).All(b => b.path == "BarrelPivot"));
                }
                view.Sample(CannonBarrelPhase.Active, 0); var activeRotation = view.BarrelPivot.localRotation;
                var activePosition = view.BarrelPivot.localPosition;
                view.Sample(CannonBarrelPhase.FireDisable, 0);
                Assert.Less(Quaternion.Angle(activeRotation, view.BarrelPivot.localRotation), .01f);
                view.Sample(CannonBarrelPhase.Inactive, 0); var inactiveRotation = view.BarrelPivot.localRotation;
                Assert.That(Quaternion.Angle(activeRotation, inactiveRotation), Is.EqualTo(32).Within(.1f));
                Assert.Greater(barrel.sharedMesh.vertices.Min(v => barrel.transform.TransformPoint(v).y), .2f,
                    "Actual geometry must clear the floor; rotated mesh AABB includes empty corners");
                view.Sample(CannonBarrelPhase.FireDisable, 1);
                Assert.Less(Quaternion.Angle(inactiveRotation, view.BarrelPivot.localRotation), .01f);
                view.Sample(CannonBarrelPhase.Recover, 0);
                Assert.Less(Quaternion.Angle(inactiveRotation, view.BarrelPivot.localRotation), .01f);
                for (int i = 0; i <= 100; i++)
                {
                    view.Sample(CannonBarrelPhase.Recover, i / 100f);
                    Assert.AreEqual(fixedMatrix, chassis.transform.localToWorldMatrix, "Carriage/wheels cannot wobble or slide");
                }
                Assert.Less(Quaternion.Angle(activeRotation, view.BarrelPivot.localRotation), .01f);
                Assert.Less(Vector3.Distance(activePosition, view.BarrelPivot.localPosition), .0001f);
                // Four authored ratchet peaks, each above its settled step, then stable rest.
                for (int i = 0; i < 4; i++)
                {
                    view.Sample(CannonBarrelPhase.Recover, (i * .66f + .39f) / 2.8f);
                    var peak = view.BarrelPivot.localRotation;
                    view.Sample(CannonBarrelPhase.Recover, (i * .66f + .61f) / 2.8f);
                    Assert.That(Quaternion.Angle(peak, view.BarrelPivot.localRotation), Is.InRange(.8f, 2f));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedCourseEnablesMotionWithoutMovingSeatMuzzleOrLaunchVelocity()
        {
            var scene = EditorSceneManager.OpenPreviewScene(HumanCannonTestSceneBuilder.ScenePath);
            try
            {
                var stations = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HumanCannonStation>(true)).ToArray();
                Assert.AreEqual(10, stations.Length);
                foreach (var station in stations)
                {
                    Assert.IsTrue(station.BarrelMotionEnabled); Assert.IsTrue(station.ReadyToUse);
                    Assert.IsNotNull(station.GetComponentInChildren<CannonBarrelPresentation>());
                    Assert.Less(Vector3.Distance(new Vector3(0, 2, 1.5f), station.transform.InverseTransformPoint(station.Muzzle.position)), .001f);
                    Assert.Less(Vector3.Distance(new Vector3(0, 1.2f, 0), station.transform.InverseTransformPoint(station.Seat.position)), .001f);
                    Assert.AreEqual(12, station.LaunchVelocity.y);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
