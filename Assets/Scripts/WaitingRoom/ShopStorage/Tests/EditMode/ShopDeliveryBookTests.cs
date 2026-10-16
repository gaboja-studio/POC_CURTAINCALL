using System.Collections.Generic;
using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class ShopDeliveryBookTests
    {
        static readonly ShopItem[] Catalog =
        {
            new("tool-a", "임시 공연 도구 A", 10),
            new("escape", "임시 탈출 도구", 20),
        };

        /// <summary>공동 자금 대역: 잔액 안에서만 차감하고 장부 설명을 남긴다.</summary>
        sealed class Funds
        {
            public long Balance;
            public readonly List<string> Ledger = new();
            public Funds(long balance) => Balance = balance;

            public bool Pay(long amount, string text)
            {
                if (amount > Balance) return false;
                Balance -= amount;
                Ledger.Add(text);
                return true;
            }
        }

        [Test]
        public void 구매하면_자금이_빠지고_배송구에_놓인다()
        {
            var book = new ShopDeliveryBook(8);
            var funds = new Funds(30);

            Assert.AreEqual(ShopRefusal.None, book.Order(Catalog, "tool-a", 2, funds.Pay, out var delivery));

            Assert.AreEqual(20, funds.Balance);
            CollectionAssert.AreEqual(new[] { "상점 구매: 임시 공연 도구 A" }, funds.Ledger);
            Assert.AreEqual(1, delivery.Order);
            Assert.AreEqual(2UL, delivery.Buyer);
            Assert.AreEqual(1, book.Waiting.Count);
        }

        [Test]
        public void 자금이_모자라면_아무것도_바뀌지_않는다()
        {
            var book = new ShopDeliveryBook(8);
            var funds = new Funds(15);
            long revision = book.Revision;

            Assert.AreEqual(ShopRefusal.NotEnoughFunds, book.Order(Catalog, "escape", 1, funds.Pay, out _));

            Assert.AreEqual(15, funds.Balance);
            Assert.AreEqual(0, book.Waiting.Count);
            Assert.AreEqual(revision, book.Revision);
        }

        [Test]
        public void 없는_품목은_돈을_쓰지_않고_거절한다()
        {
            var book = new ShopDeliveryBook(8);
            var funds = new Funds(100);

            Assert.AreEqual(ShopRefusal.UnknownItem, book.Order(Catalog, "nope", 1, funds.Pay, out _));
            Assert.AreEqual(ShopRefusal.UnknownItem, book.Order(Catalog, null, 1, funds.Pay, out _));
            Assert.AreEqual(100, funds.Balance);
        }

        [Test]
        public void 배송구가_가득_차면_돈을_쓰기_전에_거절한다()
        {
            var book = new ShopDeliveryBook(2);
            var funds = new Funds(100);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out _);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out _);

            Assert.AreEqual(ShopRefusal.SlotFull, book.Order(Catalog, "tool-a", 1, funds.Pay, out _));
            Assert.AreEqual(80, funds.Balance);
            Assert.AreEqual(2, funds.Ledger.Count);
        }

        [Test]
        public void 수령은_먼저_온_한_명만_성공한다()
        {
            var book = new ShopDeliveryBook(8);
            book.Order(Catalog, "tool-a", 1, new Funds(10).Pay, out var delivery);

            Assert.AreEqual(ShopRefusal.None, book.Claim(delivery.Order, 3, out var claimed));
            Assert.AreEqual(ShopRefusal.AlreadyClaimed, book.Claim(delivery.Order, 4, out _));

            Assert.AreEqual(delivery.Order, claimed.Order);
            Assert.AreEqual(0, book.Waiting.Count);
            Assert.AreEqual(1, book.Receipts.Count);
            Assert.AreEqual(3UL, book.Receipts[0].Claimer);
            Assert.AreEqual(1UL, book.Receipts[0].Delivery.Buyer);
        }

        [Test]
        public void 없는_주문_수령은_거절한다()
        {
            var book = new ShopDeliveryBook(8);
            Assert.AreEqual(ShopRefusal.NotFound, book.Claim(5, 1, out _));
        }

        [Test]
        public void 주문_번호는_수령_뒤에도_겹치지_않는다()
        {
            var book = new ShopDeliveryBook(8);
            var funds = new Funds(100);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out var first);
            book.Claim(first.Order, 1, out _);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out var second);

            Assert.AreEqual(2, second.Order);
        }

        [Test]
        public void 수령_기록은_정해진_수만_남긴다()
        {
            var book = new ShopDeliveryBook(1);
            var funds = new Funds(1000);
            for (int i = 0; i < ShopDeliveryBook.ReceiptLimit + 5; i++)
            {
                book.Order(Catalog, "tool-a", 1, funds.Pay, out var delivery);
                book.Claim(delivery.Order, 1, out _);
            }

            Assert.AreEqual(ShopDeliveryBook.ReceiptLimit, book.Receipts.Count);
            Assert.AreEqual(6, book.Receipts[0].Delivery.Order);
        }

        [Test]
        public void 초기화하면_번호부터_다시_시작한다()
        {
            var book = new ShopDeliveryBook(8);
            var funds = new Funds(100);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out _);
            long revision = book.Revision;

            book.Clear();

            Assert.AreEqual(0, book.Waiting.Count);
            Assert.Greater(book.Revision, revision);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out var next);
            Assert.AreEqual(1, next.Order);
        }

        [Test]
        public void 세대_교체는_배송구를_비우고_번호는_이어_간다()
        {
            var book = new ShopDeliveryBook(8);
            var funds = new Funds(100);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out _);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out var claimed);
            book.Claim(claimed.Order, 1, out _);

            book.ClearRun();

            Assert.AreEqual(0, book.Waiting.Count);
            Assert.AreEqual(0, book.Receipts.Count);
            book.Order(Catalog, "tool-a", 1, funds.Pay, out var next);
            Assert.AreEqual(3, next.Order);
        }

        [Test]
        public void 장부_설명은_공용_장부_길이_제한을_넘지_않는다()
        {
            var item = new ShopItem("long", new string('가', 200), 1);
            Assert.LessOrEqual(ShopDeliveryBook.LedgerText(item).Length, 100);
        }
    }
}
