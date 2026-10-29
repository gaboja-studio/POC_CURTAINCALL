// Native Unity render: three identical poses under the same lights.
var task="Assets/_ThirdParty/CharacterArt/Rabbit/Source~";
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
bool previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
var meshes=new System.Collections.Generic.List<UnityEngine.Mesh>();
UnityEngine.Material floorMat=null;UnityEngine.RenderTexture rt=null;
try{
 for(int n=0;n<4;n++){
  var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab"));
  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
  var model=go.GetComponent<CurtainCall.Player.HumanoidModel>();var a=model.Animator;
  go.GetComponent<CurtainCall.CharacterArt.RabbitSurfaceStates>().SetVariant(n);
  a.cullingMode=UnityEngine.AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);a.SetLayerWeight(1,0);a.Play("Idle",0,.5f);a.Update(.016f);a.Update(.016f);
  go.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>().CorrectPose();
  var skin=model.Original;var mesh=new UnityEngine.Mesh();skin.BakeMesh(mesh,true);meshes.Add(mesh);skin.enabled=false;
  var display=new UnityEngine.GameObject(new[]{"Red","Blue","Gold","Green"}[n]);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(display,scene);
  display.AddComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;var renderer=display.AddComponent<UnityEngine.MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;var block=new UnityEngine.MaterialPropertyBlock();for(int i=0;i<2;i++){skin.GetPropertyBlock(block,i);renderer.SetPropertyBlock(block,i);block.Clear();}
  display.transform.SetPositionAndRotation(skin.transform.position+new UnityEngine.Vector3((1.5f-n)*.92f,0,0),UnityEngine.Quaternion.Euler(0,-14,0)*skin.transform.rotation);display.transform.localScale=skin.transform.lossyScale;
 }
 var floor=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=new UnityEngine.Vector3(0,-.04f,0);floor.transform.localScale=new UnityEngine.Vector3(12,.08f,8);
 floorMat=new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"));floorMat.color=new UnityEngine.Color(.20f,.21f,.23f);floor.GetComponent<UnityEngine.Renderer>().sharedMaterial=floorMat;
 foreach(var pair in new[]{new{pos=new UnityEngine.Vector3(-3,5,4),power=1.65f},new{pos=new UnityEngine.Vector3(3,3,-3),power=.7f}}){var lg=new UnityEngine.GameObject("Studio Light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lg,scene);var l=lg.AddComponent<UnityEngine.Light>();l.type=UnityEngine.LightType.Directional;l.intensity=pair.power;lg.transform.position=pair.pos;lg.transform.LookAt(new UnityEngine.Vector3(0,1,0));}
 var cg=new UnityEngine.GameObject("Comparison Camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cg,scene);var camera=cg.AddComponent<UnityEngine.Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=1.24f;camera.transform.position=new UnityEngine.Vector3(0,1.55f,7);camera.transform.LookAt(new UnityEngine.Vector3(0,1.06f,0));camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.12f,.14f,.16f);camera.nearClipPlane=.01f;camera.farClipPlane=50;
 rt=new UnityEngine.RenderTexture(2000,1100,24);camera.targetTexture=rt;
 var path=task+"/Previews/rabbit-v16-variants.png";
 var previous=UnityEngine.RenderTexture.active;UnityEngine.Texture2D image=null;
 try{camera.Render();camera.Render();UnityEngine.RenderTexture.active=rt;image=new UnityEngine.Texture2D(2000,1100,UnityEngine.TextureFormat.RGB24,false);image.ReadPixels(new UnityEngine.Rect(0,0,2000,1100),0,0);image.Apply();System.IO.File.WriteAllBytes(path,image.EncodeToPNG());}
 finally{UnityEngine.RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
 return path;
}finally{UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);foreach(var m in meshes)UnityEngine.Object.DestroyImmediate(m);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(floorMat!=null)UnityEngine.Object.DestroyImmediate(floorMat);}
