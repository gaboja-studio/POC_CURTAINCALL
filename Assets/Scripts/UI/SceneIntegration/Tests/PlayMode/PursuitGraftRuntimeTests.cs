#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class PursuitGraftRuntimeTests
    {
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.WaitingRoom.Pursuit." + name)).First(t => t != null);
        [UnityTest] public IEnumerator SharedSurfaceSurvivesAnotherActorRemovalAndReleasesAfterLastUser()
        {
            var root = new GameObject("Surface graft runtime fixture"); root.SetActive(false);
            try {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_01.prefab");
                var a = Object.Instantiate(prefab, root.transform); var b = Object.Instantiate(prefab, root.transform);
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                var skinA = a.GetComponentInChildren<SkinnedMeshRenderer>(); var skinB = b.GetComponentInChildren<SkinnedMeshRenderer>(); var original = skinA.sharedMesh;
                var type = TypeOf("PursuitLimbGraft"); var choices = Array.CreateInstance(type, 4);
                for (int i = 0; i < 4; i++) {
                    var choice = Activator.CreateInstance(type);
                    type.GetField("Target").SetValue(choice, Enum.ToObject(TypeOf("PursuitLimb"), i));
                    type.GetField("Source").SetValue(choice, Enum.ToObject(TypeOf("PursuitLimb"), i));
                    type.GetField("DonorModel").SetValue(choice, i); type.GetField("DonorOutfit").SetValue(choice, i);
                    choices.SetValue(choice, i);
                }
                var graftType = TypeOf("PursuitSuturedLimbs"); var apply = graftType.GetMethod("Apply");
                var graftA = a.AddComponent(graftType); var graftB = b.AddComponent(graftType);
                root.SetActive(true);
                apply.Invoke(graftA, new object[] { choices }); apply.Invoke(graftB, new object[] { choices });
                yield return null;
                var block = new MaterialPropertyBlock(); skinA.GetPropertyBlock(block); var atlas = block.GetTexture("_GraftMap") as RenderTexture;
                skinB.GetPropertyBlock(block); Assert.AreSame(atlas, block.GetTexture("_GraftMap"));
                Assert.IsTrue(atlas.IsCreated()); Assert.AreSame(original, skinA.sharedMesh); Assert.AreSame(original, skinB.sharedMesh);
                var before = RenderTexture.active; var pixels = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false, true);
                try {
                    RenderTexture.active = atlas; pixels.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0); pixels.Apply();
                    Assert.Greater(pixels.GetPixels32().Count(p => p.a > 128), 12000, "The player must receive rendered surface data, not just a texture reference");
                } finally { RenderTexture.active = before; Object.Destroy(pixels); }
                Object.Destroy(a); yield return null; yield return null;
                Assert.IsTrue(atlas.IsCreated(), "The surviving actor still owns this surface");
                apply.Invoke(graftB, new object[] { Array.CreateInstance(type, 0) });
                yield return null; yield return null;
                Assert.IsTrue(atlas == null, "Release generated textures when their last user clears the graft");
                skinB.GetPropertyBlock(block); Assert.AreEqual(0, block.GetFloat("_GraftEnabled")); Assert.AreSame(original, skinB.sharedMesh);
            } finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
