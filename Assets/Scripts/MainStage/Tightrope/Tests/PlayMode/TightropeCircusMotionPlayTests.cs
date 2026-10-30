using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    [DefaultExecutionOrder(2000)]
    public sealed class RescueMotionProbe : MonoBehaviour
    {
        public Animator Animator;
        public Vector3 Left,Right,Head;
        public Action Capture;
        void LateUpdate()
        {
            if(Animator==null)return;
            Vector3 Palm(bool left)
            {
                var h=Animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var f=Animator.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
                return f!=null?Vector3.Lerp(h.position,f.position,.7f):h.position;
            }
            Left=Palm(true);Right=Palm(false);Head=Animator.GetBoneTransform(HumanBodyBones.Head).position;
            var capture=Capture;Capture=null;capture?.Invoke();
        }
    }
    public sealed partial class TightropeCampaignPlayTests
    {
        static Animator RescueAnimator(Component view) => (Animator)Get(Get(view,"Model"),"Animator");
        static Vector3 RescuePalm(Animator a,bool left)
        {
            var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var finger=a.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
            return finger!=null?Vector3.Lerp(hand.position,finger.position,.7f):hand.position;
        }
        void RescueShot(string name,Vector3 center)
        {
            var go=new GameObject("Rescue evidence");var camera=go.AddComponent<Camera>();
            var rt=new RenderTexture(1100,850,24);var previous=RenderTexture.active;
            camera.targetTexture=rt;camera.orthographic=true;camera.orthographicSize=2;
            var direction=(Vector3)Get(course,"Forward");
            camera.transform.position=center+Vector3.Cross(Vector3.up,direction)*5+Vector3.up*1.4f-direction*2;
            camera.transform.LookAt(center);camera.nearClipPlane=.02f;camera.farClipPlane=150;
            try
            {
                camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1100,850,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1100,850),0,0);tex.Apply();
                string directory=Environment.GetEnvironmentVariable("CURTAINCALL_MOTION_EVIDENCE_DIR")
                    ?? "Tasks/Active/Task-20261030-083-cannon-character-animation/Previews";
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),tex.EncodeToPNG());Object.Destroy(tex);
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.Destroy(rt);Object.Destroy(go);}
        }

        [UnityTest]
        public IEnumerator CircusMotion_RopeRescueUsesBothRolesAndAllSurvivingLimbVariants()
        {
            yield return PrepareHangRescue();
            var hangs=(Component)Static("CurtainCall.Tightrope.TightropeHangSystem","Current");
            for(int stage=0;stage<4;stage++)
            {
                Call(player,"ServerRestoreAll");if(stage>0)Call(player,"ServerCut",stage);
                yield return StandOnRopeForSawTest(1,4f);
                var helper=SpawnHandholdHelper(1,6.2f);
                var target=helper.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                try
                {
                    yield return null;
                    // This synthetic remote peer is not a campaign damage participant.
                    // Use the real authoritative restoration API to seed its replicated injury state.
                    if(stage>0){Call(target,"ServerSetOneArm");for(int restore=stage;restore<3;restore++)Call(target,"ServerRestorePart");}
                    yield return null;
                    var view=player.GetComponent(GameType("CurtainCall.Player.HumanoidPlayerPresentation"));
                    var targetView=target.GetComponent(GameType("CurtainCall.Player.HumanoidPlayerPresentation"));
                    var probe=player.gameObject.AddComponent<RescueMotionProbe>();probe.Animator=RescueAnimator(view);
                    var other=target.gameObject.AddComponent<RescueMotionProbe>();other.Animator=RescueAnimator(targetView);
                    Assert.IsTrue((bool)Call(hangs,"ServerTryHang",target,6.2f,1));
                    yield return new WaitForSeconds(.25f);
                    string suffix=stage==0?"":"_Cut"+stage;
                    Assert.AreEqual("RopeHang"+suffix,Get(Get(targetView,"Model"),"Motion"));
                    if(stage==0||stage==3)
                    {
                        Call(hangs,"ServerRescue",Get(player,"OwnerClientId"),Get(target,"OwnerClientId"));
                        yield return new WaitForSeconds(.3f);
                        Call(hangs,"ServerCancel",Get(player,"OwnerClientId"));
                        yield return new WaitForSeconds(.35f);
                        Assert.IsNull(Get(view,"RescueMotion"),"cancel releases helper");
                        Assert.AreEqual("RopeHang"+suffix,Get(Get(targetView,"Model"),"Motion"),"cancel returns receiver to surviving-arm hang");
                    }
                    Call(hangs,"ServerRescue",Get(player,"OwnerClientId"),Get(target,"OwnerClientId"));
                    yield return new WaitForSeconds(.4f);
                    Assert.AreEqual("RopePull"+suffix,Get(view,"RescueMotion"));
                    Assert.AreEqual("RopeLift"+suffix,Get(targetView,"RescueMotion"));
                    var a=RescueAnimator(view);var b=RescueAnimator(targetView);
                    var grip=(Vector3)Call(course,"GetLanePosition",1,6.2f);
                    probe.Capture=()=>RescueShot("play-rope-reach-cut"+stage,grip);
                    yield return null;
                    Assert.Less(Mathf.Abs(a.GetCurrentAnimatorStateInfo(0).normalizedTime-b.GetCurrentAnimatorStateInfo(0).normalizedTime),.08f,"shared pull progress");
                    float startHead=other.Head.y;
                    float separation=stage==3?Vector3.Distance(probe.Right,other.Right):
                        Mathf.Min(Vector3.Distance(probe.Right,other.Right),Vector3.Distance(probe.Right,other.Left));
                    Assert.Less(separation,.16f,"remaining hands meet stage "+stage+" helper "+probe.Right+" receiver "+other.Left+"/"+other.Right+" grip "+grip);
                    yield return new WaitForSeconds(.85f);
                    Assert.Greater(other.Head.y,startHead+.25f,"receiver visibly rises");
                    probe.Capture=()=>RescueShot("play-rope-pull-cut"+stage,grip+Vector3.up*.3f);
                    yield return Until(()=>!(bool)Call(hangs,"IsRescuing",player),4,"rescue complete");
                    yield return new WaitForSeconds(.4f);
                    Assert.IsFalse((bool)Call(hangs,"IsHanging",target));
                    Assert.IsNull(Get(view,"RescueMotion"));Assert.IsNull(Get(targetView,"RescueMotion"));
                    Object.Destroy(probe);
                }
                finally{if(helper!=null)Call(helper,"Despawn",true);}
            }
        }
    }
}
