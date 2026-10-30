using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 탐색 진행(Task-079 3단계). 런 동안 남는다(DontDestroyOnLoad, 씬 사본은 버림).
    /// - 판정은 공동 자금과 같은 판정 화면(<see cref="PerformanceRewards.HasAuthority"/>)이 한다. 참가자는 요청만 보내고 받은 상태를 그린다.
    /// - 부술 수 있는 벽(<see cref="BreakableWall"/>): F 한 번이 한 번 내려치기. 맞는 도구를 손에 들어야 하고, 타격은 팀 모두에게 같게 쌓이며
    ///   손을 떼도 남는다. 필요한 수에 닿으면 열리고, 그때 연 사람의 도구 사용 횟수를 1 줄인다(item-durability.md).
    /// - 탐색 발견품(<see cref="FoundItemSpot"/>): 세대마다 한 번 바닥 기물로 놓는다(공통 기물 소지 <see cref="PropCarry"/>).
    /// - 세대 교체 때 비운다.
    /// </summary>
    public sealed class ExplorationState : MonoBehaviour
    {
        const int SyncRequest = 0, HitRequest = 1;
        const float MinHitGap = .08f;

        public static ExplorationState Instance { get; private set; }

        /// <summary>진행이 바뀌었다(받은 상태 포함).</summary>
        public event Action Changed;

        /// <summary>내 요청의 결과 문구.</summary>
        public event Action<string> RequestAnswered;

        public string LastMessage { get; private set; } = "";

        public bool HasAuthority
        {
            get
            {
                var rewards = PerformanceRewards.Instance;
                return rewards != null && rewards.HasAuthority;
            }
        }

        public bool CanReachLocal(IShopTarget target)
        { var body = FindBody(channel.LocalClientId); return body != null && target.Reach(body.position) >= 0f; }
        public Vector3? LocalFeet { get { var body = FindBody(channel.LocalClientId); return body != null ? body.position : null; } }

        public int Hits(string target) => book.Hits(target);
        public bool IsOpen(string target) => book.IsOpen(target);

        readonly ExplorationBook book = new();
        readonly ExplorationCooperation cooperation = new();
        readonly Dictionary<ulong, float> lastHit = new();
        readonly HashSet<string> seeded = new();
        readonly HashSet<string> warnedDrops = new();
        readonly System.Random rng = new();
        ShopChannel channel;
        int seenGeneration;
        float generationSince;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            else Debug.LogWarning("[Exploration] 탐색 진행은 씬 최상위 오브젝트에 둬야 장소 전환 뒤에도 남습니다.", this);
            channel = new ShopChannel("CurtainCall.WaitingRoom.Exploration", HandleRequest, HandleState, HandleResult, HandleReset,
                () => channel.Request(JsonUtility.ToJson(new RequestData { kind = SyncRequest })));
        }

        void OnDestroy()
        {
            channel?.Dispose();
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Update()
        {
            channel.Update();
            if (!HasAuthority) return;
            WatchGeneration();
            SeedFoundItems();
            DeliverRewards();
        }

        /// <summary>F: 이 탐색 대상에 손에 든 도구를 한 번 쓴다.</summary>
        public void RequestUse(string target) => RequestCode(target, null);

        public void RequestCode(string target, string password)
        {
            if (HasAuthority) Answer(channel.LocalClientId, Use(channel.LocalClientId, target, password));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = HitRequest, target = target, password = password })))
                Answer(channel.LocalClientId, (false, "접속 중이 아니라 내려칠 수 없습니다."));
        }

        // ---- 판정 ----

        // 판정 화면: 탐색 대상에 손에 든 도구를 한 번 쓴다(벽 내려치기·자물쇠 따기·판자 뜯기·UV 비추기 공통).
        (bool, string) Use(ulong player, string targetId, string password = null)
        {
            var target = ExplorationTargets.Find(targetId);
            if (target == null) return (false, "대상을 찾지 못했습니다.");
            if (book.IsOpen(targetId)) return (false, "이미 열렸습니다.");
            var body = FindBody(player);
            if (body == null || target.Reach(body.position) < 0f) return (false, "너무 멉니다.");
            var carry = PropCarry.Instance;
            var tool = carry != null ? carry.Selected(player) : default;
            bool needsTool = !string.IsNullOrEmpty(target.ToolId);
            if (needsTool && (tool.Uid == 0 || tool.ItemId != target.ToolId)) return (false, $"{target.ToolName}을(를) 손에 들어야 합니다.");
            if (needsTool && tool.Uses == 0) return (false, PropCarry.Explain(CarryRefusal.UsedUp));
            float now = Time.unscaledTime;
            if (lastHit.TryGetValue(player, out float last) && now - last < MinHitGap) return (false, "");
            lastHit[player] = now;
            if (target is ExplorationGate gate)
            {
                if (!gate.ConditionsMet) return (false, "선행 조건을 충족해야 합니다.");
                if (gate.NeedsPassword && !gate.MatchesPassword(password)) return (false, "비밀번호가 다릅니다.");
                // 열쇠 구멍이 있는 문은 두 사람이 서로 다른 구멍에서 눌러야 한다.
                int slot = gate.HasKeySlots ? gate.SlotAt(body.position) : -1;
                if (gate.NeedsPartner && !cooperation.Press(targetId, player, Time.unscaledTimeAsDouble, gate.CooperationWindow,
                    id => {
                        var other = FindBody(id);
                        if (other == null || gate.Reach(other.position) < 0f) return false;
                        if (gate.HasKeySlots && id != player && gate.SlotAt(other.position) == slot) return false;
                        var held = carry != null ? carry.Selected(id) : default;
                        return !needsTool || (held.Uid != 0 && held.ItemId == target.ToolId && held.Uses != 0);
                    }))
                    return (false, gate.HasKeySlots ? "다른 한 명이 반대쪽 열쇠 구멍에서 함께 F를 눌러야 합니다." : "다른 한 명도 가까이에서 F를 눌러야 합니다.");
            }


            int attempt = book.Hits(targetId) + 1;
            bool success = target.Resolve(attempt, rng);
            var refusal = book.Attempt(targetId, success, out int count, out bool openedNow);
            if (refusal != ExplorationRefusal.None) return (false, "더 쓸 수 없습니다.");
            string text = target.Describe(openedNow, count);
            if (openedNow)
            {
                // 대상이 열리거나 파괴되거나 새로 발견된 때에만 도구를 한 번 쓴다.
                if (needsTool)
                {
                    carry.UseItem(player, tool.Uid, out var after, out bool gone);
                    text += gone ? $" {ItemCatalog.DisplayName(after)}을(를) 다 썼습니다." : $" {ItemCatalog.DisplayName(after)} 남은 {after.Uses}회";
                }
                Debug.Log($"[Exploration] {targetId} 열림({count}번째) — {WaitingRoomShop.PlayerLabel(player)}");
            }
            DeliverRewards();
            Publish();
            return (true, text);
        }

        void WatchGeneration()
        {
            var run = CampaignRun.Instance;
            if (run == null) return;
            int generation = run.State.Generation;
            if (generation != seenGeneration)
            {
                if (seenGeneration > 0)
                {
                    book.Clear();
                    cooperation.Clear();
                    Debug.Log($"[Exploration] 세대 교체 {seenGeneration} → {generation}: 탐색 진행을 비웠습니다.");
                    Publish();
                }
                seeded.Clear();
                warnedDrops.Clear();
                seenGeneration = generation;
                generationSince = Time.unscaledTime;
            }
        }

        // 세대마다 한 번, 대기실이 열려 있을 때 발견품을 바닥에 놓는다. 기물 소지가 세대 교체를 먼저 정리하도록 잠깐 기다린다.
        void SeedFoundItems()
        {
            var carry = PropCarry.Instance;
            if (carry == null || seenGeneration <= 0 || !FloorPropsView.Active || Time.unscaledTime - generationSince < .5f) return;
            foreach (var spot in FoundItemSpot.All)
            {
                if (spot == null || !spot.CanSeed || seeded.Contains(spot.SpotId)) continue;
                var item = spot.MakeItem(seenGeneration);
                var refusal = carry.Exists(item.Uid) ? CarryRefusal.None : carry.PlaceFound(item, spot.FloorPoint);
                if (refusal == CarryRefusal.None) seeded.Add(spot.SpotId);
            }
        }

        // 열린 보상은 기물 시스템이 준비될 때까지 재시도. 지급 완료 표식도 같은 스냅샷에 저장한다.
        // 기물이 나중에 소모되어 Exists가 false가 되어도 재지급하지 않는다.
        void DeliverRewards()
        {
            var carry = PropCarry.Instance;
            if (!HasAuthority || carry == null || seenGeneration <= 0 || !FloorPropsView.Active
                || Time.unscaledTime - generationSince < .5f) return;
            bool changed = false;
            foreach (var target in ExplorationTargets.All)
            {
                if (!book.IsOpen(target.TargetId)) continue;
                var component = target as Component;
                var drops = component != null ? component.GetComponent<ExplorationDrops>() : null;
                if (drops != null)
                {
                    if (drops.Pending) continue;
                    if (!drops.TryValidate(out var error))
                    {
                        if (warnedDrops.Add(target.TargetId)) Debug.LogWarning("[Exploration] " + target.TargetId + ": " + error);
                        continue;
                    }
                    if (drops.entries != null) for (int i = 0; i < drops.entries.Length; i++)
                    {
                        var entry = drops.entries[i];
                        if (entry != null) Deliver(target, i, entry.itemId, entry.uidOffset,
                            entry.point != null ? entry.point.position : target.RewardPoint);
                    }
                }
                else Deliver(target, 0, target.RewardItemId, target.RewardUidOffset, target.RewardPoint);
            }
            if (changed) Publish();

            void Deliver(IExplorationTarget owner, int index, string itemId, int offset, Vector3 at)
            {
                string receipt = "reward:" + owner.TargetId + ":" + index;
                if (book.IsOpen(receipt) || string.IsNullOrEmpty(itemId) || offset < 1 || offset > 99) return;
                if (!UniqueOffset(owner, index, offset))
                {
                    if (warnedDrops.Add(owner.TargetId + ":uid:" + offset))
                        Debug.LogWarning("[Exploration] " + owner.TargetId + ": 다른 발견품/보상과 드랍 UID가 겹칩니다. 지급을 보류합니다.");
                    return;
                }
                var item = new PropItem(ItemUses.FoundUidBase + seenGeneration * 100 + offset, itemId,
                    ItemCatalog.Get(itemId)?.displayName ?? itemId, ItemCatalog.FoundUses(itemId));
                if (!carry.Exists(item.Uid) && carry.PlaceFound(item, at) != CarryRefusal.None) return;
                book.Attempt(receipt, true, out _, out _);
                changed = true;
            }
        }

        // 현재 등록된 대상 전체(아직 열리지 않은 대상 포함)의 UID를 예약한다.
        // 충돌을 이미 지급한 것으로 처리하지 않으므로 설정 수정 뒤 재시도할 수 있다.
        bool UniqueOffset(IExplorationTarget owner, int index, int offset)
        {
            int uid = ItemUses.FoundUidBase + seenGeneration * 100 + offset;
            foreach (var spot in FoundItemSpot.All) if (spot != null && spot.MakeItem(seenGeneration).Uid == uid) return false;
            foreach (var target in ExplorationTargets.All)
            {
                var component = target as Component;
                var drops = component != null ? component.GetComponent<ExplorationDrops>() : null;
                if (drops != null)
                {
                    if (drops.Pending || drops.entries == null) continue;
                    for (int i = 0; i < drops.entries.Length; i++)
                    {
                        var entry = drops.entries[i];
                        if (entry != null && !string.IsNullOrEmpty(entry.itemId) && entry.uidOffset == offset
                            && !(ReferenceEquals(target, owner) && i == index)) return false;
                    }
                }
                else if (!string.IsNullOrEmpty(target.RewardItemId) && target.RewardUidOffset == offset
                    && !(ReferenceEquals(target, owner) && index == 0)) return false;
            }
            return true;
        }

        static Transform FindBody(ulong clientId)
        {
            foreach (var player in NetworkPlayer.All)
                if (player.IsSpawned && player.OwnerClientId == clientId) return player.transform;
            if (NetworkPlayer.All.Count > 0) return null;
            var reader = FindFirstObjectByType<PlayerInputReader>(); // 접속하지 않은 씬 확인
            return reader != null ? reader.transform : null;
        }

        void Answer(ulong target, (bool ok, string text) result)
        {
            if (string.IsNullOrEmpty(result.text)) return;
            if (target == channel.LocalClientId || !channel.IsHost)
            {
                LastMessage = result.text;
                RequestAnswered?.Invoke(LastMessage);
                return;
            }
            channel.SendResult(target, JsonUtility.ToJson(new ResultData { ok = result.ok, text = result.text }));
        }

        void Publish()
        {
            channel.SendState(JsonUtility.ToJson(StateData.From(book)));
            Changed?.Invoke();
        }

        // ---- 메시지 ----

        void HandleRequest(ulong sender, string json)
        {
            if (!HasAuthority) return;
            var request = JsonUtility.FromJson<RequestData>(json);
            if (request == null) return;
            if (request.kind == SyncRequest) channel.SendState(JsonUtility.ToJson(StateData.From(book)), sender);
            else if (request.kind == HitRequest) Answer(sender, Use(sender, request.target, request.password));
        }

        void HandleState(string json)
        {
            if (HasAuthority) return;
            var state = JsonUtility.FromJson<StateData>(json);
            if (state == null) return;
            book.Load(state.targets, state.hits, state.opened);
            Changed?.Invoke();
        }

        void HandleResult(string json)
        {
            var result = JsonUtility.FromJson<ResultData>(json);
            if (result == null) return;
            LastMessage = result.text;
            RequestAnswered?.Invoke(LastMessage);
        }

        void HandleReset()
        {
            book.Clear();
            cooperation.Clear();
            seeded.Clear();
            warnedDrops.Clear();
            lastHit.Clear();
            seenGeneration = 0;
            LastMessage = "";
            Changed?.Invoke();
        }

        [Serializable] sealed class RequestData { public int kind; public string target; public string password; }
        [Serializable] sealed class ResultData { public bool ok; public string text; }

        [Serializable]
        sealed class StateData
        {
            public string[] targets;
            public int[] hits;
            public string[] opened;

            public static StateData From(ExplorationBook book)
            {
                var t = new List<string>(book.Targets);
                var h = new int[t.Count];
                for (int i = 0; i < t.Count; i++) h[i] = book.Hits(t[i]);
                return new StateData { targets = t.ToArray(), hits = h, opened = new List<string>(book.Opened).ToArray() };
            }
        }
    }
}
