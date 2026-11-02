using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.Cameras.Tests
{
    public sealed class WaitingRoomCameraCoverageTests
    {
        const string BaseGuid = "0ce9037bd7c44c8b9d57785c26620388";
        const string PursuitGuid = "9d8832c4cb77f9343a74c3dc35c1d3a3";
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static Type Surface => TypeOf("CurtainCall.Cameras.CameraCollisionSurface");
        static Type Authoring => TypeOf("CurtainCall.Cameras.Editor.WaitingRoomCameraCollisionAuthoring");

        [TestCase(BaseGuid)]
        [TestCase(PursuitGuid)]
        public void SavedMap_StructuralFacesBlockBothSidesAndPassagesRemainHollow(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try {
                var targets = (MeshFilter[])Authoring.GetMethod("Collect").Invoke(null, new object[] { scene });
                Assert.Greater(targets.Length, 400, "Audit must include all rooms, connecting corridors and upper decks.");
                int faces = 0, hollowPassages = 0;
                var method = Surface.GetMethod("Intersect");
                foreach (var filter in targets) {
                    string label = AnimationUtility.CalculateTransformPath(filter.transform, null);
                    var surface = filter.GetComponent(Surface);
                    Assert.IsNotNull(surface, label);
                    Assert.IsTrue(((Behaviour)surface).enabled, label);
                    bool meshMode = (bool)Surface.GetField("UseMeshGeometry").GetValue(surface);
                    Assert.AreEqual(filter.sharedMesh.name != "Cube", meshMode, label + " must preserve holes");
                    var vertices = filter.sharedMesh.vertices; var indices = filter.sharedMesh.triangles;
                    int triangles = indices.Length / 3;
                    for (int i = 0; i < triangles; i += Math.Max(1, triangles / 16)) {
                        Vector3 a = filter.transform.TransformPoint(vertices[indices[i * 3]]);
                        Vector3 b = filter.transform.TransformPoint(vertices[indices[i * 3 + 1]]);
                        Vector3 c = filter.transform.TransformPoint(vertices[indices[i * 3 + 2]]);
                        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                        if (normal.sqrMagnitude < .5f) continue;
                        Vector3 center = (a + b + c) / 3;
                        foreach (float side in new[] { -1f, 1f }) {
                            object[] args = { center + normal * (.6f * side), -normal * side, .1f, 0f };
                            Assert.IsTrue((bool)method.Invoke(surface, args), label + " face " + i);
                            Assert.That((float)args[3], Is.InRange(0, .501f), label + " face distance"); faces++;
                        }
                    }
                    if (filter.name != "PassageFloorCeiling" && filter.name != "BranchFloorCeiling") continue;
                    object[] ceiling = { filter.GetComponent<Renderer>().bounds.center, Vector3.up, .1f, 0f };
                    Assert.IsTrue((bool)method.Invoke(surface, ceiling), label);
                    Assert.Greater((float)ceiling[3], .8f, label + " must not fill the corridor with a bounding box");
                    hollowPassages++;
                }
                Assert.AreEqual(7, hollowPassages);
                Assert.Greater(faces, 3000);
                bool dirty = scene.isDirty;
                string result = (string)Authoring.GetMethod("Apply").Invoke(null, new object[] { scene });
                StringAssert.Contains("added=0, updated=0", result);
                Assert.AreEqual(dirty, scene.isDirty, "Repeated repair must not dirty the scene.");
                TestContext.WriteLine($"{scene.name}: {targets.Length} surfaces, {faces} two-sided probes, {hollowPassages} hollow passages.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

    }
}
