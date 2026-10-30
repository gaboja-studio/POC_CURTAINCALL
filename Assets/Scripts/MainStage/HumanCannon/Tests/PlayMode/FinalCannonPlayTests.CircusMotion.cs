using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    [DefaultExecutionOrder(2000)]
    public sealed class CannonMotionProbe : MonoBehaviour
    {
        public Component View;
        public float LeftError, RightError, TorsoForward;
        public Animator Animator;
        public Vector3 Head;
        public string Shot;
        public Vector3 ShotCenter;
        public float ShotSize=2;
        public int Samples;
        void LateUpdate()
        {
            if(View==null)return;
            var t=View.GetType();var model=t.GetProperty("Model").GetValue(View) as Component;
            if(model==null)return;
            Animator=(Animator)model.GetType().GetField("Animator").GetValue(model);
            if(!Animator.enabled)return;
            Vector3 P(HumanBodyBones b)=>Animator.GetBoneTransform(b).position;
            Head=P(HumanBodyBones.Head);
            TorsoForward=Vector3.Dot((Head-P(HumanBodyBones.Hips)).normalized,transform.forward);
            foreach(bool left in new[]{true,false}){
                var hand=P(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var finger=Animator.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
                var palm=finger!=null?Vector3.Lerp(hand,finger.position,.7f):hand;
                var contact=(Vector3)t.GetProperty(left?"LeftCarryContact":"RightCarryContact").GetValue(View);
                if(left)LeftError=Vector3.Distance(palm,contact);else RightError=Vector3.Distance(palm,contact);
            }
            Samples++;
            if(Shot==null)return;
            var go=new GameObject("Motion Evidence Camera");var camera=go.AddComponent<Camera>();
            var rt=new RenderTexture(1100,850,24);camera.targetTexture=rt;
            camera.orthographic=true;camera.orthographicSize=ShotSize;camera.nearClipPlane=.02f;camera.farClipPlane=150;
            camera.transform.position=ShotCenter+transform.right*4+Vector3.up*1.8f+transform.forward*6;camera.transform.LookAt(ShotCenter);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.19f);
            var old=RenderTexture.active;
            try{camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1100,850,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1100,850),0,0);tex.Apply();System.IO.File.WriteAllBytes("Tasks/Active/Task-20261030-083-cannon-character-animation/Previews/"+Shot+".png",tex.EncodeToPNG());Destroy(tex);}
            finally{camera.targetTexture=null;RenderTexture.active=old;Destroy(go);rt.Release();Destroy(rt);Shot=null;}
        }
    }

    public sealed partial class FinalCannonPlayTests
    {
        CannonMotionProbe MotionProbe()
        {
            var probe=player.gameObject.AddComponent<CannonMotionProbe>();
            probe.View=player.GetComponent(GameType("CurtainCall.Player.HumanoidPlayerPresentation"));
            return probe;
        }

        [UnityTest]
        public IEnumerator CircusMotion_CarryJumpKeepsSurvivingHandsAndCutSeatUsesMatchingClip()
        {
            DisableEnhancementThreats();var probe=MotionProbe();
            var t=(Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline","Current");
            Assert.IsTrue((bool)Get(t,"UsePropOutline"));
            for(int stage=0;stage<4;stage++)
            {
                Call(player,"ServerRestoreAll");if(stage>0)Call(player,"ServerCut",stage);
                yield return null;
                string suffix=stage==0?"":"_Cut"+stage;
                var handle=(Transform)Get(t,"Handle");
                Call(mover,"SetStartPose",handle.position,Quaternion.identity,true);yield return new WaitForSeconds(.15f);
                Call(t,"RequestGrab");yield return WaitUntil(()=>(ulong)Get(t,"Carrier")==Actor,3,"cut grip ownership");
                yield return new WaitForSeconds(.4f);
                Call(mover,"RequestJump");
                yield return WaitUntil(()=>(bool)Get(mover,"IsAirborne"),2,"carry jump");
                int samples=0;
                while((bool)Get(mover,"IsAirborne"))
                {
                    yield return null;
                    if(!(bool)Get(mover,"IsAirborne"))break;
                    Assert.AreEqual(1f,probe.Animator.GetLayerWeight(1));
                    Assert.AreEqual("CarryJump"+suffix,Get(Get(probe.View,"Model"),"Motion"));
                    if(stage<3)Assert.Less(probe.LeftError,.06f,"jump left palm stage "+stage);
                    Assert.Less(probe.RightError,.06f,"jump right palm stage "+stage);
                    if(samples++==3){probe.Shot="play-carry-jump-cut"+stage;probe.ShotCenter=player.transform.position+Vector3.up*.7f;probe.ShotSize=2.5f;}
                    Assert.Less(samples,1000,"jump must finish");
                }
                Assert.Greater(samples,3);
                Call(t,"RequestDrop");yield return WaitUntil(()=>(ulong)Get(t,"Carrier")==ulong.MaxValue,2,"drop");
                t.transform.position=new Vector3(0,0,57);Call(t,"ResetPreviousPose");yield return null;
                yield return Board(0);yield return new WaitForSeconds(.35f);
                Assert.AreEqual("CannonLoaded"+suffix,Get(probe.View,"CircusMotion"));
                var muzzle=(Transform)Get(Call(system,"Station",0),"Muzzle");
                Assert.Less(Vector3.Distance(probe.Head,muzzle.position),.3f);
                yield return ReleaseThenArm();Input(held:true,progress:1);yield return new WaitForSeconds(.2f);
                Assert.IsNull(Get(probe.View,"CircusMotion"));
            }
        }

        [UnityTest]
        public IEnumerator CircusMotion_AllFourCharactersGripAndSeatAtActualMuzzle()
        {
            DisableEnhancementThreats();
            var probe=MotionProbe();
            var t=(Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline","Current");
            for(int i=0;i<4;i++)
            {
                Call(player,"RequestAppearance",i,2);yield return new WaitForSeconds(.2f);
                var handle=(Transform)Get(t,"Handle");
                Call(mover,"SetStartPose",handle.position,Quaternion.identity,true);yield return null;
                Call(t,"RequestGrab");yield return WaitUntil(()=>(ulong)Get(t,"Carrier")==Actor,3,"grip ownership");
                yield return new WaitForSeconds(.4f);
                Assert.IsTrue((bool)Get(probe.View,"HasCarryContacts"));
                Assert.Less(probe.LeftError,.035f,"left palm on actual bar model "+i);
                Assert.Less(probe.RightError,.035f,"right palm on actual bar model "+i);
                probe.Shot="play-grip-"+i;probe.ShotCenter=player.transform.position+player.transform.forward*.8f+Vector3.up*.9f;probe.ShotSize=2.5f;
                yield return WaitUntil(()=>probe.Shot==null,2,"grip evidence");
                Call(t,"RequestDrop");
                yield return WaitUntil(()=>(ulong)Get(t,"Carrier")==ulong.MaxValue,2,"released ownership");
                // Keep the just-dropped bounce surface away from the next boarding fixture.
                t.transform.position=new Vector3(0,0,57);Call(t,"ResetPreviousPose");yield return null;
                yield return Board(0);yield return new WaitForSeconds(.3f);
                Assert.AreEqual("CannonLoaded",Get(probe.View,"CircusMotion"));
                Assert.Greater(probe.TorsoForward,.60f,"prone body "+i);
                var station=(Component)Call(system,"Station",0);var muzzle=(Transform)Get(station,"Muzzle");
                Assert.Less(Vector3.Distance(probe.Head,muzzle.position),.30f,"head in padded muzzle "+i);
                probe.Shot="play-seat-"+i;probe.ShotCenter=station.transform.position+Vector3.up*1.2f;probe.ShotSize=2;
                yield return WaitUntil(()=>probe.Shot==null,2,"seat evidence");
                yield return ReleaseThenArm();Input(held:true,progress:1);yield return new WaitForSeconds(.2f);
                Assert.IsFalse((bool)Call(system,"IsSeated",Actor));
                Assert.IsNull(Get(probe.View,"CircusMotion"));
            }
        }

        [UnityTest]
        public IEnumerator CircusMotion_SafeLandingAbsorbsThenReturnsToLocomotion()
        {
            DisableEnhancementThreats(); var probe=MotionProbe();
            int before=SafeLandings;
            yield return Board(6); yield return ReleaseThenArm(); Input(tapped:true);
            yield return WaitUntil(()=>Flying,2,"launch");
            yield return WaitUntil(()=>SafeLandings>before,8,"real safe landing");
            yield return null;
            Assert.AreEqual("FlightLand",Get(probe.View,"CircusMotion"));
            yield return new WaitForSeconds(.18f);
            probe.Shot="play-land";probe.ShotCenter=player.transform.position+Vector3.up*.85f;probe.ShotSize=1.5f;
            yield return WaitUntil(()=>probe.Shot==null,2,"landing evidence");
            yield return new WaitForSeconds(.8f);
            Assert.IsNull(Get(probe.View,"CircusMotion"));
            AssertBody(0); Assert.IsFalse(Flying);
        }

        [UnityTest]
        public IEnumerator CircusMotion_RealLaunchApexFallAndTrampolineRebound()
        {
            DisableEnhancementThreats();var probe=MotionProbe();
            yield return Board(0);yield return ReleaseThenArm();Input(tapped:true);
            yield return WaitUntil(()=>Flying,2,"launch");yield return new WaitForSeconds(.24f);
            Assert.AreEqual("CannonRise",Get(probe.View,"CircusMotion"));
            yield return WaitUntil(()=>Equals(Get(probe.View,"CircusMotion"),"FlightApex"),5,"apex transition");
            yield return WaitUntil(()=>Equals(Get(probe.View,"CircusMotion"),"PanicFall"),3,"shared panic fall");
            probe.Shot="play-panic";probe.ShotCenter=player.transform.position+Vector3.up;probe.ShotSize=1.6f;
            yield return WaitUntil(()=>probe.Shot==null,2,"fall evidence");
            // Put the real trampoline under this falling player; host must authorize its actual contact.
            var t=(Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline","Current");
            int before=(int)Get(t,"LocalBounceCount");
            t.transform.position=new Vector3(player.transform.position.x,player.transform.position.y-1.2f,player.transform.position.z+1f);
            Call(t,"ResetPreviousPose");
            yield return WaitUntil(()=>(int)Get(t,"LocalBounceCount")>before,3,"actual falling contact rebound");
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual("TrampolineRise",Get(probe.View,"CircusMotion"));
            yield return WaitUntil(()=>Equals(Get(probe.View,"CircusMotion"),"PanicFall"),5,"rebound shares fall animation");
        }
    }
}

