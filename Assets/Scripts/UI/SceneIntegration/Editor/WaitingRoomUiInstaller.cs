#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.CommonIcons;
using CurtainCall.UI.BackstageHudV1;
using CurtainCall.UI.CaptionsV1;
using CurtainCall.UI.NotificationsV1;
using CurtainCall.WaitingRoom;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class WaitingRoomUiInstaller
    {
        public const string RoomPath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        [MenuItem("Tools/Curtain Call UI/Install waiting room HUD")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Editor must be idle.");
            SceneUiLayoutPreview.Stop();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CommonWaitingRoomUI.ScenePath) != null)
                throw new InvalidOperationException("Already installed. Edit existing scene instances instead of reinstalling.");
            var hudAsset = Require("BackstageHud/BackstageHud");
            var captionsAsset = Require("Captions/Captions");
            var messagesAsset = Require("Notifications/SystemNotifications");
            var room = EditorSceneManager.OpenScene(RoomPath, OpenSceneMode.Single);
            var components = room.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var gather = components.OfType<StageEntranceGather>().Single();
            var announcer = components.OfType<ExplorationTimerAnnouncer>().Single();
            var legacyClock = components.OfType<ExplorationTimerView>().Single();
            var legacyFunds = components.OfType<CurtainCall.WaitingRoom.SharedFundsView>().Single();
            var carry = components.OfType<PropCarry>().Single();
            if (room.GetRootGameObjects().Any(r => r.name == "UI")) throw new InvalidOperationException("Inspect existing UI root before installation.");
            var ui = new GameObject("UI");
            ui.AddComponent<WaitingRoomUiBinding>().Configure(gather, announcer);
            // Audio and announcement events stay live; only the replaced runtime Canvas is suppressed.
            SetBool(legacyClock, "showLegacyUi", false);
            announcer.transform.SetParent(ui.transform, true);
            announcer.gameObject.name = "ExplorationAnnouncements";
            SetBool(gather, "showStatus", false);
            SetBool(carry, "showHud", false);
            UnityEngine.Object.DestroyImmediate(legacyFunds); // FloorPropsView/HeldPropsView on the same object must survive.
            foreach (var canvas in room.GetRootGameObjects().Where(r => r.GetComponent<Canvas>() != null))
                canvas.transform.SetParent(ui.transform, true);
            EditorSceneManager.MarkSceneDirty(room);
            if (!EditorSceneManager.SaveScene(room)) throw new InvalidOperationException("Could not save waiting room.");

            var common = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(common);
            var root = new GameObject("UI");
            var owner = root.AddComponent<CommonWaitingRoomUI>();
            var hud = Instantiate(hudAsset, root, "BackstageHudCanvas").GetComponent<BackstageHUDView>();
            var captionsRoot = Instantiate(captionsAsset, root, "BroadcastCaptionCanvas");
            var captions = captionsRoot.GetComponentInChildren<CaptionView>(true);
            var messages = Instantiate(messagesAsset, root, "WaitingRoomNotificationsCanvas").GetComponent<SystemNotificationsView>();
            owner.Configure(hud, captions, messages, Resources.Load<CommonIconCatalog>("Prefabs/UIs/CommonIcons/CommonIconCatalog"));
            hud.ResetForRuntime(); captions.ResetForRuntime(); messages.ResetForRuntime();
            var fonts = root.AddComponent<UiRuntimeFontFallback>();
            fonts.Configure(Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF"));
            fonts.ConfigureSources(AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-Medium.ttf"),
                AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-SemiBold.ttf"));
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            if (!EditorSceneManager.SaveScene(common, CommonWaitingRoomUI.ScenePath)) throw new InvalidOperationException("Could not save common UI.");
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == CommonWaitingRoomUI.ScenePath)) scenes.Add(new EditorBuildSettingsScene(CommonWaitingRoomUI.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            SceneManager.SetActiveScene(room);
        }
        static GameObject Require(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/" + name + ".prefab")
            ?? throw new InvalidOperationException("Missing tested Assets prefab: " + name);
        static GameObject Instantiate(GameObject prefab, GameObject parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            instance.name = name;
            return instance;
        }
        static void SetBool(UnityEngine.Object target, string name, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
