using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// ⑥ 무대 입구 통로를 프로토타입 터널 구성(길이 ~42m 터널 + 옆방 3개 + 끝의 공연 스테이지 입구)으로 다시 배치한다.
    /// 유지: 대기실 문(로비 x=10·셔터), 커튼 방 열림 3·닫힘 5(열림 = 실제 방 출입구), 집결 판정 구역, ⑦ 공연 입구 커튼.
    /// 옆방 3개: 공연 준비실·장비 보관소(남), 무대장치 작업실(북) — 내부는 WaitingRoomMoodDresser가 프로토타입에서 옮긴다.
    /// 그레이박스 로컬 좌표. 이미 배치한 씬에는 다시 적용하지 않는다(벽 W_06_East 위치로 판별).
    /// </summary>
    public static class WaitingRoomTunnelLayout
    {
        const string GreyboxName = "WaitingRoomGreybox";
        const string WallMaterial = "Assets/Scenes/Tests/WaitingRoom/Base/Materials/GB_Wall.mat";
        const string FloorMaterial = "Assets/Scenes/Tests/WaitingRoom/Base/Materials/GB_Floor.mat";
        const float OldEnd = 37f;
        public const float TunnelEnd = 52f;
        public const float DoorWidth = 2.6f, DoorHeight = 2.8f;
        public const float RoomCeiling = 6f;
        const float StageShift = 15f;             // ⑦ 입구 이동량(x 36.5 → 51.5)
        const float CurtainOffset = 3.7f;         // 커튼 방 장식의 벽 앞 위치(|z|)

        public static readonly Rect Tunnel = Rect.MinMaxRect(10f, -4f, TunnelEnd, 4f);
        public static readonly Rect Gather = Rect.MinMaxRect(36f, -3.5f, 50f, 3.5f);

        /// <summary>옆방: 이름, 범위, 출입구 x, 쪽(+1 북 / -1 남), 프로토타입 백스테이지 방 이름, 프로토타입 월드 좌표 + 이동량 = 현재 그레이박스 로컬 좌표.</summary>
        public static readonly (string name, string label, Rect rect, float doorX, int side, string proto, Vector3 offset)[] Rooms =
        {
            ("R1_PrepRoom", "공연 준비실", Rect.MinMaxRect(10f, -13.1f, 21f, -4f), 15.1f, -1, "공연준비실", new Vector3(-148.4f, 0f, -9.5f)), // 서쪽 벽 = 대기실 문 벽(x 10)이라 거울이 벽에 붙게 1.4m 더 당김
            ("R2_EquipmentStore", "장비 보관소", Rect.MinMaxRect(21f, -13.1f, 31.6f, -4f), 25.5f, -1, "장비보관소", new Vector3(-147f, 0f, -9.5f)),
            ("R3_StageWorkshop", "무대장치 작업실", Rect.MinMaxRect(19.4f, 4f, 31.7f, 13.2f), 25.5f, 1, "무대장치작업실", new Vector3(-144f, 0f, -8.5f)),
        };

        /// <summary>닫힌 커튼(통과 불가) 자리: x, 쪽.</summary>
        public static readonly (float x, int side)[] ClosedDoors = { (14f, 1), (36.5f, 1), (44.5f, 1), (38f, -1), (46f, -1) };

        /// <summary>터널 벽의 모든 커튼 자리(열림·닫힘) — 벽쪽 장식이 가리면 안 되는 x 구간.</summary>
        public static IEnumerable<(float x0, float x1, int side)> CurtainSpans =>
            Rooms.Select(r => (r.doorX - 1.6f, r.doorX + 1.6f, r.side)).Concat(ClosedDoors.Select(d => (d.x - 1.6f, d.x + 1.6f, d.side)));

        [MenuItem("Tools/CurtainCall/대기실/무대 입구 통로 터널 배치(프로토타입 구성)")]
        static void ApplyMenu()
        {
            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == GreyboxName);
            if (root == null) { Debug.LogError($"[WaitingRoomTunnel] '{GreyboxName}' 없음"); return; }
            bool applied = Apply(root.transform);
            WaitingRoomLayoutFitter.Fit(root.transform);            // 옆방 카메라 구역 포함해 다시 만든다
            WaitingRoomLayoutFitter.FitMouseLookZone(scene, root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(applied ? "[WaitingRoomTunnel] 터널·옆방 3개·커튼 8곳·집결 구역·공연 입구를 다시 배치했습니다." : "[WaitingRoomTunnel] 이미 터널 배치됨 — 카메라 구역만 갱신");
        }

        public static bool Apply(Transform greybox)
        {
            var east = greybox.Find("Walls/W_06_East/Seg0");
            if (east == null || Mathf.Abs(east.localPosition.x - OldEnd) > 0.05f) return false;

            var corridor = greybox.Find("S06_StageCorridor_Temp");
            var walls = greybox.Find("Walls");
            RebuildWalls(walls);
            BuildRoomFloors(corridor);
            MoveCurtainRooms(corridor.Find("CurtainRooms"));
            MoveGatherZone(corridor);
            ShiftStageEntrance(greybox.Find("S07_StageEntrance_Temp"));
            AddRoomLabels(corridor);

            var floor = corridor.Find("Floor");
            Undo.RecordObject(floor, "Tunnel Layout");
            floor.localPosition = new Vector3(Tunnel.center.x, floor.localPosition.y, 0f);
            floor.localScale = new Vector3(Tunnel.width, floor.localScale.y, Tunnel.height);
            return true;
        }

        // ── 벽 ─────────────────────────────────────────────

        static void RebuildWalls(Transform walls)
        {
            foreach (var n in new[] { "W_06_North_08_South", "W_06_South", "W_06_East" })
            {
                var old = walls.Find(n);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
            var doors = Rooms.Select(r => (r.doorX, r.side)).ToArray();
            Wall(walls, "W_06_North_08_South", true, Tunnel.yMax, Tunnel.xMin, TunnelEnd, doors.Where(d => d.side > 0).Select(d => d.doorX));
            Wall(walls, "W_06_South", true, Tunnel.yMin, Tunnel.xMin, TunnelEnd, doors.Where(d => d.side < 0).Select(d => d.doorX));
            Wall(walls, "W_06_East", false, TunnelEnd, Tunnel.yMin, Tunnel.yMax, new float[0]);

            // 옆방 바깥 벽(대기실 문 쪽 x=10 벽은 기존 벽 사용)
            var r1 = Rooms[0].rect; var r2 = Rooms[1].rect; var r3 = Rooms[2].rect;
            Wall(walls, "W_09_South_Rooms", true, r1.yMin, r1.xMin, r2.xMax, new float[0]);
            Wall(walls, "W_09_Partition_R1R2", false, r1.xMax, r1.yMin, r1.yMax, new float[0]);
            Wall(walls, "W_09_East_R2", false, r2.xMax, r2.yMin, r2.yMax, new float[0]);
            Wall(walls, "W_09_West_R3", false, r3.xMin, r3.yMin, r3.yMax, new float[0]);
            Wall(walls, "W_09_East_R3", false, r3.xMax, r3.yMin, r3.yMax, new float[0]);
            Wall(walls, "W_09_North_R3", true, r3.yMax, r3.xMin, r3.xMax, new float[0]);
        }

        static void Wall(Transform parent, string name, bool alongX, float k, float from, float to, IEnumerable<float> doorCenters)
        {
            var w = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(w.gameObject, "Tunnel Layout");
            w.SetParent(parent, false);
            float height = WaitingRoomGreyboxBuilder.WallHeight;
            int index = 0;
            void Segment(float s0, float s1, float y0, float y1)
            {
                if (s1 - s0 < 0.01f) return;
                float mid = (s0 + s1) / 2f, len = s1 - s0;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Seg" + index++;
                go.transform.SetParent(w, false);
                go.transform.localPosition = alongX ? new Vector3(mid, (y0 + y1) / 2f, k) : new Vector3(k, (y0 + y1) / 2f, mid);
                go.transform.localScale = alongX ? new Vector3(len, y1 - y0, 0.3f) : new Vector3(0.3f, y1 - y0, len);
                go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(WallMaterial);
            }
            float cursor = from;
            foreach (var c in doorCenters.OrderBy(c => c))
            {
                Segment(cursor, c - DoorWidth / 2f, 0f, height);
                Segment(c - DoorWidth / 2f, c + DoorWidth / 2f, DoorHeight, height);
                cursor = c + DoorWidth / 2f;
            }
            Segment(cursor, to, 0f, height);
        }

        static void BuildRoomFloors(Transform corridor)
        {
            var group = new GameObject("RoomFloors").transform;
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Tunnel Layout");
            group.SetParent(corridor, false);
            foreach (var r in Rooms)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Floor_" + r.name;
                go.transform.SetParent(group, false);
                go.transform.localPosition = new Vector3(r.rect.center.x, -0.1f, r.rect.center.y);
                go.transform.localScale = new Vector3(r.rect.width, 0.2f, r.rect.height);
                go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterial);
            }
        }

        // ── 커튼 방 8곳: 열림 3 = 옆방 출입구, 닫힘 5 = 빈 벽 ─────────────

        static void MoveCurtainRooms(Transform rooms)
        {
            if (rooms == null) return;
            var open = rooms.Cast<Transform>().Where(t => t.name.StartsWith("CurtainRoom_Open_")).OrderBy(t => t.name).ToArray();
            var closed = rooms.Cast<Transform>().Where(t => t.name.StartsWith("CurtainRoom_Closed_")).OrderBy(t => t.name).ToArray();
            for (int i = 0; i < open.Length && i < Rooms.Length; i++)
            {
                MoveCurtain(open[i], Rooms[i].doorX, Rooms[i].side);
                var dark = open[i].Find("DarkRoom");
                if (dark != null) { Undo.RecordObject(dark.gameObject, "Tunnel Layout"); dark.gameObject.SetActive(false); } // 뒤에 실제 방이 생김
            }
            for (int i = 0; i < closed.Length && i < ClosedDoors.Length; i++)
                MoveCurtain(closed[i], ClosedDoors[i].x, ClosedDoors[i].side);
        }

        /// <summary>커튼 장식 묶음(자식이 절대 좌표, 묶음은 원점)을 새 문 위치로. 쪽이 바뀌면 180° 돌린다.</summary>
        static void MoveCurtain(Transform group, float x, int side)
        {
            var valance = group.Find("Valance");
            if (valance == null) return;
            float oldX = valance.localPosition.x;
            int oldSide = valance.localPosition.z > 0f ? 1 : -1;
            Undo.RecordObject(group, "Tunnel Layout");
            bool flip = oldSide != side;
            group.localRotation = Quaternion.Euler(0f, flip ? 180f : 0f, 0f);
            var rotated = group.localRotation * new Vector3(oldX, 0f, oldSide * CurtainOffset);
            group.localPosition = new Vector3(x, 0f, side * CurtainOffset) - rotated;
            // 표지는 통로 쪽에서 바로 읽히게(그레이박스 원래 배치는 북쪽이 거울 글자)
            foreach (var t in group.GetComponentsInChildren<TextMesh>(true))
            {
                Undo.RecordObject(t.transform, "Tunnel Layout");
                t.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, side), Vector3.up);
            }
        }

        static void MoveGatherZone(Transform corridor)
        {
            var zone = corridor.Find("GatherZone");
            if (zone != null)
            {
                Undo.RecordObject(zone, "Tunnel Layout");
                zone.localPosition = new Vector3(Gather.center.x, zone.localPosition.y, Gather.center.y);
                zone.localScale = new Vector3(Gather.width, zone.localScale.y, Gather.height);
            }
            var border = corridor.Find("GatherZone_Border");
            if (border == null) return;
            void Set(string n, Vector3 p, Vector3 s)
            {
                var t = border.Find(n);
                if (t == null) return;
                Undo.RecordObject(t, "Tunnel Layout");
                t.localPosition = p;
                if (s != Vector3.zero) t.localScale = s;
            }
            Set("N", new Vector3(Gather.center.x, 0.01f, Gather.yMax - 0.05f), new Vector3(Gather.width, 0.02f, 0.1f));
            Set("S", new Vector3(Gather.center.x, 0.01f, Gather.yMin + 0.05f), new Vector3(Gather.width, 0.02f, 0.1f));
            Set("W", new Vector3(Gather.xMin + 0.05f, 0.01f, 0f), new Vector3(0.1f, 0.02f, Gather.height));
            Set("E", new Vector3(Gather.xMax - 0.05f, 0.01f, 0f), new Vector3(0.1f, 0.02f, Gather.height));
            Set("Label", new Vector3(Gather.center.x, 0.04f, Gather.yMin + 0.9f), Vector3.zero);
        }

        static void ShiftStageEntrance(Transform stage)
        {
            if (stage == null) return;
            foreach (Transform t in stage)
            {
                Undo.RecordObject(t, "Tunnel Layout");
                t.localPosition += new Vector3(StageShift, 0f, 0f);
            }
        }

        /// <summary>옆방 바닥 이름표(통로의 기존 바닥 글씨를 복제해 글만 바꿈).</summary>
        static void AddRoomLabels(Transform corridor)
        {
            var template = corridor.Find("Label");
            if (template == null) return;
            foreach (var r in Rooms)
            {
                var copy = Object.Instantiate(template.gameObject, corridor);
                copy.name = "Label_" + r.name;
                Undo.RegisterCreatedObjectUndo(copy, "Tunnel Layout");
                copy.transform.localPosition = new Vector3(r.rect.center.x, template.localPosition.y, r.rect.center.y);
                var text = copy.GetComponent<TextMesh>();
                if (text != null) text.text = r.label;
            }
        }
    }
}
