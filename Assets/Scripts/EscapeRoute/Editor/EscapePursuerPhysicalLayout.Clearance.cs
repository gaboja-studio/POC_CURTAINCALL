using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuerPhysicalLayout
    {
        [MenuItem("Tools/CurtainCall/탈출로/추격 발판 낙하 여유 복구")]
        public static void RestoreFallClearance()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != EscapePursuerLayoutReview.SourceScene)
                throw new InvalidOperationException("운영 탈출로 A 편집 모드에서 실행하세요.");
            var route = Object.FindFirstObjectByType<EscapePursuerRoute>();
            module = route.transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            root = module.Find(RootName);
            var chase = module.GetComponentInChildren<EscapeUnderstageChase>(true);
            var data = new SerializedObject(chase);
            var entries = data.FindProperty("encounters");
            var walls = module.GetComponent<EscapePursuerWalls>();
            // Keep the outbound upper service route clear of its returning lower route.
            var revised = new System.Collections.Generic.Dictionary<int, Vector3>
            {
                [3] = new(52.5f, -6, 58), [4] = new(52.5f, -7.1f, 49),
                [17] = new(150, -5, 145), [18] = new(78, -5, 145), [19] = new(78, -5, 60)
            };
            foreach (var change in revised)
            {
                route.Points[change.Key].feet.position = frame.TransformPoint(change.Value);
                PrefabUtility.RecordPrefabInstancePropertyModifications(route.Points[change.Key].feet);
            }
            for (int i = 20; i <= 28; i++)
            {
                var position = frame.InverseTransformPoint(route.Points[i].feet.position);
                position.y = -5;
                route.Points[i].feet.position = frame.TransformPoint(position);
                PrefabUtility.RecordPrefabInstancePropertyModifications(route.Points[i].feet);
            }
            var routeData = new SerializedObject(route);
            var points = routeData.FindProperty("points");
            for (int i = 1; i < 47; i++)
            {
                Vector3 delta = route.Points[i].feet.position - route.Points[i - 1].feet.position;
                points.GetArrayElementAtIndex(i).FindPropertyRelative("jump").boolValue =
                    Mathf.Abs(delta.y) > .5f && delta.magnitude < 60;
            }
            routeData.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                int point = entry.FindPropertyRelative("routePoint").intValue;
                int gate = entry.FindPropertyRelative("gate").intValue;
                var landing = (Transform)entry.FindPropertyRelative("landing").objectReferenceValue;
                var area = (BoxCollider)entry.FindPropertyRelative("platform").objectReferenceValue;
                var floor = root.Find("Pursuit platform " + walls.At(gate).routePoint);
                var staging = (Transform)entry.FindPropertyRelative("staging").objectReferenceValue;
                staging.position = route.Points[walls.At(gate).routePoint].feet.position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(staging);
                Vector3 center = frame.InverseTransformPoint(area.transform.position);
                Vector3 size = area.size;
                switch (point)
                {
                    case 2: center = new(48, -6, 66); size = new(4, 5, 6); break;
                    case 5: center = new(60, -10, 57); size = new(3.2f, 5, 10); break;
                    case 7: center = new(60.5f, -16, 34); size = new(4, 5, 6); break;
                    case 8: center = new(60.5f, -22, 10); size = new(4, 5, 6); break;
                    case 12: center = new(30, -22, -53.5f); size = new(4, 5, 37); break;
                    case 14: center = new(39, -18.5f, -78.2f); size = new(3.2f, 5, 3.2f); break;
                    case 15: center = new(88, -18.5f, -77); size = new(4, 5, 3.6f); break;
                    case 22: center = new(94, -5, 38); size = new(4, 5, 6); break;
                    case 28: center = new(102, 0, 39); size = new(4, 5, 4); break;
                    case 37: center = new(143, 0, 101.5f); size = new(3.2f, 5, 25); break;
                    case 47: center = new(145, 0, 94); size = new(6, 5, 8); break;
                    default: continue;
                }
                Vector3 land = center;
                if (point == 12) land.z = -40;
                if (point == 37) land.z = 109;
                area.transform.position = frame.TransformPoint(center);
                area.size = size;
                landing.position = frame.TransformPoint(land);
                floor.position = frame.TransformPoint(center - Vector3.up * .3f);
                floor.rotation = frame.rotation;
                floor.localScale = new(size.x, .6f, size.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(area);
                PrefabUtility.RecordPrefabInstancePropertyModifications(area.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(landing);
                PrefabUtility.RecordPrefabInstancePropertyModifications(floor);
                if (point == 47)
                {
                    route.Points[47].feet.position = landing.position;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(route.Points[47].feet);
                }
            }
            // Land inside the workshop so the long leap clears the fire isolation lintel.
            route.Points[72].feet.position = frame.TransformPoint(new Vector3(384, 0, 78));
            PrefabUtility.RecordPrefabInstancePropertyModifications(route.Points[72].feet);
            var finalLanding = root.Find("Pursuit landing 72");
            finalLanding.position = route.Points[72].feet.position - Vector3.up * .3f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(finalLanding);
            // The workshop already supports both landings. Extra pads duplicate its visible floor.
            foreach (int i in new[] { 72, 73 })
            {
                var pad = root.Find("Pursuit landing " + i).gameObject;
                pad.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pad);
            }
            Physics.SyncTransforms();
            foreach (int i in new[] { 72, 73 })
                if (!EscapePursuerNavigation.Supported(route.Points[i].feet.position))
                    throw new InvalidOperationException("작업장 착지 " + i + "의 기존 바닥 지지가 없습니다.");
            var lightSection = frame.GetComponentsInChildren<Transform>()
                .First(t => t.name == "06_FlamesSwingCannonAndUpperWire");
            foreach (Transform fixture in lightSection)
            {
                var p = frame.InverseTransformPoint(fixture.position);
                if (Mathf.Abs(p.x - 136) > .1f || Mathf.Abs(p.z - 109) > .1f || p.y < 6.9f || p.y > 7.2f) continue;
                p.z = 115;
                fixture.position = frame.TransformPoint(p);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            }
            Physics.SyncTransforms();
            Save(scene);
        }

        [MenuItem("Tools/CurtainCall/탈출로/추격 경로 몸통 통과 공간 복구")]
        public static void RepairTraversalClearance()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != EscapePursuerLayoutReview.SourceScene)
                throw new InvalidOperationException("운영 탈출로 A 편집 모드에서 실행하세요.");
            var route = Object.FindFirstObjectByType<EscapePursuerRoute>();
            module = route.transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            root = module.Find(RootName);
            var chase = module.GetComponentInChildren<EscapeUnderstageChase>(true);
            var entries = new SerializedObject(chase).FindProperty("encounters");
            concrete = root.GetComponentsInChildren<MeshRenderer>().First().sharedMaterial;
            foreach (var floor in module.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name.StartsWith("Hidden concrete floor ") || t.name.StartsWith("Supported wall route ")).ToArray())
                Object.DestroyImmediate(floor.gameObject);
            for (int i = 1; i < route.Points.Count; i++)
                ClearLeg(route.Points[i - 1].feet.position, route.Points[i].feet.position, route.Points[i].jump);
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                Transform At(string field) => (Transform)entry.FindPropertyRelative(field).objectReferenceValue;
                ClearLeg(At("staging").position, At("approach").position, false);
                ClearLeg(At("approach").position, At("exit").position, false);
                ClearLeg(At("exit").position, At("landing").position, true);
                var area = (BoxCollider)entry.FindPropertyRelative("platform").objectReferenceValue;
                Vector3 center = area.transform.position;
                Vector3 direction = area.transform.forward * Mathf.Max(0, area.size.z * .5f - 1.5f);
                ClearLeg(center - direction, center + direction, false);
            }
            var unique = new System.Collections.Generic.HashSet<string>();
            foreach (var piece in root.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "Concrete remainder").ToArray())
            {
                var local = frame.InverseTransformPoint(piece.bounds.center);
                bool looseStrip = Mathf.Abs(local.x - 140.81f) < .1f && local.y > 0 && local.y < .5f
                    && piece.bounds.size.x < .4f && piece.bounds.size.y < .5f;
                string key = piece.bounds.center.ToString("F3") + piece.bounds.size.ToString("F3");
                if (looseStrip || !unique.Add(key)) Object.DestroyImmediate(piece.gameObject);
            }
            // Rebuild the support after clearing the body envelope. Keep the upper face on the
            // authored path even when the ramp is tilted.
            for (int i = 1; i <= 47; i++)
            {
                var from = route.Points[i - 1].feet.position;
                var to = route.Points[i].feet.position;
                if (route.Points[i].jump)
                {
                    Box("Supported wall route " + i + " takeoff", from - Vector3.up * .3f, new Vector3(3.2f, .6f, 3.2f));
                    Box("Supported wall route " + i + " landing", to - Vector3.up * .3f, new Vector3(3.2f, .6f, 3.2f));
                    continue;
                }
                var delta = to - from;
                if (delta.sqrMagnitude < .001f) continue;
                var rotation = Quaternion.LookRotation(delta);
                var support = Box("Supported wall route " + i, (from + to) * .5f - rotation * Vector3.up * .3f,
                    new Vector3(3.6f, .6f, delta.magnitude + .15f));
                support.transform.rotation = rotation;
            }
            MergeConcretePieces();
            Physics.SyncTransforms();
            Save(scene);
        }

        static void ClearLeg(Vector3 from, Vector3 to, bool jump)
        {
            if (!jump)
            {
                EscapePursuerConcreteRoutes.OpenPhysicalPassage(frame, module, root, from, to);
                return;
            }
            float arc = EscapeUnderstageChase.ServiceJumpArc(from, to);
            var previous = from;
            for (int k = 1; k <= 12; k++)
            {
                float t = k / 12f;
                var next = Vector3.Lerp(from, to, t) + Vector3.up * (arc * t * (1 - t));
                EscapePursuerConcreteRoutes.OpenPhysicalPassage(frame, module, root, previous, next);
                previous = next;
            }
        }

        static void MergeConcretePieces()
        {
            // CSG cuts leave adjacent boxes. Merge matching cross-sections to avoid accumulating
            // tiny renderers and overlapping colliders when the authoring operation is repeated.
            for (int pass = 0; pass < 3; pass++)
                for (int axis = 0; axis < 3; axis++)
                {
                    int a = (axis + 1) % 3, b = (axis + 2) % 3;
                    var groups = root.GetComponentsInChildren<BoxCollider>()
                        .Where(c => c.name == "Concrete remainder")
                        .GroupBy(c => new { minA = Mathf.RoundToInt(c.bounds.min[a] * 1000),
                            maxA = Mathf.RoundToInt(c.bounds.max[a] * 1000),
                            minB = Mathf.RoundToInt(c.bounds.min[b] * 1000),
                            maxB = Mathf.RoundToInt(c.bounds.max[b] * 1000),
                            material = c.GetComponent<MeshRenderer>().sharedMaterial });
                    foreach (var group in groups)
                    {
                        BoxCollider previous = null;
                        foreach (var piece in group.OrderBy(c => c.bounds.min[axis]).ToArray())
                        {
                            if (previous && piece.bounds.min[axis] <= previous.bounds.max[axis] + .001f)
                            {
                                var bounds = previous.bounds;
                                bounds.Encapsulate(piece.bounds);
                                previous.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
                                previous.transform.localScale = bounds.size;
                                Object.DestroyImmediate(piece.gameObject);
                                PrefabUtility.RecordPrefabInstancePropertyModifications(previous.transform);
                                Physics.SyncTransforms();
                            }
                            else previous = piece;
                        }
                    }
                }
        }
    }
}
