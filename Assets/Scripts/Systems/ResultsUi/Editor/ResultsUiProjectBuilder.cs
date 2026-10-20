#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.ResultsV1.Editor
{
    /// <summary>C12 전용 산출물만 조립한다. 승인 정본19개와 공유 폰트는 읽기 전용이다.</summary>
    public static class ResultsUiProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_results_v1";
        public const string PrefabFolder = "Assets/Resources/Prefabs/UIs/ResultsUi";
        public const string PrefabPath = PrefabFolder + "/ResultsPanel.prefab";
        public const string SceneFolder = "Assets/Scenes/Tests/ResultsUi";
        public const string ScenePath = SceneFolder + "/ResultsUi.unity";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        // 상단14개·하단14개. 기능을 합친 버튼은 개수 하한/상한, 목적지 토글, 시간/명암/백그라운드 토글이다.
        public static readonly string[] Labels = { "실패 열기", "성공 열기", "대성공 열기", "2명", "3명", "4명", "로컬 0명",
            "위치·추락 겹침", "null·NaN·Inf", "범위 밖 위치", "긴 문구", "같은 세션 갱신", "다른 세션 거절", "확인 활성 토글",
            "확인 라벨 토글", "확인 의도", "Hide", "Hide 후 재열기", "동전 하한·중복", "동전 상한·중복", "동전 잘못된 세션",
            "목적지 활성 토글", "목적지 null", "목적지 복구", "시간 정지/복구", "배경 명암", "백그라운드 실행", "미리보기 복구" };
        public static string RequiredText => ResultsUiPreview.RequiredText + " " + string.Join(" ", Labels);
        static bool queued;
        sealed class Source
        {
            public string Key, RelativePath, CanonicalKit;
            public int Width, Height;
            public Vector4 Border;
            public bool New;
            public string CopyPath => Kit + "/" + RelativePath;
            public string Path => New ? CopyPath : "Assets/_ThirdParty/" + CanonicalKit + "/" + RelativePath;
        }
        [MenuItem("Tools/Curtain Call/C12 Results UI/Build prefab and preview")]
        public static void Build()
        {
            ValidateEnvironment(); ValidateEmptyOutputs();
            if (queued) throw new InvalidOperationException("C12 조립이 이미 대기 중입니다.");
            var sources = Sources(); foreach (var source in sources) ValidateSource(source, !source.New);
            ResolveFonts(out _, out _, out _);
            foreach (var source in sources.Where(s => s.New)) { ConfigureNewImporter(source); ValidateSource(source, true); }
            queued = true; EditorApplication.update += FinalizeWhenIdle;
            Debug.Log("C12 유휴 틱 조립 대기. 리로드 후 자동 재시도하지 않으며 부분 출력은 보존합니다.");
        }
        static void FinalizeWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= FinalizeWhenIdle; queued = false;
            Scene original = default, created = default; bool saved = false;
            try
            {
                ValidateEnvironment(); ValidateEmptyOutputs();
                var sources = Sources(); foreach (var source in sources) ValidateSource(source, true);
                ResolveFonts(out var medium, out var semibold, out var bold);
                var sprites = sources.ToDictionary(s => s.Key, s => AssetDatabase.LoadAssetAtPath<Sprite>(s.Path));
                EnsureFolder(PrefabFolder); EnsureFolder(SceneFolder);
                original = SceneManager.GetActiveScene();
                created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(created);
                var canvas = MakeCanvas("C12_ResultsCanvas", 0);
                var view = CurtainCall.UI.Editor.ResultsPanelBuilder.BuildProject(canvas, medium, semibold, bold, sprites);
                if (view == null || view.IsVisible) throw new InvalidOperationException("C12 조립 결과는 Hidden이어야 합니다.");
                if (Exists(PrefabPath)) throw new IOException("C12 조립 중 prefab/meta가 생겼습니다. 덮어쓰지 않습니다.");
                PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.gameObject, PrefabPath, InteractionMode.AutomatedAction, out bool success);
                if (!success) throw new IOException("C12 프리팹 저장 실패. 부분 출력은 보존합니다.");
                BuildPreview(MakeCanvas("PreviewControls", 10), view, medium);
                if (Exists(ScenePath)) throw new IOException("C12 조립 중 씬/meta가 생겼습니다. 덮어쓰지 않습니다.");
                if (!EditorSceneManager.SaveScene(created, ScenePath)) throw new IOException("C12 씬 저장 실패. 부분 출력과 dirty 조립 씬을 보존합니다.");
                saved = true; Debug.Log("C12 프리팹·독립 확인 씬 조립 완료. 판정·정산·게임 흐름은 연결하지 않았습니다.");
            }
            catch (Exception ex) { Debug.LogError("C12 중단. importer·부분 출력·dirty 조립 씬 보존, 삭제/롤백/자동 재시도 없음: " + ex); }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (created.IsValid() && created.isLoaded && saved) EditorSceneManager.CloseScene(created, true);
            }
        }
        static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;
        static void ValidateEnvironment()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorUtility.scriptCompilationFailed || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("C12는 Play/컴파일/import/Prefab Stage가 아닌 유휴 에디터가 필요합니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("C12 조립 전에 사람이 저장하지 않은 씬을 처리해야 합니다.");
        }
        static void ValidateEmptyOutputs()
        {
            foreach (string path in new[] { PrefabPath, ScenePath }) if (Exists(path)) throw new InvalidOperationException("C12 기존 출력/meta 보존: " + path);
            foreach (string folder in new[] { PrefabFolder, SceneFolder })
            {
                if (Directory.Exists(folder))
                { if (Directory.EnumerateFileSystemEntries(folder).Any()) throw new InvalidOperationException("C12 출력 폴더가 비어 있지 않습니다: " + folder); }
                else if (Exists(folder)) throw new InvalidOperationException("C12 출력 폴더/meta 충돌: " + folder);
            }
        }
        static Source[] Sources()
        {
            var result = new List<Source>();
            void Add(string key, string folder, string file, int width, int height, Vector4 border, string canonical = null)
            { result.Add(new Source { Key = key, RelativePath = "Assets/CurtainCallUI/" + folder + "/Sprites/" + file + "@2x.png", Width = width, Height = height, Border = border, New = canonical == null, CanonicalKit = canonical }); }
            foreach (string outcome in new[] { "failure", "success", "grand" }) Add("ribbon_" + outcome, "Results", "ui_ribbon_results_" + outcome, 1024, 176, new Vector4(96, 24, 96, 24));
            Add("grand_crest", "Results", "ui_deco_results_crest_grand", 384, 96, Vector4.zero);
            Add("reward_row", "Results", "ui_row_results_reward_normal", 512, 112, Vector4.one * 24);
            Add("coin", "Results", "ui_fx_results_coin_normal", 96, 96, Vector4.zero);
            foreach (string part in new[] { "shadow", "body", "frame", "corners" }) Add("panel_" + part, "BasicPanels", "ui_panel_base_" + part + "_normal", 256, 256, Vector4.one * 64, "curtaincall_panels_v1");
            Add("panel_divider", "BasicPanels", "ui_panel_divider_normal", 256, 32, new Vector4(16, 12, 16, 12), "curtaincall_panels_v1");
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled" }) Add("confirm_" + state, "CommonButtons", "ui_btn_confirm_" + state, 384, 112, new Vector4(32, 24, 32, 24), "curtaincall_buttons_v1");
            foreach (string part in new[] { "shadow", "focus" }) Add("button_" + part, "CommonButtons", "ui_btn_" + part + "_normal", 384, 112, new Vector4(32, 24, 32, 24), "curtaincall_buttons_v1");
            Add("progress_track", "PerformanceHUD", "ui_hud_progress_track_normal", 256, 32, Vector4.one * 12, "curtaincall_performance_hud_v1");
            Add("progress_tick", "PerformanceHUD", "ui_hud_progress_tick_normal", 8, 24, Vector4.zero, "curtaincall_performance_hud_v1");
            string[] markerKeys = { "self", "teammate", "fall", "max" }; int[] sizes = { 64, 48, 40, 32 };
            for (int i = 0; i < markerKeys.Length; i++) Add("marker_" + markerKeys[i], "PerformanceHUD", "ui_hud_progress_" + markerKeys[i] + "_normal", sizes[i], sizes[i], Vector4.zero, "curtaincall_performance_hud_v1");
            foreach (string icon in new[] { "currency", "flag" }) Add("icon_" + icon, "CommonIcons", "ui_icon_" + icon + "_normal", 64, 64, Vector4.zero, "curtaincall_icons_v1");
            if (result.Count != 25 || result.Count(s => s.New) != 6 || result.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count() != 25) throw new InvalidOperationException("C12 원본25개/고유 키25개/신규6개 계약 오류.");
            return result.ToArray();
        }
        static string Hash(string path) { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)); }
        static void ValidateSource(Source source, bool imported)
        {
            if (!File.Exists(source.CopyPath) || !File.Exists(source.Path)) throw new IOException("C12 원본/정본 PNG 없음: " + source.Key);
            if (!source.New && Hash(source.CopyPath) != Hash(source.Path)) throw new InvalidOperationException("C12 복사본/정본 SHA 불일치: " + source.Key);
            var importer = AssetImporter.GetAtPath(source.Path) as TextureImporter; var copyImporter = AssetImporter.GetAtPath(source.CopyPath) as TextureImporter;
            if (importer == null || copyImporter == null) throw new InvalidOperationException("C12 importer 없음: " + source.Key);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height); copyImporter.GetSourceTextureWidthAndHeight(out int copyWidth, out int copyHeight);
            if (width != source.Width || height != source.Height || copyWidth != width || copyHeight != height || !importer.DoesSourceTextureHaveAlpha() || !copyImporter.DoesSourceTextureHaveAlpha()) throw new InvalidOperationException("C12 PNG 크기/알파 불일치: " + source.Key);
            foreach (string platform in Platforms) if (importer.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("C12 플랫폼 override는 자동 해제하지 않습니다: " + source.Key + " / " + platform);
            if (!imported) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(source.Path); var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            if (sprite == null || sprite.rect.size != new Vector2(width, height) || sprite.texture.width != width || sprite.texture.height != height ||
                sprite.border != source.Border || sprite.pixelsPerUnit != 200 || sprite.pivot != new Vector2(width / 2f, height / 2f) ||
                (sprite.texture.format != TextureFormat.RGBA32 && sprite.texture.format != TextureFormat.ARGB32 && sprite.texture.format != TextureFormat.BGRA32) ||
                importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                !importer.alphaIsTransparency || !importer.sRGBTexture || importer.mipmapEnabled || importer.isReadable || importer.filterMode != FilterMode.Bilinear || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.npotScale != TextureImporterNPOTScale.None || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression || settings.spriteMeshType != SpriteMeshType.FullRect)
                throw new InvalidOperationException("C12 Sprite 계약 불일치. 승인 정본은 수정하지 않습니다: " + source.Key);
        }
        static void ConfigureNewImporter(Source source)
        {
            if (!source.New) throw new InvalidOperationException("C12 승인 정본 importer는 읽기 전용입니다.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(source.Path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 200; importer.spriteBorder = source.Border;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = importer.sRGBTexture = true;
            importer.mipmapEnabled = importer.isReadable = false; importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(source.Width, source.Height));
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = Vector2.one * .5f; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        static void ResolveFonts(out TMP_FontAsset medium, out TMP_FontAsset semibold, out TMP_FontAsset bold)
        { medium = ResolveFont("Medium"); semibold = ResolveFont("SemiBold"); bold = ResolveFont("Bold"); }
        static TMP_FontAsset ResolveFont(string weight)
        {
            // 부모 확인에 따라 전용 폰트를 먼저 사용하고, 완전한 기존 폰트만 읽기 전용 후보로 허용한다.
            var candidates = new List<string> {
                "Assets/Resources/Fonts/ResultsUi/Pretendard-" + weight + " SDF.asset",
                "Assets/Resources/Fonts/PerformanceHud/Pretendard-" + weight + " SDF.asset" };
            if (weight == "Medium") candidates.Add("Assets/Resources/Fonts/Spectator/Pretendard-Medium SDF.asset");
            if (weight == "SemiBold") candidates.Add("Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset");
            var errors = new List<string>();
            foreach (string path in candidates)
            {
                try { return RequireFontStatic(path, RequiredText); }
                catch (InvalidOperationException ex) { errors.Add(ex.Message); }
            }
            throw new InvalidOperationException("C12 " + weight + " 폰트 준비 필요:\n" + string.Join("\n", errors));
        }
        public static TMP_FontAsset RequireFontStatic(string path, string text)
        {
            // 글리프 추가, fallback 변경, 동적 폰트 생성 없이 완성된 정적 폰트만 수용한다.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Static || font.isMultiAtlasTexturesEnabled || font.material == null ||
                font.atlasTextures == null || font.atlasTextures.Length != 1 || font.atlasTextures[0] == null || font.atlasWidth <= 1 || font.atlasHeight <= 1 ||
                font.atlasTextures[0].width != font.atlasWidth || font.atlasTextures[0].height != font.atlasHeight || font.material.mainTexture != font.atlasTextures[0] ||
                (font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Count != 0)) throw new InvalidOperationException("C12는 fallback 0인 완전한 읽기 전용 Static 폰트가 필요합니다: " + path);
            string missing = new string(text.Where(c => !char.IsControl(c) && !font.HasCharacter(c, false, false)).Distinct().ToArray());
            if (missing.Length != 0) throw new InvalidOperationException("C12 글리프 누락. 공유 폰트 변경 없이 부모가 전용 폰트를 준비해야 합니다: " + path + " / " + missing);
            return font;
        }
        static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order; canvas.referencePixelsPerUnit = 100;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f; scaler.referencePixelsPerUnit = 100; return canvas;
        }
        static void BuildPreview(Canvas canvas, ResultsPanelView view, TMP_FontAsset font)
        {
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight = 0;
            var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(14, 24, 28, 255); camera.cullingMask = 0;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Background(canvas.transform, "TopControlsBackground", new Vector2(16, -16), new Vector2(1888, 224));
            Background(canvas.transform, "BottomControlsBackground", new Vector2(16, 180), new Vector2(1888, 164), true);
            var status = Text("Status", canvas.transform, font, new Vector2(32, -20), new Vector2(1856, 88)); status.text = ResultsUiPreview.Instructions;
            var caption = Text("Caption", canvas.transform, font, new Vector2(32, 64), new Vector2(1856, 56)); caption.text = ResultsUiPreview.Caption; caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = Vector2.zero;
            var preview = canvas.gameObject.AddComponent<ResultsUiPreview>();
            UnityEngine.Events.UnityAction[] actions = { preview.OpenFailure, preview.OpenSuccess, preview.OpenGrand, preview.TwoMembers, preview.ThreeMembers, preview.FourMembers, preview.NoLocal,
                preview.Overlap, preview.MissingPositions, preview.OutsidePositions, preview.LongText, preview.RefreshPresentation, preview.OtherSession, preview.ToggleAcknowledge,
                preview.ToggleAcknowledgeLabel, preview.RequestAcknowledge, preview.Hide, preview.Reopen, preview.CoinMinimum, preview.CoinMaximum, preview.CoinWrongSession,
                preview.ToggleDestination, preview.NullDestination, preview.RestoreDestination, preview.TogglePause, preview.ToggleBackground, preview.ToggleRunInBackground, preview.ResetPreview };
            var buttons = new UnityEngine.UI.Button[Labels.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                int row = i / 7;
                var rect = Rect("PreviewAction_" + i, canvas.transform, new Vector2(32 + i % 7 * 264, -(116 + row * 56)), new Vector2(252, 44));
                // 상단은 y216 이내. 하단은 H-180부터다. C12 하단 y864와 16:9/16:10 모두 겹치지 않는다.
                if (row >= 2) { rect.anchorMin = rect.anchorMax = Vector2.zero; rect.anchoredPosition = new Vector2(32 + i % 7 * 264, 180 - (row - 2) * 56); }
                rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(57, 68, 78, 255);
                var label = Text("Label", rect, font, Vector2.zero, rect.sizeDelta); label.alignment = TextAlignmentOptions.Center; label.text = Labels[i];
                buttons[i] = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); buttons[i].targetGraphic = rect.GetComponent<UnityEngine.UI.Image>(); UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
            }
            for (int i = 0; i < buttons.Length; i++) buttons[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length], selectOnRight = buttons[(i + 1) % buttons.Length], selectOnUp = buttons[(i + buttons.Length - 7) % buttons.Length], selectOnDown = buttons[(i + 7) % buttons.Length] };
            var destination = view.transform.Find("00_Visuals/10_LayoutStage/00_ResultsPanel/40_Content/00_Art/43_CurrencyIcon") as RectTransform;
            if (destination == null) throw new InvalidOperationException("C12 로컬 목적지 참조가 없습니다.");
            preview.Configure(view, status, camera, buttons, destination);
        }
        static void Background(Transform parent, string name, Vector2 position, Vector2 size, bool bottom = false)
        { var rect = Rect(name, parent, position, size); if (bottom) rect.anchorMin = rect.anchorMax = Vector2.zero; var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color32(14, 24, 28, 255); image.raycastTarget = false; }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect; }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 20; label.enableAutoSizing = false;
            label.raycastTarget = label.richText = label.parseCtrlCharacters = false; label.color = new Color32(240, 238, 220, 255); label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next) && (Exists(next) || string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[i])))) throw new IOException("C12 폴더 충돌/생성 실패: " + next); current = next; }
        }
    }
}
#endif
