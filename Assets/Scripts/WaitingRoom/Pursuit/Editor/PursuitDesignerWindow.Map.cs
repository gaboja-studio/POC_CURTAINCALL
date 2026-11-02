using System;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        Bounds viewBounds;
        static Vector2 Project(Vector3 world, Bounds bounds, Rect rect) => new(
            rect.center.x + (world.x - bounds.center.x) / bounds.size.x * rect.width,
            rect.center.y - (world.z - bounds.center.z) / bounds.size.z * rect.height);
        Vector3 Unproject(Vector2 mouse, Rect rect) => new(
            viewBounds.center.x + (mouse.x - rect.center.x) / rect.width * viewBounds.size.x,
            viewFloor,
            viewBounds.center.z - (mouse.y - rect.center.y) / rect.height * viewBounds.size.z);
        static Bounds Fit(Bounds bounds, Rect rect, float padding = 1.15f)
        {
            float aspect = Mathf.Max(.1f, rect.width / Mathf.Max(1, rect.height));
            float z = Mathf.Max(bounds.size.z, bounds.size.x / aspect) * padding;
            bounds.size = new Vector3(z * aspect, bounds.size.y, z); return bounds;
        }
        void PreparePreview(Rect rect)
        {
            // Never allocate a second scene camera or render target while the game runs.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (previewCamera == null) {
                var go = new GameObject("__PursuitDesignerCamera") { hideFlags = HideFlags.HideAndDontSave };
                previewCamera = go.AddComponent<Camera>(); previewCamera.enabled = false;
                previewCamera.orthographic = true; previewCamera.cameraType = CameraType.Preview;
                previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.065f, .075f, .09f);
                previewCamera.cullingMask = ~(1 << 5); previewCamera.allowHDR = false;
                var data = previewCamera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false;
                data.renderShadows = false; data.antialiasing = AntialiasingMode.None;
            }
            int w = Mathf.Clamp(Mathf.RoundToInt(rect.width * EditorGUIUtility.pixelsPerPoint), 64, 1600);
            int h = Mathf.Clamp(Mathf.RoundToInt(rect.height * EditorGUIUtility.pixelsPerPoint), 64, 1600);
            if (texture == null || texture.width != w || texture.height != h) {
                if (texture != null) { texture.Release(); DestroyImmediate(texture); }
                texture = new RenderTexture(w, h, 24) { name = "Pursuit room top view", hideFlags = HideFlags.HideAndDontSave };
            }
            previewCamera.transform.SetPositionAndRotation(new Vector3(viewBounds.center.x, viewFloor + 60, viewBounds.center.z), Quaternion.Euler(90, 0, 0));
            // Clip both above and below this storey. The old far plane also drew two lower floors.
            float ceiling = rooms.Select(FloorHeight).Where(y => y > viewFloor + .75f).DefaultIfEmpty(float.PositiveInfinity).Min();
            float top = Mathf.Min(viewFloor + cutHeight, ceiling - .15f);
            previewCamera.nearClipPlane = 60 - (top - viewFloor);
            previewCamera.farClipPlane = 60.35f;
            previewCamera.orthographicSize = viewBounds.size.z * .5f; previewCamera.aspect = viewBounds.size.x / viewBounds.size.z;
            previewCamera.targetTexture = texture;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                var placements = VisiblePlacements();
                foreach (var key in previews.Keys.ToArray()) if (key == null || !placements.Contains(key)) { if (previews[key] != null) DestroyImmediate(previews[key]); previews.Remove(key); }
                foreach (var item in placements) {
                    if (item == null || item.Prefab == null) continue;
                    if (!previews.TryGetValue(item, out var visual) || visual == null || visual.name != "__Preview_" + item.Prefab.name) {
                        if (visual != null) DestroyImmediate(visual);
                        visual = Instantiate(item.Prefab); visual.name = "__Preview_" + item.Prefab.name;
                        foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                        foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                        previews[item] = visual;
                    }
                    visual.transform.SetPositionAndRotation(item.transform.position, item.transform.rotation);
                }
            }
            previewCamera.Render();
        }
        void DrawTopView(Rect area)
        {
            if (area.width <= 1 || area.height <= 1) return;
            EnsureView(area);
            if (Event.current.type == EventType.Repaint) PreparePreview(area);
            if (texture != null) GUI.DrawTexture(area, texture, ScaleMode.StretchToFill, false);
            else EditorGUI.DrawRect(area, new Color(.08f, .09f, .11f));
            GUI.BeginClip(area);
            var local = new Rect(0, 0, area.width, area.height);
            Handles.BeginGUI();
            if (EditorApplication.isPlaying) {
                foreach (var actor in director.Actors) {
                    if (actor == null || !OnActiveFloor(actor.transform.position)) continue;
                    var p = Project(actor.transform.position, viewBounds, local);
                    if (!local.Contains(p)) continue;
                    DrawActor(p, actor.transform.eulerAngles.y, actor.State == PursuitMotion.Run ? new Color(1, .2f, .15f) : actor.State == PursuitMotion.Walk ? Color.cyan : Color.gray, false);
                    GUI.Label(new Rect(p.x + 10, p.y - 20, 190, 20), actor.name.Split('_')[1] + " " + actor.Behaviour + (actor.IsRendered ? "" : " · 렌더 숨김"), EditorStyles.whiteMiniLabel);
                }
            } else {
                if (selected != null) DrawRouteAndSight(local);
                foreach (var item in VisiblePlacements()) {
                    if (item == null) continue;
                    var p = Project(item.transform.position, viewBounds, local);
                    DrawActor(p, item.transform.eulerAngles.y, item == selected ? new Color(1, .32f, .16f)
                        : HasTargetRoom && Room.Placements.Contains(item) ? new Color(.2f, .85f, .92f) : Color.gray, item == selected);
                    string label = item.Prefab != null ? item.Prefab.name.Replace("Pursuit_", "") : "타입 없음";
                    GUI.Label(new Rect(p.x + 12, p.y - 16, 130, 20), label, EditorStyles.whiteMiniLabel);
                }
            }
            Handles.color = HasTargetRoom ? CurrentRoomColor : Color.gray;
            Handles.DrawAAPolyLine(2, local.center + Vector2.left * 7, local.center + Vector2.right * 7);
            Handles.DrawAAPolyLine(2, local.center + Vector2.up * 7, local.center + Vector2.down * 7);
            Handles.EndGUI();
            GUI.Label(new Rect(12, 8, 340, 24), $"N ↑   {viewFloor:0}m · {zoom:0.0}× · " + (selected != null ? "선택 마네킹 기준" : HasTargetRoom ? "＋ 중심 기준" : "중심: 방 외부"), EditorStyles.whiteBoldLabel);
            HandleMapInput(local);
            GUI.EndClip();
        }
        void DrawActor(Vector2 p, float yaw, Color color, bool active)
        {
            Handles.color = color; Handles.DrawSolidDisc(p, Vector3.forward, active ? 9 : 6);
            var forward = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), -Mathf.Cos(yaw * Mathf.Deg2Rad));
            Handles.DrawAAPolyLine(3, p, p + forward * 24);
            var tip = p + forward * 24; var side = new Vector2(-forward.y, forward.x) * 4;
            Handles.DrawAAConvexPolygon(tip, tip - forward * 8 + side, tip - forward * 8 - side);
        }
        void DrawRouteAndSight(Rect rect)
        {
            var rules = GameSettings.WaitingRoom.Pursuit;
            if (rules.Stages != null && rules.Stages.Length > 0) {
                var stage = rules.Stages[Mathf.Clamp(stageIndex, 0, rules.Stages.Length - 1)];
                if (stage != null && !stage.AlwaysNearest) {
                    var center = Project(selected.transform.position, viewBounds, rect);
                    var fan = new Vector3[34]; fan[0] = center;
                    for (int i = 0; i <= 32; i++) {
                        float angle = (selected.transform.eulerAngles.y - stage.FieldOfView * .5f + stage.FieldOfView * i / 32) * Mathf.Deg2Rad;
                        fan[i + 1] = Project(selected.transform.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * stage.SightRadius, viewBounds, rect);
                    }
                    Handles.color = new Color(1, .65f, .1f, .10f); Handles.DrawAAConvexPolygon(fan);
                    Handles.color = new Color(1, .65f, .1f, .8f); Handles.DrawAAPolyLine(1, fan);
                }
            }
            var points = selected.PatrolPoints;
            if (points == null) return;
            for (int i = 0; i < points.Length; i++) {
                if (points[i] == null || !OnActiveFloor(points[i].position)) continue;
                var p = Project(points[i].position, viewBounds, rect);
                var next = points[(i + 1) % points.Length];
                if (next != null && OnActiveFloor(next.position) && points.Length > 1) {
                    var q = Project(next.position, viewBounds, rect);
                    Handles.color = new Color(.15f, .9f, 1, .85f); Handles.DrawAAPolyLine(2, p, q);
                    var dir = (q - p).normalized; var mid = Vector2.Lerp(p, q, .6f); var side = new Vector2(-dir.y, dir.x) * 4;
                    Handles.DrawAAConvexPolygon(mid + dir * 6, mid - dir * 5 + side, mid - dir * 5 - side);
                }
                Handles.color = pointIndex == i ? Color.yellow : new Color(.1f, .6f, .75f);
                Handles.DrawSolidDisc(p, Vector3.forward, pointIndex == i ? 13 : 10);
                GUI.Label(new Rect(p.x - 12, p.y - 10, 24, 20), (i + 1).ToString(), badgeStyle);
            }
        }
        void HandleMapInput(Rect rect)
        {
            var e = Event.current;
            if (HandlePan(rect)) return;
            if (rect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel) { zoom = Mathf.Clamp(zoom * (e.delta.y > 0 ? .9f : 1.1f), .5f, 4); e.Use(); }
            if (EditorApplication.isPlaying) return;
            if (rect.Contains(e.mousePosition) && (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)) {
                var prefab = DragAndDrop.objectReferences.OfType<GameObject>().FirstOrDefault(p => director.Mannequins.Contains(p));
                var dropPosition = Unproject(e.mousePosition, rect);
                var dropRoom = RoomAt(dropPosition);
                bool inside = dropRoom != null;
                DragAndDrop.visualMode = prefab != null && inside ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                if (e.type == EventType.DragPerform && prefab != null && inside) {
                    DragAndDrop.AcceptDrag();
                    SelectPlacement(PursuitAuthoring.Add(dropRoom, prefab, dropPosition)); pointIndex = -1;
                    if (selected == null) ShowNotification(new GUIContent("이 위치에는 바닥이 없습니다. 방 내부 바닥 위에 놓아 주세요."));
                    e.Use();
                }
                return;
            }
            if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == 0) {
                if (e.keyCode == KeyCode.Escape) { addPoint = false; pointIndex = -1; e.Use(); }
                if (e.keyCode == KeyCode.Q) { Rotate(-15); e.Use(); }
                if (e.keyCode == KeyCode.E) { Rotate(15); e.Use(); }
                if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) { RemoveSelection(); e.Use(); }
            }
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition)) {
                GUIUtility.keyboardControl = 0;
                if (HasTargetRoom && addPoint && selected != null) {
                    if (PursuitAuthoring.Ground(Room, Unproject(e.mousePosition, rect), out var ground)) {
                        PursuitAuthoring.AddPoint(selected, ground); pointIndex = selected.PatrolPoints.Length - 1;
                    } else ShowNotification(new GUIContent("바닥 위에 감시 지점을 추가해 주세요."));
                    e.Use(); return;
                }
                if (e.clickCount == 2) {
                    var hit = VisiblePlacements()
                        .OrderBy(p => Vector2.Distance(e.mousePosition, Project(p.transform.position, viewBounds, rect)))
                        .FirstOrDefault(p => Vector2.Distance(e.mousePosition, Project(p.transform.position, viewBounds, rect)) < 18);
                    if (hit != null) {
                        SelectPlacement(hit); pointIndex = -1; dragging = false; dragUndo = -1;
                        if (!previews.ContainsKey(hit)) PreparePreview(rect);
                        FrameInScene(hit.gameObject, previews.TryGetValue(hit, out var model) ? model : null);
                        e.Use(); return;
                    }
                }
                pointIndex = -1;
                if (selected != null) for (int i = 0; i < selected.PatrolPoints.Length; i++)
                    if (selected.PatrolPoints[i] != null && OnActiveFloor(selected.PatrolPoints[i].position) && Vector2.Distance(e.mousePosition, Project(selected.PatrolPoints[i].position, viewBounds, rect)) < 14) { pointIndex = i; break; }
                if (pointIndex < 0) SelectPlacement(VisiblePlacements()
                    .OrderBy(p => Vector2.Distance(e.mousePosition, Project(p.transform.position, viewBounds, rect)))
                    .FirstOrDefault(p => Vector2.Distance(e.mousePosition, Project(p.transform.position, viewBounds, rect)) < 18));
                dragging = selected != null;
                if (dragging) { Undo.IncrementCurrentGroup(); dragUndo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("탑뷰 드래그 이동"); }
                else BeginPan(panInputId);
                e.Use();
            }
            if (e.type == EventType.MouseDrag && e.button == 0 && dragging && selected != null) {
                if (rect.Contains(e.mousePosition) && PursuitAuthoring.Ground(Room, Unproject(e.mousePosition, rect), out var ground)) {
                    if (pointIndex >= 0) { var point = selected.PatrolPoints[pointIndex]; Undo.RecordObject(point, "감시 지점 이동"); point.position = ground; PursuitAuthoring.Dirty(point); }
                    else PursuitAuthoring.Move(selected, ground, selected.transform.rotation);
                }
                e.Use();
            }
            if (e.type == EventType.MouseUp && dragging) { dragging = false; if (dragUndo >= 0) Undo.CollapseUndoOperations(dragUndo); dragUndo = -1; e.Use(); }
        }
    }
}
