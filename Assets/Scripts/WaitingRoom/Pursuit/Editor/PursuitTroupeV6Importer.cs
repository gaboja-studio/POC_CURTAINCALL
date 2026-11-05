using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
 public static class PursuitTroupeV6Importer
 {
  public const string Root="Assets/_ThirdParty/curtaincall/Pursuit/TroupeV6";
  public const string Source="Assets/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/Source~/V6";
  [Serializable] public sealed class ModelData {
   public string name,sourceModel;public float[][] vertices,normals,colors,design,masks;
   public Dictionary<string,float>[] weights;public int[] triangles;public string[] joints;public int baseIndexCount;
  }
  static Vector3 V(float[] v)=>new Vector3(v[0],v[1],v[2]);
  static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
  [MenuItem("Tools/Curtain Call/Pursuit/Apply V6 troupe (preserve animations)")]
  public static void Build(){
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit play mode before applying art.");
   Folder(Root);
   var shader=Shader.Find("CurtainCall/Pursuit/TroupeV6");
   if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("V6 shader missing or has errors.");
   var materials=new List<Material>();
   foreach(var state in new[]{"Clean","Worn","Bloodied"}){
    string path=Root+"/Troupe "+state+".mat";
    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
    if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
    mat.shader=shader;mat.SetColor("_BaseColor",Color.white);
    mat.SetFloat("_WearAmount",state=="Clean"?.04f:state=="Worn"?.85f:.4f);
    mat.SetFloat("_BaseBlood",state=="Bloodied"?1:0);mat.SetFloat("_BloodVisible",1);
    EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);materials.Add(mat);
   }
   int index=0;
   foreach(string file in Directory.GetFiles(Source,"Pursuit_*-mesh.json").OrderBy(p=>p)){
    var data=JsonConvert.DeserializeObject<ModelData>(File.ReadAllText(file));
    string path="Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/"+data.sourceModel+".prefab";
    var go=PrefabUtility.LoadPrefabContents(path);
    try{
     var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
     if(skins.Length!=1)throw new InvalidOperationException("Expected one original skin: "+path);
     var skin=skins[0];var prior=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_ThirdParty/curtaincall/Pursuit/Reclaimed/"+data.sourceModel+" repaired shell.asset");
     if(prior==null)throw new InvalidOperationException("Original Unity mesh missing");
     var boneIndices=skin.bones.Select((b,i)=>new{b.name,i}).ToDictionary(x=>x.name,x=>x.i);
     var mesh=new Mesh{name=data.sourceModel+" V6",indexFormat=IndexFormat.UInt32};
     mesh.vertices=prior.vertices.Concat(data.vertices.Skip(data.baseIndexCount).Select(V)).ToArray();mesh.normals=prior.normals.Concat(data.normals.Skip(data.baseIndexCount).Select(V)).ToArray();
     mesh.colors=Enumerable.Repeat(new Color(.78f,.69f,.50f,1),prior.vertexCount).Concat(data.colors.Skip(data.baseIndexCount).Select(c=>new Color(c[0],c[1],c[2],c[3]))).ToArray();
     mesh.SetUVs(2,prior.vertices.Select(v=>new Vector3(-v.x*100,v.y*100,v.z*100)).Concat(data.design.Skip(data.baseIndexCount).Select(V)).ToList());
     mesh.SetUVs(3,prior.vertices.Select(v=>NearestMask(v,data)).Concat(data.masks.Skip(data.baseIndexCount).Select(c=>new Vector4(c[0],c[1],c[2],c[3]))).ToList());
     mesh.bindposes=prior.bindposes;
     mesh.boneWeights=prior.boneWeights.Concat(data.weights.Skip(data.baseIndexCount).Select(w=>{
      var list=w.OrderByDescending(kv=>kv.Value).ToArray();
      if(list.Length==0 || list.Length>4 || Math.Abs(list.Sum(kv=>kv.Value)-1)>.001)throw new InvalidOperationException("Invalid skin weights");
      var bw=new BoneWeight();
      if(list.Length>0){bw.boneIndex0=boneIndices[list[0].Key];bw.weight0=list[0].Value;}
      if(list.Length>1){bw.boneIndex1=boneIndices[list[1].Key];bw.weight1=list[1].Value;}
      if(list.Length>2){bw.boneIndex2=boneIndices[list[2].Key];bw.weight2=list[2].Value;}
      if(list.Length>3){bw.boneIndex3=boneIndices[list[3].Key];bw.weight3=list[3].Value;}
      return bw;
     })).ToArray();
     mesh.triangles=prior.triangles.Concat(data.triangles.Skip(data.baseIndexCount).Select(i=>i-data.baseIndexCount+prior.vertexCount)).ToArray();mesh.RecalculateBounds();
     string meshPath=Root+"/"+data.sourceModel+".asset";
     var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
     if(existing==null){AssetDatabase.CreateAsset(mesh,meshPath);existing=mesh;}
     else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);}
     skin.sharedMesh=existing;skin.sharedMaterials=new[]{materials[2]};
     // Keep original renderer, transform, bones, bindposes, rootBone, actor and all animation clips.
     // Conservative bounds are unioned over the *existing* motions, including both advanced clips.
     var actor=go.GetComponent<PursuitActor>();var appearance=go.GetComponent<PursuitAppearance>();
     var transforms=go.GetComponentsInChildren<Transform>(true);
     var positions=transforms.Select(t=>t.localPosition).ToArray();var rotations=transforms.Select(t=>t.localRotation).ToArray();
     var bounds=existing.bounds;var baked=new Mesh();
     try{
      foreach(var clip in actor.Clips.Concat(new[]{appearance.AdvancedWalk,appearance.AdvancedRun}).Where(c=>c!=null))
       for(int frame=0;frame<=16;frame++){
        clip.SampleAnimation(go,clip.length*frame/16f);skin.BakeMesh(baked,true);
        foreach(var p in baked.vertices)bounds.Encapsulate(p);
       }
     }finally{
      for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];}
      Object.DestroyImmediate(baked);
     }
     bounds.Expand(.0015f);skin.localBounds=bounds;
     foreach(var t in go.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Reclaimed_")).OrderByDescending(t=>Depth(t)).ToArray())
      if(t!=null)Object.DestroyImmediate(t.gameObject);
     appearance.AdvancedDetails=Array.Empty<GameObject>();
     appearance.SurfaceState=PursuitSurfaceState.Bloodied;appearance.SurfaceSeed=++index;
     PrefabUtility.SaveAsPrefabAsset(go,path);
    }finally{PrefabUtility.UnloadPrefabContents(go);}
   }
   Debug.Log("V6 applied to "+index+" pursuit prefabs; existing clips and skeleton retained.");
  }
  static Vector4 NearestMask(Vector3 p,ModelData data){
   int best=0;float dist=float.PositiveInfinity;
   for(int i=0;i<data.baseIndexCount;i++){float d=(V(data.vertices[i])-p).sqrMagnitude;if(d<dist){dist=d;best=i;}}
   var c=data.masks[best];return new Vector4(c[0],c[1],c[2],c[3]);
  }
  static int Depth(Transform t){int n=0;while(t.parent!=null){n++;t=t.parent;}return n;}
 }
}