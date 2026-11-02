using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public static class PursuitMotionReview
    {
        const string Root="Assets/_ThirdParty/curtaincall/Pursuit";
        [Serializable] public class Sample { public string motion; public float time; public Vector3[] vertices; public Vector3[] joints; }
        [Serializable] public class Model { public string name; public int[] triangles; public string[] bones; public List<Sample> samples=new(); }
        [Serializable] public class Report { public List<Model> models=new(); public int clips; public float maxBoneLengthError; public float minimumKneeForward=float.MaxValue; }
        public static string LastReport;
        [MenuItem("Tools/Curtain Call/Pursuit/4 Export motion review")]
        public static void Export()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop play before review");
            var report=new Report();
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"})) {
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
                try {
                    root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    var actor=root.GetComponent<PursuitActor>();
                    var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                    var bones=skin.bones;
                    var bind=bones.ToDictionary(b=>b,b=>(b.localPosition,b.localRotation));
                    var edges=bones.Where(b=>b.parent!=null && bones.Contains(b.parent) && b.name!="Chest" && b.name!="Head" && !b.name.StartsWith("UpperArm") && !b.name.StartsWith("Thigh")).ToArray();
                    var lengths=edges.ToDictionary(b=>b,b=>Vector3.Distance(b.position,b.parent.position));
                    var model=new Model{name=root.name.Replace("(Clone)",""),triangles=skin.sharedMesh.triangles,bones=bones.Select(b=>b.name).ToArray()};
                    var mesh=new Mesh();
                    foreach(var clip in actor.Clips) {
                        for(int i=0;i<=60;i++) {
                            foreach(var pair in bind){pair.Key.localPosition=pair.Value.localPosition;pair.Key.localRotation=pair.Value.localRotation;}
                            float t=i/60f;
                            clip.SampleAnimation(root,t*clip.length);
                            foreach(var edge in edges) report.maxBoneLengthError=Mathf.Max(report.maxBoneLengthError,Mathf.Abs(Vector3.Distance(edge.position,edge.parent.position)-lengths[edge]));
                            if(clip.name!="Display" && clip.name!="Twitch" && clip.name!="Awaken") foreach(string side in new[]{"L","R"}) {
                                var hip=bones.First(b=>b.name=="Thigh_"+side).position; var knee=bones.First(b=>b.name=="Shin_"+side).position; var ankle=bones.First(b=>b.name=="Foot_"+side).position;
                                var axis=(ankle-hip).normalized;
                                report.minimumKneeForward=Mathf.Min(report.minimumKneeForward,Vector3.Dot(knee-hip-Vector3.Project(knee-hip,axis),Vector3.forward));
                            }
                            if(i%15!=0 || i==60) continue;
                            skin.BakeMesh(mesh);
                            // BakeMesh already incorporates renderer scale (FBX children use 100x).
                            model.samples.Add(new Sample{motion=clip.name,time=t*clip.length,vertices=mesh.vertices.Select(v=>skin.transform.position+skin.transform.rotation*v).ToArray(),joints=bones.Select(b=>b.position).ToArray()});
                        }
                        report.clips++;
                    }
                    UnityEngine.Object.DestroyImmediate(mesh);report.models.Add(model);
                } finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Directory.CreateDirectory(Root+"/Review");
            File.WriteAllText(Root+"/Review/unity-motion-samples.json",JsonUtility.ToJson(report));
            LastReport=$"models={report.models.Count}, clips={report.clips}, maxBoneLengthError={report.maxBoneLengthError:F6}m, minKneeForward={report.minimumKneeForward:F6}m";
            File.WriteAllText(Root+"/Review/motion-validation.txt",LastReport+Environment.NewLine);
            if(report.models.Count!=10 || report.clips!=70 || report.maxBoneLengthError>.002f || report.minimumKneeForward<0)
                throw new InvalidOperationException("Pursuit motion regression: "+LastReport);
            Debug.Log("[Pursuit review] "+LastReport);
        }
    }
}
