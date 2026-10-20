#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.UI.WorldLabelsV2;
using CurtainCall.WaitingRoom;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class WaitingRoomWorldLabelInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Install waiting room W4 W5")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Editor must be idle.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(WaitingRoomUiInstaller.RoomPath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Open the saved WaitingRoomBase first.");
            if (scene.GetRootGameObjects().Any(g => g.GetComponentInChildren<WaitingRoomWorldLabels>(true) != null))
                throw new InvalidOperationException("Already installed. Adjust existing UI/WorldLabels instances.");
            var ui = scene.GetRootGameObjects().First(g => g.name == "UI");
            var root = new GameObject("WorldLabels"); root.transform.SetParent(ui.transform, false);
            var binding = root.AddComponent<WaitingRoomWorldLabels>();
            var fonts = root.AddComponent<UiRuntimeFontFallback>();
            fonts.Configure(Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF"));
            fonts.ConfigureSources(AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-Medium.ttf"),
                AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/Pretendard-SemiBold.ttf"));
            var items = new GameObject("Items"); items.transform.SetParent(root.transform, false);
            T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();
            var expert = Find<ExpertRoomView>(); var shelf = Find<HandoverShelfView>();
            var old = new SerializedObject(expert).FindProperty("sign").objectReferenceValue as TextMesh;
            if (old == null) throw new InvalidOperationException("Expert sign anchor is required.");
            var sign = Spawn("WorldLabelCloth", "W4_ExpertStatus", root.transform);
            sign.transform.SetPositionAndRotation(new Vector3(9.6f, 3.33f, 76.5f), old.transform.rotation);
            sign.transform.localScale = Vector3.one * .003f;
            sign.TrySetSize(new Vector2(800, 96));
            if (!sign.TrySetContent("밤 정비 때 영업")) throw new InvalidOperationException(sign.LastIssue);
            var expertData = new SerializedObject(expert);
            expertData.FindProperty("sign").objectReferenceValue = null;
            expertData.ApplyModifiedPropertiesWithoutUndo();
            Undo.DestroyObjectImmediate(old.gameObject);
            var boards = shelf.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Board_")).OrderBy(t => t.name).ToArray();
            if (boards.Length != 4) throw new InvalidOperationException("Four shelf boards required.");
            var shelfData = new SerializedObject(shelf);
            var shelfVisual = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "HandoverShelf_Visual");
            shelfData.FindProperty("visualRoot").objectReferenceValue = shelfVisual;
            var boardField = shelfData.FindProperty("boards"); boardField.arraySize = boards.Length;
            for (int i = 0; i < boards.Length; i++) boardField.GetArrayElementAtIndex(i).objectReferenceValue = boards[i];
            shelfData.ApplyModifiedPropertiesWithoutUndo();
            var slots = new WorldLabelView[boards.Length];
            for (int i = 0; i < boards.Length; i++)
            {
                slots[i] = Spawn("WorldLabelShelf", "W5_ShelfSlot_" + (i + 1), root.transform);
                var bounds = boards[i].GetComponent<Renderer>().bounds;
                slots[i].transform.position = new Vector3(bounds.min.x + .3f, bounds.center.y, shelfVisual.GetComponent<Collider>().bounds.max.z + .05f);
                slots[i].TrySetContent((i + 1) + "칸");
            }
            var so = new SerializedObject(binding);
            void Ref(string field, UnityEngine.Object value) => so.FindProperty(field).objectReferenceValue = value;
            Ref("expert", expert); Ref("expertLabel", sign); Ref("shelf", shelf);
            Ref("floor", Find<FloorPropsView>()); Ref("delivery", Find<ShopDeliverySlot>()); Ref("held", Find<HeldPropsView>());
            Ref("itemPrefab", Require("WorldLabelItem").GetComponent<WorldLabelView>());
            Ref("itemsRoot", items.transform); Ref("fonts", fonts);
            var slotField = so.FindProperty("shelfLabels"); slotField.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) slotField.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var view in new Component[] { Find<FloorPropsView>(), Find<ShopDeliverySlot>(), shelf, Find<HeldPropsView>() })
            {
                var viewData = new SerializedObject(view); viewData.FindProperty("showLegacyLabels").boolValue = false;
                viewData.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        static GameObject Require(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/WorldLabels/" + name + ".prefab")
            ?? throw new InvalidOperationException("Missing completed prefab: " + name);
        static WorldLabelView Spawn(string prefab, string name, Transform root)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Require(prefab), root);
            go.name = name;
            var view = go.GetComponent<WorldLabelView>();
            view.SetVisible(true);
            return view;
        }
    }
}
#endif
