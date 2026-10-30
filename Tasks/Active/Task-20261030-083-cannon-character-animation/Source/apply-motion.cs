var root="Assets/Resources/Models/Characters/CannonMotion";
var controllerPath=root+"/CircusPlayer.controller";
if(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(controllerPath)==null)
    if(!UnityEditor.AssetDatabase.CopyAsset("Assets/_ThirdParty/Task062/PlayerPresentation/Player.controller",controllerPath)) throw new System.Exception("Controller copy failed");
var c=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
foreach(var name in System.IO.Directory.GetFiles(root,"*.fbx").Select(System.IO.Path.GetFileNameWithoutExtension)){
foreach(var layer in name.StartsWith("TrampolineCarry")?new[]{0,1}:new[]{0}){
var sm=c.layers[layer].stateMachine;
var state=sm.states.Select(x=>x.state).FirstOrDefault(x=>x.name==name)??sm.AddState(name);
state.motion=UnityEditor.AssetDatabase.LoadAllAssetsAtPath(root+"/"+name+".fbx").OfType<UnityEngine.AnimationClip>().First(x=>!x.name.StartsWith("__preview__"));
state.writeDefaultValues=true;
UnityEditor.EditorUtility.SetDirty(state);
}
}
UnityEditor.EditorUtility.SetDirty(c);
UnityEditor.AssetDatabase.SaveAssetIfDirty(c);
var paths=new[]{"Rabbit/Rabbit","Bear/Bear","PinkGirl/PinkGirl","BluePierrot/BluePierrot"};
foreach(var p in paths){
var path="Assets/Resources/Prefabs/Characters/Players/Models/"+p+".prefab";
var obj=UnityEditor.PrefabUtility.LoadPrefabContents(path);
try{var a=obj.GetComponentInChildren<UnityEngine.Animator>(true);a.runtimeAnimatorController=c;a.applyRootMotion=false;UnityEditor.PrefabUtility.SaveAsPrefabAsset(obj,path);}
finally{UnityEditor.PrefabUtility.UnloadPrefabContents(obj);}
}
return "Applied to four character prefabs; original controller retained";
