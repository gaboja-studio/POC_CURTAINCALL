using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitUpgradeTests
    {
        GameObject root;
        static readonly Vector3 Origin = new(23000, 0, 23000);
        [SetUp] public void Setup() => root = new GameObject("wall-support-test");
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        void Box(Vector3 p, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.transform.SetParent(root.transform);
            box.transform.position = Origin + p; box.transform.localScale = size; Physics.SyncTransforms();
        }
        [Test] public void BalconySlabAboveEmptyAirCannotBeClimbed()
        {
            Box(new Vector3(0, 3.9f, 0), new Vector3(5, .2f, 2));
            Assert.IsFalse(PursuitClimbPath.HasWallSupport(Origin + new Vector3(0, 0, -1.5f), Origin + new Vector3(0, 4, -.6f), null));
        }
        [Test] public void ContinuousWallCanBeClimbed()
        {
            Box(new Vector3(0, 2, 0), new Vector3(5, 4, 2));
            Assert.IsTrue(PursuitClimbPath.HasWallSupport(Origin + new Vector3(0, 0, -1.5f), Origin + new Vector3(0, 4, -.6f), null));
        }
        [Test] public void VerticalGapInWallRejectsAirClimbing()
        {
            Box(new Vector3(0, .5f, 0), new Vector3(5, 1, 2));
            Box(new Vector3(0, 3.5f, 0), new Vector3(5, 1, 2));
            Assert.IsFalse(PursuitClimbPath.HasWallSupport(Origin + new Vector3(0, 0, -1.5f), Origin + new Vector3(0, 4, -.6f), null));
        }
        [Test] public void GraftAndRemovalPreserveOriginalSharedMeshes()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_01.prefab");
            var actor = Object.Instantiate(prefab, root.transform);
            var original = actor.GetComponentsInChildren<SkinnedMeshRenderer>().ToDictionary(s => s, s => s.sharedMesh);
            var grafts = actor.AddComponent<PursuitSuturedLimbs>();
            grafts.Apply(new[] { new PursuitLimbGraft { Target = PursuitLimb.LeftArm, Source = PursuitLimb.LeftArm, DonorModel = 0 } });
            Assert.AreEqual(original.Count, actor.GetComponentsInChildren<SkinnedMeshRenderer>().Length);
            foreach (var pair in original) Assert.AreSame(pair.Value, pair.Key.sharedMesh);
            var block = new MaterialPropertyBlock(); original.Keys.First().GetPropertyBlock(block);
            Assert.AreEqual(1, block.GetFloat("_GraftEnabled"));
            grafts.Apply(System.Array.Empty<PursuitLimbGraft>());
            foreach (var pair in original) Assert.AreSame(pair.Value, pair.Key.sharedMesh);
            Assert.IsFalse(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s => s.name.StartsWith("Sutured_")));
            original.Keys.First().GetPropertyBlock(block); Assert.AreEqual(0, block.GetFloat("_GraftEnabled"));
        }
        [Test] public void ImportedMotionHasCrouchAndDeathCurves()
        {
            foreach (var name in new[] { "Crouch", "CrouchWalk", "Death" }) {
                var clip = Resources.Load<AnimationClip>("Animations/Pursuit/" + name);
                Assert.IsNotNull(clip, name); Assert.IsTrue(clip.legacy); Assert.Greater(clip.length, .2f);
                Assert.IsTrue(UnityEditor.AnimationUtility.GetCurveBindings(clip).Any(b => b.path.EndsWith("Hips")));
            }
        }
        [Test] public void LowVentLandingAllowsCrouchingButNotStanding()
        {
            Box(new Vector3(0, 1.35f, 0), new Vector3(3, .3f, 3));
            Assert.IsTrue(PursuitDirectClimb.LandingRoom(Origin, null));
            Assert.IsFalse(PursuitDirectClimb.StandingRoom(Origin, null));
        }
        [Test] public void CrouchPoseFitsVentAndDoesNotKeepStandingHitBounds()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_01.prefab");
            var go = Object.Instantiate(prefab, root.transform); go.transform.position = Vector3.zero;
            var actor = go.GetComponent<PursuitActor>();
            var clip = Resources.Load<AnimationClip>("Animations/Pursuit/CrouchWalk");
            var baked = new Mesh();
            try {
                for (int frame = 0; frame < 20; frame++) {
                    clip.SampleAnimation(go, clip.length * frame / 20);
                    float high = 0;
                    foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                        skin.BakeMesh(baked);
                        foreach (var p in baked.vertices) high = Mathf.Max(high, (skin.transform.position + skin.transform.rotation * p).y);
                    }
                    Assert.LessOrEqual(high * 1.16f, PursuitActor.CrouchBodyHeight + .01f, "Medium body must fit too");
                    Assert.IsFalse(actor.SweepSaw(new Vector3(0, 1.6f, -2), Vector3.forward, 4, .15f, .03f, out _), "No standing-height hits on crouching body");
                    Assert.IsTrue(actor.SweepSaw(new Vector3(0, .65f, -2), Vector3.forward, 4, .15f, .03f, out _));
                }
            } finally { Object.DestroyImmediate(baked); }
        }
    }
}
