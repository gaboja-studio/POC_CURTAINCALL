using System;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public enum ItemUse { Performance, Exploration }
    public enum ItemKind { Equipment, Tool, Object }

    /// <summary>
    /// 기물 목록(기물 시스템 초안 27525121, Task-079). 이름·분류·내구도·가격·쿨타임·낡은 접두사·효과 수치를 한 에셋에 둔다.
    /// 확정: 새 제품 내구도·가격·차감·소진 처리. 효과 수치·쿨타임·접두사·발견품 내구도는 초안값이며 기획서가 오면 이 에셋만 고친다.
    /// 에셋: Resources/Settings/GameSettings/Items/ItemCatalog.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        public const string ResourcePath = "Settings/GameSettings/Items/ItemCatalog";

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string displayName;
            [Tooltip("남은 내구도가 wornAtOrBelow 이하일 때 쓰는 이름(낡은 …). 비우면 접두사 없음.")]
            public string wornName;
            [Tooltip("-1이면 접두사 기준 없음(미정).")]
            public int wornAtOrBelow = -1;
            public ItemUse use;
            public ItemKind kind;
            [Tooltip("새 제품 내구도(확정).")]
            public int newUses;
            [Tooltip("탐색 발견품 내구도(초안). 0 이하면 새 제품 값을 쓴다.")]
            public int foundUses;
            [Tooltip("새 제품 구매 가격(토큰, 확정).")]
            public int price;
            [Tooltip("쿨타임(초, 초안). 0이면 없음.")]
            public float cooldown;
            [Tooltip("핵심 효과 수치(초안): 구조 장갑 균형 손실 감소율, 끈끈이 장갑 버티기 배율, 벌룬 바지 하강 감속률, 머리핀 개방 확률 등.")]
            public float effect;
            [Tooltip("보조 수치(초안): 구조 장갑 적용 거리(m), 머리핀 보장 시도 번째 등. 0이면 미정.")]
            public float effect2;
            [TextArea] public string summary;
        }

        [SerializeField] List<Entry> items = new();

        public IReadOnlyList<Entry> Items => items;

        static ItemCatalog current;

        /// <summary>현재 기물 목록(Resources). 없으면 null.</summary>
        public static ItemCatalog Current => current != null ? current : current = Resources.Load<ItemCatalog>(ResourcePath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public static Entry Get(string id)
        {
            var catalog = Current;
            if (catalog == null || string.IsNullOrEmpty(id)) return null;
            foreach (var e in catalog.items) if (e != null && e.id == id) return e;
            return null;
        }

        /// <summary>새 제품 사용 횟수. 목록에 없으면 횟수를 세지 않는 기물.</summary>
        public static int NewUses(string id) => Get(id) is { } e && e.newUses > 0 ? e.newUses : ItemUses.Unlimited;

        /// <summary>탐색 발견품 사용 횟수(초안). 없으면 새 제품 값.</summary>
        public static int FoundUses(string id) => Get(id) is { } e ? (e.foundUses > 0 ? e.foundUses : NewUses(id)) : ItemUses.Unlimited;

        /// <summary>남은 내구도에 따른 표시 이름(낡은 접두사).</summary>
        public static string DisplayName(PropItem item)
        {
            var e = Get(item.ItemId);
            if (e == null) return item.Name;
            bool worn = e.wornAtOrBelow >= 0 && item.Uses != ItemUses.Unlimited && item.Uses <= e.wornAtOrBelow && !string.IsNullOrEmpty(e.wornName);
            return worn ? e.wornName : string.IsNullOrEmpty(e.displayName) ? item.Name : e.displayName;
        }

        /// <summary>목록의 새 제품 한 개.</summary>
        public static PropItem NewProduct(int uid, string id, string fallbackName) =>
            new(uid, id, Get(id)?.displayName ?? fallbackName, NewUses(id));

#if UNITY_EDITOR
        /// <summary>기물 시스템 초안 값으로 채운다(에셋 생성용). 확정: 내구도·가격. 나머지는 초안.</summary>
        public void FillFromDraft()
        {
            items = new List<Entry>
            {
                new() { id = ItemUses.RescueGloves, displayName = "구조 장갑", wornName = "낡은 구조 장갑", wornAtOrBelow = 2, use = ItemUse.Performance, kind = ItemKind.Equipment,
                        newUses = 5, foundUses = 2, price = 100, cooldown = 1f, effect = .3f, effect2 = 0f,
                        summary = "외줄: 가까운 팀원이 내 줄로 이동해 착지하면 착지 균형 손실 30% 감소. 실제 완화 시 1 소모. 적용 거리 미정." },
                new() { id = ItemUses.StickyGloves, displayName = "끈끈이 장갑", wornName = "낡은 끈끈이 장갑", wornAtOrBelow = 1, use = ItemUse.Performance, kind = ItemKind.Equipment,
                        newUses = 3, foundUses = 1, price = 100, cooldown = 3f, effect = 1.5f,
                        summary = "외줄 매달리기·리프트 버티기 시간 1.5배. 기본 시간을 넘겨 매달린 때 매달리기당 1 소모. 마지막 추가 시간은 유지 후 파괴." },
                new() { id = ItemUses.BalloonPants, displayName = "광대 벌룬 바지", wornName = "낡은 광대 벌룬 바지", wornAtOrBelow = 2, use = ItemUse.Performance, kind = ItemKind.Equipment,
                        newUses = 4, foundUses = 2, price = 125, cooldown = 3f, effect = .3f,
                        summary = "인간 대포: 하강할 때만 수직 하강 속도 30% 감소, 상승·수평 유지. 대포·트램펄린 비행당 최대 1 소모." },
                new() { id = ItemUses.SpringShoes, displayName = "스프링 신발", wornName = "낡은 스프링 신발", wornAtOrBelow = 1, use = ItemUse.Performance, kind = ItemKind.Equipment,
                        newUses = 3, foundUses = 1, price = 175, cooldown = 3f, effect = 1f,
                        summary = "인간 대포: 트램펄린 없는 바닥에 낙하하면 기본 트램펄린과 같은 반동으로 다시 도약. 실제 반동 시 1 소모." },
                new() { id = ItemUses.MagicianHairpin, displayName = "마술사의 머리핀", wornName = "", wornAtOrBelow = -1, use = ItemUse.Exploration, kind = ItemKind.Tool,
                        newUses = 5, foundUses = 0, price = 50, cooldown = 0f, effect = .5f, effect2 = 3f,
                        summary = "좌물쇠: 시도마다 50% 개방, 같은 좌물쇠 세 번째 시도는 반드시 개방(시도 수 팀 공유·런 유지). 개방 성공 시 1 소모." },
                new() { id = ItemUses.StakePuller, displayName = "천막 말뚝 뽑개", wornName = "낡은 천막 말뚝 뽑개", wornAtOrBelow = 1, use = ItemUse.Exploration, kind = ItemKind.Tool,
                        newUses = 4, foundUses = 1, price = 75, cooldown = 0f,
                        summary = "판자·덮개·걸린 서랍 개방, 숨은 길 벽 파괴. 개방·파괴 성공 시 1 소모." },
                new() { id = ItemUses.UvLamp, displayName = "서커스 UV 등", wornName = "낡은 서커스 UV 등", wornAtOrBelow = 1, use = ItemUse.Exploration, kind = ItemKind.Tool,
                        newUses = 3, foundUses = 1, price = 75, cooldown = 0f,
                        summary = "의심스러운 표면을 비추면 숨은 표식·지워진 글씨가 드러남(런 동안 유지). 새 단서 발견 시 1 소모." },
            };
        }
#endif
    }
}
