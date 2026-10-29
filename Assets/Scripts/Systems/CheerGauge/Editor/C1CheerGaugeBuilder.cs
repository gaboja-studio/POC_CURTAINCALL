using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace CurtainCall.CheerGauge.Editor
{
    /// <summary>승인된 전용 경로에만 C1 프리팹과 확인 씬을 만든다.</summary>
    public static class C1CheerGaugeBuilder
    {
        const string Kit = "Assets/_ThirdParty/curtaincall_c1_v1";
        const string Sprites = Kit + "/Assets/CurtainCallUI/C1/Sprites/";
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/CheerGauge/C1CheerGauge.unity";
        const string FontRoot = "Assets/Resources/Fonts/CheerGauge";
        static readonly string[] Layers = { "shadow", "center", "track", "fill", "highlight", "bezel", "roof", "flag" };

        [MenuItem("Tools/Curtain Call/C1 호응 게이지/전용 프리팹과 테스트 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("편집 모드와 컴파일 완료 상태에서만 조립할 수 있습니다.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null || File.Exists(ScenePath))
                throw new InvalidOperationException("기존 결과물을 덮어쓰지 않습니다. 생성된 프리팹/씬을 직접 수정하세요.");
            foreach (string layer in Layers)
                if (!File.Exists(SpritePath(layer))) throw new FileNotFoundException(SpritePath(layer));
            foreach (var scene in EnumerableScenes())
                if (scene.isDirty) throw new InvalidOperationException("열린 씬에 미저장 변경이 있어 중단합니다.");

            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EnsureFolder(FontRoot);
            var sprites = new Sprite[Layers.Length];
            for (int i = 0; i < Layers.Length; i++) sprites[i] = ImportSprite(Layers[i], i == 6 ? 160 : i == 7 ? 32 : 144);
            var medium = MakeFont("Medium");
            var bold = MakeFont("Bold");

            var sceneAsset = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(25, 34, 38, 255);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.tag = "MainCamera";
            var canvas = new GameObject("CheerGaugeCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = Rect("C1CheerGauge", canvas.transform, new Vector2(192, 208), new Vector2(0, -24));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1);
            var ring = Rect("RingRoot", root, new Vector2(144, 144), new Vector2(0, -28));
            Image("Shadow", ring, sprites[0], new Vector2(144, 144), Vector2.zero, new Color(5f / 255, 9f / 255, 11f / 255, 0.32f));
            Image("Center", ring, sprites[1], new Vector2(144, 144), Vector2.zero, new Color(1, 1, 1, 0.96f));
            Image("Track", ring, sprites[2], new Vector2(144, 144), Vector2.zero, Color.white);
            var fills = new UnityEngine.UI.Image[4];
            var highlights = new UnityEngine.UI.Image[4];
            for (int i = 0; i < 4; i++) fills[i] = Image("FillP" + (i + 1), ring, sprites[3], new Vector2(144, 144), Vector2.zero, Color.white);
            var overlay = Rect("Highlights", ring, new Vector2(144, 144), Vector2.zero);
            for (int i = 0; i < 4; i++) highlights[i] = Image("HighlightP" + (i + 1), overlay, sprites[4], new Vector2(144, 144), Vector2.zero, Color.white);
            Image("Bezel", ring, sprites[5], new Vector2(144, 144), Vector2.zero, Color.white);
            var total = Text("Total", ring, new Vector2(100, 52), new Vector2(0, 12), bold, 44, "—");
            var label = Text("Label", ring, new Vector2(80, 24), new Vector2(0, -28), medium, 18, "호응");
            label.fontWeight = FontWeight.Medium;
            Image("Roof", root, sprites[6], new Vector2(160, 40), new Vector2(0, 56), Color.white);
            Image("Flag", root, sprites[7], new Vector2(32, 24), new Vector2(10, 85), Color.white);
            var view = root.gameObject.AddComponent<C1CheerGaugeView>();
            view.Configure(fills, highlights, total, label);
            root.gameObject.AddComponent<C1CheerGaugeBinding>();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            // 프리팹에는 실제 데이터 대기 상태를, 테스트 씬에만 정적155 샘플을 저장한다.
            view.SetScores(new long[] { 60, 45, 30, 20 }, 400, false);
            foreach (var component in root.GetComponentsInChildren<Component>())
                if (component is UnityEngine.UI.Graphic || component is RectTransform)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            var driver = new GameObject("CheerGaugePreview").AddComponent<C1CheerGaugePreview>();
            driver.Configure(view, root.GetComponent<C1CheerGaugeBinding>(),
                AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-Medium.ttf"));
            EditorSceneManager.SaveScene(sceneAsset, ScenePath);
            AssetDatabase.SaveAssetIfDirty(medium);
            AssetDatabase.SaveAssetIfDirty(bold);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("C1 호응 게이지 프리팹·전용 확인 씬 생성 완료: " + ScenePath);
        }

        static System.Collections.Generic.IEnumerable<Scene> EnumerableScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) yield return SceneManager.GetSceneAt(i);
        }
        static string SpritePath(string layer) => Sprites + "ui_hud_cheergauge_" + layer + "_normal@2x.png";
        static Sprite ImportSprite(string layer, int uiWidth)
        {
            string path = SpritePath(layer);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("아직 import되지 않은 이미지: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = width / (float)uiWidth * 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static TMP_FontAsset MakeFont(string weight)
        {
            string path = FontRoot + "/Pretendard-" + weight + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null && existing.HasCharacters("0123456789호응—")) return existing;
            if (existing != null) throw new InvalidOperationException("기존 폰트에 필요한 글리프가 없습니다: " + path);
            if (weight == "Medium")
            {
                var shared = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium SDF.asset");
                if (shared != null && shared.HasCharacters("0123456789호응—")) return shared;
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf");
            if (font == null) throw new InvalidOperationException("폰트 원본 없음: " + weight);
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
            asset.name = "Pretendard-" + weight + " SDF";
            if (!asset.TryAddCharacters("0123456789호응—", out string missing))
                throw new InvalidOperationException("글리프 누락: " + missing);
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
        static UnityEngine.UI.Image Image(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position, Color color)
        {
            var image = Rect(name, parent, size, position).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }
        static TextMeshProUGUI Text(string name, Transform parent, Vector2 size, Vector2 position, TMP_FontAsset font, int fontSize, string value)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = new Color32(240, 238, 220, 255);
            text.raycastTarget = false;
            text.richText = false;
            text.text = value;
            return text;
        }
    }
}
