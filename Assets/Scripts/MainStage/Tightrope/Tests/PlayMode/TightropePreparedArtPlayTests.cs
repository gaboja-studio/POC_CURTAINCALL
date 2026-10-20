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
            var platform = generated.Find("StartPlatform");
            Assert.IsNotNull(platform.Find("Prepared Art"));
            Assert.IsTrue(platform.GetComponent<BoxCollider>().enabled);
            Assert.That(platform.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            Assert.IsFalse(platform.GetComponent<MeshRenderer>().enabled);
            var rope = generated.Find("Rope0");
            Assert.IsTrue(rope.GetComponentsInChildren<Transform>().Any(t => t.name == "Prepared Art"));
            var support = rope.Find("ContinuousSupport").GetComponent<BoxCollider>();
            float width = (float)Get(Get(course, "Shape"), "RopeWalkWidth");
            Assert.That(support.size.x, Is.EqualTo(width));
            Assert.IsTrue(rope.GetComponentsInChildren<Transform>().Where(t => t.name == "Prepared Art")
                .All(t => t.GetComponentsInChildren<Collider>().Length == 0));
            Call(course, "SetLaneBlocked", 0, true);
            yield return null;
            Assert.That(generated.Find("Rope0").GetComponentsInChildren<MeshRenderer>().Length, Is.Zero,
                "Burning/removing a lane must also remove its authored rope model.");
            Call(course, "SetLaneBlocked", 0, false);
            yield return null;
            Assert.IsTrue(generated.Find("Rope0").GetComponentsInChildren<Transform>().Any(t => t.name == "Prepared Art"));

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
