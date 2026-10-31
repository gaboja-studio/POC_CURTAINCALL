var p=UnityEngine.Object.FindFirstObjectByType<CurtainCall.ItemArt.ItemGameplayPlayground>();
var model=p.Player.GetComponent<CurtainCall.Player.PlayerModelSlot>().CurrentModel.GetComponent<CurtainCall.Player.HumanoidModel>();
var a=model.Animator;
var prop=p.Player.GetComponent<CurtainCall.Player.HumanoidPlayerPresentation>().HeldItemVisual;
var hand=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightHand);
var middle=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightMiddleProximal);
var index=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightIndexProximal);
var little=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightLittleProximal);
var fingers=(middle.position-hand.position).normalized;
var across=(index.position-little.position).normalized;
var normal=UnityEngine.Vector3.Cross(across,fingers).normalized;
var center=middle.position-normal*.04f;
var cameraObject=new UnityEngine.GameObject("Temporary live grip evidence camera");
var camera=cameraObject.AddComponent<UnityEngine.Camera>();camera.CopyFrom(p.ViewCamera);
camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=30;
foreach(string view in new[]{"full","hand"})
{
    var target=view=="full"?p.Player.transform.position+UnityEngine.Vector3.up*.95f:center;
    camera.orthographicSize=view=="full"?1.3f:.21f;
    camera.transform.position=view=="full"?target+new UnityEngine.Vector3(-2,1,4):center-normal*.7f+across*.4f+fingers*.2f;
    camera.transform.rotation=UnityEngine.Quaternion.LookRotation(target-camera.transform.position,view=="full"?UnityEngine.Vector3.up:fingers);
    var rt=new UnityEngine.RenderTexture(1000,1000,24);camera.targetTexture=rt;camera.Render();
    var old=UnityEngine.RenderTexture.active;UnityEngine.RenderTexture.active=rt;
    var tex=new UnityEngine.Texture2D(1000,1000,UnityEngine.TextureFormat.RGB24,false);tex.ReadPixels(new UnityEngine.Rect(0,0,1000,1000),0,0);tex.Apply();
    System.IO.File.WriteAllBytes("Tasks/Active/Task-20261101-088-item-art-interaction/Art/grip-live-"+model.name.ToLowerInvariant()+"-"+model.Stage+"-"+view+".png",tex.EncodeToPNG());
    UnityEngine.RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
}
UnityEngine.Object.DestroyImmediate(cameraObject);
var mesh=new UnityEngine.Mesh();var skin=model.Stage==0?model.Original:model.Bodies[model.Stage-1];skin.BakeMesh(mesh,true);
var clearance=CurtainCall.ItemArt.EditorTools.ItemArtValidation.HandClearance(skin,mesh,prop,"stake-puller");UnityEngine.Object.DestroyImmediate(mesh);
return new{frame=UnityEngine.Time.frameCount,character=model.name,stage=model.Stage,palmClearance=clearance.x,fingerClearance=clearance.y,held=prop.name};
