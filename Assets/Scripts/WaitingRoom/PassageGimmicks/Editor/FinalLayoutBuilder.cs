using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    /// <summary>
    /// 대기실 최종본 배치(2026-11-12 사용자 구조도). WaitingRoom 씬에서 한 단계씩 실행하는 편집 도구.
    /// 1단계 <see cref="SwapStageAndCraftRooms"/>: 무대 입구 통로·공연 스테이지 입구를 로비 북쪽으로,
    /// 작업실·소품실을 로비 바로 남쪽(작업실 서쪽·소품실 동쪽, 사이에 로비로 통하는 전실)으로 옮긴다.
    /// 방 내부는 그대로 두고 방 공간 프리팹 인스턴스의 위치·방향만 바꾼다.
    /// </summary>
    public static partial class FinalLayoutBuilder
    {
        const string LayoutPath = "WaitingRoom_LayoutA";
        const string SwapMarker = "FinalLayout_Swap";
        // 로비 원형 벽 중심(WaitingRoomLayoutA.DomeC)과 반지름(Frame8LobbyR).
        internal static readonly Vector2 DomeC = new(-6f, 77.4f);
        internal const float LobbyR = 14.25f;
        // 무대 입구 통로: 로비 중심으로 반 바퀴 돌린 뒤 서쪽으로 3.7m(계단실 서벽 x-0.4와 연습실 동쪽 끝 x-19.2 사이).
        internal const float StageShiftX = -3.7f;
        // 남쪽 전실(로비 옛 무대 입구 안쪽) x 범위. 서쪽에 작업실 문, 동쪽에 소품실 문이 마주 본다.
        internal const float VestibuleX0 = -5.2f, VestibuleX1 = -2.1f;
        // 두 방과 전실의 남쪽 끝(긴 복도 북벽 바깥 면).
        internal const float CraftSouthZ = 51.35f;

        // 옮기기 전 방 범위(바닥 충돌체 기준).
        static readonly Rect PropFrom = Rect.MinMaxRect(-10.8f, 95.3f, -0.2f, 107.6f);
        static readonly Rect WorkshopFrom = Rect.MinMaxRect(-20.19f, 99.4f, -9.88f, 117.63f);
        // 옮긴 뒤 방 범위. 작업실은 문이 동쪽(전실), 소품실은 문이 서쪽(전실)을 본다.
        internal static readonly Rect WorkshopTo = Rect.MinMaxRect(VestibuleX0 - WorkshopFrom.height, CraftSouthZ, VestibuleX0, CraftSouthZ + WorkshopFrom.width);
        internal static readonly Rect PropTo = Rect.MinMaxRect(VestibuleX1, CraftSouthZ, VestibuleX1 + PropFrom.height, CraftSouthZ + PropFrom.width);

        [MenuItem("CurtainCall/Waiting Room/Final Layout/1. Swap Stage Corridor And Craft Rooms")]
        static void SwapMenu() => Debug.Log(SwapStageAndCraftRooms());

        public static string SwapStageAndCraftRooms()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            if (layout.Find(SwapMarker) != null) return "이미 옮겼습니다.";
            var scene = layout.gameObject.scene;
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: swap stage corridor and craft rooms");
            var notes = new List<string>();

            // 무대 입구 통로: 반 바퀴 회전 (x, z) → (2cx - x, 2cz - z).
            var stagePivot = new Vector3(DomeC.x + StageShiftX * .5f, 0, DomeC.y);
            foreach (var name in new[] { "S06_StageCorridor", "S07_StageEntrance", "SecurityCctv/Cctv_무대 통로" })
                Rigid(layout.Find(name), stagePivot, stagePivot, 180f);
            notes.Add("무대 입구 통로 → 북쪽");

            // 작업실: 동남쪽 모서리가 새 자리의 북동쪽 모서리로(문이 동쪽). 소품실: 서남쪽 모서리가 북서쪽 모서리로(문이 서쪽).
            int extras = 0;
            extras += MoveRoom(layout, scene, "S10_Workshop", WorkshopFrom, new Vector3(WorkshopFrom.xMax, 0, WorkshopFrom.yMin), new Vector3(WorkshopTo.xMax, 0, WorkshopTo.yMax), -90f);
            extras += MoveRoom(layout, scene, "S04_PropRoom", PropFrom, new Vector3(PropFrom.xMin, 0, PropFrom.yMin), new Vector3(PropTo.xMin, 0, PropTo.yMax), 90f);
            notes.Add($"작업실·소품실 → 로비 남쪽(딸린 영역·순찰 지점·배치 기물 {extras}개 함께 이동)");

            // 방이 떠난 자리의 연결 통로와 방이 들어온 자리의 로비 장식을 끈다(2단계에서 새 연결을 만든다).
            int muted = 0;
            foreach (var path in new[] { "FacilityConnections/Passage_F8_PropRoom", "FacilityConnections/Passage_F8_Stage",
                         "S01_Lobby/Frame8Lobby/AbandonedFactory/WorkshopBranch", "S01_Lobby/Frame8Lobby/AbandonedFactory/FactoryPipes_F8_PropRoom" })
                muted += Mute(layout.Find(path));
            var south = Rect.MinMaxRect(WorkshopTo.xMin - 2f, 40f, PropTo.xMax + 1f, 63f);
            var lobby = layout.Find("S01_Lobby");
            bool InSouth(Renderer r) { var c = r.bounds.center; return c.y > -1f && south.Contains(new Vector2(c.x, c.z)); }
            foreach (var r in lobby.GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy || !InSouth(r)) continue;
                // 통째로 남쪽에 있는 가장 큰 묶음을 끈다(로비 바로 아래 묶음은 건드리지 않는다).
                var t = r.transform;
                while (t.parent != lobby && t.parent.parent != lobby && t.parent.GetComponentsInChildren<Renderer>(false).All(InSouth)) t = t.parent;
                muted += Mute(t);
            }
            notes.Add($"옛 연결 통로·로비 남쪽 장식 {muted}개 끔");

            // 시설 전체를 감싸는 어두운 바깥 상자는 북쪽으로 길어진 통로 끝(z 169)을 덮도록 늘린다.
            foreach (var shell in layout.Find("ExteriorEnclosure").GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == "DarkOuterShell"))
            {
                var b = shell.GetComponent<Renderer>().bounds; float want = 176f;
                if (b.max.z >= want) continue;
                var t = shell.transform; Undo.RecordObject(t, "Extend outer shell");
                float k = (want - b.min.z) / b.size.z; float min = b.min.z;
                t.localScale = Vector3.Scale(t.localScale, new Vector3(1, 1, k));
                var nb = shell.GetComponent<Renderer>().bounds; t.position += Vector3.forward * (min - nb.min.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }

            var marker = new GameObject(SwapMarker); Undo.RegisterCreatedObjectUndo(marker, "Final layout marker");
            marker.transform.SetParent(layout, false); marker.SetActive(false);
            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
            return string.Join(" / ", notes);
        }

        static int MoveRoom(Transform layout, UnityEngine.SceneManagement.Scene scene, string room, Rect footprint, Vector3 from, Vector3 to, float yaw)
        {
            Rigid(layout.Find(room), from, to, yaw);
            int count = 0;
            var area = layout.Find("ExplorationTimer/Frame8Areas/Area_" + room);
            if (area != null) { Rigid(area, from, to, yaw); count++; }
            var pursuit = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "PursuitSystem")?.transform.Find(room);
            if (pursuit != null) foreach (Transform point in pursuit) { Rigid(point, from, to, yaw); count++; }
            // 사용자가 씬에 직접 놓은 기물(씬 루트)도 방과 함께 간다.
            var grown = Rect.MinMaxRect(footprint.xMin - .3f, footprint.yMin - .3f, footprint.xMax + .3f, footprint.yMax + .3f);
            foreach (var root in scene.GetRootGameObjects())
            {
                var p = root.transform.position;
                if (!root.activeSelf || root.transform == layout || p.y < -1f || !grown.Contains(new Vector2(p.x, p.z))) continue;
                if (root.GetComponentInChildren<Renderer>() == null) continue;
                Rigid(root.transform, from, to, yaw); count++;
            }
            return count;
        }

        // from 점을 to로 옮기면서 y축으로 yaw만큼 돌린다.
        static void Rigid(Transform t, Vector3 from, Vector3 to, float yaw)
        {
            if (t == null) return;
            Undo.RecordObject(t, "Move room");
            var q = Quaternion.Euler(0, yaw, 0); var p = t.position;
            var moved = to + q * (new Vector3(p.x, 0, p.z) - from); moved.y = p.y;
            t.SetPositionAndRotation(moved, q * t.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        static int Mute(Transform t)
        {
            if (t == null || !t.gameObject.activeSelf) return 0;
            Undo.RecordObject(t.gameObject, "Mute moved-room leftover"); t.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            return 1;
        }
    }
}
