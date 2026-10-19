using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class ExpertServiceBookTests
    {
        const ulong Buyer = 1, Other = 2;

        static ExpertServiceBook NewBook() => new(3f, 2f);

        static ExpertRefusal BookMedical(ExpertServiceBook book, ulong buyer = Buyer, int day = 1, bool night = true,
            bool alive = true, int lost = 1, bool funds = true) =>
            book.BookMedical(buyer, day, night, alive, lost, () => funds);

        [Test]
        public void 일차별_담당자는_1_3일차_의료_2일차_시설이다()
        {
            Assert.AreEqual(ExpertKind.Medical, ExpertServiceBook.StaffFor(1));
            Assert.AreEqual(ExpertKind.Facility, ExpertServiceBook.StaffFor(2));
            Assert.AreEqual(ExpertKind.Medical, ExpertServiceBook.StaffFor(3));
            Assert.AreEqual(ExpertKind.None, ExpertServiceBook.StaffFor(4));
        }

        [Test]
        public void 이틀째_밤_의료는_담당자_부재중이다()
        {
            Assert.AreEqual(ExpertRefusal.Absent, ExpertServiceBook.Availability(ExpertKind.Medical, 2, true));
            Assert.AreEqual(ExpertRefusal.Absent, BookMedical(NewBook(), day: 2));
            Assert.AreEqual("담당자 부재중", ExpertServiceBook.Explain(ExpertRefusal.Absent));
        }

        [Test]
        public void 시설은_2일차에만_오고_1_3일차는_부재중이다()
        {
            Assert.AreEqual(ExpertRefusal.Absent, ExpertServiceBook.Availability(ExpertKind.Facility, 1, true));
            Assert.AreEqual(ExpertRefusal.Absent, ExpertServiceBook.Availability(ExpertKind.Facility, 3, true));
            Assert.AreEqual(ExpertRefusal.None, ExpertServiceBook.Availability(ExpertKind.Facility, 2, true));
        }

        [Test]
        public void 밤이_아니면_살_수_없다()
        {
            Assert.AreEqual(ExpertRefusal.NotNight, BookMedical(NewBook(), night: false));
            Assert.AreEqual(ExpertRefusal.NotNight, NewBook().BookFacility(ObstacleAdjust.Strengthen, 2, false, () => true));
        }

        [Test]
        public void 온전한_몸이나_죽은_사람은_살_수_없다()
        {
            Assert.AreEqual(ExpertRefusal.WholeBody, BookMedical(NewBook(), lost: 0));
            Assert.AreEqual(ExpertRefusal.NotAlive, BookMedical(NewBook(), alive: false));
        }

        [Test]
        public void 의료_비용은_잃은_팔다리_수만큼이다()
        {
            Assert.AreEqual(0, ExpertServiceBook.MedicalCost(0, 30));
            Assert.AreEqual(30, ExpertServiceBook.MedicalCost(1, 30));
            Assert.AreEqual(90, ExpertServiceBook.MedicalCost(3, 30));
        }

        [Test]
        public void 구매하면_잃은_팔다리를_모두_복구하도록_예약한다()
        {
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.None, BookMedical(book, lost: 3));
            Assert.AreEqual(3, book.BookedLimbs);
        }

        [Test]
        public void 조건이_안_맞으면_자금을_쓰지_않는다()
        {
            bool spent = false;
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.Absent, book.BookMedical(Buyer, 2, true, true, 1, () => spent = true));
            Assert.AreEqual(ExpertRefusal.Absent, book.BookFacility(ObstacleAdjust.Weaken, 1, true, () => spent = true));
            Assert.IsFalse(spent);
        }

        [Test]
        public void 자금이_모자라면_예약되지_않는다()
        {
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.NotEnoughFunds, BookMedical(book, funds: false));
            Assert.AreEqual(ExpertRoomPhase.Idle, book.Phase);
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
            Assert.AreEqual(ExpertRefusal.AlreadyBooked, book.BookMedical(Buyer, 1, true, true, 1, () => spent = true));
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
        public void 비우면_다시_살_수_있고_상태_번호가_오른다()
        {
            var book = NewBook();
            long before = book.Revision;
            BookMedical(book);
            book.Clear();
            Assert.Greater(book.Revision, before);
            Assert.AreEqual(ExpertRefusal.None, BookMedical(book, buyer: Other));
        }

        [Test]
        public void 시설_조정은_2일차_밤에_한_번_사고_3일차에_적용된다()
        {
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.None, book.BookFacility(ObstacleAdjust.Strengthen, 2, true, () => true));
            Assert.AreEqual(ObstacleAdjust.Strengthen, book.ObstacleFor(3));
            Assert.AreEqual(ObstacleAdjust.None, book.ObstacleFor(2));
            Assert.AreEqual(ExpertRefusal.AlreadyAdjusted, book.BookFacility(ObstacleAdjust.Weaken, 2, true, () => true));
        }

        [Test]
        public void 시설_조정은_고르지_않으면_살_수_없고_자금이_모자라면_남지_않는다()
        {
            var book = NewBook();
            Assert.AreEqual(ExpertRefusal.BadChoice, book.BookFacility(ObstacleAdjust.None, 2, true, () => true));
            Assert.AreEqual(ExpertRefusal.NotEnoughFunds, book.BookFacility(ObstacleAdjust.Weaken, 2, true, () => false));
            Assert.AreEqual(ObstacleAdjust.None, book.ObstacleFor(3));
        }

        [Test]
        public void 적용_날이_지나면_시설_조정이_지워진다()
        {
            var book = NewBook();
            book.BookFacility(ObstacleAdjust.Weaken, 2, true, () => true);
            Assert.IsFalse(book.ExpireObstacle(3));
            Assert.IsTrue(book.ExpireObstacle(4));
            Assert.AreEqual(ObstacleAdjust.None, book.ObstacleFor(3));
        }

        [Test]
        public void 초기화하면_방과_시설_조정이_모두_비워진다()
        {
            var book = NewBook();
            BookMedical(book);
            book.BookFacility(ObstacleAdjust.Strengthen, 2, true, () => true);
            book.Reset();
            Assert.AreEqual(ExpertRoomPhase.Idle, book.Phase);
            Assert.AreEqual(ObstacleAdjust.None, book.ObstacleFor(3));
        }
    }
}
