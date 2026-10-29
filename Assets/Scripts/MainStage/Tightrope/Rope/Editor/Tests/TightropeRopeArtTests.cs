using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeRopeArtTests
    {
        const string Kit = "Assets/Resources/Prefabs/Objects/ETCs/TightropeArtKit/";

        [TestCase(2f, 1)]
        [TestCase(6f, 2)]
        [TestCase(10f, 4)]
        public void RopeRepeatsAtMostThreeMetresWithoutChangingSupportOrMaterial(float length, int count)
        {
            var holder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                holder.transform.localScale = new Vector3(.2f, .2f, length);
                holder.transform.localPosition = new Vector3(2, -.1f, 8);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "WalkingRope.prefab");
                Assert.That(TightropeArtFit.Rope(holder.transform, source), Is.True);
                var art = holder.transform.Find("Prepared Art");
                Assert.That(art.childCount, Is.EqualTo(count));
                foreach (Transform module in art)
                {
                    var r = module.GetComponentInChildren<MeshRenderer>();
                    Assert.That(r.bounds.size.z, Is.LessThanOrEqualTo(3.001f));
                    Assert.That(r.bounds.max.y, Is.EqualTo(0f).Within(.0001f));
                    Assert.That(r.sharedMaterial, Is.SameAs(source.GetComponentInChildren<MeshRenderer>().sharedMaterial));
                    Assert.IsFalse(r.HasPropertyBlock());
                }
                Assert.That(TightropeArtFit.LocalBounds(art).size.z, Is.EqualTo(1).Within(.0001f));
                Assert.That(holder.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
                Assert.IsTrue(holder.GetComponent<Collider>().enabled);
                TightropeArtFit.Rope(holder.transform, source);
                Assert.That(art.childCount, Is.EqualTo(count), "Repeated calls cannot accumulate modules.");
            }
            finally { Object.DestroyImmediate(holder); }
        }

        [Test]
        public void PoleChangesOnlyGripColourAndKeepsSteelBodyUntinted()
        {
            var holder = new GameObject("Pole holder");
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "PoleBarUnitX.prefab");
                TightropeArtFit.Box(holder.transform, source, Color.green);
                foreach (var r in holder.GetComponentsInChildren<MeshRenderer>())
                {
                    var block = new MaterialPropertyBlock(); r.GetPropertyBlock(block);
                    if (r.name == "MaterialBody") Assert.IsFalse(block.HasColor("_BaseColor"));
                    else Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.green));
                }
            }
            finally { Object.DestroyImmediate(holder); }
        }
    }
}
