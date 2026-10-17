using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ③ 상점의 공개 진입점: 수화기 구매 → 배송구 수령. 다른 기능은 이 컴포넌트만 쓴다.
    /// 판정은 공동 자금과 같은 화면(호스트 또는 오프라인 런, <see cref="PerformanceRewards.HasAuthority"/>)이 한다.
    /// 참가자는 요청만 보내고, 판정 화면이 배송구 상태를 모두에게 보낸다. 잔액·장부는 공용 공동 자금이 모두에게 같게 보낸다.
    /// 받은 물건은 <see cref="PropCarry"/>가 손에 들린다(한도를 넘으면 받지 못한다). 품목·가격은 임시값(M1 범위 밖)이다.
    /// 런 동안 남도록 씬이 바뀌어도 유지된다(DontDestroyOnLoad, 씬 최상위에 둔다). 화면 표시(수화기·배송구)는 대기실 씬에 둔다.
    /// </summary>
    public sealed class WaitingRoomShop : MonoBehaviour
    {
        const int SyncRequest = 0, BuyRequest = 1, ClaimRequest = 2;

        [Serializable]
        sealed class Entry
        {
            public string id;
            public string name;
            [Min(1)] public long price = 1;
            [Tooltip("받으면 메는 배낭(칸을 차지하지 않고 소지 한도 +). 한 사람 한 개.")]
            public bool backpack;
        }

        [Header("임시값 — 정식 품목·가격 아님(M1 범위 밖)")]
        [SerializeField] List<Entry> items = new()
        {
            new Entry { id = "tool-a", name = "임시 공연 도구 A", price = 10 },
            new Entry { id = "tool-b", name = "임시 공연 도구 B", price = 15 },
            new Entry { id = "escape", name = "임시 탈출 도구", price = 20 },
            new Entry { id = "backpack", name = "임시 배낭", price = 25, backpack = true },
        };

        [Tooltip("배송구 바닥에 동시에 놓일 수 있는 최대 개수(메시지 크기 보호용 임시값).")]
        [SerializeField, Min(1)] int slotCapacity = 8;

        public static WaitingRoomShop Instance { get; private set; }

        readonly List<ShopItem> catalog = new();
        readonly HashSet<string> backpackIds = new();
        readonly List<ShopDelivery> receivedWaiting = new();
        readonly List<ShopReceipt> receivedReceipts = new();
        ShopDeliveryBook book;
        PerformanceRewards rewards;
        ShopChannel channel;
        int seenGeneration;

        /// <summary>판매 품목(임시값).</summary>
        public IReadOnlyList<ShopItem> Catalog => catalog;

        /// <summary>배송구 바닥에 놓여 아직 아무도 받지 않은 물건(주문 순).</summary>
        public IReadOnlyList<ShopDelivery> Waiting => HasAuthority ? book.Waiting : receivedWaiting;

        /// <summary>최근 수령 기록.</summary>
        public IReadOnlyList<ShopReceipt> Receipts => HasAuthority ? book.Receipts : receivedReceipts;

        /// <summary>공동 자금 잔액(공용 보상 시스템 값).</summary>
        public long Balance => rewards != null ? rewards.Balance : 0;

        /// <summary>내 마지막 요청 결과 문구(화면 안내용).</summary>
        public string LastMessage { get; private set; } = "";

        /// <summary>이 화면이 구매·수령을 판정한다.</summary>
        public bool HasAuthority => rewards != null && rewards.HasAuthority;

        /// <summary>배송구 상태가 바뀌었다(모든 화면).</summary>
        public event Action Changed;

        /// <summary>내 요청이 판정됐다(성공 여부, 안내 문구).</summary>
        public event Action<bool, string> RequestAnswered;

        /// <summary>받으면 메는 배낭 품목인지.</summary>
        public bool IsBackpack(string itemId) => itemId != null && backpackIds.Contains(itemId);

        void Awake()
        {
            // 대기실 씬이 다시 열리면 씬에 놓인 사본은 버리고, 런 동안 남아 있던 상점을 그대로 쓴다.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            else Debug.LogWarning("[Shop] 상점은 씬 최상위 오브젝트에 둬야 장소 전환 뒤에도 남습니다.", this);
            book = new ShopDeliveryBook(slotCapacity);
            foreach (var entry in items)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.price <= 0) continue;
                catalog.Add(new ShopItem(entry.id, string.IsNullOrEmpty(entry.name) ? entry.id : entry.name, entry.price));
                if (entry.backpack) backpackIds.Add(entry.id);
            }
            channel = new ShopChannel("CurtainCall.WaitingRoom.Shop", HandleRequest, HandleState, HandleResult, HandleReset,
                () => channel.Request(JsonUtility.ToJson(new RequestData { kind = SyncRequest })));
        }

        void OnDestroy()
        {
            channel?.Dispose();
            if (rewards != null) rewards.Changed -= NotifyChanged;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (rewards == null)
            {
                rewards = PerformanceRewards.EnsureExists();
                rewards.Changed += NotifyChanged;
            }
            channel.Update();
            WatchGeneration();
        }

        // 세대 교체(런 실패) 때 배송구를 비운다. 공동 자금은 공용 보상 시스템이 초기화한다.
        void WatchGeneration()
        {
            var run = CampaignRun.Instance;
            if (run == null || !HasAuthority) return;
            int generation = run.State.Generation;
            if (seenGeneration > 0 && generation > seenGeneration)
            {
                book.ClearRun();
                Debug.Log($"[Shop] 세대 교체 {seenGeneration} → {generation}: 배송구를 비웠습니다.");
                Publish();
            }
            if (generation > 0) seenGeneration = generation;
        }

        /// <summary>수화기로 품목을 주문한다. 결과는 <see cref="RequestAnswered"/>로 온다.</summary>
        public void RequestPurchase(string itemId)
        {
            if (HasAuthority) Answer(channel.LocalClientId, Buy(itemId, channel.LocalClientId));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = BuyRequest, item = itemId })))
                Answer(channel.LocalClientId, (false, "접속 중이 아니라 주문할 수 없습니다."));
        }

        /// <summary>배송구의 물건을 받는다. 같은 물건은 먼저 요청한 한 명만 받는다.</summary>
        public void RequestClaim(int order)
        {
            if (HasAuthority) Answer(channel.LocalClientId, Claim(order, channel.LocalClientId));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = ClaimRequest, order = order })))
                Answer(channel.LocalClientId, (false, "접속 중이 아니라 받을 수 없습니다."));
        }

        /// <summary>화면 표시용 이름(접속 슬롯 기준).</summary>
        public static string PlayerLabel(ulong clientId)
        {
            foreach (var player in NetworkPlayer.All)
                if (player.IsSpawned && player.OwnerClientId == clientId)
                    return player.Slot >= 0 ? $"플레이어 {player.Slot + 1}" : $"플레이어 #{clientId}";
            return NetworkPlayer.All.Count == 0 ? "나" : $"플레이어 #{clientId}";
        }

        /// <summary>공연 중에는 구매·보관을 하지 않는다(게임 흐름 기획 2-3: 쉬는 시간·밤 정비만).</summary>
        public static bool InPerformance => CampaignRun.Instance != null && CampaignRun.Instance.State.Phase == CampaignPhase.Performance;

        const string PerformanceRefusal = "공연 중에는 상점을 쓸 수 없습니다.";

        (bool, string) Buy(string itemId, ulong buyer)
        {
            if (InPerformance) return (false, PerformanceRefusal);
            var refusal = book.Order(catalog, itemId, buyer, rewards.TrySpend, out var delivery);
            if (refusal != ShopRefusal.None) return (false, Explain(refusal));
            Debug.Log($"[Shop] 구매 #{delivery.Order} {delivery.ItemName} ← {PlayerLabel(buyer)} / 잔액 {rewards.Balance}");
            Publish();
            return (true, $"{delivery.ItemName} 주문 완료 — 배송구를 확인하세요.");
        }

        (bool, string) Claim(int order, ulong claimer)
        {
            if (InPerformance) return (false, PerformanceRefusal);
            // 손에 들 수 있는지 먼저 본다. 못 들면 배송구에 그대로 남는다.
            int index = -1;
            for (int i = 0; i < book.Waiting.Count; i++) if (book.Waiting[i].Order == order) { index = i; break; }
            var carry = PropCarry.Instance;
            if (index >= 0 && carry != null)
            {
                var carryRefusal = carry.CanReceive(claimer, IsBackpack(book.Waiting[index].ItemId));
                if (carryRefusal != CarryRefusal.None) return (false, PropCarry.Explain(carryRefusal));
            }

            var refusal = book.Claim(order, claimer, out var delivery);
            if (refusal != ShopRefusal.None) return (false, Explain(refusal));
            bool backpack = IsBackpack(delivery.ItemId);
            if (carry != null) carry.Receive(claimer, new PropItem(delivery.Order, delivery.ItemId, delivery.ItemName), backpack);
            Debug.Log($"[Shop] 수령 #{delivery.Order} {delivery.ItemName} → {PlayerLabel(claimer)}");
            Publish();
            return (true, backpack ? $"{delivery.ItemName}을 멨습니다." : $"{delivery.ItemName} 수령");
        }

        static string Explain(ShopRefusal refusal) => refusal switch
        {
            ShopRefusal.UnknownItem => "없는 품목입니다.",
            ShopRefusal.SlotFull => "배송구가 가득 찼습니다. 먼저 물건을 받아 주세요.",
            ShopRefusal.NotEnoughFunds => "공동 자금이 모자라거나 지금은 쓸 수 없습니다.",
            ShopRefusal.AlreadyClaimed => "이미 다른 사람이 받았습니다.",
            _ => "배송구에 없는 물건입니다.",
        };

        void Answer(ulong target, (bool ok, string text) result)
        {
            if (target == channel.LocalClientId || !channel.IsHost)
            {
                LastMessage = result.text;
                RequestAnswered?.Invoke(result.ok, result.text);
                return;
            }
            channel.SendResult(target, JsonUtility.ToJson(new ResultData { ok = result.ok, text = result.text }));
        }

        void Publish()
        {
            channel.SendState(JsonUtility.ToJson(StateData.From(book)));
            NotifyChanged();
        }

        void NotifyChanged() => Changed?.Invoke();

        // ---- 메시지 ----

        void HandleRequest(ulong sender, string json)
        {
            if (!HasAuthority) return;
            var request = JsonUtility.FromJson<RequestData>(json);
            if (request == null) return;
            switch (request.kind)
            {
                case SyncRequest: channel.SendState(JsonUtility.ToJson(StateData.From(book)), sender); break;
                case BuyRequest: Answer(sender, Buy(request.item, sender)); break;
                case ClaimRequest: Answer(sender, Claim(request.order, sender)); break;
            }
        }

        void HandleState(string json)
        {
            var state = JsonUtility.FromJson<StateData>(json);
            if (state == null) return;
            receivedWaiting.Clear();
            foreach (var d in state.waiting ?? Array.Empty<DeliveryData>()) receivedWaiting.Add(d.Read());
            receivedReceipts.Clear();
            foreach (var r in state.receipts ?? Array.Empty<ReceiptData>()) receivedReceipts.Add(new ShopReceipt(r.delivery.Read(), r.claimer));
            NotifyChanged();
        }

        void HandleResult(string json)
        {
            var result = JsonUtility.FromJson<ResultData>(json);
            if (result == null) return;
            LastMessage = result.text;
            RequestAnswered?.Invoke(result.ok, result.text);
        }

        void HandleReset()
        {
            // 방이 바뀌면 이전 방의 배송구를 남기지 않는다.
            book.Clear();
            receivedWaiting.Clear();
            receivedReceipts.Clear();
            seenGeneration = 0;
            LastMessage = "";
            NotifyChanged();
        }

        [Serializable] sealed class RequestData { public int kind; public string item; public int order; }
        [Serializable] sealed class ResultData { public bool ok; public string text; }

        [Serializable]
        sealed class DeliveryData
        {
            public int order; public string item; public string name; public ulong buyer;
            public ShopDelivery Read() => new(order, item, name, buyer);
            public static DeliveryData From(ShopDelivery d) => new() { order = d.Order, item = d.ItemId, name = d.ItemName, buyer = d.Buyer };
        }

        [Serializable] sealed class ReceiptData { public DeliveryData delivery; public ulong claimer; }

        [Serializable]
        sealed class StateData
        {
            public long revision;
            public DeliveryData[] waiting;
            public ReceiptData[] receipts;

            public static StateData From(ShopDeliveryBook book)
            {
                var data = new StateData
                {
                    revision = book.Revision,
                    waiting = new DeliveryData[book.Waiting.Count],
                    receipts = new ReceiptData[book.Receipts.Count],
                };
                for (int i = 0; i < data.waiting.Length; i++) data.waiting[i] = DeliveryData.From(book.Waiting[i]);
                for (int i = 0; i < data.receipts.Length; i++)
                    data.receipts[i] = new ReceiptData { delivery = DeliveryData.From(book.Receipts[i].Delivery), claimer = book.Receipts[i].Claimer };
                return data;
            }
        }
    }
}
