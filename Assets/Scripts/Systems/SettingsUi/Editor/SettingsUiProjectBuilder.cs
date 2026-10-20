#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.UI;
using CurtainCall.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace CurtainCall.SettingsUi.Editor
{
    /// <summary>C14 독립 산출물을 한 번 조립한다. 게임 설정과 공유 리소스는 수정하지 않는다.</summary>
    public static class SettingsUiProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_settings_v1";
        public const string PrefabFolder = "Assets/Resources/Prefabs/UIs/SettingsUi";
        public const string PrefabPath = PrefabFolder + "/SettingsPanel.prefab";
        public const string TabPrefabPath = PrefabFolder + "/CommonTab.prefab";
        public const string SliderPrefabPath = PrefabFolder + "/CommonSlider.prefab";
        public const string TogglePrefabPath = PrefabFolder + "/CommonToggle.prefab";
        public const string SceneFolder = "Assets/Scenes/Tests/SettingsUi";
        public const string ScenePath = SceneFolder + "/SettingsUi.unity";
        public const string FontFolder = "Assets/Resources/Fonts/SettingsUi";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        static readonly string[] Weights = { "Medium", "SemiBold", "Bold" };
        public static readonly string[] Labels = { "데모 열기", "닫기", "적용 요청", "성공 응답", "거절 응답", "응답 보류", "시간 정지/복구", "배경 명암", "이전 응답 재전송", "샘플 활성/비활성", "미리보기 복구" };
        static bool finalizationQueued;
        sealed class Source
        {
            public string Key, RelativePath;
            public int Width, Height;
            public Vector4 Border;
            public bool New;
            public string CopyPath => Kit + "/" + RelativePath;
            public string Path => New ? CopyPath : "Assets/_ThirdParty/" +
                (Key.StartsWith("panel_", StringComparison.Ordinal) ? "curtaincall_panels_v1" : "curtaincall_buttons_v1") + "/" + RelativePath; // CommonIcons도 승인된 buttons 설정 아이콘 정본을 사용한다.
        }

        [MenuItem("Tools/Curtain Call/C14 Settings UI/Build prefab and preview")]
        public static void Build()
        {
            ValidateEnvironment(); ValidateEmptyOutputs(); ValidateFontFolder();
            if (finalizationQueued) throw new InvalidOperationException("C14 finalization is already queued.");
            var sources = Sources();
            foreach (var source in sources) ValidateSource(source, !source.New);
            // 임포트 전에 모든 폰트를 확인한다. 불완전한 공유 Static/Dynamic 폰트는 수정하지 않는다.
            var existingFonts = Weights.Select(w => FindReusableFont(w)).ToArray();
            for (int i = 0; i < Weights.Length; i++)
            {
                if (Exists(FontPath(Weights[i]))) throw new InvalidOperationException("Dedicated output already exists; use explicit Resume after fonts: " + FontPath(Weights[i]));
                if (existingFonts[i] == null && AssetDatabase.LoadAssetAtPath<Font>(FontSource(Weights[i])) == null)
                    throw new InvalidOperationException("C14 source font missing: " + Weights[i]);
            }
            foreach (var source in sources.Where(s => s.New)) { ConfigureNewImporter(source); ValidateSource(source, true); }
            for (int i = 0; i < Weights.Length; i++) if (existingFonts[i] == null) MakeFont(Weights[i]);
            QueueFinalization();
        }

        /// <summary>폰트 생성 후 리로드의 명시적 복구. 온전한 폰트만 수용하며 부분 프리팹·씬 출력은 거절한다.</summary>
        [MenuItem("Tools/Curtain Call/C14 Settings UI/Resume after fonts")]
        public static void ResumeAfterFonts()
        {
            ValidateEnvironment(); ValidateEmptyOutputs(); ValidateFontFolder();
            if (finalizationQueued) throw new InvalidOperationException("C14 finalization is already queued.");
            foreach (var source in Sources()) ValidateSource(source, true);
            foreach (string weight in Weights) ResolveFont(weight);
            QueueFinalization();
        }
        static void QueueFinalization()
        {
            finalizationQueued = true;
            EditorApplication.update += FinalizeWhenIdle;
            Debug.Log("C14 finalization queued for an idle editor tick. After a domain reload, explicitly use Resume after fonts. No automatic overwrite/retry.");
        }
        static void FinalizeWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= FinalizeWhenIdle; finalizationQueued = false;
            Scene original = default, created = default; bool saved = false;
            try
            {
                ValidateEnvironment(); ValidateEmptyOutputs(); ValidateFontFolder();
                var sources = Sources(); foreach (var source in sources) ValidateSource(source, true);
                var sprites = sources.ToDictionary(s => s.Key, s => AssetDatabase.LoadAssetAtPath<Sprite>(s.Path));
                var medium = ResolveFont("Medium"); var semibold = ResolveFont("SemiBold"); var bold = ResolveFont("Bold");
                EnsureFolder(PrefabFolder); EnsureFolder(SceneFolder);
                original = SceneManager.GetActiveScene();
                created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(created);
                var canvas = MakeCanvas("C14_SettingsCanvas", 0);
                var view = SettingsPanelBuilder.BuildProject(canvas, medium, semibold, bold, sprites);
                SavePrefab(canvas.gameObject, PrefabPath); // 교차 참조를 포함한 Canvas 전체를 저장한다.
                var controls = MakeCanvas("PreviewControls", 10);
                var tab = SettingsPanelBuilder.BuildCommonTab(controls.transform, semibold, sprites);
                SavePrefab(tab, TabPrefabPath); Place(tab, new Vector2(32, -860));
                var slider = SettingsPanelBuilder.BuildCommonSlider(controls.transform, medium, sprites);
                SavePrefab(slider, SliderPrefabPath); Place(slider, new Vector2(288, -848));
                var toggle = SettingsPanelBuilder.BuildCommonToggle(controls.transform, medium, sprites);
                SavePrefab(toggle, TogglePrefabPath); Place(toggle, new Vector2(1040, -848));
                BuildPreview(controls, view, tab, slider, toggle, medium);
                if (Exists(ScenePath)) throw new IOException("C14 scene/meta appeared during assembly; protected.");
                if (!EditorSceneManager.SaveScene(created, ScenePath)) throw new IOException("C14 preview save failed; partial outputs preserved.");
                saved = true;
                Debug.Log("C14 assembly complete: SettingsPanel Canvas + three standalone controls and independent preview. Actual game services are not connected.");
            }
            catch (Exception ex) { Debug.LogError("C14 stopped. Fonts/imports/partial outputs and dirty assembly scene are preserved; no deletion, rollback or automatic retry: " + ex); }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (created.IsValid() && created.isLoaded && (saved || (!created.isDirty && created.rootCount == 0 && string.IsNullOrEmpty(created.path))))
                    EditorSceneManager.CloseScene(created, true);
            }
        }
        static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;
        static void ValidateEnvironment()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("C14 requires an idle editor, not Play/compile/import/Prefab Stage.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("C14 refuses unsaved scenes.");
        }
        static void ValidateEmptyOutputs()
        {
            foreach (string path in new[] { PrefabPath, TabPrefabPath, SliderPrefabPath, TogglePrefabPath, ScenePath })
                if (Exists(path)) throw new InvalidOperationException("Existing C14 output/meta protected: " + path);
            foreach (string folder in new[] { PrefabFolder, SceneFolder })
            {
                if (Directory.Exists(folder))
                {
                    if (Directory.EnumerateFileSystemEntries(folder).Any()) throw new InvalidOperationException("C14 output folder must be empty: " + folder);
                }
                else if (Exists(folder)) throw new InvalidOperationException("C14 output folder/meta conflict: " + folder);
            }
        }
        static void ValidateFontFolder()
        {
            if (!Directory.Exists(FontFolder))
            {
                if (Exists(FontFolder)) throw new InvalidOperationException("C14 font folder/meta conflict.");
                return;
            }
            var allowed = new HashSet<string>(Weights.SelectMany(w => new[] { FontPath(w), FontPath(w) + ".meta" }).Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            if (Directory.EnumerateFileSystemEntries(FontFolder).Any(p => !allowed.Contains(Path.GetFullPath(p))))
                throw new InvalidOperationException("Unexpected C14 font outputs are preserved; recovery refused.");
        }
        static Source[] Sources()
        {
            var result = new List<Source>();
            void Add(string key, string folder, string file, int width, int height, Vector4 border, bool fresh)
            { result.Add(new Source { Key = key, RelativePath = "Assets/CurtainCallUI/" + folder + "/Sprites/" + file + "@2x.png", Width = width, Height = height, Border = border, New = fresh }); }
            var tabBorder = new Vector4(24, 20, 24, 20);
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "selected", "focus" })
                Add("tab_" + state, "CommonTabs", "ui_tab_standard_" + state, 288, state == "selected" ? 16 : 96, state == "selected" ? new Vector4(16, 0, 16, 0) : tabBorder, true);
            Add("slider_track", "CommonSliders", "ui_slider_track_normal", 512, 32, new Vector4(16, 12, 16, 12), true);
            Add("slider_fill", "CommonSliders", "ui_slider_fill_normal", 512, 16, new Vector4(0, 4, 0, 4), true);
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                Add(state == "focus" ? "slider_focus" : "slider_handle_" + state, "CommonSliders", "ui_slider_handle_" + state, 64, 64, Vector4.zero, true);
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "on", "focus" })
                Add("toggle_" + state, "CommonToggles", "ui_toggle_standard_" + state, 160, 80, tabBorder, true);
            Add("toggle_thumb", "CommonToggles", "ui_toggle_thumb_normal", 64, 64, Vector4.zero, true);
            foreach (string part in new[] { "shadow", "body", "frame", "corners" })
                Add("panel_" + part, "BasicPanels", "ui_panel_base_" + part + "_normal", 256, 256, Vector4.one * 64, false);
            Add("panel_divider", "BasicPanels", "ui_panel_divider_normal", 256, 32, new Vector4(16, 12, 16, 12), false);
            foreach (string role in new[] { "confirm", "cancel" })
                foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
                    Add(role + "_" + state, "CommonButtons", "ui_btn_" + role + "_" + state, 384, 112, new Vector4(32, 24, 32, 24), false);
            foreach (string part in new[] { "shadow", "focus" })
                Add("button_" + part, "CommonButtons", "ui_btn_" + part + "_normal", 384, 112, new Vector4(32, 24, 32, 24), false);
            Add("settings_icon", "CommonButtons", "ui_icon_settings_normal", 64, 64, Vector4.zero, false);
            return result.ToArray();
        }
        static string Hash(string path)
        { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)); }
        static void ValidateSource(Source source, bool imported)
        {
            if (!source.New && Hash(source.CopyPath) != Hash(source.Path)) throw new InvalidOperationException("C14 copy/canonical bytes mismatch: " + source.Key);
            var importer = AssetImporter.GetAtPath(source.Path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("C14 importer missing: " + source.Key);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != source.Width || height != source.Height || !importer.DoesSourceTextureHaveAlpha()) throw new InvalidOperationException("C14 source size/alpha mismatch: " + source.Key);
            foreach (string platform in Platforms) if (importer.GetPlatformTextureSettings(platform).overridden)
                throw new InvalidOperationException("Platform override requires review, not automatic clearing: " + source.Key + " / " + platform);
            if (!imported) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(source.Path);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            if (sprite == null || sprite.rect.size != new Vector2(width, height) || sprite.texture.width != width || sprite.texture.height != height ||
                sprite.border != source.Border || sprite.pixelsPerUnit != 200 || sprite.pivot != new Vector2(width / 2f, height / 2f) ||
                (sprite.texture.format != TextureFormat.RGBA32 && sprite.texture.format != TextureFormat.ARGB32 && sprite.texture.format != TextureFormat.BGRA32) ||
                importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.alphaIsTransparency || !importer.sRGBTexture ||
                importer.mipmapEnabled || importer.isReadable || importer.filterMode != FilterMode.Bilinear || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.npotScale != TextureImporterNPOTScale.None || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression ||
                settings.spriteMeshType != SpriteMeshType.FullRect)
                throw new InvalidOperationException("C14 Sprite contract mismatch; canonical assets stay read-only: " + source.Key);
        }
        static void ConfigureNewImporter(Source source)
        {
            if (!source.New) throw new InvalidOperationException("C14 canonical imports are read-only.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(source.Path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200; importer.spriteBorder = source.Border;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = importer.sRGBTexture = true;
            importer.mipmapEnabled = importer.isReadable = false; importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(source.Width, source.Height));
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = Vector2.one * .5f;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        static string FontPath(string weight) => FontFolder + "/Pretendard-" + weight + " SDF.asset";
        static string FontSource(string weight) => Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf";
        static string Characters(string weight)
        {
            if (weight == "Bold") return "설정";
            return new string(Enumerable.Range(32, 95).Concat(Enumerable.Range(0xAC00, 11172)).Concat(Enumerable.Range(0x3131, 51)).Select(c => (char)c).ToArray()) + "…·—•←→×";
        }
        static bool CompleteStatic(TMP_FontAsset font, string weight)
        {
            return font != null && font.atlasPopulationMode == AtlasPopulationMode.Static && !font.isMultiAtlasTexturesEnabled &&
                (font.fallbackFontAssetTable == null || font.fallbackFontAssetTable.Count == 0) && font.material != null &&
                font.atlasTextures != null && font.atlasTextures.Length == 1 && font.atlasTextures[0] != null &&
                font.atlasTextures[0].width == font.atlasWidth && font.atlasTextures[0].height == font.atlasHeight && font.atlasWidth > 1 &&
                font.HasCharacters(Characters(weight)); // 동적 글리프 추가나 fallback 검색은 하지 않는다.
        }
        static TMP_FontAsset FindReusableFont(string weight)
        {
            string preferred = "Assets/Resources/Fonts/" + (weight == "Medium" ? "Spectator" : weight == "SemiBold" ? "BasicPanels" : "WorldLabels") + "/Pretendard-" + weight + " SDF.asset";
            var paths = new[] { preferred }.Concat(AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Resources/Fonts" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)).Distinct();
            foreach (string path in paths)
            {
                if (path.StartsWith(FontFolder + "/", StringComparison.Ordinal) || !path.EndsWith("/Pretendard-" + weight + " SDF.asset", StringComparison.Ordinal)) continue;
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (CompleteStatic(font, weight)) return font;
            }
            return null;
        }
        static TMP_FontAsset ResolveFont(string weight)
        {
            if (Exists(FontPath(weight)))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath(weight));
                var guid = font == null ? null : new SerializedObject(font).FindProperty("m_SourceFontFileGUID");
                if (!CompleteStatic(font, weight) || guid == null || guid.stringValue != AssetDatabase.AssetPathToGUID(FontSource(weight)) ||
                    AssetDatabase.GetAssetPath(font.material) != FontPath(weight) || AssetDatabase.GetAssetPath(font.atlasTextures[0]) != FontPath(weight))
                    throw new InvalidOperationException("C14 dedicated font incomplete; preserved, not rebaked: " + weight);
                return font;
            }
            return FindReusableFont(weight) ?? throw new InvalidOperationException("C14 complete Static font unavailable: " + weight);
        }
        static void MakeFont(string weight)
        {
            if (Exists(FontPath(weight))) throw new InvalidOperationException("C14 font output/meta protected: " + weight);
            EnsureFolder(FontFolder);
            int size = weight == "Bold" ? 2048 : 8192;
            var font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(FontSource(weight)), 48, 5, GlyphRenderMode.SDFAA, size, size, AtlasPopulationMode.Dynamic, false);
            if (font == null) throw new InvalidOperationException("C14 font creation failed: " + weight);
            font.name = "SettingsUi Pretendard-" + weight + " SDF";
            AssetDatabase.CreateAsset(font, FontPath(weight));
            font.material.name = font.name + " Material"; AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures.Where(t => t != null)) { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }
            bool added = font.TryAddCharacters(Characters(weight), out string missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; font.isMultiAtlasTexturesEnabled = false; font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            EditorUtility.SetDirty(font); foreach (var atlas in font.atlasTextures.Where(t => t != null)) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssetIfDirty(font);
            if (!added || !CompleteStatic(font, weight)) throw new InvalidOperationException("C14 dedicated glyphs missing; partial font preserved: " + missing);
        }
        static Canvas MakeCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortingOrder; canvas.referencePixelsPerUnit = 100;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f; scaler.referencePixelsPerUnit = 100;
            return canvas;
        }
        static void SavePrefab(GameObject root, string path)
        {
            if (Exists(path)) throw new IOException("C14 prefab/meta appeared during assembly; protected: " + path);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction, out bool success);
            if (!success) throw new IOException("C14 prefab save failed; partial outputs preserved: " + path);
        }
        static void Place(GameObject go, Vector2 position)
        {
            var rect = (RectTransform)go.transform;
            // 확인 씬 샘플은 하단에 고정해 16:10에서도 중앙 설정 패널과 겹치지 않는다.
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = new Vector2(position.x, 1080 + position.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        static void BuildPreview(Canvas canvas, SettingsPanelView view, GameObject tab, GameObject slider, GameObject toggle, TMP_FontAsset font)
        {
            // 공용 컨트롤의 밝은 글자를 확인 씬의 밝은 카메라 배경에서도 읽을 수 있게 한다.
            // 프리팹과 입력 영역은 바꾸지 않는 미리보기 전용 국소 배경이다.
            var sampleBackground = new GameObject("StandaloneControlsBackground", typeof(RectTransform), typeof(Image));
            sampleBackground.transform.SetParent(canvas.transform, false);
            sampleBackground.transform.SetAsFirstSibling();
            var sampleRect = (RectTransform)sampleBackground.transform;
            sampleRect.anchorMin = sampleRect.anchorMax = Vector2.zero; sampleRect.pivot = new Vector2(0, 1);
            sampleRect.anchoredPosition = new Vector2(16, 248); sampleRect.sizeDelta = new Vector2(1888, 96);
            var sampleImage = sampleBackground.GetComponent<Image>();
            sampleImage.color = new Color32(14, 24, 28, 255); sampleImage.raycastTarget = false;
            var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(14, 24, 28, 255); camera.cullingMask = 0;
            // 출력 씬에만 하나를 만들고 다른 열린 씬의 입력 모듈은 변경하지 않는다.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var status = Text("Status", canvas.transform, font, new Vector2(32, -24), new Vector2(1856, 96));
            status.text = "C14 합성 확인 · 실제 설정 서비스 연결 없음 · Play 시작 Hidden / 데모 열기로 시작";
            var caption = Text("StandaloneControlsCaption", canvas.transform, font, new Vector2(32, -940), new Vector2(1856, 72));
            var captionRect = (RectTransform)caption.transform;
            captionRect.anchorMin = captionRect.anchorMax = Vector2.zero; captionRect.anchoredPosition = new Vector2(32, 140);
            caption.text = "아래 공용 컨트롤은 설정 창과 독립입니다. 포인터/키보드로 조작하세요.\n1920×1080 / 1920×1200 · 명암 배경 · 시간 정지 중 응답/모션 확인";
            var preview = canvas.gameObject.AddComponent<SettingsUiPreview>();
            preview.Configure(view, status, caption, camera, tab.GetComponentInChildren<CurtainCallTabVisual>(true), slider.GetComponentInChildren<CurtainCallSliderVisual>(true), toggle.GetComponentInChildren<CurtainCallToggleVisual>(true));
            UnityEngine.Events.UnityAction[] actions = { preview.OpenDemo, preview.Close, preview.RequestApply, preview.AcceptResponse, preview.RejectResponse, preview.DeferResponse, preview.TogglePause, preview.ToggleBackground, preview.ReplayPreviousResponse, preview.ToggleSamplesInteractable, preview.ResetPreview };
            var buttons = new Button[Labels.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                var go = new GameObject("PreviewAction_" + i, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(canvas.transform, false);
                var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(32 + i % 6 * 304, -128 - i / 6 * 64); rect.sizeDelta = new Vector2(288, 52);
                go.GetComponent<Image>().color = new Color32(57, 68, 78, 255);
                var label = Text("Label", rect, font, Vector2.zero, rect.sizeDelta); label.alignment = TextAlignmentOptions.Center; label.text = Labels[i];
                buttons[i] = go.GetComponent<Button>(); UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
            }
            var tabControl = tab.GetComponentInChildren<Toggle>(true); var sliderControl = slider.GetComponentInChildren<Slider>(true); var toggleControl = toggle.GetComponentInChildren<Toggle>(true);
            for (int i = 0; i < buttons.Length; i++) buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length], selectOnRight = buttons[(i + 1) % buttons.Length],
                selectOnUp = i >= 6 ? buttons[i - 6] : toggleControl, selectOnDown = i + 6 < buttons.Length ? buttons[i + 6] : tabControl };
            tabControl.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = buttons[0], selectOnDown = sliderControl, selectOnRight = sliderControl };
            sliderControl.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = tabControl, selectOnDown = toggleControl }; // 좌우 입력은 native 값 조절에 남긴다.
            toggleControl.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = sliderControl, selectOnDown = buttons[0], selectOnLeft = tabControl };
            foreach (var control in new Selectable[] { tabControl, sliderControl, toggleControl }) PrefabUtility.RecordPrefabInstancePropertyModifications(control);
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 22; text.enableAutoSizing = false;
            text.color = new Color32(240, 238, 220, 255); text.raycastTarget = text.richText = text.parseCtrlCharacters = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next) && (Exists(next) || string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[i]))))
                    throw new IOException("C14 folder creation conflict/failure: " + next);
                current = next;
            }
        }
    }
}
#endif
