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

namespace CurtainCall.UI.ShopV1.Editor
{
    /// <summary>W2 소유 Canvas 프리팹과 독립 확인 씬만 조립한다. 공유 에셋·폰트·기존 출력은 읽기 전용이다.</summary>
    public static class ShopUiProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_shop_v1";
        public const string PrefabFolder = "Assets/Resources/Prefabs/UIs/ShopUi";
        public const string PrefabPath = PrefabFolder + "/ShopPanel.prefab";
        public const string SceneFolder = "Assets/Scenes/Tests/ShopUi";
        public const string ScenePath = SceneFolder + "/ShopUi.unity";
        const string MediumPath = "Assets/Resources/Fonts/Spectator/Pretendard-Medium SDF.asset";
        const string DedicatedMediumPath = "Assets/Resources/Fonts/ShopUi/Pretendard-Medium SDF.asset";
        const string SemiBoldPath = "Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset";
        const string BoldPath = "Assets/Resources/Fonts/WorldLabels/Pretendard-Bold SDF.asset";
        const string DedicatedBoldPath = "Assets/Resources/Fonts/ShopUi/Pretendard-Bold SDF.asset";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        public static readonly string[] Labels = { "데모 열기", "기물", "서비스", "장부", "빈 목록", "긴 목록 12행", "긴 설명·가격", "거래 불가",
            "구매 요청", "판매 요청", "성공 응답", "거절 응답", "응답 보류", "이전 응답", "자료 갱신", "시간 정지/복구",
            "배경 명암", "백그라운드 실행", "닫기", "미리보기 복구", "행 활성/비활성", "행동 비활성", "닫고 다시 열기", "선택 항목 제거" };
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

