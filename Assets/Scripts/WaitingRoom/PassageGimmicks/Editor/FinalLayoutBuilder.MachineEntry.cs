using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 6단계: 기계실 입구(숙소 환풍구가 내려오는 설비실, 2026-11-12 사용자). 다시 실행할 수 있다.
    // - 설비 상자를 밟고 뛰어내리던 것을 조립식 비계 계단(누군가 몰래 세운 탈출로)으로 바꿔 오르내릴 수 있게 한다.
    // - 기계실의 배관·배선이 올라와 각 공간으로 갈라지는 중간 지점: 동벽 수직 배관 3줄이 서·남·북으로 갈라진다.
    // - 도형으로 만든 설비 상자·소켓 함·전등은 끄고 에셋(제어반·기계·배관·전선·작업등)으로 대신한다.
    // 설비실 뼈대(벽·덕트·셔터)는 숙소 공간 프리팹 그대로 두고, 새 물체는 FinalLayout/MachineEntry에 둔다.
    public static partial class FinalLayoutBuilder
    {
        const string Factory = "Assets/_ThirdParty/LeartesStudios/HDRP/Art/Prefabs/";
        const string Apocalypse = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/";
        // 설비실 안쪽 면과 높이(DormVentBuilder와 같은 값).
        const float EntryX0 = 23.85f, EntryX1 = 27.85f, EntryZ0 = 71.7f, EntryZ1 = 78.5f, EntryFloor = -5f, EntryRoof = 4.5f, DuctFloor = 2.3f;
        // 비계 계단: 폭 1.16m 두 줄, 계단 길이 2.31m, 남·북 층계참. 5단으로 7.3m를 내려간다.
        const float LaneW = 1.16f, StairRun = 2.31f, StairZ0 = 73f, StairZ1 = StairZ0 + StairRun, LandN = 76.5f;
        const int Flights = 5;

        [MenuItem("CurtainCall/Waiting Room/Final Layout/6. Build Machine Room Entry")]
        static void EntryMenu() => Debug.Log(BuildMachineEntry());

        public static string BuildMachineEntry()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            var room = layout != null ? layout.Find("S02_Dormitory/DormVentRoute/UtilityRoom") : null;
            if (room == null) return "숙소 환풍구 설비실을 찾지 못했습니다(WaitingRoom 씬을 여세요).";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: machine room entry");
            // 도형으로 만든 것들을 끈다(공간 프리팹의 이 씬 덮어쓰기).
            int muted = 0;
            foreach (var t in room.Cast<Transform>().Where(t => t.name is "Machinery" or "CageLamp" or "LampWire")) muted += Mute(t);
            foreach (var name in new[] { "PowerSocket/SocketBox", "PowerSocket/SocketSlot" }) muted += Mute(room.Find(name));

            var old = layout.Find(RootName + "/MachineEntry"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "MachineEntry");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Machine entry");

            // ---- 비계 계단 ----
            var tower = Child(root, "ScaffoldStairs");
            float rise = (DuctFloor - EntryFloor) / Flights, lane1 = EntryX0, lane2 = EntryX0 + LaneW, east = EntryX0 + 2 * LaneW;
            for (int k = 0; k < Flights; k++)
            {
                // k번째 단(위에서부터): 짝수는 서쪽 줄에서 북쪽으로, 홀수는 동쪽 줄에서 남쪽으로 내려간다.
                bool north = k % 2 == 0; float high = DuctFloor - k * rise, low = high - rise, x0 = north ? lane1 : lane2;
                var stair = Spawn(tower, Factory + "SM_Scaffolding_Stairs_01_SM_Scaffolding_Stairs_01.prefab", "Flight_" + (k + 1));
                stair.localScale = new Vector3(1, rise / 1.217f, north ? 1 : -1);
                stair.position = new Vector3(x0 + LaneW - .1f, low, north ? StairZ0 + .1f : StairZ1 - .1f);
                // 계단 턱에 걸리지 않게 밟는 면은 경사판으로, 난간 쪽(안쪽·바깥쪽)은 얇은 막이로 대신한다.
                foreach (var mc in stair.GetComponentsInChildren<MeshCollider>()) mc.enabled = false;
                float zHigh = north ? StairZ0 : StairZ1, zLow = north ? StairZ1 : StairZ0;
                Slope(tower, "Ramp_" + (k + 1), new Vector3(x0 + LaneW * .5f, high, zHigh), new Vector3(x0 + LaneW * .5f, low, zLow), LaneW - .14f, .1f, 0);
                Slope(tower, "Guard_" + (k + 1), new Vector3(x0 + LaneW - .05f, high, zHigh), new Vector3(x0 + LaneW - .05f, low, zLow), .06f, 1.0f, 1.0f);
                // 동쪽 줄은 서쪽(아래 단이 지나는 쪽)으로도 떨어지지 않게 막는다.
                if (!north) Slope(tower, "GuardInner_" + (k + 1), new Vector3(x0 + .03f, high, zHigh), new Vector3(x0 + .03f, low, zLow), .06f, 1.0f, 1.0f);
                // 층계참: 맨 위(덕트 앞)와 각 단이 끝나는 곳. 마지막 단은 바닥에 닿는다.
                if (k == 0) Landing(tower, "Landing_Top", high, EntryZ0, StairZ0, lane1, east);
                if (k < Flights - 1) Landing(tower, "Landing_" + (k + 1), low, north ? StairZ1 : EntryZ0, north ? LandN : StairZ0, lane1, east);
            }
            // 기둥: 층계참 네 귀를 바닥에서 받친다(남쪽은 맨 위 층계참까지, 북쪽은 북쪽 맨 위 층계참까지).
            foreach (float x in new[] { lane1 + .06f, lane2, east - .06f })
            {
                foreach (float z in new[] { EntryZ0 + .06f, StairZ0 - .05f }) Post(tower, x, z, DuctFloor);
                foreach (float z in new[] { StairZ1 + .05f, LandN - .06f }) Post(tower, x, z, DuctFloor - rise);
            }

            // ---- 설비: 동벽 수직 배관 3줄이 서·남·북으로 갈라진다 ----
            var plant = Child(root, "Plant");
            float px = EntryX1 - .3f; float[] rz = { 73.7f, 74.3f, 74.9f };
            foreach (float z in rz) Run(plant, new Vector3(px, EntryFloor, z), Vector3.up, EntryRoof - EntryFloor);
            Run(plant, new Vector3(px, .5f, rz[0]), Vector3.back, rz[0] - EntryZ0);            // 남쪽으로
            Run(plant, new Vector3(px, 4.2f, rz[1]), Vector3.left, px - EntryX0);                 // 서쪽으로(맨 위 층계참 머리 위)
            Run(plant, new Vector3(px, -1.5f, rz[2]), Vector3.forward, EntryZ1 - rz[2]);          // 북쪽으로
            // 제어반: 동벽 앞 바닥. 동력 소켓 대상의 자식으로 둬서 가까이 가면 제어반에 상호작용 외곽선이 뜬다.
            var socket = room.Find("PowerSocket/Socket");
            foreach (var stale in socket.Cast<Transform>().Where(t => t.name == "ControlPanel").ToArray()) Undo.DestroyObjectImmediate(stale.gameObject);
            var panel = Spawn(socket, Factory + "SM_Control_Panel.prefab", "ControlPanel");
            panel.SetPositionAndRotation(new Vector3(EntryX1 - .62f, EntryFloor, 75.92f), Quaternion.Euler(0, -90f, 0));
            // 남동 모서리 기계와 북벽 밸브 묶음(밸브는 기존 것과 같은 자리).
            var machine = Spawn(plant, Factory + "SM_Machinery_06.prefab", "Compressor");
            machine.localScale = Vector3.one * .5f; machine.position = new Vector3(EntryX1 - .75f, EntryFloor, EntryZ0 + .78f);
            var valves = Spawn(plant, Apocalypse + "SM_Prop_Pipes_Valves_01.prefab", "ValveBank");
            valves.SetPositionAndRotation(new Vector3(25.9f, EntryFloor, EntryZ1 - .22f), Quaternion.Euler(0, 180f, 0));
            // 전선: 남벽을 타고 지붕에서 기계로 내려오는 묶음, 동벽 위쪽을 따라 남북으로 가는 두 줄.
            var mb = Bounds(machine);
            for (int i = 0; i < 4; i++) Wire(plant, new Vector3(mb.center.x - .2f + i * .12f, mb.max.y - .05f, EntryZ0 + .06f), Vector3.up, EntryRoof - mb.max.y + .05f);
            foreach (float y in new[] { 3.0f, 3.15f }) Wire(plant, new Vector3(EntryX1 - .05f, y, EntryZ0), Vector3.forward, EntryZ1 - EntryZ0);
            // 작업등: 지붕에 매단다(계단 머리 위는 피한다).
            foreach (var p in new[] { new Vector3(25.0f, EntryRoof, 77.6f), new Vector3(26.9f, EntryRoof, 72.5f) })
            {
                var work = Spawn(plant, Apocalypse + "SM_Prop_Light_Industrial_Ceiling_01.prefab", "WorkLight");
                work.position = Hang(p, Apocalypse + "SM_Prop_Light_Industrial_Ceiling_01.prefab");
                Glow(work, new Vector3(0, -.2f, 0) + Vector3.up * (Bounds(work).min.y - work.position.y), 9f, 11f);
            }
            // 계단을 따라 놓인 작업등: 층계참 구석과 바닥(누군가 드나들며 켜 둔 것). 높이마다 빛이 닿게 한다.
            float r2 = (DuctFloor - EntryFloor) / Flights;
            Lamp(plant, "Lamp_01", new Vector3(EntryX0 + .25f, DuctFloor, EntryZ0 + .25f), 5f, 6f);
            Lamp(plant, "Lamp_01", new Vector3(EntryX0 + 2 * LaneW - .25f, DuctFloor - r2, LandN - .25f), 5f, 6f);
            Lamp(plant, "Lamp_01", new Vector3(EntryX0 + 2 * LaneW - .25f, DuctFloor - 2 * r2, EntryZ0 + .25f), 5f, 6f);
            Lamp(plant, "Lamp_01", new Vector3(EntryX0 + 2 * LaneW - .25f, DuctFloor - 3 * r2, LandN - .25f), 5f, 6f);
            Lamp(plant, "Lamp_01", new Vector3(EntryX0 + .3f, EntryFloor, 77.0f), 6f, 7f);

            // ---- 누군가 드나든 흔적 ----
            var traces = Child(root, "Traces");
            // 공구 수레는 셔터로 가는 길을 피해 북동 구석에 둔다.
            Spawn(traces, Factory + "SM_ToolCart.prefab", "ToolCart").position = new Vector3(27.3f, EntryFloor, 77.55f);
            Spawn(traces, Apocalypse + "SM_Prop_Crate_01.prefab", "Crate").position = new Vector3(26.0f, EntryFloor, 72.3f);
            Spawn(traces, "Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/Prefabs/SM_Woodpallet_1.prefab", "Pallet").position = new Vector3(lane2 + LaneW * .5f, EntryFloor, 74.2f);

            // 동력 소켓·배터리 자리를 제어반 앞으로 옮긴다(공간 프리팹의 위치 덮어쓰기). 판정·셔터 연결은 그대로.
            Physics.SyncTransforms();
            var pb = Bounds(panel);
            // 소켓 대상을 옮기면 자식인 제어반도 따라가므로, 옮긴 뒤 제어반을 제자리로 되돌린다.
            var panelPose = (panel.position, panel.rotation);
            Move(socket, new Vector3(pb.min.x - .05f, EntryFloor + 1.15f, pb.center.z));
            panel.SetPositionAndRotation(panelPose.position, panelPose.rotation);
            // 배터리: 계단을 내려와 셔터로 가는 길목의 트인 바닥.
            Move(room.Find("PowerSocket/BatterySpot"), new Vector3(25.4f, EntryFloor, 77.0f));
            RustDoor(room.Find("PowerSocket/Shutter"), root);

            RegisterLights(layout, root); RegisterLights(layout, socket);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return $"도형 {muted}개 끔 / 계단 {Flights}단(단 높이 {rise:F2}m) / 제어반 x {pb.min.x:F2}~{pb.max.x:F2} z {pb.min.z:F2}~{pb.max.z:F2}";
        }

        static Transform Spawn(Transform parent, string path, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); go.name = name;
            return go.transform;
        }

        // 층계참: 비계 발판 두 장(가로 두 줄)을 크기에 맞춰 깔고, 트인 쪽에 난간을 세운다.
        static void Landing(Transform parent, string name, float y, float z0, float z1, float x0, float x1)
        {
            var g = Child(parent, name); float depth = z1 - z0, half = (x1 - x0) * .5f;
            for (int i = 0; i < 2; i++)
            {
                var tile = Spawn(g, Factory + "SM_Scaffolding_Flooring.prefab", "Deck");
                tile.localScale = new Vector3(half / .96f, 1, depth / .96f);
                tile.position = new Vector3(x0 + half * (i + 1) + .02f * half / .96f, y - .06f, z1 + .02f * depth / .96f);
            }
            // 동쪽 가장자리 난간.
            var rail = Spawn(g, Factory + "SM_Scaffolding_Railing_100m.prefab", "Rail");
            rail.localScale = new Vector3(1, 1, depth / .94f); rail.position = new Vector3(x1 + .03f, y - .06f, z1 + .03f * depth / .94f);
            // 북쪽 층계참은 북쪽 가장자리도 트여 있다.
            if (z1 > StairZ1 + .1f)
            {
                var end = Spawn(g, Factory + "SM_Scaffolding_Railing_200m.prefab", "RailEnd");
                end.localScale = new Vector3(1, 1, (x1 - x0) / 1.94f); end.SetPositionAndRotation(new Vector3(x1 + .03f * (x1 - x0) / 1.94f, y - .06f, z1 - .1f), Quaternion.Euler(0, 90f, 0));
            }
        }

        static void Post(Transform parent, float x, float z, float top)
        {
            var post = Spawn(parent, Factory + "SM_Scaffolding_Stand_Vertical_SM_Scaffolding_Stand_Vertical.prefab", "Post");
            post.position = new Vector3(x, EntryFloor, z); post.localScale = new Vector3(1, top - .06f - EntryFloor, 1);
            Spawn(parent, Factory + "SM_Scaffolding_Base.prefab", "PostBase").position = new Vector3(x, EntryFloor, z);
        }

        // 보이지 않는 경사 충돌체: high→low를 잇는 면이 윗면(lift = 0)이거나, 그 위로 lift만큼 솟은 막이.
        static void Slope(Transform parent, string name, Vector3 high, Vector3 low, float width, float thickness, float lift)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            // 막이는 기울어진 상자라 끝이 층계참 위로 넘어와 줄 사이 이동을 막는다(2026-11-12 플레이에서 확인). 양 끝 0.75m를 비운다.
            if (lift > 0) { var trim = (low - high).normalized * .75f; high += trim; low -= trim; }
            var dir = low - high; float length = dir.magnitude;
            go.transform.SetPositionAndRotation((high + low) * .5f, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, lift > 0 ? lift : thickness, length); box.center = new Vector3(0, lift > 0 ? lift * .5f : -thickness * .5f, 0);
        }

        // 배관 한 줄: from에서 direction으로 length만큼. 중간 굵기 배관(4m)을 이어 붙이고 마지막 토막만 줄인다.
        static void Run(Transform parent, Vector3 from, Vector3 direction, float length)
        {
            // 배관 에셋은 로컬 -x로 뻗는다.
            var rotation = Quaternion.FromToRotation(Vector3.left, direction);
            for (float done = 0; done < length - .01f; done += 4f)
            {
                var pipe = Spawn(parent, Factory + "SM_Pipe_Medium_01.prefab", "Pipe");
                pipe.SetPositionAndRotation(from + direction * done, rotation);
                pipe.localScale = new Vector3(Mathf.Min(4f, length - done) / 4f, 1, 1);
            }
        }

        // 전선 에셋은 로컬 +z로 3m.
        static void Wire(Transform parent, Vector3 from, Vector3 direction, float length)
        {
            var rotation = Quaternion.LookRotation(direction, Mathf.Abs(direction.y) > .9f ? Vector3.back : Vector3.up);
            for (float done = 0; done < length - .01f; done += 3f)
            {
                var wire = Spawn(parent, Factory + "SM_Wire_Medium.prefab", "Wire");
                wire.SetPositionAndRotation(from + direction * done, rotation);
                wire.localScale = new Vector3(1, 1, Mathf.Min(3f, length - done) / 3f);
            }
        }

        // 천장에 매다는 물체: 에셋의 맨 윗면이 천장에 닿는 위치.
        static Vector3 Hang(Vector3 ceilingPoint, string path)
        {
            var probe = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float top = Bounds(probe.transform).max.y; Object.DestroyImmediate(probe);
            return ceilingPoint - Vector3.up * top;
        }

        // 지붕 등에 다는 점 조명.
        static void Glow(Transform parent, Vector3 localOffset, float intensity, float range)
        {
            var go = new GameObject("Glow"); go.transform.SetParent(parent, false); go.transform.localPosition = localOffset;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = WarmBulb; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
        }

        // 기계실로 나가는 문: 임시 판(틈으로 건너편이 보임) 대신 문틀을 꽉 채우는 녹슨 철문. 셔터의 자식이라 함께 올라간다.
        static void RustDoor(Transform shutter, Transform frameParent)
        {
            if (shutter == null) return;
            foreach (var staleDoor in shutter.Cast<Transform>().Where(t => t.name == "RustDoor").ToArray()) Undo.DestroyObjectImmediate(staleDoor.gameObject);
            var blocker = shutter.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault();
            if (blocker == null) return;
            // 기존 겉모습(임시 판)은 끄고 충돌체만 남긴다.
            foreach (var r in shutter.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r, "Hide shutter slat"); r.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(r); }
            var rust = AssetDatabase.LoadAssetAtPath<Material>("Assets/_ThirdParty/Materials/WaitingArea/V4_RustyMetal.mat"); // 불투명 녹슨 철(LA_RustPatina는 덧칠용 반투명이라 비쳐 보인다)
            var iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_SteelPlate.mat");
            var b = blocker.bounds; var door = Child(shutter, "RustDoor"); door.position = Vector3.zero;
            // 문짝: 벽 구멍 안쪽에 두면 비스듬히 볼 때 문짝 가장자리 옆으로 건너편이 보인다(2026-11-12 플레이에서 확인).
            // → 구멍보다 사방 0.2m 큰 철판을 설비실 쪽 벽면과 기계실 쪽 벽면에 한 장씩 덧대 구멍을 완전히 덮는다.
            float grow = .2f, roomFace = EntryX0, plantFace = EntryX0 - .55f;
            foreach (var (x0, x1, outward) in new[] { (roomFace, roomFace + .06f, 1f), (plantFace - .06f, plantFace, -1f) })
            {
                Plain(Box(door, "Leaf", rust, new Vector3(x0, b.min.y, b.min.z - grow), new Vector3(x1, b.max.y + grow, b.max.z + grow), false));
                float face = outward > 0 ? x1 : x0;
                foreach (float y in new[] { .25f, .95f, 1.65f })
                    Plain(Box(door, "Band", iron, new Vector3(Mathf.Min(face, face + outward * .025f), b.min.y + y, b.min.z - grow), new Vector3(Mathf.Max(face, face + outward * .025f), b.min.y + y + .1f, b.max.z + grow), false));
                Plain(Box(door, "Handle", iron, new Vector3(Mathf.Min(face, face + outward * .06f), b.min.y + 1.05f, b.center.z - .2f), new Vector3(Mathf.Max(face, face + outward * .06f), b.min.y + 1.11f, b.center.z + .2f), false));
            }
            // 문틀(움직이지 않음): 설비실 쪽 문짝 바깥을 두르는 철 테두리.
            var frame = Child(frameParent, "DoorFrame");
            float fx0 = roomFace, fx1 = roomFace + .1f, z0 = b.min.z - grow, z1 = b.max.z + grow, top = b.max.y + grow;
            Box(frame, "JambS", iron, new Vector3(fx0, b.min.y, z0 - .12f), new Vector3(fx1, top + 2.02f, z0), false);
            Box(frame, "JambN", iron, new Vector3(fx0, b.min.y, z1), new Vector3(fx1, top + 2.02f, z1 + .12f), false);
            Box(frame, "Lintel", iron, new Vector3(fx0, top + 1.9f, z0), new Vector3(fx1, top + 2.02f, z1), false);
        }

        // 새 조명을 대기실 조명 회로(NightPower)에 넣어 전체 소등 때 함께 꺼지게 한다.
        static void RegisterLights(Transform layout, Transform root)
        {
            var power = layout.GetComponentInChildren<NightPower>(true); if (power == null) return;
            var pso = new SerializedObject(power); var circuit = pso.FindProperty("circuit");
            for (int i = circuit.arraySize - 1; i >= 0; i--) if (circuit.GetArrayElementAtIndex(i).objectReferenceValue == null) circuit.DeleteArrayElementAtIndex(i);
            var have = Enumerable.Range(0, circuit.arraySize).Select(i => circuit.GetArrayElementAtIndex(i).objectReferenceValue).ToHashSet();
            foreach (var light in root.GetComponentsInChildren<Light>(true).Where(l => !have.Contains(l)))
            { circuit.InsertArrayElementAtIndex(circuit.arraySize); circuit.GetArrayElementAtIndex(circuit.arraySize - 1).objectReferenceValue = light; }
            pso.ApplyModifiedProperties();
        }

        static void Move(Transform t, Vector3 position)
        {
            if (t == null) return;
            Undo.RecordObject(t, "Move"); t.position = position; PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
    }
}
