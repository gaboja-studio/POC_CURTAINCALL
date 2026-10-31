var output=new System.Collections.Generic.List<string>();
foreach(string character in new[]{"Rabbit","Bear","BluePierrot","PinkGirl"})foreach(int stage in new[]{0,3}){
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
try{
var obj=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEngine.Resources.Load<UnityEngine.GameObject>("Prefabs/Characters/Players/Models/"+character+"/"+character),scene);
var model=obj.GetComponent<CurtainCall.Player.HumanoidModel>();var a=model.Animator;a.cullingMode=UnityEngine.AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);for(int l=1;l<a.layerCount;l++)a.SetLayerWeight(l,0);var mask=CurtainCall.Player.PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);
var skin=stage==0?model.Original:model.Bodies[stage-1];var baked=new UnityEngine.Mesh();var frozen=new UnityEngine.GameObject("Pose mesh");frozen.transform.SetParent(skin.transform,false);frozen.AddComponent<UnityEngine.MeshFilter>().sharedMesh=baked;var frozenRenderer=frozen.AddComponent<UnityEngine.MeshRenderer>();frozenRenderer.sharedMaterials=skin.sharedMaterials;foreach(var renderer in obj.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true))renderer.forceRenderingOff=true;
var prop=CurtainCall.ItemArt.ItemVisualAssets.Create("stake-puller");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prop,scene);
for(int i=0;i<2;i++){var lo=new UnityEngine.GameObject("Review light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lo,scene);var light=lo.AddComponent<UnityEngine.Light>();light.type=UnityEngine.LightType.Directional;light.intensity=i==0?1.5f:.8f;lo.transform.rotation=UnityEngine.Quaternion.Euler(35,i==0?-35:145,0);}
var go=new UnityEngine.GameObject("Review camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<UnityEngine.Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=stage==0?1.3f:1.05f;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.backgroundColor=new UnityEngine.Color(.24f,.27f,.30f);camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
var views=new[]{UnityEngine.Vector3.forward,UnityEngine.Vector3.back,UnityEngine.Vector3.left,UnityEngine.Vector3.right};var times=new[]{0f,.24f,.38f,.55f,.75f};var rt=new UnityEngine.RenderTexture(400,480,24);camera.targetTexture=rt;var tex=new UnityEngine.Texture2D(2000,1920,UnityEngine.TextureFormat.RGB24,false);var previous=UnityEngine.RenderTexture.active;
for(int col=0;col<5;col++){
a.Play(CurtainCall.ItemArt.ItemVisualAssets.State("ItemStrike",stage),0,times[col]);a.Update(0);model.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>().CorrectPose();CurtainCall.ItemArt.ItemVisualAssets.Attach(prop,a,"stake-puller");
skin.BakeMesh(baked);
for(int row=0;row<4;row++){var center=new UnityEngine.Vector3(0,stage==0?1.05f:.65f,.05f);camera.transform.position=center+views[row]*4;camera.transform.LookAt(center);camera.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,400,480),col*400,(3-row)*480);}}
tex.Apply();string path="Tasks/Active/Task-20261101-088-item-art-interaction/Art/strike-four-view-"+character.ToLowerInvariant()+"-"+stage+"-v2.png";System.IO.File.WriteAllBytes(path,tex.EncodeToPNG());output.Add(path);UnityEngine.RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);
}finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}}
return output;