        /// <summary>기존 산출물이 없는 유휴 에디터에서만 신규 조립을 시작한다. 부분 실패는 지우거나 자동 재시도하지 않는다.</summary>
        [MenuItem("Tools/Curtain Call/W2 Shop UI/Build prefab and preview")]
        public static void Build()
        {
            ValidateEnvironment(); ValidateEmptyOutputs();
            if (queued) throw new InvalidOperationException("W2 조립이 이미 대기 중입니다.");
            var sources = Sources();
            foreach (var source in sources) ValidateSource(source, !source.New);
            ResolveFonts(out _, out _, out _);
            // 승인 40개는 검증만 한다. 신규 9개 importer만 이번 작업 구역에서 수정한다.
            foreach (var source in sources.Where(s => s.New)) { ConfigureNewImporter(source); ValidateSource(source, true); }
            queued = true; EditorApplication.update += FinalizeWhenIdle;
            Debug.Log("W2 유휴 틱 조립 대기. 도메인 리로드 후에는 자동 재시도하지 않습니다. 기존·부분 출력은 보존합니다.");
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
                created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(created);
                var canvas = MakeCanvas("W2_ShopCanvas", 0);
                var view = ShopPanelBuilder.BuildProject(canvas, medium, semibold, bold, sprites);
                if (view == null || view.IsVisible || view.IsBusy) throw new InvalidOperationException("W2 조립 결과가 Hidden이 아닙니다.");
                SavePrefab(canvas.gameObject);
                BuildPreview(MakeCanvas("PreviewControls", 10), view, medium);
                if (Exists(ScenePath)) throw new IOException("W2 조립 중 씬 또는 meta가 생겼습니다. 덮어쓰지 않습니다.");
                if (!EditorSceneManager.SaveScene(created, ScenePath)) throw new IOException("W2 씬 저장 실패. 부분 출력과 조립 씬은 보존합니다.");
                saved = true;
                Debug.Log("W2 Canvas 프리팹·독립 확인 씬 조립 완료. 실제 거래·경제 서비스는 연결하지 않았습니다.");
            }
            catch (Exception ex) { Debug.LogError("W2 중단. importer·부분 출력·dirty 조립 씬 보존, 삭제/롤백/자동 재시도 없음: " + ex); }
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
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorUtility.scriptCompilationFailed || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("W2는 Play/컴파일/import/Prefab Stage가 아닌 유휴 에디터가 필요합니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("저장하지 않은 씬은 W2 조립 전에 사람이 처리해야 합니다.");
        }
        static void ValidateEmptyOutputs()
        {
            foreach (string path in new[] { PrefabPath, ScenePath })
                if (Exists(path)) throw new InvalidOperationException("W2 기존 출력/meta 보존: " + path);
            foreach (string folder in new[] { PrefabFolder, SceneFolder })
            {
                if (Directory.Exists(folder))
                {
                    if (Directory.EnumerateFileSystemEntries(folder).Any()) throw new InvalidOperationException("W2 출력 폴더가 비어 있지 않습니다: " + folder);
                }
                else if (Exists(folder)) throw new InvalidOperationException("W2 출력 폴더/meta 충돌: " + folder);
            }
        }
        static Source[] Sources()
        {
            var result = new List<Source>();
            void Add(string key, string folder, string file, int width, int height, Vector4 border, string canonical = null)
            {
                result.Add(new Source { Key = key, RelativePath = "Assets/CurtainCallUI/" + folder + "/Sprites/" + file + "@2x.png",
                    Width = width, Height = height, Border = border, New = canonical == null, CanonicalKit = canonical });
            }
            var rowBorder = new Vector4(24, 20, 24, 20);
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "unavailable", "selected", "focus" })
                Add("row_" + state, "Shop", "ui_row_shop_" + state, 640, 112, rowBorder);
            Add("price_plate", "Shop", "ui_plate_shop_price_normal", 256, 64, new Vector4(20, 16, 20, 16));
            Add("handset", "Shop", "ui_decor_shop_handset_normal", 448, 160, Vector4.zero);
            foreach (string part in new[] { "shadow", "body", "frame", "corners" })
                Add("panel_" + part, "BasicPanels", "ui_panel_base_" + part + "_normal", 256, 256, Vector4.one * 64, "curtaincall_panels_v1");
            Add("panel_divider", "BasicPanels", "ui_panel_divider_normal", 256, 32, new Vector4(16, 12, 16, 12), "curtaincall_panels_v1");
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "selected", "focus" })
                Add("tab_" + state, "CommonTabs", "ui_tab_standard_" + state, 288, state == "selected" ? 16 : 96,
                    state == "selected" ? new Vector4(16, 0, 16, 0) : rowBorder, "curtaincall_settings_v1");
            foreach (string role in new[] { "confirm", "cancel", "general" })
                foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
                    Add(role + "_" + state, "CommonButtons", "ui_btn_" + role + "_" + state, 384, 112, new Vector4(32, 24, 32, 24), "curtaincall_buttons_v1");
            foreach (string part in new[] { "shadow", "focus" })
                Add("button_" + part, "CommonButtons", "ui_btn_" + part + "_normal", 384, 112, new Vector4(32, 24, 32, 24), "curtaincall_buttons_v1");
            foreach (string part in new[] { "shadow", "body", "frame", "corners" })
                Add("tooltip_plate_" + part, "Tooltips", "ui_tooltip_plate_" + part + "_normal", 256, 192, Vector4.one * 24, "curtaincall_tooltips_v1");
            Add("tooltip_bottom_edge", "Tooltips", "ui_tooltip_bottom_edge_normal", 32, 8, Vector4.zero, "curtaincall_tooltips_v1");
            // 필요한 열 아이콘 모두 icons 키트에 존재한다. 다른 키트의 세 가지 공용 아이콘과 혼동하지 않는다.
            foreach (string key in new[] { "phone", "currency", "delivery", "lock", "medical", "tools", "bread", "water", "bandage", "backpack" })
                Add("icon_" + key, "CommonIcons", "ui_icon_" + key + "_normal", 64, 64, Vector4.zero, "curtaincall_icons_v1");
            if (result.Count != 49 || result.Count(s => s.New) != 9 || result.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count() != 49)
                throw new InvalidOperationException("W2 49개/신규9개 바인딩 계약 오류.");
            return result.ToArray();
        }
        static string Hash(string path)
        { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)); }
        static void ValidateSource(Source source, bool imported)
        {
            if (!File.Exists(source.CopyPath) || !File.Exists(source.Path)) throw new IOException("W2 원본/정본 PNG 없음: " + source.Key);
            if (!source.New && Hash(source.CopyPath) != Hash(source.Path)) throw new InvalidOperationException("W2 복사본/정본 SHA 불일치: " + source.Key);
            var importer = AssetImporter.GetAtPath(source.Path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("W2 importer 없음: " + source.Key);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != source.Width || height != source.Height || !importer.DoesSourceTextureHaveAlpha())
                throw new InvalidOperationException("W2 PNG 크기/알파 불일치: " + source.Key);
            foreach (string platform in Platforms)
                if (importer.GetPlatformTextureSettings(platform).overridden)
                    throw new InvalidOperationException("플랫폼 override는 자동 해제하지 않습니다: " + source.Key + " / " + platform);
            if (!imported) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(source.Path);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            if (sprite == null || sprite.rect.size != new Vector2(width, height) || sprite.texture.width != width || sprite.texture.height != height ||
                sprite.border != source.Border || sprite.pixelsPerUnit != 200 || sprite.pivot != new Vector2(width / 2f, height / 2f) ||
                (sprite.texture.format != TextureFormat.RGBA32 && sprite.texture.format != TextureFormat.ARGB32 && sprite.texture.format != TextureFormat.BGRA32) ||
                importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.alphaIsTransparency || !importer.sRGBTexture ||
                importer.mipmapEnabled || importer.isReadable || importer.filterMode != FilterMode.Bilinear || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.npotScale != TextureImporterNPOTScale.None || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.crunchedCompression || settings.spriteMeshType != SpriteMeshType.FullRect)
                throw new InvalidOperationException("W2 Sprite 계약 불일치. 승인 정본은 수정하지 않습니다: " + source.Key);
        }
        static void ConfigureNewImporter(Source source)
        {
            if (!source.New) throw new InvalidOperationException("W2 승인 정본 importer는 읽기 전용입니다.");
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
        static void ResolveFonts(out TMP_FontAsset medium, out TMP_FontAsset semibold, out TMP_FontAsset bold)
        {
            var semiboldTexts = new List<string> { "기물 서비스 장부 닫기 구매 판매 요청" };
            var texts = new List<string> { ShopUiPreview.RequiredText, string.Join(" ", Labels),
                "기물 서비스 장부 플레이는 계속됩니다 요청 처리 중… 표시할 항목이 없습니다 설명과 이용 불가 사유를 확인하세요. 기록의 상세 내용을 확인할 수 있습니다. 현재 요청할 수 없습니다. 요청을 전달하지 못했습니다 처리하지 못했습니다" };
            foreach (var sample in new[] { ShopDemoData.Create(), ShopUiPreview.CreateSample("long"), ShopUiPreview.CreateSample("text"), ShopUiPreview.CreateSample("disabled") })
            {
                texts.Add(sample.Title);
                foreach (ShopTab tab in Enum.GetValues(typeof(ShopTab))) texts.Add(sample.GetContext(tab));
                foreach (var entry in sample.Entries)
                {
                    texts.AddRange(new[] { entry.Title, entry.Subtitle, entry.PriceText, entry.Description, entry.PriceDetail, entry.UnavailableReason, entry.StatusText });
                    semiboldTexts.Add(entry.Title);
                    foreach (var action in entry.Actions) { texts.Add(action.Label); semiboldTexts.Add(action.Label); }
                }
            }
            // 글리프 생성/동적 추가는 하지 않는다. 전용 Medium만 승인된 Static 보조 폰트 한 단계까지 검사한다.
            string text = new string(string.Join(" ", texts).Where(c => !char.IsControl(c)).Distinct().ToArray());
            medium = RequireFont(Exists(DedicatedMediumPath) ? DedicatedMediumPath : MediumPath, text);
            semibold = RequireFont(SemiBoldPath, new string(string.Join(" ", semiboldTexts).Where(c => !char.IsControl(c)).Distinct().ToArray()));
            bold = RequireFont(Exists(DedicatedBoldPath) ? DedicatedBoldPath : BoldPath, ShopDemoData.Create().Title);
        }
        static bool CompleteStatic(TMP_FontAsset font)
        {
            return font != null && font.atlasPopulationMode == AtlasPopulationMode.Static && !font.isMultiAtlasTexturesEnabled && font.material != null &&
                font.atlasTextures != null && font.atlasTextures.Length == 1 && font.atlasTextures[0] != null && font.atlasWidth > 1 &&
                font.atlasTextures[0].width == font.atlasWidth && font.atlasTextures[0].height == font.atlasHeight;
        }
        static TMP_FontAsset RequireFont(string path, string text)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (!CompleteStatic(font)) throw new InvalidOperationException("W2 읽기 전용 Static 폰트가 없거나 불완전합니다. 자동 생성하지 않습니다: " + path);
            bool allowSupport = path == DedicatedMediumPath;
            int count = font.fallbackFontAssetTable == null ? 0 : font.fallbackFontAssetTable.Count;
            if (allowSupport)
            {
                if (count != 1) throw new InvalidOperationException("W2 전용 Medium은 승인된 Static 보조 폰트 하나만 허용합니다: " + path);
                var support = font.fallbackFontAssetTable[0];
                var original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumPath);
                if (!CompleteStatic(support) || support == font ||
                    (support.fallbackFontAssetTable != null && support.fallbackFontAssetTable.Count != 0) ||
                    !AssetDatabase.GetAssetPath(support).StartsWith("Assets/Resources/Fonts/ShopUi/", StringComparison.Ordinal) ||
                    !CompleteStatic(original) || font.material != original.material || font.atlasTextures[0] != original.atlasTextures[0] ||
                    font.characterTable.Count != original.characterTable.Count || font.glyphTable.Count != original.glyphTable.Count)
                    throw new InvalidOperationException("W2 전용 Medium/보조 폰트 계약 오류. 기존 atlas·material은 읽기 전용으로 재사용해야 합니다.");
            }
            else if (count != 0) throw new InvalidOperationException("W2 기존 폰트/SemiBold/Bold에는 fallback을 허용하지 않습니다: " + path);
            string missing = new string(text.Where(c => !font.HasCharacter(c, allowSupport, false)).Distinct().ToArray());
            if (missing.Length > 0) throw new InvalidOperationException("W2 글리프 누락. 공유 폰트를 변경하지 않고 중단합니다: " + path + " / " + missing);
            return font;
        }
        static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order; canvas.referencePixelsPerUnit = 100;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f; scaler.referencePixelsPerUnit = 100; return canvas;
        }
        static void SavePrefab(GameObject root)
        {
            if (Exists(PrefabPath)) throw new IOException("W2 조립 중 prefab/meta가 생겼습니다. 덮어쓰지 않습니다.");
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction, out bool success);
            if (!success) throw new IOException("W2 프리팹 저장 실패. 부분 출력은 보존합니다.");
        }
        static void BuildPreview(Canvas canvas, ShopPanelView view, TMP_FontAsset font)
        {
            // 확인용 8열 버튼은 화면 폭에 맞춘다. 실제 패널의 Match .5는 유지한다.
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight = 0;
            var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(14, 24, 28, 255); camera.cullingMask = 0;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Background(canvas.transform, "TopControlsBackground", new Vector2(16, -16), new Vector2(1888, 232));
            Background(canvas.transform, "BottomControlsBackground", new Vector2(16, 232), new Vector2(1888, 216), true);
            var status = Text("Status", canvas.transform, font, new Vector2(32, -24), new Vector2(1856, 96)); status.text = ShopUiPreview.Instructions;
            var caption = Text("Caption", canvas.transform, font, new Vector2(32, 92), new Vector2(1856, 64)); caption.text = ShopUiPreview.Caption;
            var captionRect = (RectTransform)caption.transform; captionRect.anchorMin = captionRect.anchorMax = Vector2.zero; captionRect.anchoredPosition = new Vector2(32, 92);
            var preview = canvas.gameObject.AddComponent<ShopUiPreview>();
            UnityEngine.Events.UnityAction[] actions = { preview.OpenDemo, preview.Items, preview.Services, preview.Ledger, preview.Empty, preview.LongList, preview.LongText, preview.Unavailable,
                preview.Buy, preview.Sell, preview.AcceptResponse, preview.RejectResponse, preview.DeferResponse, preview.ReplayPreviousResponse, preview.RefreshPresentation, preview.TogglePause,
                preview.ToggleBackground, preview.ToggleRunInBackground, preview.Close, preview.ResetPreview, preview.ToggleRowsDisabled, preview.DisabledAction, preview.Reopen, preview.RemoveSelected };
            var buttons = new UnityEngine.UI.Button[Labels.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                int row = i / 8; float y = row < 2 ? 128 + row * 56 : 864 + (row - 2) * 56;
                var rect = Rect("PreviewAction_" + i, canvas.transform, new Vector2(32 + i % 8 * 232, -y), new Vector2(220, 44));
                // 하단은 화면 아래에 고정해 16:10에서 중앙 W2 패널과 겹치지 않게 한다.
                if (row >= 2) { rect.anchorMin = rect.anchorMax = Vector2.zero; rect.anchoredPosition = new Vector2(32 + i % 8 * 232, 216 - (row - 2) * 56); }
                rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(57, 68, 78, 255);
                var label = Text("Label", rect, font, Vector2.zero, rect.sizeDelta); label.alignment = TextAlignmentOptions.Center; label.text = Labels[i];
                buttons[i] = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
                buttons[i].targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
                UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
            }
            for (int i = 0; i < buttons.Length; i++) buttons[i].navigation = new UnityEngine.UI.Navigation {
                mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length],
                selectOnRight = buttons[(i + 1) % buttons.Length], selectOnUp = buttons[(i + buttons.Length - 8) % buttons.Length], selectOnDown = buttons[(i + 8) % buttons.Length] };
            preview.Configure(view, status, camera, buttons);
        }
        static void Background(Transform parent, string name, Vector2 position, Vector2 size, bool bottom = false)
        {
            var rect = Rect(name, parent, position, size);
            if (bottom) { rect.anchorMin = rect.anchorMax = Vector2.zero; rect.anchoredPosition = position; }
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color32(14, 24, 28, 255); image.raycastTarget = false;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = 22; label.enableAutoSizing = false; label.raycastTarget = label.richText = label.parseCtrlCharacters = false;
            label.color = new Color32(240, 238, 220, 255); label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next) && (Exists(next) || string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[i]))))
                    throw new IOException("W2 폴더 충돌/생성 실패: " + next);
                current = next;
            }
        }
    }
}
#endif
