using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapePursuerEntranceGaitBuilder
    {
        public static void Configure(EscapeUnderstageChase chase)
        {
            if (!chase) return;
            var actor = new SerializedObject(chase);
            var breach = actor.FindProperty("breach").objectReferenceValue as EscapeUnderstageBreach;
            if (!breach) return;
            var data = new SerializedObject(breach);
            var clip = data.FindProperty("entrance").objectReferenceValue as AnimationClip;
            if (!clip) throw new InvalidOperationException("Entrance motion is missing.");
            string path = AnimationUtility.CalculateTransformPath(chase.transform, breach.transform);
            AnimationCurve Curve(string axis) => AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalPosition."+axis));
            var x=Curve("x");var z=Curve("z");
            if (x==null && z==null) throw new InvalidOperationException("Entrance actor translation curves are missing: "+path);
            // Constant axes are omitted by the legacy clip; they contribute no travel.
            x ??= AnimationCurve.Constant(0,clip.length,chase.transform.localPosition.x);
            z ??= AnimationCurve.Constant(0,clip.length,chase.transform.localPosition.z);
            int count=Mathf.CeilToInt(clip.length*120);
            var travel=new AnimationCurve(); float distance=0;
            Vector3 previous=new Vector3(x.Evaluate(0),0,z.Evaluate(0));
            for(int i=0;i<=count;i++)
            {
                float t=clip.length*i/count;
                var point=new Vector3(x.Evaluate(t),0,z.Evaluate(t));
                distance+=breach.transform.TransformVector(point-previous).magnitude;
                travel.AddKey(t,distance);previous=point;
            }
            for(int i=0;i<travel.length;i++)
            {AnimationUtility.SetKeyLeftTangentMode(travel,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(travel,i,AnimationUtility.TangentMode.Linear);}
            data.FindProperty("entranceTravel").animationCurveValue=travel;
            data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(breach);
            if(PrefabUtility.IsPartOfPrefabInstance(breach)) PrefabUtility.RecordPrefabInstancePropertyModifications(breach);
        }

        public static void Apply()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Edit mode required.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene.");
            var prefab=PrefabUtility.LoadPrefabContents(EscapePursuerRevision7Builder.Prefab);
            try { foreach(var chase in prefab.GetComponentsInChildren<EscapeUnderstageChase>(true))Configure(chase); PrefabUtility.SaveAsPrefabAsset(prefab,EscapePursuerRevision7Builder.Prefab); }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            foreach (string path in new[] { EscapeBackgroundBuilder.LabyrinthScenePath, EscapeMppmReviewConfig.PursuerScene }
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path)))
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try{foreach(var chase in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EscapeUnderstageChase>(true)))Configure(chase);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();
        }
    }
}
