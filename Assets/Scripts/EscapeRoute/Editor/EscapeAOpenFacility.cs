using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Tightrope;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string OpenFacilityGroup = "35_AOpenFacility";
        static readonly List<string> openReport = new();

        public static string OpenAndCleanFacilityA()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("A edit scene required");
            var bg = GameObject.Find(RootName).transform;
            if (bg.Find(OpenFacilityGroup) != null) throw new InvalidOperationException("Already applied; preserve subsequent edits");
            openReport.Clear(); CompletionMaterials();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Open A facility and remove obsolete layout");
            var root = VerticalChild(bg, OpenFacilityGroup);
            root.localScale = new Vector3(1/bg.lossyScale.x,1/bg.lossyScale.y,1/bg.lossyScale.z);
            int restored = 0;
            foreach (var f in bg.GetComponentsInChildren<MeshFilter>())
                if (f.sharedMesh != null && f.sharedMesh.name.StartsWith("polished-")
                    && f.TryGetComponent<Renderer>(out var r) && r.bounds.size.y > 8)
                { RestoreSpaceSourceMesh(f, true); restored++; }
            openReport.Add("Restored obsolete cut wall meshes: " + restored);
            RestoreSpaceSourceMesh(bg.Find(VerticalGroup+"/02_Route_0_to_minus6_to_plus2/진입 캔틸레버").GetComponent<MeshFilter>(),true);
            RemoveReviewProps(bg);
            RestoreStart(root, bg);
            WidenConveyor(root, bg);
            OpenSightlines(root, bg);
            EncloseObservationAtrium(root);
            SetSteeredFirstCannon();
            RemoveUnusedSceneHistory(bg);
            RegisterAllPoles();
            var hazards = UnityEngine.Object.FindObjectsByType<HumanCannonHazard>(FindObjectsSortMode.None)
                .Where(h=>h.gameObject.scene==scene).OrderBy(h=>h.name,StringComparer.Ordinal).ToArray();
            var hazardData = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<HumanCannonHazardCourse>());
            SetFinaleArray(hazardData,"hazards",hazards); hazardData.ApplyModifiedProperties();
            UnityEngine.Object.FindFirstObjectByType<TightropeCourse>().Rebuild();
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
            return string.Join("\n",openReport);
        }

        static void RemoveOpenObject(Transform t)
        {
            if(t==null)return;
            if(PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
            {
                var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if(prefab!=null) PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            }
            Undo.DestroyObjectImmediate(t.gameObject);
        }

        static void RemoveReviewProps(Transform bg)
        {
            int labels=0,figures=0;
            foreach(var label in bg.GetComponentsInChildren<TMP_Text>(true))
                if(label!=null){RemoveOpenObject(label.transform);labels++;}
            foreach(var t in bg.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("ScaleReference_")
                || t.name=="13_PlayerScaleReferences" || t.name.StartsWith("현재 플레이어 모델")).ToArray())
                if(t!=null){RemoveOpenObject(t);figures++;}
            // These fixtures still marked the removed first landing island and former staircase.
            RemoveOpenObject(bg.Find("18_WorkingFacilityFinish/03_VerticalMaintenance"));
            openReport.Add($"Removed wall/review text: {labels}; blue scale-reference roots: {figures}");
        }

        static void RestoreStart(Transform root,Transform bg)
        {
            midWarning=RunnerMaterial("solo-danger-red");
            RemoveOpenObject(bg.Find(AFusedGroup+"/03_04A_CannonWireDescent/후방 폐쇄 정비문"));
            var p=VerticalChild(root,"01_StartPressLesson");
            var points=UnityEngine.Object.FindFirstObjectByType<PlayerSpawnPoints>();
            var data=new SerializedObject(points); var list=data.FindProperty("points");
            for(int i=0;i<list.arraySize;i++)
            {
                var t=list.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                Undo.RecordObject(t,"Return spawn to original start hall");
                t.SetPositionAndRotation(new Vector3(13.5f+i,.05f,5),Quaternion.identity);
            }
            // Original warning/movement/damage mechanics. The surrounding floor remains a recovery space.
            for(int i=0;i<2;i++)
            {
                float z=10+i*7;
                MidHazard(p,"StartPress_"+i,"05_DropLeft",new Vector3(15,7,z),new Vector3(15,1.3f,z),
                    new Vector3(5,2.2f,2),new Vector3(15,.025f,z),new Vector3(5,.02f,2.2f),1.8f,1.2f,2.8f,i*2.9f);
                foreach(float x in new[]{11.8f,18.2f}) BMass(p,"입문 압착기 가이드",new Vector3(x,3.5f,z),new Vector3(.45f,7,.6f),wornSteel);
                BLight(p,new Vector3(20,4,z),new Vector3(15,0,z),240,14);
            }
            openReport.Add("Original start reopened at Z5; two alternating original presses with 1.8s warning");
        }

        static void WidenConveyor(Transform root,Transform bg)
        {
            var old=bg.Find(AFusedGroup+"/04B_ConveyorCatch");
            var envelope=VerticalChild(root,"02_ExpandedPumpEnvelope");
            var exit=VerticalChild(root,"03_SpacedDisembarkRoute");
            var previousHall=bg.Find(DepthGroup+"/04B_PumpHall_Perimeter");
            // Retire the former envelope/core. All original mechanisms live in AFusedRoute, not here.
            foreach(Transform t in previousHall.Cast<Transform>().ToArray()) RemoveOpenObject(t);
            foreach(var t in old.Cast<Transform>().ToArray())
            {
                string n=t.name;
                if(n=="타설 콘크리트 벽" || n=="확장 집수정" || n=="확장 설비동 덮개"
                    || n=="거친 외벽 버트레스" || n=="설비동 수직 주관"
                    || n.StartsWith("폐회로 운반 레일") || n.Contains("레일 도색")
                    || n=="레일 고정 하중보" || n=="레일 수직 인장재") RemoveOpenObject(t);
            }
            // Existing west-wall attachments would otherwise float inside the expanded volume.
            var finish=bg.Find(DepthGroup+"/07_ServiceFinish");
            foreach(var t in finish.Cast<Transform>().ToArray())
                if(t.position.z<6 && t.position.x<63) RemoveOpenObject(t);
            BWallZ(envelope,20,-82,7,-86,42,3);
            BWallZ(envelope,146,-82,7,-86,42,4);
            BWallX(envelope,-82,20,146,-86,42,4);
            BWallX(envelope,7,20,58,-86,42,2);
            BFloor(envelope,"확장 설비동 심부 집수정",21.5f,144,-80,6,-86,3);
            BMass(envelope,"확장 설비동 천장",new Vector3(83,43.5f,-37.5f),new Vector3(128,3,91));
            BMass(envelope,"동측 원거리 펌프 코어",new Vector3(124,-28,-47),new Vector3(18,116,32),bConcrete);
            for(int i=0;i<4;i++)
            {
                float z=-10-i*20;
                BMass(envelope,"외곽 수직 버트레스",new Vector3(23,-22,z),new Vector3(2,120,3),bRecess);
                BLight(envelope,new Vector3(25,-8,z),new Vector3(43,-27,z),650,35);
            }
            BLight(envelope,new Vector3(110,12,-30),new Vector3(124,-30,-47),1600,70);
            // Extend west/south while keeping the north turn clear of the descent wall.
            var loops=old.GetComponentsInChildren<HumanCannonPoleLoop>();
            foreach(var loop in loops)
            {
                var d=new SerializedObject(loop); float phase=d.FindProperty("phase01").floatValue;
                Undo.RecordObject(loop.transform,"Widen circulation route"); Undo.RecordObject(loop,"Widen circulation route");
                loop.transform.position=new Vector3(51,-17,-32);
                loop.Configure(loop.Carriage,Vector3.zero,34,16,5,phase);
            }
            var rail=VerticalChild(root,"04_ExpandedOverheadRail");
            var model=loops[0]; double period=model.Length/5;
            for(int i=0;i<100;i++)
            {
                model.EvaluatePose(period*i/100,out var a,out _,out _,out _);
                model.EvaluatePose(period*(i+1)/100,out var b,out _,out _,out _);
                VerticalCable(rail,"연속 운반 레일 "+i,a+Vector3.up*4.7f,b+Vector3.up*4.7f,.35f,wornSteel);
            }
            foreach(var at in new[]{new Vector3(67,-12.3f,-18),new Vector3(35,-12.3f,-45),new Vector3(51,-12.3f,-65)})
            {
                BMass(rail,"레일 매립 하중보",at+Vector3.up,new Vector3(5,1,1),wornSteel);
                VerticalCable(rail,"레일 천장 인장재",at+Vector3.up,new Vector3(at.x,42,at.z),.12f,wornSteel);
            }
            Vector3 shift=new(-12,0,-24);
            foreach(var t in old.Cast<Transform>().ToArray())
            {
                string n=t.name;
                bool move=n.StartsWith("A04B_") && !n.Contains("ConveyorCannon") && !n.Contains("Circulating")
                    || n=="04_ReturnCannon" || n=="후속 하차 작업대" || n=="두 하차 경로 합류대"
                    || n=="압착기 통과와 복귀 대포대" || n=="외줄 고정 베이스"
                    || n.StartsWith("01_WestSaw") || n.StartsWith("02_EastSaw") || n.StartsWith("01_FallingPress")
                    || n.Contains("톱 회수함") || n.StartsWith("복귀 압착기") || n=="닳은 진행 도색";
                if(!move) continue;
                Undo.SetTransformParent(t,exit,"Separate disembark mechanics from capture");
                Undo.RecordObject(t,"Move complete device and mounting"); t.position+=shift;
                foreach(var cue in t.GetComponentsInChildren<HumanCannonHazardCue>(true))
                {Undo.RecordObject(cue,"Move authoritative hazard coordinates");cue.origin+=shift;cue.destination+=shift;EditorUtility.SetDirty(cue);}
            }
            // Old light fixtures pointed to the packed junction; replace their detached housings as one set.
            foreach(var t in old.Cast<Transform>().ToArray())
                if(t.name=="매입 정비등" || t.name=="작업등 함체" || t.name=="작업등 광원") RemoveOpenObject(t);
            foreach(var at in new[]{new Vector3(35,-23,-43),new Vector3(47,-23,-24),new Vector3(73,-22,-24)})
                BLight(exit,at+new Vector3(2,9,2),at,430,25);
            FeedbackAim(GameObject.Find("04_ReturnCannon").GetComponent<HumanCannonStation>(),new Vector3(85,0,16.5f),2.8f);
            FeedbackAim(GameObject.Find("A04B_ConveyorCannon").GetComponent<HumanCannonStation>(),new Vector3(67,-18.5f,-15),2.2f);
            FitStartEnvelopeJoin(envelope);
            RepairLoopCarriages();
            RelocateCoreServices(root,bg);
            openReport.Add($"Pump envelope 126m x 89m; loop {model.Length:F2}m / {period:F2}s; exit mechanics shifted (-12,0,-24)");
        }

        static void FitStartEnvelopeJoin(Transform envelope)
        {
            CompletionMaterials();
            foreach(var wall in envelope.Cast<Transform>().Where(t=>t.name=="타설 콘크리트 벽"
                && (Mathf.Abs(t.position.x-20)<.01f || Mathf.Abs(t.position.z-7)<.01f)).ToArray())
                RemoveOpenObject(wall);
            // The old start hall occupies X0..30/Z0..22.5: join below its floor, never through the room.
            BWallZ(envelope,20,-82,-2,-86,42,3);
            BWallZ(envelope,20,-2,7,-86,-2,3);
            BWallX(envelope,7,20,32,-86,-2,2);
            BWallX(envelope,7,32,58,-86,42,2);
        }

        static void RepairLoopCarriages()
        {
            CompletionMaterials();
            foreach(var loop in UnityEngine.Object.FindObjectsByType<HumanCannonPoleLoop>(FindObjectsSortMode.None))
            {
                var carriage=loop.Carriage;
                foreach(var t in carriage.Cast<Transform>().Where(t=>t.name.Contains("천장 매립 하중보")
                    || t.name.EndsWith("_LowerSuspensionBeam") || t.name.EndsWith("_FixedDropRod")
                    || t.name=="레일 운반 연결부").ToArray()) RemoveOpenObject(t);
                // BMass takes parent-local positions. A moving carriage must never use world-space mounting coordinates.
                var mount=VerticalChild(carriage,"레일 운반 연결부");
                BMass(mount,"손잡이 케이블 가로보",new Vector3(0,4.25f,0),new Vector3(3.6f,.35f,.5f),wornSteel,false);
                BMass(mount,"운반 레일 결합부",new Vector3(0,4.6f,0),new Vector3(.6f,.6f,.7f),wornSteel,false);
            }
            openReport.Add("Replaced six displaced moving ceiling mounts and 12 long drop rods with local rail carriages");
        }

        static void RelocateCoreServices(Transform root,Transform bg)
        {
            var destination=VerticalChild(root,"07_CoreServices");
            var finish=bg.Find(DepthGroup+"/07_ServiceFinish");
            foreach(var t in finish.Cast<Transform>().Where(t=>t.position.z<6).ToArray())
            {
                string n=t.name;
                if(n.StartsWith("코어 통풍구") || n.StartsWith("펌프 코어") || n.StartsWith("펌프 기계")
                    || n=="기존 산업 펌프 장치" || n=="현장 압력 계기" || n=="오래된 시설 배관" || n=="배관 플랜지 이음")
                {
                    Undo.SetTransformParent(t,destination,"Attach industrial equipment to relocated core");
                    Undo.RecordObject(t,"Keep new cannon path clear"); t.position+=new Vector3(37.2f,0,-19);
                }
                else RemoveOpenObject(t);
            }
            openReport.Add("Moved pump equipment onto distant core; removed floating old cable trays/lights from flight volume");
        }

        static void SetSteeredFirstCannon()
        {
            var station=GameObject.Find("03_Cannon").GetComponent<HumanCannonStation>();
            FeedbackAim(station,new Vector3(48,-6,69),1.9f);
            station.transform.Find("AuthoredLanding").position=new Vector3(48,-6,75);
            openReport.Add("A03 neutral landing Z69; intended wire Z75: +6m lateral air steering required");
        }

        static void OpenSightlines(Transform root,Transform bg)
        {
            var p=VerticalChild(root,"05_ObservationSlots");
            var gallery=bg.Find(BrutalGroup+"/02_CompressionGallery");
            var wall=gallery.Cast<Transform>().Single(t=>t.name=="타설 콘크리트 벽"&&t.position.x>20);
            CutObservation(p,wall,34,58,"진입로 다음 묘기 전망",new Vector2(.8f,3.4f));
            var shell=bg.Find(VerticalGroup+"/01_Shell_50x50_134m");
            CutObservation(p,shell.Find("동측 외벽"),69,91,"첫 대포 협동구간 전망",new Vector2(-4,3));
            var descent=bg.Find(AFusedGroup+"/03_04A_CannonWireDescent");
            wall=descent.Cast<Transform>().First(t=>t.name=="타설 콘크리트 벽"&&t.position.x<60);
            CutObservation(p,wall,18,50,"하강 공동 깊이 전망",new Vector2(-27,-5));
            openReport.Add("Three full-sized observation openings retained; no bars or invisible blockers");
        }

        static void CutObservation(Transform p,Transform wall,float along1,float along2,string name,params Vector2[] levels)
        {
            Physics.SyncTransforms();
            var bounds=wall.GetComponent<BoxCollider>().bounds; var mat=wall.GetComponent<Renderer>().sharedMaterial;
            BuildObservationPieces(p,bounds,mat,along1,along2,name,levels);
            RemoveOpenObject(wall);
        }

        static void BuildObservationPieces(Transform p,Bounds bounds,Material mat,float along1,float along2,string name,params Vector2[] levels)
        {
            const int alongAxis=2;
            float a=Mathf.Max(along1,bounds.min.z),b=Mathf.Min(along2,bounds.max.z);
            void Piece(string suffix,float l,float r,float bottom,float top)
            {
                if(r-l<.01f||top-bottom<.01f)return;
                Vector3 pos=bounds.center,size=bounds.size; pos[alongAxis]=(l+r)/2;pos.y=(bottom+top)/2;
                size[alongAxis]=r-l;size.y=top-bottom; BMass(p,name+suffix,pos,size,mat);
            }
            float bottom=bounds.min.y;
            foreach(var level in levels.OrderBy(v=>v.x))
            {
                if(b<=a||level.x<bottom||level.y>bounds.max.y||level.y<=level.x)
                    throw new InvalidOperationException("Opening outside wall "+name);
                Piece(" 수평 벽체",bounds.min.z,bounds.max.z,bottom,level.x);
                Piece(" 좌잼",bounds.min.z,a,level.x,level.y); Piece(" 우잼",b,bounds.max.z,level.x,level.y);
                bottom=level.y;
            }
            Piece(" 상부",bounds.min.z,bounds.max.z,bottom,bounds.max.y);
        }

        static void ReplaceObservationBars()
        {
            CompletionMaterials();
            var p=GameObject.Find(RootName+"/"+OpenFacilityGroup+"/05_ObservationSlots").transform;
            foreach(var child in p.Cast<Transform>().ToArray())RemoveOpenObject(child);
            BuildObservationPieces(p,new Bounds(new Vector3(23.5f,7,42.5f),new Vector3(2,50,40)),bConcrete,34,58,"진입로 다음 묘기 전망",new Vector2(.8f,3.4f));
            BuildObservationPieces(p,new Bounds(new Vector3(73.25f,-23,87.5f),new Vector3(1.5f,134,53)),bConcrete,69,91,"첫 대포 협동구간 전망",new Vector2(-4,3));
            BuildObservationPieces(p,new Bounds(new Vector3(56.5f,-25,34.25f),new Vector3(3,122,56.5f)),bConcrete,18,50,"하강 공동 깊이 전망",new Vector2(-27,-5));
        }

        static void EncloseObservationAtrium(Transform root)
        {
            if(root.Find("08_ObservationAtrium")!=null)return;
            CompletionMaterials();var p=VerticalChild(root,"08_ObservationAtrium");
            // Openings reveal one deep facility volume rather than the exterior sky under old wall fragments.
            BFloor(p,"관찰 공동 심부 바닥",24.5f,55,6,62,-90,2);
            BWallX(p,62,23.5f,56.5f,-92,42,1);
            BWallX(p,5,32,56.5f,-92,42,2);
            BWallX(p,5,23.5f,32,-92,-2,2);
            BWallZ(p,23.5f,6,62,-92,-18,2);
            BMass(p,"관찰 공동 상부 덮개",new Vector3(40,43.5f,34),new Vector3(35,3,60));
            BLight(p,new Vector3(39,-36,61),new Vector3(39,-88,33),650,65);
        }

        static void RemoveUnusedSceneHistory(Transform bg)
        {
            var referenced=new HashSet<Transform>();
            foreach(var component in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>()))
            {
                if(component==null || component is Transform || component is Behaviour behaviour && !behaviour.enabled)continue;
                var it=new SerializedObject(component).GetIterator();
                while(it.NextVisible(true))
                {
                    if(it.propertyType!=SerializedPropertyType.ObjectReference)continue;
                    var value=it.objectReferenceValue;
                    var target=value is GameObject go?go.transform:value is Component co?co.transform:null;
                    if(target==null||!target.IsChildOf(bg)||target.gameObject.activeInHierarchy)continue;
                    for(var t=target;t!=null&&t!=bg;t=t.parent)referenced.Add(t);
                }
            }
            int removed=0,kept=0;
            foreach(var t in bg.GetComponentsInChildren<Transform>(true).Where(t=>t!=bg&&!t.gameObject.activeSelf
                &&t.parent.gameObject.activeInHierarchy).ToArray())
            {
                if(t==null)continue;
                if(referenced.Contains(t)){kept++;continue;}
                RemoveOpenObject(t);removed++;
            }
            openReport.Add($"Removed inactive obsolete scene roots: {removed}; runtime-referenced inactive roots retained: {kept}");
        }
    }
}
