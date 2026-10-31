using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Player;
using CurtainCall.CharacterArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.ItemArt.EditorTools
{
    public static class ItemArtValidation
    {
        [Serializable] public class Report { public int poses,avatars,clips,gripPoses; public float minPalmClearance=100,minFingerClearance=100,minSurfaceY=100,maxHeight;public List<string> failures=new(); }
        public static string Run()
        {
            var report=new Report();var scene=EditorSceneManager.NewPreviewScene();var mesh=new Mesh();
            try
            {
                foreach(string character in CurtainCall.HumanCannon.CircusMotionGallery.Characters)
                {
                    var prefab=Resources.Load<GameObject>("Prefabs/Characters/Players/Models/"+character+"/"+character);
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var model=obj.GetComponent<HumanoidModel>();var a=model.Animator;
                    if(!a.isHuman||!a.avatar.isValid)report.failures.Add(character+" invalid avatar");else report.avatars++;
                    a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.applyRootMotion=false;a.Rebind();a.Update(0);for(int l=1;l<a.layerCount;l++)a.SetLayerWeight(l,0);
                    for(int stage=0;stage<4;stage++)
                    {
                        var mask=PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);
                        foreach(string motion in ItemVisualAssets.Motions)
                        {
                            string state=ItemVisualAssets.State(motion,stage);if(!a.HasState(0,Animator.StringToHash(state))){report.failures.Add(character+" missing "+state);continue;}
                            string item=motion=="ItemAim"?"uv-lamp":motion=="ItemManipulate"?"magician-hairpin":"stake-puller";
                            var prop=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ItemArt/Prefabs/"+item));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prop,scene);
                            foreach(float t in new[]{0f,.28f,.56f,.78f,.999f})
                            {
                                a.Play(state,0,t);a.Update(0);model.GetComponent<RabbitTrialGrounding>()?.CorrectPose();
                                if(item=="uv-lamp")ItemVisualAssets.AimLamp(a,Vector3.forward,motion=="ItemCrawl");
                                ItemVisualAssets.Attach(prop,a,item);
                                var skin=stage==0?model.Original:model.Bodies[stage-1];skin.BakeMesh(mesh,true);
                                if(prop!=null)
                                {
                                    var clearance=HandClearance(skin,mesh,prop,item);
                                    report.minPalmClearance=Mathf.Min(report.minPalmClearance,clearance.x);report.minFingerClearance=Mathf.Min(report.minFingerClearance,clearance.y);report.gripPoses++;
                                    if(clearance.x<-.002f||clearance.y<-.002f)report.failures.Add(character+" "+state+" @"+t+" handle/palm,finger clearance "+clearance);
                                }
                                float low=float.PositiveInfinity,high=float.NegativeInfinity;
                                foreach(var vertex in mesh.vertices){var p=skin.transform.TransformPoint(vertex);if(!float.IsFinite(p.x+p.y+p.z)){report.failures.Add(character+state+" nonfinite vertex");break;}low=Mathf.Min(low,p.y);high=Mathf.Max(high,p.y);}
                                report.minSurfaceY=Mathf.Min(report.minSurfaceY,low);report.maxHeight=Mathf.Max(report.maxHeight,high-low);
                                if(low<-.055f)report.failures.Add(character+" "+state+" @"+t+" penetration "+low);
                                if(stage>=2&&a.GetBoneTransform(HumanBodyBones.Hips).position.y>.65f)report.failures.Add(character+" "+state+" unsupported pelvis");
                                report.poses++;
                            }
                            UnityEngine.Object.DestroyImmediate(prop);
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(obj);
                }
                foreach(string motion in ItemVisualAssets.Motions)for(int stage=0;stage<4;stage++)
                {
                    string name=ItemVisualAssets.State(motion,stage);var clip=AssetDatabase.LoadAllAssetsAtPath(ItemArtBuilder.Root+"/Motions/"+name+".fbx").OfType<AnimationClip>().FirstOrDefault(c=>c.name==name);
                    if(clip!=null&&clip.humanMotion&&clip.length>.4f)report.clips++;else report.failures.Add("Invalid clip "+name);
                }
                string json=JsonUtility.ToJson(report,true);File.WriteAllText("Tasks/Active/Task-20261101-088-item-art-interaction/Art/pose-validation.json",json);return json;
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);EditorSceneManager.ClosePreviewScene(scene);}
        }
        public static Vector2 HandClearance(SkinnedMeshRenderer skin,Mesh mesh,GameObject prop,string item)
        {
            var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;var points=new Vector3[vertices.Length];var region=new int[vertices.Length];
            for(int v=0;v<vertices.Length;v++)
            {
                string name=bones[weights[v].boneIndex0].name;
                region[v]=name=="RightHand"?1:name.StartsWith("Right")&&(name.Contains("Index")||name.Contains("Middle")||name.Contains("Ring")||name.Contains("Little")||name.Contains("Thumb"))?2:0;
                if(region[v]!=0)points[v]=prop.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[v]))-ItemVisualAssets.Grip(item);
            }
            float radius=item=="uv-lamp"?.017f:item=="magician-hairpin"||item=="route-key"?.0095f:.01995f;Vector2 result=Vector2.one;
            var triangles=mesh.triangles;
            for(int k=0;k<triangles.Length;k+=3)
            {
                int a=triangles[k],b=triangles[k+1],c=triangles[k+2];if(region[a]==0||region[b]==0||region[c]==0)continue;
                int part=region[a]==1&&region[b]==1&&region[c]==1?0:1;
                for(int u=0;u<=3;u++)for(int v=0;v<=3-u;v++)
                {
                    var p=points[a]*(u/3f)+points[b]*(v/3f)+points[c]*(1-(u+v)/3f);
                    if(Mathf.Abs(p.y)>.055f)continue;
                    float clearance=new Vector2(p.x,p.z).magnitude-radius;result[part]=Mathf.Min(result[part],clearance);
                }
            }
            return result;
        }
    }
}
