var player=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Prefabs/Characters/Players/Player.prefab");
var rabbit=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab");
var manager=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Prefabs/Controllers/Network/NetworkManager.prefab").GetComponent<Unity.Netcode.NetworkManager>();
if(manager.NetworkConfig.PlayerPrefab!=player)throw new System.Exception("Network spawn uses another player");
var rows=new System.Collections.Generic.List<object>();var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
try{
 foreach(var path in UnityEditor.EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path).Where(System.IO.File.Exists)){
  var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
  try{
   var slots=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<CurtainCall.Player.PlayerModelSlot>(true)).ToArray();
   foreach(var slot in slots){var so=new UnityEditor.SerializedObject(slot);if(so.FindProperty("modelPrefab").objectReferenceValue!=rabbit)throw new System.Exception("Non rabbit scene slot: "+path+"/"+slot.name);}
   rows.Add(new{scene=path,playerSlots=slots.Length,networkSpawns=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Unity.Netcode.NetworkManager>(true)).Select(x=>UnityEditor.AssetDatabase.GetAssetPath(x.NetworkConfig.PlayerPrefab)).ToArray()});
  }finally{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
 }
}finally{UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);}
var skins=rabbit.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true);
foreach(var skin in skins)if(!UnityEditor.AssetDatabase.GetAssetPath(skin.sharedMesh).StartsWith("Assets/_ThirdParty/CharacterArt/Rabbit/"))throw new System.Exception("Public mesh: "+skin.name);
var report=new{networkPlayer=UnityEditor.AssetDatabase.GetAssetPath(manager.NetworkConfig.PlayerPrefab),model=UnityEditor.AssetDatabase.GetAssetPath(rabbit),scenes=rows,privateSkins=skins.Length,missingScripts=UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(rabbit)};
System.IO.File.WriteAllText("Tasks/Active/Task-20261029-078-character-art-production/Source/gameflow-rabbit-report.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
