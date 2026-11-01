using System.Collections.Generic;
using CurtainCall.CommonIcons;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Survival;
using CurtainCall.UI.BackstageHudV1;
using CurtainCall.WaitingRoom;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CurtainCall.UI.SceneIntegration
{
    public sealed class CommonInventoryUI : MonoBehaviour
    {
        public InventorySlotsView slots;
        public CanvasGroup group;
        public TMP_Text selectedLabel;
        public CommonIconCatalog icons;
        [Header("손 상태 문구 (빈 칸이면 표시 안 함)")]
        [Tooltip("아무 칸도 고르지 않았을 때. {keys} = 칸 선택 키(보통 1–2, 탈출로 3–4)")]
        public string emptyHandText;
        [Tooltip("도구를 손에 들었을 때. {item} = 기물 이름")]
        public string holdingFormat;
        [Tooltip("장비 칸을 골랐을 때(장비는 손에 들지 않고 슬롯에서 자동 적용). {item} = 기물 이름")]
        public string equipmentSelectedFormat;
        [Tooltip("잠긴 칸의 번호 자리. {n} = 칸 번호")]
        public string lockedSlotFormat;
        [Tooltip("기물 그림이 없을 때 쓰는 아이콘 키(Icons 카탈로그)")]
        public string fallbackIconKey;
        [Header("손 아이콘 (도구를 손에 들었을 때 이름 옆)")]
        [Tooltip("이름 칸(SelectedProp) 아래에 배치한 손 아이콘. 그림·크기는 씬에서 고친다")]
        [SerializeField] UnityEngine.UI.Image heldIndicator;
        [Tooltip("이름 글자 끝과 손 아이콘 중심 사이 거리(px)")]
        [SerializeField] float heldIndicatorGap = 14f;
        public const int VisibleSlotCount = 2;
        readonly List<InventorySlotData> data = new(VisibleSlotCount);
        static readonly Key[] Keys = { Key.Digit1, Key.Digit2 };
        static readonly Key[] EscapeKeys = { Key.Digit3, Key.Digit4 };
        string signature;
        void Awake() { InputGate.AddReleaseKeys(Keys); InputGate.AddReleaseKeys(EscapeKeys); }
        void LateUpdate()
        {
            var scene = CommonMenuUI.CurrentScene;
            bool active = scene.IsValid() && (scene.name == "WaitingRoomBase" || scene.name == "Tightrope" || scene.name == "HumanCannon" || scene.name == "escape_route")
                && !SceneCurtainTransition.IsActive
                && !(PerformanceEndingPresentation.Current != null && PerformanceEndingPresentation.Current.ResultVisible);
            group.alpha = active ? 1 : 0;
            group.blocksRaycasts = group.interactable = false;
            if (!active) { slots.SetContext(false); signature = null; return; }
            var carry = PropCarry.Instance;
            var local = NetworkPlayer.Local;
            bool escapeKeys = CurtainCall.EscapeRoute.EscapePerspectiveToggle.ReservesNumberKeys;
            var selectionKeys = escapeKeys ? EscapeKeys : Keys;
            int firstKey = escapeKeys ? 3 : 1;
            if (carry != null && !InputGate.IsBlocked && local != null && local.State == PlayerState.Normal && Keyboard.current != null)
                for (int i = 0; i < selectionKeys.Length; i++) if (Keyboard.current[selectionKeys[i]].wasPressedThisFrame) carry.RequestSelectSlot(i);
            ulong id = carry != null ? carry.LocalClientId : 0;
            var held = carry != null ? carry.Held(id) : null;
            int capacity = carry != null ? carry.Slots(id) : SurvivalRules.ItemSlots();
            var selected = carry != null ? carry.Selected(id) : default;
            var reader = local != null ? local.GetComponent<PlayerInputReader>() : FindFirstObjectByType<PlayerInputReader>();
            bool ownsUv = false;
            if (held != null) foreach (var item in held) if (item.ItemId == ItemUses.UvLamp) ownsUv = true;
            bool uvHeld = reader != null && reader.Current.UvLightHeld && !InputGate.IsBlocked && ownsUv;
            string next = $"{id}:{capacity}:{selected.Uid}:{uvHeld}:{escapeKeys}";
            if (held != null) foreach (var item in held) next += $":{item.Uid}:{item.ItemId}:{item.Name}:{item.Uses}";
            if (signature != next)
            {
                signature = next; data.Clear();
                for (int i = 0; i < VisibleSlotCount; i++)
                {
                    bool occupied = held != null && i < held.Count;
                    data.Add(new InventorySlotData { IsEmpty = !occupied, ItemSprite = occupied ? Icon(held[i].ItemId) : null,
                        IsHeld = occupied && held[i].Uid == selected.Uid, KeyLabel = i < capacity ? (i + firstKey).ToString() : Fill(lockedSlotFormat, ("n", i + firstKey)), QuantityLabel = !occupied ? "" : held[i].Uses == ItemUses.Unlimited ? "1" : held[i].Uses.ToString() }); // 남은 사용 횟수(item-durability.md)
                }
                slots.SetSlots(data);
                for (int i = 0; i < VisibleSlotCount; i++)
                {
                    var view = slots.GetSlot(i);
                    var opacity = view.GetComponent<CanvasGroup>();
                    if (opacity == null) opacity = view.gameObject.AddComponent<CanvasGroup>();
                    opacity.alpha = i < capacity ? 1f : .35f;
                    // 칸 선택 테두리는 슬롯 템플릿(씬)에 배치했다. 슬롯은 템플릿의 복제본이라 같은 테두리를 가진다.
                    var border = view.GetComponentInChildren<InventorySlotSelectionBorder>(true);
                    if (border != null) border.Show(data[i].IsHeld);
                }
                // 도구는 손에 들고(손 아이콘 표시), 장비는 칸만 고른다(손에 들지 않고 슬롯에서 자동 적용). 2026-11-01 사용자.
                // 탈출로에서 1·2를 다른 조작이 쓰면 칸 선택 키가 3·4다({keys}).
                selectedLabel.text = selected.Uid == 0 ? Fill(emptyHandText, ("keys", $"{firstKey}–{firstKey + 1}"))
                    : Fill(PropCarry.IsHoldable(selected.ItemId) ? holdingFormat : equipmentSelectedFormat, ("item", ItemCatalog.DisplayName(selected)));
                if (heldIndicator != null) heldIndicator.enabled = selected.Uid != 0 && CurtainCall.ItemArt.ItemVisualAssets.IsTool(selected.ItemId) && (!uvHeld || selected.ItemId == ItemUses.UvLamp);
            }
            if (heldIndicator != null && heldIndicator.enabled)
            {
                selectedLabel.ForceMeshUpdate();
                ((RectTransform)heldIndicator.transform).anchoredPosition = new Vector2(selectedLabel.preferredWidth * .5f + heldIndicatorGap, 0f);
            }
            slots.SetContext(true); slots.ShowInventory();
        }
        Sprite Icon(string id)
        {
            var itemIcon = CurtainCall.ItemArt.ItemVisualAssets.Icon(id);
            if (itemIcon != null) return itemIcon;
            Sprite fallback = null;
            if (icons == null) return null;
            for (int i = 0; i < icons.Count; i++)
            {
                var entry = icons.GetEntry(i);
                if (entry.key == id) return entry.sprite;
                if (entry.key == fallbackIconKey) fallback = entry.sprite;
            }
            return fallback;
        }
        static string Fill(string template, params (string name, object value)[] values) =>
            string.IsNullOrEmpty(template) ? "" : WaitingRoomText.Fill(template, values);
        void OnDisable() { if (group != null) group.alpha = 0; if (slots != null) slots.SetContext(false); }
    }
}
