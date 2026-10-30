var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var rows=new System.Collections.Generic.List<object>();
try{foreach(var path in new[]{"Rabbit/Rabbit","Bear/Bear","BluePierrot/BluePierrot","PinkGirl/PinkGirl"}){
var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/"+path+".prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
var model=go.GetComponent<CurtainCall.Player.HumanoidModel>();var a=model.Animator;a.Rebind();a.Update(0);a.SetLayerWeight(1,0);
for(int stage=0;stage<4;stage++){var mask=CurtainCall.Player.PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);var skin=stage==0?model.Original:model.Bodies[stage-1]; foreach(float t in new[]{0f,.21f,.5f,.99f}){a.Play("RopePull"+(stage==0?"":"_Cut"+stage),0,t);a.Update(0);var grounding=go.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>();grounding.CorrectPose();var mesh=new UnityEngine.Mesh();skin.BakeMesh(mesh,true);float min=mesh.vertices.Min(v=>skin.transform.TransformPoint(v).y);rows.Add(new{path,stage,t,min,hip=a.GetBoneTransform(UnityEngine.HumanBodyBones.Hips).position.y});UnityEngine.Object.DestroyImmediate(mesh);}
}UnityEngine.Object.DestroyImmediate(go);
}return rows;}finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}


