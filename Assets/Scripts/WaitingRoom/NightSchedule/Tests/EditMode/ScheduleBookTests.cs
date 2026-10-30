using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class ScheduleBookTests
    {
        const string Rope = "Tightrope";
        const string Cannon = "HumanCannon";

        static ScheduleBook NewBook() => new(new[] { Rope, Cannon }, new[] { Rope, Cannon });

        static void Fill(ScheduleBook book, int day, string first, string second)
        {
            Assert.AreEqual(ScheduleRefusal.None, book.Set(day, 0, first));
            Assert.AreEqual(ScheduleRefusal.None, book.Set(day, 1, second));
        }

        [Test]
        public void 첫날_고정_공연은_처음부터_있고_잠겨_있다()
        {
            var book = NewBook();
            CollectionAssert.AreEqual(new[] { Rope, Cannon }, book.Setlist(1));
            Assert.IsTrue(book.IsLocked(1));
            book.BeginNight(1);
            Assert.AreEqual(ScheduleRefusal.NotEditable, book.Set(1, 0, Cannon));
        }

        [Test]
        public void 밤이_아니면_쓸_수_없다()
        {
            var book = NewBook();
            Assert.AreEqual(ScheduleRefusal.NotNight, book.Set(2, 0, Rope));
            Assert.AreEqual(ScheduleRefusal.NotNight, book.Confirm());
        }

        [Test]
        public void 첫날_밤에는_2_3일차만_쓴다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Assert.IsTrue(book.IsEditable(2));
            Assert.IsTrue(book.IsEditable(3));
            Assert.IsFalse(book.IsEditable(4));
            Assert.AreEqual(ScheduleRefusal.NotEditable, book.Set(4, 0, Rope));
        }

        [Test]
        public void 칸이_비면_확정을_거절한다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Fill(book, 2, Rope, Cannon);
            book.Set(3, 0, Rope);
            Assert.AreEqual(ScheduleRefusal.Incomplete, book.Confirm());
            Assert.IsFalse(book.IsConfirmedTonight);
        }

        [Test]
        public void 첫날_밤_확정은_2일차만_잠그고_3일차는_2일차_밤에_다시_연다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Fill(book, 2, Rope, Cannon);
            Fill(book, 3, Cannon, Rope);
            Assert.AreEqual(ScheduleRefusal.None, book.Confirm());
            Assert.IsTrue(book.IsConfirmedTonight);
            Assert.IsTrue(book.IsLocked(2));
            Assert.IsFalse(book.IsLocked(3));
            Assert.AreEqual(ScheduleRefusal.AlreadyConfirmed, book.Set(3, 0, Rope));

            book.EndNight();
            book.BeginNight(2);
            Assert.IsFalse(book.IsConfirmedTonight);
            Assert.AreEqual(ScheduleRefusal.NotEditable, book.Set(2, 0, Cannon));
            Assert.AreEqual(ScheduleRefusal.None, book.Set(3, 0, Rope));
            Assert.AreEqual(ScheduleRefusal.None, book.Confirm());
            Assert.IsTrue(book.IsLocked(3));
            CollectionAssert.AreEqual(new[] { Rope, Rope }, book.Setlist(3));
        }

        [Test]
        public void 셋째_날_밤에는_편성과_확정이_필요없다()
        {
            var book = NewBook();
            book.BeginNight(3);
            Assert.IsTrue(book.IsConfirmedTonight);
            Assert.IsEmpty(ScheduleBook.EditableDays(3));
            Assert.AreEqual(ScheduleRefusal.AlreadyConfirmed, book.Set(4, 0, Cannon));
            Assert.AreEqual("", book.Get(4, 0));
            Assert.IsFalse(book.IsLocked(4));
        }

        [Test]
        public void 넷째_날은_실제_공연으로_넘기지_않는다()
        {
            CollectionAssert.AreEqual(new[] { 2, 3 }, ScheduleBook.DaysToApply(1));
            CollectionAssert.AreEqual(new[] { 3 }, ScheduleBook.DaysToApply(2));
            CollectionAssert.IsEmpty(ScheduleBook.DaysToApply(3));
        }

        [Test]
        public void 같은_공연을_하루_두_번_골라도_된다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Fill(book, 2, Rope, Rope);
            Fill(book, 3, Rope, Rope);
            Assert.AreEqual(ScheduleRefusal.None, book.Confirm());
        }

        [Test]
        public void 목록에_없는_공연은_거절한다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Assert.AreEqual(ScheduleRefusal.UnknownTrick, book.Set(2, 0, "Juggling"));
            Assert.AreEqual(ScheduleRefusal.BadSlot, book.Set(2, 2, Rope));
        }

        [Test]
        public void 난도는_칸마다_고르고_깬_단계의_다음_단계까지만_고른다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Fill(book, 2, Rope, Cannon);
            Assert.AreEqual(1, book.Difficulty(1, 0), "1일차는 1단계 고정");
            Assert.AreEqual(1, book.Difficulty(2, 0), "고르지 않으면 1단계");
            Assert.AreEqual(ScheduleRefusal.None, book.Set(2, 0, Rope, 2, 2), "1단계를 깼으면 2단계까지");
            Assert.AreEqual(ScheduleRefusal.BadTier, book.Set(2, 1, Cannon, 2, 1), "못 깨면 다음 단계가 열리지 않는다");
            Assert.AreEqual(ScheduleRefusal.BadTier, book.Set(2, 1, Cannon, 4, 9), "최대 3단계");
            Assert.AreEqual(ScheduleRefusal.None, book.Set(3, 0, Rope, 3, 3));
            Assert.AreEqual(2, book.Difficulty(2, 0));
            Assert.AreEqual(1, book.Difficulty(2, 1));
            Assert.AreEqual(3, book.Difficulty(3, 0));
            CollectionAssert.AreEqual(new[] { 2, 1 }, book.Tiers(2));
            Assert.AreEqual(0, book.Difficulty(4, 0)); // 빈 칸
            Assert.AreEqual(ScheduleRefusal.None, book.Set(2, 0, ""));
            Assert.AreEqual(0, book.Difficulty(2, 0), "비우면 단계도 없다");
        }

        [Test]
        public void 쉬는_시간에는_잠긴_칸도_단계만_바꾼다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Fill(book, 2, Rope, Cannon);
            Fill(book, 3, Rope, Rope);
            Assert.AreEqual(ScheduleRefusal.None, book.Confirm());
            book.EndNight();
            Assert.IsTrue(book.IsLocked(2));
            long before = book.Revision;
            Assert.AreEqual(ScheduleRefusal.None, book.AdjustTier(2, 0, 2, 2));
            Assert.AreEqual(2, book.Difficulty(2, 0));
            Assert.AreEqual(Rope, book.Get(2, 0), "묘기는 그대로");
            Assert.Greater(book.Revision, before);
            Assert.AreEqual(ScheduleRefusal.BadTier, book.AdjustTier(2, 1, 2, 1), "못 깬 단계는 고를 수 없다");
            Assert.AreEqual(ScheduleRefusal.BadSlot, book.AdjustTier(1, 0, 1, 1), "1일차는 고정");
            Assert.AreEqual(ScheduleRefusal.BadSlot, book.AdjustTier(4, 0, 1, 1), "4일차는 공연 없음");
            before = book.Revision;
            Assert.AreEqual(ScheduleRefusal.None, book.AdjustTier(2, 0, 2, 2));
            Assert.AreEqual(before, book.Revision, "같은 단계면 리비전이 오르지 않는다");
        }

        [Test]
        public void 초기화하면_처음_상태로_돌아간다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Fill(book, 2, Rope, Cannon);
            Fill(book, 3, Rope, Cannon);
            book.Confirm();

            book.Clear();

            Assert.AreEqual(0, book.Night);
            Assert.IsFalse(book.IsLocked(2));
            CollectionAssert.AreEqual(new[] { "", "" }, book.Setlist(2));
            CollectionAssert.AreEqual(new[] { Rope, Cannon }, book.Setlist(1));
        }

        [Test]
        public void 내보낸_상태를_다른_장부가_그대로_받는다()
        {
            var host = NewBook();
            host.BeginNight(1);
            Fill(host, 2, Cannon, Rope);
            Fill(host, 3, Rope, Rope);
            Assert.AreEqual(ScheduleRefusal.None, host.Set(3, 1, Rope, 2, 2));
            host.Confirm();

            var client = NewBook();
            Assert.IsTrue(client.Load(host.Night, host.IsConfirmedTonight, host.ExportSlots(), host.ExportLocked(), host.ExportTiers()));
            Assert.AreEqual(2, client.Difficulty(3, 1), "고른 단계도 그대로 받는다");

            Assert.AreEqual(1, client.Night);
            Assert.IsTrue(client.IsConfirmedTonight);
            Assert.IsTrue(client.IsLocked(2));
            CollectionAssert.AreEqual(host.Setlist(3), client.Setlist(3));
            Assert.AreEqual(host.Difficulty(3, 1), client.Difficulty(3, 1));
        }

        [Test]
        public void 잘못된_상태는_받지_않고_1일차는_바꾸지_않는다()
        {
            var client = NewBook();
            Assert.IsFalse(client.Load(1, false, new[] { "a" }, new bool[4]));

            var flat = new[] { Cannon, Cannon, "Juggling", Rope, "", "", "", "" };
            Assert.IsTrue(client.Load(1, false, flat, new bool[4]));
            CollectionAssert.AreEqual(new[] { Rope, Cannon }, client.Setlist(1));
            Assert.IsTrue(client.IsLocked(1));
            CollectionAssert.AreEqual(new[] { "", Rope }, client.Setlist(2));
        }

        [Test]
        public void 바뀔_때만_리비전이_오른다()
        {
            var book = NewBook();
            book.BeginNight(1);
            long before = book.Revision;
            book.Set(2, 0, Rope);
            long afterSet = book.Revision;
            book.Set(2, 0, Rope);
            Assert.Greater(afterSet, before);
            Assert.AreEqual(afterSet, book.Revision);
        }
    }
}
