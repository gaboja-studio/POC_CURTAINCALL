using System.Collections.Generic;
using System.Linq;
using CurtainCall.EscapeRoute;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 5단계: 최종본 동선 연결(2026-11-12 사용자). 4단계(기계실 배선) 뒤에 실행한다. 다시 실행할 수 있다.
    // - 삭제: 열쇠 6종·배관함·비밀번호 패드·공연 기록. 열쇠가 들어 있던 보관함은 빈 채로 남기고, 강철 상자는 열리지 않는 장식.
    // - 계단 문: 계단 쪽에 쌓인 상자(맨몸 3 / 뽑개 1)를 부수면 열린다. 로비 쪽에서는 문에 막혀 닿지 않는다.
    // - 관리실 문: 녹슨 벽 안 배선을 자르면 열린다. 톱날 발사기는 작업실에 둔다.
    // - 탈출로: 관리실 컴퓨터(종말 날짜 + RUN)와 기계실 탈출로 배선 절단을 둘 다 마치면 숨겨진 문·탈출로 문이 함께 열린다.
    public static partial class FinalLayoutBuilder
    {
        const string StairsBarId = "final:stairs-bar", CargoBarId = "final:cargo-door-bar", ManagementCableId = "final:management-cable";
        const string ComputerId = "final:office-computer", EscapeCableId = "final:escape-cable", EscapeRouteId = "final:escape-route";
        // 기계실 서벽·남벽 안쪽 면(충돌체 기준).
        const float MachineWestX = -3.8f, MachineSouthZ = 64.05f;
        // 세계 종말 날짜(2026-11-12 사용자: 낡은 신문의 날짜).
        internal const string ComputerDate = "20240912";
        static readonly Vector3 SawSpot = new(-20f, 0f, 53.2f);

        static readonly string[] RemovedObjects =
        {
            "S02_Dormitory/Restroom/Exploration_A/key1", "S02_Dormitory/Exploration_A/managementKey", "S05_JunkStorage/Exploration_A/stairsKey",
            "S11_StageGear/Exploration_A/escapeKey1", "B1_Office/Exploration_A/escapeKey2", "S12_CargoBay/Exploration_A/key2",
            "B1_MachineRoom/Exploration_A/PipeBox", "B1_MachineRoom/Exploration_A/passwordPad", "S05_JunkStorage/Exploration_A/performanceRecord",
        };
        // 경로 연결 컴포넌트가 시작할 때 옛 조건(열쇠·패드)을 다시 붙이지 않도록 비우는 참조.
        static readonly (string type, string[] fields)[] ClearedRouteFields =
        {
            ("StructureAKeyRoute", new[] { "key1", "steelBox", "managementKey", "stairsKey", "stairsDoor", "escapeKey1", "escapeKey2", "escapeDoor" }),
            ("StructureAExitRoute", new[] { "key2", "machineDoor", "passwordPad", "escapeDoor" }),
            ("StructureAClueRoute", new[] { "performanceRecord" }),
        };

        [MenuItem("CurtainCall/Waiting Room/Final Layout/5. Connect Final Route")]
        static void RouteMenu() => Debug.Log(ConnectFinalRoute());

        public static string ConnectFinalRoute()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            var saw = layout.GetComponentsInChildren<EscapeExitSaw>(true).FirstOrDefault(s => s.name == "PortableSaw");
            var hidden = layout.Find(RootName + "/MachineWiring/HiddenCable")?.GetComponent<EscapeCuttableCable>();
                        var keyboard = layout.Find("B1_Office/SecurityRoom/Console/Keyboard");
            if (saw == null || hidden == null || keyboard == null) return "4단계(기계실 배선)를 먼저 실행하세요. 또는 톱날 발사기·관리실 자판을 찾지 못했습니다.";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: connect route");
            var notes = new List<string>();

            // 경로 참조 비우기 → 삭제.
            foreach (var route in layout.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var cleared = ClearedRouteFields.FirstOrDefault(c => route != null && c.type == route.GetType().Name);
                if (cleared.fields == null) continue;
                var so = new SerializedObject(route);
                foreach (var field in cleared.fields) so.FindProperty(field).objectReferenceValue = null;
                so.ApplyModifiedProperties();
            }
            int removed = 0;
            foreach (var removePath in RemovedObjects)
            {
                var t = layout.Find(removePath); if (t == null) continue;
                Undo.DestroyObjectImmediate(t.gameObject); removed++;
            }
            notes.Add($"삭제 {removed}개");

            // 강철 상자: 열 방법이 없어져 장식으로 둔다(상호작용 끔).
            var steel = layout.Find("S02_Dormitory/Exploration_A/steelBox")?.GetComponent<ExplorationGate>();
            if (steel != null && steel.enabled) { Undo.RecordObject(steel, "Steel box decoration"); steel.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(steel); }

            var old = layout.Find(RootName + "/Route"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "Route");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Final route");

            // 계단 문: 계단 쪽 문짝에 걸린 빗장(맨몸 3 / 뽑개 1)을 부수면 열린다. 로비 쪽에서는 문 충돌체에 막혀 닿지 않는다.
            var door = layout.Find("ST_Stairwell/Exploration_A/stairsDoor");
            var iron = layout.Find("ST_Stairwell/StairwellDoorway/LeafHingeL/Hinge").GetComponent<Renderer>().sharedMaterial;
            var plate = layout.Find("ST_Stairwell/StairwellDoorway/LeafHingeL/LockedDoorLeafL").GetComponent<Renderer>().sharedMaterial;
            var leaves = Bounds(layout.Find("ST_Stairwell/StairwellDoorway/LeafHingeL")); leaves.Encapsulate(Bounds(layout.Find("ST_Stairwell/StairwellDoorway/LeafHingeR")));
            Bar(root, "StairsBar", StairsBarId, 3, iron, new Vector3(leaves.min.x + .12f, door.position.y + 1.1f, leaves.max.z), new Vector3(leaves.max.x - .12f, door.position.y + 1.1f, leaves.max.z), Vector3.forward);
            Rewire(door, StairsBarId);

            // 화물 적재장 ↔ 로비: 밤 정비 철창을 끄고 같은 자리에 굳게 닫힌 철문을 세운다. 화물 적재장 쪽 빗장(맨몸 6 / 뽑개 1)을 부수면 열린다.
            var cage = layout.Find("NightMaintenance/CargoGate");
            if (cage != null && cage.gameObject.activeSelf) { Undo.RecordObject(cage.gameObject, "Replace cargo cage"); cage.gameObject.SetActive(false); }
            var passage = layout.Find("FacilityConnections/Passage_F8_Cargo");
            var walls = passage.Cast<Transform>().Where(t => t.name == "PassageWall").Select(t => t.GetComponent<Collider>().bounds).OrderBy(b => b.center.z).ToArray();
            float z0 = walls[0].max.z, z1 = walls[1].min.z, top = walls[0].max.y - .02f, mid = (z0 + z1) * .5f, floorCargo = walls[0].min.y;
            const float doorX = -18.75f, half = .05f;
            var cargoDoor = Child(root, "CargoDoor");
            Plain(Box(cargoDoor, "IronLeafS", plate, new Vector3(doorX - half, floorCargo + .01f, z0), new Vector3(doorX + half, top, mid - .01f), false));
            Plain(Box(cargoDoor, "IronLeafN", plate, new Vector3(doorX - half, floorCargo + .01f, mid + .01f), new Vector3(doorX + half, top, z1), false));
            foreach (float y in new[] { .5f, 2.1f, 3.6f })
                foreach (float side in new[] { -1f, 1f })
                    Plain(Box(cargoDoor, "LeafBand", iron, new Vector3(doorX + side * half - (side < 0 ? .03f : 0), floorCargo + y, z0 + .03f), new Vector3(doorX + side * half + (side > 0 ? .03f : 0), floorCargo + y + .12f, z1 - .03f), false));
            var slab = cargoDoor.gameObject.AddComponent<BoxCollider>();
            slab.center = new Vector3(doorX, (floorCargo + top) * .5f, mid); slab.size = new Vector3(.16f, top - floorCargo, z1 - z0);
            cargoDoor.gameObject.AddComponent<Pursuit.PursuitDoorObstacle>().Source = slab;
            var cargoBar = Bar(root, "CargoDoorBar", CargoBarId, 6, iron, new Vector3(doorX - half, floorCargo + 1.2f, z0 + .1f), new Vector3(doorX - half, floorCargo + 1.2f, z1 - .1f), Vector3.left);
            var cgo = new SerializedObject(cargoBar);
            Refs(cgo.FindProperty("hideOnOpen"), cargoBar.transform.Find("Visual").gameObject, cargoDoor.gameObject);
            cgo.FindProperty("blocker").objectReferenceValue = slab; cgo.ApplyModifiedPropertiesWithoutUndo();
            notes.Add($"화물 적재 철문 z {z0:F2}~{z1:F2} 높이 {top - floorCargo:F2}");

            // 관리실 문: 녹슨 벽 안 배선이 잘리면 열린다(문에 직접 F하지 않는다).
            // 위치는 사용자 구조도(2026-11-12)대로 기계실 남서 모서리: 녹슨 벽을 남벽 서쪽 끝에 붙이고 배선은 그 안쪽 남벽을 탄다.
            var rust = layout.Find(RootName + "/MachineWiring/RustWall");
            var rb = Bounds(rust); float rustWidth = Mathf.Max(rb.size.x, rb.size.z);
            var corner = new Vector3(MachineWestX + .05f + rustWidth * .5f, rust.position.y, MachineSouthZ);
            Undo.RecordObject(rust, "Move rust wall"); rust.SetPositionAndRotation(corner, Quaternion.identity);
            Undo.RecordObject(hidden.transform, "Move cable"); hidden.transform.position = new Vector3(corner.x, corner.y, MachineSouthZ + .16f);
            Set(hidden, "openTarget", ManagementCableId);
            Rewire(layout.Find("B1_Office/Exploration_A/management-door"), ManagementCableId);

            // 탈출로 배선 4가닥: 배관함이 있던 벽면을 타고 올라가 숨겨진 문 위 틀로 들어간다. 처음부터 드러나 있고 모두 잘라야 한다.
            var hiddenDoor = layout.Find("B1_MachineRoom/Exploration_A/machineDoor");
            float wallX = MachineWestX, floorY = rust.position.y, doorEdgeZ = hiddenDoor.position.z + 1.05f;
            var colors = new[] { "cable-red", "cable-yellow", "cable-green", "cable-cyan" };
            var escapeCables = new EscapeCuttableCable[colors.Length]; var cableIds = new string[colors.Length];
            for (int n = 0; n < colors.Length; n++)
            {
                float cableZ = doorEdgeZ + .4f + n * .42f, turn = 3.5f + n * .12f, reach = doorEdgeZ - cableZ;
                var cableGo = new GameObject("EscapeCable_" + (n + 1), typeof(MeshFilter), typeof(MeshRenderer)); cableGo.transform.SetParent(root, false);
                cableGo.transform.position = new Vector3(wallX + .05f, floorY, cableZ);
                var cable = cableGo.AddComponent<EscapeCuttableCable>(); var cso = new SerializedObject(cable);
                var path = new[] { new Vector3(0, 0, 0), new Vector3(0, 1.2f, .03f), new Vector3(0, 2.6f, -.03f), new Vector3(0, turn - .2f, 0), new Vector3(0, turn, reach * .5f), new Vector3(0, turn, reach) };
                var points = cso.FindProperty("localPoints"); points.arraySize = path.Length;
                for (int i = 0; i < path.Length; i++) points.GetArrayElementAtIndex(i).vector3Value = path[i];
                cableIds[n] = EscapeCableId + "-" + (n + 1);
                cso.FindProperty("radius").floatValue = .05f; cso.FindProperty("openTarget").stringValue = cableIds[n];
                cso.FindProperty("insulationMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CableMaterials + colors[n] + ".mat");
                cso.FindProperty("copperMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CableMaterials + "cable-copper-core.mat");
                cso.ApplyModifiedPropertiesWithoutUndo(); cable.Rebuild(); escapeCables[n] = cable;
            }

            // 톱날 발사기: 관리실 배선과 탈출로 배선 4가닥을 모두 자를 수 있게 연결하고 작업실 남쪽 벽 앞 바닥으로 옮긴다. 문은 배선마다 따로 열린다.
            var sso = new SerializedObject(saw);
            Refs(sso.FindProperty("cables"), new Object[] { hidden }.Concat(escapeCables).ToArray()); sso.FindProperty("openTarget").stringValue = "";
            sso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(saw);
            Undo.RecordObject(saw.transform, "Move saw"); saw.transform.position = SawSpot;
            PrefabUtility.RecordPrefabInstancePropertyModifications(saw.transform);

            // 관리실 컴퓨터: 감시 콘솔 자판에 단말기 화면을 붙인다. 위 칸 종말 날짜(낡은 신문의 날짜 2024년 09월 12일), 아래 칸 RUN.
            var computer = keyboard.GetComponent<ExplorationGate>() ?? Undo.AddComponent<ExplorationGate>(keyboard.gameObject);
            var pso = new SerializedObject(computer);
            pso.FindProperty("targetId").stringValue = ComputerId; pso.FindProperty("label").stringValue = "관리실 컴퓨터";
            pso.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Keypad;
            pso.FindProperty("password").stringValue = ComputerDate; pso.FindProperty("passwordExplicit").boolValue = true;
            pso.FindProperty("terminalWord").stringValue = "RUN";
            pso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(computer);

            // 컴퓨터 + 탈출로 배선 → 숨겨진 문·탈출로 문이 함께 열린다.
            var both = root.gameObject.AddComponent<ExplorationAllOf>(); var bso = new SerializedObject(both);
            var required = bso.FindProperty("required"); required.arraySize = 1 + cableIds.Length;
            required.GetArrayElementAtIndex(0).stringValue = ComputerId;
            for (int n = 0; n < cableIds.Length; n++) required.GetArrayElementAtIndex(n + 1).stringValue = cableIds[n];
            bso.FindProperty("openTarget").stringValue = EscapeRouteId; bso.ApplyModifiedPropertiesWithoutUndo();
            // 탈출로 진입 판정(EscapeEntrance)은 탈출로 문 대상(structure-a:escape-door)이 장부에서 열렸는지를 본다.
            // 문은 이제 EscapeRouteId로 함께 열리므로, 길이 열리면 문 대상도 같이 기록해 탈출로로 넘어갈 수 있게 한다.
            var doorMark = root.gameObject.AddComponent<ExplorationAllOf>(); var dso = new SerializedObject(doorMark);
            var doorRequired = dso.FindProperty("required"); doorRequired.arraySize = 1; doorRequired.GetArrayElementAtIndex(0).stringValue = EscapeRouteId;
            dso.FindProperty("openTarget").stringValue = StructureAKeyRoute.EscapeDoor; dso.ApplyModifiedPropertiesWithoutUndo();
            Rewire(hiddenDoor, EscapeRouteId);
            Rewire(layout.Find("B1_EscapeEntrance/Exploration_A/escapeDoor"), EscapeRouteId);
            var panel = layout.Find("B1_MachineRoom/Exploration_A/HiddenPanel")?.GetComponent<ExplorationSlide>();
            if (panel != null) { Set(panel, "openedBy", EscapeRouteId); PrefabUtility.RecordPrefabInstancePropertyModifications(panel); }

            Physics.SyncTransforms();
            notes.Add($"탈출로 배선 4가닥 z {doorEdgeZ + .4f:F2}~{doorEdgeZ + .4f + 3 * .42f:F2} / 톱날 발사기 {SawSpot}");
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return string.Join(" / ", notes);
        }

        // 빗장: 문짝 면(from~to, 바깥 방향 outward)에 걸린 쇠막대와 걸쇠 4개. 부수면 사라진다. 문과는 따로 둔 대상이라
        // 반대편에서는 문 충돌체에 가려 닿지 않는다.
        static ExplorationGate Bar(Transform parent, string name, string id, int bareHits, Material iron, Vector3 from, Vector3 to, Vector3 outward)
        {
            var bar = Child(parent, name); bar.position = (from + to) * .5f + outward * .07f;
            var visual = Child(bar, "Visual"); visual.localPosition = Vector3.zero;
            Vector3 along = (to - from).normalized, thick = outward * .04f + Vector3.up * .06f;
            Vector3 Lo(Vector3 a, Vector3 b) => Vector3.Min(a, b); Vector3 Hi(Vector3 a, Vector3 b) => Vector3.Max(a, b);
            Vector3 c0 = from + outward * .07f, c1 = to + outward * .07f;
            Plain(Box(visual, "BarBeam", iron, Lo(c0 - thick, c1 - thick), Hi(c0 + thick, c1 + thick), false));
            // 걸쇠: 문짝에 붙어 막대를 감싼다(문짝 면에서 막대 바깥까지).
            float length = (to - from).magnitude;
            foreach (float k in new[] { .12f, .4f, .6f, .88f })
            {
                var c = from + along * (length * k); Vector3 w = along * .05f, grip = outward * .13f + Vector3.up * .1f;
                Plain(Box(visual, "BarBracket", iron, Lo(c - w - Vector3.up * .1f, c + w + grip), Hi(c - w - Vector3.up * .1f, c + w + grip), false));
            }
            var point = Child(bar, "InteractionPoint"); point.position = bar.position + outward * .08f;
            var gate = bar.gameObject.AddComponent<ExplorationGate>(); var so = new SerializedObject(gate);
            so.FindProperty("targetId").stringValue = id; so.FindProperty("label").stringValue = "빗장";
            so.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Breakable;
            so.FindProperty("toolId").stringValue = ItemUses.StakePuller;
            so.FindProperty("requiredHits").intValue = 1; so.FindProperty("bareHandHits").intValue = bareHits;
            so.FindProperty("interactionPoint").objectReferenceValue = point;
            Refs(so.FindProperty("hideOnOpen"), visual.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        // 부수면 사라지는 물체는 정적 묶음·가림 계산에서 뺀다.
        static void Plain(Transform t) => GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        // 문을 직접 여는 조건(열쇠 등)을 지우고, 지정 대상이 열릴 때 함께 열리게 한다.
        static void Rewire(Transform door, string openedBy)
        {
            var gate = door != null ? door.GetComponent<ExplorationGate>() : null; if (gate == null) return;
            var so = new SerializedObject(gate);
            so.FindProperty("requiredTargets").arraySize = 0; so.FindProperty("openedBy").stringValue = openedBy;
            so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
        }

        static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target); so.FindProperty(field).stringValue = value; so.ApplyModifiedProperties();
        }

        static void Refs(SerializedProperty array, params Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
