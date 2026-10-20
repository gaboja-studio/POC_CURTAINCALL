using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace CurtainCall.CommonButtons.Editor
{
    public static class CommonButtonsProjectBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tests/CommonButtons/CommonButtons.unity";
        const string FontRoot = "Assets/Resources/Fonts/CommonButtons";
        const string Kit = "Assets/_ThirdParty/curtaincall_buttons_v1";

        [MenuItem("Tools/Curtain Call/공용 버튼/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("컴파일 완료 후 편집 모드에서 실행하세요.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("기존 확인 씬을 덮어쓰지 않습니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("열린 씬에 미저장 변경이 있습니다.");
            EnsureFolder("Assets/Scenes/Tests/CommonButtons");
            EnsureFolder(FontRoot);
            var font = MakeFont();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.tag = "MainCamera";
            CurtainCall.UI.ButtonsV1.Editor.CommonButtonBuilder.BuildProject(font);
            var preview = new GameObject("CommonButtonsPreview").AddComponent<CommonButtonsPreview>();
            preview.Configure(UnityEngine.Object.FindObjectsByType<CurtainCall.UI.ButtonsV1.CurtainCallButton>(FindObjectsSortMode.None),
                AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-SemiBold.ttf"));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("확인 씬 저장 실패");
            AssetDatabase.SaveAssetIfDirty(font);
        }

        static TMP_FontAsset MakeFont()
        {
            string path = FontRoot + "/Pretendard-SemiBold SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-SemiBold.ttf");
            if (source == null) throw new InvalidOperationException("SemiBold 원본 폰트가 없습니다.");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Pretendard-SemiBold SDF";
            if (!font.TryAddCharacters("확인취소설정일반…가나다라마바사아자차카타파하 긴 문구 테스트0123456789", out string missing))
                throw new InvalidOperationException("누락 글리프: " + missing);
            AssetDatabase.CreateAsset(font, path);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.name = font.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            EditorUtility.SetDirty(font);
            return font;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
