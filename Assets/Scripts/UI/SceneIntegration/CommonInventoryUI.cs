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
        public const int VisibleSlotCount = 2;
        readonly List<InventorySlotData> data = new(VisibleSlotCount);
        static readonly Key[] Keys = { Key.Digit1, Key.Digit2 };
        string signature;
        UnityEngine.UI.Image heldIndicator;
        readonly UnityEngine.UI.Image[] selectionBorders = new UnityEngine.UI.Image[VisibleSlotCount];
        void Awake() => InputGate.AddReleaseKeys(Keys);
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
            if (carry != null && !InputGate.IsBlocked && local != null && local.State == PlayerState.Normal && Keyboard.current != null)
                for (int i = 0; i < Keys.Length; i++) if (Keyboard.current[Keys[i]].wasPressedThisFrame) carry.RequestSelectSlot(i);
            ulong id = carry != null ? carry.LocalClientId : 0;
            var held = carry != null ? carry.Held(id) : null;
            int capacity = carry != null ? carry.Slots(id) : SurvivalRules.ItemSlots();
            var selected = carry != null ? carry.Selected(id) : default;
            var reader = local != null ? local.GetComponent<PlayerInputReader>() : FindFirstObjectByType<PlayerInputReader>();
            bool ownsUv = false;
            if (held != null) foreach (var item in held) if (item.ItemId == ItemUses.UvLamp) ownsUv = true;
            bool uvHeld = reader != null && reader.Current.UvLightHeld && !InputGate.IsBlocked && ownsUv;
            string next = $"{id}:{capacity}:{selected.Uid}:{uvHeld}";
            if (held != null) foreach (var item in held) next += $":{item.Uid}:{item.ItemId}:{item.Name}:{item.Uses}";
            if (signature != next)
            {
                signature = next; data.Clear();
                for (int i = 0; i < VisibleSlotCount; i++)
                {
                    bool occupied = held != null && i < held.Count;
                    data.Add(new InventorySlotData { IsEmpty = !occupied, ItemSprite = occupied ? Icon(held[i].ItemId) : null,
                        IsHeld = occupied && held[i].Uid == selected.Uid, KeyLabel = i < capacity ? (i + 1).ToString() : WaitingRoomText.Format("inventory.slot-locked", "{n} 잠금", ("n", i + 1)), QuantityLabel = !occupied ? "" : held[i].Uses == ItemUses.Unlimited ? "1" : held[i].Uses.ToString() }); // 남은 사용 횟수(item-durability.md)
                }
                slots.SetSlots(data);
                EnsureIndicators();
                for (int i = 0; i < VisibleSlotCount; i++)
                {
                    var view = slots.GetSlot(i);
                    var opacity = view.GetComponent<CanvasGroup>();
                    if (opacity == null) opacity = view.gameObject.AddComponent<CanvasGroup>();
                    opacity.alpha = i < capacity ? 1f : .35f;
                    if (selectionBorders[i] != null) selectionBorders[i].enabled = data[i].IsHeld;
                }
                selectedLabel.text = selected.Uid != 0 ? ItemCatalog.DisplayName(selected) : string.Empty;
                if (heldIndicator != null) heldIndicator.enabled = selected.Uid != 0 && CurtainCall.ItemArt.ItemVisualAssets.IsTool(selected.ItemId) && (!uvHeld || selected.ItemId == ItemUses.UvLamp);
            }
            if (heldIndicator != null && heldIndicator.enabled)
            {
                selectedLabel.ForceMeshUpdate();
                ((RectTransform)heldIndicator.transform).anchoredPosition = new Vector2(selectedLabel.preferredWidth * .5f + 14f, 0f);
            }
            slots.SetContext(true); slots.ShowInventory();
        }
        void EnsureIndicators()
        {
            var border = Resources.Load<Sprite>("UI/Inventory/inventory-selection");
            var hand = Resources.Load<Sprite>("UI/Inventory/inventory-held");
            for (int i = 0; i < VisibleSlotCount; i++)
            {
                var slot = slots.GetSlot(i);
                if (slot == null || selectionBorders[i] != null || border == null) continue;
                var go = new GameObject("SelectionBorder", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(slot.transform, false);
                var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-3, -3); rect.offsetMax = new Vector2(3, 3);
                var image = go.GetComponent<UnityEngine.UI.Image>(); image.sprite = border; image.raycastTarget = false;
                selectionBorders[i] = image;
            }
            if (heldIndicator == null && selectedLabel != null && hand != null)
            {
                var go = new GameObject("HeldIndicator", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(selectedLabel.transform, false);
                var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(22, 22);
                heldIndicator = go.GetComponent<UnityEngine.UI.Image>(); heldIndicator.sprite = hand; heldIndicator.raycastTarget = false;
            }
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
                if (entry.key == "delivery") fallback = entry.sprite;
            }
            return fallback;
        }
        void OnDisable() { if (group != null) group.alpha = 0; if (slots != null) slots.SetContext(false); }
    }
}
