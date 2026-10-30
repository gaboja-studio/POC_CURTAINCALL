using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    /// <summary>Actual authored curve, thin swept blade contact, persistent split geometry and owned mesh lifecycle.</summary>
    public sealed class CuttableCableTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly List<GameObject> roots = new();
        static Type CableType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeCuttableCable")).First(t => t != null);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target)
            ?? target.GetType().GetField(name, Flags)?.GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Flags)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        Component Cable(params Vector3[] points)
        {
            var root = new GameObject("Offline authored cable") { hideFlags = HideFlags.DontSave };
            root.SetActive(false); roots.Add(root);
            var cable = root.AddComponent(CableType);
            Set(cable, "localPoints", points.Length > 0 ? points : new[] { Vector3.down * 2, Vector3.up * 2 });
            Call(cable, "Rebuild"); return cable;
        }
        static bool Disk(Component cable, Vector3 from, Vector3 to, Vector3 normal, out float time, out Vector3 point)
        {
            object[] args = { from, to, normal, .55f, .12f, 0f, Vector3.zero };
            bool hit = (bool)Call(cable, "TryIntersectDisk", args); time = (float)args[5]; point = (Vector3)args[6]; return hit;
        }
        static bool Sphere(Component cable, Vector3 from, Vector3 to, float radius)
        {
            object[] args = { from, to, radius, 0f }; return (bool)Call(cable, "TryIntersect", args);
        }
        static int ShellComponents(Mesh mesh)
        {
            int[] triangles = mesh.GetTriangles(0);
            var links = new Dictionary<int, HashSet<int>>();
            for (int i = 0; i < triangles.Length; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    int a = triangles[i + j], b = triangles[i + (j + 1) % 3];
                    if (!links.ContainsKey(a)) links[a] = new HashSet<int>();
                    if (!links.ContainsKey(b)) links[b] = new HashSet<int>();
                    links[a].Add(b); links[b].Add(a);
                }
            int components = 0; var visited = new HashSet<int>(); var pending = new Stack<int>();
            foreach (int seed in links.Keys)
            {
                if (!visited.Add(seed)) continue;
                components++; pending.Push(seed);
                while (pending.Count > 0)
                    foreach (int neighbor in links[pending.Pop()]) if (visited.Add(neighbor)) pending.Push(neighbor);
            }
            return components;
        }
        [TearDown]
        public void TearDown()
        {
            for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
            roots.Clear();
        }

        [Test]
        public void ThinBladeRejectsAdjacentStrandInsideItsSphericalBroadPhase()
        {
            var cable = Cable(new Vector3(.4f, -2, 0), new Vector3(.4f, 2, 0));
            Vector3 from = Vector3.back * 5, to = Vector3.forward * 5;
            Assert.That(Sphere(cable, from, to, .55f), Is.True);
            Assert.That(Disk(cable, from, to, Vector3.right, out _, out _), Is.False,
                "A disk only .24 m thick must not cut an adjacent strand .4 m off its plane.");
        }

        [TestCase(2f)]
        [TestCase(100f)]
        public void EdgeFirstSweepFindsFirstContactWithoutTunneling(float halfTravel)
        {
            var cable = Cable();
            Assert.That(Disk(cable, Vector3.back * halfTravel, Vector3.forward * halfTravel,
                Vector3.right, out float time, out Vector3 point), Is.True);
            Assert.That(time, Is.EqualTo(.5f - .62f / (2 * halfTravel)).Within(.00015f));
            Assert.That(point.x, Is.EqualTo(0).Within(.001f)); Assert.That(point.z, Is.EqualTo(0).Within(.001f));
            Assert.That(point.y, Is.EqualTo(0).Within(.01f), "Return the wire contact, not the projectile center.");
        }

        [Test]
        public void RoundedWireAtDiskRimUsesBothThicknessAndRadialDistance()
        {
            var cable = Cable(new Vector3(.18f, -2, 0), new Vector3(.18f, 2, 0));
            Assert.That(Disk(cable, Vector3.back * 3, Vector3.forward * 3, Vector3.right, out float time, out _), Is.True);
            float radialReach = .55f + Mathf.Sqrt(.07f * .07f - .06f * .06f);
            Assert.That(time, Is.EqualTo((3 - radialReach) / 6).Within(.0002f),
                "A rounded cable touching the disk corner must not inherit the full broad-phase sphere radius.");
        }

        [Test]
        public void CurveContactFollowsSampledBendAndCurrentWorldTransform()
        {
            var cable = Cable(new Vector3(-2, 0, 0), new Vector3(-.8f, 2, 0), new Vector3(.8f, 2, 0), new Vector3(2, 0, 0));
            Assert.That(Sphere(cable, new Vector3(0, 0, -3), new Vector3(0, 0, 3), .1f), Is.False,
                "The straight endpoint chord is not an exposed cable.");
            var path = (Vector3[])Get(cable, "path"); Vector3 localContact = path[path.Length / 2];
            // Move after rebuilding: querying a cached local curve still follows the live authored transform.
            cable.transform.SetPositionAndRotation(new Vector3(385, 1, 78), Quaternion.Euler(0, 67, 0));
            Vector3 center = cable.transform.TransformPoint(localContact), direction = cable.transform.forward;
            Assert.That(Disk(cable, center - direction * 3, center + direction * 3, cable.transform.right,
                out _, out Vector3 hit), Is.True);
            Assert.That(Vector3.Distance(hit, center), Is.LessThan(.02f));
        }

        [Test]
        public void CuttingSplitsRealTubeIntoTwoAnchoredEndsAndResetRestoresContact()
        {
            var cable = Cable(); var mesh = cable.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(ShellComponents(mesh), Is.EqualTo(1)); int originalCaps = mesh.GetTriangles(1).Length;
            Vector3 originalStart = AverageRing(mesh.vertices, 0, 8);
            Assert.That(Call(cable, "ApplyCut", new Vector3(0, .13f, 0), false), Is.True);
            Assert.That(Call(cable, "ApplyCut", Vector3.zero, false), Is.False);
            Assert.That(ShellComponents(mesh), Is.EqualTo(2));
            Assert.That(mesh.GetTriangles(1).Length, Is.EqualTo(originalCaps * 2), "Both severed ends expose copper caps.");
            Assert.That(Vector3.Distance(AverageRing(mesh.vertices, 0, 8), originalStart), Is.LessThan(.0001f));
            Assert.That(Disk(cable, Vector3.back * 3, Vector3.forward * 3, Vector3.right, out _, out _), Is.False);
            Call(cable, "SetCut", false, Vector3.zero, false);
            Assert.That(ShellComponents(mesh), Is.EqualTo(1)); Assert.That(mesh.GetTriangles(1).Length, Is.EqualTo(originalCaps));
            Assert.That(Disk(cable, Vector3.back * 3, Vector3.forward * 3, Vector3.right, out _, out _), Is.True);
        }

        [Test]
        public void RepeatedAuthoritativeSnapshotDoesNotRestartOrRebuildCutAnimation()
        {
            var cable = Cable(); Vector3 point = new(0, .13f, 0);
            Call(cable, "SetCut", true, point, false);
            Vector3[] settledVertices = cable.GetComponent<MeshFilter>().sharedMesh.vertices;
            double start = Time.unscaledTimeAsDouble - .45; Set(cable, "cutAt", start); Set(cable, "settled", false);
            Call(cable, "SetCut", true, point, true);
            Assert.That(Get(cable, "cutAt"), Is.EqualTo(start));
            CollectionAssert.AreEqual(settledVertices, cable.GetComponent<MeshFilter>().sharedMesh.vertices,
                "Repeated 10 Hz snapshots must leave mesh animation to its bounded Update, without recreating a pose.");
        }

        [Test]
        public void InvalidCurveAndInvalidBladeNeverProduceContacts()
        {
            var cable = Cable(Vector3.zero, new Vector3(float.NaN, 0, 0));
            Assert.That(cable.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.Zero);
            Assert.That(Disk(cable, Vector3.back, Vector3.forward, Vector3.right, out _, out _), Is.False);
            Assert.That(Call(cable, "ApplyCut", Vector3.zero, false), Is.False);
            Set(cable, "localPoints", new[] { Vector3.down, Vector3.up }); Call(cable, "Rebuild");
            Assert.That(Disk(cable, Vector3.back, Vector3.forward, Vector3.zero, out _, out _), Is.False);
            Assert.That(Disk(cable, new Vector3(float.NaN, 0, 0), Vector3.forward, Vector3.right, out _, out _), Is.False);
        }

        [Test]
        public void DisablingAndRebuildingOwnsExactlyOneDisposableMesh()
        {
            var cable = Cable(); var filter = cable.GetComponent<MeshFilter>(); Mesh first = filter.sharedMesh;
            Call(cable, "Rebuild"); Assert.That(filter.sharedMesh, Is.SameAs(first));
            Call(cable, "OnDisable"); Assert.That(first == null, Is.True); Assert.That(filter.sharedMesh, Is.Null);
            Call(cable, "Rebuild"); Mesh second = filter.sharedMesh; Assert.That(second, Is.Not.Null);
            Call(cable, "OnDestroy"); Assert.That(second == null, Is.True); Assert.That(filter.sharedMesh, Is.Null);
        }

        static Vector3 AverageRing(Vector3[] vertices, int offset, int count)
        {
            Vector3 sum = Vector3.zero; for (int i = 0; i < count; i++) sum += vertices[offset + i]; return sum / count;
        }
    }
}
