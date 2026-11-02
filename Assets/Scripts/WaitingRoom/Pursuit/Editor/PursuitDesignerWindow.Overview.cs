using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        int sideAxis;
        static readonly Color CurrentRoomColor = new(1, .61f, .12f);
        GUIStyle currentRoomStyle;

        void DrawCurrentRoomLabel()
        {
            currentRoomStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontStyle = FontStyle.Bold, normal = { textColor = CurrentRoomColor } };
            int total = EditorApplication.isPlaying ? livePlacedCount : TotalPlacedCount();
            string caption = HasTargetRoom ? $"현재 방 · {Room.name}\n방 {(EditorApplication.isPlaying ? LiveRoomCount(Room) : PlacedCount(Room))}체 / 전체 {total}체" : $"중심 방 없음 · 전체 {total}체\n층 또는 화면 위치를 변경하세요";
            var rect = GUILayoutUtility.GetRect(new GUIContent(caption), currentRoomStyle, GUILayout.MinHeight(38));
            EditorGUI.DrawRect(rect, new Color(.3f, .18f, .055f));
            GUI.Label(new Rect(rect.x + 5, rect.y + 3, rect.width - 10, rect.height - 6), caption, currentRoomStyle);
        }
        float FloorHeight(PursuitRoom room) => EditorApplication.isPlaying && liveFloors.TryGetValue(room, out float floor)
            ? floor : Mathf.Round(PursuitAuthoring.FloorReference(room));
        static void OutlineBox(Rect box, Color color, float width)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(width, new Vector3(box.xMin, box.yMin), new Vector3(box.xMax, box.yMin),
                new Vector3(box.xMax, box.yMax), new Vector3(box.xMin, box.yMax), new Vector3(box.xMin, box.yMin));
        }
        void DrawOverview(Rect rect) => DrawStructure(rect, false);
        void DrawSideOverview(Rect rect) => DrawStructure(rect, true);
        void DrawStructure(Rect area, bool side)
        {
            // All positions below are local to this clip, including hit tests.
            GUI.BeginClip(area);
            var canvas = new Rect(0, 0, area.width, area.height);
            EditorGUI.DrawRect(canvas, new Color(.055f, .065f, .08f));
            Rect plot = new(side ? 39 : 6, 8, area.width - (side ? 47 : 12), area.height - (side ? 23 : 16));
            var visible = VisibleRooms();
            if (visible.Length == 0) { GUI.EndClip(); return; }
            var all = PursuitAuthoring.Bounds(visible[0]); foreach (var room in visible) all.Encapsulate(PursuitAuthoring.Bounds(room));
            var top = Fit(all, plot, 1.08f);
            float minH = sideAxis == 0 ? all.min.z : all.min.x, maxH = sideAxis == 0 ? all.max.z : all.max.x;
            var levels = side ? visible.Select(FloorHeight).Distinct().OrderBy(y => y).ToArray() : Array.Empty<float>();
            float minY = side ? levels[0] - 1.5f : 0, maxY = side ? levels[levels.Length - 1] + 3.5f : 1;
            Vector2 Position(Vector3 p) => side
                ? new Vector2(Mathf.Lerp(plot.xMin, plot.xMax, Mathf.InverseLerp(minH - 2, maxH + 2, sideAxis == 0 ? p.z : p.x)),
                    plot.yMax - (p.y - minY) / (maxY - minY) * plot.height)
                : Project(p, top, plot);
            Rect RoomBox(PursuitRoom room)
            {
                var bounds = PursuitAuthoring.Bounds(room);
                var min = bounds.min; var max = bounds.max;
                if (side) { min.y = FloorHeight(room); max.y = min.y + 2.1f; }
                var a = Position(min); var b = Position(max);
                return Rect.MinMaxRect(a.x, b.y, b.x, a.y);
            }
            int Count(PursuitRoom room) => EditorApplication.isPlaying
                ? LiveRoomCount(room)
                : PlacedCount(room);

            Handles.BeginGUI();
            if (side) {
                foreach (float level in levels) {
                    float y = Position(new Vector3(minH, level, minH)).y;
                    Handles.color = new Color(.45f, .5f, .6f, .5f);
                    Handles.DrawDottedLine(new Vector3(plot.xMin, y), new Vector3(plot.xMax, y), 3);
                    GUI.Label(new Rect(1, y - 9, 38, 18), $"{level:+0;-0;0}m", EditorStyles.miniLabel);
                }
                GUI.Label(new Rect(plot.x, canvas.height - 16, plot.width, 16), sideAxis == 0 ? "Z → · 층 높이 기준 구조도" : "X → · 층 높이 기준 구조도", EditorStyles.miniLabel);
            }
            var boxes = visible.Select(room => new { room, i = Array.IndexOf(rooms, room), box = RoomBox(room) }).ToArray();
            foreach (var entry in boxes.OrderByDescending(e => e.box.width * e.box.height)) {
                EditorGUI.DrawRect(entry.box, new Color(.2f, .25f, .31f, .7f));
                OutlineBox(entry.box, new Color(.5f, .57f, .65f, .65f), 1);
            }
            // Selected room is always painted last: overlapping floors/zones cannot obscure it.
            var selectedBox = HasTargetRoom ? RoomBox(Room) : Rect.zero;
            if (HasTargetRoom) EditorGUI.DrawRect(selectedBox, new Color(1, .5f, .06f, .46f));
            foreach (var entry in boxes) {
                var label = new Rect(entry.box.center.x - 12, entry.box.center.y - 10, 24, 20);
                // Side projection collapses depth, so overlapping room counts would be unreadable.
                if ((HasTargetRoom && entry.room == Room) || (!side && entry.box.width >= 12 && !selectedBox.Overlaps(label)))
                    GUI.Label(label, Count(entry.room).ToString(), badgeStyle);
                GUI.Label(entry.box, new GUIContent("", $"{entry.room.name} · {Count(entry.room)}체 · 바닥 {FloorHeight(entry.room):0}m"));
            }
            if (EditorApplication.isPlaying) {
                foreach (var actor in director.Actors) {
                    if (actor == null || !OnActiveFloor(actor.transform.position)) continue;
                    Handles.color = actor.State == PursuitMotion.Run ? Color.red : actor.State == PursuitMotion.Walk ? Color.cyan : Color.gray;
                    Handles.DrawSolidDisc(Position(actor.transform.position), Vector3.forward, 3);
                }
                Handles.color = LivePlayerColor;
                foreach (var player in livePlayers)
                    if (player != null && OnActiveFloor(player.position)) Handles.DrawSolidDisc(Position(player.position), Vector3.forward, 4);
            } else foreach (var room in visible) foreach (var item in room.Placements ?? Array.Empty<PursuitPlacement>()) {
                if (item == null) continue;
                Handles.color = Color.cyan; Handles.DrawSolidDisc(Position(item.transform.position), Vector3.forward, 2.5f);
            }
            if (HasTargetRoom) OutlineBox(selectedBox, CurrentRoomColor, 3);
            // An outer bracket remains visible even for a very narrow connecting passage.
            var bracket = selectedBox; bracket.xMin -= 2; bracket.xMax += 2; bracket.yMin -= 2; bracket.yMax += 2;
            if (HasTargetRoom) OutlineBox(bracket, new Color(1, .72f, .25f, .8f), 1);
            Handles.EndGUI();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && canvas.Contains(Event.current.mousePosition)) {
                var hit = boxes.Where(e => e.box.Contains(Event.current.mousePosition)).OrderBy(e => e.box.width * e.box.height).FirstOrDefault();
                if (hit != null) ChooseRoom(hit.i);
                Event.current.Use();
            }
            GUI.EndClip();
        }
    }
}
