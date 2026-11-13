using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapePursuerConcreteRoutes
    {
        const string RootName = "Concrete wall routes";
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        static Transform frame, module, root;
        static Material concrete;
        static readonly List<EscapePursuerWalls.Gate> gates = new();
        static bool physicalClearance;

        [MenuItem("Tools/CurtainCall/탈출로/추격자 콘크리트 벽 뒤 경로 적용")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != EscapePursuerLayoutReview.SourceScene)
                throw new InvalidOperationException("운영 탈출로 A를 열어 주세요.");
            frame = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var route = frame.GetComponentInChildren<EscapePursuerRoute>(true);
            module = route.transform;
            if (module.Find(RootName)) throw new InvalidOperationException("이미 적용된 경로입니다. 검사 후 필요한 부분만 수정하세요.");
            concrete = frame.GetComponentsInChildren<MeshRenderer>(true)
                .First(r => r.name == "타설 콘크리트 벽" && !r.transform.IsChildOf(module)).sharedMaterial;
            root = new GameObject(RootName).transform;
            root.SetParent(module, false);
            gates.Clear();
            foreach (string name in new[] { "Service galleries", "Concealed maintenance route", "A03 Service Bypass",
                "Core pursuit inspection openings", "Cooperative service doorway" })
            {
                var old = module.Find(name);
                if (old) { old.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject); }
            }

            var positions = route.Points.Select(p => frame.InverseTransformPoint(p.feet.position)).ToArray();
            Vector3[] early = {
                new(18.5f,0,66.8f), new(26,0,53), new(47.4f,-6,53), new(52.5f,-6,58),
                new(52.5f,-7.1f,54), new(52.5f,-10,45), new(52.5f,-10,41), new(52.5f,-16,32),
                new(52.5f,-22,10), new(52.5f,-22,17), new(16,-22,17), new(16,-22,-18),
                new(16,-22,-40), new(16,-18.5f,-86), new(44,-18.5f,-86), new(80,-18.5f,-86),
                new(150,-18.5f,-86), new(150,-22,130), new(108,-3,130), new(108,-3,51),
                new(78,-4.5f,51), new(82,-4.5f,51), new(86,-5,51), new(90,-3.5f,51),
                new(92,-2.75f,51), new(94,-2.75f,51), new(96,-2.75f,51), new(100,-2.75f,51),
                new(102,-4,51), new(108,-6,51), new(108,-7,55), new(108,-8,60),
                new(108,-8,64), new(108,-8,72), new(108,-8,104), new(108,-8,130),
                new(114,-7,130), new(146,-.5f,130), new(146,0,108), new(146.5f,0,108),
                new(147,0,108), new(147.5f,0,108), new(148,0,108), new(148.5f,0,108),
                new(149,0,108), new(149.25f,0,108), new(149.5f,0,108), new(150,0,108)
            };
            Array.Copy(early, positions, early.Length);
            for (int i = 48; i <= 69; i++) positions[i] = new Vector3(positions[i].x, 0, 108);
            positions[50].x = 188;
            positions[51].x = 189;
            positions[57].x = 234;
            positions[70] = new Vector3(350, 0, 108);
            positions[71] = new Vector3(350, 0, 78);
            for (int i = 0; i < positions.Length; i++)
            {
                route.Points[i].feet.position = frame.TransformPoint(positions[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(route.Points[i].feet);
            }
            var lever = module.Find("Pursuer fire controls");
            lever.position = frame.TransformPoint(new Vector3(300, 0, 108));
            PrefabUtility.RecordPrefabInstancePropertyModifications(lever);

            // These infills continue existing concrete wall planes across former observation slots.
            // The descending player route remains east of X56.5 and clear of the south-wall endpoint.
            Box("South shaft concrete infill", new(38.25f, 1.5f, 61.75f), new(29.5f, 33, 1.5f));
            Box("Descent concrete wall infill", new(56.5f, -4.5f, 33.5f), new(3, 45, 51));
            Box("South shaft corner concrete infill", new(54.75f,1.5f,61.75f), new(3.5f,33,1.5f));
            Box("Shaft corner concrete infill", new(56.5f,-4.5f,60.375f), new(3,45,2.75f));
            Box("Lower shaft corner concrete infill", new(56.5f,-18,3), new(3,22,10));
            Box("Corridor observation concrete infill", new(23.5f,5,42.5f), new(2,10,40));
            Box("Buried facility foundation concrete infill", new(150,-25,78), new(10,44,50));
            var coopWall = FindWall(new Vector3(96.25f,-26,46.5f));
            if (coopWall) coopWall.gameObject.SetActive(true);
            var flameWall = FindWall(new Vector3(114,-10.5f,82.5f));
            if (flameWall) flameWall.gameObject.SetActive(true);

            // Restore the actual facility walls with discrete authored breakable bays.
            Breach("Corridor departure concrete breach", 0, new(23.5f,3.5f,57.6f), new(2.4f,7,7), Vector3.right);
            Breach("First wire concrete breach", 2, new(47.4f,-2.5f,61.75f), new(6,7,2), Vector3.forward);
            Breach("Second wire concrete breach", 5, new(56.5f,-6.5f,48.3f), new(3.4f,7,10), Vector3.right);
            Breach("Lower wire concrete breach", 7, new(56.5f,-12.5f,32), new(3.4f,7,6), Vector3.right);
            Breach("Pump concrete breach", 8, new(56.5f,-18.5f,10), new(3.4f,7,6), Vector3.right);
            Breach("Conveyor concrete breach", 12, new(20,-18.5f,-40), new(3.4f,7,7), Vector3.right);
            Breach("Switch wire concrete breach", 14, new(44,-15,-82), new(7,7,4.4f), Vector3.forward);
            Breach("Return cannon concrete breach", 15, new(80,-15,-82), new(7,7,4.4f), Vector3.forward);
            Breach("Cooperative concrete breach", 22, new(86,-1.5f,46.5f), new(7,7,3.4f), Vector3.back);
            Breach("Button concrete breach", 28, new(102,-.5f,46.5f), new(7,7,3.4f), Vector3.back);
            Breach("Flame corridor concrete breach", 33, new(114,-4.5f,72), new(3.4f,7,7), Vector3.right);
            Breach("Upper cannon concrete breach", 34, new(114,-4.5f,104), new(3.4f,7,7), Vector3.right);
            int[] finalPoints = {47,50,52,55,57,58,62,67};
            foreach (int i in finalPoints)
            {
                var player = frame.InverseTransformPoint(route.Points[i].playerPath.position);
                float x = Mathf.Lerp(positions[i].x, player.x, 7f / 30f);
                Breach("Final corridor concrete breach " + i, i, new(x,3.5f,101), new(10,7,4.4f), Vector3.back);
            }
            Breach("Final leap concrete breach", 70, new(350,4,101), new(7,8,4.4f), Vector3.back);

            foreach (var gate in gates)
            {
                if (gate.routePoint == 0 || gate.routePoint == 70) continue;
                var a = positions[gate.routePoint];
                var b = frame.InverseTransformPoint(route.Points[gate.routePoint].playerPath.position);
                CutPassage(a, b, new Vector3(3, 6.8f, 3), Vector3.up * 3.5f);
            }

            // Continuous floors are confined to the backs of the walls. Cross-wall service cuts
            // are made only along this authored route, with the concrete shell retained around them.
            for (int i = 0; i < 70; i++)
            {
                Vector3 a = positions[i], b = positions[i + 1];
                if (Vector3.Distance(a,b) < .01f) continue;
                CutHiddenPassage(a,b);
                var floor = Box("Hidden concrete floor " + i, (a+b)*.5f - Vector3.up*.25f,
                    new Vector3(3.8f,.5f,Vector3.Distance(a,b)+.15f));
                floor.transform.localRotation = Quaternion.LookRotation(b-a);
            }
            foreach (var piece in root.GetComponentsInChildren<Transform>(true)
                .Where(t => t != root && !t.gameObject.activeSelf && t.name.StartsWith("Concrete remainder")).ToArray())
                Object.DestroyImmediate(piece.gameObject);
            var walls = module.GetComponent<EscapePursuerWalls>();
            if (!walls) walls = module.gameObject.AddComponent<EscapePursuerWalls>();
            var actor = module.GetComponentInChildren<EscapeUnderstageChase>(true);
            var serialized = new SerializedObject(walls);
            serialized.FindProperty("actor").objectReferenceValue = actor;
            serialized.FindProperty("route").objectReferenceValue = route;
            var array = serialized.FindProperty("gates"); array.arraySize = gates.Count;
            for (int i = 0; i < gates.Count; i++)
            {
                var item = array.GetArrayElementAtIndex(i); var gate = gates[i];
                item.FindPropertyRelative("panel").objectReferenceValue = gate.panel;
                item.FindPropertyRelative("barrier").objectReferenceValue = gate.barrier;
                item.FindPropertyRelative("debris").objectReferenceValue = gate.debris;
                item.FindPropertyRelative("routePoint").intValue = gate.routePoint;
                item.FindPropertyRelative("outward").vector3Value = gate.outward;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var chase = new SerializedObject(actor);
            chase.FindProperty("walls").objectReferenceValue = walls;
            chase.ApplyModifiedPropertiesWithoutUndo();
            Tune();
            Physics.SyncTransforms();
            UnderstageChaseNavigation.Bake();
            SaveModule(scene);
        }

        static void Tune()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CurtainCall.Settings.EscapeRouteSettings>(
                "Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset");
            var tuning = settings.Pursuer;
            tuning.runSpeed = 6.6f;
            tuning.concealedRunSpeed = 13;
            tuning.finaleRunSpeed = 6.6f;
            tuning.contactRange = 3.6f;
            tuning.armRange = 36;
            tuning.hallWarning = .45f;
            tuning.stationWarning = 1.1f;
            tuning.wireWarning = .85f;
            tuning.armStrikeSeconds = .22f;
            tuning.armRetractSeconds = .3f;
            tuning.missRecoverySeconds = .35f;
            tuning.bossAttackWarning = .55f;
            tuning.bossMoveSpeed = 6.6f;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        static MeshRenderer FindWall(Vector3 center) => frame.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => !r.transform.IsChildOf(module)).FirstOrDefault(r =>
                Vector3.Distance(frame.InverseTransformPoint(r.bounds.center), center) < .1f && r.name.Contains("벽"));

        static GameObject Box(string name, Vector3 center, Vector3 size, Material material = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material ? material : concrete;
            return go;
        }

        static void Breach(string name, int point, Vector3 center, Vector3 size, Vector3 outward)
        {
            Cut(new Bounds(center, size));
            var panel = Box(name, center, size);
            var debris = new GameObject(name + " debris").transform;
            debris.SetParent(root, false);
            for (int i = 0; i < 12; i++)
            {
                var piece = Box(name + " fragment " + i, center, new Vector3(.55f, .65f, .45f));
                Object.DestroyImmediate(piece.GetComponent<Collider>());
                piece.AddComponent<EscapePursuerDebris>();
                piece.transform.SetParent(debris, false);
                var across = Vector3.Cross(Vector3.up, outward);
                piece.transform.localPosition = center + across * ((i % 4 - 1.5f) * .85f) + Vector3.up * ((i / 4 - 1) * 1.4f);
            }
            debris.gameObject.SetActive(false);
            gates.Add(new EscapePursuerWalls.Gate
            {
                panel = panel.transform,
                barrier = panel.GetComponent<BoxCollider>(),
                debris = debris,
                routePoint = point,
                outward = outward
            });
        }

        static BoxCollider[] ConcreteWalls()
        {
            Physics.SyncTransforms();
            return frame.GetComponentsInChildren<BoxCollider>(false).Where(c => !c.isTrigger &&
                c.GetComponent<MeshRenderer>() && (c.name.Contains("벽") || c.name.Contains("잼") ||
                c.name == "압축 출발부 북측 기초" || c.name == "전망을 나누는 시설 기둥" ||
                c.name.Contains("전망창") && c.name.Contains("프레임") || c.name.EndsWith("infill") ||
                c.name.StartsWith("Concrete remainder") || physicalClearance && (c.name.StartsWith("Hidden concrete floor ") ||
                c.name.StartsWith("발사대 남측 난간") || c.name == "출발부 두꺼운 낮은 인방" || c.name == "측면 파쇄 반출기 기초"))).ToArray();
        }

        internal static void OpenPhysicalPassage(Transform campaign, Transform pursuer, Transform output, Vector3 from, Vector3 to)
        {
            frame = campaign; module = pursuer; root = output;
            physicalClearance = true;
            var previous = ConcreteWalls();
            CutPassage(frame.InverseTransformPoint(from), frame.InverseTransformPoint(to), new Vector3(3.2f, 6.6f, 3.2f), Vector3.up * 3.35f);
            foreach (var wall in previous)
                if (wall && !wall.gameObject.activeSelf) Object.DestroyImmediate(wall.gameObject);
            physicalClearance = false;
        }

        static Bounds LocalBounds(BoxCollider wall) =>
            new(frame.InverseTransformPoint(wall.bounds.center), wall.bounds.size);

        static void CutHiddenPassage(Vector3 a, Vector3 b)
            => CutPassage(a, b, new Vector3(4.6f, 8, 4.6f), Vector3.up * 2.5f);

        static void CutPassage(Vector3 a, Vector3 b, Vector3 clearance, Vector3 offset)
        {
            var delta = b - a;
            float length = delta.magnitude;
            if (length < .01f) return;
            foreach (var wall in ConcreteWalls())
            {
                var envelope = LocalBounds(wall);
                envelope.Expand(clearance);
                if (!envelope.IntersectRay(new Ray(a + offset, delta.normalized), out float enter) || enter > length) continue;
                float exit = length;
                if (!envelope.Contains(b + offset) &&
                    envelope.IntersectRay(new Ray(b + offset, -delta.normalized), out float reverse)) exit -= reverse;
                Vector3 start = a + delta.normalized * Mathf.Max(0, enter) + offset;
                Vector3 end = a + delta.normalized * Mathf.Min(length, exit) + offset;
                var opening = new Bounds(start, clearance);
                opening.Encapsulate(new Bounds(end, clearance));
                CutWall(wall, opening);
            }
        }

        static void Cut(Bounds opening)
        {
            foreach (var wall in ConcreteWalls()) CutWall(wall, opening);
        }

        static void CutWall(BoxCollider wall, Bounds opening)
        {
            var bounds = LocalBounds(wall);
            if (!bounds.Intersects(opening)) return;
            Vector3 lo = Vector3.Max(bounds.min, opening.min), hi = Vector3.Min(bounds.max, opening.max);
            if (hi.x - lo.x < .005f || hi.y - lo.y < .005f || hi.z - lo.z < .005f) return;
            var material = wall.GetComponent<MeshRenderer>().sharedMaterial;
            wall.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(wall.gameObject);
            void Piece(Vector3 min, Vector3 max)
            {
                Vector3 size = max - min;
                if (size.x > .01f && size.y > .01f && size.z > .01f)
                    Box("Concrete remainder", (min + max) * .5f, size, material);
            }
            Piece(bounds.min, new Vector3(lo.x, bounds.max.y, bounds.max.z));
            Piece(new Vector3(hi.x, bounds.min.y, bounds.min.z), bounds.max);
            Piece(new Vector3(lo.x, bounds.min.y, bounds.min.z), new Vector3(hi.x, lo.y, bounds.max.z));
            Piece(new Vector3(lo.x, hi.y, bounds.min.z), new Vector3(hi.x, bounds.max.y, bounds.max.z));
            Piece(new Vector3(lo.x, lo.y, bounds.min.z), new Vector3(hi.x, hi.y, lo.z));
            Piece(new Vector3(lo.x, lo.y, hi.z), new Vector3(hi.x, hi.y, bounds.max.z));
        }

        static void SaveModule(Scene scene)
        {
            var external = new List<(Component component, string property, Object value)>();
            foreach (var component in module.GetComponentsInChildren<Component>(true))
            {
                if (!component) continue;
                var so = new SerializedObject(component);
                var p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = p.objectReferenceValue;
                    Transform t = value is Component c ? c.transform : value is GameObject g ? g.transform : null;
                    if (t && !EditorUtility.IsPersistent(t) && !t.IsChildOf(module))
                        external.Add((component, p.propertyPath, value));
                }
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(module.gameObject, Prefab, InteractionMode.AutomatedAction);
            foreach (var reference in external)
            {
                if (!reference.component) throw new InvalidOperationException("Prefab 저장 후 외부 참조 대상을 확인하세요.");
                var so = new SerializedObject(reference.component);
                so.FindProperty(reference.property).objectReferenceValue = reference.value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.SaveScene(scene, EscapeMppmReviewConfig.PursuerScene, true);
            AssetDatabase.SaveAssets();
        }
    }
}
