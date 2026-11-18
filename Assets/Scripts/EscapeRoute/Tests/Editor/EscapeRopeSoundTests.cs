using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class EscapeRopeSoundTests
    {
        static readonly Vector3 Origin = new(23871, 613, 17331);
        GameObject root;
        BoxCollider rope;
        static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name)).First(type => type != null);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Rope sound fixture");
            root.transform.position = Origin;
            rope = Box("Rope", new Vector3(5, 0, 0), new Vector3(8, .2f, .2f));
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        BoxCollider Box(string name, Vector3 position, Vector3 size)
        {
            var item = new GameObject(name);
            item.transform.SetParent(root.transform, false);
            item.transform.localPosition = position;
            var box = item.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        bool Audible(Vector3 localListener, out Vector3 point, Transform listenerRoot = null, params Collider[] supports)
        {
            Physics.SyncTransforms();
            object[] args = { supports.Length == 0 ? new Collider[] { rope } : supports,
                Origin + localListener, 8f, listenerRoot, Vector3.zero };
            bool result = (bool)RuntimeType("CurtainCall.EscapeRoute.EscapeCooperativeRopes").GetMethod("TryFindAudibleSupport",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            point = (Vector3)args[4];
            return result;
        }

        [Test]
        public void NearbyVisibleRopeIsAudibleAndDistantRopeIsSilent()
        {
            Assert.IsTrue(Audible(new Vector3(0, 1.2f, 0), out _));
            Assert.IsFalse(Audible(new Vector3(-15, 1.2f, 0), out _));
        }

        [Test]
        public void SolidWallBlocksNearbyRopeButTriggerDoesNot()
        {
            var wall = Box("Wall", new Vector3(.5f, 1, 0), new Vector3(.15f, 4, 4));
            Assert.IsFalse(Audible(new Vector3(0, 1.2f, 0), out _));
            wall.isTrigger = true;
            Assert.IsTrue(Audible(new Vector3(0, 1.2f, 0), out _));
        }

        [Test]
        public void DisappearedRopeRetainsItsAuthoredSoundPosition()
        {
            rope.enabled = false;
            Assert.IsTrue(Audible(new Vector3(0, 1.2f, 0), out var point));
            Assert.That(point.x - Origin.x, Is.EqualTo(1).Within(.01f));
        }

        [Test]
        public void ListenerColliderDoesNotBlockItsOwnRopeSound()
        {
            var listener = Box("Listener", new Vector3(.5f, 1, 0), new Vector3(.15f, 4, 4));
            Assert.IsTrue(Audible(new Vector3(0, 1.2f, 0), out _, listener.transform));
        }

        [Test]
        public void VisibleSupportIsChosenWhenClosestSupportIsBehindWall()
        {
            Box("Wall", new Vector3(.5f, 1, 0), new Vector3(.15f, 4, 4));
            var visible = Box("Visible rope", new Vector3(0, 0, 3), new Vector3(.2f, .2f, 1));
            Assert.IsTrue(Audible(new Vector3(0, 1.2f, 0), out var point, null, rope, visible));
            Assert.That(point.z - Origin.z, Is.GreaterThan(2));
        }

        [Test]
        public void FactoryImpactLoopIsRemovedAndFootstepsRemainConfigured()
        {
            var catalog = AssetDatabase.LoadAssetAtPath("Assets/Resources/Settings/Sound/SoundCatalog.asset",
                RuntimeType("CurtainCall.Sound.SoundCatalog"));
            var find = catalog.GetType().GetMethod("Find");
            Assert.IsNull(find.Invoke(catalog, new object[] { "ESC-028" }));
            var step = find.Invoke(catalog, new object[] { "ESC-003" });
            Assert.AreEqual(2, ((Array)step.GetType().GetField("clips").GetValue(step)).Length);
            Assert.AreEqual(.65f, step.GetType().GetField("volume").GetValue(step));
            Assert.AreEqual(70f, step.GetType().GetField("maxDistance").GetValue(step));
        }
    }
}
