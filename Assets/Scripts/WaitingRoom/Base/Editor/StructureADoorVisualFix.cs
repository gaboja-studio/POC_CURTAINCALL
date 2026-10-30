using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>탈출로 키패드 숨김(CC-182), 문 열림 시 부속 숨김, 계단 위 지붕 보(CC-183), 숨겨진 문·열쇠 구멍. 다시 실행해도 같다.</summary>
    public static class StructureADoorVisualFix
    {
        [MenuItem("CurtainCall/Waiting Room/Fix Door Visuals And Stair Girder")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || !scene.path.EndsWith("/WaitingRoomBase.unity"))
                throw new InvalidOperationException("Saved WaitingRoomBase EditMode required.");
            var layout = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Fix door visuals and stair girder");
            try
            {
                Lists(layout.Find("B1_EscapeEntrance/Exploration_A/escapeDoor"), layout.Find("B1_EscapeEntrance/SealedDoor"));
                Lists(layout.Find("ST_Stairwell/Exploration_A/stairsDoor"), layout.Find("ST_Stairwell/StairwellDoorway"));

                // 위층 계단(z104.6~107.05) 머리 위 0.7m를 지나던 지붕 보를 비행 사이 가림벽(z104.5)까지만 남긴다.
                const float guardZ = 104.5f;
                var girders = layout.Find("ST_Stairwell/ST_Stairwell_B1Level").GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.name.StartsWith("RoofGirder") && Mathf.Abs(r.bounds.center.x - 8.24f) < .2f && r.bounds.max.z > guardZ + .01f).ToArray();
                foreach (var girder in girders)
                {
                    var b = girder.bounds; var t = girder.transform; Undo.RecordObject(t, "Trim stair roof girder");
                    var localZ = t.InverseTransformDirection(Vector3.forward); var scale = t.localScale; float ratio = (guardZ - b.min.z) / b.size.z;
                    if (Mathf.Abs(localZ.z) > .9f) scale.z *= ratio; else if (Mathf.Abs(localZ.x) > .9f) scale.x *= ratio; else throw new InvalidOperationException("Unexpected girder orientation.");
                    t.localScale = scale; t.position += Vector3.forward * ((b.min.z + guardZ) * .5f - b.center.z);
                }
                EscapeSetup(layout);
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log($"Keypad kept visible, door parts other than leaves/locks stay after opening, {girders.Length} stair girder parts trimmed to z{guardZ}, escape hidden panel and key slots set.");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        // 기획: 키패드 정답 → 벽처럼 보이던 숨겨진 문이 옆으로 밀림 → 탈출로 문 양옆 열쇠 구멍에서 두 명이 동시에 F.
        static void EscapeSetup(Transform layout)
        {
            var group = layout.Find("B1_EscapeEntrance/Exploration_A"); var gate = group.Find("escapeDoor");
            var assembly = layout.Find("B1_EscapeEntrance/SealedDoor"); var frame = assembly.Find("DoorFrame").GetComponent<Renderer>();
            var fb = frame.bounds; var iron = frame.sharedMaterial; var brass = assembly.Find("LockWheel").GetComponent<Renderer>().sharedMaterial;
            var wall = layout.Find("B1_EscapeEntrance").GetComponentsInChildren<Renderer>(true).First(r => r.name == "WallSurfaces").sharedMaterial;
            var keypad = assembly.Find("Keypad").GetComponent<Renderer>().bounds;

            // 문틀 자리를 벽 재질 판으로 막는다. 키패드는 판 바깥(앞)에 남는다.
            if (group.Find("HiddenPanel") == null)
            {
                float z0 = keypad.max.z + .02f;
                var panel = Cube(group, "HiddenPanel", new Vector3(fb.center.x, fb.center.y, (z0 + fb.max.z) * .5f), new Vector3(fb.size.x, fb.size.y, fb.max.z - z0), wall);
                var slide = Undo.AddComponent<ExplorationSlide>(panel.gameObject); var so = new SerializedObject(slide);
                so.FindProperty("openedBy").stringValue = StructureAExitRoute.PasswordPad;
                so.FindProperty("openOffset").vector3Value = group.InverseTransformVector(Vector3.forward * (fb.max.z - z0 + .05f));
                so.ApplyModifiedProperties();
            }
            // 문틀 양쪽 기둥 앞, 손 높이 1.1m.
            var slots = new[] { ("KeySlot_L", fb.min.z + .15f), ("KeySlot_R", fb.max.z - .15f) }.Select(s =>
            {
                var existing = gate.Find(s.Item1); if (existing != null) return existing;
                var slot = new GameObject(s.Item1).transform; Undo.RegisterCreatedObjectUndo(slot.gameObject, "Add key slot");
                slot.SetParent(gate, false); slot.SetPositionAndRotation(new Vector3(fb.max.x + .03f, gate.position.y + 1.1f, s.Item2), Quaternion.identity);
                Cube(slot, "SlotPlate", slot.position, new Vector3(.04f, .22f, .14f), iron);
                Cube(slot, "Keyhole", slot.position + new Vector3(.025f, 0, 0), new Vector3(.012f, .07f, .025f), brass);
                return slot;
            }).ToArray();

            // 사슬 자물쇠 장식은 열쇠 구멍으로 대체한다. 리벳·관찰창은 문짝과 함께 숨긴다.
            var detail = layout.Find("ST_Stairwell/EnvironmentDetail/SealedExit");
            string[] chain = { "HaspPlate", "HaspStaple", "PadlockBody", "Shackle", "ChainLink", "WallEye" };
            foreach (var r in detail.GetComponentsInChildren<Renderer>(true).Where(r => chain.Contains(r.name)))
            { Undo.RecordObject(r.gameObject, "Replace chain lock with key slots"); r.gameObject.SetActive(false); }
            var face = detail.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Rivet" || r.name.StartsWith("Peep")).Select(r => (Object)r.gameObject).ToArray();

            var gso = new SerializedObject(gate.GetComponent<ExplorationGate>());
            var keys = gso.FindProperty("keySlots"); keys.arraySize = 2;
            for (int i = 0; i < 2; i++) keys.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            Add(gso.FindProperty("hiddenVisuals"), face.Concat(slots.Select(s => (Object)s.gameObject)).Append(frame.gameObject));
            Add(gso.FindProperty("hideOnOpen"), face);
            gso.ApplyModifiedProperties();
        }
        static Transform Cube(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(at, Quaternion.identity);
            go.transform.localScale = size; // 월드 축으로 세운 상자. 부모 배율은 1이다.
            go.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
        }
        static void Add(SerializedProperty list, System.Collections.Generic.IEnumerable<Object> items)
        {
            var all = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue).Concat(items).Where(o => o != null).Distinct().ToArray();
            list.arraySize = all.Length; for (int i = 0; i < all.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        }
        static void Lists(Transform gate, Transform assembly)
        {
            var so = new SerializedObject(gate.GetComponent<ExplorationGate>());
            Filter(so.FindProperty("hiddenVisuals"), o => !o.name.StartsWith("Keypad"));
            Filter(so.FindProperty("hideOnOpen"), o => !(o is GameObject g) || !g.transform.IsChildOf(assembly) || StructureAPlacement.OpensAway(g.transform, assembly));
            so.ApplyModifiedProperties();
        }
        static void Filter(SerializedProperty list, Func<Object, bool> keep)
        {
            var kept = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue).Where(o => o != null && keep(o)).ToArray();
            list.arraySize = kept.Length; for (int i = 0; i < kept.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
        }
    }
}
