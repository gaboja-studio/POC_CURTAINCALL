#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.StartLobby;
using CurtainCall.UI.PanelsV1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class CommonMenuInstaller
    {
        const string Panels = "Assets/Resources/Prefabs/UIs/BasicPanels/BasicPanels.prefab";
        const string Settings = "Assets/Resources/Prefabs/UIs/SettingsUi/SettingsPanel.prefab";
        const string Button = "Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab";
        const string Content = "60_Panels/C14_Settings/00_Visuals/10_LayoutStage/00_SettingsPanel/40_Content";
        [MenuItem("Tools/Curtain Call UI/Install common menu and settings")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be idle.");
            SceneUiLayoutPreview.Stop();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            foreach (string path in new[] { Panels, Settings, Button })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new InvalidOperationException("Missing tested prefab: " + path);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CommonMenuUI.ScenePath) != null) throw new InvalidOperationException("Already installed; edit scene instances.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("UI");
                var menuRoot = Instance(Panels, root.transform, "C13_MenuCanvas", 300);
                var settingsRoot = Instance(Settings, root.transform, "C14_SettingsCanvas", 310);
                var peersRoot = Instance(Settings, root.transform, "C14_TeammateSettingsCanvas", 320);
                var settingsGroup = settingsRoot.AddComponent<CanvasGroup>();
                // One early ESC owner per frame; remove only the instance's relay components.
                foreach (var relay in root.GetComponentsInChildren<CurtainCallPanelCancelRelay>(true)) UnityEngine.Object.DestroyImmediate(relay);
                foreach (var relay in root.GetComponentsInChildren<SettingsCancelRelay>(true)) UnityEngine.Object.DestroyImmediate(relay);
                var menu = menuRoot.GetComponent<CurtainCallMenuPanels>();
                var menuSerialized = new SerializedObject(menu);
                menuSerialized.FindProperty("showMenuOnStart").boolValue = false;
                menuSerialized.ApplyModifiedPropertiesWithoutUndo();
                var main = settingsRoot.GetComponentInChildren<SettingsPanelView>(true);
                var peers = peersRoot.GetComponentInChildren<SettingsPanelView>(true);
                // A full-screen raycast blocker is local presentation; no EventSystem is added.
                var back = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UnityEngine.UI.Image));
                back.transform.SetParent(root.transform, false);
                back.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                back.GetComponent<Canvas>().sortingOrder = 290;
                back.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .38f);
                back.SetActive(false);
                var parent = settingsRoot.transform.Find(Content);
                var peerButton = Instance(Button, parent, "TeammateControls").GetComponent<UnityEngine.UI.Button>();
                SetRect((RectTransform)peerButton.transform, 40, -348, 270, 56);
                peerButton.GetComponentInChildren<TMP_Text>(true).text = "단원별 음량 · 음소거";
                var hint = Text("TeammateHint", parent, 330, -350, 420, 58, 20);
                var controls = Text("ControlsHint", parent, 40, -344, 704, 72, 20);
                controls.text = "WASD 이동 · Space 점프 · F 상호작용\nESC 메뉴/뒤로 · M 마이크 음소거";
                peersRoot.transform.Find(Content + "/01_TitleTMP").GetComponent<TMP_Text>().text = "단원별 음성";
                var adapter = root.AddComponent<CommonSettingsUI>();
                adapter.Configure(main, peers, settingsGroup, peerButton, controls, hint);
                var owner = root.AddComponent<CommonMenuUI>();
                owner.Configure(menu, adapter, back,
                    menuRoot.transform.Find("70_ConfirmDialogs/LeaveConfirmation/50_Content/MessageTMP").GetComponent<TMP_Text>(),
                    menuRoot.transform.Find("60_Panels/Menu_C13/50_Content/ActionsAndFooter/FooterTMP").GetComponent<TMP_Text>());
                var statusCanvas = new GameObject("VoiceStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                statusCanvas.transform.SetParent(root.transform, false);
                statusCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                statusCanvas.GetComponent<Canvas>().sortingOrder = 120;
                var scaler = statusCanvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                BuildVoiceStatus(statusCanvas);
                // Use a project font covering Korean, without rebaking or mutating kit originals.
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset");
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                { text.font = font; text.fontSharedMaterial = font.material; PrefabUtility.RecordPrefabInstancePropertyModifications(text); }
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                if (!EditorSceneManager.SaveScene(scene, CommonMenuUI.ScenePath)) throw new InvalidOperationException("Menu scene save failed.");
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(CommonMenuUI.ScenePath, true) }).ToArray();
                CleanLegacy("Assets/Scenes/Start_Lobby/Start.unity");
                CleanLegacy("Assets/Scenes/Start_Lobby/Lobby.unity");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        public static void BuildVoiceStatus(GameObject canvas)
        {
            var old = canvas.transform.Find("MicrophoneStatus");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("MicrophoneStatus", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24, 70); rect.sizeDelta = new Vector2(72, 36);
            root.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            var button = root.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = root.GetComponent<UnityEngine.UI.Image>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var iconObject = new GameObject("MicrophoneIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(MicrophoneStatusIcon));
            iconObject.transform.SetParent(root.transform, false);
            SetRect((RectTransform)iconObject.transform, 0, -2, 32, 32);
            var icon = iconObject.GetComponent<MicrophoneStatusIcon>(); icon.raycastTarget = false;
            var label = Text("KeyLabel", root.transform, 40, -2, 30, 32, 22);
            label.text = "M"; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset");
            var owner = canvas.GetComponent<VoiceStatusUI>() ?? canvas.AddComponent<VoiceStatusUI>();
            owner.Configure(button, label); owner.ConfigureIcon(icon);
        }
        static void CleanLegacy(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var start = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StartMenu>(true)).FirstOrDefault();
            if (start != null)
            {
                if (start.settingsPage != null) UnityEngine.Object.DestroyImmediate(start.settingsPage.gameObject);
                start.settingsPage = null; start.useCommonSettings = true;
            }
            var lobby = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LobbyRoom>(true)).FirstOrDefault();
            if (lobby != null)
            {
                if (lobby.settings != null) UnityEngine.Object.DestroyImmediate(lobby.settings.gameObject);
                if (lobby.menu != null) UnityEngine.Object.DestroyImmediate(lobby.menu);
                lobby.menu = null; lobby.settings = null; lobby.resume = null; lobby.openSettings = null; lobby.leave = null; lobby.leaveLabel = null;
                lobby.useCommonMenu = true;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Space save failed: " + path);
        }
        static GameObject Instance(string path, Transform parent, string name, int order = -1)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            instance.name = name;
            if (order >= 0) instance.GetComponent<Canvas>().sortingOrder = order;
            return instance;
        }
        static TMP_Text Text(string name, Transform parent, float x, float y, float width, float height, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, x, y, width, height);
            var text = go.GetComponent<TMP_Text>(); text.fontSize = size; text.color = new Color32(240, 238, 220, 255); text.raycastTarget = false;
            return text;
        }
        static void SetRect(RectTransform rect, float x, float y, float width, float height)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height); }
    }
}
#endif
