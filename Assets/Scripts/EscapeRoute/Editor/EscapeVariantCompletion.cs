using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        // Carry the reviewed A shell/systems into the existing variant asset, retaining its authored middle structure and scene GUID.
        static Transform BeginVariantCompletion(string id)
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Saved completed A required");
            string path = id == "B" ? VariantBPath : VariantCPath;
            var background = GameObject.Find(RootName).transform;
            var old = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var original = old.GetRootGameObjects().Single(r=>r.name==RootName).transform.Find(TraversalSpacePrefix+id);
                if (original == null) throw new InvalidOperationException("Existing variant structure missing");
                var copy = UnityEngine.Object.Instantiate(original.gameObject,background,false);
                copy.name=original.name;
            }
            finally { EditorSceneManager.ClosePreviewScene(old); }
            if (!EditorSceneManager.SaveScene(scene,path,false)) throw new InvalidOperationException("Could not save variant with existing GUID");
            CompletionMaterials();
            background.Find(TraversalSpacePrefix+id+"/00_TransferEntryJoints").gameObject.SetActive(false);
            var route = background.Find(VerticalGroup+"/02_Route_0_to_minus6_to_plus2");
            foreach(var filter in route.GetComponentsInChildren<MeshFilter>()) RestoreSpaceSourceMesh(filter,false);
            foreach(Transform child in route)
                if(child.name=="6m 아래 고립 정비 발판"||child.name=="고립 정비대 지지각"||child.name=="대포 설치 자리"||child.name=="외줄 연결 시각 표시"
                    ||(child.name=="발판 하중 보"||child.name=="1.1m 점검 난간")&&child.position.x>40&&child.position.x<55)
                    child.gameObject.SetActive(false);
            var group=VerticalChild(background,"29_"+id+"_OriginalMechanics");
            group.localScale=new Vector3(1/background.lossyScale.x,1/background.lossyScale.y,1/background.lossyScale.z);
            var trampoline=UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>();
            if(!trampoline.Handle.IsChildOf(trampoline.transform)) trampoline.Handle.SetParent(trampoline.transform,true);
            trampoline.transform.SetParent(group,true);
            background.Find(FusionGroup).gameObject.SetActive(false);
            foreach(var pole in UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None))
                if(pole.Grip.position.x>95 && pole.Grip.position.x<120) pole.transform.parent.gameObject.SetActive(false);
            var course=UnityEngine.Object.FindAnyObjectByType<TightropeCourse>();
            var ropes=new SerializedObject(course).FindProperty("sceneRopes");
            for(int i=0;i<ropes.arraySize;i++) ((Collider)ropes.GetArrayElementAtIndex(i).objectReferenceValue).enabled=false;
            return group;
        }

        static HumanCannonStation CompletionCannon(Transform parent,string name,Vector3 at,Vector3 target,float seconds)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefab);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent); go.name=name;
            var direction=Vector3.ProjectOnPlane(target-at,Vector3.up).normalized;
            go.transform.SetPositionAndRotation(at,Quaternion.LookRotation(direction));
            var station=go.GetComponentInChildren<HumanCannonStation>(); AimCompletionCannon(station,target,seconds);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform); return station;
        }
        static void AimCompletionCannon(HumanCannonStation station,Vector3 target,float seconds)
        {
            var data=new SerializedObject(station); Vector3 delta=target-station.Muzzle.position;
            data.FindProperty("forwardSpeed").floatValue=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude/seconds;
            data.FindProperty("upwardSpeed").floatValue=(delta.y+.5f*GameSettings.Base.Character.Gravity*seconds*seconds)/seconds;
            data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(station);
            var markers=station.transform.Cast<Transform>().Where(t=>t.name=="AuthoredLanding").ToArray();
            if(markers.Length==0) ChildAt(station.transform,"AuthoredLanding",target,Quaternion.identity);
            else
            {
                Undo.RecordObject(markers[0],"Update cannon landing reference");
                markers[0].SetPositionAndRotation(target,Quaternion.identity);
                foreach(var duplicate in markers.Skip(1)) Undo.DestroyObjectImmediate(duplicate.gameObject);
            }
        }
        static BoxCollider CompletionRope(Transform parent,string name,Vector3 from,Vector3 to)
        {
            var root=ChildAt(parent,name,from,Quaternion.LookRotation(to-from)); float length=Vector3.Distance(from,to);
            var support=root.gameObject.AddComponent<BoxCollider>();
            support.center=new Vector3(0,-GameSettings.Tightrope.Course.RopeThickness*.5f,length*.5f);
            support.size=new Vector3(GameSettings.Tightrope.Course.RopeWalkWidth,GameSettings.Tightrope.Course.RopeThickness,length);
            FusionRope(parent,name,from+Vector3.up*.05f,to+Vector3.up*.05f);
            FusionSocket(parent,from); FusionSocket(parent,to); return support;
        }
        static void FinishVariantCompletion(Transform group,BoxCollider[] supports)
        {
            var scene=SceneManager.GetActiveScene();
            var course=UnityEngine.Object.FindAnyObjectByType<TightropeCourse>(); var data=new SerializedObject(course);
            SetFinaleArray(data,"sceneRopes",supports); data.FindProperty("independentSceneRopes").boolValue=true;
            var shape=data.FindProperty("sceneShape"); shape.FindPropertyRelative("laneCount").intValue=supports.Length;
            shape.FindPropertyRelative("finishDistance").floatValue=supports.Max(s=>s.size.z);
            var ends=shape.FindPropertyRelative("laneEndDistances"); ends.arraySize=supports.Length;
            for(int i=0;i<supports.Length;i++) ends.GetArrayElementAtIndex(i).floatValue=supports[i].size.z;
            data.ApplyModifiedProperties(); course.Rebuild();
            var system=UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>(); data=new SerializedObject(system);
            SetFinaleArray(data,"stations",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HumanCannonStation>()).OrderBy(s=>s.name,StringComparer.Ordinal).ToArray());
            data.ApplyModifiedProperties(); RegisterAllPoles();
            // Every authored recovery terrace uses the same original safe-landing rule.
            foreach(var floor in GameObject.Find(RootName).transform.Find(TraversalSpacePrefix+(scene.path==VariantBPath?"B":"C")).GetComponentsInChildren<BoxCollider>())
                if(floor.size.y*floor.transform.lossyScale.y<2 && floor.bounds.size.x>2 && floor.bounds.size.z>2)
                    Ensure<HumanCannonLandingZone>(floor.gameObject);
            CompleteEscapePresentation(); Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void CompleteVariantB()
        {
            var group=BeginVariantCompletion("B"); var background=group.parent;
            var station=UnityEngine.Object.FindObjectsByType<HumanCannonStation>(FindObjectsSortMode.None).OrderBy(s=>s.transform.position.x).First();
            AimCompletionCannon(station,new Vector3(38,2,75),1.35f);
            CompletionCannon(group,"B03_UpperRelayCannon",new Vector3(46,5,78),new Vector3(56.5f,.5f,82),1.45f);
            CompletionCannon(group,"B05_TransferCannon",new Vector3(86,0,26),new Vector3(96.5f,2,27),1.4f);
            var trampoline=UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>(); trampoline.transform.position=new Vector3(96.5f,2,24.8f);
            var preview=EditorSceneManager.OpenPreviewScene(OriginalCannonPath);
            try
            {
                var source=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HumanCannonPole>(true)).First();
                OriginalPole(group,source,"B03_ClimbSwing",new Vector3(42.2f,5.3f,76.5f),new Vector3(9,0,3).normalized,20);
                OriginalPole(group,source,"B03_DescentSwing",new Vector3(60,2.5f,83),new Vector3(9,0,2).normalized,20);
                OriginalPole(group,source,"B05_ReboundSwing",new Vector3(101,6,26.5f),Vector3.right,20);
                OriginalPole(group,source,"B05_FinalSwing",new Vector3(115.5f,-2.8f,29),Vector3.right,14);
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            CompletionDeck(group,"외줄 도착 정비 참",111,114,31,33,-2.5f);
            for(int i=0;i<14;i++) BFloor(group,"하부 회복대 계단 "+i,112,114,31-(i+1)*.35f,31-i*.35f,-2.5f-(i+1)*.2f,.45f);
            CompletionDeck(group,"하부 정비대 연결",111,115,24.5f,28,-5.5f);
            var rope=CompletionRope(group,"B05_ServiceWire",new Vector3(107.5f,-2.5f,27.5f),new Vector3(112,-2.5f,32));
            FusionFloorMarks(group,new Vector3(40,2.02f,76),new Vector3(9,0,3));
            FusionFloorMarks(group,new Vector3(58,.52f,82),new Vector3(9,0,2));
            FusionFloorMarks(group,new Vector3(106,-2.48f,27),new Vector3(1,0,1));
            BLight(group,new Vector3(43,11,72),new Vector3(47,5,78),480,23);
            BLight(group,new Vector3(100,10,23),new Vector3(101,6,26.5f),460,23);
            FinishVariantCompletion(group,new[]{rope});
        }
    }
}
