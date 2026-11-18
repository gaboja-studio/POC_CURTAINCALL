using CurtainCall.UI.BackstageHudV1;
using CurtainCall.UI.TooltipsV1;
using CurtainCall.WaitingRoom;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 대기실에서 공연 기물 칸의 슬롯 키(1·2)를 누르면 그 칸 위에 기물 설명을 켜고 끈다(2026-11-18 사용자).
    /// 공연 기물은 대기실에서 꺼내고 넣을 수 없으므로, 슬롯 키를 설명 보기로 쓴다.
    /// - 다른 칸 키를 누르면 기존 설명을 끄고 그 칸 설명을 연다(한 번에 하나만).
    /// - ESC, 이동, 창·메뉴 열림, 칸의 기물이 바뀌거나 대기실을 벗어나면 끈다.
    /// 그림은 HUD 역할 공용 툴팁 프리팹(Retained/Tooltips, C15)을 쓰고, 문구는 상점 마우스 올림 설명과 같다(기물 목록 shopTooltip → shopDescription).
    /// </summary>
    sealed class InventorySlotTooltip
    {
        const string ResourcePath = "Prefabs/UIs/Roles/Retained/Tooltips/Tooltips";
        const float Width = 384f, Gap = 10f, Margin = 12f, MoveTolerance = .15f;

        GameObject instance;
        Canvas canvas;
        TooltipView view;
        int slot = -1, uid;
        Vector3 openedAt;
        bool hasFeet;

        public int Slot => slot;
        public bool IsOpen => slot >= 0;

        /// <summary>그 칸 설명을 켜고(다른 칸이 켜져 있으면 바꾸고), 같은 칸이면 끈다.</summary>
        public void Toggle(int index, PropItem item, RectTransform slotRect, Canvas slotCanvas, Transform owner, Vector3? feet)
        {
            if (slot == index && uid == item.Uid) { Hide(); return; }
            Hide(true); // 다른 칸 설명은 바로 끄고 연다(중복 표시 없음)
            if (slotRect == null || !Ensure(owner)) return;
            string description = ItemCatalog.Tooltip(item.ItemId);
            if (string.IsNullOrWhiteSpace(description)) description = ItemCatalog.Description(item.ItemId);
            if (slotCanvas != null) canvas.sortingOrder = slotCanvas.sortingOrder + 1;
            string title = ItemCatalog.DisplayName(item);
            // 툴팁 프리팹 폰트는 일부 글자만 구운 정적 폰트라 설명 대부분이 □로 깨진다(2026-11-18 사용자).
            // 런 동안 남는 HUD 관리자의 런타임 폰트 보충으로 원본 Pretendard에서 필요한 글자를 채운다(HudRoleCard와 같은 경로).
            if (HudUIManager.Current.TryGetComponent(out UiRuntimeFontFallback fonts)) fonts.WarmUpLocalText(instance.transform, title + description);
            if (!view.ShowNearby(title, description, Vector2.zero, Width)) return;
            PanelHoverTooltip.FitWholeDescription(view);
            slot = index; uid = item.Uid;
            hasFeet = feet.HasValue; openedAt = feet ?? default;
            Place(slotRect, slotCanvas);
        }

        /// <summary>매 프레임: 칸 위에 붙여 두고, 닫을 조건이면 끈다.</summary>
        public void Tick(InventorySlotsView slots, Canvas slotCanvas, PropItem current, Vector3? feet, bool allowed)
        {
            if (!IsOpen) return;
            bool moved = hasFeet && feet.HasValue && Vector3.ProjectOnPlane(feet.Value - openedAt, Vector3.up).sqrMagnitude > MoveTolerance * MoveTolerance;
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (!allowed || moved || escape || current.Uid != uid || view == null) { Hide(); return; }
            var slotView = slots != null && slot < slots.SlotCount ? slots.GetSlot(slot) : null;
            if (slotView == null || !slotView.isActiveAndEnabled) { Hide(); return; }
            if (slotCanvas != null) canvas.sortingOrder = slotCanvas.sortingOrder + 1;
            Place((RectTransform)slotView.transform, slotCanvas);
        }

        void Place(RectTransform target, Canvas slotCanvas)
        {
            var parent = (RectTransform)view.transform.parent;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners); // 1 = 왼쪽 위, 2 = 오른쪽 위
            var camera = slotCanvas != null && slotCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? slotCanvas.worldCamera : null;
            Vector2 topCenter = RectTransformUtility.WorldToScreenPoint(camera, (corners[1] + corners[2]) * .5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, topCenter, null, out var local);
            float anchorX = local.x - parent.rect.xMin, anchorY = parent.rect.yMax - local.y;
            var size = ((RectTransform)view.transform).rect.size;
            float height = size.y + 8.5f; // 꼬리 포함
            float x = Mathf.Clamp(anchorX - Width * .5f, Margin, Mathf.Max(Margin, parent.rect.width - Width - Margin));
            float y = Mathf.Max(Margin, anchorY - height - Gap);
            view.SetPosition(new Vector2(x, y));
            view.SetTailCenter(anchorX - x);
        }

        bool Ensure(Transform owner)
        {
            if (view != null) return true;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) { Debug.LogWarning("[Inventory] 툴팁 프리팹이 없습니다: " + ResourcePath); return false; }
            instance = Object.Instantiate(prefab);
            instance.name = "InventorySlotTooltip";
            // 인벤토리 UI와 같은 수명으로 둔다(장소 전환으로 활성 씬이 내려가도 남는다).
            var scene = owner != null ? owner.gameObject.scene : default;
            if (scene.IsValid() && scene.name == "DontDestroyOnLoad") Object.DontDestroyOnLoad(instance);
            else if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(instance, scene);
            canvas = instance.GetComponent<Canvas>();
            view = instance.GetComponentInChildren<TooltipView>(true);
            if (view != null) return true;
            Dispose();
            return false;
        }

        public void Hide(bool immediate = false)
        {
            if (view != null && slot >= 0) { if (immediate) view.HideImmediate(); else view.Hide(); }
            slot = -1; uid = 0; hasFeet = false;
        }

        public void Dispose()
        {
            Hide(true);
            if (instance != null) Object.Destroy(instance);
            instance = null; canvas = null; view = null;
        }
    }
}
