using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.UI.Wayfinding.Editor
{
    public sealed class NavigationContractTests
    {
        [Test]
        public void ObjectEndpointTracksMovementAndNeverFallsBackAfterDestruction()
        {
            var go = new GameObject("Endpoint test");
            NavigationTarget endpoint = go;
            try
            {
                go.transform.position = new Vector3(1, 2, 3);
                Assert.IsTrue(endpoint.TryGetPosition(out var position));
                Assert.AreEqual(go.transform.position, position);
                go.transform.position = Vector3.one * 7;
                Assert.IsTrue(endpoint.TryGetPosition(out position));
                Assert.AreEqual(go.transform.position, position);
                go.SetActive(false);
                Assert.IsFalse(endpoint.TryGetPosition(out _));
            }
            finally { Object.DestroyImmediate(go); }
            Assert.IsFalse(endpoint.TryGetPosition(out _));
        }

        [Test]
        public void InvalidPositionIsRejected()
        {
            Assert.IsFalse(NavigationTarget.Position(new Vector3(float.NaN, 0, 0)).TryGetPosition(out _));
            Assert.IsFalse(NavigationTarget.Position(new Vector3(0, float.PositiveInfinity, 0)).TryGetPosition(out _));
            Assert.IsTrue(NavigationTarget.Position(Vector3.zero).TryGetPosition(out var result));
            Assert.AreEqual(Vector3.zero, result);
        }

        [Test]
        public void TintIsIndependentAndEmphasisDoesNotCompound()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/Navigation/V2/PulseArrow");
            var go = Object.Instantiate(prefab);
            try
            {
                var glyph = go.GetComponent<NavigationPulseGlyph>();
                glyph.SetAppearance(Color.magenta, .5f);
                glyph.Apply(.8f);
                Assert.AreEqual(.4f, glyph.Opacity, .001f);
                glyph.SetAppearance(Color.yellow, .5f);
                Assert.AreEqual(.4f, glyph.Opacity, .001f);
                foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>())
                {
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    Assert.AreEqual(Color.yellow, block.GetColor("_SignalColor"));
                    bool contrast = renderer.color.r + renderer.color.g + renderer.color.b <= .1f;
                    Assert.AreEqual(contrast ? 0 : 1, block.GetFloat("_UseSignalTint"));
                }
                glyph.SetAppearance(Color.white, 0);
                Assert.AreEqual(0, glyph.Opacity);
                glyph.SetAppearance(Color.white, 1);
                Assert.AreEqual(.8f, glyph.Opacity, .001f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
