var report = new List<object>();
foreach(var p in new[]{"Assets/Resources/Prefabs/Objects/ETCs/HumanCannonArtKit/CannonStationVisual.prefab","Assets/Resources/Prefabs/Objects/ETCs/HumanCannonArtKit/RoundCarryTrampolineVisual.prefab"}) {
 var g=AssetDatabase.LoadAssetAtPath<GameObject>(p);
 report.Add(new {path=p, parts=g.GetComponentsInChildren<Transform>(true).Select(t=>new {t.name,pos=t.localPosition.ToString("F3"),scale=t.localScale.ToString("F3"),rot=t.localEulerAngles.ToString("F2"),components=t.GetComponents<Component>().Select(c=>c.GetType().Name).ToArray()}).ToArray()});
}
foreach(var p in new[]{"Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity","Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity"}) {
 var s=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(p);
 try {report.Add(new {scene=p,props=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.GetComponent<CurtainCall.HumanCannon.HumanCannonStation>()||t.GetComponent<CurtainCall.HumanCannon.HumanCannonTrampoline>()||t.name=="AppliedCannon"||t.name=="AppliedTrampoline").Select(t=>new {t.name,pos=t.position.ToString("F3"),scale=t.lossyScale.ToString("F3"),children=t.Cast<Transform>().Select(c=>c.name).ToArray(),data=t.GetComponents<MonoBehaviour>().Where(c=>c).Select(c=>EditorJsonUtility.ToJson(c)).ToArray()}).ToArray()});}
 finally {UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(s);}
}
var json=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("Tasks/Active/Task-20261030-082-human-cannon-prop-art/Source/baseline.json",json);
return json;

