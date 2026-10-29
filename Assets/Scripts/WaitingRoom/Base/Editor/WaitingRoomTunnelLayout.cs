using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// ⑥ 무대 입구 통로를 프로토타입 터널 구성(길이 ~42m 터널 + 끝의 ⑦ 공연 스테이지 입구)으로 다시 배치한다.
    /// 유지: 대기실 문(로비 x=10·셔터), 커튼 방 열림 3·닫힘 5(열림 = 커튼 뒤 어두운 방), 집결 판정 구역, ⑦ 공연 입구 커튼.
    /// 「대기실 2차 기획서」 3.3.1에 없는 터널 옆방(공연 준비실·장비 보관소·무대장치 작업실)은 두지 않는다(2026-10-20 사용자 지시).
    /// 그레이박스 로컬 좌표. 터널 늘이기는 한 번만 하고(벽 W_06_East 위치로 판별), 옆방 제거·집결 구역 옮기기는 이전 배치에서 한 번만 옮긴다.
    /// </summary>
    public static class WaitingRoomTunnelLayout
    {
        const string GreyboxName = "WaitingRoomGreybox";
        const string WallMaterial = "Assets/Scenes/Tests/WaitingRoom/Base/Materials/GB_Wall.mat";
        const float OldEnd = 37f;
        public const float TunnelEnd = 52f;
        const float StageShift = 15f;             // ⑦ 입구 이동량(x 36.5 → 51.5)
        const float CurtainOffset = 3.7f;         // 커튼 방 장식의 벽 앞 위치(|z|)

        public static readonly Rect Tunnel = Rect.MinMaxRect(10f, -4f, TunnelEnd, 4f);
        /// <summary>집결 판정 구역 처음 값: ⑦ 커튼(x ~51.6) 바로 앞 4m × 통로 폭 7m. 이후는 StageEntranceGather 인스펙터에서 조정.</summary>
        public static readonly Rect Gather = Rect.MinMaxRect(47.2f, -3.5f, 51.2f, 3.5f);
        const float GatherHeight = 3f;

        /// <summary>열린 커튼(커튼 뒤 어두운 방) 자리: x, 쪽(+1 북 / -1 남).</summary>
        public static readonly (float x, int side)[] OpenDoors = { (15.1f, -1), (25.5f, -1), (25.5f, 1) };

        /// <summary>닫힌 커튼(통과 불가) 자리: x, 쪽.</summary>
        public static readonly (float x, int side)[] ClosedDoors = { (14f, 1), (36.5f, 1), (44.5f, 1), (38f, -1), (46f, -1) };

        /// <summary>터널 벽의 모든 커튼 자리(열림·닫힘) — 벽쪽 장식이 가리면 안 되는 x 구간.</summary>
        public static IEnumerable<(float x0, float x1, int side)> CurtainSpans =>
            OpenDoors.Concat(ClosedDoors).Select(d => (d.x - 1.6f, d.x + 1.6f, d.side));

        [MenuItem("Tools/CurtainCall/대기실/무대 입구 통로 터널 배치(프로토타입 구성)")]
        static void ApplyMenu()
        {
            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == GreyboxName);
            if (root == null) { Debug.LogError($"[WaitingRoomTunnel] '{GreyboxName}' 없음"); return; }
            bool applied = Apply(root.transform);
            WaitingRoomLayoutFitter.Fit(root.transform);
            WaitingRoomLayoutFitter.FitMouseLookZone(scene, root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(applied ? "[WaitingRoomTunnel] 터널·커튼 8곳·집결 구역·공연 입구를 다시 배치했습니다." : "[WaitingRoomTunnel] 이미 터널 배치됨 — 옆방 정리·카메라 구역만 갱신");
        }

        /// <summary>터널을 늘이고(한 번만) 옆방을 정리하고 집결 구역을 새 방식으로 옮긴다. 터널을 처음 늘였으면 true.</summary>
        public static bool Apply(Transform greybox)
        {
            var corridor = greybox.Find("S06_StageCorridor_Temp");
            var east = greybox.Find("Walls/W_06_East/Seg0");
            bool first = east != null && Mathf.Abs(east.localPosition.x - OldEnd) < 0.05f;
            if (first)
            {
                var walls = greybox.Find("Walls");
                foreach (var n in new[] { "W_06_North_08_South", "W_06_South", "W_06_East" })
                {
                    var old = walls.Find(n);
                    if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                }
                BuildTunnelWalls(walls);
                MoveCurtainRooms(corridor.Find("CurtainRooms"));
                ShiftStageEntrance(greybox.Find("S07_StageEntrance_Temp"));

                var floor = corridor.Find("Floor");
                Undo.RecordObject(floor, "Tunnel Layout");
                floor.localPosition = new Vector3(Tunnel.center.x, floor.localPosition.y, 0f);
                floor.localScale = new Vector3(Tunnel.width, floor.localScale.y, Tunnel.height);
            }
            RemoveSideRooms(greybox);
            MigrateGatherZone(corridor);
            return first;
        }

        // ── 벽 ─────────────────────────────────────────────

        static void BuildTunnelWalls(Transform walls)
        {
            Wall(walls, "W_06_North_08_South", true, Tunnel.yMax, Tunnel.xMin, TunnelEnd);
            Wall(walls, "W_06_South", true, Tunnel.yMin, Tunnel.xMin, TunnelEnd);
            Wall(walls, "W_06_East", false, TunnelEnd, Tunnel.yMin, Tunnel.yMax);
        }

        static void Wall(Transform parent, string name, bool alongX, float k, float from, float to)
        {
            var w = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(w.gameObject, "Tunnel Layout");
            w.SetParent(parent, false);
            float height = WaitingRoomGreyboxBuilder.WallHeight, len = to - from, mid = (from + to) / 2f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Seg0";
            go.transform.SetParent(w, false);
            go.transform.localPosition = alongX ? new Vector3(mid, height / 2f, k) : new Vector3(k, height / 2f, mid);
            go.transform.localScale = alongX ? new Vector3(len, height, 0.3f) : new Vector3(0.3f, height, len);
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(WallMaterial);
        }

        /// <summary>
        /// 이전 배치에서 만든 터널 옆방 3개(벽 W_09_*, 바닥, 이름표, 터널 벽 출입구)를 지우고,
        /// 열린 커튼 3곳은 원래 그레이박스처럼 커튼 뒤 어두운 방(DarkRoom)을 다시 켠다. 없으면 아무 일도 하지 않는다.
        /// </summary>
        static void RemoveSideRooms(Transform greybox)
        {
            var walls = greybox.Find("Walls");
            foreach (var w in walls.Cast<Transform>().Where(t => t.name.StartsWith("W_09_")).ToArray())
                Undo.DestroyObjectImmediate(w.gameObject);
            foreach (var n in new[] { "W_06_North_08_South", "W_06_South" })
            {
                var wall = walls.Find(n);
                if (wall == null || wall.childCount <= 1) continue; // 출입구가 없으면 조각이 하나
                Undo.DestroyObjectImmediate(wall.gameObject);
                Wall(walls, n, true, n == "W_06_South" ? Tunnel.yMin : Tunnel.yMax, Tunnel.xMin, TunnelEnd);
            }
            var corridor = greybox.Find("S06_StageCorridor_Temp");
            foreach (var t in corridor.Cast<Transform>().Where(t => t.name == "RoomFloors" || t.name.StartsWith("Label_R")).ToArray())
                Undo.DestroyObjectImmediate(t.gameObject);
            var rooms = corridor.Find("CurtainRooms");
            if (rooms == null) return;
            foreach (var dark in rooms.GetComponentsInChildren<Transform>(true).Where(t => t.name == "DarkRoom"))
                if (!dark.gameObject.activeSelf) { Undo.RecordObject(dark.gameObject, "Tunnel Layout"); dark.gameObject.SetActive(true); }
        }

        // ── 커튼 방 8곳: 열림 3·닫힘 5 ─────────────

        static void MoveCurtainRooms(Transform rooms)
        {
            if (rooms == null) return;
            var open = rooms.Cast<Transform>().Where(t => t.name.StartsWith("CurtainRoom_Open_")).OrderBy(t => t.name).ToArray();
            var closed = rooms.Cast<Transform>().Where(t => t.name.StartsWith("CurtainRoom_Closed_")).OrderBy(t => t.name).ToArray();
            for (int i = 0; i < open.Length && i < OpenDoors.Length; i++)
                MoveCurtain(open[i], OpenDoors[i].x, OpenDoors[i].side);
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
            foreach (var t in group.GetComponentsInChildren<TextMesh>(true))
            {
                Undo.RecordObject(t.transform, "Tunnel Layout");
                t.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, side), Vector3.up);
            }
        }

        /// <summary>
        /// 이전 방식(상자 크기 = 오브젝트 배율 14×3×7)의 집결 구역을 ⑦ 앞 4m로 옮기고, 범위를 StageEntranceGather 인스펙터 값으로 넘긴다.
        /// 이미 넘긴 구역(배율 1)은 인스펙터에서 정한 값을 존중해 건드리지 않는다.
        /// </summary>
        static void MigrateGatherZone(Transform corridor)
        {
            var zone = corridor.Find("GatherZone");
            if (zone == null || (zone.localScale - Vector3.one).sqrMagnitude < 0.0001f) return;
            var gather = zone.GetComponent<StageEntranceGather>();
            Undo.RecordObject(zone, "Tunnel Layout");
            zone.localPosition = new Vector3(Gather.center.x, GatherHeight / 2f, Gather.center.y);
            zone.localScale = Vector3.one;
            var mr = zone.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            if (gather == null) return;
            var so = new SerializedObject(gather);
            so.FindProperty("areaCenter").vector3Value = Vector3.zero;
            so.FindProperty("areaSize").vector3Value = new Vector3(Gather.width, GatherHeight, Gather.height);
            so.FindProperty("border").objectReferenceValue = corridor.Find("GatherZone_Border");
            so.ApplyModifiedProperties();
            var border = corridor.Find("GatherZone_Border");
            if (border != null)
                foreach (Transform strip in border) Undo.RecordObject(strip, "Tunnel Layout");
            gather.ApplyArea();
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
    }
}
