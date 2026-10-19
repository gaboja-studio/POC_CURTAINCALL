using System.Collections.Generic;
using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class ItemCarryBookTests
    {
        static PropItem Item(int uid) => new(uid, "tool-a", $"도구{uid}");

        [Test]
        public void 한도까지만_든다()
        {
            var book = new ItemCarryBook(16);

            Assert.AreEqual(CarryRefusal.None, book.Take(1, 2, Item(1), false));
            Assert.AreEqual(CarryRefusal.None, book.Take(1, 2, Item(2), false));
            Assert.AreEqual(CarryRefusal.HandsFull, book.Take(1, 2, Item(3), false));

            Assert.AreEqual(2, book.Count(1));
            Assert.AreEqual(0, book.Count(2), "다른 사람 칸과 섞이지 않는다");
        }

        [Test]
        public void 배낭은_칸을_차지하지_않고_한_개만_멘다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 2, Item(1), false);
            book.Take(1, 2, Item(2), false);

            Assert.AreEqual(CarryRefusal.None, book.Take(1, 2, Item(3), true), "손이 차 있어도 배낭은 멘다");
            Assert.IsTrue(book.HasBackpack(1));
            Assert.AreEqual(2, book.Count(1));
            Assert.AreEqual(CarryRefusal.AlreadyHasBackpack, book.CanTake(1, 4, true));
        }

        [Test]
        public void 내려놓으면_마지막에_든_것이_바닥에_간다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 2, Item(1), false);
            book.Take(1, 2, Item(2), false);

            Assert.AreEqual(CarryRefusal.None, book.Drop(1, 1f, 0f, 2f, out var dropped));

            Assert.AreEqual(2, dropped.Item.Uid);
            Assert.AreEqual(1, book.Count(1));
            Assert.AreEqual(1, book.Floor.Count);
            Assert.AreEqual(2f, book.Floor[0].Z);
        }

        [Test]
        public void 빈손이면_내려놓을_수_없다()
        {
            var book = new ItemCarryBook(16);
            Assert.AreEqual(CarryRefusal.NothingHeld, book.Drop(1, 0, 0, 0, out _));
        }

        [Test]
        public void 바닥_기물은_누구나_한도_안에서_줍는다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 2, Item(1), false);
            book.Drop(1, 0, 0, 0, out _);

            Assert.AreEqual(CarryRefusal.None, book.PickUp(1, 2, 2, out var picked));
            Assert.AreEqual(1, picked.Uid);
            Assert.AreEqual(1, book.Count(2));
            Assert.AreEqual(0, book.Floor.Count);
            Assert.AreEqual(CarryRefusal.NotFound, book.PickUp(1, 3, 2, out _), "이미 주운 것은 다시 못 줍는다");
        }

        [Test]
        public void 손이_차면_바닥_기물을_줍지_못한다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 1, Item(1), false);
            book.Drop(1, 0, 0, 0, out _);
            book.Take(2, 1, Item(2), false);

            Assert.AreEqual(CarryRefusal.HandsFull, book.PickUp(1, 2, 1, out _));
            Assert.AreEqual(1, book.Floor.Count);
        }

        [Test]
        public void 바닥이_가득_차면_내려놓기를_거절한다()
        {
            var book = new ItemCarryBook(1);
            book.Take(1, 2, Item(1), false);
            book.Take(1, 2, Item(2), false);
            book.Drop(1, 0, 0, 0, out _);

            Assert.AreEqual(CarryRefusal.FloorFull, book.Drop(1, 0, 0, 0, out _));
            Assert.AreEqual(1, book.Count(1));
        }

        [Test]
        public void 한도가_줄면_넘친_만큼_떨어진다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 4, Item(1), false);
            book.Take(1, 4, Item(2), false);
            book.Take(1, 4, Item(3), false);
            var dropped = new List<FloorProp>();

            Assert.AreEqual(2, book.Trim(1, 1, 5f, 0f, 5f, dropped));

            Assert.AreEqual(1, book.Count(1));
            Assert.AreEqual(1, book.Held(1)[0].Uid, "먼저 든 것이 손에 남는다");
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, new[] { dropped[0].Item.Uid, dropped[1].Item.Uid });
            Assert.AreEqual(2, book.Floor.Count);
        }

        [Test]
        public void 한도_안이면_떨어뜨리지_않는다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 2, Item(1), false);
            long revision = book.Revision;

            Assert.AreEqual(0, book.Trim(1, 2, 0, 0, 0));
            Assert.AreEqual(revision, book.Revision);
        }

        [Test]
        public void 선반에는_아래_칸부터_올린다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 2, Item(1), false);
            book.Take(1, 2, Item(2), false);

            Assert.AreEqual(CarryRefusal.None, book.UseShelf(1, 2, out int first, out bool placed, out var item));
            Assert.IsTrue(placed);
            Assert.AreEqual(0, first);
            Assert.AreEqual(2, item.Uid, "마지막에 든 것을 올린다");
            book.UseShelf(1, 2, out int second, out _, out _);
            Assert.AreEqual(1, second);
            Assert.AreEqual(0, book.Count(1));
        }

        [Test]
        public void 빈손이면_위_칸부터_꺼낸다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 2, Item(1), false);
            book.Take(1, 2, Item(2), false);
            book.UseShelf(1, 2, out _, out _, out _);
            book.UseShelf(1, 2, out _, out _, out _);

            Assert.AreEqual(CarryRefusal.None, book.UseShelf(2, 2, out int slot, out bool placed, out var item));
            Assert.IsFalse(placed);
            Assert.AreEqual(1, slot);
            Assert.AreEqual(1, item.Uid);
            Assert.AreEqual(1, book.Count(2), "다른 사람도 꺼낼 수 있다");
            Assert.IsTrue(ItemCarryBook.IsEmpty(book.Shelf[1]));
        }

        [Test]
        public void 선반이_가득_차면_들고_있을_때_꺼낸다()
        {
            var book = new ItemCarryBook(16);
            for (int i = 1; i <= 4; i++)
            {
                book.Take(1, 4, Item(i), false);
                book.UseShelf(1, 4, out _, out _, out _);
            }
            book.Take(1, 4, Item(5), false);

            Assert.AreEqual(CarryRefusal.None, book.UseShelf(1, 4, out int slot, out bool placed, out var item));
            Assert.IsFalse(placed);
            Assert.AreEqual(3, slot);
            Assert.AreEqual(4, item.Uid);
            Assert.AreEqual(2, book.Count(1));
        }

        [Test]
        public void 선반이_가득_차고_손도_차면_거절한다()
        {
            var book = new ItemCarryBook(16);
            for (int i = 1; i <= 4; i++)
            {
                book.Take(1, 1, Item(i), false);
                book.UseShelf(1, 1, out _, out _, out _);
            }
            book.Take(1, 1, Item(5), false);

            Assert.AreEqual(CarryRefusal.ShelfFull, book.UseShelf(1, 1, out _, out _, out _));
            Assert.AreEqual(1, book.Count(1));
        }

        [Test]
        public void 빈_선반에서_빈손이면_거절한다()
        {
            var book = new ItemCarryBook(16);
            Assert.AreEqual(CarryRefusal.ShelfEmpty, book.UseShelf(1, 2, out _, out _, out _));
        }

        [Test]
        public void 세대_교체는_선반만_남긴다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 4, Item(1), false);
            book.UseShelf(1, 4, out _, out _, out _);
            book.Take(1, 4, Item(2), false);
            book.Take(1, 4, Item(3), false);
            book.Drop(1, 0, 0, 0, out _);
            book.Take(1, 4, Item(9), true);

            book.ResetGeneration();

            Assert.AreEqual(1, book.Shelf[0].Uid);
            Assert.AreEqual(0, book.Count(1));
            Assert.IsFalse(book.HasBackpack(1));
            Assert.AreEqual(0, book.Floor.Count);
        }

        [Test]
        public void 초기화하면_손_배낭_바닥이_모두_빈다()
        {
            var book = new ItemCarryBook(16);
            book.Take(1, 2, Item(1), false);
            book.Take(1, 2, Item(2), true);
            book.Drop(1, 0, 0, 0, out _);
            book.Take(1, 2, Item(3), false);
            book.UseShelf(1, 2, out _, out _, out _);

            book.Clear();

            Assert.AreEqual(0, book.Count(1));
            Assert.IsFalse(book.HasBackpack(1));
            Assert.AreEqual(0, book.Floor.Count);
            Assert.IsTrue(ItemCarryBook.IsEmpty(book.Shelf[0]), "방이 바뀌면 선반도 비운다");
        }
    }
}
