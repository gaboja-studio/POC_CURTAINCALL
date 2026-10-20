#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.UI.WorldLabelsV2;
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
using Builder = CurtainCall.UI.WorldLabelsV2.Editor.WorldLabelBuilder;

namespace CurtainCall.WorldLabels.Editor
{
    public static class WorldLabelProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_worldlabels_v2";
        public const string PrefabFolder = "Assets/Resources/Prefabs/UIs/WorldLabels";
        public const string ScenePath = "Assets/Scenes/Tests/WorldLabels/WorldLabels.unity";
        public const string FontFolder = "Assets/Resources/Fonts/WorldLabels";
        public const string MediumFontPath = FontFolder + "/Pretendard-Medium SDF.asset";
        public const string BoldFontPath = FontFolder + "/Pretendard-Bold SDF.asset";
        const string ButtonPath = "Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        public static readonly string[] Labels = { "내용 전환", "최소 크기", "기본 크기", "초과 거부", "무효 크기", "소품 표면 전환", "Observer 거리 순환", "Observer 해제", "Billboard 전환", "카메라 회전", "배경 전환", "초기화" };
        [Serializable] class Plan { public Source[] assets; }
        [Serializable] class Source { public string key; public string relative_path; public int[] source_px; public float[] border_lr_tb_px; public bool @new; public string sha256; }
        public static string PrefabPath(WorldLabelKind kind) => PrefabFolder + "/WorldLabel" + kind + ".prefab";
        static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;
        static string Canonical(Source source) => source.@new ? Kit + "/" + source.relative_path : "Assets/_ThirdParty/" + (source.key.StartsWith("plate_", StringComparison.Ordinal) ? "curtaincall_performance_hud_v1" : "curtaincall_icons_v1") + "/" + source.relative_path;
        static Vector2 Size(Source s) => new Vector2(s.source_px[0], s.source_px[1]);
        static Vector4 Border(Source s) => new Vector4(s.border_lr_tb_px[0], s.border_lr_tb_px[3], s.border_lr_tb_px[1], s.border_lr_tb_px[2]);

