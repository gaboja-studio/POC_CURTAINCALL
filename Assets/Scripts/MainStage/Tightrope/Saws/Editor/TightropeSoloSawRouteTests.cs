using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.Saws.EditorTools.Tests
{
    public sealed class TightropeSoloSawRouteTests
    {
        GameObject root;
        TightropeCourse course;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Solo saw route test");
            course = root.AddComponent<TightropeCourse>();
            Assert.That(course.LaneCount, Is.GreaterThanOrEqualTo(4), "현재 코스 줄 수로 연결된 회피 범위를 검사한다.");
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void LastRouteProtectionFollowsProgressiveFireBlocksAndReset()
        {
            Assert.That(Blocks(1, 20f, 1, 30f), Is.False);
            for (int lane = 0; lane < course.LaneCount; lane++)
                if (lane != 1 && lane != 2) course.BlockSegment(lane, 0f, course.FinishDistance, false);
            Assert.That(Blocks(1, 20f, 1, 30f), Is.False, "피할 다른 줄이 남아 있으면 기존 수직톱을 유지한다.");
            course.BlockSegment(2, 0f, course.FinishDistance, false);
            Assert.That(Blocks(1, 20f, 1, 30f), Is.True, "마지막 2번 줄 앞을 막는 수직톱만 보호한다.");
            Assert.That(Blocks(1, 20f, 1, 19.5f), Is.True, "중심만 지나갔고 날이 몸에 겹치는 상황도 보호한다.");
            course.ClearBlockedSegments();
            Assert.That(Blocks(1, 20f, 1, 30f), Is.False, "다시 열린 코스에서는 보호 조건도 풀린다.");
        }

        [Test]
        public void ProtectionUsesTheSurvivorsDistanceForPartialFireSections()
        {
            BlockOtherLanes(1, 50f, 95f);
            Assert.That(Blocks(1, 20f, 1, 93f), Is.False, "현재 위치에서 옆줄로 회피할 수 있다.");
            Assert.That(Blocks(1, 60f, 1, 93f), Is.True);
            Assert.That(Blocks(1, 95f, 1, 99f), Is.False, "화재 구간 끝의 안전 지대에서는 모든 줄을 다시 쓴다.");
        }

        [TestCase(0, 20f, 1, 30f)]
        [TestCase(2, 20f, 1, 30f)]
        [TestCase(1, 20f, 2, 30f)]
        [TestCase(1, 20f, 1, 19f)]
        [TestCase(1, -1f, 1, 30f)]
        public void OtherPlayersLanesAndPassedSawsDoNotTriggerProtection(int survivors, float distance, int sawLane, float sawDistance)
        {
            BlockOtherLanes(1, 0f, course.FinishDistance);
            Assert.That(Blocks(survivors, distance, sawLane, sawDistance), Is.False);
        }

        [Test]
        public void FinishPlatformDoesNotTriggerProtection()
        {
            BlockOtherLanes(1, 0f, course.FinishDistance);
            Assert.That(Blocks(1, course.FinishDistance, 1, course.FinishDistance + 1f), Is.False);
        }

        [Test]
        public void BurnedMiddleLaneSeparatesAnIsolatedEdgeFromOtherUsableLanes()
        {
            course.BlockSegment(1, 0f, course.FinishDistance, false);
            Assert.That(course.IsLaneUsable(2, 20f), Is.True, "불탄 줄 건너편에는 사용 가능한 줄이 남아 있다.");
            Assert.That(Blocks(1, 20f, 0, 30f, 0), Is.True, "건너편 줄은 1번 줄에서 긴 점프 없이 갈 수 있는 회피 경로가 아니다.");
            Assert.That(Blocks(1, 20f, 2, 30f, 0), Is.False, "다른 구역의 톱날은 현재 플레이어를 막지 않는다.");
            Assert.That(Blocks(2, 20f, 0, 30f, 0), Is.False, "2명 이상인 기존 협동 규칙은 유지한다.");
            course.ClearBlockedSegments();
            Assert.That(Blocks(1, 20f, 0, 30f, 0), Is.False);
        }

        [Test]
        public void UsableLanesBeyondBothBurnedNeighborsDoNotSaveAnIsolatedMiddleLane()
        {
            course.BlockSegment(1, 0f, course.FinishDistance, false);
            course.BlockSegment(3, 0f, course.FinishDistance, false);
            Assert.That(course.IsLaneUsable(0, 20f), Is.True);
            Assert.That(Blocks(1, 20f, 2, 30f, 2), Is.True, "양쪽 인접 줄이 불타면 가운데 줄도 고립된다.");
        }

        [Test]
        public void TwoConnectedUsableLanesStillAllowDodgingRegardlessOfOtherIslands()
        {
            course.BlockSegment(2, 0f, course.FinishDistance, false);
            Assert.That(Blocks(1, 20f, 0, 30f, 0), Is.False, "현재 구역의 바로 옆 2번 줄로 피할 수 있다.");
            Assert.That(Blocks(1, 20f, 1, 30f, 1), Is.False, "구역 끝에서도 연결된 반대쪽 줄로 피할 수 있다.");
        }

        void BlockOtherLanes(int remainingLane, float from, float to)
        {
            for (int lane = 0; lane < course.LaneCount; lane++)
                if (lane != remainingLane) course.BlockSegment(lane, from, to, false);
        }

        bool Blocks(int survivors, float distance, int sawLane, float sawDistance, int survivorLane = 1) =>
            (bool)typeof(TightropeSaws).GetMethod("BlocksSoloRoute", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { course, survivors, course.GetLanePosition(survivorLane, distance), sawLane, sawDistance, 0.8f });
    }
}
