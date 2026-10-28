using NUnit.Framework;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    /// <summary>M2 외줄 추락 매달림 규칙(tightrope-course-rules.md #1-1): 7초 버팀·2초 끌어올리기 중 정지·끊김·한 줄에 한 명.</summary>
    public sealed class TightropeHangBookTests
    {
        static bool AllLanes(int lane, float distance) => true;

        [Test]
        public void HoldRunsOut_DropsAfterSevenSeconds()
        {
            var book = new TightropeHangBook();
            book.Begin(1, 0, 10f, 7d);
            Assert.IsEmpty(book.Tick(6.9d));
            var finished = book.Tick(.2d);
            Assert.AreEqual(1, finished.Count);
            Assert.AreEqual(TightropeHangBook.Outcome.Dropped, finished[0].outcome);
            Assert.IsFalse(book.IsHanging(1));
        }

        [Test]
        public void Pull_StopsHoldTimer_AndRescuesAfterTwoSeconds()
        {
            var book = new TightropeHangBook();
            book.Begin(1, 0, 10f, 7d);
            book.Tick(6d);
            Assert.IsTrue(book.StartPull(1, 2, 0, 2d));
            Assert.IsTrue(book.IsRescuing(2));
            Assert.IsEmpty(book.Tick(1.9d), "끌어올리는 동안 남은 1초가 줄지 않는다.");
            book.TryGet(1, out var hang);
            Assert.AreEqual(1d, hang.Remaining, 1e-6);
            var finished = book.Tick(.2d);
            Assert.AreEqual(TightropeHangBook.Outcome.Rescued, finished[0].outcome);
        }

        [Test]
        public void CancelledPull_ResumesRemainingHold()
        {
            var book = new TightropeHangBook();
            book.Begin(1, 0, 10f, 7d);
            book.Tick(5d);
            book.StartPull(1, 2, 0, 2d);
            book.Tick(1.5d);
            Assert.IsTrue(book.CancelPull(1), "도와주던 동료가 다치거나 죽으면 끊긴다.");
            Assert.IsEmpty(book.Tick(1.9d));
            Assert.AreEqual(TightropeHangBook.Outcome.Dropped, book.Tick(.2d)[0].outcome);
        }

        [Test]
        public void Pull_RejectsSelfOtherHangerAndSecondRescuer()
        {
            var book = new TightropeHangBook();
            book.Begin(1, 0, 10f, 7d);
            book.Begin(2, 1, 10f, 7d);
            Assert.IsFalse(book.StartPull(1, 1, 0, 2d));
            Assert.IsFalse(book.StartPull(1, 2, 0, 2d), "매달린 사람은 남을 끌어올릴 수 없다.");
            Assert.IsTrue(book.StartPull(1, 3, 0, 2d));
            Assert.IsFalse(book.StartPull(1, 4, 0, 2d));
            Assert.IsFalse(book.StartPull(2, 3, 0, 2d), "한 사람은 한 명만 끌어올린다.");
        }

        [Test]
        public void PiggybackFall_OneHangerPerLane_InFallOrder_ExtraDrops()
        {
            var book = new TightropeHangBook();
            // 3줄 코스 가운데(1번)에서 목마 3명 낙하: 아래 → 위 순서로 같은 줄, 왼쪽, 오른쪽.
            int first = book.PickLane(1, 20f, 3, 1, 2f, AllLanes); book.Begin(1, first, 20f, 7d);
            int second = book.PickLane(1, 20f, 3, 1, 2f, AllLanes); book.Begin(2, second, 20f, 7d);
            int third = book.PickLane(1, 20f, 3, 1, 2f, AllLanes); book.Begin(3, third, 20f, 7d);
            Assert.AreEqual(new[] { 1, 0, 2 }, new[] { first, second, third });
            Assert.AreEqual(-1, book.PickLane(1, 20.5f, 3, 1, 2f, AllLanes), "줄이 모자라면 남은 사람은 낙사.");
            Assert.AreEqual(1, book.PickLane(1, 23f, 3, 1, 2f, AllLanes), "멀리 떨어진 같은 줄은 비어 있다.");
        }

        [Test]
        public void PickLane_SkipsMissingRopeAndRespectsReach()
        {
            var book = new TightropeHangBook();
            Assert.AreEqual(2, book.PickLane(1, 5f, 4, 1, 2f, (lane, _) => lane != 1 && lane != 0));
            Assert.AreEqual(-1, book.PickLane(0, 5f, 4, 1, 2f, (lane, _) => lane >= 2), "근처(1줄) 밖은 잡지 못한다.");
        }
    }
}
