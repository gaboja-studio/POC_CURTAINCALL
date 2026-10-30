using System;
using System.Linq;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ReviseAPlayerRoute()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Saved A edit scene required");
            var bg = GameObject.Find(RootName).transform;
            if (bg.Find("41_AReadableRoute") != null) throw new InvalidOperationException("Revision already applied");
            CompletionMaterials();
            var root = VerticalChild(bg, "41_AReadableRoute");
            root.localScale = new Vector3(1/bg.lossyScale.x,1/bg.lossyScale.y,1/bg.lossyScale.z);
            RevisionExit(root, bg);
            RevisionShell(VerticalChild(root, "01_SealedFacilityAndReveal"), bg);
            RevisionConveyor(VerticalChild(root, "02_OneWayDisembark"), bg);
            RevisionCrossing(VerticalChild(root, "03_IslandRescueAndDescent"), bg);
            RegisterAllPoles();
            var hazards = UnityEngine.Object.FindObjectsByType<HumanCannonHazard>(FindObjectsSortMode.None).Where(h=>h.gameObject.scene==scene && h.isActiveAndEnabled).OrderBy(h=>h.name,StringComparer.Ordinal).ToArray();
            var hd = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<HumanCannonHazardCourse>());
            SetFinaleArray(hd,"hazards",hazards); hd.ApplyModifiedProperties();
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject=root.gameObject;
            return "A revised: horizontal twin saws/24 wires; pursuit 6s; sealed reveal; sequential conveyor; rescue islands; safe stair descent";
        }

        static void RevisionExit(Transform root, Transform bg)
        {
            var fire = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<EscapePursuitFire>());
            fire.FindProperty("stepSeconds").floatValue=6; fire.ApplyModifiedProperties();
            var main=bg.Find("40_AExitCableSaw").GetComponent<EscapeExitSaw>();
            var d=new SerializedObject(main);
            var banks=bg.Find("40_AExitCableSaw/ColorCableBanks");
            var originals=banks.Cast<Transform>().Where(t=>t.name.Contains("PowerBank")).ToArray();
            var positions=new[]{new Vector3(370,0,73),new Vector3(374,0,83),new Vector3(380,0,73),new Vector3(383,0,83),new Vector3(387,0,74),new Vector3(388,0,81)};
            var wires=new List<EscapeCuttableCable>();
            for(int i=0;i<6;i++)
            {
                var bank=i<3? originals[i]:UnityEngine.Object.Instantiate(originals[i%3].gameObject,banks).transform;
                bank.name=$"{i+1:00}_LongColorPowerBank";bank.position=positions[i];
                float height=i<4?8.5f+i*.35f:5.8f;
                foreach(var child in bank.Cast<Transform>().Where(t=>t.name.Contains("행거")||t.name.Contains("고정판")||t.name.Contains("접속구")).ToArray()) RemoveOpenObject(child);
                bank.Find("상부 전력 분배함").localPosition=new Vector3(.35f,height+.15f,0);
                bank.Find("상부 전력 분배함").localScale=new Vector3(.5f,.42f,3.8f);
                bank.Find("하부 전력 분배함").localScale=new Vector3(.5f,.4f,3.5f);
                var cables=bank.GetComponentsInChildren<EscapeCuttableCable>();
                for(int j=0;j<cables.Length;j++)
                {
                    var cd=new SerializedObject(cables[j]);var points=cd.FindProperty("localPoints");
                    float z=(j-1.5f)*.9f;
                    Vector3 a=new(.3f,height,z),b=new(-2.3f-j*.15f,height-1,z+.35f),c=new(-2.2f+j*.12f,.7f,z-.3f),e=new(.3f,.35f,z);
                    for(int k=0;k<points.arraySize;k++){float t=(float)k/(points.arraySize-1),u=1-t;points.GetArrayElementAtIndex(k).vector3Value=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*e;}
                    cd.ApplyModifiedProperties();cables[j].Rebuild();wires.Add(cables[j]);
                }
                foreach(float z in new[]{-1.65f,1.65f}) VerticalCable(bank,"분배함 상부 고정 케이블",new Vector3(.35f,height+.35f,z),new Vector3(.35f,i<4?15:6.5f,z),.045f,wornSteel);
                BLight(root,positions[i]+new Vector3(-2,4,0),positions[i]+Vector3.up*1.5f,220,12);
            }
            SetFinaleReferences(d.FindProperty("cables"),wires.ToArray());
            d.FindProperty("deviceId").intValue=0;
            var blade=(Transform)d.FindProperty("blade").objectReferenceValue;
            blade.localRotation=Quaternion.Euler(90,0,0);
            var template=(GameObject)d.FindProperty("projectileVisual").objectReferenceValue;
            template.transform.GetChild(0).localRotation=Quaternion.Euler(90,0,0);
            var hood=main.CarryRoot.GetComponentsInChildren<Transform>().First(t=>t.name=="톱날 상부 안전 덮개");
            hood.localRotation=Quaternion.identity;hood.localPosition=new Vector3(0,1.65f,.65f);
            main.CarryRoot.position=new Vector3(366.5f,0,75.2f);
            ((Transform)d.FindProperty("recoveryAnchor").objectReferenceValue).position=main.CarryRoot.position;
            d.ApplyModifiedProperties();
            var secondRoot=VerticalChild(root,"두 번째 휴대 절단기 시스템");
            var device=UnityEngine.Object.Instantiate(main.CarryRoot.gameObject,secondRoot).transform;
            device.name="휴대 절단기 2";
            var second=secondRoot.gameObject.AddComponent<EscapeExitSaw>();
            EditorUtility.CopySerialized(main,second);
            var sd=new SerializedObject(second);var it=sd.GetIterator();
            while(it.Next(true))
            {
                if(it.propertyType!=SerializedPropertyType.ObjectReference)continue;
                var value=it.objectReferenceValue;
                var original=value is Component component?component.transform:value is GameObject go?go.transform:null;
                if(original==null || !original.IsChildOf(main.CarryRoot))continue;
                string path=AnimationUtility.CalculateTransformPath(original,main.CarryRoot);
                var replacement=path.Length==0?device:device.Find(path);
                it.objectReferenceValue=value is GameObject?replacement.gameObject:replacement.GetComponent(value.GetType());
            }
            sd.FindProperty("deviceId").intValue=1;
            sd.FindProperty("doorPanel").objectReferenceValue=null;
            sd.FindProperty("indicator").objectReferenceValue=null;
            sd.FindProperty("sparks").objectReferenceValue=null;
            device.position=new Vector3(366.5f,0,80.8f);
            var recovery=VerticalChild(secondRoot,"절단기2 복귀 위치");recovery.SetPositionAndRotation(device.position,device.rotation);
            sd.FindProperty("recoveryAnchor").objectReferenceValue=recovery;sd.ApplyModifiedProperties();
        }

        static void RevisionShell(Transform p,Transform bg)
        {
            bg.Find("35_AOpenFacility/01_StartPressLesson").gameObject.SetActive(false);
            // A continuous outside shell catches every roof/room joint without closing interior sightlines.
            BMass(p,"연속 외피 서벽",new Vector3(-6,-45,17),new Vector3(4,214,222));
            BMass(p,"연속 외피 동벽",new Vector3(426,-45,17),new Vector3(4,214,222));
            BMass(p,"연속 외피 남벽",new Vector3(210,-45,-92),new Vector3(436,214,4));
            BMass(p,"연속 외피 북벽",new Vector3(210,-45,126),new Vector3(436,214,4));
            BMass(p,"연속 외피 지붕",new Vector3(210,64,17),new Vector3(436,4,222));
            BMass(p,"빛이 닿지 않는 심부",new Vector3(210,-154,17),new Vector3(436,4,222),bRecess);
            var hall=bg.Find("17_BrutalistRoute/01_Understage");
            foreach(var t in hall.Cast<Transform>().ToArray())
            {
                if(t.name=="타설 콘크리트 벽"&&t.position.x>30 || t.name=="무대 하중 교각"&&t.position.x>20) t.gameObject.SetActive(false);
            }
            BMass(p,"시작 전망창 하부 흉벽",new Vector3(31,-5.5f,11.25f),new Vector3(2,13,22.5f));
            BMass(p,"시작 전망창 상부 인방",new Vector3(31,11,11.25f),new Vector3(2,6,22.5f));
            BMass(p,"시작 전망창 후면 잼",new Vector3(31,4.5f,1),new Vector3(2,7,2));
            BMass(p,"시작 전망창 전면 잼",new Vector3(31,4.5f,21.5f),new Vector3(2,7,2));
            // The near corridor reveals the real descent and circulation machinery across an enclosed shaft.
            var slots=bg.Find("35_AOpenFacility/05_ObservationSlots");
            foreach(var t in slots.Cast<Transform>().Where(t=>t.name.StartsWith("진입로 다음 묘기 전망")).ToArray())t.gameObject.SetActive(false);
            BMass(p,"진입 전망 연속 하부벽",new Vector3(23.5f,-8.5f,42.5f),new Vector3(2,19,40));
            BMass(p,"진입 전망 연속 상부벽",new Vector3(23.5f,21,42.5f),new Vector3(2,22,40));
            foreach(float z in new[]{24f,42f,61f}) BMass(p,"전망을 나누는 시설 기둥",new Vector3(23.5f,5.5f,z),new Vector3(2,9,1.2f));
            var atrium=bg.Find("35_AOpenFacility/08_ObservationAtrium");
            // The distant cross wall also had a gap below its lid; overlap it with a proper coping.
            BMass(p,"관찰 공동 북측 상부 막음",new Vector3(40,46.5f,62),new Vector3(34,11,1.2f));
            BMass(p,"관찰 공동 남측 상부 막음",new Vector3(40,46.5f,5),new Vector3(34,11,2));
            for(int i=0;i<5;i++)
            {
                float z=11+i*10;
                BMass(p,"심부 수직 하중 기둥",new Vector3(50,-20,z),new Vector3(1.2f,130,1.2f),bRecess);
                BLight(p,new Vector3(48,15,z),new Vector3(48,-30,z),500,45);
            }
            BLight(p,new Vector3(28,6,13),new Vector3(44,-15,25),750,50);
            // Orient the initial view toward the reveal while preserving the north-going route.
            var sp=UnityEngine.Object.FindFirstObjectByType<CurtainCall.Network.PlayerSync.PlayerSpawnPoints>();
            var data=new SerializedObject(sp);var points=data.FindProperty("points");
            for(int i=0;i<points.arraySize;i++) ((Transform)points.GetArrayElementAtIndex(i).objectReferenceValue).rotation=Quaternion.Euler(0,35,0);
        }

        static void RevisionConveyor(Transform p,Transform bg)
        {
            var old=bg.Find("35_AOpenFacility/03_SpacedDisembarkRoute");
            var station=old.GetComponentInChildren<HumanCannonStation>();
            var hazards=old.GetComponentsInChildren<HumanCannonHazard>();
            // Preserve original mechanisms; retire the ambiguous split platforms and unnecessary transfer grips.
            foreach(var pole in old.GetComponentsInChildren<HumanCannonPole>())HideAFusedPole(bg,pole);
            Undo.SetTransformParent(station.transform,p,"Move return cannon after switch");
            station.transform.position=new Vector3(80,-18.5f,-36);
            FeedbackAim(station,new Vector3(85,0,16.5f),3.3f);
            int saw=0;
            foreach(var hazard in hazards)
            {
                var cue=hazard.GetComponent<HumanCannonHazardCue>();
                if(hazard.transform.parent.name.Contains("Press")) { hazard.transform.parent.gameObject.SetActive(false);continue; }
                float x=50+saw++*3;
                MoveAFusedHazard(hazard,p,new Vector3(x,-17.4f,-40),new Vector3(x,-17.4f,-32));
            }
            old.gameObject.SetActive(false);
            var fusion=bg.Find("37_ACooperativeFusion/04B_OpenSawAndCannonInterlock");fusion.gameObject.SetActive(false);
            AFloor(p,"한 곳으로 모이는 하차장",32.5f,44,-40,-32,-18.5f);
            AFloor(p,"톱 횡단 도착대",54,60,-38,-34,-18.5f);
            AFloor(p,"대포로 이어지는 정비교",60,76,-38,-34,-18.5f);
            AFloor(p,"다음 구간 발사대",76,86,-40,-32,-18.5f);
            var course=UnityEngine.Object.FindFirstObjectByType<TightropeCourse>();var cd=new SerializedObject(course);
            var registered=cd.FindProperty("sceneRopes");var ropes=new List<BoxCollider>();
            for(int i=0;i<registered.arraySize;i++){var c=registered.GetArrayElementAtIndex(i).objectReferenceValue as BoxCollider;if(c!=null&&!c.name.StartsWith("A04B_"))ropes.Add(c);}
            AFusionLandingRope(p,"A04B_OneWaySwitchWire",new Vector3(44,-18.5f,-36),new Vector3(54,-18.5f,-36),ropes);
            SetFinaleArray(cd,"sceneRopes",ropes.ToArray());
            var shape=cd.FindProperty("sceneShape");shape.FindPropertyRelative("laneCount").intValue=ropes.Count;
            var ends=shape.FindPropertyRelative("laneEndDistances");ends.arraySize=ropes.Count;for(int i=0;i<ropes.Count;i++)ends.GetArrayElementAtIndex(i).floatValue=ropes[i].size.z;
            cd.ApplyModifiedProperties();course.Rebuild();
            var locks=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<EscapeCooperativeLocks>());var entry=locks.FindProperty("checkpoints").GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("shoulderControl").objectReferenceValue=WorksControl(p,"목마로 누르는 대포 전원 스위치",new Vector3(46,-16.05f,-36));
            entry.FindPropertyRelative("arrival").objectReferenceValue=CoopArrival(p,"횡단 완료 감지",new Vector3(56,-18.05f,-36),new Vector3(3,1.3f,3));
            entry.FindPropertyRelative("indicator").objectReferenceValue=CoopLamp(p,"대포 전원 표시",new Vector3(46,-15.6f,-35.6f));
            entry.FindPropertyRelative("lockedStation").objectReferenceValue=station;
            entry.FindPropertyRelative("prompt").stringValue="대포 전원 켜기";
            entry.FindPropertyRelative("instruction").stringValue="전원 버튼에 F → 대포 가동 · 톱을 지나 대포로 이동합니다";
            var gate=BMass(p,"대포 진입 잠금문",new Vector3(73,-16.5f,-36),new Vector3(.4f,4,4),wornSteel);
            SetCoopShutters(entry,gate);
            locks.FindProperty("checkpoints").GetArrayElementAtIndex(1).FindPropertyRelative("instruction").stringValue="한 명이 F로 노란 봉을 잡고 대기 → 다른 사람이 대포를 타고 위 외줄에 착지 → 봉을 놓고 함께 올라갑니다";
            locks.ApplyModifiedProperties();
            BMass(p,"대포실 진입측 가림벽",new Vector3(72,-13,-30),new Vector3(10,14,1.2f));
            BMass(p,"대포 발사 개구 우측 벽",new Vector3(85.5f,-13,-30),new Vector3(3,14,1.2f));
            BMass(p,"대포 발사 개구 상부 보",new Vector3(80.5f,-5,-30),new Vector3(7,2,1.2f));
            BMass(p,"대포실 외측 가림벽",new Vector3(77,-13,-42),new Vector3(20,14,1.2f));
            foreach(float z in new[]{-40f,-32f})SpaceEdgeRail(p,new Vector3(33,-18.5f,z),new Vector3(44,-18.5f,z));
            foreach(float x in new[]{36f,40f,56f,64f,70f,78f})FusionFloorMarks(p,new Vector3(x,-18.47f,-36),Vector3.right);
            foreach(float x in new[]{36f,47f,56f,78f})BLight(p,new Vector3(x,-10,-34),new Vector3(x,-18.5f,-36),500,18);
            foreach(float x in new[]{50f,53f})foreach(float z in new[]{-40.8f,-31.2f})BMass(p,"톱 반출 회수함",new Vector3(x,-17.3f,z),new Vector3(1.8f,2.4f,1),bRecess);
        }

        static void RevisionCrossing(Transform p,Transform bg)
        {
            var expanded=bg.Find("36_ALongerCooperativeCrossing");
            expanded.Find("길어진 하부 협동 작업대").gameObject.SetActive(false);
            foreach(var t in expanded.Cast<Transform>().Where(t=>t.name.StartsWith("정비대")&&t.position.y<0).ToArray())t.gameObject.SetActive(false);
            // 1.0m gaps with broad receiving islands, within the original carrier jump envelope.
            for(int i=0;i<5;i++)
            {
                float x=87.5f+i*5.7f;
                AFloor(p,"운반 점프 작업섬 "+(i+1),x,Mathf.Min(x+4.7f,116),20,32,-5);
                FusionFloorMarks(p,new Vector3(x+3,-4.97f,26),Vector3.right);
                BMass(p,"작업섬 깊은 교각 "+i,new Vector3(x+2.35f,-52,26),new Vector3(1.1f,93,1.2f),bRecess);
            }
            // Last landing connects to the original locked return stair.
            AFloor(p,"작업섬 복귀 계단 접속",115,116.5f,20,32,-5);
            var tramp=UnityEngine.Object.FindFirstObjectByType<EscapeCooperativeRopes>().Trampoline;
            tramp.transform.position=new Vector3(89.8f,-4.92f,25);
            var fifth=bg.Find(AFusedGroup+"/05_CooperativeReboundAndExit");
            var exit=GameObject.Find("A05_ButtonExitSwing").GetComponent<HumanCannonPole>();HideAFusedPole(bg,exit);
            foreach(string name in new[]{"버튼 출구대 측면 이음","버튼에서 출구 그네로","하강 출구 착지턱"}){var t=fifth.Find(name);if(t!=null)t.gameObject.SetActive(false);}
            AFloor(p,"상부 협동 완료 합류대",116,125,29,35,0);
            var returnStair=bg.Find("32_A_CooperativeRopes/ButtonReturnStair");
            foreach(Transform t in returnStair)
                if(t.name=="상부 동료 합류 통로"||t.name=="타설 콘크리트 벽"&&t.position.x>119)t.gameObject.SetActive(false);
            // Return around the west side; a slab above the ascending flight blocks head clearance.
            AFloor(p,"복귀 계단에서 합류대로",113.5f,116,30,44,0);
            AFloor(p,"복귀 계단 상부 참",116,120,41,44,0);
            // Forty 20cm risers replace the injury-producing eight metre blind drop.
            for(int i=0;i<40;i++)
            {
                float y=-(i+1)*.2f,z=35+i*.35f;
                BMass(p,"화염 복도 연결 계단 "+i,new Vector3(122,y-.3f,z+.175f),new Vector3(5,.6f,.35f),bConcrete);
            }
            AFloor(p,"화염 복도 무단차 연결",119.5f,125,49,51,-8);
            foreach(float x in new[]{119.5f,124.5f})
                VerticalCable(p,"하강 계단 연속 난간",new Vector3(x,1.1f,35),new Vector3(x,-6.9f,49),.055f,wornSteel);
            BLight(p,new Vector3(122,5,34),new Vector3(122,-4,42),350,20);
            BLight(p,new Vector3(121,-2,48),new Vector3(122,-8,50),250,14);
        }
    }
}
