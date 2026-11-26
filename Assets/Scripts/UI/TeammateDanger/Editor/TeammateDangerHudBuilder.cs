using System.IO;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.TeammateDanger.EditorTools
{
    /// <summary>
    /// 위험한 동료 표시 화면 프리팹을 만들고 GameSettings › 월드 UI에 연결한다(2026-11-26 M3).
    /// 이미 프리팹이 있으면 덮어쓰지 않는다(배치·색은 프리팹에서 직접 고친다). 스프라이트 가져오기 설정만 맞춘다.
    /// </summary>
    public static class TeammateDangerHudBuilder
    {
        public const string Folder = "Assets/Resources/Prefabs/UIs/Roles/Retained/TeammateDanger";
        public const string PrefabPath = Folder + "/TeammateDangerHud.prefab";
        const string Sprites = Folder + "/Sprites";

        [MenuItem("Tools/Curtain Call/위험한 동료 표시/프리팹 만들기·설정 연결")]
        public static void Build()
        {
            foreach (var name in new[] { "danger-disc", "danger-ring", "danger-pointer", "danger-hang", "danger-cut", "danger-fall" })
                PrepareSprite($"{Sprites}/{name}.png");
            if (!File.Exists(PrefabPath)) CreatePrefab();
            Link();
            AssetDatabase.SaveAssets();
        }

        static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}/{name}.png");

        static void PrepareSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled && importer.alphaIsTransparency) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static RectTransform Child(Transform parent, string name, bool stretch = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            if (stretch) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            return rect;
        }

        static Image Picture(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false; image.preserveAspect = true;
            return image;
        }

        static void CreatePrefab()
        {
            var root = new GameObject("TeammateDangerHud", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 45;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                var area = Child(root.transform, "Area");

                var badgeRect = Child(area, "Badge", false);
                badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(.5f, .5f);
                badgeRect.sizeDelta = new Vector2(40, 40);
                var group = badgeRect.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false; group.blocksRaycasts = false;
                Picture(Child(badgeRect, "Disc"), Load("danger-disc"), new Color(.05f, .06f, .07f, .62f));
                var ring = Picture(Child(badgeRect, "Ring"), Load("danger-ring"), new Color(.93f, .27f, .22f, 1f));
                ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = true; ring.fillAmount = .7f;
                var iconRect = Child(badgeRect, "Icon");
                iconRect.anchorMin = new Vector2(.2f, .2f); iconRect.anchorMax = new Vector2(.8f, .8f);
                Picture(iconRect, Load("danger-hang"), new Color(.93f, .27f, .22f, 1f));
                var pivot = Child(badgeRect, "PointerPivot");
                var pointer = Child(pivot, "Pointer", false);
                pointer.anchorMin = pointer.anchorMax = new Vector2(.5f, 1f);
                pointer.pivot = new Vector2(.5f, 0f);
                pointer.sizeDelta = new Vector2(16, 16);
                pointer.anchoredPosition = new Vector2(0, 1);
                Picture(pointer, Load("danger-pointer"), new Color(.93f, .27f, .22f, 1f));

                var badge = badgeRect.gameObject.AddComponent<TeammateDangerBadge>();
                var data = new SerializedObject(badge);
                data.FindProperty("root").objectReferenceValue = badgeRect;
                data.FindProperty("disc").objectReferenceValue = badgeRect.Find("Disc").GetComponent<Image>();
                data.FindProperty("ring").objectReferenceValue = ring;
                data.FindProperty("icon").objectReferenceValue = iconRect.GetComponent<Image>();
                data.FindProperty("pointerPivot").objectReferenceValue = pivot;
                data.FindProperty("group").objectReferenceValue = group;
                data.ApplyModifiedPropertiesWithoutUndo();

                var view = root.AddComponent<TeammateDangerHudView>();
                var viewData = new SerializedObject(view);
                viewData.FindProperty("area").objectReferenceValue = area;
                viewData.FindProperty("template").objectReferenceValue = badge;
                viewData.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Link()
        {
            var settings = GameSettings.WorldUi;
            if (settings == null || !AssetDatabase.Contains(settings)) { Debug.LogWarning("[TeammateDanger] 월드 UI 설정 에셋을 찾지 못해 연결하지 않았습니다."); return; }
            var data = new SerializedObject(settings);
            void Set(string field, Object value) { var p = data.FindProperty(field); if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = value; }
            Set("teammateDangerHud", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Set("dangerHangIcon", Load("danger-hang"));
            Set("dangerCutIcon", Load("danger-cut"));
            Set("dangerFallIcon", Load("danger-fall"));
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
