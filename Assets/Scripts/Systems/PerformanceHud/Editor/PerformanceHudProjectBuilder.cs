using System;
using System.IO;
using System.Linq;
using CurtainCall.CheerGauge;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Builder = CurtainCall.UI.PerformanceHudV1.Editor.PerformanceHUDBuilder;

namespace CurtainCall.PerformanceHud.Editor
{
    public static class PerformanceHudProjectBuilder
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/PerformanceHud/PerformanceHud.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/PerformanceHud/PerformanceHud.unity";
        const string FontRoot = "Assets/Resources/Fonts/PerformanceHud";
        const string Kit = "Assets/_ThirdParty/curtaincall_performance_hud_v1";
        const string Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz—:·/().-+ " +
            "겹침남은평소경고위험미수신초기화관전외줄공연전환균형시간진행도확인합성데이터게임연결종료";

        [MenuItem("Tools/Curtain Call/공연 HUD/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed ||
                EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일을 마친 일반 씬 편집 모드에서 실행하세요.");
            if (File.Exists(PrefabPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("기존 공연 HUD 출력은 덮어쓰지 않습니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            EnsureFolder("Assets/Resources/Prefabs/UIs/PerformanceHud");
            EnsureFolder("Assets/Scenes/Tests/PerformanceHud");
            var medium = MakeFont("Medium", "Assets/Resources/Fonts/CommonIcons/Pretendard-Medium SDF.asset");
            var semiBold = MakeFont("SemiBold", "Assets/Resources/Fonts/CommonButtons/Pretendard-SemiBold SDF.asset");
            var bold = MakeFont("Bold", "Assets/Resources/Fonts/CheerGauge/Pretendard-Bold SDF.asset", "0123456789:—");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Builder.BuildProject(medium, semiBold, bold);
            root.name = "PerformanceHud";
            if (PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction) == null)
                throw new IOException("공연 HUD 프리팹 저장 실패");
            var hud = root.GetComponent<CurtainCall.UI.PerformanceHudV1.PerformanceHUDView>();
            var cheerAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab");
            if (cheerAsset == null) throw new InvalidOperationException("C1 정본 누락");
            var cheer = (GameObject)PrefabUtility.InstantiatePrefab(cheerAsset, root.transform);
            cheer.GetComponent<C1CheerGaugeBinding>().enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cheer.GetComponent<C1CheerGaugeBinding>());
            var gauge = cheer.GetComponent<C1CheerGaugeView>();
            gauge.SetScores(new long[] { 60, 45, 30, 20 }, 400, false);

            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x0D, 0x17, 0x1B, 255);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = controls.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = controls.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var preview = controls.AddComponent<PerformanceHudPreview>();
            var status = MakeText("Status", controls.transform, medium, new Vector2(48, 94), new Vector2(1824, 30));
            preview.Configure(hud, gauge, status);
            string[] labels = { "평소", "경고/위험", "겹침", "미수신", "관전 전환", "외줄 전환", "공연 전환", "초기화" };
            UnityEngine.Events.UnityAction[] actions = { preview.Normal, preview.Warning, preview.Overlap, preview.Unavailable,
                preview.ToggleObserving, preview.ToggleTightrope, preview.TogglePerformance, preview.ResetPreview };
            var buttonAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab");
            if (buttonAsset == null) throw new InvalidOperationException("공용 버튼 정본 누락");
            for (int i = 0; i < labels.Length; i++)
            {
                var button = (GameObject)PrefabUtility.InstantiatePrefab(buttonAsset, controls.transform);
                button.name = "Sample_" + i;
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(48 + i * 224, 24);
                rect.sizeDelta = new Vector2(192, 56);
                var text = button.GetComponentInChildren<TMP_Text>(true);
                text.text = labels[i];
                text.font = semiBold;
                text.fontSize = 24;
                UnityEventTools.AddPersistentListener(button.GetComponent<UnityEngine.UI.Button>().onClick, actions[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                PrefabUtility.RecordPrefabInstancePropertyModifications(button.GetComponent<UnityEngine.UI.Button>());
            }
            preview.Normal();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("공연 HUD 확인 씬 저장 실패");
            Selection.activeGameObject = root;
        }

        [MenuItem("Tools/Curtain Call/공연 HUD/P1 경고 구간 보정")]
        public static void UpgradeBalanceWarnings()
        {
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일을 마친 일반 씬 편집 모드에서 실행하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다.");
            if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("공연 HUD 확인 씬 하나만 연 상태에서 실행하세요.");
            var scenePreview = UnityEngine.Object.FindFirstObjectByType<PerformanceHudPreview>();
            var sampleButton = GameObject.Find("Controls/Sample_1");
            var button = sampleButton == null ? null : sampleButton.GetComponent<UnityEngine.UI.Button>();
            if (scenePreview == null || button == null || sampleButton.GetComponentInChildren<TMP_Text>(true) == null ||
                button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != scenePreview ||
                button.onClick.GetPersistentMethodName(0) != "Warning")
                throw new InvalidOperationException("기존 확인 씬 연결이 예상과 다릅니다.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (source == null) throw new InvalidOperationException("공연 HUD 정본 누락");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool changed = false;
            try
            {
                var hud = root.GetComponent<CurtainCall.UI.PerformanceHudV1.PerformanceHUDView>();
                if (hud == null || hud.Balance == null) throw new InvalidOperationException("P1 참조 누락");
                var view = hud.Balance;
                var data = new SerializedObject(view);
                var zones = RectReferences(data, "zoneRects");
                var edges = RectReferences(data, "boundaries");
                var masks = RectReferences(data, "hatchMasks");
                string[] names = { "LeftDanger", "LeftWarning", "Safe", "RightWarning", "RightDanger" };
                string[] edgeNames = { "LeftWarningBoundary", "LeftBoundary", "RightBoundary", "RightWarningBoundary" };
                if (masks.Length != 2 || (zones.Length != 3 && zones.Length != 5) ||
                    edges.Length != (zones.Length == 3 ? 2 : 4))
                    throw new InvalidOperationException("P1은 구형3/2/2 또는 완료5/2/4 연결이어야 합니다.");
                string[] expected = zones.Length == 3 ? new[] { "LeftDanger", "Safe", "RightDanger" } : names;
                string[] expectedEdges = edges.Length == 2 ? new[] { "LeftBoundary", "RightBoundary" } : edgeNames;
                if (!zones.Select(x => x.name).SequenceEqual(expected) || !edges.Select(x => x.name).SequenceEqual(expectedEdges) ||
                    zones.Any(x => x.parent != zones[0].parent) || edges.Any(x => x.parent != edges[0].parent))
                    throw new InvalidOperationException("기존 P1 구간 순서가 예상과 다릅니다.");
                if (zones.Length == 3)
                {
                    if (zones[0].parent.childCount != 3 || edges[0].parent.childCount != 2)
                        throw new InvalidOperationException("일부만 보정된 P1은 자동 저장하지 않습니다.");
                    var warning = new Color32(0xF2, 0xB4, 0x5B, 255);
                    zones = new[] { zones[0], AddLayer(zones[1], "LeftWarning", warning), zones[1],
                        AddLayer(zones[1], "RightWarning", warning), zones[2] };
                    edges = new[] { AddLayer(edges[0], edgeNames[0], edges[0].GetComponent<UnityEngine.UI.Image>().color),
                        edges[0], edges[1], AddLayer(edges[1], edgeNames[3], edges[1].GetComponent<UnityEngine.UI.Image>().color) };
                    for (int i = 0; i < zones.Length; i++) zones[i].SetSiblingIndex(i);
                    for (int i = 0; i < edges.Length; i++) edges[i].SetSiblingIndex(i);
                    view.Configure((CanvasGroup)data.FindProperty("visibility").objectReferenceValue, zones, masks, edges,
                        (RectTransform)data.FindProperty("needle").objectReferenceValue,
                        (RectTransform)data.FindProperty("needleUnderlay").objectReferenceValue,
                        (UnityEngine.UI.Image)data.FindProperty("dwellFill").objectReferenceValue,
                        new[] { (UnityEngine.UI.Image)data.FindProperty("keyBodies").GetArrayElementAtIndex(0).objectReferenceValue,
                            (UnityEngine.UI.Image)data.FindProperty("keyBodies").GetArrayElementAtIndex(1).objectReferenceValue },
                        new[] { (TMP_Text)data.FindProperty("keyLabels").GetArrayElementAtIndex(0).objectReferenceValue,
                            (TMP_Text)data.FindProperty("keyLabels").GetArrayElementAtIndex(1).objectReferenceValue });
                    hud.ShowEditorSample(CurtainCall.UI.PerformanceHudV1.PerformanceHUDView.EditorSample.Normal);
                    if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new IOException("P1 보정 저장 실패");
                    changed = true;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // 새 버튼 문구에 필요한 두 글자만 소유한 전용 폰트에 추가합니다. 공유 폰트는 수정하지 않습니다.
            EnsureWarningGlyphs();
            if (changed) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            sampleButton = GameObject.Find("Controls/Sample_1");
            var label = sampleButton.GetComponentInChildren<TMP_Text>(true);
            if (label.text != "경고/위험")
            {
                label.text = "경고/위험";
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            if (changed || SceneManager.GetActiveScene().isDirty)
            {
                UnityEngine.Object.FindFirstObjectByType<PerformanceHudPreview>().Normal();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            if (SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new IOException("확인 씬 보정 저장 실패");
            Debug.Log(changed ? "P1 경고 구간 보정 완료" : "P1 경고 구간 보정 상태 유지(중복 생성 없음)");
        }

        static RectTransform[] RectReferences(SerializedObject data, string field)
        {
            var property = data.FindProperty(field);
            if (property == null || !property.isArray) throw new InvalidOperationException("P1 필드 누락: " + field);
            var values = Enumerable.Range(0, property.arraySize)
                .Select(i => property.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform).ToArray();
            if (values.Any(x => x == null)) throw new InvalidOperationException("P1 연결 누락: " + field);
            return values;
        }

        static RectTransform AddLayer(RectTransform template, string name, Color color)
        {
            if (template.parent.Find(name) != null) throw new InvalidOperationException("기존 보정 자식 중복: " + name);
            var source = template.GetComponent<UnityEngine.UI.Image>();
            if (source == null || source.sprite == null) throw new InvalidOperationException("구간 Sprite 누락");
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(template.parent, false);
            rect.anchorMin = template.anchorMin;
            rect.anchorMax = template.anchorMax;
            rect.pivot = template.pivot;
            rect.anchoredPosition = template.anchoredPosition;
            rect.sizeDelta = template.sizeDelta;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = source.sprite;
            image.type = source.type;
            image.preserveAspect = source.preserveAspect;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static void EnsureWarningGlyphs()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRoot + "/Pretendard-SemiBold SDF.asset");
            if (font == null || font.HasCharacters("위험")) return;
            // Static 폰트의 원본 참조는 TMP의 편집기 getter로 GUID에서 복원합니다.
            typeof(TMP_FontAsset).GetProperty("SourceFont_EditorRef",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(font);
            var mode = font.atlasPopulationMode;
            try
            {
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                if (font.sourceFontFile == null) throw new InvalidOperationException("전용 폰트 원본 누락");
                if (!font.TryAddCharacters("위험", out string missing)) throw new InvalidOperationException("위험 글리프 부족: " + missing);
                foreach (var atlas in font.atlasTextures.Where(x => x != null))
                    if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            }
            finally { font.atlasPopulationMode = mode; }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }

        static TMP_FontAsset MakeFont(string weight, string sharedPath, string characters = Characters)
        {
            characters = new string(characters.Distinct().ToArray());
            var shared = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sharedPath);
            if (shared != null && shared.atlasPopulationMode == AtlasPopulationMode.Static && shared.HasCharacters(characters)) return shared;
            EnsureFolder(FontRoot);
            string path = FontRoot + "/Pretendard-" + weight + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                if (!existing.HasCharacters(characters)) throw new InvalidOperationException("기존 전용 폰트 글리프 부족");
                return existing;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf");
            if (source == null) throw new InvalidOperationException("원본 폰트 누락: " + weight);
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Pretendard-" + weight + " SDF";
            if (!font.TryAddCharacters(characters, out string missing)) throw new InvalidOperationException("폰트 글리프 부족: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures.Where(texture => texture != null))
            {
                atlas.name = font.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 20;
            text.color = new Color32(0xF0, 0xEE, 0xDC, 255);
            text.raycastTarget = false;
            return text;
        }
        static void EnsureFolder(string path)
        {
            string[] bits = path.Split('/');
            string current = bits[0];
            for (int i = 1; i < bits.Length; i++)
            {
                string next = current + "/" + bits[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, bits[i]);
                current = next;
            }
        }
    }
}
