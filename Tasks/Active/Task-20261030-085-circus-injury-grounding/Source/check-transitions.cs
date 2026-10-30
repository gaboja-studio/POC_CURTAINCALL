var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var report=new System.Collections.Generic.List<object>();
try {
foreach(var character in new[]{"Rabbit","Bear","BluePierrot","PinkGirl"}) {
var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{character}/{character}.prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
var m=go.GetComponent<CurtainCall.Player.HumanoidModel>();var a=m.Animator;var g=go.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>();
a.Rebind();a.Update(0);a.SetLayerWeight(1,0);
for(int stage=1;stage<4;stage++) {
var mask=CurtainCall.Player.PlayerCondition.PartsForLostCount(stage);m.Apply(mask,mask,false,null);a.speed=1;var suffix="_Cut"+stage;
foreach(var pair in new[]{new[]{"PanicFall"+suffix,"FlightLand"+suffix},new[]{"FlightLand"+suffix,stage>=2?"Crawl":"Idle"},new[]{"TrampolineCarry"+suffix,"CarryJump"+suffix},new[]{"RopePull"+suffix,stage>=2?"Crawl":"Idle"}}){
a.Play(pair[0],0,.99f);a.Update(0);g.CorrectPose();var hip=a.GetBoneTransform(UnityEngine.HumanBodyBones.Hips);var last=hip.position;float maxStep=0;
a.CrossFadeInFixedTime(pair[1],.2f,0,0);
for(int frame=0;frame<30;frame++){a.Update(1f/60);g.CorrectPose();maxStep=Mathf.Max(maxStep,UnityEngine.Vector3.Distance(last,hip.position));last=hip.position;}
if(!float.IsFinite(maxStep)||maxStep>.12f)throw new System.Exception($"Discontinuous pelvis {character} {stage} {pair[0]} -> {pair[1]}: {maxStep}");
report.Add(new{character,stage,from=pair[0],to=pair[1],maxHipStep=maxStep});
}
}UnityEngine.Object.DestroyImmediate(go);
}
System.IO.File.WriteAllText("Tasks/Active/Task-20261030-085-circus-injury-grounding/Source/transitions.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return new{transitions=report.Count};
}finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
