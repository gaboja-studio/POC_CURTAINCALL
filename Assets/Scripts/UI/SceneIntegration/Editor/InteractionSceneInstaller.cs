#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.UI.InteractionV1;
using CurtainCall.WaitingRoom;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class InteractionSceneInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Create rescue progress prefab")]
        public static void CreateRescueProgressPrefab()
        {
            const string path = "Assets/Resources/Prefabs/UIs/Interaction/RescueProgress.prefab";
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be idle.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) throw new InvalidOperationException("Rescue progress prefab already exists; edit it directly.");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_ThirdParty/curtaincall_dayclock_v2.7/Assets/CurtainCallUI/DayClock/Sprites/ui_hud_dayclock_schedulefill_normal@2x.png");
            if (sprite == null) throw new InvalidOperationException("Day clock schedule fill sprite is missing.");
            var root = new GameObject("RescueProgress", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            try
            {
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(64f, 64f);
                var track = root.GetComponent<UnityEngine.UI.Image>(); track.sprite = sprite;
                track.color = new Color(.25f, .08f, .08f, .9f); track.raycastTarget = false;
                var outline = root.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(0f, 0f, 0f, 1f); outline.effectDistance = new Vector2(2f, -2f);
                var child = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(RescueProgressRing));
                var fillRect = (RectTransform)child.transform; fillRect.SetParent(rect, false);
                fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
                var fill = child.GetComponent<RescueProgressRing>(); fill.Sprite = sprite; fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; fill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top; fill.fillClockwise = true; fill.fillAmount = 1f; fill.color = new Color(1f, .12f, .12f, 1f); fill.raycastTarget = false;
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Prefab save failed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [MenuItem("Tools/Curtain Call UI/Install common interaction highlights")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be idle.");
            SceneUiLayoutPreview.Stop();
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CommonInteractionUI.ScenePath) != null) throw new InvalidOperationException("Already installed; edit the scene instance.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Interaction/InteractionPrompt.prefab");
            if (prefab == null) throw new InvalidOperationException("Tested C6 prefab is required.");
            var common = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(common);
            var ui = new GameObject("UI");
            var owner = ui.AddComponent<CommonInteractionUI>();
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ui.transform);
            canvas.name = "InteractionPromptCanvas";
            var view = canvas.GetComponentInChildren<InteractionPromptView>(true);
            var viewRect = (RectTransform)view.transform;
            // The prefab animates its Motion child; move the existing outer RectTransform.
            // Keep the tested prefab hierarchy and its connection intact.
            viewRect.anchoredPosition = Vector2.zero;
            owner.Configure(view, viewRect);
            var fonts = ui.AddComponent<UiRuntimeFontFallback>();
            fonts.Configure(Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF"));
            fonts.ConfigureSources(AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-Medium.ttf"),
                AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-SemiBold.ttf"));
            foreach (var component in ui.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            Save(common);
            EditorSceneManager.CloseScene(common, true);
            foreach (string path in new[] {WaitingRoomUiInstaller.RoomPath, PerformanceSceneInstaller.ScenePath, PerformanceSceneInstaller.CannonScenePath, "Assets/Scenes/Start_Lobby/Lobby.unity"})
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "UI") ?? new GameObject("UI");
                if (root.GetComponent<InteractionSceneBinding>() == null) root.AddComponent<InteractionSceneBinding>();
                if (path == WaitingRoomUiInstaller.RoomPath) AlignDoor(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Save failed: " + path);
            }
            var build = EditorBuildSettings.scenes.ToList();
            if (!build.Any(s => s.path == CommonInteractionUI.ScenePath)) build.Add(new EditorBuildSettingsScene(CommonInteractionUI.ScenePath, true));
            EditorBuildSettings.scenes = build.ToArray();
            EditorSceneManager.OpenScene(WaitingRoomUiInstaller.RoomPath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(CommonInteractionUI.ScenePath, OpenSceneMode.Additive);
        }
        static void Save(Scene scene)
        {
            if (!EditorSceneManager.SaveScene(scene, CommonInteractionUI.ScenePath)) throw new InvalidOperationException("Common scene save failed.");
        }
        static void AlignDoor(Scene scene)
        {
            var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var door = transforms.Single(t => t.name == "WaitingRoomShutter");
            var lintel = transforms.Single(t => t.name == "Seg1" && t.parent.name == "W_x+10").GetComponent<Renderer>();
            var serialized = new SerializedObject(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NightDoorLock>(true)).Single());
            var shutter = (Transform)serialized.FindProperty("shutter").objectReferenceValue;
            // Existing panel geometry is 3.1m, for the 3m opening plus a 5cm overlap at each side.
            var scale = door.localScale; scale.z = 1f; door.localScale = scale;
            var position = door.position; position.z += lintel.bounds.center.z - shutter.position.z; door.position = position;
            Physics.SyncTransforms();
            if (Mathf.Abs(shutter.position.z - lintel.bounds.center.z) > .01f) throw new InvalidOperationException("Door center mismatch.");
            // The old room union overlapped the new central corridor and counted an exit as still inside.
            var watch = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaitingRoomExitWatch>(true)).Single();
            var areas = watch.GetComponentsInChildren<BoxCollider>(true);
            var east = areas.Single(c => c.name == "Area_EastRooms");
            var wall = transforms.Single(t => t.name == "Seg0" && t.parent.name == "W_06_North_08_South").GetComponent<Renderer>();
            float back = east.bounds.max.z, front = wall.bounds.center.z;
            var p = east.transform.position; p.z = (front + back) * .5f; east.transform.position = p;
            var size = east.size; size.z = (back - front) / east.transform.lossyScale.z; east.size = size;
            // The expert-room area had been given a world Z as its local Z under the +70m parent.
            var expert = areas.Single(c => c.name == "Area_Expert");
            var floor = transforms.Single(t => t.name == "Floor" && t.parent.name == "S08_ExpertRoom").GetComponent<Renderer>().bounds;
            expert.transform.position = new Vector3(floor.center.x, 3, floor.center.z);
            expert.size = new Vector3(floor.size.x, 8, floor.size.z);
            Physics.SyncTransforms();
        }
    }
}
#endif
