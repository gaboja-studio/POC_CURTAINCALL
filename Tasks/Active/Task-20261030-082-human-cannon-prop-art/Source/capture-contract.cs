var paths=new[]{"Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity","Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity"};
var rows=new System.Collections.Generic.List<object>();
foreach(var path in paths) {
 var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
 foreach(var c in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))) {
  if(c==null || !(c is MonoBehaviour || c is Collider || c is Transform))continue;
  rows.Add(new {id=c.GetInstanceID(),type=c.GetType().FullName,path,data=EditorJsonUtility.ToJson(c)});
 }
}
System.IO.File.WriteAllText("Temp/cc-prop-contract-before.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows));
return rows.Count;

