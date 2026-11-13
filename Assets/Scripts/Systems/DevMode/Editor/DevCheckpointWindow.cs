using System;
using System.Linq;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Dev.EditorTools
{
    public sealed class DevCheckpointWindow : EditorWindow
    {
        [MenuItem("Tools/CurtainCall/Dev/체크포인트 편집기")]
        static void Open() => GetWindow<DevCheckpointWindow>("Dev 체크포인트");

        DevCheckpointRoute route;
        SerializedObject data;
        ReorderableList list;
        Vector2 scroll;

        void OnEnable() => SceneView.duringSceneGui += DrawScene;
        void OnDisable() => SceneView.duringSceneGui -= DrawScene;

        void OnGUI()
        {
            var scene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField(scene.name, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("행을 끌어 순서를 변경합니다. 행을 고른 뒤 Scene 뷰에서 위치와 방향을 편집하세요. 체크포인트는 씬 저장에 포함됩니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded || string.IsNullOrEmpty(scene.path)))
            {
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
                data.Update();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                list.DoLayoutList();
                EditorGUILayout.EndScrollView();
                data.ApplyModifiedProperties();
                if (GUILayout.Button("씬 저장")) EditorSceneManager.SaveScene(scene);
            }
        }

        void Bind(DevCheckpointRoute found)
        {
            route = found;
            data = found == null ? null : new SerializedObject(found);
            if (data == null) { list = null; return; }
            list = new ReorderableList(data, data.FindProperty("points"), true, true, true, true)
            {
                elementHeight = EditorGUIUtility.singleLineHeight * 4 + 14,
                drawHeaderCallback = r => EditorGUI.LabelField(r, "F9 목록 순서 · 마지막 팀 부활 가능 행을 사용"),
                onAddCallback = _ => AddAtSceneView()
            };
            list.drawElementCallback = (r, i, active, focused) =>
            {
                var element = list.serializedProperty.GetArrayElementAtIndex(i);
                r.y += 3; r.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(r, element.FindPropertyRelative("label"), new GUIContent($"{i + 1}. 이름"));
                r.y += r.height + 2;
                EditorGUI.PropertyField(r, element.FindPropertyRelative("position"), new GUIContent("로컬 위치"));
                r.y += r.height + 2;
                EditorGUI.PropertyField(r, element.FindPropertyRelative("yaw"), new GUIContent("방향 (Y)"));
                r.y += r.height + 2;
                var left = new Rect(r.x, r.y, r.width * .5f, r.height);
                EditorGUI.PropertyField(left, element.FindPropertyRelative("teleportOnly"), new GUIContent("순간 이동 전용"));
                EditorGUI.PropertyField(new Rect(left.xMax, r.y, r.width - left.width, r.height),
                    element.FindPropertyRelative("requiresOpenEscapeExit"), new GUIContent("출구 개방 필요"));
            };
            list.onSelectCallback = _ => SceneView.RepaintAll();
            list.onChangedCallback = _ => SceneView.RepaintAll();
        }

        void AddAtSceneView()
        {
            var view = SceneView.lastActiveSceneView;
            var picked = Selection.activeTransform;
            Vector3 position = picked != null && picked.gameObject.scene == route.gameObject.scene
                ? picked.position : view != null ? view.pivot : route.transform.position;
            Vector3 forward = view != null ? view.rotation * Vector3.forward : route.transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = route.transform.forward;
            if (TryFloor(route.gameObject.scene, position + Vector3.up * 4, 12, out var floor)) position = floor;
            data.Update();
            list.index = Append(data.FindProperty("points"), route, $"체크포인트 {route.Points.Count + 1}", position, forward);
            data.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

        void DrawScene(SceneView scene)
        {
            if (route == null || list == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < route.Points.Count; i++)
            {
                var point = route.Points[i];
                if (point == null) continue;
                var pose = route.PoseFor(point);
                Handles.color = i == list.index ? Color.yellow : Color.cyan;
                Handles.Label(pose.position + Vector3.up * .3f, $"{i + 1}. {point.label}");
                Handles.ArrowHandleCap(0, pose.position, pose.rotation, HandleUtility.GetHandleSize(pose.position) * .65f, EventType.Repaint);
                if (i != list.index) continue;
                EditorGUI.BeginChangeCheck();
                var position = Handles.PositionHandle(pose.position, Quaternion.identity);
                var rotation = Handles.RotationHandle(pose.rotation, position);
                if (!EditorGUI.EndChangeCheck()) continue;
                data.Update();
                var item = data.FindProperty("points").GetArrayElementAtIndex(i);
                item.FindPropertyRelative("position").vector3Value = route.transform.InverseTransformPoint(position);
                item.FindPropertyRelative("yaw").floatValue = (Quaternion.Inverse(route.transform.rotation) * rotation).eulerAngles.y;
                data.ApplyModifiedProperties();
                Repaint();
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

        public static int SeedRoute(Scene scene)
        {
            var route = DevCheckpointRoute.In(scene) ?? CreateRoute(scene);
            if (route.Points.Count > 0) return route.Points.Count;
            var serialized = new SerializedObject(route);
            var list = serialized.FindProperty("points");
            var roots = scene.GetRootGameObjects();
            T Find<T>() where T : Component => roots.Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);
            void Add(string label, Vector3 position, Vector3 forward, bool teleportOnly = false, bool exit = false)
                => Append(list, route, label, position, forward, teleportOnly, exit);
            var content = Find<EscapeCampaignContent>();
            if (content != null)
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
                if (chase != null)
                {
                    Physics.SyncTransforms();
                    // These are player-side anchors from the updated chase, not the pursuer's own feet.
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
            }
            else if (Find<TightropeCourse>() is { } course)
            {
                var start = course.GetStartPose(0);
                Add("외줄 · 출발", start.position, course.Forward);
                float distance = 0;
                foreach (var section in course.Shape.Sections ?? Array.Empty<CurtainCall.Settings.TightropeSettings.CourseSection>())
                {
                    float entry = Mathf.Min(distance + .5f, course.FinishDistance - .5f);
                    for (int lane = 0; lane < course.LaneCount; lane++)
                        if (course.IsLaneUsable(lane, entry))
                        { Add($"외줄 · {section.label} 시작", course.GetLanePosition(lane, entry), course.Forward); break; }
                    distance = Mathf.Clamp(section.endFraction * course.FinishDistance, distance, course.FinishDistance);
                }
                var finish = course.GetFinishPose(0);
                Add("외줄 · 도착", finish.position, course.Forward, true);
            }
            else if (Find<HumanCannonSystem>() is { } cannon)
            {
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
            }
            serialized.ApplyModifiedProperties();
            return route.Points.Count;
        }

        static int Append(SerializedProperty points, DevCheckpointRoute route, string label, Vector3 position,
            Vector3 forward, bool only = false, bool exit = false)
        {
            int index = points.arraySize++;
            var point = points.GetArrayElementAtIndex(index);
            point.FindPropertyRelative("label").stringValue = label;
            point.FindPropertyRelative("position").vector3Value = route.transform.InverseTransformPoint(position);
            var local = route.transform.InverseTransformDirection(forward);
            point.FindPropertyRelative("yaw").floatValue = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
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
