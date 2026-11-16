using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;
public static class PolishInspect {
 const string Dir="C:/Unity/POC_CURTAINCALL/.harness/local/pursuer-polish-20261116/";
 static Transform Frame()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
 static string Path(Transform t)=>t.parent?Path(t.parent)+"/"+t.name:t.name;
 static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
 public static object Inventory(){if(Application.isPlaying)throw new Exception("Edit only");var f=Frame();var renderers=f.GetComponentsInChildren<MeshRenderer>(true);var data=renderers.Select(r=>{var mf=r.GetComponent<MeshFilter>(); var corners=Enumerable.Range(0,8).Select(i=>f.InverseTransformPoint(r.bounds.center+Vector3.Scale(r.bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))).ToArray();var b=new Bounds(corners[0],Vector3.zero);foreach(var p in corners)b.Encapsulate(p);return new{path=Path(r.transform),id=r.GetInstanceID(),active=r.gameObject.activeInHierarchy,enabled=r.enabled,min=V(b.min),max=V(b.max),mesh=mf&&mf.sharedMesh?AssetDatabase.GetAssetPath(mf.sharedMesh):null,vertices=mf&&mf.sharedMesh?mf.sharedMesh.vertexCount:0,materials=r.sharedMaterials.Select(m=>m?m.name:"MISSING").ToArray(),colliders=r.GetComponents<Collider>().Select(c=>new{type=c.GetType().Name,c.enabled,c.isTrigger}).ToArray(),behaviors=r.GetComponents<MonoBehaviour>().Select(c=>c?c.GetType().Name:"MISSING").ToArray()};}).ToArray();File.WriteAllText(Dir+"inventory.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));return new{renderers=data.Length,scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty};}
 public static object Guides(){var f=Frame();var route=f.GetComponentInChildren<CurtainCall.EscapeRoute.EscapePursuerRoute>();var d=new SerializedObject(route);var g=d.FindProperty("playerGuides");var rows=Enumerable.Range(0,g.arraySize).Select(i=>{var r=g.GetArrayElementAtIndex(i);var a=r.FindPropertyRelative("from").objectReferenceValue as Transform;var b=r.FindPropertyRelative("to").objectReferenceValue as Transform;return new{i,a=a?Path(a):null,b=b?Path(b):null,from=a?V(f.InverseTransformPoint(a.position)):null,to=b?V(f.InverseTransformPoint(b.position)):null,p=r.FindPropertyRelative("fromProgress").floatValue,q=r.FindPropertyRelative("toProgress").floatValue};}).ToArray();File.WriteAllText(Dir+"guides.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));return rows.Length;}
 public static object Capture(){var f=Frame();var views=new[]{
 ("descent-entry",new Vector3(121,1.65f,32),new Vector3(125,-2,40)),
 ("descent-middle",new Vector3(123,-2.3f,39),new Vector3(128,-5,45)),
 ("fire-entry",new Vector3(122.5f,-6.35f,49),new Vector3(122.5f,-6,68)),
 ("descent-overview",new Vector3(137,7,27),new Vector3(124,-3,44)),
 ("heat-side",new Vector3(135,2,49),new Vector3(120,1,61)),
 ("heat-remainders-east",new Vector3(151,4,50),new Vector3(140,6,68)),
 ("heat-remainders-close",new Vector3(146,5,58),new Vector3(141,7,73)),
 ("transfer-overview",new Vector3(192,8,39),new Vector3(201,1,62)),
 ("final-entrance",new Vector3(337,1.65f,78),new Vector3(380,5,78)),
 ("final-south",new Vector3(375,1.65f,64),new Vector3(397,5,78)),
 ("final-north",new Vector3(375,1.65f,92),new Vector3(400,5,78)),
 ("upper-face",new Vector3(113,8,21),new Vector3(124,4,46))};
 foreach(var v in views)Render(f,v.Item1,v.Item2,v.Item3);return views.Select(v=>v.Item1).ToArray();}
 static void Render(Transform f,string name,Vector3 eye,Vector3 target){var go=new GameObject("Temporary polish view"){hideFlags=HideFlags.HideAndDontSave};var c=go.AddComponent<Camera>();c.enabled=false;c.transform.SetPositionAndRotation(f.TransformPoint(eye),Quaternion.LookRotation(f.TransformDirection(target-eye)));c.fieldOfView=72;c.nearClipPlane=.08f;c.farClipPlane=600;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.black;c.GetUniversalAdditionalCameraData().renderPostProcessing=true;var rt=RenderTexture.GetTemporary(1200,800,24);var old=RenderTexture.active;Texture2D tex=null;try{RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();File.WriteAllBytes(Dir+name+".png",tex.EncodeToPNG());}finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);if(tex)UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);}}
}
