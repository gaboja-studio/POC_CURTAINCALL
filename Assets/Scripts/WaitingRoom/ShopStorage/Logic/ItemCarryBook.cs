using System.Collections.Generic;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 기물 한 개. Uid는 런 동안 겹치지 않는 번호(상점 주문 번호, 탐색 발견품은 <see cref="ItemUses.FoundUidBase"/>부터).
    /// <see cref="Uses"/>는 남은 사용 횟수, <see cref="ItemUses.Unlimited"/>(-1)이면 횟수를 세지 않는 기물(임시 품목).
    /// </summary>
    public readonly struct PropItem
    {
        public int Uid { get; }
        public string ItemId { get; }
        public string Name { get; }
        public int Uses { get; }

        public PropItem(int uid, string itemId, string name, int uses = ItemUses.Unlimited)
        {
            Uid = uid;
            ItemId = itemId;
            Name = name;
            Uses = uses;
        }

        public PropItem WithUses(int uses) => new(Uid, ItemId, Name, uses);
    }

    /// <summary>
    /// 기물 사용 횟수 규칙(결정 item-durability.md, 2026-10-29 사용자 보충).
    /// 상호작용한 대상이 파괴·비활성화되는 연출이 나오거나 보상을 얻었을 때만 1 줄인다. 실패·연타 중간·무효 입력은 줄이지 않는다.
    /// 마지막 효과를 끝까지 준 뒤 0이 되면 손에서 사라진다. 0인 기물은 새 효과를 내지 못한다.
    /// </summary>
    public static class ItemUses
    {
        public const int Unlimited = -1;

        /// <summary>탐색 발견품 UID 시작값(상점 주문 번호와 겹치지 않게).</summary>
        public const int FoundUidBase = 1_000_000;

        // 기물 7종 ID(기물 시스템 초안). 이름·내구도·가격·효과 수치는 ItemCatalog 에셋이 정본이다.
        public const string RescueGloves = "rescue-gloves";
        public const string StickyGloves = "sticky-gloves";
        public const string BalloonPants = "balloon-pants";
        public const string SpringShoes = "spring-shoes";
        public const string MagicianHairpin = "magician-hairpin";
        /// <summary>천막 말뚝 뽑개. 판자·덮개·서랍 개방과 숨은 길 벽 파괴에 쓴다.</summary>
        public const string StakePuller = "stake-puller";
        public const string UvLamp = "uv-lamp";
    }

    /// <summary>바닥에 놓인 기물과 위치(m).</summary>
    public readonly struct FloorProp
    {
        public PropItem Item { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public FloorProp(PropItem item, float x, float y, float z)
        {
            Item = item;
            X = x;
            Y = y;
            Z = z;
        }
    }

    public enum CarryRefusal : byte
    {
        None,
        HandsFull,
        NotFound = 3, // 삭제된 배낭 거절 코드(2)는 재사용하지 않는다.
        NothingHeld,
        FloorFull,
        ShelfFull,
        ShelfEmpty,
        UsedUp,
    }

    /// <summary>
    /// 기물 소지·바닥 기물·물려줄 선반 장부(판정 화면 전용 순수 로직).
    /// 한도(<c>slots</c>)는 부르는 쪽이 공통 세팅에서 읽어 넘긴다(공용 SurvivalRules.ItemSlots).
    /// 물려줄 선반(소품실 4칸)은 세대 교체 뒤에도 남는다(게임 흐름 기획 5-5·6-2). 나머지는 세대 교체 때 비운다.
    /// </summary>
    public sealed class ItemCarryBook
    {
        /// <summary>물려줄 선반 칸 수(기획 고정 4칸).</summary>
        public const int ShelfSlots = 4;

        readonly Dictionary<ulong, List<PropItem>> held = new();
        readonly List<FloorProp> floor = new();
        readonly PropItem[] shelf = new PropItem[ShelfSlots];
        static readonly IReadOnlyList<PropItem> Empty = new List<PropItem>().AsReadOnly();

        public ItemCarryBook(int floorCapacity) => FloorCapacity = floorCapacity < 1 ? 1 : floorCapacity;

        /// <summary>선반 칸(0 = 맨 아래). 빈 칸은 Uid 0.</summary>
        public IReadOnlyList<PropItem> Shelf => shelf;

        public static bool IsEmpty(PropItem item) => item.Uid == 0;

        /// <summary>
        /// 선반 상호작용(F 한 번). 들고 있고 빈 칸이 있으면 마지막에 든 기물을 아래 칸부터 올린다.
        /// 아니면 손에 자리가 있을 때 위 칸부터 꺼낸다. 어느 칸이 바뀌었는지 <paramref name="slot"/>로 알린다.
        /// </summary>
        public CarryRefusal UseShelf(ulong player, int slots, out int slot, out bool placed, out PropItem item, int selectedUid = 0)
        {
            slot = -1;
            placed = false;
            item = default;
            int empty = System.Array.FindIndex(shelf, IsEmpty);
            if (selectedUid >= 0 && Count(player) > 0 && empty >= 0)
            {
                var list = held[player];
                int index = selectedUid == 0 ? list.Count - 1 : list.FindIndex(p => p.Uid == selectedUid);
                if (index < 0) return CarryRefusal.NothingHeld;
                item = list[index];
                list.RemoveAt(index);
                shelf[empty] = item;
                slot = empty;
                placed = true;
                Revision++;
                return CarryRefusal.None;
            }
            int top = System.Array.FindLastIndex(shelf, s => !IsEmpty(s));
            if (top < 0) return Count(player) > 0 ? CarryRefusal.ShelfFull : CarryRefusal.ShelfEmpty;
            if (Count(player) >= slots) return empty < 0 ? CarryRefusal.ShelfFull : CarryRefusal.HandsFull;
            item = shelf[top];
            shelf[top] = default;
            List(player).Add(item);
            slot = top;
            Revision++;
            return CarryRefusal.None;
        }

        /// <summary>
        /// 세대 교체: 물려줄 선반만 남기고 손·바닥(소품실 보관 포함)을 비운다.
        /// </summary>
        public void ResetGeneration()
        {
            if (held.Count == 0 && floor.Count == 0) return;
            held.Clear();
            floor.Clear();
            Revision++;
        }

        /// <summary>선반을 받은 값으로 채운다(참가자 화면·세션 복원용). 4칸이 아니면 무시한다.</summary>
        public void LoadShelf(IReadOnlyList<PropItem> items)
        {
            if (items == null || items.Count != ShelfSlots) return;
            for (int i = 0; i < ShelfSlots; i++) shelf[i] = items[i];
            Revision++;
        }

        /// <summary>바닥에 동시에 둘 수 있는 최대 개수.</summary>
        public int FloorCapacity { get; }

        /// <summary>바뀔 때마다 오른다.</summary>
        public long Revision { get; private set; }

        public IReadOnlyList<FloorProp> Floor => floor.AsReadOnly();

        /// <summary>기물을 들고 있는 사람.</summary>
        public IEnumerable<ulong> Players
        {
            get
            {
                var seen = new HashSet<ulong>();
                foreach (var pair in held) if (pair.Value.Count > 0) seen.Add(pair.Key);
                return seen;
            }
        }

        public IReadOnlyList<PropItem> Held(ulong player) =>
            held.TryGetValue(player, out var list) ? list.AsReadOnly() : Empty;

        public int Count(ulong player) => held.TryGetValue(player, out var list) ? list.Count : 0;

        /// <summary>새로 받을 수 있는지(돈·배송구를 건드리기 전 확인용).</summary>
        public CarryRefusal CanTake(ulong player, int slots)
        {
            return Count(player) < slots ? CarryRefusal.None : CarryRefusal.HandsFull;
        }

        /// <summary>받은 기물을 손에 든다.</summary>
        public CarryRefusal Take(ulong player, int slots, PropItem item)
        {
            var refusal = CanTake(player, slots);
            if (refusal != CarryRefusal.None) return refusal;
            List(player).Add(item);
            Revision++;
            return CarryRefusal.None;
        }

        /// <summary>바닥 기물을 줍는다.</summary>
        public CarryRefusal PickUp(int uid, ulong player, int slots, out PropItem item)
        {
            item = default;
            int index = floor.FindIndex(f => f.Item.Uid == uid);
            if (index < 0) return CarryRefusal.NotFound;
            if (Count(player) >= slots) return CarryRefusal.HandsFull;
            item = floor[index].Item;
            floor.RemoveAt(index);
            List(player).Add(item);
            Revision++;
            return CarryRefusal.None;
        }

        /// <summary>
        /// 들고 있는 기물을 한 번 쓴다(효과가 실제로 났을 때만 부른다). 횟수를 세는 기물은 1 줄이고, 0이 되면 손에서 없앤다.
        /// <paramref name="after"/>는 사용 뒤 상태(없어졌으면 Uses 0), <paramref name="destroyed"/>는 마지막 사용이었는지.
        /// </summary>
        public CarryRefusal Use(ulong player, int uid, out PropItem after, out bool destroyed)
        {
            after = default;
            destroyed = false;
            if (!held.TryGetValue(player, out var list)) return CarryRefusal.NothingHeld;
            int index = list.FindIndex(p => p.Uid == uid);
            if (index < 0) return CarryRefusal.NothingHeld;
            var item = list[index];
            if (item.Uses == ItemUses.Unlimited) { after = item; return CarryRefusal.None; }
            if (item.Uses <= 0) return CarryRefusal.UsedUp;
            after = item.WithUses(item.Uses - 1);
            if (after.Uses == 0) { list.RemoveAt(index); destroyed = true; }
            else list[index] = after;
            Revision++;
            return CarryRefusal.None;
        }

        /// <summary>판정 화면이 바닥에 기물을 놓는다(탐색 발견품 배치). 같은 UID가 이미 있으면 무시한다.</summary>
        public CarryRefusal Place(PropItem item, float x, float y, float z)
        {
            if (IsEmpty(item) || Exists(item.Uid)) return CarryRefusal.NotFound;
            if (floor.Count >= FloorCapacity) return CarryRefusal.FloorFull;
            floor.Add(new FloorProp(item, x, y, z));
            Revision++;
            return CarryRefusal.None;
        }

        /// <summary>손·바닥·선반 어디든 이 UID가 남아 있는가.</summary>
        public bool Exists(int uid)
        {
            if (uid == 0) return false;
            foreach (var list in held.Values) if (list.Exists(p => p.Uid == uid)) return true;
            return floor.Exists(f => f.Item.Uid == uid) || System.Array.Exists(shelf, s => s.Uid == uid);
        }

        /// <summary>마지막에 든 기물을 그 자리 바닥에 내려놓는다.</summary>
        public CarryRefusal Drop(ulong player, float x, float y, float z, out FloorProp dropped, int selectedUid = 0)
        {
            dropped = default;
            if (Count(player) == 0) return CarryRefusal.NothingHeld;
            if (floor.Count >= FloorCapacity) return CarryRefusal.FloorFull;
            var list = held[player];
            int index = selectedUid == 0 ? list.Count - 1 : list.FindIndex(p => p.Uid == selectedUid);
            if (index < 0) return CarryRefusal.NothingHeld;
            var item = list[index];
            list.RemoveAt(index);
            dropped = new FloorProp(item, x, y, z);
            floor.Add(dropped);
            Revision++;
            return CarryRefusal.None;
        }

        /// <summary>
        /// 한도가 줄었을 때 넘친 만큼(나중에 든 것부터) 그 자리에 떨어뜨린다. 바닥이 가득 차도 떨어뜨린다(손에서 사라지지 않게).
        /// 떨어진 개수를 돌려준다.
        /// </summary>
        public int Trim(ulong player, int slots, float x, float y, float z, List<FloorProp> dropped = null)
        {
            if (slots < 0) slots = 0;
            if (!held.TryGetValue(player, out var list) || list.Count <= slots) return 0;
            int count = 0;
            while (list.Count > slots)
            {
                var item = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                var prop = new FloorProp(item, x, y, z);
                floor.Add(prop);
                dropped?.Add(prop);
                count++;
            }
            Revision++;
            return count;
        }

        /// <summary>방이 바뀔 때: 선반까지 모두 비운다(물려주기는 같은 게임 세션 안에서만, 게임 흐름 6-2).</summary>
        public void Clear()
        {
            System.Array.Clear(shelf, 0, ShelfSlots);
            held.Clear();
            floor.Clear();
            Revision++;
        }

        List<PropItem> List(ulong player)
        {
            if (!held.TryGetValue(player, out var list)) held[player] = list = new List<PropItem>();
            return list;
        }
    }
}
