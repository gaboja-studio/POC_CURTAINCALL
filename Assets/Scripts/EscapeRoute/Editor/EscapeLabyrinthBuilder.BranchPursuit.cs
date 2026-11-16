using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void LForkEncounter(EscapePursuerRoute route)
        {
            // A dogleg in the original service-wall volume faces the fork across the void.
            // Its floor is west of the retained descending wire, never underneath that wire.
            LOpen(route,new(55.5f,-2.7f,58),new(11.8f,6.8f,4.8f));
            LOpen(route,new(58.5f,1,57.8f),new(8,14,11.6f));
            LBox("Fork concealed floor",new(58.7f,-6.45f,57.65f),new(7.6f,.9f,7.7f),bConcrete);
            LBox("Fork concealed east cheek",new(62.9f,1,57.65f),new(.8f,14,7.7f),bConcrete);
            LBox("Fork concealed back wall",new(58.7f,1,53.4f),new(7.6f,14,.8f),bConcrete);
            LBox("Fork concealed roof",new(56.3f,8.4f,57.65f),new(12.4f,.8f,7.7f),bConcrete);
            LBox("Fork opaque dogleg baffle",new(54.1f,1,60),new(.6f,14,2),bConcrete);
            LBox("Fork west facade return",new(52.3f,1,60),new(4.4f,14,.6f),bConcrete);
            var barrier=LBox("Fork breakable concrete bay",new(58.5f,1,60),new(8,14,.6f),bConcrete);
            var walls=route.GetComponent<EscapePursuerWalls>();
            int gateIndex=LBranchGate(walls,barrier,4,Vector3.forward,"Fork");
            LBranchEncounter(route,"검사 갈림길 · 연속 봉 감시",gateIndex,4,9,route.Points[3].feet,
                new(58.5f,-6,55.5f),new(58.5f,-6,57),new(58.5f,-6,60.1f),
                new(67,-6,88),new(67,-6,88),new(6,5,8),"Fork");
        }

        static int LBranchGate(EscapePursuerWalls walls,BoxCollider barrier,int point,Vector3 outward,string name)
        {
            var panel=barrier.transform;
            var debris=LAnchor(name+" concrete fragments",labyrinthFrame.InverseTransformPoint(panel.position));
            for(int i=0;i<12;i++)
            {
                var piece=GameObject.CreatePrimitive(PrimitiveType.Cube);piece.name=name+" broken fragment "+i;
                Object.DestroyImmediate(piece.GetComponent<Collider>());piece.AddComponent<EscapePursuerDebris>();
                piece.transform.SetParent(debris,false);piece.transform.localPosition=new((i%4-1.5f)*1.8f,(i/4-1)*4,0);
                piece.transform.localScale=new(1.4f,3.3f,.4f);piece.GetComponent<MeshRenderer>().sharedMaterial=bConcrete;
            }
            debris.gameObject.SetActive(false);
            var data=new SerializedObject(walls);var gates=data.FindProperty("gates");int index=gates.arraySize++;
            var g=gates.GetArrayElementAtIndex(index);g.FindPropertyRelative("panel").objectReferenceValue=panel;
            g.FindPropertyRelative("barrier").objectReferenceValue=barrier;g.FindPropertyRelative("debris").objectReferenceValue=debris;
            g.FindPropertyRelative("routePoint").intValue=point;
            g.FindPropertyRelative("outward").vector3Value=panel.parent.InverseTransformDirection(labyrinthFrame.TransformDirection(outward));
            data.ApplyModifiedPropertiesWithoutUndo();return index;
        }

        static void LBranchEncounter(EscapePursuerRoute route,string label,int gate,int point,int until,Transform ingress,
            Vector3 staging,Vector3 approach,Vector3 exit,Vector3 landing,Vector3 area,Vector3 size,string name)
        {
            var platform=LAnchor(name+" public pursuit area",area).gameObject.AddComponent<BoxCollider>();
            platform.center=Vector3.up*size.y*.5f;platform.size=size;platform.isTrigger=true;
            var data=new SerializedObject(route.GetComponentInChildren<EscapeUnderstageChase>(true));var rows=data.FindProperty("encounters");
            int index=rows.arraySize++;var row=rows.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("label").stringValue=label;row.FindPropertyRelative("gate").intValue=gate;
            row.FindPropertyRelative("routePoint").intValue=point;row.FindPropertyRelative("untilPoint").intValue=until;
            row.FindPropertyRelative("continueOutside").boolValue=false;row.FindPropertyRelative("ingress").objectReferenceValue=ingress;
            row.FindPropertyRelative("staging").objectReferenceValue=LAnchor(name+" staging",staging);
            row.FindPropertyRelative("approach").objectReferenceValue=LAnchor(name+" approach",approach);
            row.FindPropertyRelative("exit").objectReferenceValue=LAnchor(name+" takeoff",exit);
            row.FindPropertyRelative("landing").objectReferenceValue=LAnchor(name+" landing",landing);
            row.FindPropertyRelative("platform").objectReferenceValue=platform;data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void LHeatEncounter(EscapePursuerRoute route)
        {
            // One existing wall strike can lead to either public heat route. Only the airborne
            // aperture changes; the original concealed approach, wall timing and lower bay stay.
            LOpen(route,new(122.5f,6,49),new(9,18,9));
            LOpen(route,new(132.5f,8,47),new(12,16,8));
            LCutNamedBox("Switchback west retaining wall",new Bounds(new(120,5.7f,49),new(1.2f,17.4f,7)));
            var data=new SerializedObject(route.GetComponentInChildren<EscapeUnderstageChase>(true));
            var row=data.FindProperty("encounters").GetArrayElementAtIndex(5);
            Vector3 P(string key)=>labyrinthFrame.InverseTransformPoint(((Transform)row.FindPropertyRelative(key).objectReferenceValue).position);
            LBranchEncounter(route,"외부 추출로 · 화염실 벽 돌파",row.FindPropertyRelative("gate").intValue,33,48,
                (Transform)row.FindPropertyRelative("ingress").objectReferenceValue,P("staging"),P("approach"),P("exit"),
                new(140,0,47),new(140,0,47),new(5,5,6),"Extraction");
            AddLabyrinthBoardingEncounter();
        }
    }
}
