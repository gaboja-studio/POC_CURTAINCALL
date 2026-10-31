using System.IO;
using CurtainCall.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.ItemArt.EditorTools
{
    public static class ItemMotionReview
    {
        public static string Capture(string character,string item,int stage,string motion,float time)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Prefabs/Characters/Players/Models/"+character+"/"+character),scene);
                var model=obj.GetComponent<HumanoidModel>();var a=model.Animator;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);
                for(int l=1;l<a.layerCount;l++)a.SetLayerWeight(l,0);
                var mask=PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);
                a.Play(ItemVisualAssets.State(motion,stage),0,time);a.Update(0);model.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>().CorrectPose();
                if(item=="uv-lamp")ItemVisualAssets.AimLamp(a,Vector3.forward,motion=="ItemCrawl");
                var prop=ItemVisualAssets.Create(item);if(prop!=null){UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prop,scene);ItemVisualAssets.Attach(prop,a,item);}
                var lightObject=new GameObject("Review light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject,scene);
                var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.7f;light.transform.rotation=Quaternion.Euler(40,-35,0);
                var cameraObject=new GameObject("Review camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=stage>=2?.85f:1.35f;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.backgroundColor=new Color(.22f,.26f,.31f);camera.clearFlags=CameraClearFlags.SolidColor;
                Vector3 center=stage>=2?new Vector3(0,.3f,.38f):Vector3.up;
                camera.transform.position=center+new Vector3(-2.8f,1.7f,4);camera.transform.LookAt(center);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=new Vector3(0,-.03f,0);floor.transform.localScale=new Vector3(5,.04f,5);floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(ItemArtBuilder.Root+"/Materials/GalleryFloor.mat");
                var rt=new RenderTexture(1000,1000,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1000,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1000,1000),0,0);tex.Apply();
                string path="Tasks/Active/Task-20261101-088-item-art-interaction/Art/review-"+character.ToLowerInvariant()+"-"+item+"-"+stage+"-"+motion.ToLowerInvariant()+"-"+Mathf.RoundToInt(time*100)+".png";
                File.WriteAllBytes(path,tex.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
                return path;
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
