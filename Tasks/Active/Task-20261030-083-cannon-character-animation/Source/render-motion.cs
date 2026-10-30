var root="Assets/Resources/Models/Characters/CannonMotion";
var output="Tasks/Active/Task-20261030-083-cannon-character-animation/Previews";
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
bool previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
var meshes=new System.Collections.Generic.List<UnityEngine.Mesh>();
UnityEngine.RenderTexture rt=null;
try{
var camGo=new UnityEngine.GameObject("Motion Camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo,scene);
var camera=camGo.AddComponent<UnityEngine.Camera>();camera.scene=scene;camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.12f,.14f,.17f);
foreach(var p in new[]{new UnityEngine.Vector3(3,5,4),new UnityEngine.Vector3(-3,2,-2)}){var lg=new UnityEngine.GameObject("Studio Light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lg,scene);var l=lg.AddComponent<UnityEngine.Light>();l.type=UnityEngine.LightType.Directional;l.intensity=p.y==5?1.5f:.6f;lg.transform.position=p;lg.transform.LookAt(UnityEngine.Vector3.up);}
rt=new UnityEngine.RenderTexture(1000,850,24);camera.targetTexture=rt;
foreach(var name in new[]{"CannonLoaded","CannonRise","FlightApex","PanicFall","TrampolineRise","FlightLand"}){
var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab"));
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
var m=go.GetComponent<CurtainCall.Player.HumanoidModel>();var a=m.Animator;a.cullingMode=UnityEngine.AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);a.SetLayerWeight(1,0);a.Play(name,0,name=="FlightLand"?.21f:.25f);a.Update(0);
UnityEngine.GameObject cannon=null;
if(name=="CannonLoaded"){
cannon=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Models/Objects/HumanCannonProps/Prefabs/CC_Cannon.prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cannon,scene);
var target=new UnityEngine.Vector3(0,2,1.5f)-new UnityEngine.Vector3(0,.4226183f,.9063078f)*.10f-UnityEngine.Vector3.up*.10f;
go.transform.position+=target-a.GetBoneTransform(UnityEngine.HumanBodyBones.Head).position;
}
var mesh=new UnityEngine.Mesh();m.Original.BakeMesh(mesh,true);meshes.Add(mesh);
var show=new UnityEngine.GameObject(name);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(show,scene);show.AddComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;var rend=show.AddComponent<UnityEngine.MeshRenderer>();rend.sharedMaterials=m.Original.sharedMaterials;show.transform.SetPositionAndRotation(m.Original.transform.position,m.Original.transform.rotation);show.transform.localScale=m.Original.transform.lossyScale;
go.SetActive(false);
var b=rend.bounds;if(cannon!=null)foreach(var cr in cannon.GetComponentsInChildren<UnityEngine.Renderer>())b.Encapsulate(cr.bounds);
camera.orthographicSize=Mathf.Max(b.size.y*.66f,b.size.z*.55f,1.15f);camera.transform.position=b.center+new UnityEngine.Vector3(4,1.2f,6);camera.transform.LookAt(b.center);
camera.Render();camera.Render();var old=UnityEngine.RenderTexture.active;UnityEngine.RenderTexture.active=rt;var image=new UnityEngine.Texture2D(1000,850,UnityEngine.TextureFormat.RGB24,false);image.ReadPixels(new UnityEngine.Rect(0,0,1000,850),0,0);image.Apply();System.IO.File.WriteAllBytes(output+"/unity-"+name.ToLowerInvariant()+".png",image.EncodeToPNG());UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(show);if(cannon!=null)UnityEngine.Object.DestroyImmediate(cannon);
}
return "Six motion previews rendered";
}finally{UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);foreach(var m in meshes)UnityEngine.Object.DestroyImmediate(m);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}

