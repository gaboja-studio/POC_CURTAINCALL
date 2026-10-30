var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var rows=new System.Collections.Generic.List<object>();
try {
foreach(var character in new[]{"Rabbit","Bear","BluePierrot","PinkGirl"}) {
var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{character}/{character}.prefab"));
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
var model=go.GetComponent<CurtainCall.Player.HumanoidModel>();var a=model.Animator;
a.cullingMode=UnityEngine.AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);a.SetLayerWeight(1,0);
var grounding=go.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>();
for(int stage=0;stage<4;stage++) {
var mask=CurtainCall.Player.PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);
var skin=stage==0?model.Original:model.Bodies[stage-1];var mesh=new UnityEngine.Mesh();
try { foreach(var motion in new[]{"FlightLand","TrampolineCarry","RopePull","RopeLift","CarryJump","Crawl"}) {
var name=motion+(stage==0||motion=="Crawl"?"":"_Cut"+stage);
for(int f=0;f<=20;f++) {
float t=Mathf.Min(.9999f,f/20f);
a.Play(name,0,t);a.Update(0);grounding.CorrectPose();skin.BakeMesh(mesh,false);
var vs=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();
var hip=a.GetBoneTransform(UnityEngine.HumanBodyBones.Hips);
var lu=a.GetBoneTransform(UnityEngine.HumanBodyBones.LeftUpperLeg);var ru=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightUpperLeg);
var bones=skin.bones;var weights=skin.sharedMesh.boneWeights;
float SupportWeight(UnityEngine.BoneWeight w){float W(int b,float value)=>bones[b]==hip||bones[b]==lu||bones[b]==ru?value:0;return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3);}
float pelvisMin=System.Linq.Enumerable.Range(0,vs.Length).Where(i=>SupportWeight(weights[i])>.5f).Select(i=>vs[i].y).DefaultIfEmpty(float.NaN).Min();
rows.Add(new{character,stage,motion,t,min=vs.Min(v=>v.y)-go.transform.position.y,
pelvisMin,
hips=a.GetBoneTransform(UnityEngine.HumanBodyBones.Hips).position.y,
hand=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightHand).position.y,
foot=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightFoot).position.y,lift=grounding.Lift});
}} } finally {UnityEngine.Object.DestroyImmediate(mesh);}
}UnityEngine.Object.DestroyImmediate(go);
}
var output="Tasks/Active/Task-20261030-085-circus-injury-grounding/Source/support-latest.json";
System.IO.File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
return new{output,samples=rows.Count};
}finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
