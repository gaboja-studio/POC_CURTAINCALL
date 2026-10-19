using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CurtainCall.Tightrope.UI
{
    /// <summary>연습 HUD의 배치는 유지하면서 클릭은 Input System의 실제 UI Button으로 받는다.</summary>
    public sealed class TightropeActionButtons : IDisposable
    {
        readonly GameObject root;
        readonly Button[] buttons;
        readonly TMP_Text[] labels;
        readonly bool[] reserved;
        readonly TMP_FontAsset runtimeFont;

        public TightropeActionButtons(Transform parent, string name, params Action[] actions)
        {
            root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Practice UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(root.transform, false);
            }
            buttons = new Button[actions.Length]; labels = new TMP_Text[actions.Length]; reserved = new bool[actions.Length];
            // 새 버튼 글자 때문에 공용 Static 폰트 에셋을 수정하지 않는다.
            var source = Resources.Load<Font>("Fonts/Pretendard-Medium/Pretendard-Medium 1");
            runtimeFont = source != null ? TMP_FontAsset.CreateFontAsset(source) : null;
            if (runtimeFont != null) runtimeFont.hideFlags = HideFlags.DontSave;
            var font = runtimeFont != null ? runtimeFont : Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
            for (int i = 0; i < actions.Length; i++)
            {
                var child = new GameObject($"{name} action {i}", typeof(RectTransform), typeof(Image), typeof(Button));
                child.transform.SetParent(root.transform, false);
                var rect = (RectTransform)child.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                var graphic = child.GetComponent<Image>(); graphic.color = new Color(.18f, .23f, .31f, 1f);
                var button = child.GetComponent<Button>(); button.targetGraphic = graphic;
                var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
                var action = actions[i]; button.onClick.AddListener(() => action());
                var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(child.transform, false);
                var textRect = (RectTransform)textObject.transform;
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(3f, 1f); textRect.offsetMax = new Vector2(-3f, -1f);
                var text = textObject.GetComponent<TextMeshProUGUI>();
                text.font = font; text.fontSize = 14f; text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
                buttons[i] = button; labels[i] = text; child.SetActive(false);
            }
        }

        readonly struct Frame : IDisposable
        {
            readonly TightropeActionButtons owner;
            public Frame(TightropeActionButtons owner) => this.owner = owner;
            public void Dispose()
            {
                if (owner == null) return;
                for (int i = 0; i < owner.buttons.Length; i++)
                    if (!owner.reserved[i]) owner.buttons[i].gameObject.SetActive(false);
            }
        }

        public IDisposable Begin()
        {
            if (Event.current.type != EventType.Repaint) return new Frame(null);
            Array.Clear(reserved, 0, reserved.Length);
            return new Frame(this);
        }

        /// <summary>IMGUI에는 자리만 확보하고 같은 화면 위치에 실제 UI 클릭 영역을 둔다.</summary>
        public void Reserve(int slot, string label, Rect area, bool interactable = true)
        {
            var space = GUILayoutUtility.GetRect(new GUIContent(label), GUI.skin.button, GUILayout.Height(26f));
            if (Event.current.type != EventType.Repaint) return;
            reserved[slot] = true;
            var button = buttons[slot]; var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(area.x + space.x, -(area.y + space.y));
            rect.sizeDelta = space.size;
            labels[slot].text = label; button.interactable = interactable;
            button.gameObject.SetActive(true);
        }

        public void Dispose()
        {
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            if (runtimeFont != null)
            {
                foreach (var atlas in runtimeFont.atlasTextures) if (atlas != null) UnityEngine.Object.Destroy(atlas);
                UnityEngine.Object.Destroy(runtimeFont.material); UnityEngine.Object.Destroy(runtimeFont);
            }
        }
    }
}
