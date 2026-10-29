using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    // Task-079 3단계: 빠루 연타 벽과 기물 사용 횟수 규칙(2026-10-29 사용자).
    public sealed class ExplorationRulesTests
    {
        const string Wall = "restroom-breach";

        [Test]
        public void 여섯_번째_타격에_열리고_그_전에는_진행만_쌓인다()
        {
            var book = new ExplorationBook();
            for (int i = 1; i <= 5; i++)
            {
                Assert.AreEqual(ExplorationRefusal.None, book.Hit(Wall, 6, out int count, out bool opened));
                Assert.AreEqual(i, count);
                Assert.IsFalse(opened);
            }
            Assert.AreEqual(ExplorationRefusal.None, book.Hit(Wall, 6, out int last, out bool openedNow));
            Assert.AreEqual(6, last);
            Assert.IsTrue(openedNow);
            Assert.IsTrue(book.IsOpen(Wall));
            Assert.AreEqual(ExplorationRefusal.AlreadyOpen, book.Hit(Wall, 6, out _, out bool again));
            Assert.IsFalse(again, "열린 벽을 다시 쳐도 또 열리지 않는다(사용 횟수 중복 차감 방지).");
        }

        [Test]
        public void 머리핀은_두_번_실패해도_세_번째에_반드시_열린다()
        {
            var book = new ExplorationBook();
            const string lockId = "footlocker";
            Assert.IsFalse(ExplorationBook.LockOpens(1, .5f, 3, .9));
            book.Attempt(lockId, false, out int c1, out bool o1);
            Assert.IsFalse(ExplorationBook.LockOpens(2, .5f, 3, .7));
            book.Attempt(lockId, false, out int c2, out bool o2);
            Assert.AreEqual(2, c2); Assert.IsFalse(o1 || o2);
            Assert.IsTrue(ExplorationBook.LockOpens(3, .5f, 3, .99), "세 번째 시도는 확률과 상관없이 연다.");
            Assert.IsTrue(ExplorationBook.LockOpens(1, .5f, 3, .2), "그 전에도 확률에 들면 연다.");
            book.Attempt(lockId, true, out int c3, out bool o3);
            Assert.AreEqual(3, c3); Assert.IsTrue(o3);
            Assert.AreEqual(ExplorationRefusal.AlreadyOpen, book.Attempt(lockId, true, out _, out _), "열린 대상에 또 쓰지 않는다(소모 없음).");
        }

        [Test]
        public void 받은_상태로_진행을_그대로_이어_간다()
        {
            var host = new ExplorationBook();
            host.Hit(Wall, 6, out _, out _); host.Hit(Wall, 6, out _, out _);
            var client = new ExplorationBook();
            client.Load(new[] { Wall }, new[] { host.Hits(Wall) }, new string[0]);
            Assert.AreEqual(2, client.Hits(Wall));
            Assert.IsFalse(client.IsOpen(Wall));
            client.Clear();
            Assert.AreEqual(0, client.Hits(Wall));
        }

        [Test]
        public void 빠루는_사용할_때마다_1_줄고_마지막에_사라진다()
        {
            var carry = new ItemCarryBook(8);
            var crowbar = new PropItem(ItemUses.FoundUidBase + 1, ItemUses.StakePuller, "빠루", 4); // 새 제품 4회(확정)
            Assert.AreEqual(CarryRefusal.None, carry.Place(crowbar, 0, 0, 0));
            Assert.AreEqual(CarryRefusal.NotFound, carry.Place(crowbar, 1, 0, 0), "같은 발견품을 두 번 놓지 않는다.");
            Assert.AreEqual(CarryRefusal.None, carry.PickUp(crowbar.Uid, 7, 2, out _));
            for (int left = 3; left >= 1; left--)
            {
                Assert.AreEqual(CarryRefusal.None, carry.Use(7, crowbar.Uid, out var after, out bool gone));
                Assert.AreEqual(left, after.Uses);
                Assert.IsFalse(gone);
            }
            Assert.AreEqual(CarryRefusal.None, carry.Use(7, crowbar.Uid, out var final, out bool destroyed));
            Assert.AreEqual(0, final.Uses);
            Assert.IsTrue(destroyed, "마지막 효과 뒤 손에서 사라진다.");
            Assert.AreEqual(0, carry.Count(7));
            Assert.IsFalse(carry.Exists(crowbar.Uid));
            Assert.AreEqual(CarryRefusal.NothingHeld, carry.Use(7, crowbar.Uid, out _, out _));
        }

        [Test]
        public void 횟수를_세지_않는_임시_기물은_줄지_않는다()
        {
            var carry = new ItemCarryBook(8);
            var temp = new PropItem(5, "tool-a", "임시");
            Assert.AreEqual(ItemUses.Unlimited, temp.Uses);
            carry.Take(1, 2, temp);
            Assert.AreEqual(CarryRefusal.None, carry.Use(1, 5, out var after, out bool gone));
            Assert.AreEqual(ItemUses.Unlimited, after.Uses);
            Assert.IsFalse(gone);
            Assert.AreEqual(1, carry.Count(1));
        }
    }
}
