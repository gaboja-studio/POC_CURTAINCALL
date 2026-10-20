using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropePreparedAssetsTests
    {
        const string Kit = "Assets/Resources/Prefabs/Objects/ETCs/TightropeArtKit/";
        static GameObject Asset(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".prefab");

        [TestCase("HorizontalSaw", 1.2f, .1f)]
        [TestCase("IncomingSaw", 1.2f, .1f)]
        [TestCase("CooperativeSaw", 2.1f, .2f)]
        public void SawModelsFitTheirExistingEnvelopesAndHaveNoPhysics(string name, float diameter, float depth)
        {
            var root = Asset(name);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            float radial = 0;
            var bounds = TightropeArtFit.LocalBounds(root.transform);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Assert.That(filter.sharedMesh, Is.Not.Null);
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var p = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    radial = Mathf.Max(radial, new Vector2(p.x, p.z).magnitude);
                    Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(depth * .5f + .0001f));
                }
            }
            Assert.That(radial, Is.EqualTo(diameter * .5f).Within(.0001f));
            Assert.That(bounds.center.magnitude, Is.LessThan(.0001f));
            Assert.That(bounds.size.y, Is.EqualTo(depth).Within(.0001f));
        }

        [TestCase("ReleasePlate", .8f)]
        [TestCase("CooperativePlate", 1.6f)]
        public void PlatesFitTheExistingMathematicalContactArea(string name, float length)
        {
            var root = Asset(name);
            var bounds = TightropeArtFit.LocalBounds(root.transform);
            Assert.That(Vector3.Distance(bounds.size, new Vector3(.8f, .04f, length)), Is.LessThan(.0001f));
            Assert.That(bounds.center.magnitude, Is.LessThan(.0001f));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [TestCase("PoleBarUnitX")]
        [TestCase("PoleBarUnitY")]
        [TestCase("CableUnitY")]
        public void MovingPolePartsHaveCenteredUnitBounds(string name)
        {
            var bounds = TightropeArtFit.LocalBounds(Asset(name).transform);
            Assert.That(bounds.center.magnitude, Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(bounds.size, Vector3.one), Is.LessThan(.0001f));
        }

        [Test]
        public void PreparedProfileIsOptInAndAllItsModelsAreVisualOnly()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TightropeArtProfile>(Kit + "TightropeArtProfile.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.safetyNet, Is.Null, "The course must not connect an obsolete safety net.");
            foreach (var source in new[] { profile.floor, profile.rope, profile.barX, profile.barY, profile.cableY })
            {
                Assert.That(source, Is.Not.Null);
                Assert.That(source.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(source.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(source.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            }
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Objects/Interactables/Tightrope/TightropeCourse.prefab");
            Assert.That(original.GetComponent<TightropeCourse>().ArtProfile, Is.Null);
            Assert.That(Asset("Prepared/TightropeCoursePrepared").GetComponent<TightropeCourse>().ArtProfile, Is.SameAs(profile));
        }

        [Test]
        public void AllKitReferencesAndSourceMeshesResolveWithoutSceneObjects()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] {
                Kit.TrimEnd('/'), "Assets/Resources/Prefabs/Objects/ETCs/PerformanceArtKit" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero, path);
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) Assert.That(filter.sharedMesh, Is.Not.Null, path);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.That(renderer.sharedMaterials.Length, Is.GreaterThan(0), path);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, path);
                        Assert.That(material.shader != null && material.shader.isSupported, Is.True, path);
                    }
                }
                foreach (var component in root.GetComponentsInChildren<Component>(true).Where(c => c != null))
                {
                    var iterator = new SerializedObject(component).GetIterator();
                    while (iterator.Next(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = iterator.objectReferenceValue;
                        Assert.That(reference != null || iterator.objectReferenceInstanceIDValue == 0, Is.True, path + ": " + iterator.propertyPath);
                        if (reference is Component part && !EditorUtility.IsPersistent(part)) Assert.Fail(path + ": scene reference " + iterator.propertyPath);
                        if (reference is GameObject go && !EditorUtility.IsPersistent(go)) Assert.Fail(path + ": scene reference " + iterator.propertyPath);
                    }
                }
            }
        }
    }
}
