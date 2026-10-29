using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.UI.TooltipsV1;
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
using Builder = CurtainCall.UI.TooltipsV1.Editor.TooltipBuilder;

namespace CurtainCall.Tooltips.Editor
{
    public static class TooltipProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_tooltips_v1";
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/Tooltips/Tooltips.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/Tooltips/Tooltips.unity";
        const string FontFolder = "Assets/Resources/Fonts/Tooltips";
        const string ButtonPath = "Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        public static readonly string[] Keys = { "plate_shadow", "plate_body", "plate_frame", "plate_corners", "bottom_edge", "tail_body", "tail_frame" };
        static readonly string[] Labels = { "근접", "패널 품목", "좁게·긴 글", "넓게·긴 글", "패널 긴 글", "최소 폭", "최대 폭", "꼬리 왼쪽", "꼬리 오른쪽", "꼬리 중앙", "위치 이동", "꼬리 이동", "반복 Show", "제어 문자", "문자 그대로", "퇴장", "즉시 숨김", "시간 정지", "배경", "초기화" };
        public static string SpritePath(string key) => Kit + "/Assets/CurtainCallUI/Tooltips/Sprites/ui_tooltip_" + key + "_normal@2x.png";
        static Vector2 Size(int index) => index < 4 ? new Vector2(256, 192) : index == 4 ? new Vector2(32, 8) : new Vector2(32, 24);
        static Vector4 Border(int index) => index < 4 ? Vector4.one * 24 : Vector4.zero;

