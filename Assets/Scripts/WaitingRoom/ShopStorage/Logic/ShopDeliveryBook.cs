using System;
using System.Collections.Generic;

namespace CurtainCall.WaitingRoom
{
    /// <summary>상점 품목(임시값). 가격은 공동 자금 단위.</summary>
    public readonly struct ShopItem
    {
        public string Id { get; }
        public string Name { get; }
        public long Price { get; }

        public ShopItem(string id, string name, long price)
        {
            Id = id;
            Name = name;
            Price = price;
        }
    }

    /// <summary>배송구 바닥에 놓인 물건 한 개. 주문 번호는 런 동안 1부터 오른다.</summary>
    public readonly struct ShopDelivery
    {
        public int Order { get; }
        public string ItemId { get; }
        public string ItemName { get; }
        public ulong Buyer { get; }

        public ShopDelivery(int order, string itemId, string itemName, ulong buyer)
        {
            Order = order;
            ItemId = itemId;
            ItemName = itemName;
            Buyer = buyer;
        }
    }

    /// <summary>수령 기록. 누가 산 물건을 누가 받았는지.</summary>
    public readonly struct ShopReceipt
    {
        public ShopDelivery Delivery { get; }
        public ulong Claimer { get; }

        public ShopReceipt(ShopDelivery delivery, ulong claimer)
        {
            Delivery = delivery;
            Claimer = claimer;
        }
    }

    public enum ShopRefusal : byte
    {
        None,
        UnknownItem,
        SlotFull,
        NotEnoughFunds,
        NotFound,
        AlreadyClaimed,
    }

    /// <summary>
    /// 배송구 장부(판정 화면 전용 순수 로직). 구매하면 배송구 바닥에 놓이고, 한 사람만 한 번 수령한다.
    /// 자금 차감은 <c>pay</c>가 맡는다(공용 공동 자금). 자리가 없으면 돈을 쓰기 전에 거절해, 돈만 빠지는 일이 없다.
    /// </summary>
    public sealed class ShopDeliveryBook
    {
        /// <summary>남겨 두는 수령 기록 수.</summary>
        public const int ReceiptLimit = 20;

        readonly List<ShopDelivery> waiting = new();
        readonly List<ShopReceipt> receipts = new();
        int lastOrder;

        public ShopDeliveryBook(int capacity) => Capacity = Math.Max(1, capacity);

        /// <summary>배송구 바닥에 동시에 놓일 수 있는 최대 개수.</summary>
        public int Capacity { get; }

        /// <summary>바뀔 때마다 오른다(공유 상태 비교용).</summary>
        public long Revision { get; private set; }

        public IReadOnlyList<ShopDelivery> Waiting => waiting.AsReadOnly();
        public IReadOnlyList<ShopReceipt> Receipts => receipts.AsReadOnly();

        public static bool TryFind(IReadOnlyList<ShopItem> catalog, string id, out ShopItem item)
        {
            item = default;
            if (catalog == null || string.IsNullOrEmpty(id)) return false;
            foreach (var candidate in catalog)
            {
                if (candidate.Id != id) continue;
                item = candidate;
                return true;
            }
            return false;
        }

        /// <summary>구매. 성공하면 <paramref name="pay"/>(가격, 장부 설명)가 한 번 불리고 배송구에 물건이 놓인다.</summary>
        public ShopRefusal Order(IReadOnlyList<ShopItem> catalog, string itemId, ulong buyer,
            Func<long, string, bool> pay, out ShopDelivery delivery)
        {
            delivery = default;
            if (!TryFind(catalog, itemId, out var item) || item.Price <= 0) return ShopRefusal.UnknownItem;
            if (waiting.Count >= Capacity) return ShopRefusal.SlotFull;
            if (pay == null || !pay(item.Price, LedgerText(item))) return ShopRefusal.NotEnoughFunds;

            delivery = new ShopDelivery(++lastOrder, item.Id, item.Name, buyer);
            waiting.Add(delivery);
            Revision++;
            return ShopRefusal.None;
        }

        /// <summary>수령. 먼저 도착한 요청 한 번만 성공한다.</summary>
        public ShopRefusal Claim(int order, ulong claimer, out ShopDelivery delivery)
        {
            delivery = default;
            int index = waiting.FindIndex(d => d.Order == order);
            if (index < 0)
                return receipts.Exists(r => r.Delivery.Order == order) ? ShopRefusal.AlreadyClaimed : ShopRefusal.NotFound;

            delivery = waiting[index];
            waiting.RemoveAt(index);
            receipts.Add(new ShopReceipt(delivery, claimer));
            if (receipts.Count > ReceiptLimit) receipts.RemoveAt(0);
            Revision++;
            return ShopRefusal.None;
        }

        /// <summary>세대 교체: 배송구·수령 기록을 비우되 주문 번호는 이어 간다(물려줄 선반 기물 번호와 겹치지 않게).</summary>
        public void ClearRun()
        {
            if (waiting.Count == 0 && receipts.Count == 0) return;
            waiting.Clear();
            receipts.Clear();
            Revision++;
        }

        public void Clear()
        {
            if (waiting.Count == 0 && receipts.Count == 0 && lastOrder == 0) return;
            waiting.Clear();
            receipts.Clear();
            lastOrder = 0;
            Revision++;
        }

        /// <summary>공동 자금 장부에 남길 설명(공용 장부 100자 제한 안).</summary>
        public static string LedgerText(ShopItem item)
        {
            string text = WaitingRoomText.Format("ledger.shop-purchase", "상점 구매: {item}", ("item", item.Name));
            return text.Length > 100 ? text.Substring(0, 100) : text;
        }
    }
}
