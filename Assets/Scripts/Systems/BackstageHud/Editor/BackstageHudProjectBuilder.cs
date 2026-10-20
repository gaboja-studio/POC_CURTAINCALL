using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.UI.BackstageHudV1;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Builder = CurtainCall.UI.BackstageHudV1.Editor.BackstageHUDBuilder;

namespace CurtainCall.BackstageHud.Editor
{
    public static class BackstageHudProjectBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_backstage_hud_v1";
        public const string PrefabRoot = "Assets/Resources/Prefabs/UIs/BackstageHud";
        public const string ScenePath = "Assets/Scenes/Tests/BackstageHud/BackstageHud.unity";
        const string FontRoot = "Assets/Resources/Fonts/BackstageHud";
        const string Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz,:—/•…().-+ " +
            "템플릿탐색멈춤소집결방코드장단평소경보미수신초기화로비가림긴값슬롯컨테이너상위하단우선가변빈칸비활성합성데이터게임연결없음자금증가감소복사요청회클립보드쓰기아주이름가나다라마바사";
        [Serializable] class Plan { public Source[] assets; public Source[] reused; }
        [Serializable] class Source { public string key; public string relative_path; public int[] source_px; public float[] border_lr_tb_px; public bool @new; }

        [MenuItem("Tools/Curtain Call/백스테이지 HUD/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일·임포트를 마친 일반 씬 편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            string[] names = { "BackstageHud", "LobbyHud", "InventorySlotTemplate" };
            foreach (string path in names.Select(x => PrefabRoot + "/" + x + ".prefab").Concat(new[] { ScenePath }))
                if (File.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException("기존 출력은 덮어쓰지 않습니다: " + path);
            var buttonAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab");
            if (buttonAsset == null) throw new InvalidOperationException("공용 버튼 정본 누락");
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Kit + "/Docs/hud_asset_plan.json"));
            var sources = plan.assets.Concat(plan.reused).ToArray();
            var sprites = new Dictionary<string, Sprite>();
            foreach (var source in sources)
            {
                string path = Canonical(source);
                if (!File.Exists(path)) throw new InvalidOperationException("Sprite 원본 누락: " + path);
                if (!source.@new)
                {
                    using var sha = SHA256.Create();
                    if (!sha.ComputeHash(File.ReadAllBytes(path)).SequenceEqual(sha.ComputeHash(File.ReadAllBytes(Kit + "/" + source.relative_path))))
                        throw new InvalidOperationException("재사용 원본 바이트 불일치: " + source.key);
                    ValidateSprite(source, path);
                }
            }
            foreach (string weight in new[] { "Medium", "Bold" })
            {
                if (AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf") == null)
                    throw new InvalidOperationException("원본 폰트 누락: " + weight);
                string output = FontRoot + "/Pretendard-" + weight + " SDF.asset";
                var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(output);
                if ((File.Exists(output) || File.Exists(output + ".meta")) && (existing == null || !existing.HasCharacters(Characters)))
                    throw new InvalidOperationException("기존 전용 폰트 충돌/글리프 부족");
            }
            foreach (var source in sources)
            {
                string path = Canonical(source);
                if (source.@new) ConfigureImporter(source, path);
                sprites.Add(source.key, ValidateSprite(source, path));
            }
            var medium = MakeFont("Medium", "Assets/Resources/Fonts/PerformanceHud/Pretendard-Medium SDF.asset");
            var bold = MakeFont("Bold", "Assets/Resources/Fonts/CheerGauge/Pretendard-Bold SDF.asset");
            if (!medium.HasCharacters(Characters) || !bold.HasCharacters(Characters)) throw new InvalidOperationException("글리프 부족");
            var original = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var roots = new GameObject[3];
                for (int i = 0; i < 3; i++)
                {
                    roots[i] = Builder.BuildProject(i, medium, bold, sprites);
                    roots[i].name = names[i];
                    if (i == 1) roots[i].AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    if (PrefabUtility.SaveAsPrefabAssetAndConnect(roots[i], PrefabRoot + "/" + names[i] + ".prefab", InteractionMode.AutomatedAction) == null)
                        throw new IOException("프리팹 저장 실패: " + names[i]);
                }
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(13, 23, 27, 255);
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                controls.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                controls.GetComponent<Canvas>().sortingOrder = 10;
                var scaler = controls.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var preview = controls.AddComponent<BackstageHudPreview>();
                var text = Text("Status", controls.transform, medium, new Vector2(480, 245), new Vector2(1392, 30));
                preview.Configure(roots[0].GetComponent<BackstageHUDView>(), roots[1].GetComponentInChildren<LobbyRoomInfoView>(true),
                    roots[2].GetComponentInChildren<InventorySlotsView>(true), text,
                    new[] { sprites["icon_bread"], sprites["icon_water"], sprites["icon_bandage"], sprites["icon_backpack"] });
                string[] labels = { "평소", "멈춤", "소집", "미수신", "가변 슬롯", "하단 우선", "로비", "긴 값", "비활성", "슬롯 템플릿", "초기화" };
                UnityEngine.Events.UnityAction[] actions = { preview.Normal, preview.Paused, preview.Alarm, preview.Unavailable, preview.Inventory,
                    preview.Priority, preview.LobbySample, preview.LongValues, preview.Disabled, preview.SlotTemplate, preview.ResetPreview };
                var buttons = new UnityEngine.UI.Button[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(buttonAsset, controls.transform);
                    go.name = "Sample_" + i;
                    var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(480 + i % 6 * 224, 32 + i / 6 * 72); rect.sizeDelta = new Vector2(192, 56);
                    var label = go.GetComponentInChildren<TMP_Text>(true); label.text = labels[i]; label.font = medium; label.fontSize = 22;
                    buttons[i] = go.GetComponent<UnityEngine.UI.Button>();
                    UnityEventTools.AddPersistentListener(buttons[i].onClick, actions[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                var roomButtons = roots[1].GetComponentsInChildren<UnityEngine.UI.Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    var nav = buttons[i].navigation; nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                    nav.selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length]; nav.selectOnRight = buttons[(i + 1) % buttons.Length];
                    nav.selectOnUp = roomButtons[0]; nav.selectOnDown = buttons[(i + 6) % buttons.Length]; buttons[i].navigation = nav;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                }
                foreach (var button in roomButtons) { var nav = button.navigation; nav.selectOnDown = buttons[6]; button.navigation = nav; PrefabUtility.RecordPrefabInstancePropertyModifications(button); }
                preview.Normal();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("백스테이지 씬 저장 실패");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(original); }
            Debug.Log("백스테이지·로비 프리팹3종 및 합성 확인 씬 생성 완료");
        }

        static string Canonical(Source source)
        {
            if (source.@new) return Kit + "/" + source.relative_path;
            string bundle = source.relative_path.Contains("/C1/") ? "curtaincall_c1_v1" :
                source.relative_path.Contains("/PerformanceHUD/") ? "curtaincall_performance_hud_v1" : "curtaincall_icons_v1";
            return "Assets/_ThirdParty/" + bundle + "/" + source.relative_path;
        }
        static Vector4 Border(Source source) => new Vector4(source.border_lr_tb_px[0], source.border_lr_tb_px[3], source.border_lr_tb_px[1], source.border_lr_tb_px[2]);
        static Sprite ValidateSprite(Source source, string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(sprite.pixelsPerUnit, 200) || sprite.border != Border(source) || sprite.rect.size != new Vector2(source.source_px[0], source.source_px[1]) ||
                sprite.texture.width != source.source_px[0] || sprite.texture.height != source.source_px[1])
                throw new InvalidOperationException("Sprite 규격 불일치(정본 자동 수정 없음): " + path);
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps" })
                if (importer.GetPlatformTextureSettings(platform).overridden) throw new InvalidOperationException("플랫폼 override 확인 필요: " + path);
            return sprite;
        }
        static void ConfigureImporter(Source source, string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Importer 누락: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200; importer.spriteBorder = Border(source); importer.spritePivot = Vector2.one * .5f;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center; importer.SetTextureSettings(settings);
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true; importer.sRGBTexture = true;
            importer.mipmapEnabled = false; importer.isReadable = false; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false; importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(source.source_px[0], source.source_px[1]));
            importer.SaveAndReimport();
        }
        static TMP_FontAsset MakeFont(string weight, string sharedPath)
        {
            var characters = new string(Characters.Distinct().ToArray());
            var shared = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sharedPath);
            if (shared != null && shared.atlasPopulationMode == AtlasPopulationMode.Static && shared.HasCharacters(characters)) return shared;
            EnsureFolder(FontRoot);
            string path = FontRoot + "/Pretendard-" + weight + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
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
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 20; text.color = new Color32(240, 238, 220, 255); text.raycastTarget = false; return text;
        }
        static void EnsureFolder(string path)
        {
            var bits = path.Split('/'); string current = bits[0];
            for (int i = 1; i < bits.Length; i++) { string next = current + "/" + bits[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, bits[i]); current = next; }
        }
    }
}
