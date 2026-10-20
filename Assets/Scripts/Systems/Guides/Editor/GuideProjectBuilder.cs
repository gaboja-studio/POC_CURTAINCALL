using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.UI.GuidesV1;
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
using Builder = CurtainCall.UI.GuidesV1.Editor.GuideBuilder;

namespace CurtainCall.Guides.Editor
{
    public static class GuideProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_guides_v1";
        public const string PerformanceKit = "Assets/_ThirdParty/curtaincall_performance_hud_v1";
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/Guides/Guides.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/Guides/Guides.unity";
        public const string FontPath = "Assets/Resources/Fonts/Guides/Pretendard-Medium SDF.asset";
        const string SourceFontPath = Kit + "/Source/Fonts/Pretendard-Medium.ttf";
        const string ButtonPath = "Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        public static readonly string[] Labels = { "일반 키", "넓은 키", "아주 넓은 키", "좌클릭", "우클릭", "마우스 이동", "긴 문구", "문자 그대로", "무효·제어 문자", "반복 Show", "차단", "차단 해제", "퇴장", "즉시 숨김", "시간 정지", "배경", "초기화" };
        public static readonly string[] Keys = { "keycap_wide_body", "keycap_wide_frame", "keycap_extra_wide_body", "keycap_extra_wide_frame", "mouse_base", "mouse_left", "mouse_right", "mouse_move", "plate_shadow", "plate_body", "plate_frame", "plate_corners", "keycap_body", "keycap_frame" };
        [Serializable] class Plan { public Source[] assets; }
        [Serializable] class Source { public string key; public string relative_path; public int[] source_px; public float[] border_lr_tb_px; public bool @new; public string sha256; }
        static string Canonical(Source source) => (source.@new ? Kit : PerformanceKit) + "/" + source.relative_path;
        static Vector2 Size(Source source) => new Vector2(source.source_px[0], source.source_px[1]);
        static Vector4 Border(Source source) => new Vector4(source.border_lr_tb_px[0], source.border_lr_tb_px[3], source.border_lr_tb_px[1], source.border_lr_tb_px[2]);
        static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;

