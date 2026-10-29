using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using CurtainCall.UI.PanelsV1;

namespace CurtainCall.BasicPanels.Editor
{
    public static class BasicPanelsProjectBuilder
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/BasicPanels/BasicPanels.prefab";
        public const string ScenePath = "Assets/Scenes/Tests/BasicPanels/BasicPanels.unity";
        const string FontRoot = "Assets/Resources/Fonts/BasicPanels";
        const string Kit = "Assets/_ThirdParty/curtaincall_panels_v1";
        const string Characters = "메뉴계속하기설정나가기방에서 나갈까요?돌아가기공연은 계속 진행 중입니다나가면 사망 처리되고방에서 나갑니다.확인전용실제기능연결하지않았습니다신호모두닫기카운터초기화긴문구테스트가나다라마바사아자차카타파하…—0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz !?.,:/()-_";

        [MenuItem("Tools/Curtain Call/기본 패널/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorUtility.scriptCompilationFailed || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일을 마친 일반 씬 편집 모드에서 실행하세요.");
            if (File.Exists(PrefabPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("기존 패널 프리팹·확인 씬은 덮어쓰지 않습니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("열린 씬에 미저장 변경이 있습니다.");
            EnsureFolder(FontRoot);
            EnsureFolder("Assets/Resources/Prefabs/UIs/BasicPanels");
            EnsureFolder("Assets/Scenes/Tests/BasicPanels");
            var medium = MakeFont("Medium");
            var semiBold = MakeFont("SemiBold");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x0D, 0x17, 0x1B, 0xFF);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.tag = "MainCamera";
            var root = CurtainCall.UI.PanelsV1.Editor.BasicPanelBuilder.BuildProject(medium, semiBold, false);
            root.name = "BasicPanels";
            var controller = root.GetComponent<CurtainCallMenuPanels>();
            if (controller == null) throw new InvalidOperationException("패널 컨트롤러가 없습니다.");
            controller.ShowEditorPreview(true, false);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            if (prefab == null) throw new IOException("패널 프리팹 저장 실패");
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("showMenuOnStart").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var previewRoot = new GameObject("BasicPanelsPreview");
            previewRoot.SetActive(false);
            previewRoot.AddComponent<BasicPanelsPreview>().Configure(controller,
                AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-Medium.ttf"));
            previewRoot.SetActive(true);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("패널 확인 씬 저장 실패");
        }

        static TMP_FontAsset MakeFont(string weight)
        {
            string path = FontRoot + "/Pretendard-" + weight + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                if (!existing.HasCharacters(Characters)) throw new InvalidOperationException("기존 패널 폰트의 글리프가 부족합니다: " + path);
                return existing;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-" + weight + ".ttf");
            if (source == null) throw new InvalidOperationException("패널 원본 폰트가 없습니다: " + weight);
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Pretendard-" + weight + " SDF";
            string uniqueCharacters = new string(Characters.Distinct().ToArray());
            if (!font.TryAddCharacters(uniqueCharacters, out string missing))
                throw new InvalidOperationException("패널 폰트 누락 글리프: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.name = font.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
