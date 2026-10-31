using System.IO;
using System.Text;
using CurtainCall.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.ItemArt.EditorTools
{
    public static class ItemGripReview
    {
        public static string Capture(string suffix, string item = "stake-puller", int stage = 0)
        {
            string directory = "Tasks/Active/Task-20261101-088-item-art-interaction/Art";
            var scene = EditorSceneManager.NewPreviewScene();
            var report = new StringBuilder();
            try
            {
                foreach (string character in CurtainCall.HumanCannon.CircusMotionGallery.Characters)
                {
                    var prefab = Resources.Load<GameObject>("Prefabs/Characters/Players/Models/"+character+"/"+character);
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    var model = obj.GetComponent<HumanoidModel>(); var a = model.Animator;
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.Rebind(); a.Update(0);
                    for(int i=1;i<a.layerCount;i++)a.SetLayerWeight(i,0);
                    var mask = PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);
                    a.Play(ItemVisualAssets.State(item=="uv-lamp"?"ItemAim":item=="magician-hairpin"?"ItemManipulate":"ItemHold",stage),0,.5f);a.Update(0);
                    var prop = ItemVisualAssets.Create(item);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prop,scene);
                    ItemVisualAssets.Attach(prop,a,item);
                    var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
                    var middle=a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                    var index=a.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                    var little=a.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                    Vector3 fingers=(middle.position-hand.position).normalized;
                    Vector3 across=(index.position-little.position).normalized;
                    Vector3 normal=Vector3.Cross(across,fingers).normalized;
                    Vector3 center=middle.position+normal*.04f;
                    var lightObject = new GameObject("Grip review light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject,scene);
                    var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.LookRotation(-normal-fingers*.4f);
                    var cameraObject=new GameObject("Grip review camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
                    var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=.19f;camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.3f,.36f);
                    foreach(string side in new[]{"palm","side"})
                    {
                        camera.transform.position=center+(side=="palm"?-normal+fingers*.35f:across+normal*.3f)*.8f;camera.transform.rotation=Quaternion.LookRotation(center-camera.transform.position, fingers);
                        var rt=new RenderTexture(768,768,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
                        var tex=new Texture2D(768,768,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,768,768),0,0);tex.Apply();File.WriteAllBytes(directory+"/grip-"+character.ToLowerInvariant()+"-"+item+"-"+stage+"-"+suffix+"-"+side+".png",tex.EncodeToPNG());
                        RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
                    }
                    report.AppendLine(character);
                    var mesh=new Mesh();var skin=stage==0?model.Original:model.Bodies[stage-1];skin.BakeMesh(mesh);
                    var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                    float minPalm=1,minFinger=1;
                    for(int v=0;v<vertices.Length;v++)
                    {
                        var w=weights[v];string name=bones[w.boneIndex0].name;if(!name.StartsWith("Right"))continue;
                        bool palm=name=="RightHand";bool finger=name.Contains("Index")||name.Contains("Middle")||name.Contains("Ring")||name.Contains("Little")||name.Contains("Thumb");if(!palm&&!finger)continue;
                        Vector3 p=prop.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[v]))-ItemVisualAssets.Grip(item);
                        if(Mathf.Abs(p.y)>.055f)continue;
                        float distance=new Vector2(p.x,p.z).magnitude;
                        if(palm)minPalm=Mathf.Min(minPalm,distance);else minFinger=Mathf.Min(minFinger,distance);
                    }
                    report.AppendLine("mesh palm/finger axis clearance "+minPalm.ToString("F4")+" / "+minFinger.ToString("F4"));Object.DestroyImmediate(mesh);
                    foreach(var bone in new[]{HumanBodyBones.RightIndexProximal,HumanBodyBones.RightIndexIntermediate,HumanBodyBones.RightIndexDistal,HumanBodyBones.RightThumbProximal,HumanBodyBones.RightThumbIntermediate,HumanBodyBones.RightThumbDistal})
                    {Vector3 d=a.GetBoneTransform(bone).position-middle.position;report.AppendLine(bone+" "+new Vector3(Vector3.Dot(d,across),Vector3.Dot(d,fingers),Vector3.Dot(d,normal)).ToString("F4"));}
                    Object.DestroyImmediate(obj);Object.DestroyImmediate(prop);Object.DestroyImmediate(lightObject);Object.DestroyImmediate(cameraObject);
                }
                File.WriteAllText(directory+"/grip-"+item+"-"+suffix+".txt",report.ToString());return report.ToString();
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
