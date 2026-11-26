using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 기물 최초 획득 안내(2026-11-26 사용자, M3 피드백: 기물 사용법을 몰라 기물을 안 쓰게 됨).
    /// 내 소지 칸에 안내 대상 기물이 처음 들어오면 ① 가운데 시스템 배너로 한두 줄 요약을 띄우고,
    /// ② 배너가 사라진 뒤 왼쪽 가이드 패널에 기존 설명을 띄운다.
    /// - 탐색 기물 머리핀·말뚝 뽑개·UV 등: 왼쪽은 기존 대기실 튜토리얼(G06a·b·c). 튜토리얼은 <see cref="Busy"/> 동안 기다린다.
    /// - 공연 기물: 왼쪽은 기존 상점 설명.
    /// - 배터리·키 카드 복제본·비디오 테이프: 배너만(기존 왼쪽 설명 없음). 열쇠류는 제외(2026-11-26 사용자).
    /// 런(세대)마다 기물 종류별 1회. 들어올 때 이미 들고 있던 기물(씬 이동·재접속)은 새로 얻은 것으로 보지 않는다.
    /// 문구: 대기실 문구 파일 `guide.item.title`, `guide.item.&lt;기물 ID&gt;`.
    /// </summary>
    public sealed class ItemPickupGuide : MonoBehaviour
    {
        // 배너 문구 규칙(2026-11-26 사용자): 최대 4줄. 위 줄 = 기물 설명, 아래 줄 = 조작법.
        // 조작법은 살펴보기(오버뷰) 조작법처럼 항목을 나눠 회색(#B8B29C)으로 적는다. {key} = 그 칸의 공연 기물 사용 키.
        // 문구 파일에 guide.item.<ID>(설명), guide.item.<ID>.controls(조작법, 항목은 | 로 나눔)가 있으면 그쪽을 쓴다.
        public const string ControlsColor = "#B8B29C";
        public const int MaxLines = 4;
        static readonly Dictionary<string, (string description, string controls)> Summaries = new()
        {
            [ItemUses.MagicianHairpin] = ("잠긴 상자·캐비닛의 자물쇠를 땁니다.", "손에 들고 F: 자물쇠 따기"),
            [ItemUses.StakePuller] = ("나무 상자를 뜯어 열고, 약한 벽을 부숩니다.", "손에 들고 F: 뜯기·부수기"),
            [ItemUses.UvLamp] = ("비추면 숨겨진 낙서가 드러납니다.", "우클릭 누르는 동안: 비추기"),
            [ItemUses.PowerBattery] = ("동력 소켓에 꽂으면 전력이 연결되어 문이 열리고, 빼면 문이 닫힙니다.", "소켓 앞에서 F: 꽂기 | 다시 F: 빼기"),
            ["keycard-copy"] = ("관리실 문을 여는 카드입니다.", ""),
            ["video-tape"] = ("재생할 기기가 필요할 것 같다.", ""),
            ["video-tape-1"] = ("재생할 기기가 필요할 것 같다.", ""),
            ["video-tape-2"] = ("재생할 기기가 필요할 것 같다.", ""),
            ["rescue-gloves"] = ("안전한 바닥에서 앞쪽 가까운 동료를 내 곁으로 끌어옵니다.", "{key}: 사용 | 연습실·공연장에서 사용"),
            ["sticky-gloves"] = ("매달린 동안 쓰면 3초 동안 매달림 시간이 줄지 않습니다.", "{key}: 사용 | 연습실·공연장에서 사용"),
            ["balloon-pants"] = ("공중에서 펼치면 최대 3초 동안 천천히 내려옵니다.", "{key}: 사용 | 연습실·공연장에서 사용"),
            ["spring-shoes"] = ("공중에서 한 번 더 점프합니다.", "{key}: 사용 | 연습실·공연장에서 사용"),
        };        /// <summary>배너 뒤 왼쪽 패널은 기존 대기실 튜토리얼이 맡는 기물.</summary>
        static readonly HashSet<string> TutorialItems = new() { ItemUses.MagicianHairpin, ItemUses.StakePuller, ItemUses.UvLamp };

        static ItemPickupGuide instance;
        readonly HashSet<string> guided = new();
        readonly Queue<(string title, string body)> leftAfterBanner = new();
        int generation = -1;
        bool primed, bannerOut;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(ItemPickupGuide));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ItemPickupGuide>();
        }

        public static bool IsGuided(string itemId) => Summaries.ContainsKey(itemId);

        /// <summary>
        /// 기물 배너가 아직 나오지 않았거나 화면·줄에 남아 있다. 대기실 튜토리얼은 이 동안 왼쪽 안내를 미뤄 배너 → 왼쪽 순서를 지킨다.
        /// 같은 프레임에 튜토리얼이 먼저 돌아도 새 기물을 보고 미루도록, 소지 칸을 직접 본다.
        /// </summary>
        public static bool Busy => instance != null && (instance.bannerOut || instance.leftAfterBanner.Count > 0 || instance.HasUnannounced());

        /// <summary>배너 문구(제목, 요약).</summary>
        public static (string title, string body, string controls) Banner(string itemId, string displayName, string useKey)
        {
            string title = WaitingRoomText.Get("guide.item.title", "{item} 획득").Replace("{item}", displayName);
            Summaries.TryGetValue(itemId, out var fallback);
            string description = WaitingRoomText.Get("guide.item." + itemId, fallback.description ?? "").Replace("{key}", useKey ?? "").Trim();
            string controls = WaitingRoomText.Get("guide.item." + itemId + ".controls", fallback.controls ?? "").Replace("{key}", useKey ?? "");
            var parts = new List<string>();
            foreach (var part in controls.Split('|')) if (!string.IsNullOrWhiteSpace(part)) parts.Add(part.Trim());
            // 조작법은 오버뷰처럼 항목 사이를 띄워 한 줄로 나눠 적는다(이어진 문장으로 쓰지 않는다).
            return (title, description, string.Join("   ", parts));
        }        // 내가 띄운 배너 카드가 화면에 있거나 줄에서 차례를 기다리는 동안. 카드가 사라지면 null이 된다.
        CurtainCall.UI.SceneIntegration.HudRoleCard bannerCard;
        bool BannerShowing => bannerCard && !bannerCard.Closing;

        bool HasUnannounced()
        {
            if (!primed) return false;
            var local = NetworkPlayer.Local;
            var items = local != null && PropCarry.Instance != null ? PropCarry.Instance.Held(local.OwnerClientId) : null;
            if (items == null) return false;
            foreach (var item in items) if (IsGuided(item.ItemId) && !guided.Contains(item.ItemId)) return true;
            return false;
        }

        void Update()
        {
            // 배너가 사라진 뒤에 왼쪽 설명을 차례로 띄운다.
            if (bannerOut && !BannerShowing) bannerOut = false;
            if (!bannerOut && leftAfterBanner.Count > 0 && !WaitingRoomTutorialPanel.IsVisible)
            {
                var next = leftAfterBanner.Peek();
                if (WaitingRoomTutorialPanel.ShowAs(RoomNotice.Guide, next.title, next.body, 0f)) leftAfterBanner.Dequeue();
            }

            var local = NetworkPlayer.Local;
            var carry = PropCarry.Instance;
            if (local == null || !local.IsSpawned || carry == null) { primed = false; return; }
            int current = CurtainCall.Campaign.CampaignRun.Instance != null ? CurtainCall.Campaign.CampaignRun.Instance.State.Generation : 0;
            if (current != generation) { generation = current; guided.Clear(); primed = false; }

            var items = carry.Held(local.OwnerClientId);
            var reader = local.GetComponent<PlayerInputReader>();
            for (int slot = 0; items != null && slot < items.Count; slot++)
            {
                var item = items[slot];
                if (!IsGuided(item.ItemId)) continue;
                if (!primed) { guided.Add(item.ItemId); continue; }
                if (!guided.Add(item.ItemId)) continue;
                var entry = ItemCatalog.Get(item.ItemId);
                bool performance = entry != null && entry.use == ItemUse.Performance;
                string key = !performance ? "" : slot == 1
                    ? (reader ? reader.BindingLabel("UseSlotTwo", "E") : "E")
                    : (reader ? reader.BindingLabel("UseSlotOne", "Q") : "Q");
                string name = ItemCatalog.DisplayName(item);
                var (title, body, controls) = Banner(item.ItemId, name, key);
                ColorUtility.TryParseHtmlString(ControlsColor, out var gray);
                bannerCard = CurtainCall.UI.SceneIntegration.HudUIManager.Current.ShowSystemEventDetail(body, transform, title, controls, 6f, gray,
                    CurtainCall.UI.SceneIntegration.HudEventKind.Reward); // 인간대포 획득과 같은 초록 선물 마크(2026-11-26 사용자)
                bannerOut = true;
                // 공연 기물은 배너 뒤 왼쪽에 기존 상점 설명을 띄운다. 탐색 도구 3종은 기존 튜토리얼이 이어 받는다.
                if (performance && !TutorialItems.Contains(item.ItemId) && !string.IsNullOrWhiteSpace(entry.shopDescription))
                    leftAfterBanner.Enqueue((name, entry.shopDescription.Trim()));
            }
            primed = true;
        }
    }
}
