using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        readonly Vector3[] liveSightFan = new Vector3[34];
        Rect liveViewport;
        void LiveLine(Vector2 a, Vector2 b, Color color, float width = 1)
        {
            liveLine[0] = a; liveLine[1] = b; Handles.color = color; Handles.DrawAAPolyLine(width, liveLine);
        }
        void DrawLiveMap(Rect area)
        {
            if (area.width <= 1 || area.height <= 1) return;
            liveViewport = area;
            EnsureView(area);
            GUI.BeginClip(area);
            var canvas = new Rect(0, 0, area.width, area.height);
            if (Event.current.type == EventType.Repaint) {
                EditorGUI.DrawRect(canvas, new Color(.055f, .07f, .085f));
                Handles.BeginGUI();
                foreach (var room in liveRoomsBySize) {
                    if (room == null || !OnActiveFloor(room)) continue;
                    var bounds = PursuitAuthoring.Bounds(room);
                    var a = Project(bounds.min, viewBounds, canvas); var b = Project(bounds.max, viewBounds, canvas);
                    var box = Rect.MinMaxRect(a.x, b.y, b.x, a.y);
                    if (!box.Overlaps(canvas)) continue;
                    bool active = HasTargetRoom && room == Room;
                    EditorGUI.DrawRect(box, active ? new Color(.32f, .22f, .10f) : new Color(.13f, .18f, .22f));
                    OutlineBox(box, active ? CurrentRoomColor : new Color(.38f, .45f, .5f), active ? 2 : 1);
                    if (box.width > 65 && box.height > 24)
                        GUI.Label(new Rect(box.x + 4, box.y + 3, box.width - 8, 20), $"{room.name} · {LiveRoomCount(room)}체", EditorStyles.miniLabel);
                }
                DrawLiveWalls(canvas);
                if (liveSelectedActor != null && OnActiveFloor(liveSelectedActor.transform.position)) DrawLiveRoute(canvas);
                foreach (var actor in director.Actors) {
                    if (actor == null || !OnActiveFloor(actor.transform.position)) continue;
                    var p = Project(actor.transform.position, viewBounds, canvas);
                    if (!canvas.Contains(p)) continue;
                    bool active = actor == liveSelectedActor;
                    var color = actor.ChasedPlayer != null ? new Color(1, .2f, .15f)
                        : actor.State == PursuitMotion.Walk ? Color.cyan : Color.gray;
                    if (active) EditorGUI.DrawRect(new Rect(p.x - 7, p.y - 7, 14, 14), Color.white);
                    DrawLiveMarker(p, actor.transform.eulerAngles.y, color, 8);
                    if (active || liveActorLabels)
                        GUI.Label(new Rect(p.x + 10, p.y - 19, 220, 20), $"{actor.name} · {actor.Behaviour}", EditorStyles.whiteMiniLabel);
                    if (active && actor.ChasedPlayer != null)
                        LiveLine(p, Project(actor.ChasedPlayer.transform.position, viewBounds, canvas), Color.red, 2);
                }
                var followed = LiveFollowTarget();
                for (int i = 0; i < livePlayers.Count; i++) {
                    var player = livePlayers[i];
                    if (player == null || !OnActiveFloor(player.position)) continue;
                    var p = Project(player.position, viewBounds, canvas);
                    if (!canvas.Contains(p)) continue;
                    if (liveFollowPlayer && player == followed) OutlineBox(new Rect(p.x - 10, p.y - 10, 20, 20), Color.white, 2);
                    DrawLiveMarker(p, player.eulerAngles.y, LivePlayerColor, 10);
                    GUI.Label(new Rect(p.x + 12, p.y, 180, 20), livePlayerOptions[i + 1], EditorStyles.whiteMiniLabel);
                }
                LiveLine(canvas.center + Vector2.left * 6, canvas.center + Vector2.right * 6, Color.gray);
                LiveLine(canvas.center + Vector2.up * 6, canvas.center + Vector2.down * 6, Color.gray);
                Handles.EndGUI();
                GUI.Label(new Rect(10, 7, canvas.width - 20, 22), $"N ↑  {viewFloor:0}m · {zoom:0.0}× · "
                    + (liveFollowPlayer ? "플레이어 추적" : "자유 모드") + " · " + (HasTargetRoom ? Room.name : "방 외부"), EditorStyles.whiteBoldLabel);
            }
            var e = Event.current;
            bool picked = false;
            if (e.type == EventType.MouseDown && e.button == 0 && canvas.Contains(e.mousePosition)) {
                PursuitActor closest = null; float distance = 13;
                foreach (var actor in director.Actors) {
                    if (actor == null || !OnActiveFloor(actor.transform.position)) continue;
                    float d = Vector2.Distance(e.mousePosition, Project(actor.transform.position, viewBounds, canvas));
                    if (d < distance) { closest = actor; distance = d; }
                }
                if (closest != null) { liveSelectedActor = closest; e.Use(); picked = true; }
            }
            if (!picked) HandleMapInput(canvas);
            GUI.EndClip();
        }
        void DrawLiveMarker(Vector2 p, float yaw, Color color, float size)
        {
            EditorGUI.DrawRect(new Rect(p.x - size * .5f, p.y - size * .5f, size, size), color);
            var forward = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), -Mathf.Cos(yaw * Mathf.Deg2Rad));
            LiveLine(p, p + forward * 17, color, 2);
        }
        void DrawLiveRoute(Rect canvas)
        {
            var actor = liveSelectedActor;
            var points = actor.Placement != null ? actor.Placement.Route(actor.Room) : actor.Room.PatrolPoints;
            if (points != null) for (int i = 0; i < points.Length; i++) {
                var point = points[i]; var next = points[(i + 1) % points.Length];
                if (point == null || !OnActiveFloor(point.position)) continue;
                var p = Project(point.position, viewBounds, canvas);
                if (next != null && OnActiveFloor(next.position)) LiveLine(p, Project(next.position, viewBounds, canvas), new Color(0, .6f, .7f));
                EditorGUI.DrawRect(new Rect(p.x - 3, p.y - 3, 6, 6), Color.cyan);
                GUI.Label(new Rect(p.x + 3, p.y, 30, 18), (i + 1).ToString(), EditorStyles.whiteMiniLabel);
            }
            var rule = director.CurrentStageRules;
            if (rule == null || rule.AlwaysNearest) return;
            var center = actor.transform.position;
            liveSightFan[0] = Project(center, viewBounds, canvas);
            for (int i = 0; i <= 32; i++) {
                float angle = (actor.transform.eulerAngles.y - rule.FieldOfView * .5f + rule.FieldOfView * i / 32) * Mathf.Deg2Rad;
                liveSightFan[i + 1] = Project(center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * rule.SightRadius, viewBounds, canvas);
            }
            Handles.color = new Color(1, .65f, .1f, .09f); Handles.DrawAAConvexPolygon(liveSightFan);
        }
    }
}
