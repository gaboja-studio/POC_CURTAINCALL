#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    /// <summary>미조립 C10 키트만 신규 Assets 프리팹으로 조립. 제공 원본/importer/기존 프리팹은 보존한다.</summary>
    public static class PerformanceFeedbackInstaller
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/Feedback/Feedback.prefab";
        const string ImageFolder = "Assets/Resources/Images/UIs/Feedback";
        const string AssetFolder = "Assets/Resources/Prefabs/UIs/Feedback";
        const string Source = "Assets/_ThirdParty/curtaincall_feedback_v1/Assets/CurtainCallUI/Feedback/Sprites/";

        [MenuItem("Tools/Curtain Call UI/Install C10 feedback")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor must be idle.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) prefab = Build();
                var scene = EditorSceneManager.OpenScene(CommonPerformanceUI.ScenePath);
                var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CommonPerformanceUI>(true)).Single();
                if (owner.Feedback == null)
                {
                    var ui = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "UI")?.transform ?? owner.transform.Find("UI") ?? owner.transform;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ui);
                    instance.name = "CommonFeedbackCanvas";
                    owner.ConfigureFeedback(instance.GetComponentInChildren<PerformanceFeedback>(true));
                    EditorUtility.SetDirty(owner);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        static GameObject Build()
        {
            Folder(AssetFolder); Folder(ImageFolder);
            var root = new GameObject("Feedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 85;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var layer = Rect("40_TransientFeedback", root.transform); Stretch(layer);
                var stable = Rect("C10_Feedback", layer); stable.sizeDelta = new Vector2(360, 80); stable.anchoredPosition = new Vector2(0, -48);
                var motionRoot = Rect("Motion", stable); Stretch(motionRoot);
                var positive = new List<Image>(); var negative = new List<Image>();
                Pair(positive, motionRoot, "00_FanfareShadow", "01_Fanfare", Sprite("ui_fx_cheer_fanfare_normal@2x.png"), 0, 8, 128, 64, Hex(0x73C7A1), .65f);
                Pair(positive, motionRoot, "10_SparklesShadow", "11_Sparkles", Sprite("ui_fx_cheer_sparkles_normal@2x.png"), 24, 8, 80, 64, Hex(0x73C7A1), .9f);
                Pair(positive, motionRoot, "20_StarShadow", "21_Star", Icon("star"), 48, 24, 32, 32, Hex(0x73C7A1), 1);
                Pair(negative, motionRoot, "30_SplatShadow", "31_Splat", Sprite("ui_fx_boo_splat_normal@2x.png"), 16, 8, 96, 64, Hex(0xE06C78), .45f);
                Pair(negative, motionRoot, "40_TomatoShadow", "41_Tomato", Sprite("ui_fx_boo_tomato_normal@2x.png"), 44, 20, 40, 40, Hex(0xE06C78), 1);
                Pair(negative, motionRoot, "50_ThumbShadow", "51_Thumb", Icon("thumb_down"), 96, 44, 24, 24, Hex(0xE06C78), 1);
                var medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium SDF.asset");
                var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/PerformanceHud/Pretendard-Bold SDF.asset");
                var label = Text("60_LabelTMP", motionRoot, 136, 20, 112, 40, medium, 24);
                var amount = Text("70_AmountTMP", motionRoot, 256, 16, 104, 48, bold, 32);
                var motion = stable.gameObject.AddComponent<FeedbackMotion>();
                motion.Configure(motionRoot, motionRoot.gameObject.AddComponent<CanvasGroup>());
                var view = stable.gameObject.AddComponent<FeedbackView>(); view.Configure(motion, positive.ToArray(), negative.ToArray(), label, amount);
                var adapter = stable.gameObject.AddComponent<PerformanceFeedback>(); adapter.Configure(view, amount);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Sprite Sprite(string file)
        {
            string path = ImageFolder + "/" + file;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                if (!AssetDatabase.CopyAsset(Source + file, path)) throw new InvalidOperationException("Missing C10 source: " + file);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 200; importer.spriteBorder = Vector4.zero;
                importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.sRGBTexture = true; importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false;
                importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("C10 sprite missing: " + path);
        }
        static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_ThirdParty/curtaincall_icons_v1/Assets/CurtainCallUI/CommonIcons/Sprites/ui_icon_" + name + "_normal@2x.png") ?? throw new InvalidOperationException("Missing icon: " + name);
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; return rect;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static RectTransform TL(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = Rect(name, parent); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        static void Pair(List<Image> images, Transform parent, string shadow, string name, Sprite sprite, float x, float y, float w, float h, Color color, float alpha)
        {
            images.Add(Image(TL(shadow, parent, x, y + 1, w, h), sprite, new Color(5f / 255, 9f / 255, 11f / 255, .32f)));
            color.a = alpha; images.Add(Image(TL(name, parent, x, y, w, h), sprite, color));
        }
        static Image Image(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = color;
            image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        static TMP_Text Text(string name, Transform parent, float x, float y, float w, float h, TMP_FontAsset font, float size)
        {
            if (font == null) throw new InvalidOperationException("Missing provided font.");
            string path = AssetFolder + "/" + font.name + " Outline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material); material.SetColor("_OutlineColor", Hex(0x111B20));
                material.SetFloat("_OutlineWidth", .18f); material.EnableKeyword("OUTLINE_ON"); AssetDatabase.CreateAsset(material, path);
            }
            var text = TL(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = material; text.fontSize = size; text.color = Hex(0xF0EEDC);
            text.raycastTarget = false; text.text = ""; text.UpdateMeshPadding(); return text;
        }
        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
#endif
