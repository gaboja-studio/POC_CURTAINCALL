using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>C14 원본을 바꾸지 않는 그래픽 선택·런 포기 확인용 보조 패널.</summary>
    public sealed class SettingsExtensionPanel
    {
        readonly GameObject root;
        readonly Transform panel;
        readonly TMP_FontAsset font;
        readonly TMP_Text title;
        readonly TMP_Text message;
        readonly UnityEngine.UI.Button accept;
        readonly UnityEngine.UI.Button cancel;
        readonly GameObject[] rows = new GameObject[3];
        readonly TMP_Text[] labels = new TMP_Text[3];
        readonly TMP_Text[] values = new TMP_Text[3];
        readonly UnityEngine.UI.Button[] previous = new UnityEngine.UI.Button[3];
        readonly UnityEngine.UI.Button[] next = new UnityEngine.UI.Button[3];
        Action accepted;
        Action cancelled;
        public bool IsOpen => root.activeSelf;
        public UnityEngine.UI.Button AcceptButton => accept;

        public SettingsExtensionPanel(Transform parent, TMP_FontAsset textFont)
        {
            font = textFont;
            root = new GameObject("SettingsExtensionCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 330;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var background = new GameObject("InputBlocker", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(root.transform, false);
            var rect = (RectTransform)background.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .55f);
            var surface = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            surface.transform.SetParent(root.transform, false); panel = surface.transform;
            rect = (RectTransform)panel;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(784, 552);
            surface.GetComponent<UnityEngine.UI.Image>().color = new Color32(26, 40, 36, 255);
            title = Text("Title", panel, 40, -28, 704, 60, 34);
            message = Text("Message", panel, 40, -94, 704, 88, 21);
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = new GameObject("Choice" + i, typeof(RectTransform));
                rows[i].transform.SetParent(panel, false);
                Place((RectTransform)rows[i].transform, 40, -190 - i * 70, 704, 60);
                labels[i] = Text("Label", rows[i].transform, 0, 0, 174, 56, 23);
                values[i] = Text("Value", rows[i].transform, 242, 0, 370, 56, 23);
                values[i].alignment = TextAlignmentOptions.Center;
                previous[i] = Button("Previous", rows[i].transform, "<", 180, 0, 56, 56, font);
                next[i] = Button("Next", rows[i].transform, ">", 642, 0, 56, 56, font);
            }
            cancel = Button("Cancel", panel, "취소", 40, -448, 270, 64, font);
            accept = Button("Accept", panel, "적용", 474, -448, 270, 64, font);
            cancel.onClick.AddListener(Cancel);
            accept.onClick.AddListener(() => accepted?.Invoke());
            root.SetActive(false);
        }

        public void Open(string heading, string description, string acceptLabel, Action onAccept, Action onCancel)
        {
            accepted = onAccept; cancelled = onCancel;
            title.text = heading; message.text = description;
            accept.GetComponentInChildren<TMP_Text>(true).text = acceptLabel;
            cancel.GetComponentInChildren<TMP_Text>(true).text = acceptLabel == "예" ? "아니요" : "취소";
            accept.interactable = true;
            foreach (var row in rows) row.SetActive(false);
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }

        public void Choice(int index, string label, string value, Action<int> change, bool enabled = true)
        {
            rows[index].SetActive(true); labels[index].text = label; values[index].text = value;
            previous[index].onClick.RemoveAllListeners(); next[index].onClick.RemoveAllListeners();
            previous[index].onClick.AddListener(() => change(-1)); next[index].onClick.AddListener(() => change(1));
            previous[index].interactable = next[index].interactable = enabled;
            values[index].color = enabled ? new Color32(240, 238, 220, 255) : new Color32(166, 174, 169, 255);
        }
        public void SetAcceptEnabled(bool value) => accept.interactable = value;
        public void SetMessage(string value) => message.text = value;
        public void Hide()
        {
            root.SetActive(false); accepted = cancelled = null;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(root.transform))
                EventSystem.current.SetSelectedGameObject(null);
        }
        public void Cancel() { var callback = cancelled; Hide(); callback?.Invoke(); }

        public static UnityEngine.UI.Button Button(string name, Transform parent, string label,
            float x, float y, float width, float height, TMP_FontAsset font)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/CommonButtons/Button_General");
            var go = UnityEngine.Object.Instantiate(prefab, parent, false); go.name = name;
            Place((RectTransform)go.transform, x, y, width, height);
            var text = go.GetComponentInChildren<TMP_Text>(true);
            text.text = label; text.font = font; text.fontSharedMaterial = font.material;
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.onClick.RemoveAllListeners();
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            return button;
        }
        TMP_Text Text(string name, Transform parent, float x, float y, float width, float height, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, width, height);
            var text = go.GetComponent<TMP_Text>();
            text.font = font; text.fontSharedMaterial = font.material; text.fontSize = size;
            text.color = new Color32(240, 238, 220, 255); text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }
        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
