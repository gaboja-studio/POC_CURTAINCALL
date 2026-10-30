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
    /// <summary>
    /// 2026-10-30 사용자 지적 보정: 숙소 캐비넷 막힘, 공중 부유, 낙서 벽 부착, 비밀번호 패드 위치.
    /// 이미 설치된 구조 A 배치를 실제 충돌면 기준으로 옮긴다. 대상 ID·연결·수치는 바꾸지 않으며 다시 실행해도 같은 결과다.
    /// </summary>
    public static class StructureAPlacementFix
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const float Gap = .003f;
        static Transform layout;
        static readonly List<string> report = new();

        [MenuItem("CurtainCall/Waiting Room/Fix Structure A Placement")]
        public static void MenuFix() => Debug.Log(Fix());

        public static string Fix()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open WaitingRoomBase in EditMode.");
            layout = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            report.Clear();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Fix Structure A placement");
            try
            {
                // 1. 단순 캐비넷/상자: 문짝 경첩이 몸체 옆으로 비틀려 통로를 막던 생성 오류. 앞면(-z) 경첩으로 되돌린다.
                foreach (var name in new[] { "emptyCabinet", "mapCabinet", "cabinet3", "lockedBox2", "managementDrawer" }) RebuildHinge(Find(name));

                // 2. 숙소: 기존 옷장/책상 사이 통로를 비우고 화장실 문 옆 빈 남쪽 벽에 붙인다. 열면 지도는 앞 바닥에 놓인다.
                float south = Cast(null, new Vector3(16f, 1f, 70.5f), Vector3.back, 4f).point.z;
                Place(Find("emptyCabinet"), new Vector3(15.55f, 0, south + .33f), 180);
                Place(Find("mapCabinet"), new Vector3(16.55f, 0, south + .33f), 180);
                Place(Find("mapClue"), new Vector3(16.55f, .5f, south + 1.05f), null); Floor(Find("mapClue"));

                // 3. 낙서·UV·벽 단서: 가장 가까운 실제 벽면에 밀착하고 얇은 면을 벽과 평행하게 돌린다.
                Wall(Find("graffiti1"), Vector3.left);
                Wall(Find("graffiti2"), Vector3.right);
                Wall(Find("uv-1-detail"), Vector3.back);
                // 원래 z109.6은 봉인된 출입구 문틀에 파묻혀 보이지 않았다. 같은 서쪽 벽 문틀 앞 빈 면.
                Place(Find("graffiti-3"), new Vector3(-19.2f, 1.25f, 108.6f), null); Wall(Find("graffiti-3"), Vector3.left);
                Place(Find("graffiti4"), new Vector3(11.4f, 1.25f, 55.5f), null); Wall(Find("graffiti4"), Vector3.left);
                Place(Find("hiddenGraffiti3"), new Vector3(-39.1f, 1.25f, 79.4f), null); Wall(Find("hiddenGraffiti3"), Vector3.forward);
                Place(Find("cargo-uv"), new Vector3(-39.1f, 1.25f, 79.4f), null); Wall(Find("cargo-uv"), Vector3.forward, .012f);
                Place(Find("passwordPrefixClue"), new Vector3(37.6f, 1.25f, 38.2f), null); Wall(Find("passwordPrefixClue"), Vector3.right);
                Place(Find("passwordPrefixUv"), new Vector3(37.6f, 1.25f, 38.2f), null); Wall(Find("passwordPrefixUv"), Vector3.right, .012f);

                // 4. 기계실 모니터: 기록 책상 위 기존 CRT 화면에 맞춘다. 공연 기록은 같은 방 다른 책상 빈자리에 눕힌다.
                Screen(Find("monitor"), FindIn("S05_JunkStorage", "PoweredOffTube", new Vector3(18.7f, 1.3f, 37.45f)));
                Place(Find("performanceRecord"), new Vector3(21.7f, 1.5f, 44.05f), null); Floor(Find("performanceRecord"));

                // 5. 바닥 위 열쇠·노트·신문: 아래 실제 면에 내려놓는다.
                foreach (var name in new[] { "key1", "managementKey", "stairsKey", "escapeKey1", "key2", "escapeKey2", "newspaper", "confiscatedNotebook" }) Floor(Find(name));

                // 6. 문: 바닥에서 20cm 뜬 판을 바닥에 붙이고, 기존 문짝이 있는 문은 판을 문짝 안쪽에 숨겨 실제 문 장식을 가리지 않는다.
                foreach (var name in new[] { "machineDoor", "management-door" }) Floor(Find(name));
                DoorLeaf(Find("stairsDoor"), layout.Find("ST_Stairwell/StairwellDoorway"));
                DoorLeaf(Find("escapeDoor"), layout.Find("B1_EscapeEntrance/SealedDoor"));
                // 문판을 문짝 안에 숨기면 외곽선도 가려진다. 문 앞 8mm에 깊이 전용 판을 둬 외곽선을 문짝 둘레 한 줄로 그린다.
                DoorProxy(Find("stairsDoor"), layout.Find("ST_Stairwell/StairwellDoorway"), Vector3.forward, true);
                DoorProxy(Find("escapeDoor"), layout.Find("B1_EscapeEntrance/SealedDoor"), Vector3.right, false);

                // 8. 화장실 금 간 벽: 벽 조각 40개마다 외곽선이 그려져 격자·철제 띠까지 잡히던 문제. 타일 높이 앞 깊이 전용 판 하나를 외곽선 기준으로.
                BreachProxy(layout.Find("S02_Dormitory/Restroom/BreachWall"));

                // 7. 비밀번호 패드: 탈출문 옆 기존 키패드 뒤 받침판으로 옮긴다(판정 지점은 키패드 앞).
                Keypad(Find("passwordPad"), layout.Find("B1_EscapeEntrance/SealedDoor/Keypad"));

                Physics.SyncTransforms(); Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
                return "Structure A placement fixed:\n" + string.Join("\n", report);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Transform Find(string name)
        {
            var found = layout.GetComponentsInChildren<Transform>(true).Where(t => t.name == name && t.parent != null && t.parent.name == "Exploration_A").ToArray();
            return found.Length == 1 ? found[0] : throw new InvalidOperationException($"Exploration target {name}: {found.Length} matches");
        }
        static Transform FindIn(string room, string name, Vector3 near) =>
            layout.Find(room).GetComponentsInChildren<Transform>(true).Where(t => t.name == name).OrderBy(t => (t.position - near).sqrMagnitude).First();
        static Bounds RenderBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); return b;
        }
        static bool Own(Collider c, Transform t) => c.transform.IsChildOf(t);
        static RaycastHit Cast(Transform self, Vector3 from, Vector3 dir, float max)
        {
            var hits = Physics.RaycastAll(from, dir, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(h => self == null || !Own(h.collider, self)).OrderBy(h => h.distance).ToArray();
            return hits.Length > 0 ? hits[0] : throw new InvalidOperationException($"No surface from {from} toward {dir}");
        }
        static void Place(Transform t, Vector3 at, float? worldYaw)
        {
            Undo.RecordObject(t, "Move exploration target"); t.position = at;
            if (worldYaw.HasValue) t.rotation = Quaternion.Euler(0, worldYaw.Value, 0);
            Physics.SyncTransforms();
        }
        static void RebuildHinge(Transform root)
        {
            var body = root.Find("Body"); var hinge = root.Find("Hinge"); var door = hinge.Find("Door");
            var size = body.localScale;
            Undo.RecordObject(hinge, "Fix hinge"); Undo.RecordObject(door, "Fix door");
            hinge.localPosition = new Vector3(-size.x * .5f, 0, -size.z * .5f - .03f); hinge.localRotation = Quaternion.identity;
            door.localPosition = new Vector3(size.x * .5f, size.y * .5f, 0); door.localRotation = Quaternion.identity;
            report.Add($"- {root.name}: 문짝을 앞면 경첩으로 복구");
        }
        static void Floor(Transform t)
        {
            Physics.SyncTransforms();
            var b = RenderBounds(t);
            // 바닥면 바로 위에서 쏜다. 키 큰 문판도 천장/문틀 대신 발밑 면을 찾는다.
            var hit = Cast(t, new Vector3(b.center.x, b.min.y + .08f, b.center.z), Vector3.down, 3f);
            float lift = hit.point.y + Gap - b.min.y;
            Undo.RecordObject(t, "Rest on surface"); t.position += Vector3.up * lift;
            report.Add($"- {t.name}: {hit.collider.name} 위 {t.position.y:F3} (이동 {lift:+0.000;-0.000}m)");
        }
        static void Wall(Transform t, Vector3 dir, float extra = 0f)
        {
            // 문서/얼룩은 local z가 얇은 면이다. 벽 방향을 local z로 맞춘 뒤 뒷면을 벽에 붙인다.
            Undo.RecordObject(t, "Attach to wall"); t.rotation = Quaternion.LookRotation(dir, Vector3.up); Physics.SyncTransforms();
            var hit = Cast(t, t.position - dir * .6f, dir, 3f);
            float half = t.GetComponentsInChildren<MeshFilter>(true).Max(m => m.transform.lossyScale.z) * .5f;
            t.position = hit.point - dir * (half + Gap + extra);
            report.Add($"- {t.name}: {hit.collider.name} 벽면 {hit.point.x:F2},{hit.point.z:F2} 부착");
        }
        static void Screen(Transform monitor, Transform tube)
        {
            // 화면판을 기존 CRT 튜브 앞면에 덧댄다. 판정 지점도 화면 앞.
            var tb = tube.GetComponent<Renderer>().bounds; var visual = monitor.Find("Visual");
            Undo.RecordObject(monitor, "Mount monitor"); Undo.RecordObject(visual, "Mount monitor");
            monitor.SetPositionAndRotation(new Vector3(tb.max.x + .012f, tb.min.y, tb.center.z), Quaternion.Euler(0, 90, 0));
            visual.localPosition = new Vector3(0, tb.size.y * .5f, 0); visual.localScale = new Vector3(tb.size.z, tb.size.y, .01f);
            report.Add($"- {monitor.name}: 기존 CRT 화면 {tb.center.x:F2},{tb.center.y:F2},{tb.center.z:F2}");
        }
        static void DoorLeaf(Transform gate, Transform existing)
        {
            var leaves = existing.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.Contains("Door") && !r.name.Contains("Frame") && !r.name.Contains("Label") && !r.name.Contains("Latch") && !r.name.Contains("Rib")).ToArray();
            var b = leaves[0].bounds; foreach (var r in leaves) b.Encapsulate(r.bounds);
            var visual = gate.Find("Visual");
            Undo.RecordObject(gate, "Hide panel in door leaf"); Undo.RecordObject(visual, "Hide panel in door leaf");
            float floor = Cast(gate, gate.position + Vector3.up * .1f, Vector3.down, 2f).point.y;
            gate.position = new Vector3(gate.position.x, floor, gate.position.z);
            // 1cm 안쪽으로 줄여 문짝 표면 아래에 둔다. 외곽선은 문짝 밖으로 비친다.
            visual.position = b.center; visual.localScale = Vector3.Max(b.size - Vector3.one * .02f, Vector3.one * .01f);
            report.Add($"- {gate.name}: 기존 문짝 {string.Join("/", leaves.Select(l => l.name).Distinct())} 안쪽으로 판 정리, 바닥 {floor:F2}");
        }
        const string ProxyMaterialPath = "Assets/Scenes/Tests/WaitingRoom/Base/Materials/WR_OutlineProxyDepth.mat";
        static Material ProxyMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ProxyMaterialPath);
            if (material != null) return material;
            var shader = Shader.Find("CurtainCall/WaitingRoom/OutlineProxyDepth") ?? throw new InvalidOperationException("OutlineProxyDepth shader missing");
            material = new Material(shader) { name = "WR_OutlineProxyDepth" };
            AssetDatabase.CreateAsset(material, ProxyMaterialPath); return material;
        }
        static Transform Plate(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var found = parent.Find(name); GameObject plate;
            if (found != null) plate = found.gameObject;
            else
            {
                plate = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(plate, "Outline proxy"); plate.name = name;
                Object.DestroyImmediate(plate.GetComponent<Collider>()); plate.transform.SetParent(parent, false);
            }
            Undo.RecordObject(plate.transform, "Outline proxy");
            plate.transform.SetPositionAndRotation(center, Quaternion.identity);
            var scale = parent.lossyScale; plate.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
            var r = plate.GetComponent<MeshRenderer>(); Undo.RecordObject(r, "Outline proxy");
            r.sharedMaterial = ProxyMaterial(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return plate.transform;
        }
        static void DoorProxy(Transform gate, Transform existing, Vector3 normal, bool bothSides)
        {
            var parts = existing.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.Contains("Frame")).ToArray();
            var leaf = parts.Where(r => r.name.Contains("Door") && !r.name.Contains("Label") && !r.name.Contains("Latch") && !r.name.Contains("Rib"))
                .Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var side = Vector3.Cross(Vector3.up, normal);
            float Along(Vector3 p, Vector3 axis) => Vector3.Dot(p, axis);
            float half = Mathf.Abs(Vector3.Dot(leaf.extents, side));
            // 문짝 면 범위 안에서 30cm 이내로 튀어나온 장식(보강대·손잡이·로프)까지 앞면으로 친다.
            var near = parts.Select(r => r.bounds).Where(b => Mathf.Abs(Along(b.center, side) - Along(leaf.center, side)) < half && b.min.y < leaf.max.y && b.max.y > leaf.min.y).ToArray();
            var visual = gate.Find("Visual");
            var size = side * (half * 2 + .02f) + Vector3.up * (leaf.size.y + .02f);
            size = new Vector3(Mathf.Abs(size.x), size.y, Mathf.Abs(size.z)) + new Vector3(Mathf.Abs(normal.x), 0, Mathf.Abs(normal.z)) * .004f;
            var plates = new List<Transform>();
            foreach (var dir in bothSides ? new[] { normal, -normal } : new[] { normal })
            {
                float front = near.Where(b => Mathf.Abs(Along(b.center, dir) - Along(leaf.center, dir)) < .3f).Max(b => Along(b.center, dir) + Mathf.Abs(Vector3.Dot(b.extents, dir)));
                var center = leaf.center + dir * (front + .008f - Along(leaf.center, dir));
                plates.Add(Plate(gate, dir == normal ? "OutlineProxy_Front" : "OutlineProxy_Back", center, size));
            }
            // 기존 숨긴 문판은 외곽선에 끼지 않게 끈다(그림은 기존 문짝이 맡는다).
            var hidden = visual.GetComponent<MeshRenderer>(); Undo.RecordObject(hidden, "Outline proxy"); hidden.enabled = false;
            var target = gate.GetComponent<ExplorationGate>(); var so = new SerializedObject(target);
            foreach (var field in new[] { "hideOnOpen", "hiddenVisuals" })
            {
                var p = so.FindProperty(field); var list = Enumerable.Range(0, p.arraySize).Select(i => p.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
                foreach (var plate in plates) if (!list.Contains(plate.gameObject)) list.Add(plate.gameObject);
                p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedProperties();
            report.Add($"- {gate.name}: 문 앞 외곽선 기준판 {plates.Count}장");
        }
        static void BreachProxy(Transform wall)
        {
            var blocks = wall.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Block_")).Select(r => r.bounds).ToArray();
            var ledger = wall.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "SteelLedger").Min(r => r.bounds.min.y);
            var face = wall.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "RestroomFace").Max(r => r.bounds.max.z);
            float minX = blocks.Min(b => b.min.x), maxX = blocks.Max(b => b.max.x), floor = blocks.Min(b => b.min.y), top = ledger - .02f;
            var plate = Plate(wall, "OutlineProxy", new Vector3((minX + maxX) * .5f, (floor + top) * .5f, face + .006f), new Vector3(maxX - minX + .01f, top - floor, .004f));
            var so = new SerializedObject(wall.GetComponent<BreakableWall>()); so.FindProperty("outlineRoot").objectReferenceValue = plate; so.ApplyModifiedProperties();
            report.Add($"- {wall.name}: 타일 높이 외곽선 기준판 x{minX:F2}~{maxX:F2} y{floor:F2}~{top:F2}");
        }
        static void Keypad(Transform pad, Transform keypad)
        {
            var kb = keypad.GetComponent<Renderer>().bounds; var visual = pad.Find("Visual");
            float wall = Cast(pad, new Vector3(kb.max.x + .2f, kb.center.y, kb.center.z), Vector3.left, 1f).point.x;
            Undo.RecordObject(pad, "Mount keypad"); Undo.RecordObject(visual, "Mount keypad");
            pad.SetPositionAndRotation(new Vector3(kb.max.x + .06f, kb.center.y, kb.center.z), Quaternion.identity);
            // 받침판: 벽면과 키패드 뒷면 사이, 키패드보다 사방 4cm 크게.
            float back = (wall + kb.min.x) * .5f;
            visual.position = new Vector3(back, kb.center.y, kb.center.z);
            visual.localScale = new Vector3(Mathf.Max(.01f, kb.min.x - wall - .004f), kb.size.y + .08f, kb.size.z + .08f);
            report.Add($"- {pad.name}: 기존 키패드 {kb.center.x:F2},{kb.center.y:F2},{kb.center.z:F2} 받침판, 벽 {wall:F2}");
        }
    }
}
