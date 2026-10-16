using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Survival;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 기물 소지의 공개 진입점: 손에 든 기물·배낭·바닥 기물. 판정은 상점과 같은 화면(호스트 또는 오프라인 런)이 한다.
    /// - 한도: 공용 <see cref="SurvivalRules.ItemSlots(PlayerCondition, bool)"/>(공통 세팅 · 기물 소지: 팔 2개 2, 팔 1개 1, 배낭 +2).
    /// - F 길게: 마지막에 든 기물을 발밑에 내려놓는다. 바닥 기물은 누구나 F로 줍는다(<see cref="FloorPropsView"/>).
    /// - 팔을 잃어 한도가 줄면 넘친 만큼 그 자리에 떨어진다. 사망 중·공연 중에는 떨어뜨리지 않는다(공연 사망·잡힘에도 기물 유지).
    /// - ④ 소품실: 바닥 기물은 런 동안 개수 제한 없이 남는다. 물려줄 선반 4칸(<see cref="HandoverShelfView"/>)에 올린 기물만 세대 교체 뒤에도 남는다.
    /// - 세대 교체(런 실패) 때 선반만 남기고 손·배낭·바닥 기물을 비운다(게임 흐름 기획 5-5·6-2). 방이 바뀌면 선반까지 비운다.
    /// 런 동안 남도록 씬이 바뀌어도 유지된다(DontDestroyOnLoad, 씬 최상위에 둔다). 표시·HUD·한도 초과 떨어뜨리기는 대기실 씬이 열려 있을 때만 한다.
    /// 탈출로 리타이어 때 소실은 이 범위 밖이다.
    /// </summary>
    public sealed class PropCarry : MonoBehaviour
    {
        const int SyncRequest = 0, DropRequest = 1, PickUpRequest = 2, ShelfRequest = 3;

        [Tooltip("바닥에 동시에 둘 수 있는 기물 수(메시지 크기 보호용 임시값).")]
        [SerializeField, Min(1)] int floorCapacity = 32;

        [Tooltip("내려놓을 때 캐릭터 앞쪽으로 떨어지는 거리(m).")]
        [SerializeField, Min(0f)] float dropAhead = .45f;

        [Tooltip("화면 왼쪽 아래에 내 소지·플레이어별 소지를 표시한다(공동 테스트 확인용).")]
        [SerializeField] bool showHud = true;

        public static PropCarry Instance { get; private set; }

        readonly Dictionary<ulong, List<PropItem>> receivedHeld = new();
        readonly HashSet<ulong> receivedBackpacks = new();
        readonly List<FloorProp> receivedFloor = new();
        readonly PropItem[] receivedShelf = new PropItem[ItemCarryBook.ShelfSlots];
        readonly List<FloorProp> trimmed = new();
        static readonly IReadOnlyList<PropItem> Empty = new List<PropItem>().AsReadOnly();
        ItemCarryBook book;
        ShopChannel channel;
        int seenGeneration;

        /// <summary>바닥에 놓인 기물(모든 화면).</summary>
        public IReadOnlyList<FloorProp> Floor => HasAuthority ? book.Floor : receivedFloor;

        /// <summary>물려줄 선반 4칸(0 = 맨 아래, 빈 칸은 Uid 0).</summary>
        public IReadOnlyList<PropItem> Shelf => HasAuthority ? book.Shelf : receivedShelf;

        /// <summary>대기실 씬(기물 표시)이 열려 있다.</summary>
        static bool InWaitingRoom => FloorPropsView.Active;

        /// <summary>내 접속 번호.</summary>
        public ulong LocalClientId => channel != null ? channel.LocalClientId : 0;

        /// <summary>내 마지막 요청 결과 문구.</summary>
        public string LastMessage { get; private set; } = "";

        /// <summary>이 화면이 판정한다(공동 자금과 같은 기준).</summary>
        public bool HasAuthority
        {
            get
            {
                var rewards = PerformanceRewards.Instance;
                return rewards != null && rewards.HasAuthority;
            }
        }

        /// <summary>소지·바닥 상태가 바뀌었다(모든 화면).</summary>
        public event Action Changed;

        public IReadOnlyList<PropItem> Held(ulong player) => HasAuthority ? book.Held(player)
            : receivedHeld.TryGetValue(player, out var list) ? list.AsReadOnly() : Empty;

        public bool HasBackpack(ulong player) => HasAuthority ? book.HasBackpack(player) : receivedBackpacks.Contains(player);

        /// <summary>지금 몸 상태로 지닐 수 있는 개수.</summary>
        public int Slots(ulong player) => SurvivalRules.ItemSlots(FindCondition(player), HasBackpack(player));

        void Awake()
        {
            // 대기실 씬이 다시 열리면 씬에 놓인 사본은 버리고, 런 동안 남아 있던 장부를 그대로 쓴다.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            else Debug.LogWarning("[Carry] 기물 소지는 씬 최상위 오브젝트에 둬야 장소 전환 뒤에도 남습니다.", this);
            book = new ItemCarryBook(floorCapacity);
            channel = new ShopChannel("CurtainCall.WaitingRoom.Carry", HandleRequest, HandleState, HandleResult, HandleReset,
                () => channel.Request(JsonUtility.ToJson(new RequestData { kind = SyncRequest })));
        }

        void OnEnable() => ShopReach.LongPressed += RequestDrop;

        void OnDisable() => ShopReach.LongPressed -= RequestDrop;

        void OnDestroy()
        {
            channel?.Dispose();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            channel.Update();
            if (!HasAuthority) return;
            WatchGeneration();
            if (InWaitingRoom) TrimOverLimit();
        }

        void WatchGeneration()
        {
            var run = CampaignRun.Instance;
            if (run == null) return;
            int generation = run.State.Generation;
            if (seenGeneration > 0 && generation > seenGeneration)
            {
                book.ResetGeneration();
                Debug.Log($"[Carry] 세대 교체 {seenGeneration} → {generation}: 물려줄 선반만 남기고 기물을 비웠습니다.");
                Publish();
            }
            if (generation > 0) seenGeneration = generation;
        }

        /// <summary>물려줄 선반을 쓴다(F): 들고 있으면 아래 빈 칸부터 올리고, 아니면 위 칸부터 꺼낸다.</summary>
        public void RequestShelf()
        {
            if (HasAuthority) Answer(LocalClientId, UseShelf(LocalClientId));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = ShelfRequest })))
                Answer(LocalClientId, (false, "접속 중이 아니라 선반을 쓸 수 없습니다."));
        }

        /// <summary>마지막에 든 기물을 발밑에 내려놓는다(F 길게).</summary>
        public void RequestDrop()
        {
            if (HasAuthority) Answer(LocalClientId, Drop(LocalClientId));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = DropRequest })))
                Answer(LocalClientId, (false, "접속 중이 아니라 내려놓을 수 없습니다."));
        }

        /// <summary>바닥 기물을 줍는다(F).</summary>
        public void RequestPickUp(int uid)
        {
            if (HasAuthority) Answer(LocalClientId, PickUp(uid, LocalClientId));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = PickUpRequest, uid = uid })))
                Answer(LocalClientId, (false, "접속 중이 아니라 주울 수 없습니다."));
        }

        public static string Explain(CarryRefusal refusal) => refusal switch
        {
            CarryRefusal.HandsFull => "손이 가득 찼습니다. F 길게로 내려놓은 뒤 받으세요.",
            CarryRefusal.AlreadyHasBackpack => "이미 배낭을 메고 있습니다.",
            CarryRefusal.NothingHeld => "들고 있는 기물이 없습니다.",
            CarryRefusal.FloorFull => "바닥에 더 놓을 수 없습니다.",
            CarryRefusal.ShelfFull => "선반 4칸이 모두 찼고 손에도 자리가 없습니다.",
            CarryRefusal.ShelfEmpty => "선반이 비어 있습니다. 기물을 들고 오세요.",
            _ => "이미 없는 기물입니다.",
        };

        // ---- 상점이 쓰는 판정 화면 전용 진입점 ----

        /// <summary>판정 화면: 상점 배송구에서 받을 수 있는지.</summary>
        public CarryRefusal CanReceive(ulong player, bool backpack) => book.CanTake(player, Slots(player), backpack);

        /// <summary>판정 화면: 상점 배송구에서 받은 기물을 들린다(또는 배낭을 메운다).</summary>
        public CarryRefusal Receive(ulong player, PropItem item, bool backpack)
        {
            var refusal = book.Take(player, Slots(player), item, backpack);
            if (refusal == CarryRefusal.None) Publish();
            return refusal;
        }

        // ---- 판정 ----

        (bool, string) Drop(ulong player)
        {
            if (!TryDropPoint(player, out var point)) return (false, "캐릭터를 찾지 못했습니다.");
            var refusal = book.Drop(player, point.x, point.y, point.z, out var dropped);
            if (refusal != CarryRefusal.None) return (false, Explain(refusal));
            Debug.Log($"[Carry] 내려놓음 #{dropped.Item.Uid} {dropped.Item.Name} ← {WaitingRoomShop.PlayerLabel(player)}");
            Publish();
            return (true, $"{dropped.Item.Name}을 내려놓았습니다.");
        }

        (bool, string) PickUp(int uid, ulong player)
        {
            var refusal = book.PickUp(uid, player, Slots(player), out var item);
            if (refusal != CarryRefusal.None) return (false, Explain(refusal));
            Debug.Log($"[Carry] 주움 #{item.Uid} {item.Name} → {WaitingRoomShop.PlayerLabel(player)}");
            Publish();
            return (true, $"{item.Name}을 주웠습니다.");
        }

        (bool, string) UseShelf(ulong player)
        {
            var refusal = book.UseShelf(player, Slots(player), out int slot, out bool placed, out var item);
            if (refusal != CarryRefusal.None) return (false, Explain(refusal));
            Debug.Log($"[Carry] 선반 {slot + 1}칸 {(placed ? "올림" : "꺼냄")} #{item.Uid} {item.Name} — {WaitingRoomShop.PlayerLabel(player)}");
            Publish();
            return (true, placed ? $"{item.Name}을 선반 {slot + 1}칸에 올렸습니다." : $"선반 {slot + 1}칸에서 {item.Name}을 꺼냈습니다.");
        }

        void TrimOverLimit()
        {
            var run = CampaignRun.Instance;
            if (run != null && run.State.Phase == CampaignPhase.Performance) return;
            bool changed = false;
            foreach (ulong player in new List<ulong>(book.Players))
            {
                if (IsDead(player) || !TryDropPoint(player, out var point)) continue;
                trimmed.Clear();
                if (book.Trim(player, Slots(player), point.x, point.y, point.z, trimmed) == 0) continue;
                changed = true;
                foreach (var prop in trimmed)
                    Debug.Log($"[Carry] 한도 초과로 떨어짐 #{prop.Item.Uid} {prop.Item.Name} ← {WaitingRoomShop.PlayerLabel(player)}");
            }
            if (changed) Publish();
        }

        void Answer(ulong target, (bool ok, string text) result)
        {
            if (target == LocalClientId || !channel.IsHost)
            {
                LastMessage = result.text;
                return;
            }
            channel.SendResult(target, JsonUtility.ToJson(new ResultData { ok = result.ok, text = result.text }));
        }

        void Publish()
        {
            channel.SendState(JsonUtility.ToJson(StateData.From(book)));
            Changed?.Invoke();
        }

        // ---- 캐릭터 찾기 ----

        static NetworkPlayer FindPlayer(ulong clientId)
        {
            foreach (var player in NetworkPlayer.All)
                if (player.IsSpawned && player.OwnerClientId == clientId) return player;
            return null;
        }

        static Transform FindBody(ulong clientId)
        {
            var player = FindPlayer(clientId);
            if (player != null) return player.transform;
            if (NetworkPlayer.All.Count > 0) return null;
            var reader = FindFirstObjectByType<PlayerInputReader>(); // 접속하지 않은 씬 확인
            return reader != null ? reader.transform : null;
        }

        static PlayerCondition FindCondition(ulong clientId)
        {
            var body = FindBody(clientId);
            return body != null ? body.GetComponent<PlayerCondition>() : null;
        }

        static bool IsDead(ulong clientId)
        {
            var player = FindPlayer(clientId);
            return player != null && (player.State == PlayerState.Fallen || player.State == PlayerState.Retired);
        }

        bool TryDropPoint(ulong clientId, out Vector3 point)
        {
            var body = FindBody(clientId);
            point = default;
            if (body == null) return false;
            Vector3 ahead = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized * dropAhead;
            point = body.position + ahead;
            if (Physics.Raycast(point + Vector3.up, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                point.y = hit.point.y;
            return true;
        }

        // ---- 메시지 ----

        void HandleRequest(ulong sender, string json)
        {
            if (!HasAuthority) return;
            var request = JsonUtility.FromJson<RequestData>(json);
            if (request == null) return;
            switch (request.kind)
            {
                case SyncRequest: channel.SendState(JsonUtility.ToJson(StateData.From(book)), sender); break;
                case DropRequest: Answer(sender, Drop(sender)); break;
                case PickUpRequest: Answer(sender, PickUp(request.uid, sender)); break;
                case ShelfRequest: Answer(sender, UseShelf(sender)); break;
            }
        }

        void HandleState(string json)
        {
            var state = JsonUtility.FromJson<StateData>(json);
            if (state == null) return;
            receivedHeld.Clear();
            receivedBackpacks.Clear();
            foreach (var p in state.players ?? Array.Empty<PlayerData>())
            {
                var list = new List<PropItem>();
                foreach (var item in p.held ?? Array.Empty<ItemData>()) list.Add(item.Read());
                receivedHeld[p.client] = list;
                if (p.backpack) receivedBackpacks.Add(p.client);
            }
            receivedFloor.Clear();
            foreach (var f in state.floor ?? Array.Empty<FloorData>()) receivedFloor.Add(new FloorProp(f.item.Read(), f.x, f.y, f.z));
            Array.Clear(receivedShelf, 0, receivedShelf.Length);
            var shelf = state.shelf ?? Array.Empty<ItemData>();
            for (int i = 0; i < shelf.Length && i < receivedShelf.Length; i++) receivedShelf[i] = shelf[i].Read();
            Changed?.Invoke();
        }

        void HandleResult(string json)
        {
            var result = JsonUtility.FromJson<ResultData>(json);
            if (result != null) LastMessage = result.text;
        }

        void HandleReset()
        {
            book.Clear();
            receivedHeld.Clear();
            receivedBackpacks.Clear();
            receivedFloor.Clear();
            Array.Clear(receivedShelf, 0, receivedShelf.Length);
            seenGeneration = 0;
            LastMessage = "";
            Changed?.Invoke();
        }

        [Serializable] sealed class RequestData { public int kind; public int uid; }
        [Serializable] sealed class ResultData { public bool ok; public string text; }

        [Serializable]
        sealed class ItemData
        {
            public int uid; public string id; public string name;
            public PropItem Read() => new(uid, id, name);
            public static ItemData From(PropItem i) => new() { uid = i.Uid, id = i.ItemId, name = i.Name };
        }

        [Serializable] sealed class PlayerData { public ulong client; public bool backpack; public ItemData[] held; }
        [Serializable] sealed class FloorData { public ItemData item; public float x, y, z; }

        [Serializable]
        sealed class StateData
        {
            public long revision;
            public PlayerData[] players;
            public FloorData[] floor;
            public ItemData[] shelf;

            public static StateData From(ItemCarryBook book)
            {
                var players = new List<PlayerData>();
                foreach (ulong client in book.Players)
                {
                    var held = book.Held(client);
                    var items = new ItemData[held.Count];
                    for (int i = 0; i < items.Length; i++) items[i] = ItemData.From(held[i]);
                    players.Add(new PlayerData { client = client, backpack = book.HasBackpack(client), held = items });
                }
                var floor = new FloorData[book.Floor.Count];
                for (int i = 0; i < floor.Length; i++)
                {
                    var f = book.Floor[i];
                    floor[i] = new FloorData { item = ItemData.From(f.Item), x = f.X, y = f.Y, z = f.Z };
                }
                var shelf = new ItemData[ItemCarryBook.ShelfSlots];
                for (int i = 0; i < shelf.Length; i++) shelf[i] = ItemData.From(book.Shelf[i]);
                return new StateData { revision = book.Revision, players = players.ToArray(), floor = floor, shelf = shelf };
            }
        }

        // ---- 화면 ----

        void OnGUI()
        {
            if (!showHud || !InWaitingRoom) return;
            ulong me = LocalClientId;
            var mine = Held(me);
            var lines = new List<string>
            {
                $"소지 {mine.Count}/{Slots(me)}{(HasBackpack(me) ? " (배낭)" : "")}  [F 길게] 내려놓기",
            };
            for (int i = 0; i < mine.Count; i++) lines.Add($"  {i + 1}. {mine[i].Name} #{mine[i].Uid}");
            if (NetworkPlayer.All.Count > 1)
                foreach (var player in NetworkPlayer.All)
                {
                    if (!player.IsSpawned || player.OwnerClientId == me) continue;
                    ulong id = player.OwnerClientId;
                    lines.Add($"{WaitingRoomShop.PlayerLabel(id)}: {Held(id).Count}/{Slots(id)}{(HasBackpack(id) ? " (배낭)" : "")}");
                }
            if (!string.IsNullOrEmpty(LastMessage)) lines.Add(LastMessage);

            float height = 25f * lines.Count + 12f;
            GUILayout.BeginArea(new Rect(10, Screen.height - height - 10, 360, height), GUI.skin.box);
            foreach (var line in lines) GUILayout.Label(line);
            GUILayout.EndArea();
        }
    }
}
