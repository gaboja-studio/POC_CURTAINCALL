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
        public static void RefineLabyrinthLaunchBays()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!=LabyrinthScenePath)throw new InvalidOperationException("Saved working prototype required.");
            labyrinthFrame=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            var existing=labyrinthFrame.Find("EscapeBackground/70_LabyrinthRemake");
            if(existing.Find("81_ParallelLaunchBays"))throw new InvalidOperationException("Parallel launch bays already authored.");
            Material M(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFactoryArt/factory-"+name+".mat")??throw new InvalidOperationException(name);
            bConcrete=M("concrete");bSteel=M("steel");bRecess=M("recess");wornSteel=bSteel;
            maintenancePaint=M("safety-ochre");bBulb=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Sounds/EscapeRouteMaterials/concrete-service-bulb.mat");
            labyrinthRoot=new GameObject("81_ParallelLaunchBays").transform;labyrinthRoot.SetParent(existing,false);
            labyrinthRopes.Clear();labyrinthGuides.Clear();ResetLabyrinthPoles();
            var route=labyrinthFrame.GetComponentInChildren<EscapePursuerRoute>(true);
            // Open a lower loading hall below the upper route. The rail's abyss stays empty.
            LOpen(route,new(92,-16.1f,5.25f),new(42,13.4f,11.5f));
            foreach(var t in existing.Cast<Transform>().Where(t=>t.name.StartsWith("Conveyor launch restored outer edge")).ToArray())RemovePlain(t);
            // The first apron remains the inspection arm's landing. Only its obsolete
            // front rail, facing the new loading hall, is removed to make a flush junction.
            foreach(var t in existing.Cast<Transform>().Where(t=>t.name.StartsWith("Conveyor arrival front rail")).ToArray())RemovePlain(t);
            var cannons=BuildConveyorParallelStations();
            float last=labyrinthFrame.InverseTransformPoint(cannons[3].transform.position).x;
            // The two sides of each tall process core are usable escape routes.
            LFloor("Parallel launch cross passage",new((71+last+4)*.5f,-22,7.5f),last+4-71,7);
            LBox("Parallel launch rear retaining wall",new((79+last+4)*.5f,-15,11.3f),new(last+4-79,14,.6f),bConcrete);
            LBox("Parallel launch far retaining wall",new(last+4.3f,-15,5.5f),new(.6f,14,11.6f),bConcrete);
            for(int i=1;i<4;i++)
            {
                float x=labyrinthFrame.InverseTransformPoint(cannons[i].transform.position).x;
                LFloor("Parallel launch supported berth "+i,new(x,-22,2),8,4);
                LGuard("Parallel berth west edge "+i,new(x-4,-22,0),new(x-4,-22,4));
                LGuard("Parallel berth east edge "+i,new(x+4,-22,0),new(x+4,-22,4));
                float previous=labyrinthFrame.InverseTransformPoint(cannons[i-1].transform.position).x;
                LGuard("Parallel cross passage void edge "+i,new(previous+4,-22,4),new(x-4,-22,4));
                LBox("Parallel process core "+i,new((previous+x)*.5f,-15,7.5f),new(2,14,2.5f),bConcrete);
                foreach(float dx in new[]{-3.2f,3.2f})LBox("Parallel berth deep support "+i,new(x+dx,-86,2),new(.9f,127.2f,1.2f),bConcrete);
                LLamp("Parallel cannon task light "+i,new(x,-15.5f,6.2f),new(x,-21,1),115);
                LGuide("Parallel boarding guide "+i,new(previous,-22,7.5f),new(x,-22,7.5f),8,8);
            }
            LBox("Parallel loading hall roof",new(92,-8,5.5f),new(42,1,12),bConcrete);
            // Join the existing landing without overlapping its top surface.
            LGuide("Parallel inspection junction",new(76,-22,14),new(76,-22,7.5f),8,8);
            RegisterReturnJunction(route);
            BuildFirstParallelCannons();
            CompleteParallelCirculation(route);
            FitParallelEncounterLandings();
            LRemoveDuplicateBoxes();
            Physics.SyncTransforms();UnderstageChaseNavigation.Bake();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        public static void FinishLabyrinthLaunchBays()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!=LabyrinthScenePath)throw new InvalidOperationException("Working prototype required.");
            labyrinthFrame=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            labyrinthRoot=labyrinthFrame.Find("EscapeBackground/70_LabyrinthRemake/81_ParallelLaunchBays");
            if(!labyrinthRoot)throw new InvalidOperationException("Parallel bays not built.");
            if(labyrinthRoot.parent.Find("82_IndependentTransferRoutes"))
                throw new InvalidOperationException("Independent transfer routes supersede these bays. Use FinishIndependentConveyorChoices.");
            bConcrete=AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFactoryArt/factory-concrete.mat");
            CompleteParallelCirculation(labyrinthFrame.GetComponentInChildren<EscapePursuerRoute>(true));
            FitParallelEncounterLandings();LRemoveDuplicateBoxes();
            Physics.SyncTransforms();UnderstageChaseNavigation.Bake();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        static void CompleteParallelCirculation(EscapePursuerRoute route)
        {
            const float right=112.142f;
            if(!labyrinthRoot.Find("Parallel rear passing aisle"))
            {
                LOpen(route,new((79+right+.8f)*.5f,-16.1f,12.3f),new(right+.8f-79,13.4f,2.6f));
                LFloor("Parallel rear passing aisle",new((79+right)*.5f,-22,12),right-79,2);
            }
            var rear=labyrinthRoot.Find("Parallel launch rear retaining wall");
            var local=labyrinthFrame.InverseTransformPoint(rear.position);local.z=13.3f;rear.position=LW(local);
            var end=labyrinthRoot.Find("Parallel launch far retaining wall");
            FitBox(end,new(right+.3f,-15,6.5f),new(.6f,14,13.6f));
            for(int i=1;i<4;i++)
            {
                var core=labyrinthRoot.Find("Parallel process core "+i);local=labyrinthFrame.InverseTransformPoint(core.position);local.z=8.5f;core.position=LW(local);
            }
            var roof=labyrinthRoot.Find("Parallel loading hall roof");FitBox(roof,new(92,-8,6.5f),new(42,1,14));
            var actor=new SerializedObject(labyrinthFrame.GetComponentInChildren<EscapeUnderstageChase>(true));
            var platform=(BoxCollider)actor.FindProperty("encounters").GetArrayElementAtIndex(1).FindPropertyRelative("platform").objectReferenceValue;
            // This is the existing public-floor search volume, never a physical floor.
            if(!platform.isTrigger)throw new InvalidOperationException("Expected pursuit search trigger.");
            platform.transform.position=LW(new((63+right)*.5f,-22,8.5f));
            platform.size=new(right-63,5,17);
        }

        static void FitParallelEncounterLandings()
        {
            var actor=new SerializedObject(labyrinthFrame.GetComponentInChildren<EscapeUnderstageChase>(true));
            var encounters=actor.FindProperty("encounters");
            // Keep the same wall exits and common jump rules. Land behind the cannon
            // row and beside the conveyor, clear of both chassis and moving crossbars.
            ((Transform)encounters.GetArrayElementAtIndex(0).FindPropertyRelative("landing").objectReferenceValue).position=LW(new(24.5f,0,76));
            ((Transform)encounters.GetArrayElementAtIndex(2).FindPropertyRelative("landing").objectReferenceValue).position=LW(new(34,-19.2f,-65));
        }

        static void BuildFirstParallelCannons()
        {
            var source=labyrinthFrame.GetComponentsInChildren<HumanCannonStation>(true).Single(s=>s.name=="03_Cannon");
            var stations=new HumanCannonStation[3];
            float[] z={71.5f,78.5f,82f},landing={44.8f,51.5f,55.5f};
            for(int i=0;i<stations.Length;i++)
            {
                var copy=Object.Instantiate(source.gameObject,source.transform.parent);copy.name="Labyrinth_01_ParallelEntryCannon_"+(i+2);
                copy.transform.SetPositionAndRotation(LW(new(29,0,z[i])),Quaternion.LookRotation(LW(new(landing[i],0,75))-LW(new(29,0,z[i]))));
                var station=copy.GetComponent<HumanCannonStation>();AimCompletionCannon(station,LW(new(landing[i],-6,75)),1.9f);
                RequireInternalTransforms(station,station.transform,"boardingPoint","seat","muzzle");stations[i]=station;
            }
            AppendParallelDevices(labyrinthFrame.GetComponentInChildren<HumanCannonSystem>(true),"stations",stations.Cast<Object>());
            // Extend only the existing solid launch platform, never below its landing wire.
            LFloor("Entry four cannon northern berth",new(27.25f,0,83.5f),9.5f,3);
            var platform=labyrinthFrame.Find("EscapeBackground/16_VerticalFacility/02_Route_0_to_minus6_to_plus2");
            foreach(var t in platform.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="1.1m 점검 난간").ToArray())
                if(labyrinthFrame.InverseTransformPoint(t.position).z>80)Object.DestroyImmediate(t.gameObject);
            LGuard("Entry four cannon north guard",new(22.5f,0,85),new(32,0,85));
            var bounds=labyrinthFrame.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Shared player floor pursuit 1").GetComponent<BoxCollider>();
            // Extend the existing public pursuit area to the new edge; no private path is exposed.
            bounds.transform.position=LW(new(27.25f,0,77.5f));bounds.size=new(9.5f,5,15);
        }
    }
}