        [MenuItem("Tools/Curtain Call/C15 툴팁/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일·임포트를 마친 일반 씬 편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            foreach (string path in new[] { PrefabPath, ScenePath })
                if (File.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("기존 출력은 덮어쓰지 않습니다: " + path);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath) == null) throw new InvalidOperationException("버튼 정본 누락");
            string characters = Characters();
            foreach (string weight in new[] { "Medium", "SemiBold" })
            {
                string path = FontFolder + "/Pretendard-" + weight + " SDF.asset";
                if (File.Exists(path) || File.Exists(path + ".meta"))
                {
                    if (!SafeFont(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path), characters)) throw new InvalidOperationException("전용 폰트 출력 충돌: " + path);
                }
                else if (AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf") == null) throw new InvalidOperationException("원본 폰트 누락: " + weight);
            }
            for (int i = 0; i < Keys.Length; i++)
            {
                var importer = AssetImporter.GetAtPath(SpritePath(Keys[i])) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("신규 importer 누락: " + Keys[i]);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (new Vector2(width, height) != Size(i) || !importer.DoesSourceTextureHaveAlpha()) throw new InvalidOperationException("원본 크기/알파 불일치: " + Keys[i]);
                foreach (string platform in Platforms) if (importer.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override 확인 필요: " + Keys[i]);
            }
            var sprites = new Dictionary<string, Sprite>();
            for (int i = 0; i < Keys.Length; i++)
            {
                string path = SpritePath(Keys[i]); var t = (TextureImporter)AssetImporter.GetAtPath(path);
                t.textureType = TextureImporterType.Sprite; t.spriteImportMode = SpriteImportMode.Single; t.spritePixelsPerUnit = 200; t.spriteBorder = Border(i);
                var settings = new TextureImporterSettings(); t.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = Vector2.one * .5f; t.SetTextureSettings(settings);
                t.alphaSource = TextureImporterAlphaSource.FromInput; t.alphaIsTransparency = true; t.sRGBTexture = true; t.mipmapEnabled = false; t.isReadable = false;
                t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.npotScale = TextureImporterNPOTScale.None;
                t.textureCompression = TextureImporterCompression.Uncompressed; t.crunchedCompression = false; t.maxTextureSize = i < 4 ? 256 : 32; t.SaveAndReimport();
                sprites.Add(Keys[i], ValidateSprite(i));
            }
            EnsureFolder(FontFolder); var medium = FontAsset("Medium", characters); var semiBold = FontAsset("SemiBold", characters);
            EnsureFolder("Assets/Resources/Prefabs/UIs/Tooltips"); EnsureFolder("Assets/Scenes/Tests/Tooltips");
            var original = EditorSceneManager.GetSceneManagerSetup(); bool completed = false;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var go = Builder.BuildProject(medium, semiBold, sprites);
                if (PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction) == null) throw new IOException("툴팁 프리팹 저장 실패");
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = controls.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
                var scaler = controls.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var status = Text("Status", controls.transform, medium, new Vector2(48, 320), new Vector2(1824, 40));
                var preview = controls.AddComponent<TooltipPreview>(); preview.Configure(go.GetComponentInChildren<TooltipView>(true), status, camera);
                UnityEngine.Events.UnityAction[] actions = { preview.Nearby, preview.Panel, preview.Narrow, preview.Wide, preview.PanelLong,
                    preview.WidthLow, preview.WidthHigh, preview.TailLeft, preview.TailRight, preview.TailCenter, preview.Move, preview.SetTail,
                    preview.RepeatShow, preview.Controls, preview.Literal, preview.Hide, preview.HideImmediate, preview.TogglePause, preview.ToggleBackground, preview.ResetPreview };
                var buttons = new Button[Labels.Length];
                for (int i = 0; i < Labels.Length; i++)
                {
                    var button = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath), controls.transform);
                    button.name = "Sample_" + i; var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(32 + i % 5 * 212, 32 + i / 5 * 64); rect.sizeDelta = new Vector2(192, 56);
                    var label = button.GetComponentInChildren<TMP_Text>(true); label.text = Labels[i]; label.font = medium; label.fontSize = 22;
                    buttons[i] = button.GetComponent<Button>(); UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(label); PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = buttons[(i + 19) % 20], selectOnRight = buttons[(i + 1) % 20], selectOnUp = buttons[(i + 5) % 20], selectOnDown = buttons[(i + 15) % 20] };
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                preview.ResetPreview();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("툴팁 씬 저장 실패"); completed = true;
            }
            catch (Exception exception) { Debug.LogError("툴팁 부분 결과·씬·폰트·importer를 보존합니다: " + exception); throw; }
            finally { if (completed) EditorSceneManager.RestoreSceneManagerSetup(original); }
        }
        public static Sprite ValidateSprite(int index)
        {
            string path = SpritePath(Keys[index]); var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); var t = AssetImporter.GetAtPath(path) as TextureImporter;
            var settings = new TextureImporterSettings(); if (t != null) t.ReadTextureSettings(settings);
            if (s == null || t == null || s.rect.size != Size(index) || new Vector2(s.texture.width, s.texture.height) != Size(index) ||
                s.border != Border(index) || s.pivot != Size(index) / 2 || s.pixelsPerUnit != 200 || s.texture.format != TextureFormat.RGBA32 ||
                t.textureType != TextureImporterType.Sprite || t.spriteImportMode != SpriteImportMode.Single || t.mipmapEnabled || t.isReadable ||
                !t.sRGBTexture || !t.alphaIsTransparency || t.wrapMode != TextureWrapMode.Clamp || t.filterMode != FilterMode.Bilinear ||
                t.npotScale != TextureImporterNPOTScale.None || settings.spriteMeshType != SpriteMeshType.FullRect || t.textureCompression != TextureImporterCompression.Uncompressed)
                throw new InvalidOperationException("Sprite 규격 불일치: " + path);
            foreach (string platform in Platforms) if (t.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override: " + path);
            return s;
        }
        static string Characters()
        {
            string content = string.Join("", Labels) + File.ReadAllText("Assets/Scripts/Systems/Tooltips/TooltipPreview.cs") +
                File.ReadAllText(Kit + "/Assets/CurtainCallUI/Tooltips/Runtime/TooltipView.cs") + "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz…—•합성표시게임연결없음미설정시간정지";
            return new string(content.Where(c => (c >= ' ' && c <= '~') || (c >= '가' && c <= '힣') || "…—•".Contains(c)).Distinct().ToArray());
        }
        static bool SafeFont(TMP_FontAsset font, string characters) => font != null && font.atlasPopulationMode == AtlasPopulationMode.Static &&
            (font.fallbackFontAssetTable == null || font.fallbackFontAssetTable.Count == 0) && font.HasCharacters(characters);
        static TMP_FontAsset FontAsset(string weight, string characters)
        {
            string path = FontFolder + "/Pretendard-" + weight + " SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (SafeFont(font, characters)) return font;
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf");
            font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
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
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 20; text.color = new Color32(240, 238, 220, 255); text.raycastTarget = false; text.richText = false; return text;
        }
        static void EnsureFolder(string path)
        {
            var bits = path.Split('/'); string current = bits[0];
            for (int i = 1; i < bits.Length; i++) { string next = current + "/" + bits[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, bits[i]); current = next; }
        }
    }
}
