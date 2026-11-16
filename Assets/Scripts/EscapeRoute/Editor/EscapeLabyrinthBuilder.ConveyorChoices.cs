using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static void BuildIndependentConveyorChoices()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!=LabyrinthScenePath)throw new InvalidOperationException("Working prototype required.");
            labyrinthFrame=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            var existing=labyrinthFrame.Find("EscapeBackground/70_LabyrinthRemake");
            if(existing.Find("82_IndependentTransferRoutes"))throw new InvalidOperationException("Independent routes already built.");
            Material M(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFactoryArt/factory-"+name+".mat");
            bConcrete=M("concrete");bSteel=M("steel");bRecess=M("recess");wornSteel=bSteel;
            maintenancePaint=M("safety-ochre");bBulb=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Sounds/EscapeRouteMaterials/concrete-service-bulb.mat");
            RemoveSupersededParallelDevices();
            labyrinthRoot=existing.Find("81_ParallelLaunchBays");
            FitIndependentChoiceHall();
            RepairEntryCannonAims();
            labyrinthRoot=new GameObject("82_IndependentTransferRoutes").transform;labyrinthRoot.SetParent(existing,false);
            labyrinthRopes.Clear();labyrinthGuides.Clear();ResetLabyrinthPoles();
            var route=labyrinthFrame.GetComponentInChildren<EscapePursuerRoute>(true);
            // B descends underneath the moving line. It can be entered without a cannon
            // and never uses a timed carriage, boarding queue or custom movement rule.
            for(int i=0;i<12;i++)
            {
                float top=-22-(i+1)/6f,z=4-(i+.5f)/3f;
                LBox("Inspection access stair "+i,new(80.714f,top-.25f,z),new(4,.5f,1f/3f),bConcrete);
            }
            LGuard("Inspection stair west guard",new(78.714f,-22,4),new(78.714f,-24,0));
            LGuard("Inspection stair east guard",new(82.714f,-22,4),new(82.714f,-24,0));
            LRope("Inspection lower access cable",new(80.714f,-24,0),new(80.714f,-24,-5));
            LFloor("Inspection lower swing departure",new(80.714f,-24,-7.35f),4,4.7f);
            foreach(float x in new[]{79.214f,82.214f})LBox("Inspection stair landing deep support",new(x,-88.4f,-7.35f),new(.8f,127.2f,.8f),bConcrete);
            var points=new[]{new Vector3(80,-24.5f,-12),new Vector3(76,-23,-20),new Vector3(72,-21.5f,-28),
                new Vector3(68,-20,-36),new Vector3(64,-18.5f,-43),new Vector3(60,-17.7f,-49),new Vector3(56,-17.7f,-54.5f)};
            for(int i=0;i<points.Length;i++)
            {
                var target=i+1<points.Length?points[i+1]:new Vector3(52,-19.2f,-60.5f);
                LOpen(route,points[i]+Vector3.up*2,new(8,9,8));
                LPole("Labyrinth lower inspection swing "+(i+1),points[i],target-points[i]);
                LGuide("Lower inspection route "+i,points[i]-Vector3.up*1.5f,target-Vector3.up*1.5f,Mathf.Lerp(8,12,(float)i/points.Length),Mathf.Lerp(8,12,(float)(i+1)/points.Length));
            }
            LFloor("Inspection receiving island",new(52,-19.2f,-60.5f),6,8);
            LRope("Inspection receiver joining cable",new(49,-19.2f,-63),new(44,-19.2f,-63));
            LGuide("Inspection receiver merge",new(52,-19.2f,-60.5f),new(38,-19.2f,-65),12,13);
            LLamp("Inspection receiver task light",new(50,-13.2f,-58),new(51,-19.2f,-62),100);

            // C launches over the moving line to its own supported process balcony.
            // Its fixed bars and last cable meet B only on an ordinary walking island.
            var cannon=labyrinthFrame.GetComponentsInChildren<HumanCannonStation>(true).Single(s=>s.name=="Labyrinth_03_ProcessingLaunch");
            var targetLanding=new Vector3(77,-19.2f,-43);
            cannon.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(LW(targetLanding)-cannon.transform.position,Vector3.up));
            AimCompletionCannon(cannon,LW(targetLanding),4.5f);
            var velocity=cannon.LaunchVelocity;var origin=cannon.Muzzle.position;
            for(int i=0;i<=30;i++)
            {
                float t=4.5f*i/30;var at=origin+velocity*t+Vector3.down*(CurtainCall.Settings.GameSettings.Base.Character.Gravity*.5f*t*t);
                LOpen(route,labyrinthFrame.InverseTransformPoint(at)+Vector3.up*1.2f,new(3.2f,3.6f,3.2f));
            }
            LFloor("Processing launch receiving balcony",targetLanding,8,8);
            LPole("Labyrinth processing swing 1",new(72,-17.7f,-49),new(-5,0,-4.5f));
            LPole("Labyrinth processing swing 2",new(67,-17.7f,-53.5f),new(-5,0,-7));
            LFloor("Processing receiving island",new(62,-19.2f,-60.5f),5.5f,8);
            LRope("Processing receiver joining cable",new(59.25f,-19.2f,-62.5f),new(55,-19.2f,-62.5f));
            LGuide("Processing launch route",labyrinthFrame.InverseTransformPoint(cannon.transform.position),targetLanding,8,11);
            LGuide("Processing first swing route",targetLanding,new(67,-19.2f,-53.5f),11,12);
            LGuide("Processing second swing route",new(67,-19.2f,-53.5f),new(52,-19.2f,-60.5f),12,12.5f);
            LLamp("Processing receiving balcony task light",new(79.8f,-12.7f,-40.2f),targetLanding,110);
            LLamp("Processing last island task light",new(64,-13.2f,-63.5f),new(61,-19.2f,-62),100);
            foreach(var pair in new[]{(p:new Vector3(77,-19.2f,-43),w:6f),(p:new Vector3(52,-19.2f,-60.5f),w:4f),(p:new Vector3(62,-19.2f,-60.5f),w:3.5f)})
                foreach(float sign in new[]{-1f,1f})LBox("Independent balcony deep support",new(pair.p.x+sign*pair.w*.5f,-85.6f,pair.p.z),new(.8f,132,.8f),bConcrete);
            AddIndependentPressureEncounter(route);
            RegisterReturnJunction(route);RegisterLabyrinthPoles();LCarveNewDrops();LCarvePoleDrops();LRemoveDuplicateBoxes();
            TrimLabyrinthForkFloorSeams();
            RefineLabyrinthTransferFront();
            GroundLabyrinthExtractionPlate();
            AddLabyrinthBoardingEncounter();
            Physics.SyncTransforms();UnderstageChaseNavigation.Bake();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        public static void FinishIndependentConveyorChoices()
        {
            var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.path!=LabyrinthScenePath)throw new InvalidOperationException();
            labyrinthFrame=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            var existing=labyrinthFrame.Find("EscapeBackground/70_LabyrinthRemake");
            labyrinthRoot=existing.Find("81_ParallelLaunchBays");FitIndependentChoiceHall();RepairEntryCannonAims();
            FitDirectReturnLanding(existing.Find("80_ReturnSortingJunction"));
            labyrinthRoot=existing.Find("82_IndependentTransferRoutes");
            var route=labyrinthFrame.GetComponentInChildren<EscapePursuerRoute>(true);
            bConcrete=AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFactoryArt/factory-concrete.mat");
            foreach(var fixture in labyrinthRoot.Cast<Transform>().Where(t=>t.name.StartsWith("Processing last island task light")))
            {
                var p=labyrinthFrame.InverseTransformPoint(fixture.position);p.z=-63.5f;fixture.position=LW(p);
                if(fixture.GetComponent<Light>())fixture.rotation=Quaternion.LookRotation(LW(new(61,-19.2f,-62))-fixture.position);
            }
            LOpen(route,new(80.714f,-22.7f,1.6f),new(4.6f,5.6f,5.2f));
            LCutNamedBox("Concrete remainder",new Bounds(new(80.714f,-23,-2.5f),new(4.6f,4.2f,5)));
            labyrinthRopes.Clear();labyrinthRopes.AddRange(labyrinthRoot.GetComponentsInChildren<BoxCollider>(true).Where(b=>b.name.EndsWith("cable")));
            LCarveNewDrops();LCarvePoleDrops();AddIndependentPressureEncounter(route);LRemoveDuplicateBoxes();
            TrimLabyrinthForkFloorSeams();
            RefineLabyrinthTransferFront();
            GroundLabyrinthExtractionPlate();
            AddLabyrinthBoardingEncounter();
            var routeData=new SerializedObject(route);var guides=routeData.FindProperty("playerGuides");
            for(int i=guides.arraySize-1;i>=0;i--)
            {
                var row=guides.GetArrayElementAtIndex(i);
                if(!row.FindPropertyRelative("from").objectReferenceValue && !row.FindPropertyRelative("to").objectReferenceValue)
                    guides.DeleteArrayElementAtIndex(i);
            }
            routeData.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();UnderstageChaseNavigation.Bake();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        static void AddIndependentPressureEncounter(EscapePursuerRoute route)
        {
            var data=new SerializedObject(route.GetComponentInChildren<EscapeUnderstageChase>(true));var rows=data.FindProperty("encounters");
            var source=rows.GetArrayElementAtIndex(1);
            Vector3 P(string key)=>labyrinthFrame.InverseTransformPoint(((Transform)source.FindPropertyRelative(key).objectReferenceValue).position);
            var landing=new Vector3(78.5f,-19.2f,-45.5f);
            if(!labyrinthRoot.Find("Processing transfer landing"))
                LBranchEncounter(route,"가공동 도약 · 고정 봉 분기 추격",source.FindPropertyRelative("gate").intValue,source.FindPropertyRelative("routePoint").intValue,14,
                    (Transform)source.FindPropertyRelative("ingress").objectReferenceValue,P("staging"),P("approach"),P("exit"),
                    landing,new(77,-19.2f,-43),new(8,5,8),"Processing transfer");
            else
            {
                var row=Enumerable.Range(0,rows.arraySize).Select(rows.GetArrayElementAtIndex)
                    .Single(r=>((Transform)r.FindPropertyRelative("landing").objectReferenceValue).name=="Processing transfer landing");
                foreach(string key in new[]{"gate","routePoint"})row.FindPropertyRelative(key).intValue=source.FindPropertyRelative(key).intValue;
                row.FindPropertyRelative("ingress").objectReferenceValue=source.FindPropertyRelative("ingress").objectReferenceValue;
                foreach(string key in new[]{"staging","approach","exit"})((Transform)row.FindPropertyRelative(key).objectReferenceValue).position=LW(P(key));
                ((Transform)row.FindPropertyRelative("landing").objectReferenceValue).position=LW(landing);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            // Approach from the northern broken bay above the conveyor. The west bay's
            // long diagonal intersects the active transport rail and pole suspension.
            // Trim only obsolete concrete returns along the existing common jump arc.
            var start=P("exit");float arc=EscapeUnderstageChase.ServiceJumpArc(LW(start),LW(landing));
            for(int i=0;i<=160;i++)
            {
                float t=i/160f;var feet=Vector3.Lerp(start,landing,t)+Vector3.up*(arc*t*(1-t));
                LCutNamedBox("Concrete remainder",new Bounds(feet+Vector3.up*2.5f,new(3.2f,5,3.2f)));
            }
        }

        static void RemoveSupersededParallelDevices()
        {
            var loops=labyrinthFrame.GetComponentsInChildren<HumanCannonPoleLoop>(true).Where(l=>l.name.StartsWith("Labyrinth_03_ParallelConveyorLoop_")).ToArray();
            var doomedPoles=loops.SelectMany(l=>l.GetComponentsInChildren<HumanCannonPole>(true)).Cast<Object>().ToArray();
            var stations=labyrinthFrame.GetComponentsInChildren<HumanCannonStation>(true);
            var remove=stations.Where(s=>s.name=="Labyrinth_03_ParallelConveyorCannon_2"||s.name=="Labyrinth_03_ParallelConveyorCannon_4").ToArray();
            void Remove(Object system,string field,Object[] doomed)
            {
                var d=new SerializedObject(system);var array=d.FindProperty(field);
                for(int i=array.arraySize-1;i>=0;i--)if(doomed.Contains(array.GetArrayElementAtIndex(i).objectReferenceValue)){array.GetArrayElementAtIndex(i).objectReferenceValue=null;array.DeleteArrayElementAtIndex(i);}
                d.ApplyModifiedPropertiesWithoutUndo();
            }
            Remove(labyrinthFrame.GetComponentInChildren<HumanCannonSystem>(true),"stations",remove.Cast<Object>().ToArray());
            Remove(labyrinthFrame.GetComponentInChildren<HumanCannonPoleSystem>(true),"poles",doomedPoles);
            foreach(var s in remove)Object.DestroyImmediate(s.gameObject);
            foreach(var l in loops)Object.DestroyImmediate(l.gameObject);
            stations.Single(s=>s&&s.name=="Labyrinth_03_ParallelConveyorCannon_3").name="Labyrinth_03_ProcessingLaunch";
        }

        static void FitIndependentChoiceHall()
        {
            foreach(var t in labyrinthRoot.Cast<Transform>().Where(t=>t.name.Contains("berth")&&t.name.EndsWith(" 3")||t.name.StartsWith("Parallel berth west edge 3")||t.name.StartsWith("Parallel berth east edge 3")||t.name.StartsWith("Parallel cross passage void edge 3")||t.name.StartsWith("Parallel cannon task light 3")||t.name=="Parallel process core 3"||t.name.StartsWith("Parallel boarding guide 3")||t.name=="Parallel launch supported berth 1"||t.name.StartsWith("Parallel berth west edge 1")||t.name.StartsWith("Parallel berth east edge 1")).ToArray())
                Object.DestroyImmediate(t.gameObject);
            FitBox(labyrinthRoot.Find("Parallel launch cross passage"),new((71+98.428f)*.5f,-22.4f,7.5f),new(98.428f-71,.8f,7));
            var rear=labyrinthRoot.Find("Parallel rear passing aisle");if(rear)FitBox(rear,new((79+98.428f)*.5f,-22.4f,11.4f),new(98.428f-79,.8f,.8f));
            FitBox(labyrinthRoot.Find("Parallel launch rear retaining wall"),new((79+98.428f)*.5f,-15,12.1f),new(98.428f-79,14,.6f));
            FitBox(labyrinthRoot.Find("Parallel launch far retaining wall"),new(98.728f,-15,5.65f),new(.6f,14,12.3f));
            FitBox(labyrinthRoot.Find("Parallel loading hall roof"),new((71+99)*.5f,-8,5.65f),new(28,1,12.3f));
            for(int i=1;i<3;i++){var core=labyrinthRoot.Find("Parallel process core "+i);var p=labyrinthFrame.InverseTransformPoint(core.position);p.z=8;FitBox(core,p,new(2,14,1));}
            var data=new SerializedObject(labyrinthFrame.GetComponentInChildren<EscapeUnderstageChase>(true));
            var platform=(BoxCollider)data.FindProperty("encounters").GetArrayElementAtIndex(1).FindPropertyRelative("platform").objectReferenceValue;
            platform.transform.position=LW(new((63+98.428f)*.5f,-22,8.5f));platform.size=new(98.428f-63,5,17);
        }

        static void RepairEntryCannonAims()
        {
            var original=labyrinthFrame.GetComponentsInChildren<HumanCannonStation>(true).Single(s=>s.name=="03_Cannon");
            // The original launch velocity already lands here; repair its stale editor
            // marker as well so future placement audits use the actual first wire.
            AimCompletionCannon(original,LW(new(47.5f,-6,75)),1.9f);
            float[] landing={44.8f,51.5f,55.5f};
            var additional=new HumanCannonStation[3];
            for(int i=0;i<3;i++)
            {
                var station=labyrinthFrame.GetComponentsInChildren<HumanCannonStation>(true).Single(s=>s.name=="Labyrinth_01_ParallelEntryCannon_"+(i+2));
                additional[i]=station;
                var target=LW(new(landing[i],-6,75));station.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(target-station.transform.position,Vector3.up));
                AimCompletionCannon(station,target,1.9f);
            }
            foreach(var component in labyrinthFrame.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c=>c is EscapePursuerRoute || c is UnderstageCannonBypass))
            {
                var data=new SerializedObject(component);var entries=data.FindProperty("additionalFirstCannons");
                entries.arraySize=additional.Length;
                for(int i=0;i<additional.Length;i++)entries.GetArrayElementAtIndex(i).objectReferenceValue=additional[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void FitDirectReturnLanding(Transform junction)
        {
            FitBox(junction.Find("Return direct cable departure"),new(66.75f,-18.9f,-74),new(5.5f,.8f,4));
            SupportReturnIslands(junction);
            const string name="Labyrinth Return direct cable";
            var rope=junction.Find(name).GetComponent<BoxCollider>();
            var from=new Vector3(69.5f,-18.5f,-74);var to=new Vector3(71,-18.5f,-74);float length=Vector3.Distance(from,to);
            rope.transform.position=LW(from);var size=rope.size;size.z=length;rope.size=size;
            var center=rope.center;center.z=length*.5f;rope.center=center;
            for(int i=0;i<12;i++)
            {
                Vector3 P(float t)=>Vector3.Lerp(from,to,t)+Vector3.up*(.05f-.08f*Mathf.Sin(t*Mathf.PI));
                var a=P(i/12f);var b=P((i+1)/12f);var art=junction.Find(name+" cable "+i);
                art.SetPositionAndRotation(LW((a+b)*.5f),Quaternion.FromToRotation(Vector3.up,LW(b)-LW(a)));
                art.localScale=new(.075f,Vector3.Distance(a,b)*.5f,.075f);
            }
            foreach(var socket in junction.Cast<Transform>().Where(t=>t.name==name+" tension socket"))
                if(labyrinthFrame.InverseTransformPoint(socket.position).x<70)socket.position=LW(from+Vector3.up*.045f);
            var course=labyrinthFrame.GetComponentInChildren<CurtainCall.Tightrope.TightropeCourse>(true);
            var data=new SerializedObject(course);var ropes=data.FindProperty("sceneRopes");var ends=data.FindProperty("sceneShape").FindPropertyRelative("laneEndDistances");
            for(int i=0;i<ropes.arraySize;i++)if(ropes.GetArrayElementAtIndex(i).objectReferenceValue==rope)ends.GetArrayElementAtIndex(i).floatValue=length;
            data.ApplyModifiedPropertiesWithoutUndo();course.Rebuild();
        }
    }
}
