#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.WaitingRoom;
using CurtainCall.UI.ScheduleV1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class WaitingRoomScheduleInstaller
    {
        [MenuItem("Tools/Curtain Call UI/Install waiting room schedule")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be idle.");
            SceneUiLayoutPreview.Stop();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            var room = EditorSceneManager.OpenScene(WaitingRoomUiInstaller.RoomPath, OpenSceneMode.Single);
            var common = EditorSceneManager.OpenScene(CommonWaitingRoomUI.ScenePath, OpenSceneMode.Additive);
            var ui = common.GetRootGameObjects().Single(r => r.name == "UI");
            if (ui.GetComponentInChildren<WaitingRoomScheduleUI>(true) != null) throw new InvalidOperationException("Already installed.");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/ScheduleUi/SchedulePanel.prefab");
            if (asset == null) throw new InvalidOperationException("Missing tested SchedulePanel prefab.");
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(asset, ui.transform);
            canvas.name = "SchedulePanelCanvas";
            canvas.GetComponent<Canvas>().sortingOrder = 110;
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvas.GetComponent<Canvas>());
            var view = canvas.GetComponentInChildren<SchedulePanelView>(true);
            var stage = view.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "10_LayoutStage");
            var edits = CardButtons(view);
            var confirm = Button(stage, "ConfirmSchedule", "Confirm", 1072, 736, 192);
            var ready = Button(stage, "Ready", "General", 1280, 736, 176);
            var footer = view.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "61_FooterTMP");
            footer.rectTransform.sizeDelta = new Vector2(368, 56);
            PrefabUtility.RecordPrefabInstancePropertyModifications(footer.rectTransform);
            canvas.AddComponent<WaitingRoomScheduleUI>().Configure(view, edits, confirm, ready);
            var board = room.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ScheduleBoardView>(true)).Single();
            var serialized = new SerializedObject(board);
            serialized.FindProperty("showLegacyUi").boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(room); EditorSceneManager.MarkSceneDirty(common);
            if (!EditorSceneManager.SaveScene(room) || !EditorSceneManager.SaveScene(common)) throw new InvalidOperationException("Save failed.");
            SceneManager.SetActiveScene(room);
        }
        public static void UseClickableCards()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be idle.");
            SceneUiLayoutPreview.Stop();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            var common = SceneManager.GetSceneByPath(CommonWaitingRoomUI.ScenePath);
            if (!common.isLoaded) common = EditorSceneManager.OpenScene(CommonWaitingRoomUI.ScenePath, OpenSceneMode.Additive);
            var adapter = common.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaitingRoomScheduleUI>(true)).Single();
            var serialized = new SerializedObject(adapter);
            var edits = serialized.FindProperty("editButtons");
            for (int i = 0; i < edits.arraySize; i++)
            {
                var old = edits.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.UI.Button;
                if (old != null && old.name.StartsWith("EditDay", StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var cards = CardButtons(adapter.View);
            serialized.Update(); edits = serialized.FindProperty("editButtons"); edits.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++) edits.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(common);
            if (!EditorSceneManager.SaveScene(common)) throw new InvalidOperationException("Save failed.");
        }
        static UnityEngine.UI.Button[] CardButtons(SchedulePanelView view)
        {
            var layers = new SerializedObject(view).FindProperty("dayLayers");
            var buttons = new UnityEngine.UI.Button[6];
            for (int day = 0; day < 3; day++)
                for (int slot = 0; slot < 2; slot++)
                {
                    var card = (ScheduleCardVisual)layers.GetArrayElementAtIndex(day).FindPropertyRelative("cards").GetArrayElementAtIndex(slot).objectReferenceValue;
                    var body = card.transform.Find("00_Body").GetComponent<UnityEngine.UI.Image>();
                    body.raycastTarget = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                    var button = card.GetComponent<UnityEngine.UI.Button>() ?? card.gameObject.AddComponent<UnityEngine.UI.Button>();
                    button.targetGraphic = body;
                    var colors = UnityEngine.UI.ColorBlock.defaultColorBlock;
                    colors.highlightedColor = new Color(.88f, .92f, .96f, 1);
                    colors.selectedColor = colors.highlightedColor;
                    colors.pressedColor = new Color(.75f, .82f, .9f, 1);
                    colors.disabledColor = Color.white; // Locked entries remain readable.
                    button.colors = colors;
                    button.interactable = false;
                    buttons[day * 2 + slot] = button;
                }
            return buttons;
        }
        static UnityEngine.UI.Button Button(Transform parent, string name, string kind, float x, float y, float width)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Prefabs/UIs/CommonButtons/Button_{kind}.prefab");
            if (prefab == null) throw new InvalidOperationException("Missing tested button prefab.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, 48);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            var label = go.GetComponentInChildren<TMP_Text>(true); label.fontSize = 22;
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            return go.GetComponent<UnityEngine.UI.Button>();
        }
    }
}
#endif
