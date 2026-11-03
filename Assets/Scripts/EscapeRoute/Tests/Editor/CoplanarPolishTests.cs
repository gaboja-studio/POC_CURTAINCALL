using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class CoplanarPolishTests
    {
        static Type Tool => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute.EditorTools.EscapeCoplanarPolish")).First(t => t != null);
        static Vector3 P(float x, float y) => new(x, y, 13);
        static List<Vector3> Subject => new() { P(0, 0), P(2, 0), P(0, 2) };
        static List<List<Vector3>> Difference(List<Vector3> subject, Vector3[] cutter) =>
            (List<List<Vector3>>)Tool.GetMethod("Subtract").Invoke(null, new object[] { subject, cutter, 2 });
        static double Area(List<List<Vector3>> pieces) => pieces.Sum(p =>
            (double)Tool.GetMethod("Area").Invoke(null, new object[] { p, 2 }));

        [TestCase(false)] [TestCase(true)]
        public void PartialCoverRemovesOnlySharedAreaAndPreservesOriginalPlane(bool reverse)
        {
            var cutter = new[] { P(0, 0), P(1, 0), P(0, 1) }; if (reverse) Array.Reverse(cutter);
            var pieces = Difference(Subject, cutter);
            Assert.That(Area(pieces), Is.EqualTo(1.5).Within(.00001));
            foreach (var point in pieces.SelectMany(p => p))
            {
                Assert.That(point.z, Is.EqualTo(13)); Assert.That(point.x, Is.GreaterThanOrEqualTo(-.00001));
                Assert.That(point.y, Is.GreaterThanOrEqualTo(-.00001)); Assert.That(point.x + point.y, Is.LessThanOrEqualTo(2.00001));
            }
        }
        [Test]
        public void TouchingAndDisjointFacesKeepAreaWhileCompleteCoverRemovesTheDuplicate()
        {
            Assert.That(Area(Difference(Subject, new[] { P(2, 0), P(3, 0), P(2, 1) })), Is.EqualTo(2));
            Assert.That(Area(Difference(Subject, new[] { P(4, 0), P(5, 0), P(4, 1) })), Is.EqualTo(2));
            Assert.That(Difference(Subject, new[] { P(-1, -1), P(5, -1), P(-1, 5) }), Is.Empty);
        }
        [Test]
        public void MultipleOccludersPreserveTheUncoveredSurfaceWithoutDoubleRemovingIt()
        {
            var first = Difference(Subject, new[] { P(0, 0), P(1, 0), P(0, 1) });
            var next = first.SelectMany(p => Difference(p, new[] { P(1, 0), P(2, 0), P(1, 1) })).ToList();
            Assert.That(Area(next), Is.EqualTo(1).Within(.00001));
            var duplicateCut = next.SelectMany(p => Difference(p, new[] { P(1, 0), P(2, 0), P(1, 1) })).ToList();
            Assert.That(Area(duplicateCut), Is.EqualTo(1).Within(.00001));
        }

        [Test]
        public void CuboidInspectionUsesActualGeometryAfterArtImportRenamesTheMesh()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var filter = cube.GetComponent<MeshFilter>(); var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.name = "factory-concrete-derived-surface"; filter.sharedMesh = mesh;
            var builder = AppDomain.CurrentDomain.GetAssemblies().Select(a =>
                a.GetType("CurtainCall.EscapeRoute.EditorTools.EscapeBackgroundBuilder")).First(t => t != null);
            var method = builder.GetMethod("IsPolishBox", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                Assert.That(method.Invoke(null, new object[] { filter }), Is.True);
                var vertices = mesh.vertices; vertices[0].x += .1f; mesh.vertices = vertices; mesh.RecalculateBounds();
                Assert.That(method.Invoke(null, new object[] { filter }), Is.False, "A similarly sized custom silhouette must remain intact.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cube); UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
