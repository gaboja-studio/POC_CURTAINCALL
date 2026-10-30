using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Linq;
using CurtainCall.Player;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonMotionTests
    {
        [TestCase("Rabbit")]
        [TestCase("Bear")]
        [TestCase("BluePierrot")]
        [TestCase("PinkGirl")]
        public void InjuredGroundActionsPlantTheActualPelvisOrSurvivingSole(string character)
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var mesh=new Mesh();
            try
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{character}/{character}.prefab"));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
                // Nonzero elevation and yaw catch accidental world-zero or local/world mixing.
                go.transform.SetPositionAndRotation(new Vector3(2,3,4),Quaternion.Euler(0,137,0));
                var model=go.GetComponent<HumanoidModel>();var a=model.Animator;
                var ground=go.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>();
                a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);a.SetLayerWeight(1,0);
                for(int stage=1;stage<4;stage++)
                {
                    var mask=PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);
                    var skin=model.Bodies[stage-1];var bones=skin.bones;var weights=skin.sharedMesh.boneWeights;
                    var supportBones=(stage==1?new[]{HumanBodyBones.RightFoot,HumanBodyBones.RightToes}:
                        new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg}).Select(a.GetBoneTransform).ToArray();
                    float W(int index,float weight)=>supportBones.Contains(bones[index])?weight:0;
                    var support=Enumerable.Range(0,weights.Length).Where(i=>{
                        var w=weights[i];return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.5f;
                    }).ToArray();
                    Assert.IsNotEmpty(support);
                    foreach(var motion in new[]{"FlightLand","TrampolineCarry","RopePull"})
                    for(int frame=0;frame<=30;frame++)
                    {
                        a.Play(motion+"_Cut"+stage,0,Mathf.Min(.9999f,frame/30f));a.Update(0);ground.CorrectPose();
                        skin.BakeMesh(mesh,false);var vertices=mesh.vertices;
                        float floor=go.transform.position.y;
                        float contact=support.Min(i=>skin.transform.TransformPoint(vertices[i]).y)-floor;
                        string label=$"{character} {motion}_Cut{stage} frame {frame}";
                        Assert.That(contact,Is.InRange(-.006f,.012f),label+" load-bearing support; a fingertip alone is insufficient");
                        float bottom=vertices.Min(v=>skin.transform.TransformPoint(v).y)-floor;
                        Assert.GreaterOrEqual(bottom,-.018f,label+" remaining surface must not penetrate the floor");
                    }
                    foreach(var air in new[]{"CarryJump","PanicFall","RopeHang","RopeLift","CannonLoaded"})
                    {
                        a.Play(air+"_Cut"+stage,0,.4f);a.Update(0);ground.CorrectPose();
                        Assert.AreEqual(0,ground.Lift,air+" must not be pinned to the floor");
                    }
                }
            }
            finally{Object.DestroyImmediate(mesh);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void ApexPanicReboundAndLandingDoNotLatchOldFlight()
        {
            var p=new CannonMotionPhase();
            Assert.AreEqual("CannonRise",p.Step(true,0,1,0,12,.02f));
            for(int i=0;i<6;i++)p.Step(true,0,1,0,5,.02f);
            Assert.AreEqual("FlightApex",p.Step(true,0,1,0,.2f,.02f));
            for(int i=0;i<30;i++)p.Step(true,0,1,0,-4,.02f);
            Assert.AreEqual("PanicFall",p.Motion);
            Assert.AreEqual("TrampolineRise",p.Step(true,0,1,1,12,.02f));
            Assert.AreEqual("TrampolineRise",p.Step(true,0,1,1,-4,.02f),"stale remote downward sample must not latch falling after rebound");
            Assert.AreEqual("FlightLand",p.Step(false,0,1,1,0,.02f));
            for(int i=0;i<50;i++)p.Step(false,0,1,1,0,.02f);
            Assert.IsNull(p.Motion);
            Assert.AreEqual("CannonRise",p.Step(true,0,2,0,12,.02f));
            p.Reset();Assert.IsNull(p.Motion);
        }

        [TestCase("Rabbit/Rabbit")]
        [TestCase("Bear/Bear")]
        [TestCase("PinkGirl/PinkGirl")]
        [TestCase("BluePierrot/BluePierrot")]
        public void FourAvatarsRetargetActualPosesAndPanicMotion(string path)
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/"+path+".prefab"));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
                var a=go.GetComponent<HumanoidModel>().Animator;
                Assert.IsTrue(a.avatar.isValid && a.isHuman);
                a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Update(0);a.SetLayerWeight(1,0);
                void Pose(string state,float time){a.Play(state,0,time);a.Update(0);}
                Vector3 P(HumanBodyBones b)=>a.GetBoneTransform(b).position;
                Pose("CannonLoaded",.2f);
                var torso=(P(HumanBodyBones.Head)-P(HumanBodyBones.Hips)).normalized;
                Assert.Greater(Vector3.Dot(torso,go.transform.forward),.60f,"prone torso points down barrel, not standing");
                Pose("TrampolineRise",.25f);
                Assert.Greater(P(HumanBodyBones.LeftHand).y,P(HumanBodyBones.LeftUpperArm).y+.15f);
                Assert.Greater(P(HumanBodyBones.RightHand).y,P(HumanBodyBones.RightUpperArm).y+.15f);
                Pose("PanicFall",0);var hand=P(HumanBodyBones.LeftHand);var foot=P(HumanBodyBones.LeftFoot);
                Pose("PanicFall",.25f);
                Assert.Greater(Vector3.Distance(hand,P(HumanBodyBones.LeftHand)),.15f,"panic has authored moving arms");
                Assert.Greater(Vector3.Distance(foot,P(HumanBodyBones.LeftFoot)),.04f,"panic has alternating moving legs");
                Pose("PanicFall",0);hand=P(HumanBodyBones.LeftHand);
                Pose("PanicFall",.9999f);Assert.Less(Vector3.Distance(hand,P(HumanBodyBones.LeftHand)),.012f,"loop seam");
                foreach(var clip in a.runtimeAnimatorController.animationClips.Where(c=>new[]{"CannonLoaded","CannonRise","FlightApex","PanicFall","TrampolineRise","FlightLand"}.Contains(c.name)))
                    Assert.IsTrue(clip.humanMotion,clip.name);
                var model=go.GetComponent<HumanoidModel>();
                for(int stage=0;stage<4;stage++)
                {
                    var mask=PlayerCondition.PartsForLostCount(stage);model.Apply(mask,mask,false,null);
                    string suffix=stage==0?"":"_Cut"+stage;
                    foreach(string motion in new[]{"CannonLoaded","CannonRise","FlightApex","PanicFall","TrampolineRise","FlightLand","TrampolineCarry","CarryJump","RopeHang","RopeLift","RopePull"})
                    {
                        Assert.IsTrue(a.HasState(0,Animator.StringToHash(motion+suffix)),motion+suffix);
                        Pose(motion+suffix,.15f);var start=P(HumanBodyBones.RightHand);
                        Pose(motion+suffix,.65f);
                        Assert.IsTrue(float.IsFinite(P(HumanBodyBones.Head).y));
                        if(motion=="PanicFall"||motion=="RopeLift"||motion=="RopePull")
                            Assert.Greater(Vector3.Distance(start,P(HumanBodyBones.RightHand)),.025f,motion+suffix+" surviving arm moves");
                    }
                    Assert.IsTrue(a.HasState(1,Animator.StringToHash("TrampolineCarry"+suffix)));
                    Pose("CannonLoaded"+suffix,.2f);
                    Assert.Greater(Vector3.Dot((P(HumanBodyBones.Head)-P(HumanBodyBones.Hips)).normalized,go.transform.forward),.60f,"prone cut "+stage);
                    foreach(var motion in new[]{"FlightLand","RopePull","TrampolineCarry"})
                    foreach(float at in new[]{0f,.21f,.5f,.99f})
                    {
                        Pose(motion+suffix,at);
                        go.GetComponent<CurtainCall.CharacterArt.RabbitTrialGrounding>().CorrectPose();
                        var skin=stage==0?model.Original:model.Bodies[stage-1];var mesh=new Mesh();
                        try
                        {
                            skin.BakeMesh(mesh,true);float bottom=mesh.vertices.Min(v=>skin.transform.TransformPoint(v).y);
                            Assert.That(bottom,Is.InRange(-.025f,.045f),path+" "+motion+suffix+" support at "+at);
                        }
                        finally{Object.DestroyImmediate(mesh);}
                    }
                }
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
