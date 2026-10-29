using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Builder = CurtainCall.UI.IconsV1.Editor.CommonIconBuilder;

namespace CurtainCall.CommonIcons.Editor
{
    public static class CommonIconsProjectBuilder
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/CommonIcons/CommonIcons.prefab";
        public const string CatalogPath = "Assets/Resources/Prefabs/UIs/CommonIcons/CommonIconCatalog.asset";
        public const string ScenePath = "Assets/Scenes/Tests/CommonIcons/CommonIcons.unity";
        const string FontRoot = "Assets/Resources/Fonts/CommonIcons";
        const string Kit = "Assets/_ThirdParty/curtaincall_icons_v1";
        const string BuilderPath = Kit + "/Assets/CurtainCallUI/CommonIcons/Editor/CommonIconBuilder.cs";
        const string ControlsText = "다음 색다음 크기초기화기본성공위험경고비활성 정보단원흰색버튼 입력그림 승인 전 확인용설정0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ·×→…!?.,:/()-_#";

        [MenuItem("Tools/Curtain Call/공용 아이콘/프리팹과 확인 씬 생성")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorUtility.scriptCompilationFailed || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("컴파일을 마친 일반 씬 편집 모드에서 실행하세요.");
            if (File.Exists(PrefabPath) || File.Exists(CatalogPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("기존 아이콘 출력은 덮어쓰지 않습니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("열린 씬에 미저장 변경이 있습니다.");
            EnsureFolder(FontRoot);
            EnsureFolder("Assets/Resources/Prefabs/UIs/CommonIcons");
            EnsureFolder("Assets/Scenes/Tests/CommonIcons");
            var font = MakeFont();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Builder.BuildProject(font);
            root.name = "CommonIcons";
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var sprites = Builder.LoadProjectSprites();
            var catalog = ScriptableObject.CreateInstance<CommonIconCatalog>();
            catalog.Configure(Builder.GetKeys(), Builder.GetLabels(), sprites);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssetIfDirty(catalog);
            if (PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction) == null)
                throw new IOException("아이콘 비교 프리팹 저장 실패");

            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x0D, 0x17, 0x1B, 255);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.tag = "MainCamera";
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var controls = new GameObject("Controls", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            controls.SetActive(false);
            controls.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            controls.GetComponent<Canvas>().sortingOrder = 10;
            var scaler = controls.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var preview = controls.AddComponent<CommonIconsPreview>();
            var status = MakeText("Status", controls.transform, font, "", new Vector2(80, 54), new Vector2(1760, 28));
            preview.Configure(catalog, root.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .Where(image => image.sprite != null).ToArray(), status);
            MakeButton("NextTint", "다음 색", controls.transform, font, 80, preview.NextTint);
            MakeButton("NextSize", "다음 크기", controls.transform, font, 300, preview.NextSize);
            MakeButton("ResetPreview", "초기화", controls.transform, font, 520, preview.ResetPreview);
            var sampleAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/CommonButtons/Button_Settings.prefab");
            if (sampleAsset == null) throw new InvalidOperationException("설정 버튼 정본 누락");
            var sample = (GameObject)PrefabUtility.InstantiatePrefab(sampleAsset, controls.transform);
            sample.name = "SampleButton";
            var sampleRect = sample.GetComponent<RectTransform>();
            PlaceBottom(sampleRect, new Vector2(740, 0), new Vector2(192, 56));
            var icon = sample.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(image => image.sprite != null && image.sprite.name.Contains("icon_settings"));
            icon.sprite = catalog.GetSprite("backpack");
            icon.raycastTarget = false;
            UnityEventTools.AddPersistentListener(sample.GetComponent<UnityEngine.UI.Button>().onClick, preview.CountSampleClick);
            PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sampleRect);
            controls.SetActive(true);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("아이콘 확인 씬 저장 실패");
            Selection.activeGameObject = root;
        }

        static TMP_FontAsset MakeFont()
        {
            string characters = new string((File.ReadAllText(BuilderPath) + ControlsText)
                .Where(c => c >= ' ' && c != '﻿').Distinct().ToArray());
            string path = FontRoot + "/Pretendard-Medium SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                if (!existing.HasCharacters(characters)) throw new InvalidOperationException("기존 아이콘 폰트 글리프 부족");
                return existing;
            }
            var shared = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/BasicPanels/Pretendard-Medium SDF.asset");
            if (shared != null && shared.HasCharacters(characters)) return shared;
            var source = AssetDatabase.LoadAssetAtPath<Font>(Kit + "/Source/Fonts/Pretendard-Medium.ttf");
            if (source == null) throw new InvalidOperationException("아이콘 원본 폰트 누락");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Pretendard-Medium SDF";
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("아이콘 폰트 누락 글리프: " + missing);
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

        static void MakeButton(string name, string label, Transform parent, TMP_FontAsset font, float x, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            PlaceBottom(go.GetComponent<RectTransform>(), new Vector2(x, 0), new Vector2(192, 48));
            go.GetComponent<UnityEngine.UI.Image>().color = new Color32(0x30, 0x48, 0x52, 255);
            var text = MakeText("Label", go.transform, font, label, Vector2.zero, new Vector2(192, 48));
            text.alignment = TextAlignmentOptions.Center;
            UnityEventTools.AddPersistentListener(go.GetComponent<UnityEngine.UI.Button>().onClick, action);
        }

        static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font, string value, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            PlaceBottom(go.GetComponent<RectTransform>(), position, size);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = 20;
            text.color = new Color32(0xF0, 0xEE, 0xDC, 255);
            text.raycastTarget = false;
            return text;
        }

        static void PlaceBottom(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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