        [MenuItem("Tools/Curtain Call/C7 조작 가이드/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일·임포트를 마친 일반 씬 편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            foreach (string path in new[] { PrefabPath, ScenePath })
                if (Exists(path)) throw new InvalidOperationException("기존 출력은 덮어쓰지 않습니다: " + path);
            var buttonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
            if (buttonAsset == null || buttonAsset.GetComponent<Button>() == null || buttonAsset.GetComponent<RectTransform>() == null || buttonAsset.GetComponentInChildren<TMP_Text>(true) == null)
                throw new InvalidOperationException("공용 버튼 정본 누락/규격 불일치");
            var sources = JsonUtility.FromJson<Plan>(File.ReadAllText(Kit + "/Docs/guides_assembly.json"))?.assets;
            if (sources == null || sources.Length != Keys.Length || !sources.Select(s => s.key).OrderBy(k => k).SequenceEqual(Keys.OrderBy(k => k)) || sources.Count(s => s.@new) != 8)
                throw new InvalidOperationException("가이드 리소스 명세 불일치");
            string characters = Characters();
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null) throw new InvalidOperationException("원본 Medium 폰트 누락");
            var medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (Exists(FontPath) && !SafeFont(medium, sourceFont, characters)) throw new InvalidOperationException("전용 폰트 출력 충돌: " + FontPath);
            // Complete read-only preflight before any importer/font/scene changes.
            foreach (var source in sources)
            {
                string path = Canonical(source);
                var t = AssetImporter.GetAtPath(path) as TextureImporter;
                if (t == null) throw new InvalidOperationException("Importer 누락: " + path);
                t.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (new Vector2(w, h) != Size(source) || !t.DoesSourceTextureHaveAlpha()) throw new InvalidOperationException("원본 크기/알파 불일치: " + path);
                foreach (string platform in Platforms)
                    if (t.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override 확인 필요: " + path);
                if (!source.@new)
                {
                    using var sha = SHA256.Create();
                    string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(hash, source.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("정본 SHA 불일치: " + source.key);
                    ValidateSprite(source, path);
                }
            }
            var original = EditorSceneManager.GetSceneManagerSetup(); bool completed = false;
            try
            {
                var sprites = new Dictionary<string, Sprite>();
                foreach (var source in sources)
                {
                    string path = Canonical(source);
                    if (source.@new) ConfigureImporter(source, path);
                    sprites.Add(source.key, ValidateSprite(source, path));
                }
                if (medium == null) medium = MakeFont(sourceFont, characters);
                EnsureFolder("Assets/Resources/Prefabs/UIs/Guides"); EnsureFolder("Assets/Scenes/Tests/Guides");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var go = Builder.BuildProject(medium, sprites);
                if (PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction) == null) throw new IOException("가이드 프리팹 저장 실패");
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = controls.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
                var scaler = controls.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var status = Text("Status", controls.transform, medium, new Vector2(32, -24), new Vector2(1856, 32));
                status.rectTransform.anchorMin = status.rectTransform.anchorMax = status.rectTransform.pivot = new Vector2(0, 1);
                var preview = controls.AddComponent<GuidePreview>(); preview.Configure(go.GetComponentInChildren<GuideView>(true), status, camera);
                UnityEngine.Events.UnityAction[] actions = { preview.Normal, preview.Wide, preview.ExtraWide, preview.MouseLeft, preview.MouseRight, preview.MouseMove,
                    preview.LongText, preview.Literal, preview.Invalid, preview.RepeatShow, preview.Block, preview.Unblock, preview.Hide, preview.HideImmediate,
                    preview.TogglePause, preview.ToggleBackground, preview.ResetPreview };
                if (actions.Length != Labels.Length) throw new InvalidOperationException("가이드 버튼·메서드 불일치");
                var buttons = new Button[Labels.Length];
                for (int i = 0; i < Labels.Length; i++)
                {
                    var button = (GameObject)PrefabUtility.InstantiatePrefab(buttonAsset, controls.transform); button.name = "Sample_" + i;
                    var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    // Upper-screen rows: lowest bottom is 744 UI, safely above guide top 216 UI.
                    rect.anchoredPosition = new Vector2(32 + i % 5 * 212, -88 - i / 5 * 64); rect.sizeDelta = new Vector2(192, 56);
                    var label = button.GetComponentInChildren<TMP_Text>(true); label.text = Labels[i]; label.font = medium; label.fontSize = 22;
                    label.enableAutoSizing = false; label.richText = false; label.parseCtrlCharacters = false;
                    buttons[i] = button.GetComponent<Button>(); UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(label); PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                for (int i = 0; i < buttons.Length; i++)
                {
                    int rowStart = i / 5 * 5, rowEnd = Mathf.Min(rowStart + 5, buttons.Length) - 1;
                    buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                        selectOnLeft = buttons[i == rowStart ? rowEnd : i - 1], selectOnRight = buttons[i == rowEnd ? rowStart : i + 1],
                        selectOnUp = buttons[i >= 5 ? i - 5 : i], selectOnDown = buttons[i + 5 < buttons.Length ? i + 5 : i] };
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                preview.ResetPreview();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("가이드 씬 저장 실패");
                completed = true;
            }
            catch (Exception exception) { Debug.LogError("가이드 부분 결과·씬·폰트·신규 importer를 보존합니다: " + exception); throw; }
            finally { if (completed) EditorSceneManager.RestoreSceneManagerSetup(original); }
        }
        static void ConfigureImporter(Source source, string path)
        {
            var t = (TextureImporter)AssetImporter.GetAtPath(path);
            t.textureType = TextureImporterType.Sprite; t.spriteImportMode = SpriteImportMode.Single; t.spritePixelsPerUnit = 200; t.spriteBorder = Border(source);
            var settings = new TextureImporterSettings(); t.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = Vector2.one * .5f; t.SetTextureSettings(settings);
            t.alphaSource = TextureImporterAlphaSource.FromInput; t.alphaIsTransparency = true; t.sRGBTexture = true; t.mipmapEnabled = false; t.isReadable = false;
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.npotScale = TextureImporterNPOTScale.None;
            t.textureCompression = TextureImporterCompression.Uncompressed; t.crunchedCompression = false;
            t.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(source.source_px[0], source.source_px[1])); t.SaveAndReimport();
        }
        static Sprite ValidateSprite(Source source, string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); var t = AssetImporter.GetAtPath(path) as TextureImporter;
            var settings = new TextureImporterSettings(); if (t != null) t.ReadTextureSettings(settings);
            if (s == null || t == null || s.rect.size != Size(source) || new Vector2(s.texture.width, s.texture.height) != Size(source) ||
                s.border != Border(source) || s.pivot != Size(source) / 2 || s.pixelsPerUnit != 200 ||
                (s.texture.format != TextureFormat.RGBA32 && s.texture.format != TextureFormat.ARGB32 && s.texture.format != TextureFormat.BGRA32) ||
                t.textureType != TextureImporterType.Sprite || t.spriteImportMode != SpriteImportMode.Single || t.mipmapEnabled || t.isReadable ||
                !t.sRGBTexture || !t.alphaIsTransparency || t.alphaSource != TextureImporterAlphaSource.FromInput ||
                t.wrapMode != TextureWrapMode.Clamp || t.filterMode != FilterMode.Bilinear || t.npotScale != TextureImporterNPOTScale.None ||
                settings.spriteMeshType != SpriteMeshType.FullRect || t.textureCompression != TextureImporterCompression.Uncompressed || t.crunchedCompression)
                throw new InvalidOperationException("Sprite 규격 불일치: " + path);
            foreach (string platform in Platforms) if (t.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override: " + path);
            return s;
        }
        static string Characters()
        {
            string content = string.Join("", Labels) + File.ReadAllText("Assets/Scripts/Systems/Guides/GuidePreview.cs") +
                File.ReadAllText(Kit + "/Assets/CurtainCallUI/Guides/Runtime/GuideView.cs") +
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz…—•·행동 안내점프달리기주 행동보조 행동둘러보기";
            return new string(content.Where(c => !char.IsControl(c) && !char.IsSurrogate(c) && c != (char)0x2028 && c != (char)0x2029).Distinct().ToArray());
        }
        static bool SafeFont(TMP_FontAsset font, Font source, string characters) => font != null && font.sourceFontFile == source &&
            font.atlasPopulationMode == AtlasPopulationMode.Static && !font.isMultiAtlasTexturesEnabled &&
            (font.fallbackFontAssetTable == null || font.fallbackFontAssetTable.Count == 0) && font.HasCharacters(characters);
        static TMP_FontAsset MakeFont(Font source, string characters)
        {
            EnsureFolder("Assets/Resources/Fonts/Guides");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            if (font == null) throw new InvalidOperationException("가이드 폰트 생성 실패");
            font.name = "Guides Pretendard-Medium SDF";
            if (!font.TryAddCharacters(characters, out string missing)) throw new InvalidOperationException("폰트 글리프 부족: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; font.isMultiAtlasTexturesEnabled = false;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>(); AssetDatabase.CreateAsset(font, FontPath);
            font.material.name = font.name + " Material"; AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures.Where(x => x != null)) { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); return font;
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 20; text.color = new Color32(240, 238, 220, 255);
            text.raycastTarget = false; text.richText = false; text.parseCtrlCharacters = false; return text;
        }
        static void EnsureFolder(string path)
        {
            var bits = path.Split('/'); string current = bits[0];
            for (int i = 1; i < bits.Length; i++) { string next = current + "/" + bits[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, bits[i]); current = next; }
        }
    }
}
