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
        void Awake() => InputGate.AddReleaseKeys(Keys);
        void LateUpdate()
        {
            var scene = CommonMenuUI.CurrentScene;
            bool active = scene.IsValid() && (scene.name == "WaitingRoomBase" || scene.name == "Tightrope" || scene.name == "HumanCannon")
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
            string next = $"{id}:{capacity}:{selected.Uid}";
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
                for (int i = 0; i < VisibleSlotCount; i++)
                {
                    var view = slots.GetSlot(i);
                    var opacity = view.GetComponent<CanvasGroup>();
                    if (opacity == null) opacity = view.gameObject.AddComponent<CanvasGroup>();
                    opacity.alpha = i < capacity ? 1f : .35f;
                }
                selectedLabel.text = selected.Uid != 0 ? WaitingRoomText.Format("inventory.holding", "{item} · 손에 듦", ("item", ItemCatalog.DisplayName(selected))) : WaitingRoomText.Get("inventory.empty-hand", "빈손 · 1–2 기물 선택");
            }
            slots.SetContext(true); slots.ShowInventory();
        }
        Sprite Icon(string id)
        {
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
