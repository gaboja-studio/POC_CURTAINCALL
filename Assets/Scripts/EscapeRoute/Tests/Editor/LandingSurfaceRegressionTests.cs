using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class LandingSurfaceRegressionTests
    {
        static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        static string[] Check(Transform frame) => (string[])FindType("CurtainCall.EscapeRoute.EditorTools.EscapeLandingSurfaceAudit")
            .GetMethod("FindCoincidentLandingFaces").Invoke(null, new object[] { frame });

        [TestCase(false)] [TestCase(true)]
        public void FunctionalLandingFloorIsAuditedRegardlessOfDecorationName(bool disableRuntimeReadWrite)
        {
            var root = new GameObject("Surface regression");
            Mesh importedStyleMesh = null;
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(root.transform, false);
                floor.AddComponent(FindType("CurtainCall.HumanCannon.HumanCannonLandingZone"));
                var decoration = GameObject.CreatePrimitive(PrimitiveType.Cube);
                decoration.name = "An unrelated name";
                decoration.transform.SetParent(root.transform, false);
                if (disableRuntimeReadWrite)
                {
                    var filter = decoration.GetComponent<MeshFilter>();
                    importedStyleMesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    filter.sharedMesh = importedStyleMesh;
                    importedStyleMesh.UploadMeshData(true);
                }
                Assert.That(Check(root.transform), Has.Length.EqualTo(1), "A landing component must not exclude this floor from geometry inspection.");
                decoration.transform.localPosition = Vector3.up * .02f;
                Assert.That(Check(root.transform), Is.Empty, "Separated surfaces are not exact coplanar duplicates.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (importedStyleMesh) UnityEngine.Object.DestroyImmediate(importedStyleMesh);
            }
        }

        [Test]
        public void ProductionLandingFloorsHaveNoDuplicatedTopFaces()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Levels/escape_route.unity");
            try
            {
                var frame = scene.GetRootGameObjects().Single(g => g.name == "EscapeCampaignFrame").transform;
                Assert.That(Check(frame), Is.Empty);
                Assert.That(frame.GetComponentsInChildren<Transform>(true).Any(t => t.name == "층위를 드러내는 벽체 받침"), Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
