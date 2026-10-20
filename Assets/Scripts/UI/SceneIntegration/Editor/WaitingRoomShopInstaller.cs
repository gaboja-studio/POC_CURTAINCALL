#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.WaitingRoom;
using CurtainCall.UI.ShopV1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class WaitingRoomShopInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Install waiting room shop")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Editor must be idle.");
            SceneUiLayoutPreview.Stop();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/ShopUi/ShopPanel.prefab");
            if (asset == null) throw new InvalidOperationException("Missing tested ShopPanel prefab.");
            var room = EditorSceneManager.OpenScene(WaitingRoomUiInstaller.RoomPath, OpenSceneMode.Single);
            var common = EditorSceneManager.OpenScene(CommonWaitingRoomUI.ScenePath, OpenSceneMode.Additive);
            var ui = common.GetRootGameObjects().Single(r => r.name == "UI");
            if (ui.GetComponentInChildren<WaitingRoomShopUI>(true) != null)
                throw new InvalidOperationException("Already installed. Edit the scene instance.");
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(asset, ui.transform);
            canvas.name = "ShopPanelCanvas";
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvas.GetComponent<Canvas>());
            var binding = canvas.AddComponent<WaitingRoomShopUI>();
            binding.Configure(canvas.GetComponentInChildren<ShopPanelView>(true));
            var phone = room.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopPhone>(true)).Single();
            var serialized = new SerializedObject(phone);
            serialized.FindProperty("showLegacyUi").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var legacy in room.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExpertServicePhonePanel>(true)))
                UnityEngine.Object.DestroyImmediate(legacy);
            EditorSceneManager.MarkSceneDirty(room); EditorSceneManager.MarkSceneDirty(common);
            if (!EditorSceneManager.SaveScene(room) || !EditorSceneManager.SaveScene(common)) throw new InvalidOperationException("Save failed.");
            SceneManager.SetActiveScene(room);
        }
    }
}
#endif