        [MenuItem("Tools/Curtain Call/월드 라벨 v2/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일·임포트를 마친 일반 씬 편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            foreach (string path in new[] { PrefabFolder, FontFolder, "Assets/Scenes/Tests/WorldLabels" })
                if (Exists(path)) throw new InvalidOperationException("기존 출력 폴더/파일은 재생성하지 않습니다: " + path);
            var buttonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
            if (buttonAsset == null || buttonAsset.GetComponent<Button>() == null || buttonAsset.GetComponent<RectTransform>() == null || buttonAsset.GetComponentInChildren<TMP_Text>(true) == null)
                throw new InvalidOperationException("공용 버튼 정본 누락 또는 규격 불일치");
            var sources = ReadSources();
            var mediumSource = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-Medium.ttf");
            var boldSource = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-Bold.ttf");
            if (mediumSource == null || boldSource == null) throw new InvalidOperationException("원본 Medium/Bold 폰트 누락");
            var sprites = new Dictionary<string, Sprite>();
            // 변경 전에 19종 원본 규격과 재사용 정본 7종의 해시를 모두 확인합니다.
            foreach (var source in sources)
            {
                string path = Canonical(source);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Importer 누락: " + path);
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (new Vector2(w, h) != Size(source) || !importer.DoesSourceTextureHaveAlpha()) throw new InvalidOperationException("PNG 크기/알파 불일치: " + path);
                foreach (string platform in Platforms) if (importer.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override 확인 필요: " + path);
                if (!source.@new)
                {
                    using var sha = SHA256.Create();
                    string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(hash, source.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("정본 SHA 불일치: " + path);
                    sprites.Add(source.key, ValidateSprite(source, path));
                }
            }
            string characters = Characters();
            Scene original = SceneManager.GetActiveScene(), created = default; bool saved = false;
            try
            {
                foreach (var source in sources.Where(s => s.@new))
                {
                    ConfigureNewImporter(source); sprites.Add(source.key, ValidateSprite(source, Canonical(source)));
                }
                var medium = MakeFont(mediumSource, characters, MediumFontPath);
                var bold = MakeFont(boldSource, characters, BoldFontPath);
                EnsureFolder(PrefabFolder); EnsureFolder("Assets/Scenes/Tests/WorldLabels");
                created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(created);
                var views = new WorldLabelView[5];
                Vector3[] positions = { new Vector3(-.95f,.48f,0), new Vector3(.05f,.48f,0), new Vector3(1.04f,.35f,0), new Vector3(-.4f,-.05f,0), new Vector3(.4f,-.05f,0) };
                for (int i = 0; i < views.Length; i++)
                {
                    var kind = (WorldLabelKind)i; var go = Builder.BuildProject(kind, medium, bold, sprites);
                    if (PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath(kind), InteractionMode.AutomatedAction) == null) throw new IOException("프리팹 저장 실패: " + kind);
                    views[i] = go.GetComponent<WorldLabelView>(); go.transform.position = positions[i];
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                }
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 1.15f; camera.transform.position = new Vector3(0,0,-5);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(14,24,28,255);
                var observer = new GameObject("ExplicitPreviewObserver").transform; observer.position = new Vector3(0,0,-3);
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                controls.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = controls.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
                var status = Text("Status", controls.transform, medium, new Vector2(32,-104), new Vector2(-64,88));
                status.rectTransform.anchorMax = new Vector2(1,1);
                var buttonRows = new GameObject("PreviewButtons", typeof(RectTransform)).GetComponent<RectTransform>();
                buttonRows.SetParent(controls.transform, false); buttonRows.anchorMin = new Vector2(0,1); buttonRows.anchorMax = new Vector2(1,1);
                buttonRows.pivot = new Vector2(.5f,1); buttonRows.anchoredPosition = new Vector2(0,-780); buttonRows.sizeDelta = new Vector2(-64,136);
                status.text = "월드 라벨 v2 · 독립 합성 검토 / 실게임 연결 없음 · Play에서 확인 · 크기/거리/scale은 제안값";
                var preview = controls.AddComponent<WorldLabelPreview>(); preview.Configure(views, status, observer, camera);
                UnityEngine.Events.UnityAction[] actions = { preview.Content, preview.MinimumSize, preview.DefaultSize, preview.OverflowRejection, preview.InvalidSize, preview.ToggleSurfaces,
                    preview.ObserverDistance, preview.ClearObserver, preview.ToggleBillboard, preview.RotateCamera, preview.ToggleBackground, preview.ResetPreview };
                var buttons = new Button[Labels.Length];
                for (int i = 0; i < Labels.Length; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(buttonAsset, buttonRows); go.name = "Sample_" + i;
                    var rect = go.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(i % 6 / 6f,1); rect.anchorMax = new Vector2((i % 6 + 1) / 6f,1); rect.pivot = new Vector2(.5f,1);
                    rect.offsetMin = new Vector2(8,-i / 6 * 76 - 60); rect.offsetMax = new Vector2(-8,-i / 6 * 76);
                    var label = go.GetComponentInChildren<TMP_Text>(true); label.font = medium; label.fontSize = 22; label.text = Labels[i];
                    label.enableAutoSizing = false; label.richText = false; label.parseCtrlCharacters = false;
                    buttons[i] = go.GetComponent<Button>(); UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(label); PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                for (int i = 0; i < buttons.Length; i++)
                {
                    int start = i / 6 * 6, end = start + 5;
                    buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = buttons[i == start ? end : i-1], selectOnRight = buttons[i == end ? start : i+1],
                        selectOnUp = buttons[i >= 6 ? i-6 : i], selectOnDown = buttons[i+6 < buttons.Length ? i+6 : i] };
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                if (!EditorSceneManager.SaveScene(created, ScenePath)) throw new IOException("확인 씬 저장 실패"); saved = true;
            }
            catch (Exception exception)
            {
                if (created.IsValid() && created.isLoaded && !saved)
                {
                    try { saved = EditorSceneManager.SaveScene(created, ScenePath); }
                    catch (Exception saveException) { Debug.LogError("부분 씬 저장 실패. 열린 씬을 보존합니다: " + saveException); }
                }
                Debug.LogError("월드 라벨 부분 출력·폰트·신규 importer를 보존합니다. 삭제/자동 재시도 없음: " + exception); throw;
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (saved && created.IsValid() && created.isLoaded) EditorSceneManager.CloseScene(created, true);
            }
        }
        static Source[] ReadSources()
        {
            var sources = JsonUtility.FromJson<Plan>(File.ReadAllText(Kit + "/Docs/worldlabels_assembly.json"))?.assets;
            var expected = new Dictionary<string, string>();
            foreach (string material in new[] { "wood", "cloth", "paper" }) foreach (string part in new[] { "shadow", "body", "frame", "corners" })
                expected.Add(material + "_" + part, "Assets/CurtainCallUI/WorldLabels/Sprites/ui_worldlabel_" + material + "_" + part + "_normal@2x.png");
            foreach (string part in new[] { "shadow", "body", "frame", "corners" }) expected.Add("plate_" + part, "Assets/CurtainCallUI/PerformanceHUD/Sprites/ui_hud_smallplate_" + part + "_normal@2x.png");
            foreach (string icon in new[] { "flag", "person", "tools" }) expected.Add("icon_" + icon, "Assets/CurtainCallUI/CommonIcons/Sprites/ui_icon_" + icon + "_normal@2x.png");
            if (sources == null || sources.Length != 19 || sources.Any(s => s == null || s.key == null) || !sources.Select(s => s.key).OrderBy(x => x).SequenceEqual(expected.Keys.OrderBy(x => x)))
                throw new InvalidOperationException("월드 라벨 19종 명세 불일치");
            foreach (var s in sources)
            {
                bool fresh = !s.key.StartsWith("plate_", StringComparison.Ordinal) && !s.key.StartsWith("icon_", StringComparison.Ordinal);
                int w = s.key.StartsWith("paper_", StringComparison.Ordinal) ? 384 : fresh ? 512 : s.key.StartsWith("plate_", StringComparison.Ordinal) ? 256 : 64;
                int h = w == 384 ? 320 : w == 512 ? 192 : w == 256 ? 112 : 64;
                int border = fresh ? 32 : w == 256 ? 24 : 0;
                if (s.@new != fresh || s.relative_path != expected[s.key] || s.source_px == null || s.source_px.Length != 2 || Size(s) != new Vector2(w,h) ||
                    s.border_lr_tb_px == null || s.border_lr_tb_px.Length != 4 || s.border_lr_tb_px.Any(b => b != border) || (!fresh && (s.sha256 == null || s.sha256.Length != 64)))
                    throw new InvalidOperationException("리소스 경로/신규 여부/크기/border 명세 불일치: " + s.key);
            }
            return sources;
        }
        static void ConfigureNewImporter(Source source)
        {
            if (!source.@new) throw new InvalidOperationException("정본 importer 변경 금지");
            var t = (TextureImporter)AssetImporter.GetAtPath(Canonical(source));
            t.textureType = TextureImporterType.Sprite; t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = 200; t.spriteBorder = Border(source); t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = t.sRGBTexture = true; t.mipmapEnabled = t.isReadable = false;
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; t.npotScale = TextureImporterNPOTScale.None;
            t.textureCompression = TextureImporterCompression.Uncompressed; t.crunchedCompression = false; t.maxTextureSize = 512;
            var settings = new TextureImporterSettings(); t.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = Vector2.one * .5f; t.SetTextureSettings(settings); t.SaveAndReimport();
        }
        static Sprite ValidateSprite(Source source, string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); var t = AssetImporter.GetAtPath(path) as TextureImporter;
            var settings = new TextureImporterSettings(); if (t != null) t.ReadTextureSettings(settings);
            if (t == null) throw new InvalidOperationException("Importer 누락: " + path);
            if (s == null || s.rect.size != Size(source) || new Vector2(s.texture.width,s.texture.height) != Size(source) || s.border != Border(source) || s.pivot != Size(source)/2 || s.pixelsPerUnit != 200 ||
                (s.texture.format != TextureFormat.RGBA32 && s.texture.format != TextureFormat.ARGB32 && s.texture.format != TextureFormat.BGRA32) ||
                t.textureType != TextureImporterType.Sprite || t.spriteImportMode != SpriteImportMode.Single || t.mipmapEnabled || t.isReadable || !t.sRGBTexture || !t.alphaIsTransparency ||
                t.alphaSource != TextureImporterAlphaSource.FromInput || t.wrapMode != TextureWrapMode.Clamp || t.filterMode != FilterMode.Bilinear || t.npotScale != TextureImporterNPOTScale.None ||
                settings.spriteMeshType != SpriteMeshType.FullRect || t.textureCompression != TextureImporterCompression.Uncompressed || t.crunchedCompression)
                throw new InvalidOperationException("Sprite 규격 불일치: " + path);
            return s;
        }
        static string Characters()
        {
            string content = new string(Enumerable.Range(32,95).Select(c => (char)c).ToArray()) + string.Join("",Labels) +
                File.ReadAllText("Assets/Scripts/Systems/WorldLabels/WorldLabelPreview.cs") +
                File.ReadAllText(Kit + "/Assets/CurtainCallUI/WorldLabels/Runtime/WorldLabelView.cs") + "월드 라벨 독립 합성 검토 실게임 연결 없음 크기 거리 제안값 Play에서 확인";
            return new string(content.Where(c => !char.IsControl(c) && !char.IsSurrogate(c) && c != (char)0x2028 && c != (char)0x2029).Distinct().ToArray());
        }
        static TMP_FontAsset MakeFont(Font source, string characters, string path)
        {
            EnsureFolder(FontFolder);
            var font = TMP_FontAsset.CreateFontAsset(source,90,9,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
            if (font == null) throw new InvalidOperationException("전용 SDF 생성 실패");
            font.name = "WorldLabels " + source.name + " SDF"; AssetDatabase.CreateAsset(font,path);
            font.material.name = font.name + " Material"; AssetDatabase.AddObjectToAsset(font.material,font);
            foreach (var atlas in font.atlasTextures.Where(x => x != null)) { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas,font); }
            bool added = font.TryAddCharacters(characters,out string missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; font.isMultiAtlasTexturesEnabled = false; font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            EditorUtility.SetDirty(font); foreach (var atlas in font.atlasTextures.Where(x => x != null)) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssetIfDirty(font);
            if (!added || !font.HasCharacters(characters)) throw new InvalidOperationException("전용 글리프 부족: " + missing);
            return font;
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 24; text.color = new Color32(240,238,220,255);
            text.richText = false; text.parseCtrlCharacters = false; text.raycastTarget = false; return text;
        }
        static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); string current = parts[0];
            for (int i=1;i<parts.Length;i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current,parts[i]))) throw new IOException("폴더 생성 실패: " + next); current=next; }
        }
    }
}
#endif
