using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.UI.InteractionV1;
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
using Builder = CurtainCall.UI.InteractionV1.Editor.InteractionPromptBuilder;

namespace CurtainCall.Interaction.Editor
{
    public static class InteractionProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_interaction_v1";
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/Interaction/InteractionPrompt.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/Interaction/Interaction.unity";
        const string FontPath = "Assets/Resources/Fonts/Interaction/Pretendard-Medium SDF.asset";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        static readonly string[] Labels = { "일반 가능", "길게 가능", "일반 불가", "길게 불가", "진행 0%", "진행 25%", "진행 50%", "진행 65%", "진행 75%", "진행 100%", "새 대상", "반복 Show", "긴 문구", "제어 문자", "기본 보조", "보조 있음", "퇴장", "즉시 숨김", "시간 정지", "배경", "HUD 전환", "초기화" };
        [Serializable] class Plan { public Source[] assets; }
        [Serializable] class Source { public string key; public string relative_path; public int[] source_px; public float[] border_lr_tb_px; public bool @new; }

        [MenuItem("Tools/Curtain Call/C6 상호작용 UI/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일·임포트를 마친 일반 씬 편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            foreach (string path in new[] { PrefabPath, ScenePath })
                if (File.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("기존 출력은 덮어쓰지 않습니다: " + path);
            var sources = JsonUtility.FromJson<Plan>(File.ReadAllText(Kit + "/Docs/interaction_assembly.json")).assets;
            foreach (var source in sources)
            {
                string path = Canonical(source);
                if (!File.Exists(path)) throw new InvalidOperationException("Sprite 누락: " + path);
                if (!source.@new)
                {
                    using var sha = SHA256.Create();
                    if (!sha.ComputeHash(File.ReadAllBytes(path)).SequenceEqual(sha.ComputeHash(File.ReadAllBytes(Kit + "/" + source.relative_path))))
                        throw new InvalidOperationException("정본 PNG 불일치: " + source.key);
                    ValidateSprite(source, path);
                }
                else
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("신규 importer 누락: " + path);
                    foreach (string platform in Platforms)
                        if (importer.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override 확인 필요: " + path);
                }
            }
            string[] dependencies = {
                "Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab",
                "Assets/Resources/Prefabs/UIs/PerformanceHud/PerformanceHud.prefab",
                "Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab",
                "Assets/Resources/Prefabs/UIs/BackstageHud/BackstageHud.prefab" };
            foreach (string path in dependencies)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new InvalidOperationException("의존 프리팹 누락: " + path);
            string characters = Characters();
            var shared = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Notifications/Pretendard-Medium SDF.asset");
            bool reuse = SafeFont(shared, characters);
            if (!reuse && (File.Exists(FontPath) || File.Exists(FontPath + ".meta")))
            {
                shared = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (!SafeFont(shared, characters)) throw new InvalidOperationException("전용 폰트 출력 충돌: " + FontPath);
                reuse = true;
            }
            if (!reuse && AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-Medium.ttf") == null)
                throw new InvalidOperationException("원본 Medium 폰트 누락");
            var sprites = new Dictionary<string, Sprite>();
            foreach (var source in sources)
            { string path = Canonical(source); if (source.@new) ConfigureImporter(source, path); sprites.Add(source.key, ValidateSprite(source, path)); }
            var medium = reuse ? shared : MakeFont(characters);
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Notifications/Pretendard-Bold SDF.asset");
            if (!SafeFont(bold, "호응0123456789")) bold = medium;
            EnsureFolder("Assets/Resources/Prefabs/UIs/Interaction"); EnsureFolder("Assets/Scenes/Tests/Interaction");
            var original = EditorSceneManager.GetSceneManagerSetup();
            bool completed = false;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var go = Builder.BuildProject(medium, sprites);
                if (PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction) == null)
                    throw new IOException("C6 프리팹 저장 실패");
                var view = go.GetComponentInChildren<InteractionPromptView>(true);
                var performance = new GameObject("PerformanceContext");
                var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dependencies[1]), performance.transform);
                var cheer = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dependencies[2]), hud.transform);
                foreach (var canvas in cheer.GetComponentsInChildren<Canvas>(true))
                {
                    var rect = canvas.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero; PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                }
                var backstage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dependencies[3])); backstage.name = "BackstageContext";
                foreach (var context in new[] { performance, backstage })
                {
                    foreach (var text in context.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (SafeFont(text.font, "")) continue;
                        text.font = text.font != null && text.font.name.Contains("Bold") ? bold : medium;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    }
                    foreach (var canvas in context.GetComponentsInChildren<Canvas>(true)) { canvas.sortingOrder = 0; PrefabUtility.RecordPrefabInstancePropertyModifications(canvas); }
                    foreach (var graphic in context.GetComponentsInChildren<Graphic>(true)) { graphic.raycastTarget = false; PrefabUtility.RecordPrefabInstancePropertyModifications(graphic); }
                    foreach (var group in context.GetComponentsInChildren<CanvasGroup>(true))
                    { group.interactable = false; group.blocksRaycasts = false; PrefabUtility.RecordPrefabInstancePropertyModifications(group); }
                    foreach (var raycaster in context.GetComponentsInChildren<GraphicRaycaster>(true))
                    { raycaster.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(raycaster); }
                }
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var controlsCanvas = controls.GetComponent<Canvas>(); controlsCanvas.renderMode = RenderMode.ScreenSpaceOverlay; controlsCanvas.sortingOrder = 10;
                var scaler = controls.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var status = Text("Status", controls.transform, medium, new Vector2(48, 544), new Vector2(1824, 32));
                var preview = controls.AddComponent<InteractionPreview>(); preview.Configure(view, status, camera, performance, backstage);
                UnityEngine.Events.UnityAction[] actions = { preview.Tap, preview.Hold, preview.Unavailable, preview.UnavailableHold,
                    preview.Progress0, preview.Progress25, preview.Progress50, preview.Progress65, preview.Progress75, preview.Progress100,
                    preview.NewTarget, preview.RepeatShow, preview.LongText, preview.ControlText, preview.DefaultHelper, preview.CustomHelper,
                    preview.Hide, preview.HideImmediate, preview.TogglePause, preview.ToggleBackground, preview.ToggleContext, preview.ResetPreview };
                var buttons = new Button[Labels.Length];
                for (int i = 0; i < Labels.Length; i++)
                {
                    var button = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dependencies[0]), controls.transform);
                    button.name = "Sample_" + i; var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    int column = i % 6; rect.anchorMin = rect.anchorMax = column < 3 ? Vector2.zero : Vector2.right;
                    rect.anchoredPosition = new Vector2(column < 3 ? 32 + column * 212 : -648 + (column - 3) * 212, 256 + i / 6 * 64); rect.sizeDelta = new Vector2(192, 56);
                    var label = button.GetComponentInChildren<TMP_Text>(true); label.text = Labels[i]; label.font = medium; label.fontSize = 22;
                    buttons[i] = button.GetComponent<Button>(); UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(label); PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                        selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length], selectOnRight = buttons[(i + 1) % buttons.Length],
                        selectOnUp = buttons[(i + 6) % buttons.Length], selectOnDown = buttons[(i + buttons.Length - 6) % buttons.Length] };
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                preview.ResetPreview();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("C6 씬 저장 실패");
                completed = true;
            }
            catch (Exception exception)
            { Debug.LogError("C6 부분 결과를 보존합니다. 출력·추가 씬·importer 상태를 확인하세요: " + exception); throw; }
            finally { if (completed) EditorSceneManager.RestoreSceneManagerSetup(original); }
            Debug.Log("C6 프리팹1종·독립 합성 확인 씬 생성 완료");
        }
        static string Canonical(Source source) => "Assets/_ThirdParty/" + (source.@new ? "curtaincall_interaction_v1/" : "curtaincall_performance_hud_v1/") + source.relative_path;
        static Vector4 Border(Source s) => new Vector4(s.border_lr_tb_px[0], s.border_lr_tb_px[3], s.border_lr_tb_px[1], s.border_lr_tb_px[2]);
        static Sprite ValidateSprite(Source s, string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); var t = AssetImporter.GetAtPath(path) as TextureImporter;
            var settings = new TextureImporterSettings(); if (t != null) t.ReadTextureSettings(settings);
            if (sprite == null || t == null || t.textureType != TextureImporterType.Sprite || t.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(sprite.pixelsPerUnit, 200) || sprite.border != Border(s) || sprite.rect.size != new Vector2(s.source_px[0], s.source_px[1]) ||
                sprite.pivot != new Vector2(s.source_px[0] / 2f, s.source_px[1] / 2f) || sprite.texture.width != s.source_px[0] || sprite.texture.height != s.source_px[1] ||
                t.mipmapEnabled || t.isReadable || !t.sRGBTexture || !t.alphaIsTransparency || t.wrapMode != TextureWrapMode.Clamp || t.filterMode != FilterMode.Bilinear ||
                t.npotScale != TextureImporterNPOTScale.None || settings.spriteMeshType != SpriteMeshType.FullRect ||
                t.textureCompression != TextureImporterCompression.Uncompressed ||
                (sprite.texture.format != TextureFormat.RGBA32 && sprite.texture.format != TextureFormat.ARGB32 && sprite.texture.format != TextureFormat.BGRA32))
                throw new InvalidOperationException("Sprite 규격 불일치(정본 자동 수정 없음): " + path);
            foreach (string platform in Platforms) if (t.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override 확인 필요: " + path);
            return sprite;
        }
        static void ConfigureImporter(Source s, string path)
        {
            var t = (TextureImporter)AssetImporter.GetAtPath(path); t.textureType = TextureImporterType.Sprite; t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = 200; t.spriteBorder = Border(s); var settings = new TextureImporterSettings(); t.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = Vector2.one * .5f; t.SetTextureSettings(settings);
            t.alphaSource = TextureImporterAlphaSource.FromInput; t.alphaIsTransparency = true; t.sRGBTexture = true; t.mipmapEnabled = false; t.isReadable = false;
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.npotScale = TextureImporterNPOTScale.None;
            t.textureCompression = TextureImporterCompression.Uncompressed; t.crunchedCompression = false; t.maxTextureSize = 128; t.SaveAndReimport();
        }
        static string Characters()
        {
            string content = string.Join("", Labels) + File.ReadAllText("Assets/Scripts/Systems/Interaction/InteractionPreview.cs") +
                File.ReadAllText(Kit + "/Assets/CurtainCallUI/Interaction/Runtime/InteractionPromptView.cs") + "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz…—•합성표시게임연결없음호응";
            return new string(content.Where(c => (c >= ' ' && c <= '~') || (c >= '가' && c <= '힣') || "…—•".Contains(c)).Distinct().ToArray());
        }
        static bool SafeFont(TMP_FontAsset font, string characters) => font != null && font.atlasPopulationMode == AtlasPopulationMode.Static && (font.fallbackFontAssetTable == null || font.fallbackFontAssetTable.Count == 0) && font.HasCharacters(characters);
        static TMP_FontAsset MakeFont(string characters)
        {
            EnsureFolder("Assets/Resources/Fonts/Interaction");
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-Medium.ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            font.name = "Pretendard-Medium SDF";
            if (!font.TryAddCharacters(characters, out string missing)) throw new InvalidOperationException("폰트 글리프 부족: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; AssetDatabase.CreateAsset(font, FontPath);
            font.material.name = font.name + " Material"; AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures.Where(x => x != null)) { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); return font;
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 20; text.color = new Color32(240, 238, 220, 255);
            text.raycastTarget = false; text.richText = false; return text;
        }
        static void EnsureFolder(string path)
        {
            var bits = path.Split('/'); string current = bits[0];
            for (int i = 1; i < bits.Length; i++) { string next = current + "/" + bits[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, bits[i]); current = next; }
        }
    }
}
