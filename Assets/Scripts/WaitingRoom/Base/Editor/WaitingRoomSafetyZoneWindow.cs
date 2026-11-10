using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.WaitingRoom.Pursuit;
using CurtainCall.WaitingRoom.Pursuit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>실제 탐색 타이머의 방 콜라이더를 층별 평면도에서 편집한다.</summary>
    public sealed class WaitingRoomSafetyZoneWindow : EditorWindow
    {
        ExplorationTimerZones target;
        BoxCollider[] zones = Array.Empty<BoxCollider>();
        readonly HashSet<BoxCollider> selected = new();
        readonly Dictionary<BoxCollider, float> roomFloors = new();
        float[] floors = { 0 };
        int floorIndex;
        Vector2 scroll, pan;
        float zoom = 1;
        bool panning;
        Bounds bounds;

        [MenuItem("Tools/Curtain Call/Waiting Room/Safety Zones")]
        public static WaitingRoomSafetyZoneWindow Open()
        {
            var window = GetWindow<WaitingRoomSafetyZoneWindow>("제한시간 안전지대");
            window.minSize = new Vector2(980, 650);
            window.Show();
            return window;
        }

        void OnEnable() { Undo.undoRedoPerformed += Refresh; EditorSceneManager.activeSceneChangedInEditMode += SceneChanged; Refresh(); }
        void OnDisable() { Undo.undoRedoPerformed -= Refresh; EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged; }
        void SceneChanged(Scene _, Scene __) { target = null; selected.Clear(); pan = Vector2.zero; zoom = 1; Refresh(); }
        void Refresh()
        {
            var scene = SceneManager.GetActiveScene();
            if (target == null || target.gameObject.scene != scene)
                target = scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren<ExplorationTimerZones>(true)).FirstOrDefault() : null;
            zones = target != null ? target.Zones.Where(z => z != null).ToArray() : Array.Empty<BoxCollider>();
            if (target != null) target.RebuildZones();
            selected.RemoveWhere(z => z == null || !zones.Contains(z));
            roomFloors.Clear();
            if (scene.IsValid() && scene.isLoaded)
                foreach (var room in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PursuitRoom>(true)))
                    if (room.Area != null) roomFloors[room.Area] = Mathf.Round(PursuitAuthoring.FloorReference(room));
            floors = roomFloors.Values.Distinct().OrderBy(y => y).ToArray();
            if (floors.Length == 0) floors = zones.Select(z => Mathf.Round(z.bounds.min.y)).Distinct().OrderBy(y => y).ToArray();
            // 방 콜라이더의 바닥은 층 바닥보다 약간 낮을 수 있으므로 같은 높이를 합친다.
            var merged = new List<float>();
            foreach (float y in floors) if (merged.Count == 0 || y - merged[merged.Count - 1] > 1f) merged.Add(y);
            floors = merged.Count > 0 ? merged.ToArray() : new[] { 0f };
            floorIndex = Mathf.Clamp(floorIndex, 0, floors.Length - 1);
            Repaint();
        }
        BoxCollider[] VisibleZones => zones.Where(z => OnFloor(z, floors[floorIndex])).ToArray();
        bool OnFloor(BoxCollider zone, float floor)
        {
            // 판정 상자의 천장이 위층까지 올라와도 그 방의 실제 층을 바꾸지 않는다.
            // 계단은 여러 층에서 같은 안전 설정을 편집할 수 있다.
            if (zone.name.IndexOf("Stairwell", StringComparison.OrdinalIgnoreCase) >= 0)
                return zone.bounds.min.y <= floor + .5f && zone.bounds.max.y > floor + .5f;
            float assigned = roomFloors.TryGetValue(zone, out float value) ? value
                : floors.OrderBy(y => Mathf.Abs(y - zone.bounds.min.y)).First();
            return Mathf.Approximately(assigned, floor);
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(80))) Refresh();
                int next = EditorGUILayout.Popup(floorIndex, floors.Select(y => $"{y:0.#}m 층").ToArray(), GUILayout.Width(130));
                if (next != floorIndex) { floorIndex = next; pan = Vector2.zero; zoom = 1; selected.Clear(); }
                if (GUILayout.Button("전체 보기", EditorStyles.toolbarButton, GUILayout.Width(80))) { pan = Vector2.zero; zoom = 1; }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(target == null || EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("모두 안전지대", EditorStyles.toolbarButton)) Change(zones, true, true);
                    if (GUILayout.Button("모두 안전지대 해제", EditorStyles.toolbarButton)) Change(zones, false, true);
                    if (GUILayout.Button("씬 저장", EditorStyles.toolbarButton)) EditorSceneManager.SaveScene(target.gameObject.scene);
                }
            }
            if (target == null)
            {
                EditorGUILayout.HelpBox("대기실 씬을 활성화한 뒤 새로고침하세요. ExplorationTimerZones가 있는 구역을 편집합니다.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("초록: 안전지대 · 빨강: 시간 진행. 살아 있는 전원이 안전지대에 있으면 모든 휴식·정비의 제한시간이 멈춥니다. 이미 시작한 추격은 계속됩니다.\n클릭 선택 · Ctrl/Shift로 여러 구역 선택 · 더블클릭 안전 전환 · 휠 확대 · 가운데 버튼 드래그 이동 · Ctrl+Z 실행 취소", MessageType.Info);
            if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("플레이 중에는 조회만 합니다. 안전지대 편집은 플레이 종료 후 가능합니다.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect map = GUILayoutUtility.GetRect(300, 300, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawMap(map);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(285))) DrawInspector();
            }
        }

        void DrawInspector()
        {
            EditorGUILayout.LabelField($"현재 층 {VisibleZones.Length}구역 · 선택 {selected.Count}개", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || selected.Count == 0))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택 안전지대 지정")) Change(selected.ToArray(), true);
                if (GUILayout.Button("선택 해제")) Change(selected.ToArray(), false);
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                bool outside = EditorGUILayout.Toggle("구역 밖도 안전지대", target.OutsideIsSafe);
                if (outside != target.OutsideIsSafe) { Undo.RecordObject(target, "구역 밖 안전지대"); target.OutsideIsSafe = outside; Dirty(); }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("이 층 모두 지정")) Change(VisibleZones, true);
                    if (GUILayout.Button("이 층 모두 해제")) Change(VisibleZones, false);
                }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var zone in VisibleZones.OrderBy(z => z.name, StringComparer.Ordinal))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool wasSelected = selected.Contains(zone);
                    bool chosen = GUILayout.Toggle(wasSelected, Label(zone), "Button", GUILayout.ExpandWidth(true));
                    if (chosen != wasSelected) { if (chosen) selected.Add(zone); else selected.Remove(zone); }
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    {
                        bool safe = target.IsSafeZone(zone);
                        bool value = GUILayout.Toggle(safe, "안전", GUILayout.Width(48));
                        if (value != safe) Change(new[] { zone }, value);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
        static string Label(BoxCollider zone) => zone.name.Replace("Area_", "").Replace('_', ' ');
        void Change(BoxCollider[] items, bool safe, bool outside = false)
        {
            if (target == null || EditorApplication.isPlaying) return;
            Undo.RecordObject(target, safe ? "안전지대 지정" : "안전지대 해제");
            target.SetSafety(items, safe);
            if (outside) target.OutsideIsSafe = safe;
            Dirty();
        }
        void Dirty()
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            SceneView.RepaintAll(); Repaint();
        }
        Vector2 Project(Vector3 point, Rect rect) => new(rect.center.x + pan.x + (point.x - bounds.center.x) / bounds.size.x * rect.width,
            rect.center.y + pan.y - (point.z - bounds.center.z) / bounds.size.z * rect.height);
        Rect ProjectBounds(BoxCollider zone, Rect rect)
        {
            var a = Project(zone.bounds.min, rect); var b = Project(zone.bounds.max, rect);
            return Rect.MinMaxRect(a.x, b.y, b.x, a.y);
        }
        void DrawMap(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(.08f, .1f, .12f));
            var visible = VisibleZones;
            if (visible.Length == 0) return;
            bounds = visible[0].bounds;
            foreach (var zone in visible.Skip(1)) bounds.Encapsulate(zone.bounds);
            float aspect = rect.width / Mathf.Max(1, rect.height);
            float height = Mathf.Max(5, Mathf.Max(bounds.size.z, bounds.size.x / aspect) * 1.12f / zoom);
            bounds.size = new Vector3(height * aspect, bounds.size.y, height);
            GUI.BeginClip(rect);
            var localRect = new Rect(0, 0, rect.width, rect.height);
            foreach (var zone in visible.OrderByDescending(z => z.bounds.size.x * z.bounds.size.z))
            {
                var box = ProjectBounds(zone, localRect);
                Color color = target.IsSafeZone(zone) ? new Color(.13f, .55f, .33f, .65f) : new Color(.65f, .21f, .18f, .65f);
                EditorGUI.DrawRect(box, color);
                Handles.color = selected.Contains(zone) ? Color.yellow : new Color(.7f, .8f, .85f);
                Handles.DrawAAPolyLine(selected.Contains(zone) ? 3 : 1, new Vector3(box.xMin, box.yMin), new Vector3(box.xMax, box.yMin), new Vector3(box.xMax, box.yMax), new Vector3(box.xMin, box.yMax), new Vector3(box.xMin, box.yMin));
                if (box.width > 45 && box.height > 18) GUI.Label(box, Label(zone), new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = Color.white }, wordWrap = true });
            }
            GUI.EndClip();
            var e = Event.current;
            if (e.type == EventType.MouseUp) panning = false;
            if (rect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel) { zoom = Mathf.Clamp(zoom * (e.delta.y > 0 ? .9f : 1.1f), .4f, 6); e.Use(); Repaint(); }
            if (rect.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 2) { panning = true; e.Use(); }
            if (panning && e.type == EventType.MouseDrag) { pan += e.delta; e.Use(); Repaint(); }
            if (rect.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 0)
            {
                var hit = visible.Where(z => ProjectBounds(z, rect).Contains(e.mousePosition)).OrderBy(z => z.bounds.size.x * z.bounds.size.z).FirstOrDefault();
                if (!e.control && !e.shift) selected.Clear();
                if (hit != null)
                {
                    if (!selected.Add(hit) && (e.control || e.shift)) selected.Remove(hit);
                    if (e.clickCount == 2 && !EditorApplication.isPlaying) Change(new[] { hit }, !target.IsSafeZone(hit));
                }
                e.Use(); Repaint();
            }
        }
    }
}
