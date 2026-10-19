using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator ColoredSectionsShareOneSupportAndBurnedRopeDisappearsUntilReset()
        {
            yield return null;
            var root = course.transform.Find("Generated/Rope0");
            Assert.IsNotNull(root);
            var colliders = root.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
            Assert.That(colliders.Length, Is.EqualTo(1), "색상 경계마다 충돌체 이음새를 만들지 않는다.");
            Assert.That(((BoxCollider)colliders[0]).size.z, Is.EqualTo(330f));
            var colors = root.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial.color).Distinct().Count();
            Assert.That(colors, Is.EqualTo(6), "진행 구간마다 서로 다른 줄 색상을 표시한다.");
            var unaffected = course.transform.Find("Generated/Rope1/ContinuousSupport");
            Call(course, "BlockSegment", 0, 0f, 330f, false);
            yield return null;
            root = course.transform.Find("Generated/Rope0");
            Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.Zero);
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
            Assert.IsTrue(unaffected != null && unaffected.gameObject.activeInHierarchy,
                "다른 줄의 발판은 소각 시 재생성하지 않는다.");
            Call(course, "ClearBlockedSegments");
            yield return null;
            root = course.transform.Find("Generated/Rope0");
            Assert.That(root.GetComponentsInChildren<Collider>().Count(c => c.enabled), Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(6));
        }
    }
}
