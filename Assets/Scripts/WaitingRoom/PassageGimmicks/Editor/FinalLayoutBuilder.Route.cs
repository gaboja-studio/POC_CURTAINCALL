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
        const string QuartersDoorId = "final:quarters-door";
        const float SawProjectileRadius = .35f;
        const float RustWallWidth = 2f;
        // 배선 굵기와 벽에서 띄우는 거리(배선 중심 기준).
        const float CableRadius = .06f, CableStandoff = .2f, EscapeCableStart = 1.4f;
        const string ManagementOpenId = "final:management-open", ManagementLatchId = "final:management-latch", ManagementKeycardId = "final:management-keycard";
        const string ComputerId = "final:office-computer", EscapeCableId = "final:escape-cable", EscapeRouteId = "final:escape-route";
        // 기계실 서벽·남벽 안쪽 면(충돌체 기준).
        const float MachineWestX = -3.8f, MachineSouthZ = 64.05f;
        // 세계 종말 날짜(2026-11-12 사용자: 낡은 신문의 날짜).
        internal const string ComputerDate = "20240912";
        // 톱날 발사기는 무대 장치 보관실(원래 자리)에 둔다(2026-11-13 사용자).
        static readonly Vector3 SawSpot = new(-54f, 0f, 42f);

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

            // 이전 세대 공연자 숙소 문(2026-11-13 사용자): 복도 ↔ 생활 장소 문틀(x -3~-1, 높이 2.6)을 낡은 문으로 막는다.
            // 문 자체를 부순다: 맨몸 6회, 천막 말뚝 뽑개 1회. 양쪽 어디서든 칠 수 있다.
            {
                var green = Mat("LA_AbandonedInstitutionGreen") ?? plate;
                float dz = CorZ0 - T * .5f; const float dx0 = -3f, dx1 = -1f, dTop = 2.6f, dHalf = .05f;
                var qDoor = Child(root, "QuartersDoor"); qDoor.position = new Vector3((dx0 + dx1) * .5f, 0, dz);
                var qVisual = Child(qDoor, "Visual"); qVisual.localPosition = Vector3.zero;
                Plain(Box(qVisual, "DoorLeaf", green, new Vector3(dx0 + .01f, .01f, dz - dHalf), new Vector3(dx1 - .01f, dTop - .01f, dz + dHalf), false));
                foreach (float y in new[] { .35f, 1.25f, 2.15f })
                    foreach (float side in new[] { -1f, 1f })
                        Plain(Box(qVisual, "DoorBand", iron, new Vector3(dx0 + .06f, y, dz + side * dHalf - (side < 0 ? .02f : 0)), new Vector3(dx1 - .06f, y + .1f, dz + side * dHalf + (side > 0 ? .02f : 0)), false));
                foreach (float side in new[] { -1f, 1f })
                    Plain(Box(qVisual, "DoorHandle", iron, new Vector3(dx1 - .28f, 1.0f, dz + side * dHalf - (side < 0 ? .06f : 0)), new Vector3(dx1 - .2f, 1.16f, dz + side * dHalf + (side > 0 ? .06f : 0)), false));
                var qSlab = qDoor.gameObject.AddComponent<BoxCollider>();
                qSlab.center = new Vector3(0, dTop * .5f, 0); qSlab.size = new Vector3(dx1 - dx0, dTop, .16f);
                qDoor.gameObject.AddComponent<Pursuit.PursuitDoorObstacle>().Source = qSlab;
                var qPoint = Child(qDoor, "InteractionPoint"); qPoint.localPosition = new Vector3(0, 1.2f, 0);
                var qGate = qDoor.gameObject.AddComponent<ExplorationGate>(); var qso = new SerializedObject(qGate);
                qso.FindProperty("targetId").stringValue = QuartersDoorId; qso.FindProperty("label").stringValue = "낡은 숙소 문";
                qso.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Breakable;
                qso.FindProperty("toolId").stringValue = ItemUses.StakePuller;
                qso.FindProperty("requiredHits").intValue = 1; qso.FindProperty("bareHandHits").intValue = 6;
                qso.FindProperty("interactionPoint").objectReferenceValue = qPoint;
                qso.FindProperty("blocker").objectReferenceValue = qSlab;
                Refs(qso.FindProperty("hideOnOpen"), qVisual.gameObject);
                qso.ApplyModifiedPropertiesWithoutUndo();
                notes.Add("생활 장소 문(맨몸 6 / 뽑개 1)");
            }

            // 관리실 문: 녹슨 벽 안 배선이 잘리면 열린다(문에 직접 F하지 않는다).
            // 위치는 사용자 구조도(2026-11-12)대로 기계실 남서 모서리: 녹슨 벽을 남벽 서쪽 끝에 붙이고 배선은 그 안쪽 남벽을 탄다.
            var rust = layout.Find(RootName + "/MachineWiring/RustWall");
            // 배선이 든 상자 틈이 1.15m라 톱날(지름 0.7m)을 정면에서 정확히 쏴야만 맞았다(2026-11-13 사용자: 넓혀 달라).
            // 녹슨 벽과 그 뒤 상자를 폭 2m로 넓힌다. 다시 실행해도 같은 폭이 되게 지금 폭을 재서 맞춘다.
            {
                Undo.RecordObject(rust, "Widen rust wall"); rust.rotation = Quaternion.identity;
                float nowWidth = Bounds(rust).size.x;
                if (nowWidth > .01f) { var widened = rust.localScale; widened.x *= RustWallWidth / nowWidth; rust.localScale = widened; }
            }
            var rb = Bounds(rust); float rustWidth = Mathf.Max(rb.size.x, rb.size.z);
            var corner = new Vector3(MachineWestX + .05f + rustWidth * .5f, rust.position.y, MachineSouthZ);
            Undo.RecordObject(rust, "Move rust wall"); rust.SetPositionAndRotation(corner, Quaternion.identity);
            // 배선은 벽에서 조금 띄워 건다. 톱날 판정은 네모 상자로 벽에 막히기 때문에 비스듬히 쏘면 날 끝이 벽에 닿기 전에 멈춰
            // 벽에 바짝 붙은 배선에는 닿지 않았다(2026-11-13 플레이: 배선 절단 안 됨). 45도로 쏴도 닿는 거리까지 띄운다.
            Undo.RecordObject(hidden.transform, "Move cable"); hidden.transform.position = new Vector3(corner.x, corner.y, MachineSouthZ + CableStandoff);
            Set(hidden, "openTarget", ManagementCableId);
            { var hso = new SerializedObject(hidden); hso.FindProperty("radius").floatValue = CableRadius; hso.ApplyModifiedPropertiesWithoutUndo(); hidden.Rebuild(); }
            // 녹슨 벽의 옆·윗 테두리 충돌체는 비스듬히 쏜 톱날을 먼저 막으므로 끈다(벽을 막는 충돌체는 따로 있다).
            foreach (var frameName in new[] { "BoxSideL", "BoxSideR", "BoxTop" })
                if (rust.Find(frameName)?.GetComponent<Collider>() is Collider frameCollider && frameCollider.enabled) { Undo.RecordObject(frameCollider, "Rust wall frame"); frameCollider.enabled = false; }
            // 관리실 문(2026-11-13 사용자): 배선 절단 말고도 안쪽 빗장(맨몸·도구 1회)이나 키 카드로 열린다. 셋 중 하나면 된다.
            var officeDoor = layout.Find("B1_Office/Exploration_A/management-door");
            Rewire(officeDoor, ManagementOpenId);
            if (officeDoor != null)
            {
                var ob = Bounds(officeDoor); // 문은 z면에 서 있고 관리실 안쪽이 +z(북쪽)다.
                var latch = Bar(root, "OfficeLatch", ManagementLatchId, 1, iron, new Vector3(ob.min.x + .12f, ob.min.y + 1.1f, ob.max.z), new Vector3(ob.max.x - .12f, ob.min.y + 1.1f, ob.max.z), Vector3.forward);
                Set(latch, "label", "안쪽 빗장");
                // 문이 다른 방법으로 열리면 빗장도 함께 사라진다(2026-11-13 사용자).
                Set(latch, "retiredBy", ManagementOpenId);
                // 키 카드 단말기: 문 바깥(남쪽) 벽, 문 오른쪽 어깨 높이.
                var readerAt = new Vector3(ob.max.x + .45f, ob.min.y + 1.25f, ob.min.z - .02f);
                if (Physics.Raycast(new Vector3(readerAt.x, readerAt.y, ob.min.z - 1.2f), Vector3.forward, out var wallHit, 1.6f, ~0, QueryTriggerInteraction.Ignore)) readerAt.z = wallHit.point.z;
                var reader = Child(root, "KeycardReader"); reader.position = readerAt + Vector3.back * .03f;
                var rv = Child(reader, "Visual"); rv.localPosition = Vector3.zero;
                Plain(Box(rv, "ReaderBody", iron, readerAt + new Vector3(-.07f, -.11f, -.05f), readerAt + new Vector3(.07f, .11f, 0), false));
                Plain(Box(rv, "ReaderSlot", plate, readerAt + new Vector3(-.045f, -.02f, -.058f), readerAt + new Vector3(.045f, 0, -.05f), false));
                var lampMat = Mat("LA_TentRed"); if (lampMat != null) Plain(Box(rv, "ReaderLamp", lampMat, readerAt + new Vector3(-.015f, .05f, -.058f), readerAt + new Vector3(.015f, .08f, -.05f), false));
                var rp = Child(reader, "InteractionPoint"); rp.position = readerAt + Vector3.back * .12f;
                var rg = reader.gameObject.AddComponent<ExplorationGate>(); var rso = new SerializedObject(rg);
                rso.FindProperty("targetId").stringValue = ManagementKeycardId; rso.FindProperty("label").stringValue = "키 카드 단말기";
                rso.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Breakable;
                rso.FindProperty("toolId").stringValue = KeycardId; rso.FindProperty("requiredHits").intValue = 1; rso.FindProperty("bareHandHits").intValue = 0;
                rso.FindProperty("interactionPoint").objectReferenceValue = rp;
                // 문이 열리면(어떤 방법이든) 단말기도 사라진다(2026-11-13 사용자).
                rso.FindProperty("retiredBy").stringValue = ManagementOpenId; Refs(rso.FindProperty("hideOnOpen"), rv.gameObject);
                rso.ApplyModifiedPropertiesWithoutUndo();
                var anyDoor = root.gameObject.AddComponent<ExplorationAllOf>(); var aso = new SerializedObject(anyDoor);
                var ways = aso.FindProperty("required"); ways.arraySize = 3;
                ways.GetArrayElementAtIndex(0).stringValue = ManagementCableId; ways.GetArrayElementAtIndex(1).stringValue = ManagementLatchId; ways.GetArrayElementAtIndex(2).stringValue = ManagementKeycardId;
                aso.FindProperty("any").boolValue = true; aso.FindProperty("openTarget").stringValue = ManagementOpenId; aso.ApplyModifiedPropertiesWithoutUndo();
                notes.Add($"관리실 문: 배선·안쪽 빗장·키 카드 단말기 {readerAt:F1}");
            }

            // 탈출로 배선 4가닥: 배관함이 있던 벽면을 타고 올라가 숨겨진 문 위 틀로 들어간다. 처음부터 드러나 있고 모두 잘라야 한다.
            var hiddenDoor = layout.Find("B1_MachineRoom/Exploration_A/machineDoor");
            float wallX = MachineWestX, floorY = rust.position.y, doorEdgeZ = hiddenDoor.position.z + 1.05f;
            var colors = new[] { "cable-red", "cable-yellow", "cable-green", "cable-cyan" };
            var escapeCables = new EscapeCuttableCable[colors.Length]; var cableIds = new string[colors.Length];
            for (int n = 0; n < colors.Length; n++)
            {
                // 위치는 구조도(관리실 배선과 같은 기계실 남서 모서리): 녹슨 벽 옆 서벽을 타고 올라가 벽 위로 넘어간다(2026-11-13 사용자).
                // 남쪽 모서리(녹슨 벽)에서 1.4m 떨어진 곳부터: 모서리에 붙어 있으면 비스듬히 쏜 톱날이 옆 벽에 먼저 막혔다.
                float cableZ = MachineSouthZ + EscapeCableStart + n * .42f;
                var cableGo = new GameObject("EscapeCable_" + (n + 1), typeof(MeshFilter), typeof(MeshRenderer)); cableGo.transform.SetParent(root, false);
                cableGo.transform.position = new Vector3(wallX + CableStandoff, floorY, cableZ);
                // 벽에서 띄운 배선을 잡아 주는 고정쇠(벽면 ~ 배선).
                foreach (float clampY in new[] { .45f, 2.1f, 3.9f })
                    Plain(Box(root, "CableClamp", iron, new Vector3(wallX, floorY + clampY, cableZ - .045f), new Vector3(wallX + CableStandoff + CableRadius + .01f, floorY + clampY + .05f, cableZ + .045f), false));
                var cable = cableGo.AddComponent<EscapeCuttableCable>(); var cso = new SerializedObject(cable);
                var path = new[] { new Vector3(0, 0, 0), new Vector3(0, 1.2f, .03f), new Vector3(0, 2.6f, -.03f), new Vector3(0, 4.7f, 0), new Vector3(-.14f - CableStandoff, 4.96f, 0), new Vector3(-.36f - CableStandoff, 4.99f, 0) };
                var points = cso.FindProperty("localPoints"); points.arraySize = path.Length;
                for (int i = 0; i < path.Length; i++) points.GetArrayElementAtIndex(i).vector3Value = path[i];
                cableIds[n] = EscapeCableId + "-" + (n + 1);
                cso.FindProperty("radius").floatValue = CableRadius; cso.FindProperty("openTarget").stringValue = cableIds[n];
                cso.FindProperty("insulationMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CableMaterials + colors[n] + ".mat");
                cso.FindProperty("copperMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CableMaterials + "cable-copper-core.mat");
                cso.ApplyModifiedPropertiesWithoutUndo(); cable.Rebuild(); escapeCables[n] = cable;
            }

            // 톱날 발사기: 관리실 배선과 탈출로 배선 4가닥을 모두 자를 수 있게 연결하고 작업실 남쪽 벽 앞 바닥으로 옮긴다. 문은 배선마다 따로 열린다.
            var sso = new SerializedObject(saw);
            Refs(sso.FindProperty("cables"), new Object[] { hidden }.Concat(escapeCables).ToArray()); sso.FindProperty("openTarget").stringValue = "";
            // 톱날 판정 반지름: 탈출로 씬 기준값(1.1m, 지름 2.2m)은 대기실에서 발사 순간 옆 벽·드럼통·녹슨 벽 상자에 닿아 바로 사라졌다
            // (2026-11-13 플레이: 발사도 절단도 안 됨). 장치 크기(0.7배)에 맞는 0.35m로 줄인다. 이 씬의 발사기에만 적용.
            sso.FindProperty("projectileRadius").floatValue = SawProjectileRadius;
            // 날아가는 톱날의 모습도 판정 크기에 맞춘다(발사 때 본체 크기는 1로 되돌려지므로 자식들의 크기를 바꾼다).
            if (sso.FindProperty("projectileVisual").objectReferenceValue is GameObject shotVisual)
            {
                float shotExtent = 0f;
                foreach (var shotR in shotVisual.GetComponentsInChildren<Renderer>(true))
                {
                    var shotLb = shotR.localBounds;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        var shotLocal = shotLb.center + Vector3.Scale(shotLb.extents, new Vector3((bit & 1) != 0 ? 1 : -1, (bit & 2) != 0 ? 1 : -1, (bit & 4) != 0 ? 1 : -1));
                        var shotInRoot = shotVisual.transform.InverseTransformPoint(shotR.transform.TransformPoint(shotLocal));
                        shotExtent = Mathf.Max(shotExtent, Mathf.Abs(shotInRoot.x), Mathf.Abs(shotInRoot.z));
                    }
                }
                float shotFit = shotExtent > .001f ? SawProjectileRadius / shotExtent : 1f;
                if (Mathf.Abs(shotFit - 1f) > .03f)
                    foreach (Transform shotChild in shotVisual.transform)
                    { Undo.RecordObject(shotChild, "Fit saw projectile"); shotChild.localScale *= shotFit; shotChild.localPosition *= shotFit; PrefabUtility.RecordPrefabInstancePropertyModifications(shotChild); }
                notes.Add($"톱날 모습 반지름 {shotExtent:F2} → {SawProjectileRadius:F2}(자식 {shotVisual.transform.childCount}개)");
            }
            sso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(saw);
            Undo.RecordObject(saw.transform, "Move saw"); saw.transform.position = SawSpot;
            // 충돌 범위(들고 갈 때 걸림 판정도 이 상자를 쓴다): 원래 1.15 x 1.5 x 1.05m → 0.6 x 1.2 x 0.6m로도 계단·난간에 걸렸다(2026-11-13 플레이).
            // 계단을 오르내릴 때 앞쪽 바닥·난간에 닿지 않게 0.35 x 0.45 x 0.35m로 줄이고 바닥에서 0.75m 띄운다. 손잡이 충돌체는 끈다.
            if (sso.FindProperty("loadCollider").objectReferenceValue is BoxCollider load)
            {
                Undo.RecordObject(load, "Shrink saw collider");
                var want = new Vector3(.35f, .45f, .35f); var t = load.transform; var size = load.size; var scale = t.lossyScale;
                for (int axis = 0; axis < 3; axis++)
                {
                    var dir = Vector3.zero; dir[axis] = 1; var w = t.TransformDirection(dir);
                    int wa = Mathf.Abs(w.x) >= Mathf.Abs(w.y) && Mathf.Abs(w.x) >= Mathf.Abs(w.z) ? 0 : Mathf.Abs(w.y) >= Mathf.Abs(w.z) ? 1 : 2;
                    size[axis] = want[wa] / Mathf.Max(.0001f, Mathf.Abs(scale[axis]));
                }
                var centre = load.bounds.center; centre.y = saw.transform.position.y + .75f + want.y * .5f;
                load.size = size; load.center = t.InverseTransformPoint(centre);
                PrefabUtility.RecordPrefabInstancePropertyModifications(load);
            }
            foreach (var mesh in saw.GetComponentsInChildren<MeshCollider>(true)) { if (!mesh.enabled) continue; Undo.RecordObject(mesh, "Disable handle collider"); mesh.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(mesh); }
            PrefabUtility.RecordPrefabInstancePropertyModifications(saw.transform);

            // 관리실 컴퓨터: 감시 콘솔 자판에 단말기 화면을 붙인다. 위 칸 종말 날짜(낡은 신문의 날짜 2024년 09월 12일), 아래 칸 RUN.
            var computer = keyboard.GetComponent<ExplorationGate>() ?? Undo.AddComponent<ExplorationGate>(keyboard.gameObject);
            var pso = new SerializedObject(computer);
            pso.FindProperty("targetId").stringValue = ComputerId; pso.FindProperty("label").stringValue = "관리실 컴퓨터";
            pso.FindProperty("kind").enumValueIndex = (int)ExplorationGateKind.Keypad;
            pso.FindProperty("password").stringValue = ComputerDate; pso.FindProperty("passwordExplicit").boolValue = true;
            pso.FindProperty("terminalWord").stringValue = "RUN";
            // 암호가 맞으면 단말기 화면에 탈출로 문 카메라 영상이 뜬다(2026-11-13 사용자). 문 앞 트인 쪽 위에서 문을 내려다본다.
            {
                var exitDoor = layout.Find("B1_EscapeEntrance/Exploration_A/escapeDoor");
                var template = layout.Find("B1_MachineRoom/Exploration_A/MachineRoomMonitorCamera");
                if (exitDoor != null)
                {
                    // 문이 서 있는 면의 앞쪽 = 숨겨진 문(기계실)에서 걸어오는 쪽. 그쪽 통로 위에서 문을 본다.
                    var eb = Bounds(exitDoor); bool alongX = eb.size.x < eb.size.z;
                    var towardMachine = (hiddenDoor != null ? hiddenDoor.position : eb.center + Vector3.right) - eb.center;
                    Vector3 openDir = alongX ? new Vector3(Mathf.Sign(towardMachine.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(towardMachine.z));
                    float farthest = Physics.Raycast(eb.center + openDir * ((alongX ? eb.extents.x : eb.extents.z) + .15f), openDir, out var seen, 12f, ~0, QueryTriggerInteraction.Ignore) ? seen.distance : 12f;
                    var camGo = template != null && template.GetComponent<Camera>() != null ? Object.Instantiate(template.gameObject, root) : new GameObject("Cam", typeof(Camera));
                    camGo.name = "ExitDoorFeedCamera"; camGo.transform.SetParent(root, true);
                    float camBack = Mathf.Clamp(farthest - .4f, 2f, 5.5f);
                    var camEye = eb.center + openDir * ((Mathf.Abs(openDir.x) > .5f ? eb.extents.x : eb.extents.z) + camBack) + Vector3.up * Mathf.Min(1.4f, eb.extents.y + .6f) + Vector3.Cross(Vector3.up, openDir) * .9f;
                    camGo.transform.SetPositionAndRotation(camEye, Quaternion.LookRotation(eb.center - camEye));
                    var feedCam = camGo.GetComponent<Camera>(); feedCam.enabled = false; feedCam.useOcclusionCulling = false; feedCam.targetTexture = null; feedCam.fieldOfView = 58f; feedCam.nearClipPlane = .1f; feedCam.farClipPlane = 40f;
                    foreach (var listener in camGo.GetComponents<AudioListener>()) Object.DestroyImmediate(listener);
                    pso.FindProperty("monitorCamera").objectReferenceValue = feedCam; pso.FindProperty("feedOpenTarget").stringValue = EscapeRouteId;
                    notes.Add($"문 카메라 {camEye:F1}");
                }
            }
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
            var escapeDoor = layout.Find("B1_EscapeEntrance/Exploration_A/escapeDoor");
            Rewire(escapeDoor, EscapeRouteId);
            // 탈출로 문은 바로 사라지지 않고 옆 벽 속으로 밀려 열린다(관리실 컴퓨터 화면에서 열리는 모습이 보이게, 2026-11-13 사용자).
            if (escapeDoor != null && escapeDoor.GetComponent<ExplorationGate>() is ExplorationGate escapeGate)
            {
                var eso = new SerializedObject(escapeGate); var edb = Bounds(escapeDoor);
                bool doorAlongZ = edb.size.z > edb.size.x; float doorWidth = doorAlongZ ? edb.size.z : edb.size.x;
                Vector3 slideDir = doorAlongZ ? Vector3.forward : Vector3.right;
                // 벽이 더 길게 이어진 쪽으로 민다.
                float Room(Vector3 dir) => Physics.Raycast(edb.center + dir * (doorWidth * .5f + .05f) + Vector3.up * .3f, dir, out var wallEnd, 6f, ~0, QueryTriggerInteraction.Ignore) ? wallEnd.distance : 6f;
                if (Room(-slideDir) < Room(slideDir)) slideDir = -slideDir;
                eso.FindProperty("openSlide").vector3Value = slideDir * (doorWidth + .05f); eso.FindProperty("openSlideSeconds").floatValue = 2.5f;
                var moving = eso.FindProperty("hideOnOpen");
                for (int i = 0; i < moving.arraySize; i++) if (moving.GetArrayElementAtIndex(i).objectReferenceValue is GameObject part)
                    foreach (var piece in part.GetComponentsInChildren<Transform>(true)) if (GameObjectUtility.GetStaticEditorFlags(piece.gameObject) != 0) GameObjectUtility.SetStaticEditorFlags(piece.gameObject, 0);
                eso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(escapeGate);
                notes.Add($"탈출로 문 열림 방향 {slideDir}");
            }
            // 조명 회로: 관리실 배선·탈출로 배선 어느 것이 잘려도 한 번 꺼졌다 켜진다. 다시 지은 새 공간 조명도 회로에 넣는다.
            // 기계실 안에서도 꺼진 것이 보이게, 회로에서 빠져 있던 기계실 쪽 조명도 넣는다.
            var power = layout.GetComponentInChildren<NightPower>(true);
            if (power != null)
            {
                var nso = new SerializedObject(power); var circuit = nso.FindProperty("circuit");
                for (int i = circuit.arraySize - 1; i >= 0; i--) if (circuit.GetArrayElementAtIndex(i).objectReferenceValue == null) circuit.DeleteArrayElementAtIndex(i);
                var have = Enumerable.Range(0, circuit.arraySize).Select(i => circuit.GetArrayElementAtIndex(i).objectReferenceValue).ToHashSet();
                var extra = layout.Find(RootName).GetComponentsInChildren<Light>(true).AsEnumerable();
                foreach (var roomName in new[] { "B1_MachineRoom", "B1_EscapeCorridor", "B1_EscapeEntrance", "B1_Office" })
                    if (layout.Find(roomName) is Transform powered) extra = extra.Concat(powered.GetComponentsInChildren<Light>(true));
                int addedLights = 0;
                foreach (var light in extra.Where(l => !have.Contains(l)).Distinct())
                { circuit.InsertArrayElementAtIndex(circuit.arraySize); circuit.GetArrayElementAtIndex(circuit.arraySize - 1).objectReferenceValue = light; addedLights++; }
                Refs(nso.FindProperty("blackoutCables"), new Object[] { hidden }.Concat(escapeCables).ToArray());
                nso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(power);
                notes.Add($"조명 회로 +{addedLights}, 정전 배선 {1 + escapeCables.Length}가닥");
            }
            var panel = layout.Find("B1_MachineRoom/Exploration_A/HiddenPanel")?.GetComponent<ExplorationSlide>();
            if (panel != null) { Set(panel, "openedBy", EscapeRouteId); PrefabUtility.RecordPrefabInstancePropertyModifications(panel); }

            Physics.SyncTransforms();
            notes.Add($"탈출로 배선 4가닥 z {MachineSouthZ + EscapeCableStart:F2}~{MachineSouthZ + EscapeCableStart + 3 * .42f:F2} / 톱날 발사기 {SawSpot}");
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
