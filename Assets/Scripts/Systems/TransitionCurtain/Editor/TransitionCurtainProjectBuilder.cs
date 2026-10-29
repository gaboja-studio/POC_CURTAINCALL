#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CurtainCall.UI;
using CurtainCall.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace CurtainCall.TransitionCurtain.Editor
{
    public static class TransitionCurtainProjectBuilder
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/TransitionCurtain/TransitionCurtain.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/TransitionCurtain/TransitionCurtain.unity";
        private const string Kit = "Assets/_ThirdParty/curtaincall_curtain_v1/Assets/CurtainCallUI/";
        private const string StarPath = "Assets/_ThirdParty/curtaincall_icons_v1/Assets/CurtainCallUI/CommonIcons/Sprites/ui_icon_star_normal@2x.png";
        private const string FontPath = "Assets/Resources/Fonts/Spectator/Pretendard-Medium SDF.asset";
        private static readonly string[] Keys = { "left_body", "left_fringe", "right_body", "right_fringe", "valance_body", "valance_fringe" };

        [MenuItem("Tools/CurtainCall/Transition Curtain/Build Independent Preview")]
        public static void Build()
        {
            Preflight();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Static || !font.HasCharacters("장소 전환 중…"))
                throw new InvalidOperationException("C16에는 검증된 기존 Static Medium 글리프가 필요합니다.");
            Sprite star = AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
            ValidateSprite(StarPath, star, 64, 64);
            if (Hash(StarPath) != Hash(Kit + "CommonIcons/Sprites/ui_icon_star_normal@2x.png"))
                throw new InvalidOperationException("C16 제공 별과 공용 정본의 SHA가 다릅니다.");
            foreach (string key in Keys) ValidateSource(PathFor(key), key.StartsWith("valance") ? 3968 : 2080, key.StartsWith("valance") ? 480 : 2400);
            var sprites = new Dictionary<string, Sprite> { { "star", star } };
            foreach (string key in Keys)
            {
                string path = PathFor(key);
                ConfigureNewImporter(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                ValidateSprite(path, sprite, key.StartsWith("valance") ? 3968 : 2080, key.StartsWith("valance") ? 480 : 2400);
                sprites.Add(key, sprite);
            }
            Preflight();
            Scene original = SceneManager.GetActiveScene();
            Scene created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(created);
            try
            {
                var root = new GameObject("C16_TransitionCurtainCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.referencePixelsPerUnit = 100;
                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                scaler.referencePixelsPerUnit = 100;
                TransitionCurtainView view = TransitionCurtainBuilder.BuildProject(canvas, font, sprites);
                // RectMask2D의 경계 1px 감쇠가 화면 끝에서 배경을 노출하지 않도록 클립만 확장한다.
                view.GetComponentInChildren<UnityEngine.UI.RectMask2D>(true).padding = Vector4.one * -2;
                bool saved;
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction, out saved);
                if (!saved) throw new InvalidOperationException("C16 프리팹 저장 실패. 부분 출력은 보존합니다.");
                BuildPreview(view, font);
                if (!EditorSceneManager.SaveScene(created, ScenePath)) throw new InvalidOperationException("C16 씬 저장 실패");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (!created.isDirty) EditorSceneManager.CloseScene(created, true);
            }
            Debug.Log("C16 독립 프리팹/확인 씬 제작 완료. 기존 출력은 재생성하지 않습니다.");
        }

        private static void Preflight()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("준비된 편집 모드에서만 C16을 조립합니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("저장하지 않은 씬이 있어 C16 조립을 거절합니다.");
            foreach (string path in new[] { PrefabPath, ScenePath })
                if (File.Exists(path) || File.Exists(path + ".meta")) throw new InvalidOperationException("기존 C16 출력 보존: " + path);
        }

        private static string PathFor(string key) { return Kit + "TransitionCurtain/Sprites/ui_fx_curtain_" + key + "_normal@2x.png"; }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream));
        }
        private static void ValidateSource(string path, int width, int height)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("C16 importer 없음: " + path);
            int w, h;
            importer.GetSourceTextureWidthAndHeight(out w, out h);
            if (w != width || h != height || !importer.DoesSourceTextureHaveAlpha()) throw new InvalidOperationException("C16 원본 규격 오류: " + path);
        }
        private static void ValidateSprite(string path, Sprite sprite, int width, int height)
        {
            ValidateSource(path, width, height);
            if (sprite == null || sprite.rect.size != new Vector2(width, height) || sprite.texture.width != width || sprite.texture.height != height ||
                sprite.pixelsPerUnit != 200 || sprite.border != Vector4.zero || sprite.pivot != new Vector2(width / 2f, height / 2f) || sprite.texture.format != TextureFormat.RGBA32)
                throw new InvalidOperationException("C16 Sprite 규격 오류: " + path);
        }
        private static void ConfigureNewImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200;
            importer.spriteBorder = Vector4.zero;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = 4096;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        private static void BuildPreview(TransitionCurtainView view, TMP_FontAsset font)
        {
            var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(27, 35, 42, 255);
            camera.cullingMask = 0;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var root = new GameObject("PreviewControls", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = -1;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var buttons = new List<UnityEngine.UI.Button>();
            string[] names = { "닫기", "열기", "즉시 열기", "반전", "시간 정지", "시간 복구", "배경 명암", "긴 문구", "기본 문구" };
            for (int i = 0; i < names.Length; i++)
            {
                var go = new GameObject("Action" + i, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                var rect = (RectTransform)go.transform;
                rect.SetParent(root.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(32 + i % 3 * 192, -32 - i / 3 * 64);
                rect.sizeDelta = new Vector2(180, 52);
                go.GetComponent<UnityEngine.UI.Image>().color = new Color32(57, 68, 78, 255);
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(rect, false);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.font = font;
                label.fontSize = 24;
                label.text = names[i];
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                buttons.Add(go.GetComponent<UnityEngine.UI.Button>());
            }
            root.AddComponent<TransitionCurtainPreview>().Configure(view, camera, buttons.ToArray());
        }
    }
}
#endif
