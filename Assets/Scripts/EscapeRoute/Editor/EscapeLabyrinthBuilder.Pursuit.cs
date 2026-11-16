using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void LInspectionEncounter(EscapePursuerRoute route)
        {
            var walls = route.GetComponent<EscapePursuerWalls>();
            var chase = route.GetComponentInChildren<EscapeUnderstageChase>(true);
            if (!walls || !chase || route.Points.Count <= 36)
                throw new InvalidOperationException("The inspection arm requires the existing pursuer route and wall controller.");

            // Keep the gate west of the continuous X108 service gallery. Its old route stays open.
            LOpen(route, new Vector3(100, -.95f, 105.75f), new Vector3(8, 13.9f, 1.5f));
            // The new room joins the gallery through its west shell, above the retained floor.
            LOpen(route, new Vector3(104.8f, -4.725f, 111.5f), new Vector3(3.2f, 6.75f, 7));
            LBox("Inspection concealed room floor", new Vector3(100.8f, -8.45f, 110),
                new Vector3(9.6f, .9f, 12), bConcrete);
            LBox("Inspection concealed room west wall", new Vector3(95.6f, -1, 110),
                new Vector3(.8f, 14, 12), bConcrete);
            LBox("Inspection concealed room north wall", new Vector3(100.8f, -1, 116.4f),
                new Vector3(9.6f, 14, .8f), bConcrete);
            LBox("Inspection concealed room roof", new Vector3(100.4f, 6.4f, 110.2f),
                new Vector3(10.4f, .8f, 12.4f), bConcrete);
            LBox("Inspection gate east facade", new Vector3(104.8f, -1, 106),
                new Vector3(1.6f, 14, .8f), bConcrete);
            // This return hides the service gallery through the opened bay without crossing the dogleg.
            LBox("Inspection gallery sightline baffle", new Vector3(104.4f, -1, 107.45f),
                new Vector3(.4f, 14, 2.9f), bConcrete);

            var barrier = LBox("Inspection breakable concrete bay", new Vector3(100, -1, 106),
                new Vector3(8, 14, .6f), bConcrete);
            var panel = barrier.transform;
            panel.rotation = labyrinthFrame.rotation * Quaternion.LookRotation(Vector3.back);
            var debris = LAnchor("Inspection concrete bay debris", new Vector3(100, -1, 106));
            debris.rotation = panel.parent.rotation;
            for (int i = 0; i < 12; i++)
            {
                var fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fragment.name = "Inspection concrete fragment " + i;
                Object.DestroyImmediate(fragment.GetComponent<Collider>());
                fragment.AddComponent<EscapePursuerDebris>();
                fragment.transform.SetParent(debris, false);
                fragment.transform.localPosition = new Vector3((i % 4 - 1.5f) * 1.8f, (i / 4 - 1) * 4, 0);
                fragment.transform.localScale = new Vector3(1.4f, 3.3f, .4f);
                fragment.GetComponent<MeshRenderer>().sharedMaterial = bConcrete;
            }
            debris.gameObject.SetActive(false);

            var wallData = new SerializedObject(walls);
            var gates = wallData.FindProperty("gates");
            int gateIndex = gates.arraySize;
            gates.arraySize++;
            var gate = gates.GetArrayElementAtIndex(gateIndex);
            gate.FindPropertyRelative("panel").objectReferenceValue = panel;
            gate.FindPropertyRelative("barrier").objectReferenceValue = barrier;
            gate.FindPropertyRelative("debris").objectReferenceValue = debris;
            gate.FindPropertyRelative("routePoint").intValue = 36;
            gate.FindPropertyRelative("outward").vector3Value = panel.parent.InverseTransformDirection(
                labyrinthFrame.TransformDirection(Vector3.back));
            wallData.ApplyModifiedPropertiesWithoutUndo();

            var staging = LAnchor("Inspection concealed staging", new Vector3(108, -8, 112));
            // Keep the ordinary player jump short of the 14 m gap to the service threshold.
            // The near wall face is Z106.3: Z109 is inside the shared 2.8 m strike reach.
            var approach = LAnchor("Inspection wall strike approach", new Vector3(100, -8, 109));
            var exit = LAnchor("Inspection breach takeoff", new Vector3(100, -8, 105.7f));
            var landing = LAnchor("Inspection shared deck landing", new Vector3(100, -6, 87));
            var platform = LAnchor("Inspection shared deck pursuit area", new Vector3(100, -6, 83))
                .gameObject.AddComponent<BoxCollider>();
            platform.center = Vector3.up * 2.5f;
            platform.size = new Vector3(8, 5, 14);
            platform.isTrigger = true;

            var chaseData = new SerializedObject(chase);
            var encounters = chaseData.FindProperty("encounters");
            int encounterIndex = encounters.arraySize;
            encounters.arraySize++;
            var encounter = encounters.GetArrayElementAtIndex(encounterIndex);
            encounter.FindPropertyRelative("label").stringValue = "검사 외줄 우회로 · 콘크리트 돌파";
            encounter.FindPropertyRelative("gate").intValue = gateIndex;
            encounter.FindPropertyRelative("routePoint").intValue = 36;
            encounter.FindPropertyRelative("untilPoint").intValue = 47;
            encounter.FindPropertyRelative("continueOutside").boolValue = false;
            encounter.FindPropertyRelative("ingress").objectReferenceValue = route.Points[35].feet;
            encounter.FindPropertyRelative("staging").objectReferenceValue = staging;
            encounter.FindPropertyRelative("approach").objectReferenceValue = approach;
            encounter.FindPropertyRelative("exit").objectReferenceValue = exit;
            encounter.FindPropertyRelative("landing").objectReferenceValue = landing;
            encounter.FindPropertyRelative("platform").objectReferenceValue = platform;
            chaseData.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
