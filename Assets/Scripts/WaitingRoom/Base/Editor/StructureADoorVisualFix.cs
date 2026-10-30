using System;
using System.Collections.Generic;
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
        // 기획(2026-10-30 사용자): 기계실 숨겨진 문은 벽처럼 보이고, 앞 배관함을 말뚝 뽑개 3타로 부수면 키패드가 드러난다.
        // 정답이면 숨겨진 문이 옆으로 밀려 탈출 복도가 열리고, 복도 끝 탈출로 문 양옆 열쇠 구멍에서 두 명이 동시에 F.
        static void EscapeSetup(Transform layout)
        {
            var group = layout.Find("B1_EscapeEntrance/Exploration_A"); var gate = group.Find("escapeDoor");
            var assembly = layout.Find("B1_EscapeEntrance/SealedDoor"); var frame = assembly.Find("DoorFrame").GetComponent<Renderer>();
            var fb = frame.bounds; var iron = frame.sharedMaterial; var brass = assembly.Find("LockWheel").GetComponent<Renderer>().sharedMaterial;
            float floor = gate.position.y;

            // 1차 배치(탈출로 문 자리의 벽 판·배관함)를 걷어 낸다. 탈출로 문은 숨은 문 너머라 처음부터 보인다.
            foreach (var old in new[] { "HiddenPanel", "EscapePipeBox" }) if (group.Find(old) != null) Undo.DestroyObjectImmediate(group.Find(old).gameObject);
            MachineSetup(layout, iron, assembly.Find("DoorRib").GetComponent<Renderer>().sharedMaterial, assembly);

            // 문틀 양쪽 기둥 앞, 손 높이 1.15m. 정답 뒤 문과 함께 드러난다.
            var slots = new[] { ("KeySlot_L", fb.min.z + .15f), ("KeySlot_R", fb.max.z - .15f) }.Select(s =>
            {
                var slot = gate.Find(s.Item1);
                if (slot != null && slot.Find("Lamp") != null) return slot;
                if (slot != null) Undo.DestroyObjectImmediate(slot.gameObject);
                slot = new GameObject(s.Item1).transform; Undo.RegisterCreatedObjectUndo(slot.gameObject, "Add key slot");
                slot.SetParent(gate, false); slot.SetPositionAndRotation(new Vector3(fb.max.x + .02f, floor + 1.15f, s.Item2), Quaternion.identity);
                var at = slot.position;
                Cube(slot, "SlotPlate", at, new Vector3(.04f, .34f, .22f), iron);
                Cube(slot, "SlotRing", at + new Vector3(.03f, -.02f, 0), new Vector3(.03f, .13f, .13f), brass);
                Cube(slot, "Keyhole", at + new Vector3(.05f, -.02f, 0), new Vector3(.012f, .08f, .025f), iron);
                Cube(slot, "Lamp", at + new Vector3(.035f, .12f, 0), new Vector3(.03f, .045f, .045f), assembly.parent.GetComponentsInChildren<Renderer>(true).First(r => r.name == "BeaconGlass").sharedMaterial);
                return slot;
            }).ToArray();

            // 사슬 자물쇠 장식은 열쇠 구멍으로 대체한다. 리벳·관찰창은 문짝과 함께 숨긴다.
            var detail = layout.Find("ST_Stairwell/EnvironmentDetail/SealedExit");
            string[] chain = { "HaspPlate", "HaspStaple", "PadlockBody", "Shackle", "ChainLink", "WallEye" };
            foreach (var r in detail.GetComponentsInChildren<Renderer>(true).Where(r => chain.Contains(r.name)))
            { Undo.RecordObject(r.gameObject, "Replace chain lock with key slots"); r.gameObject.SetActive(false); }
            var face = detail.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Rivet" || r.name.StartsWith("Peep")).Select(r => (Object)r.gameObject).ToArray();

            var gso = new SerializedObject(gate.GetComponent<ExplorationGate>());
            gso.FindProperty("revealTarget").stringValue = ""; gso.FindProperty("revealPending").boolValue = false;
            var keys = gso.FindProperty("keySlots"); keys.arraySize = 2;
            for (int i = 0; i < 2; i++) keys.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            Add(gso.FindProperty("hiddenVisuals"), face.Concat(slots.Select(s => (Object)s.gameObject)).Append(frame.gameObject));
            Add(gso.FindProperty("hideOnOpen"), face);
            gso.ApplyModifiedProperties();
        }
        static void MachineSetup(Transform layout, Material iron, Material rusty, Transform keypadOwner)
        {
            var group = layout.Find("B1_MachineRoom/Exploration_A"); var door = group.Find("machineDoor");
            var gate = door.GetComponent<ExplorationGate>(); var doorway = door.GetComponent<BoxCollider>().bounds; float floor = door.position.y;
            // 기계실 쪽 벽면(x)과 문 양옆 기둥. 기둥은 열리기 전 숨겨 벽만 보이게 한다.
            if (!Physics.Raycast(new Vector3(door.position.x + 3f, floor + .5f, doorway.min.z - 1f), Vector3.left, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Machine room wall not found.");
            float face = hit.point.x;
            // 문 테두리: 양옆 기둥·위 인방은 열리기 전 숨긴다. 이 층만(위 캣워크 층 제외).
            bool AtDoor(Renderer r) => Mathf.Abs(r.bounds.center.x - door.position.x) < .5f && r.bounds.min.y > floor - .5f && r.bounds.min.y < floor + 4.5f && r.bounds.center.z > doorway.min.z - .4f && r.bounds.center.z < doorway.max.z + .4f;
            var reveals = layout.GetComponentsInChildren<Renderer>(true).Where(r => (r.name == "DoorReveal" || r.name == "RevealLintel") && AtDoor(r)).ToArray();
            var jambs = reveals.Where(r => r.name == "DoorReveal").ToArray();
            float z0 = jambs.Min(r => r.bounds.min.z), z1 = jambs.Max(r => r.bounds.max.z), top = jambs.Max(r => r.bounds.max.y);
            // 바닥 문턱은 벽 두께 구간의 바닥(충돌 포함)이라 남기고, 기계실 쪽으로 튀어나온 부분만 벽면까지 줄인다.
            foreach (var sill in layout.GetComponentsInChildren<Renderer>(true).Where(r => (r.name == "DoorSill" || r.name == "HiddenPassageThreshold") && AtDoor(r) && r.bounds.max.x > face + .01f))
            {
                var b = sill.bounds; var t = sill.transform; Undo.RecordObject(t, "Trim sill to wall face");
                var localX = t.InverseTransformDirection(Vector3.right); var scale = t.localScale; float ratio = (face - b.min.x) / b.size.x;
                if (Mathf.Abs(localX.x) > .9f) scale.x *= ratio; else if (Mathf.Abs(localX.z) > .9f) scale.z *= ratio; else throw new InvalidOperationException("Unexpected sill orientation.");
                t.localScale = scale; t.position += Vector3.right * ((b.min.x + face) * .5f - b.center.x);
            }
            var wall = layout.Find("B1_MachineRoom").GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "WallSurfaces" && r.gameObject.activeInHierarchy && Mathf.Abs(r.bounds.min.x - face) < .05f && r.bounds.min.y <= floor + .01f);

            if (group.Find("HiddenPanel") == null)
            {
                var panel = WallPanel(group, wall, new Vector3(face + .01f, floor, (z0 + z1) * .5f), z0, z1, floor, top);
                var slide = new SerializedObject(Undo.AddComponent<ExplorationSlide>(panel.gameObject));
                slide.FindProperty("openedBy").stringValue = StructureAExitRoute.PasswordPad;
                slide.FindProperty("openOffset").vector3Value = group.InverseTransformVector(Vector3.back * (z1 - z0 + .05f)); // 왼쪽 빈 벽으로
                slide.ApplyModifiedProperties();
            }
            var so = new SerializedObject(gate);
            so.FindProperty("requiredTargets").arraySize = 0; so.FindProperty("revealTarget").stringValue = ""; so.FindProperty("revealPending").boolValue = false;
            so.FindProperty("openedBy").stringValue = StructureAExitRoute.PasswordPad;
            Add(so.FindProperty("showOnOpen"), reveals.Select(r => (Object)r.gameObject));
            so.ApplyModifiedProperties();
            // 문 테두리(기둥)는 저장 상태부터 꺼 두어 편집 화면에서도 벽만 보이게 한다. 열리면 showOnOpen이 켠다.
            foreach (var r in reveals.Where(r => r.gameObject.activeSelf)) { Undo.RecordObject(r.gameObject, "Hide hidden-door jambs"); r.gameObject.SetActive(false); }
            // 벽을 따라가는 강철 띠가 문 자리에서 끊겨 문이 드러난다. 끊긴 구간을 이어 둔다(2.4m 높이, 충돌 없음).
            if (group.Find("HiddenDoorLedger") == null)
            {
                var ledgers = layout.Find("B1_MachineRoom").GetComponentsInChildren<Renderer>(true).Where(r => r.name == "SteelLedger" && r.gameObject.activeInHierarchy
                    && Mathf.Abs(r.bounds.max.x - face) < .2f && r.bounds.max.z > z0 - 12f && r.bounds.min.z < z1 + 12f).ToArray();
                var left = ledgers.Where(r => r.bounds.max.z <= z0 + .05f).OrderByDescending(r => r.bounds.max.z).FirstOrDefault();
                var right = ledgers.Where(r => r.bounds.min.z >= z1 - .05f).OrderBy(r => r.bounds.min.z).FirstOrDefault();
                var sample = right ?? left;
                if (sample != null)
                {
                    float a = left != null ? left.bounds.max.z : z0, b = right != null ? right.bounds.min.z : z1; var sb = sample.bounds;
                    Cube(group, "HiddenDoorLedger", new Vector3(sb.center.x, sb.center.y, (a + b) * .5f), new Vector3(sb.size.x, sb.size.y, b - a), sample.sharedMaterial);
                }
            }
            // 문 오른쪽 빈 벽 앞 배관함.
            PipeBox(group, face, z1 + .2f, z1 + 1.35f, floor, iron, rusty, keypadOwner, layout.GetComponentsInChildren<ExplorationGate>(true).Single(g => g.name == "passwordPad").transform);
        }
        // 벽 앞 배관함. 천막 말뚝 뽑개 3타로 배관이 무너지면 안쪽 키패드를 쓸 수 있다.
        const string PipeBoxId = "structure-a:machine-pipe-box";
        static void PipeBox(Transform group, float wallX, float z0, float z1, float floor, Material iron, Material rusty, Transform keypadOwner, Transform pad)
        {
            float zc = (z0 + z1) * .5f, top = floor + 1.9f, depth = .42f;
            if (group.Find("PipeBox") is Transform built) { Lid(built, wallX, z0, z1, floor, top, depth, iron); return; }
            var root = new GameObject("PipeBox").transform; Undo.RegisterCreatedObjectUndo(root.gameObject, "Add pipe box");
            root.SetParent(group, false); root.SetPositionAndRotation(new Vector3(wallX, floor, zc), Quaternion.Euler(0, 90, 0)); // 로컬 z = 방 안쪽
            // 함 몸체: 위·양옆·바닥 턱은 막고 앞은 배관으로 가린다.
            foreach (var (name, at, size) in new[] {
                ("BoxTop", new Vector3(wallX + depth * .5f, top - .02f, zc), new Vector3(depth, .04f, z1 - z0)),
                ("BoxSideL", new Vector3(wallX + depth * .5f, (floor + top) * .5f, z0 + .02f), new Vector3(depth, top - floor, .04f)),
                ("BoxSideR", new Vector3(wallX + depth * .5f, (floor + top) * .5f, z1 - .02f), new Vector3(depth, top - floor, .04f)),
                ("BoxPlinth", new Vector3(wallX + depth * .5f, floor + .05f, zc), new Vector3(depth, .1f, z1 - z0)) })
                Cube(root, name, at, size, iron, true);
            Cube(root, "BoxBack", new Vector3(wallX + .015f, (floor + top) * .5f, zc), new Vector3(.03f, top - floor, z1 - z0), rusty);
            var pieces = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pipe.name = "Pipe"; Undo.RegisterCreatedObjectUndo(pipe, "Add pipe");
                pipe.transform.SetParent(root, false); pipe.transform.SetPositionAndRotation(new Vector3(wallX + depth - .1f, (floor + .1f + top - .04f) * .5f, Mathf.Lerp(z0 + .17f, z1 - .17f, i / 3f)), Quaternion.identity);
                pipe.transform.localScale = new Vector3(.11f, (top - .04f - floor - .1f) * .5f, .11f);
                pipe.GetComponent<Renderer>().sharedMaterial = rusty; Object.DestroyImmediate(pipe.GetComponent<Collider>()); pieces.Add(pipe.transform);
            }
            foreach (float y in new[] { floor + .55f, floor + 1.35f })
                pieces.Add(Cube(root, "PipeClamp", new Vector3(wallX + depth - .03f, y, zc), new Vector3(.04f, .06f, z1 - z0 - .08f), iron));

            // 기존 키패드와 패드 대상을 함 안쪽 뒤판으로 옮긴다(받침판 포함).
            var keys = keypadOwner.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Keypad")).Select(r => r.transform).ToArray();
            var kb = keys.Single(k => k.name == "Keypad").GetComponent<Renderer>().bounds;
            var delta = new Vector3(wallX + .03f + kb.extents.x, floor + 1.15f, zc) - kb.center;
            foreach (var key in keys) { Undo.SetTransformParent(key, root, "Move keypad into pipe box"); key.position += delta; }
            Undo.SetTransformParent(pad, group, "Move pad into pipe box"); pad.position += delta;

            var wall = Undo.AddComponent<BreakableWall>(root.gameObject); var so = new SerializedObject(wall);
            so.FindProperty("targetId").stringValue = PipeBoxId; so.FindProperty("label").stringValue = "배관함";
            so.FindProperty("requiredHits").intValue = 3; so.FindProperty("reach").floatValue = 1.7f;
            var list = so.FindProperty("pieces"); list.arraySize = pieces.Count;
            for (int i = 0; i < pieces.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            so.ApplyModifiedProperties();
            var gate = new SerializedObject(pad.GetComponent<ExplorationGate>());
            gate.FindProperty("revealTarget").stringValue = PipeBoxId; gate.FindProperty("revealPending").boolValue = false;
            gate.ApplyModifiedProperties();
            Lid(root, wallX, z0, z1, floor, top, depth, iron);
        }
        // 앞 철창(2026-10-30 확정): 키패드·배관을 가린다. 부서질 때 배관과 함께 떨어지는 조각이다. 이전 판 뚜껑은 철창으로 바꾼다.
        static void Lid(Transform root, float wallX, float z0, float z1, float floor, float top, float depth, Material iron)
        {
            var so = new SerializedObject(root.GetComponent<BreakableWall>()); var list = so.FindProperty("pieces");
            var pieces = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as Transform).Where(t => t != null).ToList();
            var lid = root.Find("BoxLid");
            if (lid != null) { pieces.Remove(lid); Undo.DestroyObjectImmediate(lid.gameObject); }
            if (root.Find("GratingBar") == null)
            {
                float x = wallX + depth + .015f, y0 = floor + .1f, y1 = top - .04f, yc = (y0 + y1) * .5f, zc = (z0 + z1) * .5f;
                foreach (var (z, h) in new[] { (z0 + .02f, y1 - y0), (z1 - .02f, y1 - y0) }) pieces.Add(Cube(root, "GratingBar", new Vector3(x, yc, z), new Vector3(.03f, h, .04f), iron));
                foreach (var y in new[] { y0 + .02f, yc, y1 - .02f }) pieces.Add(Cube(root, "GratingBar", new Vector3(x, y, zc), new Vector3(.03f, .04f, z1 - z0), iron));
                for (int i = 1; i <= 5; i++) pieces.Add(Cube(root, "GratingBar", new Vector3(x, yc, Mathf.Lerp(z0, z1, i / 6f)), new Vector3(.025f, y1 - y0 - .08f, .022f), iron));
            }
            list.arraySize = pieces.Count; for (int i = 0; i < pieces.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            so.ApplyModifiedProperties();
        }
        static Transform WallPanel(Transform parent, MeshRenderer wall, Vector3 at, float z0, float z1, float y0, float y1)
        {
            // 옆 벽 메쉬의 서쪽 면 UV 규칙(아래 띠/위 띠)을 같은 월드 좌표로 이어 쓴다.
            var mesh = wall.GetComponent<MeshFilter>().sharedMesh; var t = wall.transform; float face = wall.bounds.min.x;
            var bands = Enumerable.Range(0, mesh.subMeshCount).Select(s =>
            {
                // 서쪽 면 삼각형만(모서리를 공유하는 남·북 벽 정점 제외).
                var tri = mesh.GetTriangles(s); var world = mesh.vertices.Select(v => t.TransformPoint(v)).ToArray();
                var idx = Enumerable.Range(0, tri.Length / 3).Where(k => Enumerable.Range(0, 3).All(j => Mathf.Abs(world[tri[k * 3 + j]].x - face) < .02f))
                    .SelectMany(k => new[] { tri[k * 3], tri[k * 3 + 1], tri[k * 3 + 2] }).Distinct().ToArray();
                var p = idx.Select(i => t.TransformPoint(mesh.vertices[i])).ToArray(); var uv = idx.Select(i => mesh.uv[i]).ToArray();
                // u = a + b·z, v = c + d·y 를 두 점으로 구한다.
                int i0 = 0, iz = Array.FindIndex(p, q => Mathf.Abs(q.y - p[0].y) < .01f && Mathf.Abs(q.z - p[0].z) > 1f), iy = Array.FindIndex(p, q => Mathf.Abs(q.z - p[0].z) < .01f && Mathf.Abs(q.y - p[0].y) > .5f && Mathf.Abs(uv[Array.IndexOf(p, q)].x - uv[0].x) < .01f);
                float b = (uv[iz].x - uv[i0].x) / (p[iz].z - p[i0].z), d = (uv[iy].y - uv[i0].y) / (p[iy].y - p[i0].y);
                return (min: p.Min(q => q.y), max: p.Max(q => q.y), a: uv[i0].x - b * p[i0].z, b, c: uv[i0].y - d * p[i0].y, d, mat: wall.sharedMaterials[s]);
            }).Where(band => band.max > y0 && band.min < y1).OrderBy(band => band.min).ToArray();
            var go = new GameObject("HiddenPanel", typeof(MeshFilter), typeof(MeshRenderer)); Undo.RegisterCreatedObjectUndo(go, "Add hidden wall panel");
            go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(at, Quaternion.identity);
            var m = new Mesh { name = "HiddenWallPanel", subMeshCount = bands.Length }; var verts = new List<Vector3>(); var uvs = new List<Vector2>();
            var tris = new List<int[]>();
            foreach (var band in bands)
            {
                float lo = Mathf.Max(y0, band.min), hi = Mathf.Min(y1, band.max); int s = verts.Count;
                foreach (var (z, y) in new[] { (z0, lo), (z0, hi), (z1, hi), (z1, lo) })
                { verts.Add(go.transform.InverseTransformPoint(new Vector3(at.x, y, z))); uvs.Add(new Vector2(band.a + band.b * z, band.c + band.d * y)); }
                tris.Add(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); // +x(방 안쪽)에서 보이는 시계 방향
            }
            m.SetVertices(verts); m.SetUVs(0, uvs); for (int i = 0; i < tris.Count; i++) m.SetTriangles(tris[i], i);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = m; go.GetComponent<MeshRenderer>().sharedMaterials = bands.Select(band => band.mat).ToArray();
            return go.transform;
        }
        static Transform Cube(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(at, Quaternion.identity);
            go.transform.localScale = size; // 월드 축으로 세운 상자. 부모 배율은 1이다.
            go.GetComponent<Renderer>().sharedMaterial = material; if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
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
