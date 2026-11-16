using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitGraftSurfaceTests
    {
        GameObject go;
        [SetUp] public void Setup() => go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_01.prefab"));
        [TearDown] public void Cleanup() => Object.DestroyImmediate(go);
        [TestCase(0, PursuitLimb.LeftArm, PursuitLimb.LeftArm, PursuitSeam.ShoulderOrHip)]
        [TestCase(1, PursuitLimb.RightLeg, PursuitLimb.RightLeg, PursuitSeam.ElbowOrKnee)]
        [TestCase(2, PursuitLimb.RightArm, PursuitLimb.LeftLeg, PursuitSeam.ElbowOrKnee)]
        [TestCase(3, PursuitLimb.LeftLeg, PursuitLimb.RightArm, PursuitSeam.ShoulderOrHip)]
        public void DonorSurfaceHasCoverageWithoutChangingGeometry(int donor, PursuitLimb target, PursuitLimb source, PursuitSeam seam)
        {
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(); var mesh = skin.sharedMesh; var materials = skin.sharedMaterials;
            var vertices = mesh.vertices; var weights = mesh.boneWeights; var triangles = mesh.triangles; var bones = skin.bones;
            int renderers = go.GetComponentsInChildren<Renderer>(true).Length;
            go.AddComponent<PursuitSuturedLimbs>().Apply(new[] { new PursuitLimbGraft { Target = target, Source = source, DonorModel = donor, DonorOutfit = 2, Seam = seam } });
            Assert.AreSame(mesh, skin.sharedMesh); CollectionAssert.AreEqual(vertices, mesh.vertices); CollectionAssert.AreEqual(weights, mesh.boneWeights);
            CollectionAssert.AreEqual(triangles, mesh.triangles); CollectionAssert.AreEqual(bones, skin.bones); CollectionAssert.AreEqual(materials, skin.sharedMaterials);
            Assert.AreEqual(renderers, go.GetComponentsInChildren<Renderer>(true).Length); Assert.IsEmpty(go.GetComponentsInChildren<LineRenderer>(true));
            var block = new MaterialPropertyBlock(); skin.GetPropertyBlock(block);
            var rt = block.GetTexture("_GraftMap") as RenderTexture; Assert.IsNotNull(rt); Assert.IsTrue(rt.IsCreated());
            var previous = RenderTexture.active; var pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            try {
                RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
                int coverage = pixels.GetPixels32().Count(p => p.a > 128);
                Assert.Greater(coverage, 1000, "The selected body region must actually be painted");
                Assert.Less(coverage, rt.width * rt.height / 2, "Other body regions must stay untouched");
            } finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
            Assert.IsFalse(ShaderUtil.ShaderHasError(skin.sharedMaterial.shader));
        }
        [Test] public void AppearanceLayersAndBloodHidingPreserveGraftAndClearRestoresSurface()
        {
            var grafts = go.AddComponent<PursuitSuturedLimbs>();
            var choices = new[] { new PursuitLimbGraft { Target = PursuitLimb.LeftArm, Source = PursuitLimb.LeftArm, DonorModel = 1 } };
            grafts.Apply(choices); var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(); var look = go.GetComponent<PursuitAppearance>();
            var block = new MaterialPropertyBlock(); skin.GetPropertyBlock(block); var atlas = block.GetTexture("_GraftMap");
            look.UseLayerOverrides = true; look.LayerDirt = .8f; look.LayerWear = .7f; look.LayerBlood = .9f;
            look.RefreshSurface(true); skin.GetPropertyBlock(block);
            Assert.AreSame(atlas, block.GetTexture("_GraftMap")); Assert.AreEqual(1, block.GetFloat("_GraftEnabled"));
            Assert.AreEqual(.8f, block.GetFloat("_DirtAmount")); Assert.AreEqual(.7f, block.GetFloat("_WearAmount")); Assert.AreEqual(.9f, block.GetFloat("_BaseBlood"));
            look.SetCaptureCount(2); look.RefreshSurface(false); skin.GetPropertyBlock(block);
            Assert.AreEqual(0, block.GetFloat("_BloodVisible")); Assert.AreEqual(0, block.GetFloat("_BloodAmount")); Assert.AreEqual(1, block.GetFloat("_GraftEnabled"));
            grafts.Apply(System.Array.Empty<PursuitLimbGraft>()); skin.GetPropertyBlock(block);
            Assert.AreEqual(0, block.GetFloat("_GraftEnabled")); Assert.AreEqual(.8f, block.GetFloat("_DirtAmount"));
        }
    }
}
