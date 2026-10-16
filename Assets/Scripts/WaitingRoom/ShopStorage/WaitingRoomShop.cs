using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Reward;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ③ 상점의 공개 진입점: 수화기 구매 → 배송구 수령. 다른 기능은 이 컴포넌트만 쓴다.
    /// 판정은 공동 자금과 같은 화면(호스트 또는 오프라인 런, <see cref="PerformanceRewards.HasAuthority"/>)이 한다.
    /// 참가자는 요청만 보내고, 판정 화면이 배송구 상태를 모두에게 보낸다. 잔액·장부는 공용 공동 자금이 모두에게 같게 보낸다.
    /// 품목·가격은 임시값(M1 범위 밖)이다.
    /// </summary>
    public sealed class WaitingRoomShop : MonoBehaviour
    {
        const string StateMessage = "CurtainCall.WaitingRoom.Shop.State";
        const string RequestMessage = "CurtainCall.WaitingRoom.Shop.Request";
        const string ResultMessage = "CurtainCall.WaitingRoom.Shop.Result";
        const int MaxPayloadBytes = 16 * 1024;
        const int SyncRequest = 0, BuyRequest = 1, ClaimRequest = 2;

        [Serializable]
        sealed class Entry
        {
            public string id;
            public string name;
            [Min(1)] public long price = 1;
        }

        [Header("임시값 — 정식 품목·가격 아님(M1 범위 밖)")]
        [SerializeField] List<Entry> items = new()
        {
            new Entry { id = "tool-a", name = "임시 공연 도구 A", price = 10 },
            new Entry { id = "tool-b", name = "임시 공연 도구 B", price = 15 },
            new Entry { id = "escape", name = "임시 탈출 도구", price = 20 },
        };

        [Tooltip("배송구 바닥에 동시에 놓일 수 있는 최대 개수(메시지 크기 보호용 임시값).")]
        [SerializeField, Min(1)] int slotCapacity = 8;

        public static WaitingRoomShop Instance { get; private set; }

        readonly List<ShopItem> catalog = new();
        readonly List<ShopDelivery> receivedWaiting = new();
        readonly List<ShopReceipt> receivedReceipts = new();
        readonly Dictionary<ulong, float> requestTimes = new();
        ShopDeliveryBook book;
        PerformanceRewards rewards;
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        bool messagingHost;
        float nextSync;
        bool synced;

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

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Shop] 상점이 둘 이상입니다. 나중 것은 끕니다.", this);
                enabled = false;
                return;
            }
            Instance = this;
            book = new ShopDeliveryBook(slotCapacity);
            foreach (var entry in items)
                if (entry != null && !string.IsNullOrEmpty(entry.id) && entry.price > 0)
                    catalog.Add(new ShopItem(entry.id, string.IsNullOrEmpty(entry.name) ? entry.id : entry.name, entry.price));
        }

        void OnDestroy()
        {
            Unregister();
            if (session != null) session.ConnectionStateChanged -= HandleConnection;
            if (rewards != null) rewards.Changed -= NotifyChanged;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            Bind();
            if (session != null && session.ConnectionState == SessionConnectionState.Client
                && Time.unscaledTime >= nextSync)
            {
                // 늦게 들어왔거나 상태를 놓쳤을 때 다시 받는다. 받은 뒤에는 드물게만 확인한다.
                nextSync = Time.unscaledTime + (synced ? 10f : 1f);
                Send(SyncRequest, null, 0);
            }
        }

        /// <summary>수화기로 품목을 주문한다. 결과는 <see cref="RequestAnswered"/>로 온다.</summary>
        public void RequestPurchase(string itemId)
        {
            if (HasAuthority) Answer(LocalClientId, Buy(itemId, LocalClientId));
            else if (!Send(BuyRequest, itemId, 0)) Answer(LocalClientId, (false, "접속 중이 아니라 주문할 수 없습니다."));
        }

        /// <summary>배송구의 물건을 받는다. 같은 물건은 먼저 요청한 한 명만 받는다.</summary>
        public void RequestClaim(int order)
        {
            if (HasAuthority) Answer(LocalClientId, Claim(order, LocalClientId));
            else if (!Send(ClaimRequest, null, order)) Answer(LocalClientId, (false, "접속 중이 아니라 받을 수 없습니다."));
        }

        /// <summary>화면 표시용 이름(접속 슬롯 기준).</summary>
        public static string PlayerLabel(ulong clientId)
        {
            foreach (var player in NetworkPlayer.All)
                if (player.IsSpawned && player.OwnerClientId == clientId)
                    return player.Slot >= 0 ? $"플레이어 {player.Slot + 1}" : $"플레이어 #{clientId}";
            return NetworkPlayer.All.Count == 0 ? "나" : $"플레이어 #{clientId}";
        }

        ulong LocalClientId => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.LocalClientId : NetworkManager.ServerClientId;

        (bool, string) Buy(string itemId, ulong buyer)
        {
            var refusal = book.Order(catalog, itemId, buyer, rewards.TrySpend, out var delivery);
            if (refusal != ShopRefusal.None) return (false, Explain(refusal));
            Debug.Log($"[Shop] 구매 #{delivery.Order} {delivery.ItemName} ← {PlayerLabel(buyer)} / 잔액 {rewards.Balance}");
            Publish();
            return (true, $"{delivery.ItemName} 주문 완료 — 배송구를 확인하세요.");
        }

        (bool, string) Claim(int order, ulong claimer)
        {
            var refusal = book.Claim(order, claimer, out var delivery);
            if (refusal != ShopRefusal.None) return (false, Explain(refusal));
            Debug.Log($"[Shop] 수령 #{delivery.Order} {delivery.ItemName} → {PlayerLabel(claimer)}");
            Publish();
            return (true, $"{delivery.ItemName} 수령");
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
            if (target == LocalClientId || messaging == null)
            {
                LastMessage = result.text;
                RequestAnswered?.Invoke(result.ok, result.text);
                return;
            }
            Write(ResultMessage, JsonUtility.ToJson(new ResultData { ok = result.ok, text = result.text }), target);
        }

        // ---- 연결 ----

        void Bind()
        {
            if (rewards == null)
            {
                rewards = PerformanceRewards.EnsureExists();
                rewards.Changed += NotifyChanged;
            }
            if (session == null)
            {
                session = NetworkSessionManager.EnsureExists();
                if (session != null) session.ConnectionStateChanged += HandleConnection;
            }
            var next = session != null && (session.ConnectionState == SessionConnectionState.Host
                || session.ConnectionState == SessionConnectionState.Client) ? session.NetworkManager?.CustomMessagingManager : null;
            bool host = session != null && session.IsHost;
            if (next == messaging && messagingHost == host) return;
            Unregister();
            messaging = next;
            messagingHost = host;
            if (messaging == null) return;
            if (host) messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
            else
            {
                messaging.RegisterNamedMessageHandler(StateMessage, HandleState);
                messaging.RegisterNamedMessageHandler(ResultMessage, HandleResult);
                synced = false;
                nextSync = 0;
            }
        }

        void Unregister()
        {
            if (messaging == null) return;
            if (messagingHost) messaging.UnregisterNamedMessageHandler(RequestMessage);
            else
            {
                messaging.UnregisterNamedMessageHandler(StateMessage);
                messaging.UnregisterNamedMessageHandler(ResultMessage);
            }
            messaging = null;
        }

        void HandleConnection(SessionConnectionState connection)
        {
            // 방이 바뀌면 이전 방의 배송구를 남기지 않는다.
            book.Clear();
            receivedWaiting.Clear();
            receivedReceipts.Clear();
            requestTimes.Clear();
            synced = false;
            LastMessage = "";
            NotifyChanged();
        }

        void Publish()
        {
            if (messaging != null && session != null && session.IsHost) Write(StateMessage, JsonUtility.ToJson(StateData.From(book)), null);
            NotifyChanged();
        }

        void NotifyChanged() => Changed?.Invoke();

        bool Send(int kind, string itemId, int order)
        {
            if (messaging == null || session == null || session.ConnectionState != SessionConnectionState.Client) return false;
            return Write(RequestMessage, JsonUtility.ToJson(new RequestData { kind = kind, item = itemId, order = order }),
                NetworkManager.ServerClientId);
        }

        bool Write(string message, string json, ulong? target)
        {
            int size = FastBufferWriter.GetWriteSize(json);
            if (size > MaxPayloadBytes) { Debug.LogError("[Shop] 배송구 상태가 메시지 상한을 넘었습니다."); return false; }
            try
            {
                using var writer = new FastBufferWriter(size, Allocator.Temp);
                writer.WriteValueSafe(json);
                if (target.HasValue) messaging.SendNamedMessage(message, target.Value, writer, NetworkDelivery.ReliableSequenced);
                else messaging.SendNamedMessageToAll(message, writer, NetworkDelivery.ReliableSequenced);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return false;
            }
        }

        static bool InvalidMessage(Exception error) =>
            error is OverflowException || error is ArgumentException || error is IndexOutOfRangeException;

        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            if (!HasAuthority || reader.Length > 1024 || !IsConnected(sender)) return;
            if (requestTimes.TryGetValue(sender, out float last) && Time.unscaledTime - last < .2f) return;
            requestTimes[sender] = Time.unscaledTime;
            try
            {
                reader.ReadValueSafe(out string json);
                var request = JsonUtility.FromJson<RequestData>(json);
                if (request == null) return;
                switch (request.kind)
                {
                    case SyncRequest: Write(StateMessage, JsonUtility.ToJson(StateData.From(book)), sender); break;
                    case BuyRequest: Answer(sender, Buy(request.item, sender)); break;
                    case ClaimRequest: Answer(sender, Claim(request.order, sender)); break;
                }
            }
            catch (Exception error) when (InvalidMessage(error)) { Debug.LogWarning("[Shop] 잘못된 상점 요청을 거절했습니다."); }
        }

        void HandleState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length > MaxPayloadBytes) return;
            StateData state;
            try
            {
                reader.ReadValueSafe(out string json);
                state = JsonUtility.FromJson<StateData>(json);
            }
            catch (Exception error) when (InvalidMessage(error)) { Debug.LogWarning("[Shop] 잘못된 배송구 상태를 무시했습니다."); return; }
            if (state == null) return;
            receivedWaiting.Clear();
            foreach (var d in state.waiting ?? Array.Empty<DeliveryData>()) receivedWaiting.Add(d.Read());
            receivedReceipts.Clear();
            foreach (var r in state.receipts ?? Array.Empty<ReceiptData>()) receivedReceipts.Add(new ShopReceipt(r.delivery.Read(), r.claimer));
            synced = true;
            NotifyChanged();
        }

        void HandleResult(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length > 1024) return;
            ResultData result;
            try
            {
                reader.ReadValueSafe(out string json);
                result = JsonUtility.FromJson<ResultData>(json);
            }
            catch (Exception error) when (InvalidMessage(error)) { return; }
            if (result == null) return;
            LastMessage = result.text;
            RequestAnswered?.Invoke(result.ok, result.text);
        }

        bool IsConnected(ulong clientId)
        {
            var manager = session?.NetworkManager;
            return manager != null && manager.ConnectedClients.ContainsKey(clientId);
        }

        // ---- 메시지 형식 ----

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
