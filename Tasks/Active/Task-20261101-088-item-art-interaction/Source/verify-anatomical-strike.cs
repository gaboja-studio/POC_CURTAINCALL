var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var mesh=new UnityEngine.Mesh();var records=new System.Collections.Generic.List<object>();var failures=new System.Collections.Generic.List<string>();
try{
foreach(string character in new[]{"Rabbit","Bear","BluePierrot","PinkGirl"}){
var obj=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEngine.Resources.Load<UnityEngine.GameObject>("Prefabs/Characters/Players/Models/"+character+"/"+character),scene);
var model=obj.GetComponent<CurtainCall.Player.HumanoidModel>();var a=model.Animator;a.cullingMode=UnityEngine.AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);for(int l=1;l<a.layerCount;l++)a.SetLayerWeight(l,0);
var prop=CurtainCall.ItemArt.ItemVisualAssets.Create("stake-puller");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prop,scene);
for(int stage=0;stage<4;stage++){
var mask=CurtainCall.Player.PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);float top=0,contact=0;UnityEngine.Vector3 raised=default;
foreach(float t in System.Linq.Enumerable.Range(0,25).Select(i=>i==24?.999f:i/24f)){
a.Play(CurtainCall.ItemArt.ItemVisualAssets.State("ItemStrike",stage),0,t);a.Update(0);model.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>().CorrectPose();CurtainCall.ItemArt.ItemVisualAssets.Attach(prop,a,"stake-puller");
var skin=stage==0?model.Original:model.Bodies[stage-1];skin.BakeMesh(mesh,true);var clearance=CurtainCall.ItemArt.EditorTools.ItemArtValidation.HandClearance(skin,mesh,prop,"stake-puller");
var hand=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightHand);var shoulder=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightUpperArm);var head=a.GetBoneTransform(UnityEngine.HumanBodyBones.Head);
var tip=prop.transform.TransformPoint(new UnityEngine.Vector3(0,.62f,0));float low=mesh.vertices.Min(v=>skin.transform.TransformPoint(v).y);
float propLow=prop.GetComponentsInChildren<UnityEngine.MeshFilter>().Min(f=>f.sharedMesh.vertices.Min(v=>f.transform.TransformPoint(v).y));
bool pass=clearance.x>=-.002f&&clearance.y>=-.002f&&low>=-.055f&&propLow>=-.03f;
var elbow=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightLowerArm);var middle=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightMiddleProximal);var upper=elbow.position-shoulder.position;var forearm=hand.position-elbow.position;float elbowBend=UnityEngine.Vector3.Angle(upper,forearm);float wristBend=UnityEngine.Vector3.Angle(forearm,middle.position-hand.position);var outward=shoulder.position-a.GetBoneTransform(UnityEngine.HumanBodyBones.LeftUpperArm).position;float hinge=UnityEngine.Vector3.Dot(UnityEngine.Vector3.Cross(upper,forearm).normalized,outward.normalized);pass&=elbowBend>=5&&elbowBend<=105&&wristBend<=25&&hinge<-.75f;

records.Add(new{character,stage,t,palm=clearance.x,finger=clearance.y,low,propLow,handY=hand.position.y,tipY=tip.y,tipZ=tip.z,elbowBend,wristBend,hinge,pass});if(!pass)failures.Add(character+" stage"+stage+" @"+t);
}
}
UnityEngine.Object.DestroyImmediate(prop);UnityEngine.Object.DestroyImmediate(obj);
}
}finally{UnityEngine.Object.DestroyImmediate(mesh);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
var json=Newtonsoft.Json.JsonConvert.SerializeObject(new{records,failures},Newtonsoft.Json.Formatting.Indented);System.IO.File.WriteAllText("Tasks/Active/Task-20261101-088-item-art-interaction/Art/anatomical-strike-validation.json",json);
return new{poses=records.Count,failures};


