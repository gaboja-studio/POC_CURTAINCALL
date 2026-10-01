using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.Player.EditorTools
{
    /// <summary>
    /// 균형 게이지 기본 프리팹(그레이박스)을 만든다. 메뉴: Tools/CurtainCall/Build Balance Gauge Prefab
    /// 한 번 만든 뒤에는 디자인을 프리팹에서 직접 바꾼다. 다시 실행하면 덮어쓰므로 디자인 작업 후에는 실행하지 않는다.
    /// </summary>
    static class BalanceGaugePrefabBuilder
    {
        const string Folder = "Assets/Resources/Prefabs/UIs/Player";
        const string PrefabPath = Folder + "/BalanceGauge.prefab";
        const string FontPath = "Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium SDF.asset"; // 임시 폰트

        static readonly Color Green = new Color(0.25f, 0.68f, 0.35f);
        static readonly Color Red = new Color(0.82f, 0.25f, 0.2f);

        [MenuItem("Tools/CurtainCall/Build Balance Gauge Prefab")]
        static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                !EditorUtility.DisplayDialog("균형 게이지 프리팹", $"{PrefabPath}가 이미 있습니다. 덮어쓸까요?\n(디자인을 바꿨다면 사라집니다)", "덮어쓰기", "취소"))
                return;

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs/UIs", "Player");

            var root = new GameObject("BalanceGauge", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 게이지 막대: 화면 하단 가운데
            var bar = Rect("Bar", root.transform);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(760f, 36f);
            bar.anchoredPosition = new Vector2(0f, 70f);
            AddImage(bar, Red);

            var green = Rect("GreenZone", bar);
            Stretch(green);
            AddImage(green, Green);

            var needle = Rect("Needle", bar);
            needle.anchorMin = new Vector2(0.5f, 0f);
            needle.anchorMax = new Vector2(0.5f, 1f);
            needle.sizeDelta = new Vector2(6f, 16f); // 막대보다 위아래로 8px씩 길게
            AddImage(needle, Color.white);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogWarning($"[BalanceGaugePrefabBuilder] 폰트를 찾지 못해 TMP 기본 폰트를 씁니다: {FontPath}");

            var label = AddText(font, "ValueLabel", root.transform, "균형 0  (Green)", TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(760f, 30f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 34f);

            var left = AddText(font, "KeyLeft", bar, "◀ A", TextAlignmentOptions.Right);
            left.rectTransform.anchorMin = new Vector2(0f, 0f);
            left.rectTransform.anchorMax = new Vector2(0f, 1f);
            left.rectTransform.pivot = new Vector2(1f, 0.5f);
            left.rectTransform.sizeDelta = new Vector2(80f, 0f);
            left.rectTransform.anchoredPosition = new Vector2(-12f, 0f);

            var right = AddText(font, "KeyRight", bar, "D ▶", TextAlignmentOptions.Left);
            right.rectTransform.anchorMin = new Vector2(1f, 0f);
            right.rectTransform.anchorMax = new Vector2(1f, 1f);
            right.rectTransform.pivot = new Vector2(0f, 0.5f);
            right.rectTransform.sizeDelta = new Vector2(80f, 0f);
            right.rectTransform.anchoredPosition = new Vector2(12f, 0f);

            var view = root.AddComponent<BalanceGaugeView>();
            var so = new SerializedObject(view);
            so.FindProperty("greenZone").objectReferenceValue = green;
            so.FindProperty("needle").objectReferenceValue = needle;
            so.FindProperty("valueLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log($"[BalanceGaugePrefabBuilder] 저장: {PrefabPath}");
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static TextMeshProUGUI AddText(TMP_FontAsset font, string name, Transform parent, string content, TextAlignmentOptions alignment)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = 22f;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }
    }
}
