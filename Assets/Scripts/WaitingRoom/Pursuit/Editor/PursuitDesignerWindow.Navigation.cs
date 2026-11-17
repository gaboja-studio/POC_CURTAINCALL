using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        bool viewInitialized, autoTargetRoom = true, centerHasRoom = true, panning;
        Vector3 viewCenter;
        float viewFloor, viewSpan;
        int panControl, panInputId;
        float ActiveFloor => viewInitialized ? viewFloor : FloorHeight(Room);
        bool HasTargetRoom => Room != null && OnActiveFloor(Room) && (!autoTargetRoom || !viewInitialized || centerHasRoom);
        bool OnActiveFloor(PursuitRoom room) => OnFloor(room, ActiveFloor);
        // 계단·환풍구 출구처럼 여러 층에 걸친 방은 지정한 추가 층에서도 같은 방으로 보인다.
        bool OnFloor(PursuitRoom room, float floor) => Mathf.Abs(FloorHeight(room) - floor) <= .75f
            || (room.ExtraFloors ?? Array.Empty<float>()).Any(y => Mathf.Abs(y - floor) <= .75f);
        // 배치·감시 지점을 찍을 층: 방 기본 층이 아니라 지금 보고 있는 층.
        float? PlacementFloor(PursuitRoom room) => room != null && Mathf.Abs(FloorHeight(room) - ActiveFloor) > .75f ? ActiveFloor : (float?)null;
        PursuitRoom[] VisibleRooms() => rooms.Where(OnActiveFloor).ToArray();
        bool OnActiveFloor(Vector3 position)
        {
            return rooms.Length > 0 && Mathf.Abs(NearestFloor(position.y) - ActiveFloor) <= .75f;
        }
        float NearestFloor(float height)
        {
            float nearest = ActiveFloor, distance = float.PositiveInfinity;
            foreach (var room in rooms) {
                float floor = FloorHeight(room), d = Mathf.Abs(height - floor);
                if (d < distance) { nearest = floor; distance = d; }
            }
            return nearest;
        }
        void ChooseFloor(float floor)
        {
            if (EditorApplication.isPlaying) liveFollowPlayer = false;
            EndPan(); selected = null; pointIndex = -1; addPoint = false; dragging = false;
            var candidates = rooms.Where(r => OnFloor(r, floor)).ToArray();
            if (candidates.Length == 0) return;
            var center = viewInitialized ? viewCenter : PursuitAuthoring.Bounds(Room).center;
            viewFloor = floor; autoTargetRoom = true;
            if (viewInitialized) TargetCenterRoom();
            if (!viewInitialized || !centerHasRoom) {
                var closest = candidates.OrderBy(r => {
                    var b = PursuitAuthoring.Bounds(r);
                    return new Vector2(b.center.x - center.x, b.center.z - center.z).sqrMagnitude;
                }).First();
                ChooseRoom(Array.IndexOf(rooms, closest));
            }
            Repaint();
        }
        void SelectPlacement(PursuitPlacement placement)
        {
            selected = placement;
            if (placement == null) return;
            int owner = Array.FindIndex(rooms, r => OnActiveFloor(r) && (r.Placements ?? Array.Empty<PursuitPlacement>()).Contains(placement));
            if (owner >= 0) { roomIndex = owner; centerHasRoom = true; }
        }
        static int PlacedCount(PursuitRoom room) => room.UsePlacements
            ? (room.Placements ?? Array.Empty<PursuitPlacement>()).Count(p => p != null) : Mathf.Max(0, room.Count);
        int TotalPlacedCount() => rooms.Where(r => r.UsePlacements)
            .SelectMany(r => r.Placements ?? Array.Empty<PursuitPlacement>()).Where(p => p != null).Distinct().Count()
            + rooms.Where(r => !r.UsePlacements).Sum(PlacedCount);
        void FrameRoom()
        {
            if (EditorApplication.isPlaying) liveFollowPlayer = false;
            EndPan(); viewInitialized = false; centerHasRoom = true; zoom = 1;
        }
        Rect lastTopArea;
        // 선택 층의 모든 방이 들어오는 평면 범위(창 비율에 맞춤). 방이 없으면 현재 범위.
        Bounds FloorFit(Rect area)
        {
            var visible = VisibleRooms();
            if (visible.Length == 0) return viewBounds;
            var all = PursuitAuthoring.Bounds(visible[0]);
            foreach (var room in visible) all.Encapsulate(PursuitAuthoring.Bounds(room));
            return Fit(all, area, 1.06f);
        }
        // 기본 축소 한계는 0.5×지만, 층 전체가 한 화면에 들어올 만큼은 항상 더 축소할 수 있다.
        float MinZoom(Rect area) => lastTopArea.width > 1 ? Mathf.Min(.5f, viewSpan / Mathf.Max(.01f, FloorFit(area).size.z)) : .5f;
        void FrameFloor()
        {
            if (EditorApplication.isPlaying) liveFollowPlayer = false;
            if (!viewInitialized || lastTopArea.width <= 1) return;
            EndPan();
            var fit = FloorFit(lastTopArea);
            viewCenter = new Vector3(fit.center.x, viewFloor, fit.center.z);
            zoom = Mathf.Clamp(viewSpan / Mathf.Max(.01f, fit.size.z), MinZoom(lastTopArea), 4);
            TargetCenterRoom(); Repaint();
        }
        void EnsureView(Rect area)
        {
            lastTopArea = area;
            if (!viewInitialized) {
                var bounds = Fit(PursuitAuthoring.Bounds(Room), area, 1.12f);
                viewCenter = bounds.center; viewFloor = FloorHeight(Room); viewSpan = bounds.size.z;
                viewInitialized = true; centerHasRoom = true;
            }
            float z = viewSpan / zoom;
            viewBounds = new Bounds(new Vector3(viewCenter.x, viewFloor, viewCenter.z),
                new Vector3(z * area.width / Mathf.Max(1, area.height), 4, z));
        }
        void DrawNavigationToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying && liveFollowPlayer)) {
                    bool next = GUILayout.Toggle(autoTargetRoom, "중심 방 자동 선택", EditorStyles.toolbarButton, GUILayout.Width(125));
                    if (next != autoTargetRoom) { autoTargetRoom = next; if (viewInitialized) TargetCenterRoom(); }
                }
                var floors = rooms.Select(FloorHeight).Distinct().OrderByDescending(y => y).ToArray();
                float current = ActiveFloor;
                int level = Array.IndexOf(floors, current);
                int chosen = EditorGUILayout.Popup(Mathf.Max(0, level), floors.Select(y => $"층 {y:+0;-0;0}m").ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(85));
                if (chosen != level && level >= 0) {
                    ChooseFloor(floors[chosen]);
                }
                GUILayout.Label($"선택 층 {VisibleRooms().Length}개 방 · {VisiblePlacements().Length}체 · 평면/측면/배치 공통", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("층 전체 보기 (A)", EditorStyles.toolbarButton, GUILayout.Width(110))) FrameFloor();
                if (GUILayout.Button("현재 방 맞춤 (F)", EditorStyles.toolbarButton, GUILayout.Width(110))) FrameRoom();
            }
        }
        void TargetCenterRoom()
        {
            if (!autoTargetRoom || !viewInitialized) return;
            int found = Array.IndexOf(rooms, RoomAt(viewCenter));
            centerHasRoom = found >= 0;
            if (found < 0 || found != roomIndex) { selected = null; pointIndex = -1; addPoint = false; }
            if (found >= 0) roomIndex = found;
            // Deliberately preserve centre, zoom and floor: targeting a smaller room must not snap the camera.
        }
        PursuitRoom RoomAt(Vector3 position)
        {
            PursuitRoom found = null; float smallest = float.PositiveInfinity;
            for (int i = 0; i < rooms.Length; i++) {
                if (!OnActiveFloor(rooms[i])) continue;
                var b = PursuitAuthoring.Bounds(rooms[i]);
                if (position.x < b.min.x || position.x > b.max.x || position.z < b.min.z || position.z > b.max.z) continue;
                float size = b.size.x * b.size.z;
                if (size < smallest) { smallest = size; found = rooms[i]; }
            }
            return found;
        }
        void BeginPan(int control)
        {
            if (EditorApplication.isPlaying) liveFollowPlayer = false;
            panning = true; panControl = control; GUIUtility.hotControl = control;
            GUIUtility.keyboardControl = 0;
        }
        void EndPan()
        {
            if (panControl != 0 && GUIUtility.hotControl == panControl) GUIUtility.hotControl = 0;
            panning = false; panControl = 0;
        }
        bool HandlePan(Rect rect)
        {
            var e = Event.current;
            int id = panInputId = GUIUtility.GetControlID("PursuitPan".GetHashCode(), FocusType.Passive);
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F && GUIUtility.keyboardControl == 0) { FrameRoom(); e.Use(); return true; }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.A && GUIUtility.keyboardControl == 0) { FrameFloor(); e.Use(); return true; }
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && (e.button == 2 || (EditorApplication.isPlaying && e.button == 0))) {
                BeginPan(id); e.Use(); return true;
            }
            if (panning && e.type == EventType.MouseDrag) {
                viewCenter.x -= e.delta.x * viewBounds.size.x / rect.width;
                viewCenter.z += e.delta.y * viewBounds.size.z / rect.height;
                TargetCenterRoom(); Repaint(); e.Use(); return true;
            }
            if (panning && (e.type == EventType.MouseUp || e.type == EventType.Ignore || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))) {
                EndPan(); e.Use(); return true;
            }
            if (panning) EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);
            return false;
        }
        PursuitPlacement[] VisiblePlacements() => VisibleRooms()
            .SelectMany(r => r.Placements ?? Array.Empty<PursuitPlacement>()).Where(p => p != null).Distinct().ToArray();
    }
}
