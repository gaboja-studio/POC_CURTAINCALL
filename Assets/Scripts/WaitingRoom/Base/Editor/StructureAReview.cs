using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>실제 씬 검토용 좌표 추출과 사용자 요청 디버그 보급품 배치.</summary>
    public static class StructureAReview
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        static Transform Layout()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open WaitingRoomBase in EditMode.");
            return scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
        }
        static float[] V(Vector3 p) => new[] { p.x, p.y, p.z };
        static string PathOf(Transform t, Transform root) => AnimationUtility.CalculateTransformPath(t, root);

        [MenuItem("CurtainCall/Waiting Room/Install Debug Lobby Supplies")]
        public static void InstallDebugSupplies()
        {
            var layout = Layout();
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current edits first.");
            var lobby = layout.Find("S01_Lobby");
            var group = lobby.Find("DebugExplorationSupplies");
            if (group == null)
            {
                var go = new GameObject("DebugExplorationSupplies");
                Undo.RegisterCreatedObjectUndo(go, "Add debug supplies");
                go.transform.SetParent(lobby, false); group = go.transform;
            }
            var kinds = new[] { ItemUses.StakePuller, ItemUses.MagicianHairpin, ItemUses.UvLamp };
            for (int row = 0; row < 3; row++) for (int col = 0; col < 4; col++)
            {
                string id = "debug-lobby-" + kinds[row] + "-" + (col + 1);
                var child = group.Find(id);
                if (child == null)
                {
                    var go = new GameObject(id); Undo.RegisterCreatedObjectUndo(go, "Add debug supply");
                    go.transform.SetParent(group, false); child = go.transform;
                }
                Undo.RecordObject(child, "Position debug supply");
                child.position = new Vector3(-8f + col * .9f, .02f, 74.4f + row * .9f);
                var spot = child.GetComponent<FoundItemSpot>() ?? Undo.AddComponent<FoundItemSpot>(child.gameObject);
                var so = new SerializedObject(spot);
                so.FindProperty("spotId").stringValue = id; so.FindProperty("uidOffset").intValue = 61 + row * 4 + col;
                so.FindProperty("itemId").stringValue = kinds[row]; so.FindProperty("debugOnly").boolValue = true;
                so.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Debug lobby supplies saved: 4 stake pullers, 4 hairpins, 4 UV lamps. Editor/Development Build only.");
        }

        [MenuItem("CurtainCall/Waiting Room/Fix Prop Room Doorway Floor")]
        public static void FixDoorwayFloor()
        {
            var root = Layout();
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current edits first.");
            var shell = root.Find("S04_PropRoom/Shell");
            var part = shell.Find("DoorwayFloorInfill");
            if (part == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "DoorwayFloorInfill"; Undo.RegisterCreatedObjectUndo(go, "Fill prop room doorway visual floor gap");
                go.transform.SetParent(shell, false); part = go.transform;
                // Existing floor colliders already meet at y=0. Only the missing visible surface is filled.
                Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
            }
            Undo.RecordObject(part, "Fit doorway floor surface");
            part.rotation = Quaternion.identity; part.position = new Vector3(-8.2f, -.05f, 95.65f); part.localScale = new Vector3(3.4f, .1f, .5f);
            var renderer = part.GetComponent<Renderer>(); Undo.RecordObject(renderer, "Reuse prop room floor finish");
            renderer.sharedMaterial = shell.Find("Floor_BrokenConcrete").GetComponent<Renderer>().sharedMaterial;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Prop room doorway: filled visible floor gap z95.4–95.9, existing continuous collision retained.");
        }

        [MenuItem("CurtainCall/Waiting Room/Export Current Exploration Map")]
        public static void Export()
        {
            var root = Layout();
            var data = new
            {
                scene = ScenePath,
                exportedAt = DateTimeOffset.Now.ToString("o"),
                rooms = root.Cast<Transform>().Where(t => t.name.StartsWith("S") || t.name.StartsWith("B1_") || t.name.StartsWith("LG_") || t.name.StartsWith("P05") || t.name.StartsWith("WC_")).Select(t => new { name = t.name, p = V(t.position) }).ToArray(),
                colliders = root.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger).Select(c => new { path = PathOf(c.transform, root), type = c.GetType().Name, p = V(c.bounds.center), size = V(c.bounds.size) }).ToArray(),
                renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => new { path = PathOf(r.transform, root), p = V(r.bounds.center), size = V(r.bounds.size) }).ToArray(),
                targets = root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c is IExplorationTarget).Select(c => { var target = (IExplorationTarget)c; return new { id = target.TargetId, type = c.GetType().Name, path = PathOf(c.transform, root), p = V(c.transform.position), active = c.gameObject.activeInHierarchy }; }).ToArray(),
                spots = root.GetComponentsInChildren<FoundItemSpot>(true).Select(s => new { id = s.SpotId, item = s.MakeItem(0).ItemId, uid = s.MakeItem(0).Uid, p = V(s.FloorPoint), path = PathOf(s.transform, root), debug = new SerializedObject(s).FindProperty("debugOnly").boolValue }).ToArray()
            };
            Directory.CreateDirectory(".harness/local");
            File.WriteAllText(".harness/local/structure-a-current-map.json", JsonConvert.SerializeObject(data, Formatting.Indented));
            Debug.Log("Current map exported from saved scene transforms, renderers and colliders.");
        }
    }
}
