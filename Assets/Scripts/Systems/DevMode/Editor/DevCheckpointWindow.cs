using System;
using System.Linq;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Dev.EditorTools
{
    public sealed partial class DevCheckpointWindow : EditorWindow
    {
        [MenuItem("Tools/CurtainCall/Dev/체크포인트 편집기")]
        static void Open() => GetWindow<DevCheckpointWindow>("Dev 체크포인트");

        DevCheckpointRoute route;
        SerializedObject data;
        ReorderableList list;
        Vector2 scroll;

        bool placing;

        void OnEnable()
        {
            SceneView.duringSceneGui += DrawScene;
            Undo.undoRedoPerformed += OnUndo;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScene;
            Undo.undoRedoPerformed -= OnUndo;
        }

        void OnUndo()
        {
            Repaint();
            SceneView.RepaintAll();
        }

        void OnGUI()
        {
            var scene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField(scene.name, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("씬뷰를 클릭해 배치하고, 목록을 끌어 순서를 바꿉니다. 행을 더블 클릭하면 씬뷰에서 위치·방향·인정 범위를 조정할 수 있습니다.", MessageType.Info);
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded || string.IsNullOrEmpty(scene.path)) return;

            var found = DevCheckpointRoute.In(scene);
            if (found != route || data == null) Bind(found);
            if (route == null)
            {
                if (GUILayout.Button("이 씬에 체크포인트 목록 만들기")) Bind(CreateRoute(scene));
                return;
            }
            if (route.Points.Count == 0 && GUILayout.Button("코스 기본 지점 불러오기"))
            {
                if (SeedRoute(scene) == 0) ShowNotification(new GUIContent("자동으로 가져올 코스 지점이 없습니다."));
                data.Update();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(placing ? "씬뷰 배치 취소" : "씬뷰 클릭으로 배치")) TogglePlacement(!placing);
                if (GUILayout.Button("선택 / 씬뷰 중심에 추가")) AddAtSceneView();
            }
            if (placing) EditorGUILayout.HelpBox("씬뷰의 바닥을 왼쪽 클릭해 배치합니다. Esc로 취소할 수 있습니다.", MessageType.Info);

            data.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            list.DoLayoutList();
            EditorGUILayout.EndScrollView();
            if (list.index >= 0 && list.index < list.serializedProperty.arraySize) DrawDetails(list.index);
            if (data.ApplyModifiedProperties()) SceneView.RepaintAll();
            if (GUILayout.Button("씬 저장")) EditorSceneManager.SaveScene(scene);
        }

        void Bind(DevCheckpointRoute found)
        {
            placing = false;
            route = found;
            data = found == null ? null : new SerializedObject(found);
            if (data == null) { list = null; return; }
            list = new ReorderableList(data, data.FindProperty("points"), true, true, true, true)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 6,
                drawHeaderCallback = r => EditorGUI.LabelField(r, "F9 순서 · 더블 클릭으로 위치 보기"),
                onAddCallback = _ => TogglePlacement(true),
                onSelectCallback = _ => SceneView.RepaintAll(),
                onChangedCallback = _ => SceneView.RepaintAll()
            };
            list.drawElementCallback = (r, i, active, focused) =>
            {
                var element = list.serializedProperty.GetArrayElementAtIndex(i);
                string name = element.FindPropertyRelative("label").stringValue;
                bool only = element.FindPropertyRelative("teleportOnly").boolValue;
                EditorGUI.LabelField(r, $"{i + 1:00}. {name}{(only ? " (이동 전용)" : "")}");
                if (IsFrameClick(Event.current, r))
                {
                    list.index = i;
                    FramePoint(i);
                    Event.current.Use();
                }
            };
        }

        static bool IsFrameClick(Event e, Rect row) =>
            e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && row.Contains(e.mousePosition);

        void DrawDetails(int index)
        {
            var item = list.serializedProperty.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{index + 1:00}. 체크포인트 설정", EditorStyles.boldLabel);
                if (GUILayout.Button("씬뷰에서 보기", GUILayout.Width(116))) FramePoint(index);
            }
            EditorGUILayout.PropertyField(item.FindPropertyRelative("label"), new GUIContent("이름"));
            var pos = item.FindPropertyRelative("position");
            EditorGUI.BeginChangeCheck();
            var world = EditorGUILayout.Vector3Field("월드 위치 (m)", route.transform.TransformPoint(pos.vector3Value));
            if (EditorGUI.EndChangeCheck()) pos.vector3Value = route.transform.InverseTransformPoint(world);
            EditorGUILayout.PropertyField(item.FindPropertyRelative("yaw"), new GUIContent("방향 (로컬 Y)"));
            var only = item.FindPropertyRelative("teleportOnly");
            EditorGUILayout.PropertyField(only, new GUIContent("순간이동 전용"));
            using (new EditorGUI.DisabledScope(only.boolValue))
            {
                var radius = item.FindPropertyRelative("radius");
                EditorGUI.BeginChangeCheck();
                float value = EditorGUILayout.FloatField(new GUIContent("인정 반경 (m)", "플레이어의 발이 이 구 안에 들어오면 통과로 기록합니다."), radius.floatValue);
                if (EditorGUI.EndChangeCheck()) radius.floatValue = Mathf.Max(.1f, value);
            }
            if (route.GetComponentInParent<EscapeCampaignContent>() != null)
                EditorGUILayout.PropertyField(item.FindPropertyRelative("requiresOpenEscapeExit"), new GUIContent("출구 개방 필요"));
            var spawn = item.FindPropertyRelative("sceneSpawn");
            var ropeMode = item.FindPropertyRelative("ropePlacement");
            if (spawn.objectReferenceValue != null || ropeMode.enumValueIndex != 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("원래 자리 / 줄 선택 연동", EditorStyles.miniLabel);
                    if (GUILayout.Button("고정 좌표로 변경", GUILayout.Width(130)))
                    {
                        spawn.objectReferenceValue = null;
                        ropeMode.enumValueIndex = 0;
                        item.FindPropertyRelative("rope").objectReferenceValue = null;
                    }
                }
            }
        }

        void TogglePlacement(bool value)
        {
            placing = value;
            if (value) OpenSceneView().Focus();
            SceneView.RepaintAll();
        }

        static SceneView OpenSceneView() => SceneView.lastActiveSceneView ?? GetWindow<SceneView>();

        void FramePoint(int index)
        {
            if (route == null || index < 0 || index >= route.Points.Count) return;
            var point = route.Points[index];
            if (point == null) return;
            placing = false;
            var view = OpenSceneView();
            float size = point.teleportOnly ? 3f : Mathf.Clamp(point.radius * 2.6f, 3f, 25f);
            view.Frame(new Bounds(route.PoseFor(point).position, Vector3.one * size), true);
            view.Focus();
            SceneView.RepaintAll();
        }

        void AddAtSceneView()
        {
            var view = SceneView.lastActiveSceneView;
            var picked = Selection.activeTransform;
            Vector3 position = picked != null && picked.gameObject.scene == route.gameObject.scene
                ? picked.position : view != null ? view.pivot : route.transform.position;
            if (TryFloor(route.gameObject.scene, position + Vector3.up * 4, 12, out var floor)) position = floor;
            AddAt(position, view);
        }

        void AddAt(Vector3 position, SceneView view)
        {
            var forward = Vector3.ProjectOnPlane(view != null ? view.rotation * Vector3.forward : route.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = route.transform.forward;
            data.Update();
            list.index = Append(data.FindProperty("points"), route, $"체크포인트 {route.Points.Count + 1}", position, forward);
            data.ApplyModifiedProperties();
            placing = false;
            Repaint();
            SceneView.RepaintAll();
        }

        void DrawScene(SceneView scene)
        {
            if (route == null || list == null || EditorApplication.isPlayingOrWillChangePlaymode
                || route.gameObject.scene != SceneManager.GetActiveScene()) return;
            var e = Event.current;
            if (placing)
            {
                if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    TogglePlacement(false);
                    Repaint();
                    e.Use();
                }
                if (!e.alt && e.type == EventType.MouseDown && e.button == 0)
                {
                    var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    foreach (var hit in Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                    {
                        if (hit.collider.gameObject.scene != route.gameObject.scene) continue;
                        AddAt(hit.point, scene);
                        break;
                    }
                    if (placing) ShowNotification(new GUIContent("충돌체가 있는 씬 바닥을 클릭하거나 중심에 추가하세요."));
                    e.Use();
                }
            }

            for (int i = 0; i < route.Points.Count; i++)
            {
                var point = route.Points[i];
                if (point == null) continue;
                var pose = route.PoseFor(point);
                bool selected = i == list.index;
                using (new Handles.DrawingScope(selected ? Color.yellow : Color.cyan))
                {
                    Handles.Label(pose.position + Vector3.up * .3f, $"{i + 1}. {point.label}");
                    Handles.ArrowHandleCap(0, pose.position, pose.rotation, HandleUtility.GetHandleSize(pose.position) * .65f, EventType.Repaint);
                    if (!selected || placing) continue;
                    EditorGUI.BeginChangeCheck();
                    var position = Handles.PositionHandle(pose.position, Quaternion.identity);
                    float angle = Handles.Disc(pose.rotation, position, Vector3.up, HandleUtility.GetHandleSize(position), false, 15f).eulerAngles.y;
                    float radius = point.radius;
                    if (!point.teleportOnly) radius = Handles.RadiusHandle(Quaternion.identity, position, Mathf.Max(.1f, radius));
                    if (!EditorGUI.EndChangeCheck()) continue;
                    data.Update();
                    var item = data.FindProperty("points").GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("position").vector3Value = route.transform.InverseTransformPoint(position);
                    var direction = route.transform.InverseTransformDirection(Quaternion.Euler(0, angle, 0) * Vector3.forward);
                    item.FindPropertyRelative("yaw").floatValue = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    item.FindPropertyRelative("radius").floatValue = Mathf.Max(.1f, radius);
                    data.ApplyModifiedProperties();
                    Repaint();
                }
            }
        }

        public static DevCheckpointRoute CreateRoute(Scene scene)
        {
            var root = new GameObject("Dev Checkpoints");
            SceneManager.MoveGameObjectToScene(root, scene);
            var frame = scene.GetRootGameObjects().FirstOrDefault(o => o.GetComponent<EscapeCampaignContent>() != null);
            if (frame != null) root.transform.SetParent(frame.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Create dev checkpoint route");
            return Undo.AddComponent<DevCheckpointRoute>(root);
        }

        public static int SeedRoute(Scene scene) => PopulateDefaults(scene, false);

        // One-time migration for the shipped scenes. Never re-sort a designer's custom list in the window.
        public static int MigrateLegacyPoints(Scene scene) => PopulateDefaults(scene, true);

        static int PopulateDefaults(Scene scene, bool migrate)
        {
            var route = DevCheckpointRoute.In(scene) ?? CreateRoute(scene);
            if (WaitingRoomScenes.IsWaitingRoom(scene))
                return route.Points.Count > 0 ? route.Points.Count : SeedWaitingRoom(scene, route);
            if (route.Points.Count > 0 && !migrate) return route.Points.Count;
            var serialized = new SerializedObject(route);
            var list = serialized.FindProperty("points");
            var roots = scene.GetRootGameObjects();
            T Find<T>() where T : Component => roots.Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);

            int Add(string label, Vector3 position, Vector3 forward, bool only = false, bool exit = false, float radius = 2f)
            {
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue == label)
                        return i;
                return Append(list, route, label, position, forward, only, exit, radius);
            }

            var spawn = Find<PlayerSpawnPoints>();
            if (spawn != null && spawn.TryGetSlotPose(0, out var spawnPosition, out var spawnRotation))
            {
                int i = Add("씬 출발점", spawnPosition, spawnRotation * Vector3.forward, radius: .65f);
                list.GetArrayElementAtIndex(i).FindPropertyRelative("sceneSpawn").objectReferenceValue = spawn;
            }

            var course = Find<TightropeCourse>();
            var ropeOrder = new System.Collections.Generic.List<string> { "씬 출발점" };
            if (course != null && course.LaneCount > 0)
            {
                void Rope(string label, Vector3 p, DevCheckpointRoute.RopePlacement placement, bool only = false)
                {
                    int index = Add(label, p, course.Forward, only, radius: placement == DevCheckpointRoute.RopePlacement.Start ? .65f : 1f);
                    var row = list.GetArrayElementAtIndex(index);
                    row.FindPropertyRelative("rope").objectReferenceValue = course;
                    row.FindPropertyRelative("ropePlacement").enumValueIndex = (int)placement;
                    if (migrate && placement == DevCheckpointRoute.RopePlacement.Start) row.FindPropertyRelative("radius").floatValue = .65f;
                    ropeOrder.Add(label);
                }

                var start = course.GetStartPose(0);
                Rope("외줄 · 출발", start.position, DevCheckpointRoute.RopePlacement.Start);
                float from = 0;
                var sections = course.Shape.Sections ?? Array.Empty<CurtainCall.Settings.TightropeSettings.CourseSection>();
                for (int i = 0; i < sections.Length; i++)
                {
                    float end = Mathf.Clamp(sections[i].endFraction * course.FinishDistance, from, course.FinishDistance);
                    float entry = Mathf.Min(from + .5f, course.FinishDistance - .5f);
                    string section = string.IsNullOrWhiteSpace(sections[i].label) ? $"{i + 1}구간" : sections[i].label;
                    // Keep the old exact entry where a lane exists. A gap can start the section.
                    bool added = false;
                    for (float at = entry; !added && at < end; at += .5f)
                        for (int lane = 0; lane < course.LaneCount; lane++)
                            if (course.IsLaneUsable(lane, at))
                            {
                                Rope($"외줄 · {section} 시작", course.GetLanePosition(lane, at), DevCheckpointRoute.RopePlacement.Section);
                                added = true;
                                break;
                            }
                    from = end;
                }
                var finish = course.GetFinishPose(0);
                Rope("외줄 · 도착", finish.position, DevCheckpointRoute.RopePlacement.Finish, true);
            }

            if (Find<HumanCannonSystem>() is { } cannon)
                for (int i = 0; i < cannon.StationCount; i++)
                    if (cannon.Station(i) is { } station)
                        Add($"대포 {i + 1} 탑승 위치", station.BoardingPoint.position, station.BoardingPoint.forward);
            var zone = Find<HumanCannonDestination>()?.Zone;
            if (zone != null)
            {
                var floor = zone.GetComponentInChildren<Collider>();
                var top = floor != null ? new Vector3(floor.bounds.center.x, floor.bounds.max.y, floor.bounds.center.z) : zone.transform.position;
                Add("대포 · 최종 목적지", top, zone.transform.forward, true);
            }

            if (Find<EscapeCampaignContent>() is { } content)
            {
                var frame = content.transform;
                var forward = frame.TransformDirection(Vector3.right);
                var anchors = frame.GetComponentsInChildren<Transform>(true);
                void Anchor(string name, string label, float offset = 0, bool only = false, bool exit = false)
                {
                    var node = anchors.FirstOrDefault(t => t.name == name);
                    if (node != null) Add("탈출로 · " + label, node.position + frame.TransformVector(Vector3.right * offset), forward, only, exit);
                }

                var chase = Find<EscapePursuerRoute>();
                if (chase != null && !migrate)
                {
                    Physics.SyncTransforms();
                    int samples = Mathf.Min(8, chase.Points.Count);
                    for (int sample = 0; sample < samples; sample++)
                    {
                        int i = samples == 1 ? 0 : Mathf.RoundToInt(sample * (chase.Points.Count - 1f) / (samples - 1));
                        var point = chase.Points[i];
                        if (point?.playerPath == null || string.IsNullOrWhiteSpace(point.label)) continue;
                        if (!TryFloor(scene, point.playerPath.position + Vector3.up * 1.25f, 2.5f, out var floor)) continue;
                        Vector3 next = i + 1 < chase.Points.Count && chase.Points[i + 1].playerPath != null
                            ? chase.Points[i + 1].playerPath.position - floor : forward;
                        Add("탈출로 · 추격 / " + point.label, floor, next, true);
                    }
                }

                Anchor("Anchor_RunStart", "후반 질주 시작");
                Anchor("Anchor_SwingTakeoff", "후반 그네 앞");
                Anchor("Anchor_SwingLanding", "후반 그네 뒤");
                Anchor("Anchor_ExitToReeds", "마지막 화염 구간", -34);
                Anchor("Anchor_ExitToReeds", "출구 절단기 앞", -18);
                if (Find<EscapePursuerBoss>() is { } boss
                    && TryFloor(scene, boss.ArenaFrame.position + Vector3.up * 1.25f, 2.5f, out var arenaFloor))
                    Add("탈출로 · 추격자 최종 전투", arenaFloor, forward, true);
                Anchor("Anchor_ExitToReeds", "마지막 문 앞", -3);
                Anchor("Anchor_ExitToReeds", "갈대밭 · 엔딩 도착", 4, true, true);
                Reorder(list, item =>
                {
                    var name = item.FindPropertyRelative("label").stringValue;
                    if (name == "씬 출발점") return -2;
                    if (name == "대포 1 탑승 위치") return -1;
                    if (name.Contains("갈대밭 · 엔딩 도착")) return float.MaxValue;
                    var p = route.transform.TransformPoint(item.FindPropertyRelative("position").vector3Value);
                    return chase != null && chase.Valid ? chase.Project(p, out _) : Vector3.Dot(p - frame.position, forward);
                });
            }
            else if (course != null)
                Reorder(list, item =>
                {
                    int i = ropeOrder.IndexOf(item.FindPropertyRelative("label").stringValue);
                    return i >= 0 ? i : float.MaxValue;
                });
            else
                Reorder(list, item => item.FindPropertyRelative("sceneSpawn").objectReferenceValue != null ? -1 : 0);

            serialized.ApplyModifiedProperties();
            return route.Points.Count;
        }

        static void Reorder(SerializedProperty points, Func<SerializedProperty, float> order)
        {
            // Stable ordering for checkpoints at the same stage.
            for (int i = 1; i < points.arraySize; i++)
            {
                float value = order(points.GetArrayElementAtIndex(i));
                int at = i;
                while (at > 0 && order(points.GetArrayElementAtIndex(at - 1)) > value) at--;
                if (at != i) points.MoveArrayElement(i, at);
            }
        }

        static int Append(SerializedProperty points, DevCheckpointRoute route, string label, Vector3 position,
            Vector3 forward, bool only = false, bool exit = false, float radius = 2f)
        {
            int index = points.arraySize++;
            var point = points.GetArrayElementAtIndex(index);
            point.FindPropertyRelative("label").stringValue = label;
            point.FindPropertyRelative("position").vector3Value = route.transform.InverseTransformPoint(position);
            var local = route.transform.InverseTransformDirection(forward);
            point.FindPropertyRelative("yaw").floatValue = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            point.FindPropertyRelative("radius").floatValue = radius;
            point.FindPropertyRelative("sceneSpawn").objectReferenceValue = null;
            point.FindPropertyRelative("rope").objectReferenceValue = null;
            point.FindPropertyRelative("ropePlacement").enumValueIndex = 0;
            point.FindPropertyRelative("teleportOnly").boolValue = only;
            point.FindPropertyRelative("requiresOpenEscapeExit").boolValue = exit;
            return index;
        }

        static bool TryFloor(Scene scene, Vector3 origin, float distance, out Vector3 floor)
        {
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.normal.y > .5f && hit.collider.gameObject.scene == scene)
                { floor = hit.point; return true; }
            floor = default;
            return false;
        }
    }
}
