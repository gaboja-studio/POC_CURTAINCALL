using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>
    /// 2026-10-30 확정 기능 구현안을 씬·에셋에 적용한다. 다시 실행해도 같다.
    /// 타이머 구역, UV 장비형 손전등(입력·기물 목록·로비 배치), 배관함 철창, 거울 3곳, 연습실 서커스 조명·벽면 모니터.
    /// </summary>
    public static class StructureAFeatureUpdate
    {
        const string InputAssetPath = "Assets/Resources/Input/PlayerControls.inputactions";
        const string MonitorMaterialPath = "Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_PracticeMonitor.mat";
        static readonly int[] LobbyUvOffsets = { 41, 42, 43, 44 };

        [MenuItem("CurtainCall/Waiting Room/Install Confirmed Features (UV·Timer·Mirror·Practice)")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || !scene.path.EndsWith("/WaitingRoomBase.unity"))
                throw new InvalidOperationException("Saved WaitingRoomBase EditMode required.");
            StructureADoorVisualFix.Apply(); // 배관함 철창 포함. 저장까지 한다.
            var layout = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install confirmed features");
            try
            {
                var report = new System.Text.StringBuilder();
                report.Append(Timer(layout)).Append(Uv(layout)).Append(Mirrors(layout)).Append(Practice(layout));
                InputAction(); Catalog();
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log("Confirmed features installed:\n" + report);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static string Timer(Transform layout)
        {
            var timer = layout.Find("ExplorationTimer");
            var zones = timer.GetComponent<ExplorationTimerZones>() ?? Undo.AddComponent<ExplorationTimerZones>(timer.gameObject);
            var so = new SerializedObject(zones); so.FindProperty("areas").objectReferenceValue = timer.Find("Frame8Areas"); so.ApplyModifiedProperties();
            return "- timer zones on ExplorationTimer/Frame8Areas\n";
        }

        static string Uv(Transform layout)
        {
            var registry = layout.Find("StructureAExploration");
            if (registry.GetComponent<UvFlashlight>() == null) Undo.AddComponent<UvFlashlight>(registry.gameObject);
            foreach (var mark in layout.GetComponentsInChildren<UvMark>(true))
            { var so = new SerializedObject(mark); so.FindProperty("reach").floatValue = 10f; so.ApplyModifiedProperties(); }

            // 로비: 더미 UV(디버그 4·시험 1) 제거, 새 장비형 UV 4개.
            var lobby = layout.Find("S01_Lobby"); int removed = 0;
            foreach (var spot in lobby.GetComponentsInChildren<FoundItemSpot>(true).Where(s => s.name == "Spot_uv-lamp" || s.name.StartsWith("debug-lobby-uv-lamp")).ToArray())
            { Undo.DestroyObjectImmediate(spot.gameObject); removed++; }
            var used = layout.GetComponentsInChildren<FoundItemSpot>(true).Select(s => new SerializedObject(s).FindProperty("uidOffset").intValue)
                .Concat(layout.GetComponentsInChildren<ExplorationGate>(true).Select(g => g.RewardUidOffset))
                .Concat(layout.GetComponentsInChildren<ToolTarget>(true).Select(t => t.RewardUidOffset)).ToHashSet();
            var group = lobby.Find("LobbyToolSpots") ?? New(lobby, "LobbyToolSpots", lobby.position);
            for (int i = 0; i < 4; i++)
            {
                string id = "lobby-uv-lamp-" + (i + 1);
                var child = group.Find(id);
                if (child == null)
                {
                    if (used.Contains(LobbyUvOffsets[i])) throw new InvalidOperationException("UID offset in use: " + LobbyUvOffsets[i]);
                    child = New(group, id, Vector3.zero);
                }
                Undo.RecordObject(child, "Place lobby UV"); child.position = new Vector3(-3.6f + i * 1f, .02f, 77.2f);
                var spot = child.GetComponent<FoundItemSpot>() ?? Undo.AddComponent<FoundItemSpot>(child.gameObject);
                var so = new SerializedObject(spot);
                so.FindProperty("spotId").stringValue = id; so.FindProperty("uidOffset").intValue = LobbyUvOffsets[i];
                so.FindProperty("itemId").stringValue = ItemUses.UvLamp; so.FindProperty("debugOnly").boolValue = false;
                so.ApplyModifiedProperties();
            }
            return $"- UV: flashlight, mark reach 10, lobby dummy removed {removed}, 4 equipment lamps\n";
        }

        static string Mirrors(Transform layout)
        {
            int n = 0;
            foreach (var r in layout.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.activeInHierarchy && (
                (r.name == "Glass" && r.transform.parent.name.StartsWith("SinkMirror_")) || r.name == "VanityMirror" || r.name == "FullMirror")))
            { if (r.GetComponent<PlanarMirror>() == null) Undo.AddComponent<PlanarMirror>(r.gameObject); n++; }
            return $"- mirrors: {n}\n";
        }

        static string Practice(Transform layout)
        {
            var room = layout.Find("S09_PracticeRoom");
            // 서커스 조명: 링·스테이션 스포트를 빨강·호박·파랑·흰색으로 돌리고 더 밝게.
            var palette = new[] { new Color(1f, .32f, .28f), new Color(1f, .78f, .32f), new Color(.38f, .58f, 1f), new Color(1f, .97f, .9f) };
            int i = 0, lights = 0;
            foreach (var light in room.GetComponentsInChildren<Light>(true))
            {
                if (light.GetComponentInParent<WaitingRoomCctvMonitor>() != null) continue;
                Undo.RecordObject(light, "Circus lighting");
                if (light.name.StartsWith("RingSpot") || light.name == "StationSpot") { light.color = palette[i++ % palette.Length]; light.intensity *= 1.6f; }
                else if (light.name == "HallFill" || light.name == "PerimeterFill" || light.name == "LandingPool") { light.color = new Color(1f, .9f, .78f); light.intensity *= 2f; }
                else continue;
                lights++;
            }
            // 벽면 대형 모니터: 서쪽 둥근 벽 안쪽, 연습실 내부를 비춘다.
            var center = new Vector3(-48.5f, 0f, 112f); const float radius = 26.5f;
            var monitor = room.Find("PracticeMonitor");
            if (monitor == null)
            {
                monitor = New(room, "PracticeMonitor", center + Vector3.left * (radius - .45f) + Vector3.up * 6.5f);
                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube); frame.name = "Frame"; Undo.RegisterCreatedObjectUndo(frame, "Monitor frame");
                frame.transform.SetParent(monitor, false); frame.transform.localScale = new Vector3(.3f, 4.5f, 7.7f);
                frame.GetComponent<Renderer>().sharedMaterial = layout.GetComponentsInChildren<Renderer>(true).First(r => r.sharedMaterial != null && r.sharedMaterial.name == "V4_Iron_Dark").sharedMaterial;
                var screen = GameObject.CreatePrimitive(PrimitiveType.Quad); screen.name = "Screen"; Undo.RegisterCreatedObjectUndo(screen, "Monitor screen");
                Object.DestroyImmediate(screen.GetComponent<Collider>()); screen.transform.SetParent(monitor, false);
                screen.transform.localPosition = new Vector3(.17f, 0, 0); screen.transform.localRotation = Quaternion.LookRotation(Vector3.left); screen.transform.localScale = new Vector3(7.2f, 4.05f, 1f);
                var material = AssetDatabase.LoadAssetAtPath<Material>(MonitorMaterialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "LA_PracticeMonitor" };
                    AssetDatabase.CreateAsset(material, MonitorMaterialPath);
                }
                screen.GetComponent<Renderer>().sharedMaterial = material;
                var mount = New(monitor, "CameraMount", new Vector3(-27f, 8.5f, 112f));
                mount.rotation = Quaternion.LookRotation(center + Vector3.up * 1.5f - mount.position);
                var cctv = Undo.AddComponent<WaitingRoomCctvMonitor>(monitor.gameObject);
                cctv.screen = screen.GetComponent<Renderer>(); cctv.mounts = new[] { mount }; cctv.mountNames = new[] { "연습실" };
                cctv.secondsPerMount = 3600f; cctv.framesPerSecond = 15f; cctv.resolution = new Vector2Int(768, 432); cctv.firstChannel = 9;
            }
            return $"- practice: {lights} circus lights, wall monitor\n";
        }

        static void InputAction()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            var map = asset.FindActionMap("Common", true);
            if (map.FindAction("UvLight") != null) return;
            map.AddAction("UvLight", InputActionType.Button, "<Mouse>/rightButton");
            File.WriteAllText(InputAssetPath, asset.ToJson());
            AssetDatabase.ImportAsset(InputAssetPath);
        }

        static void Catalog()
        {
            var catalog = Resources.Load<ItemCatalog>(ItemCatalog.ResourcePath);
            var so = new SerializedObject(catalog); var items = so.FindProperty("items");
            for (int i = 0; i < items.arraySize; i++)
            {
                var e = items.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("id").stringValue != ItemUses.UvLamp) continue;
                e.FindPropertyRelative("kind").enumValueIndex = (int)ItemKind.Equipment;
                e.FindPropertyRelative("newUses").intValue = 0; e.FindPropertyRelative("foundUses").intValue = 0;
                e.FindPropertyRelative("wornName").stringValue = ""; e.FindPropertyRelative("wornAtOrBelow").intValue = -1;
                e.FindPropertyRelative("summary").stringValue = "장비. 슬롯에 넣으면 자동 적용. 우클릭을 누르는 동안 보라색 손전등으로 앞을 비추고, 빛에 닿은 숨겨진 낙서가 노란 형광으로 드러남. 내구도 없음.";
            }
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        static Transform New(Transform parent, string name, Vector3 at)
        { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Add " + name); go.transform.SetParent(parent, false); go.transform.position = at; return go.transform; }
    }
}
