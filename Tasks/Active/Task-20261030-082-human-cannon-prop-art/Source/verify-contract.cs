var baseline=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Temp/cc-prop-contract-before.json"));
var errors=new System.Collections.Generic.List<string>(); int checkedCount=0;
foreach(var row in baseline) {
 var c=EditorUtility.InstanceIDToObject((int)row["id"]) as Component;
 if(c==null){errors.Add("Missing "+row["type"]+" #"+row["id"]);continue;}
 if(c is Transform) {
  // Added visual children alter the serialized child list, but original transforms must remain identical.
  var old=Newtonsoft.Json.Linq.JObject.Parse((string)row["data"]) .Properties().First().Value;
  var now=Newtonsoft.Json.Linq.JObject.Parse(EditorJsonUtility.ToJson(c)) .Properties().First().Value;
  foreach(var key in new[]{"m_LocalPosition","m_LocalRotation","m_LocalScale","m_Father"})
   if(!Newtonsoft.Json.Linq.JToken.DeepEquals(old[key],now[key]))errors.Add("Transform changed: "+c.name+" "+key);
 } else if((string)row["data"]!=EditorJsonUtility.ToJson(c)) errors.Add("Component changed: "+c.name+" "+c.GetType().Name);
 checkedCount++;
}
var s=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity");
var roots=s.GetRootGameObjects();
var cannons=roots.SelectMany(g=>g.GetComponentsInChildren<CurtainCall.HumanCannon.HumanCannonStation>(true)).ToArray();
foreach(var c in cannons){
 var art=c.transform.Find("CC_PropArt");
 if(!art||art.GetComponentsInChildren<Renderer>().Count(r=>r.enabled)!=1) errors.Add(c.name+" visual renderers");
 if(art.GetComponentsInChildren<Collider>().Length!=0)errors.Add(c.name+" extra collider");
 var vs=art.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>c.transform.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
 if(Mathf.Abs(vs.Min(v=>v.y))>.005f || Mathf.Abs(vs.Max(v=>v.x)-vs.Min(v=>v.x)-1.92f)>.01f)errors.Add(c.name+" floor/width mismatch");
}
var t=roots.SelectMany(g=>g.GetComponentsInChildren<CurtainCall.HumanCannon.HumanCannonTrampoline>(true)).Single();
var grip=t.Handle.Find("CC_GripArt");
if(!grip||!grip.GetComponent<Renderer>().enabled)errors.Add("Visible handle missing");
var fabric=t.transform.Find("CC_PropArt").GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Fabric");
var fv=fabric.sharedMesh.vertices.Select(v=>t.transform.InverseTransformPoint(fabric.transform.TransformPoint(v))).ToArray();
if(Mathf.Abs(fv.Max(v=>v.x)-fv.Min(v=>v.x)-4)>.001f||Mathf.Abs(fv.Min(v=>v.y)-.47f)>.001f||fv.Max(v=>v.y)>.511f)errors.Add("Fabric contract mismatch");
if(t.transform.Find("CC_PropArt").GetComponentsInChildren<Collider>().Length!=0)errors.Add("Extra trampoline collider");
var result=new {checkedCount,cannons=cannons.Length,trampolines=1,errors,success=errors.Count==0};
var json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("Tasks/Active/Task-20261030-082-human-cannon-prop-art/Source/contract-verification.json",json);return json;



