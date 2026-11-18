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
        [Tooltip("아무 칸도 고르지 않았을 때")]
        public string emptyHandText;
        [Tooltip("도구를 손에 들었을 때. {item} = 기물 이름")]
        public string holdingFormat;
        [Tooltip("장비 칸을 골랐을 때. 공연 장비는 슬롯 사용키로 발동한다. {item} = 기물 이름")]
        public string equipmentSelectedFormat;
        [Tooltip("잠긴 칸의 번호 자리. {n} = 칸 번호")]
        public string lockedSlotFormat;
        [Tooltip("기물 그림이 없을 때 쓰는 아이콘 키(Icons 카탈로그)")]
        public string fallbackIconKey;
        [Header("손 아이콘 (도구를 손에 들었을 때 이름 옆)")]
        [Tooltip("이름 칸(SelectedProp) 아래에 배치한 손 아이콘. 그림·크기·위치는 씬에서 고친다(코드는 켜고 끄기만)")]
        [SerializeField] UnityEngine.UI.Image heldIndicator;
        public const int VisibleSlotCount = 2;
        readonly List<InventorySlotData> data = new(VisibleSlotCount);
        static readonly Key[] Keys = { Key.Digit1, Key.Digit2 };
        string signature;
        internal static CommonInventoryUI Current { get; private set; }
        // 칸 선택은 모든 공간에서 1·2(2026-11-02 사용자). 탈출로의 검토 비행·시점 전환은 3·4로 옮겼다.
        void Awake() => InputGate.AddReleaseKeys(Keys);
        void OnEnable() => Current = this;

        readonly InventorySlotTooltip slotTooltip = new();
        Canvas slotCanvas;
        Canvas SlotCanvas => slotCanvas != null ? slotCanvas : slotCanvas = slots != null ? slots.GetComponentInParent<Canvas>() : null;
        /// <summary>대기실 슬롯 설명이 켜져 있으면 끄고 true(ESC가 메뉴 대신 설명을 닫는다).</summary>
        internal bool CloseSlotTooltip()
        {
            if (!slotTooltip.IsOpen) return false;
            slotTooltip.Hide();
            return true;
        }
        void OnDestroy() => slotTooltip.Dispose();

        internal Vector2 PlaceStatusAboveSlots(RectTransform card, Vector2 position, float gap)
        {
            if (!group || group.alpha <= 0 || !slots || !slots.isActiveAndEnabled) return position;
            for (int i = 0; i < slots.SlotCount; i++)
            {
                var slot = slots.GetSlot(i);
                if (slot && slot.isActiveAndEnabled)
                    position = HudLayout.PlaceAbove(card, position, (RectTransform)slot.transform, gap);
            }
            if (selectedLabel && selectedLabel.isActiveAndEnabled && !string.IsNullOrWhiteSpace(selectedLabel.text))
                position = HudLayout.PlaceAbove(card, position, selectedLabel.rectTransform, gap);
            return position;
        }
        void LateUpdate()
        {
            var scene = CommonMenuUI.CurrentScene;
            var waiting = CommonUiSceneLoader.CurrentWaitingRoom;
            bool active = scene.IsValid() && ((waiting != null && waiting.gameObject.scene == scene)
                || CurtainCall.Settings.WaitingRoomScenes.IsWaitingRoom(scene) || scene.name == "Tightrope" || scene.name == "HumanCannon" || scene.name == "escape_route")
                && !SceneCurtainTransition.IsActive
                && !(PerformanceEndingPresentation.Current != null && PerformanceEndingPresentation.Current.ResultVisible);
            group.alpha = active ? 1 : 0;
            group.blocksRaycasts = group.interactable = false;
            var carry = PropCarry.Instance;
            if (!active) { slots.SetContext(false); signature = null; SkipUseMarks(); slotTooltip.Hide(); return; }
            var local = NetworkPlayer.Local;
            bool waitingRoom = (waiting != null && waiting.gameObject.scene == scene) || CurtainCall.Settings.WaitingRoomScenes.IsWaitingRoom(scene);
            ulong id = carry != null ? carry.LocalClientId : 0;
            var held = carry != null ? carry.Held(id) : null;
            Vector3? feet = local != null ? local.transform.position : null;
            if (carry != null && !InputGate.IsBlocked && local != null && local.State == PlayerState.Normal && Keyboard.current != null)
                for (int i = 0; i < Keys.Length; i++)
                {
                    if (!Keyboard.current[Keys[i]].wasPressedThisFrame) continue;
                    var item = held != null && i < held.Count ? held[i] : default;
                    // 대기실의 공연 기물은 손에 꺼내 들지 않는 대신 그 칸 위 설명을 켜고 끈다. 칸 선택은 그대로 해서
                    // 버리기·선반 보관이 된다(2026-11-18 사용자). 설명은 선택과 함께 켜지고, 선택이 풀리면 꺼진다.
                    bool willSelect = item.Uid != 0 && carry.Selected(id).Uid != item.Uid;
                    if (waitingRoom && item.Uid != 0 && ItemCatalog.Get(item.ItemId)?.use == ItemUse.Performance)
                    {
                        if (!willSelect) slotTooltip.Hide();
                        else if (slotTooltip.Slot != i)
                        {
                            var view = slots.GetSlot(i);
                            slotTooltip.Toggle(i, item, view != null ? (RectTransform)view.transform : null, SlotCanvas, transform, feet);
                        }
                    }
                    else slotTooltip.Hide();
                    carry.RequestSelectSlot(i);
                }
            var shownItem = slotTooltip.IsOpen && held != null && slotTooltip.Slot < held.Count ? held[slotTooltip.Slot] : default;
            bool tooltipAllowed = waitingRoom && !InputGate.IsBlocked && local != null && local.State == PlayerState.Normal
                && !(CommonMenuUI.Current != null && CommonMenuUI.Current.IsOpen);
            slotTooltip.Tick(slots, SlotCanvas, shownItem, feet, tooltipAllowed);
            int capacity = carry != null ? carry.Slots(id) : SurvivalRules.ItemSlots();
            var selected = carry != null ? carry.Selected(id) : default;
            var reader = local != null ? local.GetComponent<PlayerInputReader>() : CurtainCall.Player.OfflineLocalPlayer.Reader;
            bool ownsUv = false;
            if (held != null) foreach (var item in held) if (item.ItemId == ItemUses.UvLamp) ownsUv = true;
            bool uvHeld = reader != null && reader.Current.UvLightHeld && !InputGate.IsBlocked && ownsUv;
            var useMark = carry != null ? carry.LastUse(id) : default;
            string next = $"{id}:{capacity}:{selected.Uid}:{uvHeld}:{useMark.Serial}";
            string firstUse=reader ? reader.BindingLabel("UseSlotOne","Q") : "Q";
            string secondUse=reader ? reader.BindingLabel("UseSlotTwo","E") : "E";
            bool firstReady=local && local.CanUseEquipmentSlot(0), secondReady=local && local.CanUseEquipmentSlot(1);
            // 칸 키 Q·E 전환은 "지금 발동 가능"이 아니라 "공연 기물을 쓸 수 있는 공간"을 기준으로 유지한다(2026-11-18 사용자).
            // 연습실 밖 대기실처럼 쓸 수 없는 곳에서는 1·2로 돌아간다. 공연 기물이 아닌 칸은 늘 1·2.
            bool usableHere = feet.HasValue && PropCarry.PlaceAt(feet.Value) != PropCarry.EquipmentPlace.Blocked;
            int balloonSlot = local ? local.ActiveBalloonSlot : -1;
            bool firstUseKey = UseKeyShown(0, held, capacity, usableHere, balloonSlot), secondUseKey = UseKeyShown(1, held, capacity, usableHere, balloonSlot);
            next+=$":{firstUse}:{secondUse}:{firstReady}:{secondReady}:{firstUseKey}:{secondUseKey}";
            if (held != null) foreach (var item in held) next += $":{item.Uid}:{item.ItemId}:{item.Name}:{item.Uses}";
            if (signature != next)
            {
                signature = next; data.Clear();
                for (int i = 0; i < VisibleSlotCount; i++)
                {
                    bool occupied = held != null && i < held.Count;
                    // 칸 키는 기본 1·2. 공연 기물을 쓸 수 있는 공간이면 InventorySlotKeyLabel이 같은 자리를 Q·E(노랑)로 바꾼다(2026-11-18 사용자).
                    data.Add(new InventorySlotData { IsEmpty = !occupied, ItemSprite = occupied ? Icon(held[i].ItemId) : null,
                        IsHeld = occupied && held[i].Uid == selected.Uid, KeyLabel = i < capacity
                            ? (i+1).ToString()
                            : Fill(lockedSlotFormat, ("n", i + 1)),
                        QuantityLabel = "" }); // 남은 사용 횟수는 칸 아래 내구도 바로 보인다(2026-11-02 사용자)
                }
                string itemName = selected.Uid == 0 ? Fill(emptyHandText)
                    : Fill(PropCarry.IsHoldable(selected.ItemId) ? holdingFormat : equipmentSelectedFormat, ("item", ItemCatalog.DisplayName(selected)));
                HudUIManager.ShowInventory(itemName,slots,data,selectedLabel);
                for (int i = 0; i < VisibleSlotCount; i++)
                {
                    var view = slots.GetSlot(i);
                    var opacity = view.GetComponent<CanvasGroup>();
                    if (opacity == null) opacity = view.gameObject.AddComponent<CanvasGroup>();
                    bool equipment=held!=null && i<held.Count && ItemCatalog.Get(held[i].ItemId)?.kind==ItemKind.Equipment;
                    opacity.alpha = i>=capacity ? .35f : equipment && !(i==0 ? firstReady : secondReady) ? .65f : 1f;
                    // 칸 선택 테두리는 슬롯 템플릿(씬)에 배치했다. 슬롯은 템플릿의 복제본이라 같은 테두리를 가진다.
                    var border = view.GetComponentInChildren<InventorySlotSelectionBorder>(true);
                    if (border != null) border.Show(data[i].IsHeld);
                    // 칸 키 글자: 공연 기물을 쓸 수 있는 공간에서 그 칸이 공연 기물이면(벌룬 접기 포함) 1·2 → Q·E(노랑)로 부드럽게 바꾸고 유지한다.
                    // 쓸 수 없는 공간으로 가면 다시 1·2(흰색). 발동 가능 여부는 칸 불투명도로만 보인다.
                    InventorySlotKeyLabel.For(view)?.Show(data[i].KeyLabel, i==0 ? firstUse : secondUse, i==0 ? firstUseKey : secondUseKey);
                    var bar = view.GetComponentInChildren<InventorySlotDurabilityBar>(true);
                    if (bar != null)
                    {
                        if (held != null && i < held.Count && held[i].Uses != ItemUses.Unlimited)
                            bar.Show(held[i].Uses, Mathf.Max(held[i].Uses, ItemCatalog.NewUses(held[i].ItemId)), IsWorn(held[i]));
                        else bar.Hide();
                    }
                }
                PlayUseMark(id, useMark, held);
                // 도구는 손에 들고(손 아이콘 표시), 장비는 칸만 고른다(손에 들지 않고 슬롯에서 자동 적용). 2026-11-01 사용자.
                if (heldIndicator != null) heldIndicator.enabled = selected.Uid != 0 && CurtainCall.ItemArt.ItemVisualAssets.IsTool(selected.ItemId) && (!uvHeld || selected.ItemId == ItemUses.UvLamp);
            }
            // 손 아이콘 위치는 씬 레이아웃이 정한다(LabelArea 크기 맞춤 → 이름 칸 폭 = 글자 폭, 손 아이콘은 이름 칸 왼쪽에 고정). 2026-11-02 사용자.
            slots.SetContext(true); slots.ShowInventory();
        }
        // ---- 사용 연출(2026-11-02 사용자, Task-094) ----
        // 판정 화면이 내구도를 깎을 때마다 PropCarry.LastUse 번호가 오른다. 번호가 바뀌면 그 기물 칸에 연출을 띄운다.
        // 다 써서 사라졌으면 바로 앞 화면에서 그 기물이 있던 칸에 파괴 연출을 띄운다.
        ulong seenUseClient = ulong.MaxValue;
        int seenUseSerial;
        readonly List<int> lastUids = new(VisibleSlotCount);
        readonly List<Sprite> lastSprites = new(VisibleSlotCount);

        void SkipUseMarks()
        {
            seenUseClient = ulong.MaxValue;
            lastUids.Clear(); lastSprites.Clear();
        }

        void PlayUseMark(ulong id, PropCarry.UseMark mark, IReadOnlyList<PropItem> held)
        {
            bool fresh = seenUseClient == id && mark.Serial > seenUseSerial;
            seenUseClient = id;
            seenUseSerial = mark.Serial;
            if (fresh)
            {
                int index = -1;
                if (!mark.Destroyed && held != null)
                {
                    for (int i = 0; i < held.Count && i < VisibleSlotCount; i++) if (held[i].Uid == mark.Uid) index = i;
                    var view = index >= 0 ? slots.GetSlot(index) : null;
                    if (view != null)
                    {
                        view.GetComponentInChildren<InventorySlotUseEffect>(true)?.PlayUse();
                        view.GetComponentInChildren<InventorySlotDurabilityBar>(true)?.PlayConsume(held[index].Uses);
                    }
                }
                else
                {
                    index = lastUids.IndexOf(mark.Uid);
                    var view = index >= 0 ? slots.GetSlot(index) : null;
                    if (view != null) view.GetComponentInChildren<InventorySlotUseEffect>(true)?.PlayBreak(lastSprites[index]);
                }
                UseEffectPlayed?.Invoke(index, mark.Destroyed);
            }
            lastUids.Clear(); lastSprites.Clear();
            if (held != null)
                for (int i = 0; i < held.Count && i < VisibleSlotCount; i++) { lastUids.Add(held[i].Uid); lastSprites.Add(data[i].ItemSprite); }
        }

        /// <summary>소지 칸 사용 연출이 났다(칸 번호, 파괴 여부). 칸을 못 찾으면 -1. 테스트 확인용.</summary>
        public event System.Action<int, bool> UseEffectPlayed;

        /// <summary>그 칸 키를 Q·E(사용키)로 보일지: 쓸 수 있는 공간의 공연 기물 칸, 또는 펼친 벌룬을 접는 칸(마지막 내구도로 칸이 비어도).</summary>
        static bool UseKeyShown(int slot, IReadOnlyList<PropItem> held, int capacity, bool usableHere, int balloonSlot)
        {
            if (slot >= capacity) return false;
            if (slot == balloonSlot) return true;
            return usableHere && held != null && slot < held.Count && ItemCatalog.Get(held[slot].ItemId)?.use == ItemUse.Performance;
        }

        static bool IsWorn(PropItem item)
        {
            var e = ItemCatalog.Get(item.ItemId);
            return e != null && e.wornAtOrBelow >= 0 && item.Uses <= e.wornAtOrBelow;
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
        void OnDisable()
        {
            if (Current == this) Current = null;
            slotTooltip.Hide(true);
            if (group != null) group.alpha = 0;
            if (slots != null) slots.SetContext(false);
        }
    }
}
