using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ExpandCooperativeSectionA()
        {
            if(Application.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)
                throw new InvalidOperationException("A edit scene required");
            var bg=GameObject.Find(RootName).transform;
            if(bg.Find("36_ALongerCooperativeCrossing")!=null)throw new InvalidOperationException("Already expanded");
            CompletionMaterials();
            var root=VerticalChild(bg,"36_ALongerCooperativeCrossing");
            root.localScale=new Vector3(1/bg.lossyScale.x,1/bg.lossyScale.y,1/bg.lossyScale.z);
            var coop=UnityEngine.Object.FindFirstObjectByType<EscapeCooperativeRopes>();
            var cd=new SerializedObject(coop);
            var old=cd.FindProperty("ropeSupports");
            var previous=Enumerable.Range(0,old.arraySize).Select(i=>old.GetArrayElementAtIndex(i).objectReferenceValue as Collider).ToArray();
            var ropesRoot=VerticalChild(root,"FourOriginalRopes");
            var ropes=new BoxCollider[4];
            for(int i=0;i<4;i++)
            {
                ropes[i]=CompletionRope(ropesRoot,"TimedWire_"+i,new Vector3(88,0,22+i*2),new Vector3(116,0,22+i*2));
                Ensure<HumanCannonLandingZone>(ropes[i].gameObject);
            }
            var course=UnityEngine.Object.FindFirstObjectByType<TightropeCourse>();
            var data=new SerializedObject(course);var registered=data.FindProperty("sceneRopes");
            for(int i=0;i<registered.arraySize;i++)
            {
                var item=registered.GetArrayElementAtIndex(i);
                int index=Array.IndexOf(previous,item.objectReferenceValue as Collider);
                if(index>=0)item.objectReferenceValue=ropes[index];
            }
            var shape=data.FindProperty("sceneShape");
            var zones=shape.FindPropertyRelative("laneChangeZones");zones.arraySize=1;
            zones.GetArrayElementAtIndex(0).vector2Value=new Vector2(0,28);
            var ends=shape.FindPropertyRelative("laneEndDistances");ends.arraySize=registered.arraySize;
            for(int i=0;i<registered.arraySize;i++)
            {
                var box=registered.GetArrayElementAtIndex(i).objectReferenceValue as BoxCollider;
                ends.GetArrayElementAtIndex(i).floatValue=box.size.z;
            }
            shape.FindPropertyRelative("finishDistance").floatValue=28;
            data.ApplyModifiedProperties();
            SetFinaleArray(cd,"ropeSupports",ropes);SetFinaleArray(cd,"ropeVisuals",ropesRoot.GetComponentsInChildren<Renderer>());
            cd.ApplyModifiedProperties();
            RemoveOpenObject(coop.transform.Find("FourOriginalRopes"));

            var lower=coop.transform.Find("하부 협동 작업대");
            RemoveOpenObject(lower); AFloor(root,"길어진 하부 협동 작업대",87.5f,116,20,32,-5);
            foreach(var t in coop.transform.Cast<Transform>().ToArray())
            {
                if(t.name.StartsWith("정비대",StringComparison.Ordinal)){RemoveOpenObject(t);continue;}
                if(t.name=="상부 도착 정비대" || t.name=="ButtonReturnStair" || t.name.StartsWith("ReturnButton",StringComparison.Ordinal)
                    || t.position.x>=100 && (t.name=="닳은 진행 도색" || t.name=="매입 정비등" || t.name=="작업등 함체" || t.name=="작업등 광원"))
                {Undo.RecordObject(t,"Extend cooperative crossing exit");t.position+=Vector3.right*16;}
            }
            // Keep the original lower access stair; a host-owned single-worker gate controls entry.
            foreach(float z in new[]{20f,32f})SpaceEdgeRail(root,new Vector3(87.5f,-5,z),new Vector3(116,-5,z));
            SpaceEdgeRail(root,new Vector3(116,0,20),new Vector3(121,0,20));
            var fifth=bg.Find(AFusedGroup+"/05_CooperativeReboundAndExit");
            foreach(var t in fifth.Cast<Transform>().ToArray())
            {
                if(t.name=="출구 전단벽 남측 잼"){RemoveOpenObject(t);continue;}
                if(t.name.StartsWith("A05_ReboundToButton",StringComparison.Ordinal) || t.name.StartsWith("A05_ButtonExitSwing",StringComparison.Ordinal)
                    || t.name=="버튼 출구대 측면 이음" || t.name=="버튼에서 출구 그네로" || t.name=="닳은 진행 도색" && t.position.y> -1)
                {Undo.RecordObject(t,"Move exit with the longer ropes");t.position+=Vector3.right*16;}
            }
            var exit=GameObject.Find("A05_ButtonExitSwing").GetComponent<HumanCannonPole>();
            Undo.RecordObject(exit.transform,"Aim toward retained next gallery");
            exit.transform.rotation=Quaternion.LookRotation(new Vector3(-4.5f,0,9));
            exit.PresentSwing(0);
            BLight(root,new Vector3(118,7,27),new Vector3(118,0,27),380,23);
            BLight(root,new Vector3(108,5,20),new Vector3(108,-5,25),420,25);

            var trampoline=coop.Trampoline;
            var td=new SerializedObject(trampoline);td.FindProperty("requireCarrierForBounce").boolValue=false;td.ApplyModifiedProperties();
            var bounds=cd.FindProperty("dropBounds").objectReferenceValue as BoxCollider;
            Undo.RecordObject(bounds,"Expand disappearance handoff area");bounds.center=new Vector3(102,1,25);bounds.size=new Vector3(28.6f,4,9);
            var lowerBounds=VerticalChild(root,"SingleWorkerBounds").gameObject.AddComponent<BoxCollider>();
            lowerBounds.isTrigger=true;lowerBounds.center=new Vector3(98.25f,-3.75f,31.5f);lowerBounds.size=new Vector3(36.5f,5.5f,25);
            var gate=BMass(root,"SingleWorkerEntryGate",new Vector3(82.5f,.75f,33.4f),new Vector3(3,3,.25f),wornSteel);
            foreach(float x in new[]{80.8f,84.2f})BMass(root,"하부 1인 입구 문틀",new Vector3(x,.75f,33.4f),new Vector3(.3f,3.3f,.5f),bRecess);
            BMass(root,"하부 1인 입구 상부보",new Vector3(82.5f,2.55f,33.4f),new Vector3(3.7f,.3f,.5f),bRecess);
            gate.SetActive(false);
            var returnPoint=VerticalChild(root,"OverflowReturnPoint");returnPoint.position=new Vector3(85,.05f,24);returnPoint.rotation=Quaternion.Euler(0,90,0);
            cd.Update();cd.FindProperty("lowerWorkerBounds").objectReferenceValue=lowerBounds;
            cd.FindProperty("lowerEntryGate").objectReferenceValue=gate;cd.FindProperty("overflowReturnPoint").objectReferenceValue=returnPoint;
            cd.ApplyModifiedProperties();
            var development=UnityEngine.Object.FindFirstObjectByType<EscapeDevelopmentSession>();
            Undo.RecordObject(development,"Temporarily suppress requested fall consequences");development.SuppressFallConsequences=true;
            EditorUtility.SetDirty(development);
            RegisterAllPoles();course.Rebuild();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return "Four timed wires 12m -> 28m; lower work deck and exit extended; original unheld trampoline bounce restored; single-worker gate connected; fall consequences disabled for A";
        }
    }
}
