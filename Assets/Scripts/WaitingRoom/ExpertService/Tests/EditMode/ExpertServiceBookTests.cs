using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    /// <summary>M2 신체 복구: 쉬는 시간·밤 정비 언제든, 1~3개 선택, 1개당 100에서 팀 이용마다 +25, 런 초기화.</summary>
    public sealed class ExpertServiceBookTests
    {
        const ulong Buyer = 1, Other = 2;
        const int MaxPerUse = 3;

        static ExpertServiceBook NewBook() => new(3f, 2f);

        static ExpertRefusal BookMedical(ExpertServiceBook book, ulong buyer = Buyer, bool serviceTime = true,
            bool alive = true, int lost = 1, int parts = 1, bool funds = true) =>
            book.BookMedical(buyer, serviceTime, alive, lost, parts, MaxPerUse, () => funds);

        [Test]
        public void 쉬는_시간과_밤_정비에는_언제든_쓰고_공연_중에는_못_쓴다()
        {
            Assert.AreEqual(ExpertRefusal.None, ExpertServiceBook.Availability(true));
            Assert.AreEqual(ExpertRefusal.InPerformance, ExpertServiceBook.Availability(false));
            Assert.AreEqual(ExpertRefusal.InPerformance, BookMedical(NewBook(), serviceTime: false));
        }

        [Test]
        public void 온전한_몸이나_죽은_사람은_살_수_없다()
        {
            Assert.AreEqual(ExpertRefusal.WholeBody, BookMedical(NewBook(), lost: 0));
            Assert.AreEqual(ExpertRefusal.NotAlive, BookMedical(NewBook(), alive: false));
        }

        [TestCase(2, 0, ExpertRefusal.BadChoice)]
        [TestCase(2, 3, ExpertRefusal.BadChoice)]
        [TestCase(4, 4, ExpertRefusal.BadChoice)]
        [TestCase(4, 3, ExpertRefusal.None)]
        [TestCase(2, 2, ExpertRefusal.None)]
        public void 한_번에_잃은_수와_3개_중_작은_수까지만_고른다(int lost, int parts, ExpertRefusal expected)
        {
            Assert.AreEqual(expected, BookMedical(NewBook(), lost: lost, parts: parts));
            Assert.AreEqual(2, ExpertServiceBook.MaxParts(2, MaxPerUse));
            Assert.AreEqual(3, ExpertServiceBook.MaxParts(4, MaxPerUse));
        }

        [Test]
        public void 가격은_100에서_팀_이용_1회마다_25씩_오르고_여러_개도_한_번만_오른다()
        {
            Assert.AreEqual(200, ExpertServiceBook.Cost(2, 0, 100, 25), "처음 2개 = 100 × 2");
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.None, BookMedical(book, lost: 3, parts: 2));
            Assert.AreEqual(1, book.TeamUses, "2개를 고쳐도 1회");
            Assert.AreEqual(125, ExpertServiceBook.UnitPrice(book.TeamUses, 100, 25), "다음 1개 = 125");
            book.Clear();
            Assert.AreEqual(ExpertRefusal.None, BookMedical(book, buyer: Other, lost: 1, parts: 1));
            Assert.AreEqual(150, ExpertServiceBook.UnitPrice(book.TeamUses, 100, 25), "팀 전체가 같은 가격을 쓴다");
            Assert.AreEqual(0, ExpertServiceBook.Cost(0, 5, 100, 25));
        }

        [Test]
        public void 새_판이면_가격이_처음으로_돌아간다()
        {
            var book = NewBook();
            BookMedical(book, lost: 2, parts: 2);
            book.Reset();
            Assert.AreEqual(0, book.TeamUses);
            Assert.AreEqual(ExpertRoomPhase.Idle, book.Phase);
        }

        [Test]
        public void 구매하면_고른_수만큼_복구하도록_예약한다()
        {
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.None, BookMedical(book, lost: 3, parts: 2));
            Assert.AreEqual(2, book.BookedLimbs);
        }

        [Test]
        public void 조건이_안_맞으면_자금을_쓰지_않고_가격도_오르지_않는다()
        {
            bool spent = false;
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.InPerformance, book.BookMedical(Buyer, false, true, 1, 1, MaxPerUse, () => spent = true));
            Assert.AreEqual(ExpertRefusal.BadChoice, book.BookMedical(Buyer, true, true, 1, 2, MaxPerUse, () => spent = true));
            Assert.IsFalse(spent);
            Assert.AreEqual(0, book.TeamUses);
        }

        [Test]
        public void 자금이_모자라면_예약되지_않는다()
        {
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.NotEnoughFunds, BookMedical(book, funds: false));
            Assert.AreEqual(ExpertRoomPhase.Idle, book.Phase);
            Assert.AreEqual(0, book.TeamUses);
        }

        [Test]
        public void 구매하면_방이_예약되고_다른_사람은_살_수_없다()
        {
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.None, BookMedical(book));
            Assert.AreEqual(ExpertRoomPhase.Booked, book.Phase);
            Assert.AreEqual(Buyer, book.Buyer);
            Assert.AreEqual(ExpertRefusal.Busy, BookMedical(book, buyer: Other));
        }

        [Test]
        public void 구매자가_다시_사면_이미_구매했다고_알리고_자금을_쓰지_않는다()
        {
            var book = NewBook();
            BookMedical(book);
            bool spent = false;
            Assert.AreEqual(ExpertRefusal.AlreadyBooked, book.BookMedical(Buyer, true, true, 1, 1, MaxPerUse, () => spent = true));
            Assert.IsFalse(spent);
        }

        [Test]
        public void 구매자만_들어갈_수_있다()
        {
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.WrongPhase, book.Enter(Buyer));
            BookMedical(book);
            Assert.AreEqual(ExpertRefusal.NotBuyer, book.Enter(Other));
            Assert.AreEqual(ExpertRefusal.None, book.Enter(Buyer));
            Assert.AreEqual(ExpertRoomPhase.Working, book.Phase);
        }

        [Test]
        public void 복구_시간이_지나면_한_번만_복구하고_퇴실_뒤_비워진다()
        {
            var book = NewBook();
            BookMedical(book);
            book.Enter(Buyer);
            Assert.IsFalse(book.Tick(2.9f));
            Assert.IsTrue(book.Tick(0.2f));
            Assert.AreEqual(ExpertRoomPhase.Exiting, book.Phase);
            Assert.IsFalse(book.Tick(1.9f));
            Assert.IsFalse(book.Tick(0.2f));
            Assert.AreEqual(ExpertRoomPhase.Idle, book.Phase);
            Assert.IsFalse(book.HasBuyer);
            Assert.AreEqual(0, book.BookedLimbs);
            Assert.AreEqual(1, book.TeamUses, "방을 비워도 가격 상승은 런 동안 남는다");
        }

        [Test]
        public void 예약만_한_상태에서는_시간이_흘러도_그대로다()
        {
            var book = NewBook();
            BookMedical(book);
            Assert.IsFalse(book.Tick(100f));
            Assert.AreEqual(ExpertRoomPhase.Booked, book.Phase);
        }

        [Test]
        public void 참가자는_호스트의_이용_횟수까지_그대로_받는다()
        {
            var book = NewBook();
            book.Apply(ExpertRoomPhase.Booked, Buyer, 2, 3, 9);
            Assert.AreEqual(3, book.TeamUses);
            Assert.AreEqual(2, book.BookedLimbs);
            Assert.AreEqual(9, book.Revision);
        }
    }
}
