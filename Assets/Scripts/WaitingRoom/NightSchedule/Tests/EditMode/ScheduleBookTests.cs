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
        public void 셋째_날_밤에는_4일차를_쓰고_확정해야_한다()
        {
            var book = NewBook();
            book.BeginNight(3);
            Assert.AreEqual(ScheduleRefusal.Incomplete, book.Confirm());
            Fill(book, 4, Cannon, Cannon);
            Assert.AreEqual(ScheduleRefusal.None, book.Confirm());
            Assert.IsTrue(book.IsLocked(4));
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
        public void 난도는_묘기_출연_순번이고_3에서_멈춘다()
        {
            var book = NewBook();
            book.BeginNight(1);
            Fill(book, 2, Rope, Cannon);
            Fill(book, 3, Rope, Rope);

            Assert.AreEqual(1, book.Difficulty(1, 0)); // 줄타기 1번째
            Assert.AreEqual(1, book.Difficulty(1, 1)); // 인간 대포 1번째
            Assert.AreEqual(2, book.Difficulty(2, 0)); // 줄타기 2번째
            Assert.AreEqual(2, book.Difficulty(2, 1)); // 인간 대포 2번째
            Assert.AreEqual(3, book.Difficulty(3, 0)); // 줄타기 3번째
            Assert.AreEqual(3, book.Difficulty(3, 1)); // 줄타기 4번째 → 상한 3
            Assert.AreEqual(0, book.Difficulty(4, 0)); // 빈 칸
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
            host.Confirm();

            var client = NewBook();
            Assert.IsTrue(client.Load(host.Night, host.IsConfirmedTonight, host.ExportSlots(), host.ExportLocked()));

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
