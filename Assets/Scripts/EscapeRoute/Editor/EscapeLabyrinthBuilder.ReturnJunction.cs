using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static void RefineLabyrinthReturnJunction()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying || scene.isDirty || scene.path!=LabyrinthScenePath)
                throw new InvalidOperationException("Open the saved working Labyrinth scene in Edit mode.");
            labyrinthFrame=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            var background=labyrinthFrame.Find("EscapeBackground");
            var existing=background.Find(LabyrinthRoot);
            if(existing.Find("80_ReturnSortingJunction"))throw new InvalidOperationException("Return junction already authored.");
            Material Factory(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFactoryArt/factory-"+name+".mat")
                ?? throw new InvalidOperationException("Missing factory material "+name);
            bConcrete=Factory("concrete");bSteel=Factory("steel");bRecess=Factory("recess");wornSteel=bSteel;
            maintenancePaint=Factory("safety-ochre");bBulb=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Sounds/EscapeRouteMaterials/concrete-service-bulb.mat");
            labyrinthRoot=existing;
            FitConveyorArrivalBalcony(background);

            // A supported sorting canopy gives the lower route a distinct enclosed bay.
            // It is above every player swing and outside the return cannon's ascent.
            LBox("Return sorting load-bearing canopy",new(62.2f,-6,-70.5f),new(21.4f,1,18),bConcrete);
            foreach(float z in new[]{-78.8f,-62.2f})
                LBox("Return sorting west deep abutment",new(52,-79.25f,z),new(1,146.5f,1.2f),bConcrete);
            LBox("Return sorting east deep abutment",new(72.4f,-79.25f,-62.2f),new(1,146.5f,1.2f),bConcrete);

            labyrinthRoot=new GameObject("80_ReturnSortingJunction").transform;labyrinthRoot.SetParent(existing,false);
            labyrinthRopes.Clear();labyrinthGuides.Clear();ResetLabyrinthPoles();
            var quay=background.Find("41_AReadableRoute/02_OneWayDisembark");
            FitBox(quay.Find("톱 횡단 도착대"),new(56,-18.9f,-74),new(4,.8f,4));
            FitBox(quay.Find("대포로 이어지는 정비교"),new(73.5f,-18.9f,-74),new(5,.8f,4));

            // Both choices use one common pole and one common rope, in opposite order.
            LRope("Labyrinth Return north cable",new(56,-18.5f,-72),new(56,-18.5f,-68));
            LFloor("Return north swing departure",new(56,-18.5f,-66.5f),4,3);
            LPole("Labyrinth Return north swing",new(61.5f,-17,-66.5f),Vector3.right);
            LFloor("Return north receiving balcony",new(68.75f,-18.5f,-66.5f),7.5f,4.5f);
            LFloor("Return north approach to original gate",new(71,-18.5f,-70.375f),3,3.25f);
            LPole("Labyrinth Return direct swing",new(61.5f,-17,-74),Vector3.right);
            LFloor("Return direct cable departure",new(66.75f,-18.5f,-74),5.5f,4);
            LRope("Labyrinth Return direct cable",new(69.5f,-18.5f,-74),new(71,-18.5f,-74));

            // The original powered gate remains the only approach to the return cannon.
            LBox("Return cannon closed northern process wall",new(73.1f,-15,-66.8f),new(.6f,7,10.4f),bConcrete);
            LGuard("Return north balcony front rail",new(65,-18.5f,-64.25f),new(72.5f,-18.5f,-64.25f));
            LGuard("Return north balcony rear rail",new(65,-18.5f,-68.75f),new(69.5f,-18.5f,-68.75f));
            LGuard("Return north inner approach rail",new(69.5f,-18.5f,-68.75f),new(69.5f,-18.5f,-72));
            LGuard("Return north corner rail",new(69.5f,-18.5f,-72),new(71,-18.5f,-72));
            LLamp("Return gate wall task lamp",new(72.7f,-12.5f,-69),new(71.7f,-18.5f,-73),105);

            var guards=background.Find("43_AConveyorRecovery/명확한 승하차 마감");
            foreach(var t in guards.Cast<Transform>().Where(t=>t.name.StartsWith("정비교 ")).ToArray())RemovePlain(t);
            LGuard("Return retained gate south rail",new(71,-18.5f,-76),new(76,-18.5f,-76));
            LGuard("Return retained gate north rail",new(73.4f,-18.5f,-72),new(76,-18.5f,-72));
            ReplaceLegacyWireFunnel(quay);
            foreach(var t in quay.Cast<Transform>().Where(t=>t.name=="정비 권상기 지지기둥").ToArray())RemovePlain(t);
            var duplicate=guards.Find("발사대 끝 난간 지주 4");if(duplicate)RemovePlain(duplicate);

            var route=labyrinthFrame.GetComponentInChildren<EscapePursuerRoute>(true);
            float from=route.Project(LW(new(56,-18.5f,-74)),out _),to=route.Project(LW(new(74,-18.5f,-74)),out _);
            LGuide("Return north rope",new(56,-18.5f,-74),new(56,-18.5f,-66.5f),from,Mathf.Lerp(from,to,.25f));
            LGuide("Return north swing",new(56,-18.5f,-66.5f),new(68.75f,-18.5f,-66.5f),Mathf.Lerp(from,to,.25f),Mathf.Lerp(from,to,.7f));
            LGuide("Return north turn",new(68.75f,-18.5f,-66.5f),new(71,-18.5f,-73),Mathf.Lerp(from,to,.7f),to);
            LGuide("Return direct swing",new(56,-18.5f,-74),new(65.5f,-18.5f,-74),from,Mathf.Lerp(from,to,.6f));
            LGuide("Return direct rope",new(65.5f,-18.5f,-74),new(74,-18.5f,-74),Mathf.Lerp(from,to,.6f),to);
            RegisterReturnJunction(route);RegisterLabyrinthPoles();
            SupportReturnIslands(labyrinthRoot);
            var junctionRoot=labyrinthRoot;labyrinthRoot=existing;LRemoveDuplicateBoxes();labyrinthRoot=junctionRoot;
            // Only this patch's stationary architecture is eligible. Existing story
            // machinery can be animated by an external controller without a component.
            foreach(var t in junctionRoot.GetComponentsInChildren<Transform>(true))
                if(t.GetComponent<MeshRenderer>() && t.childCount==0 && t.GetComponents<MonoBehaviour>().Length==0
                    && !t.GetComponentInParent<HumanCannonPole>() && !t.GetComponentInParent<HumanCannonStation>()
                    && !t.GetComponentsInParent<Transform>().TakeWhile(p=>p!=existing).Any(p=>p.GetComponents<MonoBehaviour>().Length>0))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,GameObjectUtility.GetStaticEditorFlags(t.gameObject)|StaticEditorFlags.BatchingStatic);
            EscapePursuerEntranceGaitBuilder.Configure(route.GetComponentInChildren<EscapeUnderstageChase>(true));
            Physics.SyncTransforms();UnderstageChaseNavigation.Bake();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        static void FitBox(Transform t,Vector3 at,Vector3 size)
        {
            if(!t || !t.GetComponent<BoxCollider>())throw new InvalidOperationException("Expected authored box: "+(t?t.name:"missing"));
            t.SetPositionAndRotation(LW(at),labyrinthFrame.rotation);
            var scale=t.parent.lossyScale;t.localScale=new(size.x/scale.x,size.y/scale.y,size.z/scale.z);
            var box=t.GetComponent<BoxCollider>();box.center=Vector3.zero;box.size=Vector3.one;
        }
        static void SupportReturnIslands(Transform junction)
        {
            var prior=labyrinthRoot;labyrinthRoot=junction;
            try
            {
                var material=junction.Find("Return direct cable departure").GetComponent<MeshRenderer>().sharedMaterial;
                foreach(var item in new[]{(name:"Return direct island deep pier",x:66.75f,z:-74f),
                    (name:"Return north departure deep pier",x:56f,z:-66.5f),
                    (name:"Return north receiver deep pier",x:68.75f,z:-66.5f)})
                    if(!junction.Find(item.name))LBox(item.name,new(item.x,-85.65f,item.z),new(1.2f,132.7f,1.2f),material);
            }
            finally{labyrinthRoot=prior;}
        }
        static void RemovePlain(Transform t)
        {
            if(t.childCount>0 || t.GetComponents<MonoBehaviour>().Length>0)throw new InvalidOperationException("Refusing to delete gameplay or hierarchy: "+t.name);
            Object.DestroyImmediate(t.gameObject);
        }
        static void FitConveyorArrivalBalcony(Transform background)
        {
            FitBox(labyrinthRoot.Find("03 conveyor eastern decision apron"),new(75,-22.4f,14),new(8,.8f,6));
            foreach(var t in labyrinthRoot.Cast<Transform>().Where(t=>t.name.StartsWith("Conveyor side apron")).ToArray())RemovePlain(t);
            foreach(var t in labyrinthRoot.Cast<Transform>().Where(t=>t.name.StartsWith("Concrete remainder fitted return")).ToArray())
            {
                var p=labyrinthFrame.InverseTransformPoint(t.position);
                if(p.x>75&&p.x<78&&p.y<-22&&p.z>10&&p.z<23)RemovePlain(t);
            }
            LGuard("Conveyor arrival front rail",new(71,-22,11),new(79,-22,11));
            LGuard("Conveyor arrival east rail",new(79,-22,11),new(79,-22,17));
            LGuard("Conveyor arrival rear rail",new(71,-22,17),new(79,-22,17));
            LGuard("Conveyor launch restored outer edge",new(71,-22,.5f),new(71,-22,11));
            var light=labyrinthRoot.Find("Conveyor observation worklight");
            if(light){light.position=LW(new(78.6f,-16.5f,16));light.rotation=Quaternion.LookRotation(LW(new(74,-22,14))-light.position);}
            var mount=labyrinthRoot.Find("Conveyor observation worklight fixed wall mount");
            if(mount)mount.position=LW(new(78.6f,-16.25f,16));
            var upright=labyrinthRoot.Find("Conveyor observation worklight fixed steel upright");
            if(upright)FitBox(upright,new(78.6f,-19.25f,16),new(.14f,5.5f,.14f));
        }
        static void ReplaceLegacyWireFunnel(Transform quay)
        {
            var junction=labyrinthFrame.Find("Pole feedback architecture/Conveyor wire junction");
            foreach(var t in junction.Cast<Transform>().Where(t=>t.name=="Wire entry steel guide").ToArray())
            {
                var box=t.GetComponent<BoxCollider>();var center=box.bounds.center;
                float length=t.TransformVector(Vector3.forward*box.size.z).magnitude;
                Vector3 a=center-t.forward*(length*.5f),b=center+t.forward*(length*.5f);a.y=b.y=LW(new(0,-18.5f,0)).y;
                LGuard("Return wire open handrail",labyrinthFrame.InverseTransformPoint(a),labyrinthFrame.InverseTransformPoint(b));
                RemovePlain(t);
            }
            var marking=junction.Find("Wire entry floor marking");if(marking)RemovePlain(marking);
        }
        static void RegisterReturnJunction(EscapePursuerRoute route)
        {
            var course=labyrinthFrame.GetComponentInChildren<TightropeCourse>(true);var data=new SerializedObject(course);
            var ropes=data.FindProperty("sceneRopes");int count=ropes.arraySize;ropes.arraySize+=labyrinthRopes.Count;
            var ends=data.FindProperty("sceneShape").FindPropertyRelative("laneEndDistances");ends.arraySize=ropes.arraySize;
            for(int i=0;i<labyrinthRopes.Count;i++){ropes.GetArrayElementAtIndex(count+i).objectReferenceValue=labyrinthRopes[i];ends.GetArrayElementAtIndex(count+i).floatValue=labyrinthRopes[i].size.z;}
            data.FindProperty("sceneShape").FindPropertyRelative("laneCount").intValue=ropes.arraySize;data.ApplyModifiedPropertiesWithoutUndo();course.Rebuild();
            data=new SerializedObject(route);var guides=data.FindProperty("playerGuides");count=guides.arraySize;guides.arraySize+=labyrinthGuides.Count;
            for(int i=0;i<labyrinthGuides.Count;i++){var row=guides.GetArrayElementAtIndex(count+i);var g=labyrinthGuides[i];row.FindPropertyRelative("from").objectReferenceValue=g.from;row.FindPropertyRelative("to").objectReferenceValue=g.to;row.FindPropertyRelative("fromProgress").floatValue=g.fromProgress;row.FindPropertyRelative("toProgress").floatValue=g.toProgress;}
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
