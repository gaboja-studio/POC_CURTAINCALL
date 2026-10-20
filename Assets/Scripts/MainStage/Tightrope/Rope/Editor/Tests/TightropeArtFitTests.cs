using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Tightrope.Saws;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeArtFitTests
    {
        readonly List<GameObject> owned = new();
        GameObject Own(GameObject value) { owned.Add(value); return value; }
        GameObject Source()
        {
            var root = Own(new GameObject("Offset source"));
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(3, -2, 4);
            mesh.transform.localScale = new Vector3(2, 3, 5);
            return root;
        }
        [TearDown] public void Cleanup()
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void FittingPreservesColliderAndCentersOffsetMeshInMovingHolder()
        {
            var source = Source();
            var holder = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            holder.transform.SetPositionAndRotation(new Vector3(4, 7, -2), Quaternion.Euler(26, 47, 80));
            holder.transform.localScale = new Vector3(.2f, .2f, 30);
            var collider = holder.GetComponent<BoxCollider>();
            var originalMesh = source.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(TightropeArtFit.Box(holder.transform, source, Color.cyan), Is.True);
            Assert.That(holder.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            Assert.That(holder.GetComponent<BoxCollider>(), Is.SameAs(collider));
            Assert.That(collider.enabled, Is.True);
            Assert.That(collider.size, Is.EqualTo(Vector3.one));
            Assert.That(holder.GetComponent<Renderer>().enabled, Is.False);
            var bounds = TightropeArtFit.LocalBounds(holder.transform.Find("Prepared Art"));
            Assert.That(Vector3.Distance(bounds.center, Vector3.zero), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(bounds.size, Vector3.one), Is.LessThan(.0001f));
            Assert.That(source.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
            Assert.That(source.transform.GetChild(0).localPosition, Is.EqualTo(new Vector3(3, -2, 4)));
        }

        [Test]
        public void UnsafeVisualFallsBackToExistingGeometry()
        {
            var holder = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var unsafeSource = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Assert.That(TightropeArtFit.Box(holder.transform, unsafeSource), Is.False);
            Assert.That(holder.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(holder.transform.Find("Prepared Art"), Is.Null);
            Assert.That(TightropeArtFit.Box(holder.transform, Source()), Is.True);
            Assert.That(TightropeArtFit.Box(holder.transform, null), Is.False);
            Assert.That(holder.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(holder.transform.Find("Prepared Art").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void AnimatedLineKeepsEndpointsWithoutAccumulatingModelsOrChangingMaterial()
        {
            var source = Source();
            var holder = Own(new GameObject("Line", typeof(LineRenderer)));
            var line = holder.GetComponent<LineRenderer>(); line.startWidth = line.endWidth = .04f;
            var material = source.GetComponentInChildren<Renderer>().sharedMaterial;
            Color original = material.color;
            for (int i = 0; i < 20; i++)
            {
                Vector3 from = new Vector3(i, 7, 4), to = from + Quaternion.Euler(i * 8, 0, 0) * Vector3.up * (i + 1);
                Assert.That(TightropeArtFit.Line(line, source, from, to, Color.green), Is.True);
                var art = holder.transform.Find("Prepared Art");
                Assert.That(Vector3.Distance(art.TransformPoint(Vector3.down * .5f), from), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(art.TransformPoint(Vector3.up * .5f), to), Is.LessThan(.0001f));
                Assert.That(holder.transform.childCount, Is.EqualTo(1));
                var block = new MaterialPropertyBlock(); art.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.green));
            }
            Assert.That(material.color, Is.EqualTo(original));
            TightropeArtFit.Line(line, source, Vector3.zero, Vector3.zero, Color.red);
            Assert.That(holder.transform.Find("Prepared Art").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void AuthoredGateNeedsNoBladeChildAndKeepsIndependentCylinderCollider()
        {
            var holder = Own(new GameObject("Saws"));
            var saws = holder.AddComponent<TightropeSaws>();
            var source = Source();
            typeof(TightropeSaws).GetField("cooperativeGatePrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(saws, source);
            object[] args = { holder.transform, "Gate", null, null };
            var gate = (Transform)typeof(TightropeSaws).GetMethod("BuildGateDisc", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(saws, args);
            var collider = (MeshCollider)args[2];
            Assert.That(collider.sharedMesh, Is.SameAs(TightropeArtFit.UnitCylinder));
            Assert.That(collider.convex, Is.True);
            Assert.That(gate.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            Assert.That(collider.transform.localScale.x, Is.EqualTo(saws.Settings.cooperativeGateHeight));
            Assert.That(collider.transform.localScale.y * 2, Is.EqualTo(saws.Settings.cooperativeGateThickness));
            Assert.That(((Transform)args[3]).Find("Blade"), Is.Null);
        }
    }
}
