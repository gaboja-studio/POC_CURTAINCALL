using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.UI.CaptionsV1;
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
using Builder = CurtainCall.UI.CaptionsV1.Editor.CaptionBuilder;

namespace CurtainCall.Captions.Editor
{
    public static class CaptionProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_captions_v1";
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/Captions/Captions.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/Captions/Captions.unity";
        public const string FontPath = "Assets/Resources/Fonts/Captions/Pretendard-Medium SDF.asset";
        const string ButtonPath = "Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab";
        const string SourceFontPath = Kit + "/Source/Fonts/Pretendard-Medium.ttf";
        static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Web", "Windows Store Apps", "tvOS" };
        public static readonly string[] Labels = { "대사", "화자 없음", "방송", "기본 방송 화자", "긴 화자명", "명시적 두 줄", "본문 초과 거부", "빈 본문 거부", "수용 검사", "폭 범위", "무효 폭", "퇴장", "즉시 숨김", "역전", "반복", "문자 그대로", "시간 정지·복구", "배경 전환", "초기화" };
        static readonly string[] Keys = { "banner_shadow", "banner_body", "banner_frame", "banner_corners", "plate_shadow", "plate_body", "plate_frame", "plate_corners", "icon_megaphone" };
        [Serializable] class Plan { public Source[] assets; }
        [Serializable] class Source { public string key; public string relative_path; public int[] source_px; public float[] border_lr_tb_px; public bool @new; public string sha256; }
        static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;
        static string Canonical(Source source) => "Assets/_ThirdParty/" + (source.key.StartsWith("banner_", StringComparison.Ordinal) ? "curtaincall_notifications_v1" : source.key.StartsWith("plate_", StringComparison.Ordinal) ? "curtaincall_performance_hud_v1" : "curtaincall_icons_v1") + "/" + source.relative_path;
        static Vector2 Size(Source s) => new Vector2(s.source_px[0], s.source_px[1]);
        static Vector4 Border(Source s) => new Vector4(s.border_lr_tb_px[0], s.border_lr_tb_px[3], s.border_lr_tb_px[1], s.border_lr_tb_px[2]);

