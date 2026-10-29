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
        public static void BuildACooperativeRopes()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!=ScenePath)throw new InvalidOperationException("A edit scene required");
            CompletionMaterials(); var bg=GameObject.Find(RootName).transform;
            if(bg.Find("32_A_CooperativeRopes")!=null)throw new InvalidOperationException("Cooperative section already exists");
            var root=VerticalChild(bg,"32_A_CooperativeRopes");
            root.localScale=new Vector3(1/bg.lossyScale.x,1/bg.lossyScale.y,1/bg.lossyScale.z);
            var trampoline=UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>();
            Undo.SetTransformParent(trampoline.transform,root,"Reuse original carried trampoline");
            trampoline.transform.SetPositionAndRotation(new Vector3(94,-5,25),Quaternion.Euler(0,90,0));
            var td=new SerializedObject(trampoline);td.FindProperty("upwardSpeed").floatValue=12.5f;
            td.FindProperty("groundBounceAirControlSpeed").floatValue=7;
            td.FindProperty("requireCarrierForBounce").boolValue=true;td.ApplyModifiedProperties();
            PreserveFusionGuide(bg.Find(FusionGroup+"/05_RopeDropTrampolineSwing"));
            var oldEntry=bg.Find(BrutalGroup+"/05_TransferChasm/트램폴린 진입 대");
            PreserveFusionGuide(oldEntry);
            var course=UnityEngine.Object.FindAnyObjectByType<TightropeCourse>();var cd=new SerializedObject(course);
            var old=cd.FindProperty("sceneRopes"); var first=old.GetArrayElementAtIndex(0).objectReferenceValue as BoxCollider;
            for(int i=1;i<old.arraySize;i++)
                if(old.GetArrayElementAtIndex(i).objectReferenceValue is Collider collider){Undo.RecordObject(collider,"Replace single transfer rope");collider.enabled=false;}

            BFloor(root,"상부 진입 정비대",80,88,18,33,0,1.2f);
            Ensure<HumanCannonLandingZone>(root.Find("상부 진입 정비대").gameObject);
            BFloor(root,"하부 협동 작업대",87.5f,100,20,32,-5,1.2f);
            Ensure<HumanCannonLandingZone>(root.Find("하부 협동 작업대").gameObject);
            BFloor(root,"하부 접근 통로",85,88,31,44,-5,1.2f);
            BFloor(root,"하부 계단 끝 연결",80.5f,88,41.75f,44,-5,1.2f);
            for(int i=0;i<25;i++)BFloor(root,"동료 접근 계단_"+i,81,84,33+i*.35f,33+(i+1)*.35f,-(i+1)*.2f,.45f);
            BFloor(root,"상부 도착 정비대",100,105,20,31,0,1.2f);
            Ensure<HumanCannonLandingZone>(root.Find("상부 도착 정비대").gameObject);
            var ropes=VerticalChild(root,"FourOriginalRopes");var supports=new BoxCollider[4];
            for(int i=0;i<4;i++)supports[i]=CompletionRope(ropes,"TimedWire_"+i,new Vector3(88,0,22+i*2),new Vector3(100,0,22+i*2));
            var all=new Collider[5];all[0]=first;for(int i=0;i<4;i++)all[i+1]=supports[i];
            SetFinaleArray(cd,"sceneRopes",all);cd.FindProperty("independentSceneRopes").boolValue=true;
            var shape=cd.FindProperty("sceneShape");shape.FindPropertyRelative("laneCount").intValue=5;
            shape.FindPropertyRelative("laneSpacing").floatValue=2;
            var zones=shape.FindPropertyRelative("laneChangeZones");zones.arraySize=1;zones.GetArrayElementAtIndex(0).vector2Value=new Vector2(0,12);
            Ensure<RopeLaneJumpRules>(course.gameObject);
            var ends=shape.FindPropertyRelative("laneEndDistances");ends.arraySize=5;
            for(int i=0;i<5;i++)ends.GetArrayElementAtIndex(i).floatValue=i==0?first.size.z:12;
            shape.FindPropertyRelative("finishDistance").floatValue=Mathf.Max(first.size.z,12);cd.ApplyModifiedProperties();course.Rebuild();

            var recovery=VerticalChild(root,"ButtonReturnStair");
            BFloor(recovery,"잠긴 복귀 입구",99,104,30,32.2f,-5,1.2f);
            for(int i=0;i<25;i++)BFloor(recovery,"복귀 계단_"+i,100,104,32.2f+i*.35f,32.2f+(i+1)*.35f,-5+(i+1)*.2f,.45f);
            BFloor(recovery,"복귀 상단 참",100,106,40.95f,43.5f,0,1.2f);
            BFloor(recovery,"상부 동료 합류 통로",104,106,30,43.5f,0,1.2f);
            BWallZ(recovery,99.65f,31.8f,40.8f,-5,2.5f,.7f);
            BWallZ(recovery,104.35f,31.8f,40.8f,-5,2.5f,.7f);
            BMass(recovery,"ReturnGate",new Vector3(102,-3.25f,32.05f),new Vector3(4,3.5f,.3f),wornSteel);
            BMass(root,"ReturnButtonPedestal",new Vector3(102.5f,.5f,29.7f),new Vector3(.7f,1,.7f),bRecess);
            BMass(root,"ReturnButton",new Vector3(102.5f,1.15f,29.7f),new Vector3(.45f,.25f,.45f),maintenancePaint,false);
            var bounds=VerticalChild(root,"AuthoredDropBounds").gameObject.AddComponent<BoxCollider>();
            bounds.isTrigger=true;bounds.center=new Vector3(94,1,25);bounds.size=new Vector3(12.6f,4,9);
            var coop=root.gameObject.AddComponent<EscapeCooperativeRopes>();var data=new SerializedObject(coop);
            SetFinaleArray(data,"ropeSupports",supports);
            SetFinaleArray(data,"ropeVisuals",ropes.GetComponentsInChildren<Renderer>());
            data.FindProperty("button").objectReferenceValue=root.Find("ReturnButton");
            data.FindProperty("returnGate").objectReferenceValue=recovery.Find("ReturnGate").gameObject;
            data.FindProperty("switcher").objectReferenceValue=course.GetComponent<CourseControlSwitcher>();
            data.FindProperty("trampoline").objectReferenceValue=trampoline;
            data.FindProperty("dropBounds").objectReferenceValue=bounds;data.ApplyModifiedProperties();
            foreach(var pole in UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None).Where(p=>p.Grip.position.x>98&&p.Grip.position.x<118))
            {
                int n=pole.Grip.position.x<104?0:pole.Grip.position.x<110?1:2;
                foreach(var obsolete in bg.Find("31_FlowRefinement").Cast<Transform>().Where(t=>t.name.StartsWith(pole.name+"_",StringComparison.Ordinal)).ToArray())
                    Undo.DestroyObjectImmediate(obsolete.gameObject);
                TuneFlowPole(root,pole,new Vector3(106+n*5,1.8f-n*2.55f,28),5,38,7.2f);
            }
            CompletionDeck(root,"하강 그네 넓은 착지턱",117.5f,120,25.5f,30.5f,-8);
            foreach(float z in new[]{20f,32f})SpaceEdgeRail(root,new Vector3(87.5f,-5,z),new Vector3(100,-5,z));
            SpaceEdgeRail(root,new Vector3(100,0,20),new Vector3(105,0,20));
            SpaceEdgeRail(root,new Vector3(80,0,18),new Vector3(80,0,33));
            BLight(root,new Vector3(92,4,19),new Vector3(94,-5,25),450,20);
            BLight(root,new Vector3(105,4,30),new Vector3(102,0,29),320,16);
            FusionFloorMarks(root,new Vector3(86,.02f,25),Vector3.right);
            FusionFloorMarks(root,new Vector3(102,.02f,28),Vector3.right);
            RegisterAllPoles();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
