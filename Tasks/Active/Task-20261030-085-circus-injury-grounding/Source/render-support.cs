var output="Tasks/Active/Task-20261030-085-circus-injury-grounding/Previews";System.IO.Directory.CreateDirectory(output);
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
bool async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
var meshes=new System.Collections.Generic.List<UnityEngine.Mesh>();var materials=new System.Collections.Generic.List<UnityEngine.Material>();
var rt=new UnityEngine.RenderTexture(1600,1000,24);
try {
var cg=new UnityEngine.GameObject("Support camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cg,scene);
var cam=cg.AddComponent<UnityEngine.Camera>();cam.scene=scene;cam.orthographic=true;cam.orthographicSize=2.6f;cam.nearClipPlane=.01f;cam.farClipPlane=100;cam.targetTexture=rt;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;cam.backgroundColor=new UnityEngine.Color(.08f,.10f,.14f);
cam.transform.position=new UnityEngine.Vector3(5,7,13);cam.transform.LookAt(new UnityEngine.Vector3(3.3f,.35f,1.1f));
foreach(var v in new[]{new UnityEngine.Vector3(2,7,5),new UnityEngine.Vector3(-4,3,-3)}){var g=new UnityEngine.GameObject("Light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g,scene);var l=g.AddComponent<UnityEngine.Light>();l.type=UnityEngine.LightType.Directional;l.intensity=v.y==7?1.6f:.7f;g.transform.position=v;g.transform.LookAt(UnityEngine.Vector3.zero);}
foreach(var motion in new[]{"FlightLand","TrampolineCarry","RopePull"}){
var objects=new System.Collections.Generic.List<UnityEngine.GameObject>();
for(int character=0;character<4;character++)for(int stage=1;stage<4;stage++){
var name=new[]{"Rabbit","Bear","BluePierrot","PinkGirl"}[character];
var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{name}/{name}.prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
go.transform.position=new UnityEngine.Vector3(character*2.2f,0,(stage-1)*2.0f);objects.Add(go);
var m=go.GetComponent<CurtainCall.Player.HumanoidModel>();var a=m.Animator;a.Rebind();a.Update(0);a.SetLayerWeight(1,0);var mask=CurtainCall.Player.PlayerCondition.PartsForLostCount(stage);m.Apply(mask,mask,false,null);
a.Play(motion+"_Cut"+stage,0,.21f);a.Update(0);go.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>().CorrectPose();
var skin=m.Bodies[stage-1];var mesh=new UnityEngine.Mesh();skin.BakeMesh(mesh,false);meshes.Add(mesh);
var show=new UnityEngine.GameObject("Pose");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(show,scene);show.AddComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;show.AddComponent<UnityEngine.MeshRenderer>().sharedMaterials=skin.sharedMaterials;show.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);show.transform.localScale=skin.transform.lossyScale;objects.Add(show);go.SetActive(false);
var platform=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(platform,scene);platform.transform.position=new UnityEngine.Vector3(character*2.2f,-.04f,(stage-1)*2);platform.transform.localScale=new UnityEngine.Vector3(1.8f,.08f,1.65f);objects.Add(platform);
var mat=new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"));mat.color=new UnityEngine.Color(.22f,.25f,.29f);materials.Add(mat);platform.GetComponent<UnityEngine.Renderer>().sharedMaterial=mat;
var label=new UnityEngine.GameObject("Label");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(label,scene);var text=label.AddComponent<UnityEngine.TextMesh>();text.text=name+" / Cut"+stage;text.fontSize=40;text.characterSize=.033f;text.anchor=UnityEngine.TextAnchor.MiddleCenter;text.color=new UnityEngine.Color(1,.85f,.5f);label.transform.position=new UnityEngine.Vector3(character*2.2f,.02f,(stage-1)*2+.82f);label.transform.rotation=UnityEngine.Quaternion.Euler(60,180,0);objects.Add(label);
}
cam.Render();cam.Render();var old=UnityEngine.RenderTexture.active;UnityEngine.RenderTexture.active=rt;var im=new UnityEngine.Texture2D(rt.width,rt.height,UnityEngine.TextureFormat.RGB24,false);im.ReadPixels(new UnityEngine.Rect(0,0,rt.width,rt.height),0,0);im.Apply();System.IO.File.WriteAllBytes(output+"/latest-"+motion.ToLowerInvariant()+".png",im.EncodeToPNG());UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(im);foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);
}
return output;
}finally{UnityEditor.ShaderUtil.allowAsyncCompilation=async;UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);foreach(var m in meshes)UnityEngine.Object.DestroyImmediate(m);foreach(var m in materials)UnityEngine.Object.DestroyImmediate(m);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
