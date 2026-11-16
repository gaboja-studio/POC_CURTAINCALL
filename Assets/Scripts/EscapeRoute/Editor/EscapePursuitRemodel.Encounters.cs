using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuitRemodel
    {
        static List<EscapeUnderstageChase.PhysicalEncounter> ConfigureEncounters(EscapePursuerRoute route)
        {
            int[] sources = { 1, 6, 12, 15, 22, 32, 34, 47, 58, 70 };
            int[] until = { 6, 11, 14, 21, 31, 34, 47, 58, 70, 73 };
            Vector3[] landings = {
                new(27,0,73), new(67.6f,-22,4), new(38,-19.2f,-67), new(82,-18.5f,-71.5f),
                new(94.95f,-5,29.34f), new(122.5f,-8,52),
                new(123.6f,-8,101.4f), new(146,0,78), new(270,0,78), new(336,0,78)
            };
            Vector3[] centers = {
                new(27.25f,0,76), new(67,-22,9.25f), new(38.25f,-19.2f,-65.4f), new(81,-18.5f,-74),
                new(95.55f,-5,26), new(122.5f,-8,69),
                new(122.5f,-8,104), new(152,0,78), new(251,0,78), new(344,0,78)
            };
            Vector2[] sizes = { new(9.5f,12), new(8,17.5f), new(11.5f,6.8f), new(10,8),
                new(4.7f,12), new(5,40), new(5,8), new(24,14), new(50,14), new(44,14) };
            var walls = module.GetComponent<EscapePursuerWalls>();
            var wallsData = new SerializedObject(walls);
            var gates = wallsData.FindProperty("gates");
            for (int i = gates.arraySize - 1; i > sources.Length; i--)
            {
                var obsolete = walls.At(i);
                UnityEngine.Object.DestroyImmediate(obsolete.panel.gameObject);
                UnityEngine.Object.DestroyImmediate(obsolete.debris.gameObject);
                gates.DeleteArrayElementAtIndex(i);
            }
            wallsData.ApplyModifiedPropertiesWithoutUndo();
            var result = new List<EscapeUnderstageChase.PhysicalEncounter>();
            for (int i = 0; i < sources.Length; i++)
            {
                int source = sources[i], index = i + 1;
                var a = frame.InverseTransformPoint(route.Points[source].feet.position);
                var toward = landings[i] - a;
                Vector3 normal = Mathf.Abs(toward.x) > Mathf.Abs(toward.z)
                    ? Vector3.right * Mathf.Sign(toward.x) : Vector3.forward * Mathf.Sign(toward.z);
                if (index == 3) normal = Vector3.right;
                var side = Vector3.Cross(Vector3.up, normal);
                if (Vector3.Dot(side, toward) < 0) side = -side;
                if (index == 7 || index == 10) side = -side;
                if (index == 4) side = Vector3.left;
                float sideOffset = index == 6 ? 14 : index == 8 ? 12 : index == 10 ? 18 : 10;
                // The final entry has an oblique view along the hall: add another turn inside.
                Vector3 entry = index == 8 ? a + Vector3.right * 6 : a;
                Vector3 staging = entry + normal * 5.4f;
                Vector3 approach = staging + side * sideOffset;
                Vector3 exit = entry + normal * 7.7f + side * sideOffset;
                Vector3 panel = entry + normal * 8 + side * sideOffset;
                var gate = walls.At(index);
                gate.panel.SetPositionAndRotation(frame.TransformPoint(panel + Vector3.up * 7), Quaternion.LookRotation(frame.TransformDirection(normal)));
                gate.panel.localScale = new Vector3(2 * Mathf.Max(3.6f, LeftClearance(index), RightClearance(index)), 14, .6f);
                gate.barrier.center = Vector3.zero; gate.barrier.size = Vector3.one;
                gate.panel.gameObject.SetActive(true);
                gate.debris.SetPositionAndRotation(gate.panel.position, gate.panel.rotation);
                gate.debris.gameObject.SetActive(false);
                for (int j = 0; j < gate.debris.childCount; j++)
                {
                    var t = gate.debris.GetChild(j);
                    t.localPosition = new Vector3((j % 4 - 1.5f) * 1.1f, (j / 4 - 1) * 3, 0);
                    t.localRotation = Quaternion.identity; t.localScale = new Vector3(.85f, 1.8f, .4f);
                }
                var row = gates.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("routePoint").intValue = source + 1;
                row.FindPropertyRelative("outward").vector3Value = gate.panel.parent.InverseTransformDirection(frame.TransformDirection(normal));
                var area = new GameObject("Shared player floor pursuit " + index).AddComponent<BoxCollider>();
                area.transform.SetParent(structure, false); area.transform.position = frame.TransformPoint(centers[i]);
                area.center = Vector3.up * 2.5f; area.size = new Vector3(sizes[i].x, 5, sizes[i].y); area.isTrigger = true;
                result.Add(new EscapeUnderstageChase.PhysicalEncounter {
                    label = "벽 돌파 후 기존 기물 구역 추격 " + index,
                    gate = index, routePoint = source + 1, untilPoint = until[i], continueOutside = i == sources.Length - 1,
                    ingress = index == 8 ? Anchor("Hidden return before final bay", entry) : route.Points[source].feet,
                    staging = Anchor("Occluded turn " + index, staging), approach = Anchor("Wall contact " + index, approach),
                    exit = Anchor("Breakthrough takeoff " + index, exit), landing = Anchor("Shared floor landing " + index, landings[i]), platform = area
                });
                var light = new GameObject("Wall approach warning " + index).AddComponent<Light>();
                light.transform.SetParent(structure, false);
                light.transform.position = frame.TransformPoint(panel + normal * 3 + side * 2.8f + Vector3.up * 5.7f);
                light.range = 7; light.intensity = .55f; light.color = new Color(1,.62f,.32f); light.shadows = LightShadows.None;
                var beacon = new SerializedObject(light.gameObject.AddComponent<EscapePursuitBeacon>());
                beacon.FindProperty("pursuer").objectReferenceValue = module.GetComponentInChildren<EscapeUnderstageChase>(true);
                beacon.FindProperty("walls").objectReferenceValue = walls;
                beacon.FindProperty("lamp").objectReferenceValue = light;
                beacon.FindProperty("gate").intValue = index;
                beacon.ApplyModifiedPropertiesWithoutUndo();
            }
            wallsData.ApplyModifiedPropertiesWithoutUndo();
            var data = new SerializedObject(module.GetComponentInChildren<EscapeUnderstageChase>(true));
            var entries = data.FindProperty("encounters"); entries.arraySize = result.Count;
            for (int i = 0; i < result.Count; i++)
            {
                var e = result[i]; var p = entries.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("label").stringValue = e.label;
                p.FindPropertyRelative("gate").intValue = e.gate;
                p.FindPropertyRelative("routePoint").intValue = e.routePoint;
                p.FindPropertyRelative("untilPoint").intValue = e.untilPoint;
                p.FindPropertyRelative("continueOutside").boolValue = e.continueOutside;
                p.FindPropertyRelative("ingress").objectReferenceValue = e.ingress;
                p.FindPropertyRelative("staging").objectReferenceValue = e.staging;
                p.FindPropertyRelative("approach").objectReferenceValue = e.approach;
                p.FindPropertyRelative("exit").objectReferenceValue = e.exit;
                p.FindPropertyRelative("landing").objectReferenceValue = e.landing;
                p.FindPropertyRelative("platform").objectReferenceValue = e.platform;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return result;
        }
    }
}
