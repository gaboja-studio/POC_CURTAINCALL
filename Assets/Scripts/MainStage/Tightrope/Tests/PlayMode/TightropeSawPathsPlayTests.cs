using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator SawLaserPathsFollowActualRoutesAndResetWithoutAddingCollision()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            Call(saws, "UpdateViews");
            var paths = saws.transform.Find("Generated/Saw paths (visual only)");
            Assert.IsNotNull(paths);
            Assert.That(paths.GetComponentsInChildren<Collider>(true), Is.Empty);
            var horizontal = paths.GetComponentsInChildren<LineRenderer>().Where(l => l.name.StartsWith("Horizontal path ")).ToArray();
            Assert.That(horizontal.Length, Is.EqualTo((int)Get(saws, "HorizontalCount")));
            Assert.That(horizontal.Count(line => line.enabled), Is.EqualTo((int)Get(saws, "ActiveHorizontalCount")));
            float halfWidth = (float)Get(settings, "horizontalTravelHalfWidth");
            Vector3 right = (Vector3)Get(course, "Right");
            foreach (var line in horizontal.Where(line => line.enabled))
            {
                Assert.That(Vector3.Distance(line.GetPosition(0), line.GetPosition(1)),
                    Is.EqualTo(halfWidth * 2 / Mathf.Cos(30f * Mathf.Deg2Rad)).Within(.001f));
                Assert.That(Vector3.Dot((line.GetPosition(1) - line.GetPosition(0)).normalized, right),
                    Is.EqualTo(Mathf.Cos(30f * Mathf.Deg2Rad)).Within(.001f));
                Assert.That(line.startWidth, Is.LessThan(.1f), "Thin guides must remain distinct from walkable ropes.");
                Color color = line.sharedMaterial.color;
                Assert.That(color.r, Is.GreaterThan(color.g * 10));
            }
            int firstIndex = System.Array.FindIndex(horizontal, line => line.enabled);
            var first = horizontal[firstIndex];
            Vector3 midpoint = (first.GetPosition(0) + first.GetPosition(1)) * .5f;
            Vector3 blade = (Vector3)Call(saws, "GetHorizontalPosition", firstIndex);
            Vector3 axis = (first.GetPosition(1) - first.GetPosition(0)).normalized;
            Assert.That(Vector3.Cross(blade - midpoint, axis).magnitude, Is.LessThan(.001f), "The saw must travel on the displayed diagonal axis.");
            var incoming = paths.Find("Incoming path").GetComponent<LineRenderer>();
            Assert.IsFalse(incoming.enabled);
            Call(saws, "SetVertical", true, 1, 77, 0f, -1f);
            Call(saws, "UpdateViews");
            Assert.IsTrue(incoming.enabled);
            Vector3 lane = (Vector3)Call(course, "GetLanePosition", 1, (float)Get(settings, "verticalEndDistance"));
            Assert.That(Vector3.ProjectOnPlane(incoming.GetPosition(0) - lane, Vector3.up).magnitude, Is.LessThan(.001f));
            ((Behaviour)saws).enabled = false;
            Assert.IsFalse(paths.gameObject.activeInHierarchy);
            ((Behaviour)saws).enabled = true;
            Call(saws, "ResetObstacles"); Call(saws, "UpdateViews");
            Assert.IsTrue(paths.gameObject.activeInHierarchy);
            Assert.IsFalse(incoming.enabled, "A reset must not leave a stale incoming warning.");
            Call(saws, "BuildViews");
            yield return null;
            Assert.That(saws.transform.GetComponentsInChildren<Transform>().Count(t => t.name == "Saw paths (visual only)"), Is.EqualTo(1));
        }
    }
}
