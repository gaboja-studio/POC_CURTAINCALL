using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CurtainCall.WorldSigns.EditorTools
{
    public sealed class WorldSignStudio : EditorWindow
    {
        public const string FontFolder = "Assets/_ThirdParty/BM_Fonts";
        public const string GeneratedFolder = FontFolder + "/Generated";
        public const string ScenePath = "Assets/Scenes/Tests/WorldSigns/WorldFontShowcase.unity";
        const string PrefabFolder = "Assets/Scenes/Tests/WorldSigns/Prefabs/WorldSigns";
        const string SourceFolder = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/";
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            {"BMDOHYEON_ttf", "배민 도현"}, {"BMEULJIROTTF", "배민 을지로"},
            {"BMEuljiro10yearslater", "배민 을지로 10년후"}, {"BMEuljirooraeorae", "배민 을지로 오래오래"},
            {"BMHANNAPro", "배민 한나체 Pro"}, {"BMHANNA_11yrs_ttf", "배민 한나는 열한살"},
            {"BMHANNAAir_ttf", "배민 한나체 Air"}, {"BMJUA_ttf", "배민 주아"},
            {"BMKIRANGHAERANG-TTF", "배민 기랑해랑"}, {"BMKkubulimTTF", "배민 꾸불림"},
            {"BMYEONSUNG_ttf", "배민 연성"}
        };
        public static readonly List<string> Reports = new List<string>();
        static Queue<string> pending;
        public static bool IsBaking => pending != null;
        string sample = "대기실";
        int selected;
        Vector2 scroll;
        string status = "폰트를 선택하면 위쪽 비교 간판 3개에 즉시 반영됩니다. 아래 11종 표본은 유지됩니다.";

        [MenuItem("Tools/CurtainCall/World Signs/Font Comparison")]
        public static void Open() => GetWindow<WorldSignStudio>("월드 간판 폰트");

        static string[] Sources() => Directory.GetFiles(FontFolder, "*.ttf").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        static string Key(string path) => Path.GetFileNameWithoutExtension(path);
        static string Display(string key) => Names.TryGetValue(key, out string value) ? value : key;
        static string FontPath(string key) => GeneratedFolder + "/" + key + " SDF.asset";
        static TMP_FontAsset[] Fonts() => Sources().Select(p => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath(Key(p)))).ToArray();

        void OnGUI()
        {
            EditorGUILayout.LabelField("월드 간판 · 폰트 비교", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(status, MessageType.Info);
            if (GUILayout.Button("비교 씬 열기"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ScenePath);
                    FrameShowcase();
                }
            }
            sample = EditorGUILayout.TextField("간판 문구", sample);
            string[] names = Sources().Select(p => Display(Key(p))).ToArray();
            EditorGUI.BeginChangeCheck();
            selected = EditorGUILayout.Popup("폰트", Mathf.Clamp(selected, 0, names.Length - 1), names);
            if (EditorGUI.EndChangeCheck()) ApplyToHeroes();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ 이전")) { selected = (selected + names.Length - 1) % names.Length; ApplyToHeroes(); }
                if (GUILayout.Button("문구 적용")) ApplyToHeroes();
                if (GUILayout.Button("다음 ▶")) { selected = (selected + 1) % names.Length; ApplyToHeroes(); }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < names.Length; i++)
            {
                if (GUILayout.Button((i == selected ? "● " : "") + names[i])) { selected = i; ApplyToHeroes(); }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("각 프리팹의 WorldSignText Inspector에서도 폰트·문구를 변경할 수 있습니다. 씬 변경은 필요할 때 직접 저장하세요.", MessageType.None);
            using (new EditorGUI.DisabledScope(IsBaking))
                if (GUILayout.Button("누락된 Static 아틀라스 생성")) BeginBake();
        }

        void ApplyToHeroes()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { status = "먼저 비교 씬을 열어 주세요. 현재 게임 씬은 변경하지 않았습니다."; return; }
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "InteractiveSigns");
            if (root == null) return;
            foreach (var sign in root.GetComponentsInChildren<WorldSignText>())
            {
                Undo.RecordObjects(new UnityEngine.Object[] { sign, sign.target }, "간판 폰트 비교");
                sign.text = sample;
                sign.SelectFont(selected);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sign);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sign.target);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            status = "현재 폰트: " + Display(Key(Sources()[selected]));
        }

        [MenuItem("Tools/CurtainCall/World Signs/Bake Missing Static Atlases")]
        public static void BeginBake()
        {
            if (IsBaking) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료하세요.");
            Reports.Clear();
            pending = new Queue<string>(Sources().Where(p => !File.Exists(FontPath(Key(p)))));
            EditorApplication.update += BakeNext;
        }

        static void BakeNext()
        {
            EditorApplication.update -= BakeNext;
            if (pending == null) return;
            if (pending.Count == 0) { pending = null; Debug.Log("[WorldSigns] 모든 아틀라스 생성 완료"); return; }
            string path = pending.Dequeue();
            try
            {
                string key = Key(path);
                var source = AssetDatabase.LoadAssetAtPath<Font>(path);
                var font = TMP_FontAsset.CreateFontAsset(source, 40, 5, GlyphRenderMode.SDFAA, 4096, 4096, AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("폰트 생성 실패: " + path);
                font.name = key + " SDF";
                uint[] characters = Enumerable.Range(32, 95).Concat(Enumerable.Range(44032, 11172))
                    .Concat(Enumerable.Range(12593, 51)).Concat(Enumerable.Range(8200, 1701))
                    .Select(c => (uint)c).ToArray();
                font.TryAddCharacters(characters, out uint[] missing);
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                var serialized = new SerializedObject(font);
                serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(font, FontPath(key));
                foreach (var texture in font.atlasTextures)
                {
                    texture.name = key + " Atlas " + Array.IndexOf(font.atlasTextures, texture);
                    if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
                }
                if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssetIfDirty(font);
                string report = $"{key}: {font.characterTable.Count}자, {font.atlasTextures.Length}장, 원본 미지원/미수록 {(missing == null ? 0 : missing.Length)}자";
                Reports.Add(report);
                Debug.Log("[WorldSigns] " + report);
            }
            catch (Exception exception) { pending = null; Debug.LogException(exception); return; }
            EditorApplication.update += BakeNext;
        }

        static Material Material(string name, Color color)
        {
            string path = PrefabFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.name = name;
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }

        static TextMeshPro Text(string name, Transform parent, string value, TMP_FontAsset font, Vector3 position, Vector2 size, float maxSize, Color color)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var text = go.GetComponent<TextMeshPro>();
            text.font = font;
            text.text = value;
            text.rectTransform.sizeDelta = size;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 1;
            text.fontSizeMax = maxSize;
            text.fontSize = maxSize;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        static GameObject MakeSign(int style, TMP_FontAsset[] fonts, string[] names, Material dark, Material cream)
        {
            var root = new GameObject(new[] { "WorldSign_Billboard", "WorldSign_Standing", "WorldSign_Blackboard" }[style]);
            string source = new[] { "SM_Prop_Billboard_Sign_01", "SM_Prop_Sign_01", "SM_Prop_Blackboard_01" }[style];
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SourceFolder + source + ".prefab"), root.transform);
            var bounds = model.GetComponentsInChildren<MeshFilter>().First().sharedMesh.bounds;
            float width = style == 0 ? 4.2f : style == 1 ? 1.4f : 2.1f;
            float scale = width / bounds.size.x;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = new Vector3(-bounds.center.x * scale, style == 0 ? 0 : -bounds.max.y * scale + 1.0f, 0);
            float front = bounds.min.z * scale - 0.04f;
            float center = style == 2 ? -.55f : .1f;
            float height = style == 2 ? 2.75f : 1.45f;
            Box("BlankSignFace", root.transform, new Vector3(0, center, front), new Vector3(width * 0.92f, height, 0.04f), style == 2 ? dark : cream);
            var target = Text("SignText", root.transform, "대기실", fonts[0], new Vector3(0, center, front - 0.03f), new Vector2(width * 0.86f, height - .2f), 8, style == 2 ? new Color(1, .94f, .78f) : new Color(.13f, .16f, .15f));
            var sign = root.AddComponent<WorldSignText>();
            sign.fonts = fonts;
            sign.fontNames = names;
            sign.target = target;
            sign.Apply();
            return root;
        }

        [MenuItem("Tools/CurtainCall/World Signs/Create Comparison Scene")]
        public static void CreateShowcase()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료하세요.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("기존 비교 씬은 덮어쓰지 않습니다.");
            var fonts = Fonts();
            if (fonts.Any(f => f == null)) throw new InvalidOperationException("11종 폰트 생성이 먼저 필요합니다.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var dark = Material("Sign_Charcoal", new Color(.07f, .12f, .13f));
            var cream = Material("Sign_Ivory", new Color(.94f, .86f, .65f));
            var background = Material("Showcase_Background", new Color(.035f, .055f, .075f));
            string[] names = Sources().Select(p => Display(Key(p))).ToArray();
            var heroes = new GameObject("InteractiveSigns");
            for (int i = 0; i < 3; i++)
            {
                var sign = MakeSign(i, fonts, names, dark, cream);
                string path = PrefabFolder + "/" + sign.name + ".prefab";
                if (File.Exists(path)) throw new InvalidOperationException("기존 프리팹은 덮어쓰지 않습니다: " + path);
                PrefabUtility.SaveAsPrefabAssetAndConnect(sign, path, InteractionMode.AutomatedAction);
                sign.transform.SetParent(heroes.transform, false);
                sign.transform.localPosition = new Vector3((i - 1) * 6.3f, 7, 0);
            }
            var gallery = new GameObject("AllFonts_FixedComparison");
            for (int i = 0; i < fonts.Length; i++)
            {
                var board = new GameObject("Sample_" + Key(Sources()[i]));
                board.transform.SetParent(gallery.transform, false);
                board.transform.localPosition = new Vector3((i % 4 - 1.5f) * 5.2f, 2.8f - i / 4 * 2.7f, 0);
                Box("Board", board.transform, Vector3.zero, new Vector3(4.8f, 2.3f, .12f), dark);
                Text("FontName", board.transform, names[i], fonts[i], new Vector3(0, .75f, -.09f), new Vector2(4.5f, .45f), 2.8f, new Color(.53f, .8f, .74f));
                Text("Sample", board.transform, "대기실 · 매표소\n공연 준비 0123", fonts[i], new Vector3(0, -.15f, -.09f), new Vector2(4.4f, 1.35f), 5.5f, new Color(1, .95f, .83f));
            }
            Box("Backdrop", null, new Vector3(0, 2, 2), new Vector3(30, 24, .1f), background);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 2.6f, -30);
            camera.orthographic = true;
            camera.orthographicSize = 8.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .055f, .075f);
            var light = new GameObject("Showcase Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.eulerAngles = new Vector3(35, -25, 0);
            AddShowcaseLabels();
            EditorSceneManager.SaveScene(scene, ScenePath);
            FrameShowcase();
            Open();
        }

        public static void AddShowcaseLabels()
        {
            var root = new GameObject("ShowcaseLabels");
            var font = Fonts()[0];
            var ivory = new Color(1, .95f, .83f);
            var muted = new Color(.53f, .8f, .74f);
            Text("Title", root.transform, "월드 간판 · 폰트 비교", font, new Vector3(0, 10.3f, -.2f), new Vector2(24, .65f), 5, ivory);
            Text("Controls", root.transform, "Tools > CurtainCall > World Signs > Font Comparison", font, new Vector3(0, 9.6f, -.2f), new Vector2(24, .4f), 2.7f, muted);
            string[] styles = { "벽면 간판", "입간판", "칠판 간판" };
            for (int i = 0; i < styles.Length; i++)
                Text("Style_" + i, root.transform, styles[i], font, new Vector3((i - 1) * 6.3f, 8.6f, -.2f), new Vector2(5, .4f), 2.8f, muted);
            Text("Guide", root.transform, "상단: 선택 폰트 즉시 적용\n하단: 11종 고정 표본\n\n모두 3D 월드 글씨입니다.", font, new Vector3(7.8f, -2.6f, -.2f), new Vector2(4.6f, 2.1f), 2.9f, muted);
        }

        static void FrameShowcase()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.LookAt(new Vector3(0, 2.6f, 0), Quaternion.identity, 12, true, true);
            view.Repaint();
        }
    }

    [CustomEditor(typeof(WorldSignText))]
    public sealed class WorldSignTextEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var sign = (WorldSignText)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("text"));
            var index = serializedObject.FindProperty("fontIndex");
            if (sign.fontNames != null && sign.fontNames.Length > 0)
                index.intValue = EditorGUILayout.Popup("폰트", Mathf.Clamp(index.intValue, 0, sign.fontNames.Length - 1), sign.fontNames);
            bool changed = serializedObject.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(sign.fonts == null || sign.fonts.Length == 0 || sign.target == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ 이전")) { Undo.RecordObject(sign, "이전 폰트"); sign.fontIndex = (sign.fontIndex + sign.fonts.Length - 1) % sign.fonts.Length; changed = true; }
                if (GUILayout.Button("다음 ▶")) { Undo.RecordObject(sign, "다음 폰트"); sign.fontIndex = (sign.fontIndex + 1) % sign.fonts.Length; changed = true; }
            }
            if (changed && sign.target != null)
            {
                Undo.RecordObject(sign.target, "간판 글씨 적용");
                sign.Apply();
                EditorUtility.SetDirty(sign);
                EditorUtility.SetDirty(sign.target);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sign);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sign.target);
            }
            using (new EditorGUI.IndentLevelScope())
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("target"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fonts"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fontNames"));
                if (serializedObject.ApplyModifiedProperties()) sign.Apply();
            }
        }
    }
}
