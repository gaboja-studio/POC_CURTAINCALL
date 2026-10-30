var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CurtainCall.HumanCannon.EditorTools.CannonBarrelArtBuilder.PrefabPath);
var settings = AssetDatabase.LoadAssetAtPath<CurtainCall.Settings.HumanCannonSettings>("Assets/Resources/Settings/GameSettings/Tricks/HumanCannonSettings.asset");
var config = new SerializedObject(settings); config.FindProperty("cannonCooldown").floatValue=5; config.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
var report=new System.Collections.Generic.List<string>();
foreach (var path in new[]{"Assets/Scenes/Levels/escape_route.unity","Assets/Scenes/Levels/escape_route_b.unity","Assets/Scenes/Levels/escape_route_c.unity"})
{
 var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
 int count=0,added=0;
 foreach(var station in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CurtainCall.HumanCannon.HumanCannonStation>(true)))
 {
  count++;
  var existing=station.GetComponentInChildren<CurtainCall.HumanCannon.CannonBarrelPresentation>(true);
  if(existing==null)
  {
   var old=station.transform.Find("CannonVisual");
   if(old==null)throw new System.Exception(path+" "+station.name+" missing old visual");
   foreach(var renderer in old.GetComponentsInChildren<Renderer>(true)) {renderer.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
   var art=(GameObject)PrefabUtility.InstantiatePrefab(prefab,station.transform);art.name="CC_PropArt";
   art.transform.localPosition=Vector3.zero;art.transform.localRotation=Quaternion.identity;art.transform.localScale=Vector3.one;added++;
  }
  var so=new SerializedObject(station);so.FindProperty("animateBarrel").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
  PrefabUtility.RecordPrefabInstancePropertyModifications(station);
 }
 UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
 if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new System.Exception("Scene save failed: "+path);
 report.Add(path+" stations="+count+" new art="+added);
}
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CurtainCall.HumanCannon.EditorTools.CannonBarrelArtBuilder.Gallery);
System.IO.File.WriteAllLines("Tasks/Active/Task-20261030-084-cannon-barrel-state-animation/Source/escape-cannon-apply.txt",report);
return string.Join("\n",report);
