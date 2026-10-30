var path="Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
try{
var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
var t=all.Select(o=>o.GetComponent<CurtainCall.HumanCannon.HumanCannonTrampoline>()).First(o=>o!=null);
var c=all.Select(o=>o.GetComponent<CurtainCall.HumanCannon.HumanCannonStation>()).First(o=>o!=null);
return new{handle=t.Handle.localPosition,tscale=t.transform.lossyScale,seat=c.Seat.localPosition,muzzle=c.Muzzle.localPosition,cscale=c.transform.lossyScale};
}finally{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
