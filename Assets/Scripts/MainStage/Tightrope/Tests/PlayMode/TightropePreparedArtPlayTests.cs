using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        // Changes only runtime instances. The saved test scene keeps its original visual references.
        [UnityTest]
        public IEnumerator PreparedArtFollowsSupportRemovalPoleMotionAndSawRebuild()
        {
            yield return BeginPerformance();
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            ((Behaviour)saws).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var profile = Resources.Load<ScriptableObject>("Prefabs/Objects/ETCs/TightropeArtKit/TightropeArtProfile");
            Assert.IsNotNull(profile);
            Set(course, "artProfile", profile);
            Call(course, "BuildMeshes");
            yield return null;
            var generated = (Transform)Get(course, "generated");
            Assert.IsNull(generated.Find("SafetyNet"), "No visible net or hidden catch collider may remain.");
            var platform = generated.Find("StartPlatform");
            Assert.IsNotNull(platform.Find("Prepared Art"));
            Assert.IsTrue(platform.GetComponent<BoxCollider>().enabled);
            Assert.That(platform.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            Assert.IsFalse(platform.GetComponent<MeshRenderer>().enabled);
            // A rebuild disables its old root immediately, but Destroy completes at end of frame.
            Transform Rope() => generated.Cast<Transform>().Single(t => t.name == "Rope0" && t.gameObject.activeInHierarchy);
            var rope = Rope();
            Assert.IsTrue(rope.GetComponentsInChildren<Transform>().Any(t => t.name == "Prepared Art"));
            var support = rope.Find("ContinuousSupport").GetComponent<BoxCollider>();
            Physics.SyncTransforms();
            Assert.IsTrue(support.enabled && support.gameObject.activeInHierarchy);
            Assert.That(support.bounds.size.y, Is.GreaterThan(0));
            float width = (float)Get(Get(course, "Shape"), "RopeWalkWidth");
            Assert.That(support.size.x, Is.EqualTo(width));
            var modules = rope.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Rope module ")).ToArray();
            Assert.That(modules.Length, Is.GreaterThan(1));
            foreach (var module in modules)
            {
                var renderer = module.GetComponentInChildren<MeshRenderer>();
                Assert.That(renderer.bounds.size.z, Is.LessThanOrEqualTo(3.001f));
                Assert.That(renderer.bounds.max.y, Is.EqualTo(support.bounds.max.y).Within(.001f), "Visual top must match rope support height.");
            }
            Assert.IsTrue(rope.GetComponentsInChildren<Transform>().Where(t => t.name == "Prepared Art")
                .All(t => t.GetComponentsInChildren<Collider>().Length == 0));
            Call(course, "SetLaneBlocked", 0, true);
            yield return null;
            Assert.That(Rope().GetComponentsInChildren<MeshRenderer>().Length, Is.Zero,
                "Burning/removing a lane must also remove its authored rope model.");
            Call(course, "SetLaneBlocked", 0, false);
            yield return null;
            Assert.IsTrue(Rope().GetComponentsInChildren<Transform>().Any(t => t.name == "Prepared Art"));

            var poles = Static("CurtainCall.Tightrope.TightropeSwingPole", "Current");
            var pole = (Component)Call(poles, "ForLane", 1);
            Call(pole, "BuildViews");
            yield return null;
            var bar = (Transform)Get(pole, "crossbar");
            Assert.IsNotNull(bar.Find("Prepared Art"));
            Assert.That(bar.Find("Prepared Art").localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(bar.GetComponentsInChildren<Collider>().Length, Is.Zero);

            var prepared = Resources.Load<GameObject>("Prefabs/Objects/ETCs/TightropeArtKit/Prepared/TightropeSawsPrepared");
            var source = prepared.GetComponent(GameType("CurtainCall.Tightrope.Saws.TightropeSaws"));
            foreach (string field in new[] { "horizontalPrefab", "verticalPrefab", "platePrefab", "cooperativeGatePrefab", "cooperativePlatePrefab" })
                Set(saws, field, Get(source, field));
            Call(saws, "BuildViews");
            yield return null;
            var view = saws.transform.Find("Generated/HorizontalSaw01");
            Assert.IsNotNull(view);
            Assert.That(view.GetComponentsInChildren<Collider>().Length, Is.Zero);
            Assert.IsNotNull(view.Find("Fit"), "The authored model must survive the real view-building path.");
        }
    }
}