        [MenuItem("Tools/Curtain Call/C9 자막·방송/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일·임포트를 마친 일반 씬 편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            foreach (string path in new[] { PrefabPath, ScenePath, FontPath }) if (Exists(path)) throw new InvalidOperationException("기존 출력은 덮어쓰지 않습니다: " + path);
            var buttonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
            if (buttonAsset == null || buttonAsset.GetComponent<Button>() == null || buttonAsset.GetComponent<RectTransform>() == null || buttonAsset.GetComponentInChildren<TMP_Text>(true) == null)
                throw new InvalidOperationException("공용 버튼 정본 누락 또는 규격 불일치");
            var sources = JsonUtility.FromJson<Plan>(File.ReadAllText(Kit + "/Docs/captions_assembly.json"))?.assets;
            if (sources == null || sources.Length != 9 || sources.Any(s => s == null || s.@new || s.source_px == null || s.source_px.Length != 2 || s.border_lr_tb_px == null || s.border_lr_tb_px.Length != 4) || !sources.Select(s => s.key).OrderBy(x => x).SequenceEqual(Keys.OrderBy(x => x)))
                throw new InvalidOperationException("C9 재사용 리소스 명세 불일치");
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null) throw new InvalidOperationException("원본 Medium 폰트 누락");
            var sprites = new Dictionary<string, Sprite>();
            // 모든 정본 검사는 출력 생성 전에 읽기 전용으로 끝냅니다.
            foreach (var source in sources)
            {
                string path = Canonical(source);
                using var sha = SHA256.Create();
                string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(hash, source.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("정본 SHA 불일치: " + path);
                sprites.Add(source.key, ValidateSprite(source, path));
            }
            string characters = Characters();
            Scene original = SceneManager.GetActiveScene(), created = default;
            bool saved = false;
            try
            {
                var medium = MakeFont(sourceFont, characters);
                EnsureFolder("Assets/Resources/Prefabs/UIs/Captions"); EnsureFolder("Assets/Scenes/Tests/Captions");
                created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(created);
                var go = Builder.BuildProject(medium, sprites);
                if (PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction) == null) throw new IOException("자막 프리팹 저장 실패");
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = controls.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
                var scaler = controls.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var status = Text("Status", controls.transform, medium, new Vector2(32, -24), new Vector2(1856, 56));
                var preview = controls.AddComponent<CaptionPreview>(); preview.Configure(go.GetComponentInChildren<CaptionView>(true), status, camera);
                UnityEngine.Events.UnityAction[] actions = { preview.Dialogue, preview.NoSpeaker, preview.Broadcast, preview.DefaultSpeaker, preview.LongName, preview.ExplicitTwoLines,
                    preview.OverflowRejection, preview.EmptyRejection, preview.CanFit, preview.WidthRange, preview.InvalidWidth, preview.Hide, preview.HideImmediate,
                    preview.Reverse, preview.Repeat, preview.Literals, preview.TogglePause, preview.ToggleBackground, preview.ResetPreview };
                if (actions.Length != Labels.Length) throw new InvalidOperationException("샘플 버튼과 메서드 불일치");
                var buttons = new Button[Labels.Length];
                for (int i = 0; i < Labels.Length; i++)
                {
                    var button = (GameObject)PrefabUtility.InstantiatePrefab(buttonAsset, controls.transform); button.name = "Sample_" + i;
                    var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(32 + i % 5 * 248, -104 - i / 5 * 72); rect.sizeDelta = new Vector2(232, 56);
                    var label = button.GetComponentInChildren<TMP_Text>(true); label.text = Labels[i]; label.font = medium; label.fontSize = 22;
                    label.enableAutoSizing = false; label.richText = false; label.parseCtrlCharacters = false;
                    buttons[i] = button.GetComponent<Button>(); UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(label); PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                for (int i = 0; i < buttons.Length; i++)
                {
                    int start = i / 5 * 5, end = Mathf.Min(start + 5, buttons.Length) - 1;
                    buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = buttons[i == start ? end : i - 1], selectOnRight = buttons[i == end ? start : i + 1],
                        selectOnUp = buttons[i >= 5 ? i - 5 : i], selectOnDown = buttons[i + 5 < buttons.Length ? i + 5 : i] };
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                if (!go.GetComponentInChildren<CaptionView>(true).TryShowEditImmediate(CaptionKind.Broadcast, "단장 방송", CaptionPreview.BroadcastBody))
                    throw new InvalidOperationException("편집 자막 예시 표시 실패");
                status.text = "C9 · 합성 예시 / 게임 연결 없음 · Play에서 버튼을 눌러 확인하세요.";
                camera.backgroundColor = new Color32(14, 24, 28, 255);
                if (!EditorSceneManager.SaveScene(created, ScenePath)) throw new IOException("자막 확인 씬 저장 실패");
                saved = true;
            }
            catch (Exception exception)
            {
                // 삭제하지 않고 이번 소유 출력과 부분 씬을 남겨 진단할 수 있게 합니다.
                if (created.IsValid() && created.isLoaded && !saved)
                {
                    try { saved = EditorSceneManager.SaveScene(created, ScenePath); }
                    catch (Exception saveException) { Debug.LogError("부분 씬 저장 실패. 열린 씬을 보존합니다: " + saveException); }
                }
                Debug.LogError("C9 부분 출력·폰트를 보존합니다: " + exception); throw;
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (saved && created.IsValid() && created.isLoaded) EditorSceneManager.CloseScene(created, true);
            }
        }
        static Sprite ValidateSprite(Source source, string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); var t = AssetImporter.GetAtPath(path) as TextureImporter;
            var settings = new TextureImporterSettings(); if (t != null) t.ReadTextureSettings(settings);
            if (t == null) throw new InvalidOperationException("Importer 누락: " + path);
            t.GetSourceTextureWidthAndHeight(out int w, out int h);
            if (new Vector2(w, h) != Size(source) || !t.DoesSourceTextureHaveAlpha() || s == null || s.rect.size != Size(source) || new Vector2(s.texture.width, s.texture.height) != Size(source) ||
                s.border != Border(source) || s.pivot != Size(source) / 2 || s.pixelsPerUnit != 200 ||
                (s.texture.format != TextureFormat.RGBA32 && s.texture.format != TextureFormat.ARGB32 && s.texture.format != TextureFormat.BGRA32) ||
                t.textureType != TextureImporterType.Sprite || t.spriteImportMode != SpriteImportMode.Single || t.mipmapEnabled || t.isReadable || !t.sRGBTexture || !t.alphaIsTransparency ||
                t.alphaSource != TextureImporterAlphaSource.FromInput || t.wrapMode != TextureWrapMode.Clamp || t.filterMode != FilterMode.Bilinear || t.npotScale != TextureImporterNPOTScale.None ||
                settings.spriteMeshType != SpriteMeshType.FullRect || t.textureCompression != TextureImporterCompression.Uncompressed || t.crunchedCompression)
                throw new InvalidOperationException("정본 Sprite 규격 불일치: " + path);
            foreach (string platform in Platforms) if (t.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override 확인 필요: " + path);
            return s;
        }
        static string Characters()
        {
            string content = string.Join("", Labels) + File.ReadAllText("Assets/Scripts/Systems/Captions/CaptionPreview.cs") +
                File.ReadAllText(Kit + "/Assets/CurtainCallUI/Captions/Runtime/CaptionView.cs") +
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz…—•·가방송단장단원공연 준비가 끝났습니다.단원들은 무대로 모여 주십시오.첫 번째 줄입니다.두 번째 줄입니다.";
            return new string(content.Where(c => !char.IsControl(c) && !char.IsSurrogate(c) && c != (char)0x2028 && c != (char)0x2029).Distinct().ToArray());
        }
        static TMP_FontAsset MakeFont(Font source, string characters)
        {
            EnsureFolder("Assets/Resources/Fonts/Captions");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            if (font == null) throw new InvalidOperationException("C9 폰트 생성 실패");
            font.name = "Captions Pretendard-Medium SDF";
            // 먼저 전용 경로에 보존하므로 글리프 생성이 실패해도 부분 결과를 삭제하지 않습니다.
            AssetDatabase.CreateAsset(font, FontPath);
            font.material.name = font.name + " Material"; AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures.Where(x => x != null)) { atlas.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, font); }
            bool added = font.TryAddCharacters(characters, out string missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static; font.isMultiAtlasTexturesEnabled = false; font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            EditorUtility.SetDirty(font); foreach (var atlas in font.atlasTextures.Where(x => x != null)) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssetIfDirty(font);
            if (!added || !font.HasCharacters(characters)) throw new InvalidOperationException("C9 폰트 글리프 부족: " + missing);
            return font;
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 20; text.color = new Color32(240, 238, 220, 255);
            text.raycastTarget = false; text.richText = false; text.parseCtrlCharacters = false; return text;
        }
        static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }
    }
}
