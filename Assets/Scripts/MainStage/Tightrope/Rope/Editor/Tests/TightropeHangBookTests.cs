using NUnit.Framework;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    /// <summary>M2 외줄 추락 매달림 규칙(tightrope-course-rules.md #1-1): 7초 버팀·2초 끌어올리기 중 정지·끊김·한 줄에 한 명.</summary>
    [Category("CommonProgressionRegression")]
    public sealed class TightropeHangBookTests
    {
        [Test, Category("CommonProgressionRegression")]
        public void InjuredRopeHangKeepsElapsedTimeAndTheGloveBonus()
        {
            var book = new TightropeHangBook();
            var hang = book.Begin(1, 0, 2, 7d, 3.5d);
            book.Tick(2d);
            Assert.IsTrue(hang.ShortenBaseTime(3.5d));
            Assert.That(hang.Total, Is.EqualTo(7d));
            Assert.That(hang.Remaining, Is.EqualTo(5d));
            Assert.IsFalse(hang.ShortenBaseTime(7d));
        }
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
        public void OccupiedRopeUsesPhysicalAlongDistanceAndDifferentRopesStayIndependent()
        {
            var book = new TightropeHangBook(); book.Begin(1, 1, 20f, 7d);
            Assert.IsTrue(book.IsOccupied(1, 21.99f, 2));
            Assert.IsFalse(book.IsOccupied(1, 22f, 2));
            Assert.IsFalse(book.IsOccupied(0, 20f, 2));
            book.Remove(1);
            Assert.IsFalse(book.IsOccupied(1, 20f, 2));
        }

        [Test]
        public void StickyGloves_ExtendHold_AndMarkExtraTimeOnlyAfterBaseRunsOut()
        {
            // Task-092 끈끈이 장갑: 7초 × 1.5 = 10.5초. 기본 7초를 넘겨야 추가 시간(소모 대상)이다.
            var book = new TightropeHangBook();
            var hang = book.Begin(1, 0, 10f, 7d, 3.5d);
            Assert.AreEqual(10.5d, hang.Total, 1e-6);
            Assert.AreEqual(3.5d, hang.Extra, 1e-6);
            Assert.IsEmpty(book.Tick(6.9d));
            Assert.IsFalse(hang.InExtraTime, "기본 시간 안에서는 장갑을 쓰지 않았다.");
            Assert.IsTrue(book.StartPull(1, 2, 0, 2d));
            Assert.IsEmpty(book.Tick(1d), "구조 중에는 추가 시간도 흐르지 않는다.");
            Assert.IsFalse(hang.InExtraTime);
            Assert.IsTrue(book.CancelPull(1));
            Assert.IsEmpty(book.Tick(.2d));
            Assert.IsTrue(hang.InExtraTime, "기본 7초를 넘기면 추가 시간이다.");
            Assert.IsEmpty(book.Tick(3.3d), "장갑이 없으면 이미 떨어졌을 시간도 버틴다.");
            var finished = book.Tick(.2d);
            Assert.AreEqual(TightropeHangBook.Outcome.Dropped, finished[0].outcome);
        }

        [Test]
        public void WithoutGloves_TotalIsBaseHold()
        {
            var hang = new TightropeHangBook().Begin(1, 0, 10f, 7d);
            Assert.AreEqual(7d, hang.Total, 1e-6);
            Assert.AreEqual(0d, hang.Extra, 1e-6);
            Assert.IsFalse(hang.InExtraTime);
        }
    }
}
