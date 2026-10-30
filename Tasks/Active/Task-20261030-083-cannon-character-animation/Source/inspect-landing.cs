var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var rows=new System.Collections.Generic.List<object>();
try{foreach(var path in new[]{"Rabbit/Rabbit","Bear/Bear","BluePierrot/BluePierrot","PinkGirl/PinkGirl"}){
var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/"+path+".prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
var model=go.GetComponent<CurtainCall.Player.HumanoidModel>();var a=model.Animator;a.Rebind();a.Update(0);a.SetLayerWeight(1,0);
foreach(float t in new[]{0f,.21f,.5f,.99f}){a.Play("FlightLand",0,t);a.Update(0);var grounding=go.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>();grounding.CorrectPose();var mesh=new UnityEngine.Mesh();model.Original.BakeMesh(mesh,true);float min=mesh.vertices.Min(v=>model.Original.transform.TransformPoint(v).y);rows.Add(new{path,t,min,hip=a.GetBoneTransform(UnityEngine.HumanBodyBones.Hips).position.y});UnityEngine.Object.DestroyImmediate(mesh);}
UnityEngine.Object.DestroyImmediate(go);
}return rows;}finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
