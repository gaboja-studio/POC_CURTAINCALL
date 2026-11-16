using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void LCentralEncounter(EscapePursuerRoute route)
        {
            var walls = route.GetComponent<EscapePursuerWalls>();
            var chase = route.GetComponentInChildren<EscapeUnderstageChase>(true);
            if (!walls || !chase || route.Points.Count <= 55)
                throw new InvalidOperationException("The central pursuit bay requires the existing service route and wall controller.");

            // Branch south from the preserved service gallery. Its floor ends at Z105.6;
            // the new floor meets that edge and stops at Z99, 14 m from the public deck.
            LOpen(route, new Vector3(183.8f, 3.25f, 105.2f), new Vector3(16.4f, 6.7f, 1.6f));
            LOpen(route, new Vector3(183.8f, 6.95f, 101.55f), new Vector3(16.4f, 14.1f, 6.5f));
            LOpen(route, new Vector3(188, 6.95f, 101), new Vector3(8, 14.1f, 2));
            // X188 clears the live X196 discharge chute and the X204 debris assembly.
            // Only static concrete is cut; the supported landing is the existing main floor.
            LOpen(route, new Vector3(188, 5.2f, 90.35f), new Vector3(3.2f, 10.6f, 23.9f));
            // The former scenery service shelf was a player-accessible shortcut to the bay.
            // Remove its complete decorative assembly and only the matching isolated supports.
            foreach(var t in labyrinthFrame.GetComponentsInChildren<Transform>().Where(t=>t.name=="측면 원동기와 집진 설비").ToArray())
            {
                var boxes=t.GetComponentsInChildren<Collider>();
                if(boxes.Any(c=>new Bounds(LW(new(188,-2,92)),new(10,5,11)).Intersects(c.bounds))
                    && t.GetComponentsInChildren<MonoBehaviour>(true).Length==0) Object.DestroyImmediate(t.gameObject);
            }
            var gap=new Bounds(new Vector3(188,-49.9f,92),new Vector3(8,100.2f,14));
            LCutNamedBox("Concrete remainder",gap);
            LCutNamedBox("반출 구동기 전용 받침",gap);
            LCutNamedBox("반출 구동기 받침 기둥",gap);
            var floorBounds = new Bounds(new Vector3(183.8f, -.45f, 102.3f), new Vector3(16.4f, .9f, 6.6f));
            LCutNamedBox("Concrete remainder", floorBounds, 0f);
            LBox("Central concealed room floor", floorBounds.center, floorBounds.size, bConcrete);
            LBox("Central concealed room west wall", new Vector3(175.2f, 7, 102.3f),
                new Vector3(.8f, 14, 6.6f), bConcrete);
            LBox("Central concealed room east wall", new Vector3(192.4f, 7, 102.3f),
                new Vector3(.8f, 14, 6.6f), bConcrete);
            LBox("Central concealed room roof", new Vector3(183.8f, 14.4f, 102.1f),
                new Vector3(18, .8f, 7), bConcrete);
            LBox("Central gate west facade", new Vector3(179.8f, 7, 101),
                new Vector3(8.4f, 14, .8f), bConcrete);
            // The ingress passes west of this return; the approach stays south of it.
            LBox("Central gallery sightline baffle", new Vector3(189.75f, 7, 106.2f),
                new Vector3(4.5f, 14, .4f), bConcrete);

            var barrier = LBox("Central breakable concrete bay", new Vector3(188, 7, 101),
                new Vector3(8, 14, .6f), bConcrete);
            var panel = barrier.transform;
            panel.rotation = labyrinthFrame.rotation * Quaternion.LookRotation(Vector3.back);
            var debris = LAnchor("Central concrete bay debris", new Vector3(188, 7, 101));
            debris.rotation = panel.parent.rotation;
            for (int i = 0; i < 12; i++)
            {
                var fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fragment.name = "Central concrete fragment " + i;
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
            gate.FindPropertyRelative("routePoint").intValue = 55;
            gate.FindPropertyRelative("outward").vector3Value = panel.parent.InverseTransformDirection(
                labyrinthFrame.TransformDirection(Vector3.back));
            wallData.ApplyModifiedPropertiesWithoutUndo();

            var staging = LAnchor("Central concealed staging", new Vector3(180, 0, 108));
            var approach = LAnchor("Central wall strike approach", new Vector3(188, 0, 104));
            var exit = LAnchor("Central breach takeoff", new Vector3(188, 0, 100.7f));
            var landing = LAnchor("Central shared deck landing", new Vector3(188, 0, 80));
            var platform = LAnchor("Central shared deck pursuit area", new Vector3(205, 0, 78))
                .gameObject.AddComponent<BoxCollider>();
            platform.center = Vector3.up * 2.5f;
            platform.size = new Vector3(42, 5, 14);
            platform.isTrigger = true;

            var chaseData = new SerializedObject(chase);
            var encounters = chaseData.FindProperty("encounters");
            int encounterIndex = encounters.arraySize;
            encounters.arraySize++;
            var encounter = encounters.GetArrayElementAtIndex(encounterIndex);
            encounter.FindPropertyRelative("label").stringValue = "중앙 반출동 · 외부 외줄 추격";
            encounter.FindPropertyRelative("gate").intValue = gateIndex;
            encounter.FindPropertyRelative("routePoint").intValue = 55;
            encounter.FindPropertyRelative("untilPoint").intValue = 59;
            encounter.FindPropertyRelative("continueOutside").boolValue = false;
            encounter.FindPropertyRelative("ingress").objectReferenceValue = route.Points[54].feet;
            encounter.FindPropertyRelative("staging").objectReferenceValue = staging;
            encounter.FindPropertyRelative("approach").objectReferenceValue = approach;
            encounter.FindPropertyRelative("exit").objectReferenceValue = exit;
            encounter.FindPropertyRelative("landing").objectReferenceValue = landing;
            encounter.FindPropertyRelative("platform").objectReferenceValue = platform;
            chaseData.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
