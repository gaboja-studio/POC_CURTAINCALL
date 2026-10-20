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

namespace CurtainCall.UI.ScheduleV1.Editor
{
    /// <summary>W3 전용 산출물만 조립한다. 정본 Sprite와 기존 폰트는 모두 읽기 전용이다.</summary>
    public static class ScheduleUiProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_schedule_v1";
        public const string PrefabFolder = "Assets/Resources/Prefabs/UIs/ScheduleUi";
        public const string PrefabPath = PrefabFolder + "/SchedulePanel.prefab";
        public const string SceneFolder = "Assets/Scenes/Tests/ScheduleUi";
        public const string ScenePath = SceneFolder + "/ScheduleUi.unity";
        const string MediumPath = "Assets/Resources/Fonts/Spectator/Pretendard-Medium SDF.asset";
        const string SemiBoldPath = "Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset";
        const string BoldPath = "Assets/Resources/Fonts/ScheduleUi/Pretendard-Bold SDF.asset";
        const string ExistingBoldPath = "Assets/Resources/Fonts/WorldLabels/Pretendard-Bold SDF.asset";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        public static readonly string[] Labels = { "기본 데모", "빈 열", "많은 행 12개", "긴 문구", "현재·확정 4조합", "전체 미확정",
            "1열 위", "1열 아래", "2열 위", "2열 아래", "3열 위", "3열 아래", "표시 갱신·재정렬", "다른 세션 거절", "Close",
            "Hide", "닫고 다시 열기", "시간 정지/복구", "배경 명암", "백그라운드 실행", "미리보기 복구" };
        public static string RequiredText => ScheduleUiPreview.RequiredText + " " + string.Join(" ", Labels);
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
        [MenuItem("Tools/Curtain Call/W3 Schedule UI/Build prefab and preview")]
        public static void Build()
        {
            ValidateEnvironment(); ValidateEmptyOutputs();
            if (queued) throw new InvalidOperationException("W3 조립이 이미 대기 중입니다.");
            var sources = Sources(); foreach (var source in sources) ValidateSource(source, !source.New);
            ResolveFonts(out _, out _, out _);
            foreach (var source in sources.Where(s => s.New)) { ConfigureNewImporter(source); ValidateSource(source, true); }
            queued = true; EditorApplication.update += FinalizeWhenIdle;
            Debug.Log("W3 유휴 틱 조립 대기. 리로드 후 자동 재시도하지 않으며 부분 출력은 보존합니다.");
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
                var canvas = MakeCanvas("W3_ScheduleCanvas", 0);
                var view = SchedulePanelBuilder.BuildProject(canvas, medium, semibold, bold, sprites);
                if (view == null || view.IsVisible) throw new InvalidOperationException("W3 조립 결과는 Hidden이어야 합니다.");
                if (Exists(PrefabPath)) throw new IOException("W3 조립 중 prefab/meta가 생겼습니다. 덮어쓰지 않습니다.");
                PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.gameObject, PrefabPath, InteractionMode.AutomatedAction, out bool success);
                if (!success) throw new IOException("W3 프리팹 저장 실패. 부분 출력은 보존합니다.");
                BuildPreview(MakeCanvas("PreviewControls", 10), view, medium);
                if (Exists(ScenePath)) throw new IOException("W3 조립 중 씬/meta가 생겼습니다. 덮어쓰지 않습니다.");
                if (!EditorSceneManager.SaveScene(created, ScenePath)) throw new IOException("W3 씬 저장 실패. 부분 출력과 dirty 조립 씬을 보존합니다.");
                saved = true; Debug.Log("W3 프리팹·독립 확인 씬 조립 완료. 실제 일정·보상·경제·저장·네트워크는 연결하지 않았습니다.");
            }
            catch (Exception ex) { Debug.LogError("W3 중단. importer·부분 출력·dirty 조립 씬 보존, 삭제/롤백/자동 재시도 없음: " + ex); }
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
                throw new InvalidOperationException("W3는 Play/컴파일/import/Prefab Stage가 아닌 유휴 에디터가 필요합니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("W3 조립 전에 사람이 저장하지 않은 씬을 처리해야 합니다.");
        }
        static void ValidateEmptyOutputs()
        {
            foreach (string path in new[] { PrefabPath, ScenePath })
                if (Exists(path)) throw new InvalidOperationException("W3 기존 출력/meta 보존: " + path);
            foreach (string folder in new[] { PrefabFolder, SceneFolder })
            {
                if (Directory.Exists(folder))
                {
                    if (Directory.EnumerateFileSystemEntries(folder).Any()) throw new InvalidOperationException("W3 출력 폴더가 비어 있지 않습니다: " + folder);
                }
                else if (Exists(folder)) throw new InvalidOperationException("W3 출력 폴더/meta 충돌: " + folder);
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
            Add("paper_body", "Schedule", "ui_panel_schedule_paper_normal", 512, 512, Vector4.one * 64);
            Add("paper_material", "Schedule", "ui_texture_schedule_paper_normal", 1984, 688, Vector4.zero);
            Add("day_cell", "Schedule", "ui_cell_schedule_day_normal", 512, 96, new Vector4(24, 20, 24, 20));
            Add("performance_cell", "Schedule", "ui_cell_schedule_performance_normal", 512, 224, Vector4.one * 24);
            Add("current_outline", "Schedule", "ui_cell_schedule_current", 512, 224, Vector4.one * 24);
            Add("current_marker", "Schedule", "ui_marker_schedule_current", 48, 48, Vector4.zero);
            Add("stamp_seal", "Schedule", "ui_stamp_schedule_seal_normal", 160, 160, Vector4.zero);
            Add("stamp_check", "Schedule", "ui_stamp_schedule_check_normal", 64, 64, Vector4.zero);
            foreach (string part in new[] { "shadow", "body", "frame", "corners" })
                Add("panel_" + part, "BasicPanels", "ui_panel_base_" + part + "_normal", 256, 256, Vector4.one * 64, "curtaincall_panels_v1");
            Add("panel_divider", "BasicPanels", "ui_panel_divider_normal", 256, 32, new Vector4(16, 12, 16, 12), "curtaincall_panels_v1");
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
                Add("cancel_" + state, "CommonButtons", "ui_btn_cancel_" + state, 384, 112, new Vector4(32, 24, 32, 24), "curtaincall_buttons_v1");
            foreach (string part in new[] { "shadow", "focus" })
                Add("button_" + part, "CommonButtons", "ui_btn_" + part + "_normal", 384, 112, new Vector4(32, 24, 32, 24), "curtaincall_buttons_v1");
            Add("icon_schedule", "CommonIcons", "ui_icon_schedule_normal", 64, 64, Vector4.zero, "curtaincall_icons_v1");
            if (result.Count != 20 || result.Count(s => s.New) != 8 || result.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count() != 20)
                throw new InvalidOperationException("W3 원본20개/고유 키20개/신규8개 계약 오류.");
            return result.ToArray();
        }
        static string Hash(string path)
        { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)); }
        static void ValidateSource(Source source, bool imported)
        {
            if (!File.Exists(source.CopyPath) || !File.Exists(source.Path)) throw new IOException("W3 원본/정본 PNG 없음: " + source.Key);
            if (!source.New && Hash(source.CopyPath) != Hash(source.Path)) throw new InvalidOperationException("W3 복사본/정본 SHA 불일치: " + source.Key);
            var importer = AssetImporter.GetAtPath(source.Path) as TextureImporter;
            var copyImporter = AssetImporter.GetAtPath(source.CopyPath) as TextureImporter;
            if (importer == null || copyImporter == null) throw new InvalidOperationException("W3 importer 없음: " + source.Key);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            copyImporter.GetSourceTextureWidthAndHeight(out int copyWidth, out int copyHeight);
            if (width != source.Width || height != source.Height || copyWidth != width || copyHeight != height ||
                !importer.DoesSourceTextureHaveAlpha() || !copyImporter.DoesSourceTextureHaveAlpha())
                throw new InvalidOperationException("W3 PNG 크기/알파 불일치: " + source.Key);
            foreach (string platform in Platforms)
                if (importer.GetPlatformTextureSettings(platform).overridden)
                    throw new InvalidOperationException("W3 플랫폼 override는 자동 해제하지 않습니다: " + source.Key + " / " + platform);
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
                throw new InvalidOperationException("W3 Sprite 계약 불일치. 승인 정본은 수정하지 않습니다: " + source.Key);
        }
        static void ConfigureNewImporter(Source source)
        {
            if (!source.New) throw new InvalidOperationException("W3 승인 정본 importer는 읽기 전용입니다.");
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
            // 동적 글리프 추가·fallback·자동 폰트 생성은 하지 않는다.
            medium = RequireFont(MediumPath, RequiredText);
            semibold = RequireFont(SemiBoldPath, "123일차 닫기…");
            bold = RequireFont(Exists(BoldPath) ? BoldPath : ExistingBoldPath, "공연 일정표…");
        }
        static TMP_FontAsset RequireFont(string path, string text)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Static || font.isMultiAtlasTexturesEnabled || font.material == null ||
                font.atlasTextures == null || font.atlasTextures.Length != 1 || font.atlasTextures[0] == null || font.atlasWidth <= 1 ||
                font.atlasTextures[0].width != font.atlasWidth || font.atlasTextures[0].height != font.atlasHeight ||
                (font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Count != 0))
                throw new InvalidOperationException("W3는 fallback 0인 완전한 읽기 전용 Static 폰트가 필요합니다: " + path);
            string missing = new string(text.Where(c => !char.IsControl(c) && !font.HasCharacter(c, false, false)).Distinct().ToArray());
            if (missing.Length != 0) throw new InvalidOperationException("W3 글리프 누락. 공유 폰트를 변경하지 않고 부모에게 보고합니다: " + path + " / " + missing);
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
        static void BuildPreview(Canvas canvas, SchedulePanelView view, TMP_FontAsset font)
        {
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight = 0;
            var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(14, 24, 28, 255); camera.cullingMask = 0;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Background(canvas.transform, "TopControlsBackground", new Vector2(16, -16), new Vector2(1888, 224));
            Background(canvas.transform, "BottomControlsBackground", new Vector2(16, 224), new Vector2(1888, 208), true);
            var status = Text("Status", canvas.transform, font, new Vector2(32, -24), new Vector2(1856, 88)); status.text = ScheduleUiPreview.Instructions;
            var caption = Text("Caption", canvas.transform, font, new Vector2(32, 88), new Vector2(1856, 64)); caption.text = ScheduleUiPreview.Caption;
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = Vector2.zero;
            var preview = canvas.gameObject.AddComponent<ScheduleUiPreview>();
            UnityEngine.Events.UnityAction[] actions = { preview.OpenDemo, preview.Empty, preview.Many, preview.LongText, preview.FourStates, preview.Unconfirmed,
                preview.Day1Top, preview.Day1Bottom, preview.Day2Top, preview.Day2Bottom, preview.Day3Top, preview.Day3Bottom, preview.RefreshPresentation,
                preview.OtherSession, preview.Close, preview.Hide, preview.Reopen, preview.TogglePause, preview.ToggleBackground, preview.ToggleRunInBackground, preview.ResetPreview };
            var buttons = new UnityEngine.UI.Button[Labels.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                int row = i / 7;
                var rect = Rect("PreviewAction_" + i, canvas.transform, new Vector2(32 + i % 7 * 264, -(120 + row * 56)), new Vector2(252, 44));
                // 상단 두 행은 y=220에 끝난다. 하단 행은 H-208부터, W3 끝은 H/2+284이므로 16:9·16:10에서 비중첩.
                if (row >= 2) { rect.anchorMin = rect.anchorMax = Vector2.zero; rect.anchoredPosition = new Vector2(32 + i % 7 * 264, 208); }
                rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(57, 68, 78, 255);
                var label = Text("Label", rect, font, Vector2.zero, rect.sizeDelta); label.alignment = TextAlignmentOptions.Center; label.text = Labels[i];
                buttons[i] = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); buttons[i].targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
                UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
            }
            for (int i = 0; i < buttons.Length; i++) buttons[i].navigation = new UnityEngine.UI.Navigation {
                mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length],
                selectOnRight = buttons[(i + 1) % buttons.Length], selectOnUp = buttons[(i + buttons.Length - 7) % buttons.Length], selectOnDown = buttons[(i + 7) % buttons.Length] };
            preview.Configure(view, status, camera, buttons);
        }
        static void Background(Transform parent, string name, Vector2 position, Vector2 size, bool bottom = false)
        {
            var rect = Rect(name, parent, position, size);
            if (bottom) rect.anchorMin = rect.anchorMax = Vector2.zero;
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
                    throw new IOException("W3 폴더 충돌/생성 실패: " + next);
                current = next;
            }
        }
    }
}
#endif
