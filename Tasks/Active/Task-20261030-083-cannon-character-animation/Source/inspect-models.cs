var paths = new[]{"Rabbit/Rabbit", "Bear/Bear", "PinkGirl/PinkGirl", "BluePierrot/BluePierrot"};
var result = new System.Collections.Generic.List<object>();
foreach(var p in paths){
var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/"+p+".prefab");
var a=asset.GetComponentInChildren<UnityEngine.Animator>(true);
result.Add(new{p,controller=UnityEditor.AssetDatabase.GetAssetPath(a.runtimeAnimatorController),avatar=UnityEditor.AssetDatabase.GetAssetPath(a.avatar), valid=a.avatar.isValid});
}
var scenes=new System.Collections.Generic.List<object>();
for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++){var s=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);scenes.Add(new{s.path,s.isDirty});}
return new{models=result,scenes};
