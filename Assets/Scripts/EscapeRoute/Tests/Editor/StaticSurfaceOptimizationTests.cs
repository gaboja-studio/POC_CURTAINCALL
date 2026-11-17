using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class StaticSurfaceOptimizationTests
    {
        static Type Tool => AppDomain.CurrentDomain.GetAssemblies().Select(a =>
            a.GetType("CurtainCall.EscapeRoute.EditorTools.EscapeStaticSurfaceOptimization")).First(t => t != null);
        static bool Closed(Vector3[] vertices, int[] triangles) => (bool)Tool.GetMethod("IsClosedBox").Invoke(null,
            new object[] { vertices, triangles, default(Bounds) });
        static List<Rect> Subtract(Rect source, Rect cut) => (List<Rect>)Tool.GetMethod("Subtract").Invoke(null, new object[] { source, cut });
        static List<Rect> Merge(List<Rect> rects) => (List<Rect>)Tool.GetMethod("MergeAdjacent").Invoke(null, new object[] { rects });
        static bool Occludes(Bounds box, int axis, int sign, float plane, bool owns) =>
            (bool)Tool.GetMethod("Occludes").Invoke(null, new object[] { box, axis, sign, plane, owns });

        [Test]
        public void OnlyCompleteSolidGeometryCanHideAnotherSurface()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(Closed(mesh.vertices, mesh.triangles), Is.True);
                Assert.That(Closed(mesh.vertices, mesh.triangles.Skip(3).ToArray()), Is.False, "Missing half-wall must never hide geometry behind it.");
                var duplicated = mesh.triangles;
                Array.Copy(duplicated, 0, duplicated, 3, 3);
                Assert.That(Closed(mesh.vertices, duplicated), Is.False, "A duplicated triangle has correct total area but leaves half a face open.");
                var rotated = mesh.vertices.Select(v => Quaternion.Euler(0, 23, 0) * v).ToArray();
                Assert.That(Closed(rotated, mesh.triangles), Is.False, "Rotated bounds are not a solid occluder.");
                var inverted = mesh.triangles.Reverse().ToArray();
                Assert.That(Closed(mesh.vertices, inverted), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(cube); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SharedExteriorHasOneOwnerWhileInternalTouchingFacesAreRemoved(int axis)
        {
            var box = new Bounds(Vector3.zero, Vector3.one * 2);
            Assert.That(Occludes(box, axis, 1, 1, false), Is.False);
            Assert.That(Occludes(box, axis, 1, 1, true), Is.True);
            Assert.That(Occludes(box, axis, -1, -1, false), Is.False);
            Assert.That(Occludes(box, axis, -1, -1, true), Is.True);
            Assert.That(Occludes(box, axis, 1, -1, false), Is.True);
            Assert.That(Occludes(box, axis, -1, 1, false), Is.True);
            Assert.That(Occludes(box, axis, 1, -1.01f, true), Is.False, "Real gaps stay open.");
            Assert.That(Occludes(box, axis, -1, 1.01f, true), Is.False);
        }

        [Test]
        public void RectangleDifferencePreservesHoleAndDoesNotCreateOverlappingFragments()
        {
            var source = new Rect(0, 0, 10, 10); var hole = new Rect(3, 2, 4, 6);
            var pieces = Merge(Subtract(source, hole));
            Assert.That(pieces.Sum(p => p.width * p.height), Is.EqualTo(76));
            for (int i = 0; i < pieces.Count; i++)
            {
                Assert.That(pieces[i].Overlaps(hole), Is.False);
                for (int j = i + 1; j < pieces.Count; j++) Assert.That(pieces[i].Overlaps(pieces[j]), Is.False);
            }
            Assert.That(Subtract(source, source), Is.Empty);
            Assert.That(Subtract(source, new Rect(10, 0, 5, 5)).Single(), Is.EqualTo(source));
            Assert.That(Merge(pieces.SelectMany(p => Subtract(p, hole)).ToList()).Sum(p => p.width * p.height), Is.EqualTo(76));
        }

        [Test]
        public void FragmentMergingReducesTrianglesWithoutBridgingGaps()
        {
            Assert.That(Merge(new List<Rect> { new(0, 0, 2, 1), new(2, 0, 2, 1), new(0, 1, 4, 1) }).Single(), Is.EqualTo(new Rect(0, 0, 4, 2)));
            Assert.That(Merge(new List<Rect> { new(0, 0, 1, 1), new(1.001f, 0, 1, 1) }).Count, Is.EqualTo(2));
            Assert.That(Merge(new List<Rect> { new(0, 0, 1, 2), new(1, 0, 1, 1) }).Count, Is.EqualTo(2));
        }

        [Test]
        public void SavedOptimizedSceneKeepsPhysicsMeshesSeparateAndAllVisibleMeshReferencesValid()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Levels/escape_route.unity");
            try
            {
                var frame = scene.GetRootGameObjects().Single(g => g.name == "EscapeCampaignFrame");
                bool Optimized(Mesh mesh) => mesh && AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/Scenes/Levels/EscapeFactoryArt/optimized-", StringComparison.Ordinal);
                var visible = frame.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                var edited = visible.Select(r => r.GetComponent<MeshFilter>()).Where(f => f && Optimized(f.sharedMesh)).ToArray();
                Assert.That(edited.Length, Is.GreaterThan(2000), "The production scene must actually reference the optimized meshes.");
                foreach (var filter in edited)
                {
                    var mesh = filter.sharedMesh;
                    Assert.That(mesh.triangles.Length, Is.GreaterThan(0), filter.name);
                    Assert.That(mesh.triangles.All(i => i >= 0 && i < mesh.vertexCount), Is.True, filter.name);
                    Assert.That(mesh.vertices.All(v => float.IsFinite(v.sqrMagnitude)), Is.True, filter.name);
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), filter.name);
                }
                Assert.That(frame.GetComponentsInChildren<MeshCollider>(true).Any(c => Optimized(c.sharedMesh)), Is.False,
                    "Trimmed render shells are open; assigning them to physics would change collisions and NavMesh collection.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
