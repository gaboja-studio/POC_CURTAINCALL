using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>Private circulation stays inside concrete; only the isolated scene is rebuilt.</summary>
    public static partial class EscapePursuitRemodel
    {
        public const string ScenePath = "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodel.unity";
        const string StructureName = "Concealed pursuit structure";
        static Transform frame, module, structure;
        static Material concrete;
        static readonly Vector3[] EarlyRoute =
        {
            new(18.5f,0,66.8f), new(26,0,53), new(47.4f,-6,53), new(52.5f,-6,58),
            new(52.5f,-6,52), new(52.5f,-10,45), new(52.5f,-10,41), new(45,-16,45),
            new(47,-22,10), new(47,-22,17), new(16,-22,17), new(16,-22,-18),
            new(16,-22,-40), new(16,-18.5f,-90), new(44,-18.5f,-90), new(80,-18.5f,-90),
            new(105,-18.5f,-90), new(109,-9,-43), new(100,-5,0), new(74,-5,0),
            new(74,-5,51), new(82,-5,51), new(86,-5,51), new(90,-5,51),
            new(92,-5,51), new(94,-5,51), new(96,-5,51), new(100,-5,51),
            new(102,-5,51), new(108,-6,51), new(108,-7,55), new(108,-8,60),
            new(108,-8,64), new(108,-8,72), new(108,-8,104), new(108,-8,130),
            new(114,-7,130), new(146,-.5f,130), new(146,0,108), new(146.5f,0,108),
            new(147,0,108), new(147.5f,0,108), new(148,0,108), new(148.5f,0,108),
            new(149,0,108), new(149.25f,0,108), new(149.5f,0,108), new(150,0,108)
        };

        [MenuItem("Tools/CurtainCall/탈출로/복사 씬 은폐 추격전 재구성")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("EscapeRoutePursuitRemodel 복사 씬의 편집 모드에서만 실행하세요.");
            var route = Object.FindFirstObjectByType<EscapePursuerRoute>(FindObjectsInactive.Include);
            module = route.transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            if (PrefabUtility.IsPartOfPrefabInstance(module.gameObject))
                throw new InvalidOperationException("공용 추격자 프리팹과 분리한 복사본이 필요합니다.");
            var old = module.Find(StructureName);
            if (old) throw new InvalidOperationException("원본을 새로 복사한 씬에서 재구성하세요. 기존 절단 형상을 중복 생성하지 않습니다.");
            concrete = frame.GetComponentsInChildren<MeshRenderer>(true)
                .First(r => r.name == "타설 콘크리트 벽" && !r.transform.IsChildOf(module)).sharedMaterial;
            structure = new GameObject(StructureName).transform;
            structure.SetParent(module, false);
            foreach (var session in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EscapeDevelopmentSession>(true)))
            {
                var data = new SerializedObject(session);
                data.FindProperty("useEntranceSequence").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var t in module.GetComponentsInChildren<Transform>(true).Where(t =>
                t.name.StartsWith("Supported wall route ") || t.name.StartsWith("Hidden concrete floor ") ||
                t.name.StartsWith("Pursuit platform ") || t.name.StartsWith("Pursuit landing ") ||
                t.name.StartsWith("Final pursuit platform ") || t.name.StartsWith("Breach approach ") ||
                t.name.StartsWith("Breach threshold ") || t.name.StartsWith("Pursuit area ")).ToArray())
                Object.DestroyImmediate(t.gameObject);
            var dataRoute = new SerializedObject(route);
            var points = dataRoute.FindProperty("points");
            for (int i = 0; i < route.Points.Count; i++)
            {
                Vector3 p = i < EarlyRoute.Length ? EarlyRoute[i] : frame.InverseTransformPoint(route.Points[i].feet.position);
                if (i >= 48 && i <= 70) p.z = 108;
                if (i == 71) p = new Vector3(336, 0, 78);
                route.Points[i].feet.position = frame.TransformPoint(p);
                var row = points.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("exposed").boolValue = i >= 71;
                row.FindPropertyRelative("finalSprint").boolValue = i >= 71;
                if (i > 0 && i < 48)
                    row.FindPropertyRelative("jump").boolValue = Mathf.Abs(p.y - EarlyRoute[i - 1].y) > .4f;
                if (i == 71) row.FindPropertyRelative("jump").boolValue = true;
                row.FindPropertyRelative("label").stringValue = i >= 71 ? "기존 바닥 최종 추격 " + i : "콘크리트 내부 은폐 동선 " + i;
            }
            dataRoute.ApplyModifiedPropertiesWithoutUndo();
            var lever = module.Find("Pursuer fire controls");
            if (lever) lever.position = route.Points[62].feet.position + frame.TransformDirection(Vector3.forward * 2f);
            var encounters = ConfigureEncounters(route);
            BuildEnclosures(route, encounters);
            CloseVestibuleSightlines(route, encounters);
            BuildGallerySurfaces(route);
            BuildEncounterFloors(encounters);
            RecessNorthFixture();
            CleanupFragments();
            ApplyPoleFeedback();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Transform Box(string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(structure, false);
            go.transform.SetPositionAndRotation(frame.TransformPoint(center), frame.rotation);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = concrete;
            return go.transform;
        }

        static Transform Anchor(string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(structure, false); t.position = frame.TransformPoint(local); return t;
        }

        static void RecessNorthFixture()
        {
            // Move the complete fixture and its mount off the widened seventh breach.
            string[] names = { "매입 정비등", "작업등 함체", "작업등 광원", "현재 구조물에 연결된 작업등 지지" };
            foreach (var t in frame.GetComponentsInChildren<Transform>(true))
                if (names.Contains(t.name) && (frame.InverseTransformPoint(t.position) - new Vector3(116, -2, 113)).sqrMagnitude < 1)
                    t.position += frame.TransformDirection(new Vector3(.7f, 0, 4));
        }

        static void CleanupFragments()
        {
            var ropes = new System.Collections.Generic.HashSet<Collider>();
            foreach (var course in Object.FindObjectsByType<CurtainCall.Tightrope.TightropeCourse>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var rows = new SerializedObject(course).FindProperty("sceneRopes");
                for (int i = 0; i < rows.arraySize; i++)
                    if (rows.GetArrayElementAtIndex(i).objectReferenceValue is Collider rope) ropes.Add(rope);
            }
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var c in module.GetComponentsInChildren<BoxCollider>().Concat(structure.GetComponentsInChildren<BoxCollider>())
                .Distinct().Where(c => c.name == "Concrete remainder").ToArray())
            {
                var b = c.bounds; var size = b.size;
                bool shelf = ropes.Any(rope =>
                {
                    var r = rope.bounds;
                    if (b.max.y > r.max.y + .5f || b.max.y < r.min.y - 30) return false;
                    r.Expand(new Vector3(6, 0, 6));
                    return b.min.x < r.max.x && b.max.x > r.min.x && b.min.z < r.max.z && b.max.z > r.min.z;
                });
                string key = b.center.ToString("F3") + size.ToString("F3");
                if (shelf || Mathf.Min(size.x, size.y, size.z) < .06f || !seen.Add(key)) Object.DestroyImmediate(c.gameObject);
            }
        }
    }
}
