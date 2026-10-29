using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.UI.NotificationsV1;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Builder = CurtainCall.UI.NotificationsV1.Editor.SystemNotificationsBuilder;

namespace CurtainCall.Notifications.Editor
{
    public static class NotificationsProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_notifications_v1";
        public const string PrefabRoot = "Assets/Resources/Prefabs/UIs/Notifications";
        public const string ScenePath = "Assets/Scenes/Tests/Notifications/Notifications.unity";
        const string FontRoot = "Assets/Resources/Fonts/Notifications";
        static readonly string[] Names = { "SystemNotifications", "SystemBanner", "Toast" };
        static readonly string[] Labels = { "일반", "추락", "소집", "성공", "실패", "제목만", "아이콘 없음", "긴 문구", "제어 문자", "배너만", "토스트만", "교체", "재시작", "퇴장", "즉시 숨김", "시간 정지", "배경", "HUD 전환", "초기화" };
        [Serializable] class Plan { public Source[] assets; }
        [Serializable] class Source { public string key; public string relative_path; public int[] source_px; public float[] border_lr_tb_px; public bool @new; }

        [MenuItem("Tools/Curtain Call/알림 UI/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일·임포트를 마친 일반 씬 편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            foreach (string path in Names.Select(x => PrefabRoot + "/" + x + ".prefab").Concat(new[] { ScenePath }))
                if (File.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("기존 출력은 덮어쓰지 않습니다: " + path);
            var sources = JsonUtility.FromJson<Plan>(File.ReadAllText(Kit + "/Docs/notification_assembly.json")).assets;
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
                    if (importer == null) throw new InvalidOperationException("신규 importer 누락");
                    foreach (string platform in Platforms)
                        if (importer.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("신규 플랫폼 override 확인 필요: " + path);
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
            foreach (string weight in new[] { "Medium", "Bold" })
            {
                if (AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf") == null)
                    throw new InvalidOperationException("원본 폰트 누락: " + weight);
                string output = FontRoot + "/Pretendard-" + weight + " SDF.asset";
                var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(output);
                if ((File.Exists(output) || File.Exists(output + ".meta")) && (existing == null || existing.atlasPopulationMode != AtlasPopulationMode.Static || !existing.HasCharacters(characters)))
                    throw new InvalidOperationException("전용 폰트 충돌/글리프 부족");
            }
            var sprites = new Dictionary<string, Sprite>();
            foreach (var source in sources)
            {
                string path = Canonical(source); if (source.@new) ConfigureImporter(source, path);
                sprites.Add(source.key, ValidateSprite(source, path));
            }
            var medium = MakeFont("Medium", characters); var bold = MakeFont("Bold", characters);
            EnsureFolder(PrefabRoot); EnsureFolder("Assets/Scenes/Tests/Notifications");
            var original = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var views = new SystemNotificationsView[3];
                for (int i = 0; i < 3; i++)
                {
                    var go = Builder.BuildProject(i, medium, bold, sprites); go.name = Names[i];
                    go.GetComponent<Canvas>().sortingOrder = 5;
                    if (PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabRoot + "/" + Names[i] + ".prefab", InteractionMode.AutomatedAction) == null)
                        throw new IOException("프리팹 저장 실패: " + Names[i]);
                    views[i] = go.GetComponent<SystemNotificationsView>();
                }
                var performance = new GameObject("PerformanceContext");
                var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dependencies[1]), performance.transform);
                var cheer = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dependencies[2]), hud.transform);
                var backstage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dependencies[3]));
                backstage.name = "BackstageContext";
                foreach (var context in new[] { performance, backstage })
                {
                    foreach (var text in context.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (text.font == null || text.font.atlasPopulationMode != AtlasPopulationMode.Static)
                            text.font = text.font != null && text.font.name.Contains("Bold") ? bold : medium;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    }
                    foreach (var canvas in context.GetComponentsInChildren<Canvas>(true))
                    { canvas.sortingOrder = 0; PrefabUtility.RecordPrefabInstancePropertyModifications(canvas); }
                }
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(13, 23, 27, 255);
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                controls.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; controls.GetComponent<Canvas>().sortingOrder = 10;
                var scaler = controls.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var status = Text("Status", controls.transform, medium, new Vector2(48, 560), new Vector2(1824, 40));
                var preview = controls.AddComponent<NotificationsPreview>(); preview.Configure(views[0], views[1], views[2], status, camera, performance, backstage);
                UnityEngine.Events.UnityAction[] actions = { preview.Normal, preview.Danger, preview.Alarm, preview.Success, preview.Failure, preview.TitleOnly,
                    preview.NoIcon, preview.LongText, preview.ControlText, preview.BannerOnly, preview.ToastOnly, preview.Replace, preview.Restart,
                    preview.Hide, preview.HideImmediate, preview.TogglePause, preview.ToggleBackground, preview.ToggleContext, preview.ResetPreview };
                var buttons = new UnityEngine.UI.Button[Labels.Length];
                for (int i = 0; i < Labels.Length; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dependencies[0]), controls.transform);
                    go.name = "Sample_" + i; var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(48 + i % 7 * 224, 320 + i / 7 * 72); rect.sizeDelta = new Vector2(192, 56);
                    var label = go.GetComponentInChildren<TMP_Text>(true); label.text = Labels[i]; label.font = medium; label.fontSize = 22;
                    buttons[i] = go.GetComponent<UnityEngine.UI.Button>(); UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(label); PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                for (int i = 0; i < buttons.Length; i++)
                {
                    var nav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                        selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length], selectOnRight = buttons[(i + 1) % buttons.Length],
                        selectOnUp = buttons[(i + 7) % buttons.Length], selectOnDown = buttons[(i + buttons.Length - 7) % buttons.Length] };
                    buttons[i].navigation = nav; PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                preview.ResetPreview();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("알림 씬 저장 실패");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(original); }
            Debug.Log("C8 배너·토스트 프리팹3종과 합성 확인 씬 생성 완료");
        }
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps" };
        static string Canonical(Source source)
        {
            if (source.@new) return Kit + "/" + source.relative_path;
            string bundle = source.relative_path.Contains("/PerformanceHUD/") ? "curtaincall_performance_hud_v1" :
                source.relative_path.Contains("/BasicPanels/") ? "curtaincall_panels_v1" : "curtaincall_icons_v1";
            return "Assets/_ThirdParty/" + bundle + "/" + source.relative_path;
        }
        static Vector4 Border(Source s) => new Vector4(s.border_lr_tb_px[0], s.border_lr_tb_px[3], s.border_lr_tb_px[1], s.border_lr_tb_px[2]);
        static Sprite ValidateSprite(Source s, string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); var t = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || t == null || t.textureType != TextureImporterType.Sprite || t.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(sprite.pixelsPerUnit, 200) || sprite.border != Border(s) || sprite.rect.size != new Vector2(s.source_px[0], s.source_px[1]) ||
                sprite.texture.width != s.source_px[0] || sprite.texture.height != s.source_px[1] || t.mipmapEnabled || t.textureCompression != TextureImporterCompression.Uncompressed)
                throw new InvalidOperationException("Sprite 규격 불일치(정본 자동 수정 없음): " + path);
            foreach (string platform in Platforms) if (t.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override 확인 필요: " + path);
            return sprite;
        }
        static void ConfigureImporter(Source s, string path)
        {
            var t = (TextureImporter)AssetImporter.GetAtPath(path); t.textureType = TextureImporterType.Sprite; t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = 200; t.spriteBorder = Border(s); t.spritePivot = Vector2.one * .5f;
            var settings = new TextureImporterSettings(); t.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center; t.SetTextureSettings(settings);
            t.alphaSource = TextureImporterAlphaSource.FromInput; t.alphaIsTransparency = true; t.sRGBTexture = true; t.mipmapEnabled = false;
            t.isReadable = false; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.npotScale = TextureImporterNPOTScale.None;
            t.textureCompression = TextureImporterCompression.Uncompressed; t.crunchedCompression = false;
            t.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(s.source_px[0], s.source_px[1])); t.SaveAndReimport();
        }
        static string Characters()
        {
            string content = string.Join("", Labels) + "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz,:—/•…().-+<> =\\호응합성UI게임연결없음예시유지초샘플입력시간정지정상확인";
            content += File.ReadAllText("Assets/Scripts/Systems/Notifications/NotificationsPreview.cs");
            foreach (string path in Directory.GetFiles(Kit + "/Assets/CurtainCallUI/Notifications/Runtime", "*.cs")) content += File.ReadAllText(path);
            return new string(content.Where(c => (c >= ' ' && c <= '~') || (c >= '가' && c <= '힣') || "—•…".Contains(c)).Distinct().ToArray());
        }
        static TMP_FontAsset MakeFont(string weight, string characters)
        {
            var shared = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/BackstageHud/Pretendard-" + weight + " SDF.asset");
            if (shared != null && shared.atlasPopulationMode == AtlasPopulationMode.Static && shared.fallbackFontAssetTable.Count == 0 && shared.HasCharacters(characters)) return shared;
            EnsureFolder(FontRoot); string path = FontRoot + "/Pretendard-" + weight + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            font.name = "Pretendard-" + weight + " SDF";
            if (!font.TryAddCharacters(characters, out string missing)) throw new InvalidOperationException("폰트 글리프 부족: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; AssetDatabase.CreateAsset(font, path);
            font.material.name = font.name + " Material"; AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures.Where(x => x != null)) { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); return font;
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 20; text.color = new Color32(240, 238, 220, 255); text.raycastTarget = false;
            text.richText = false; return text;
        }
        static void EnsureFolder(string path)
        {
            var bits = path.Split('/'); string current = bits[0];
            for (int i = 1; i < bits.Length; i++) { string next = current + "/" + bits[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, bits[i]); current = next; }
        }
    }
}
