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
        public IEnumerator NaturalRopeSectionsShareOneSupportAndBurnedRopeDisappearsUntilReset()
        {
            yield return null;
            var root = course.transform.Find("Generated/Rope1");
            Assert.IsNotNull(root);
            var colliders = root.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
            bool hasGap = (bool)Call(course, "HasSwingGapOnLane", 1);
            int expectedSupports = hasGap ? 2 : 1;
            float gapLength = hasGap ? (float)Get(course, "SwingGapTo") - (float)Get(course, "SwingGapFrom") : 0f;
            Assert.That(colliders.Length, Is.EqualTo(expectedSupports), "실제 끊어진 구간에서만 나누며 색상 경계에는 충돌체 이음새를 만들지 않는다.");
            float finish = (float)Get(course, "FinishDistance");
            Assert.That(colliders.Cast<BoxCollider>().Sum(c => c.size.z), Is.EqualTo(finish - gapLength));
            var visible = root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
            Assert.That(visible.Length, Is.GreaterThan(0));
            Assert.That(visible.Select(r => r.sharedMaterial).Distinct().Count(), Is.EqualTo(1), "구간색 대신 동일한 로프 재질을 사용한다.");
            foreach (var renderer in visible)
            {
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                Assert.IsFalse(block.HasColor("_BaseColor"), "에셋 본래 재질에 구간색을 덮지 않는다.");
            }
            var unaffected = course.transform.Find("Generated/Rope0/ContinuousSupport");
            Call(course, "BlockSegment", 1, 0f, finish, false);
            yield return null;
            root = course.transform.Find("Generated/Rope1");
            Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.Zero);
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
            Assert.IsTrue(unaffected != null && unaffected.gameObject.activeInHierarchy,
                "다른 줄의 발판은 소각 시 재생성하지 않는다.");
            Call(course, "ClearBlockedSegments");
            yield return null;
            root = course.transform.Find("Generated/Rope1");
            Assert.That(root.GetComponentsInChildren<Collider>().Count(c => c.enabled), Is.EqualTo(expectedSupports));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).Select(r => r.sharedMaterial).Distinct().Count(), Is.EqualTo(1));
        }
    }
}
