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
            try{camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1100,850,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1100,850),0,0);tex.Apply();
                string directory=Environment.GetEnvironmentVariable("CURTAINCALL_MOTION_EVIDENCE_DIR")??".harness/local/cannon-motion";
                System.IO.Directory.CreateDirectory(directory);System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,Shot+".png"),tex.EncodeToPNG());Destroy(tex);}
            finally{camera.targetTexture=null;RenderTexture.active=old;Destroy(go);rt.Release();Destroy(rt);Shot=null;}
        }
    }

    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator CircusGrounding_CannonLandingUsesInjuredSupport()
        {
            DisableEnhancementThreats();var probe=MotionProbe();
            for(int stage=1;stage<4;stage++)
            {
                Call(player,"ServerRestoreAll");Call(player,"ServerCut",stage);yield return new WaitForSeconds(.2f);
                int before=SafeLandings;
                yield return WaitUntil(()=>(bool)Get(Call(system,"Station",6),"ReadyToUse"),22,"cannon cooldown before next injury case");
                yield return Board(6);yield return ReleaseThenArm();Input(tapped:true);
                yield return WaitUntil(()=>Flying,2,"launch");
                yield return WaitUntil(()=>SafeLandings>before,8,"injured safe landing");
                yield return new WaitForSeconds(.18f);
                var model=(Component)Get(probe.View,"Model");
                for(int sample=0;sample<4;sample++)
                {
                    Assert.AreEqual("FlightLand_Cut"+stage,Get(probe.View,"CircusMotion"));
                    AssertGroundSupport(model,probe.Animator,stage,$"cannon landing {stage}/{sample}");
                    yield return new WaitForSeconds(.1f);
                }
                yield return new WaitForSeconds(.5f);
                Assert.IsNull(Get(probe.View,"CircusMotion"));
            }
        }

        [UnityTest]
        public IEnumerator CircusGrounding_InjuryCarryPlantsSupportWithoutLosingGrip()
        {
            DisableEnhancementThreats();var probe=MotionProbe();
            var t=(Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline","Current");
            for(int character=0;character<4;character++)
            for(int stage=1;stage<4;stage++)
            {
                Call(player,"RequestAppearance",character,2);
                Call(player,"ServerRestoreAll");Call(player,"ServerCut",stage);yield return new WaitForSeconds(.2f);
                var handle=(Transform)Get(t,"Handle");
                Call(mover,"SetStartPose",handle.position,Quaternion.identity,true);yield return new WaitForSeconds(.15f);
                Call(t,"RequestGrab");yield return WaitUntil(()=>(ulong)Get(t,"Carrier")==Actor,3,"injured carry ownership");
                yield return new WaitForSeconds(.5f);
                var model=(Component)Get(probe.View,"Model");
                Assert.AreEqual("TrampolineCarry_Cut"+stage,Get(model,"Motion"));
                if(stage<3)Assert.Less(probe.LeftError,.06f,$"left grip {character}/{stage}");
                Assert.Less(probe.RightError,.06f,$"right grip {character}/{stage}");
                AssertGroundSupport(model,probe.Animator,stage,$"carry {character}/{stage}");
                Call(mover,"RequestJump");yield return WaitUntil(()=>(bool)Get(mover,"IsAirborne"),2,"injured jump");
                yield return new WaitForSeconds(.12f);
                Assert.AreEqual("CarryJump_Cut"+stage,Get(model,"Motion"));
                Assert.Less(probe.RightError,.06f,"jump retains handle");
                yield return WaitUntil(()=>!(bool)Get(mover,"IsAirborne"),3,"land from carry jump");
                yield return new WaitForSeconds(.5f);
                AssertGroundSupport(model,probe.Animator,stage,$"after jump {character}/{stage}");
                Call(t,"RequestDrop");yield return WaitUntil(()=>(ulong)Get(t,"Carrier")==ulong.MaxValue,2,"drop");
                t.transform.position=new Vector3(0,0,57);Call(t,"ResetPreviousPose");yield return null;
            }
        }

        static void AssertGroundSupport(Component model,Animator animator,int stage,string label)
        {
            var bodies=(SkinnedMeshRenderer[])model.GetType().GetField("Bodies").GetValue(model);
            var skin=bodies[stage-1];var bones=skin.bones;var weights=skin.sharedMesh.boneWeights;
            var ids=stage==1?new[]{HumanBodyBones.RightFoot,HumanBodyBones.RightToes}:
                new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg};
            bool Selected(int index){foreach(var id in ids)if(bones[index]==animator.GetBoneTransform(id))return true;return false;}
            float W(int index,float weight)=>Selected(index)?weight:0;
            var mesh=new Mesh();
            try
            {
                skin.BakeMesh(mesh,false);var vertices=mesh.vertices;float lowest=float.PositiveInfinity;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];if(W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)<=.5f)continue;
                    lowest=Mathf.Min(lowest,skin.transform.TransformPoint(vertices[i]).y);
                }
                Assert.That(lowest-model.transform.position.y,Is.InRange(-.012f,.02f),label+" actual load-bearing surface");
            }
            finally{UnityEngine.Object.Destroy(mesh);}
        }

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
        [Category("CommonMotionRegression")]
        public IEnumerator CircusMotion_RemoteGroundedStateIgnoresUnsimulatedLocalFall()
        {
            var prefab=(GameObject)Get(Get(network,"NetworkConfig"),"PlayerPrefab");
            var root=UnityEngine.Object.Instantiate(prefab,new Vector3(9000,100,9000),Quaternion.identity);
            var obj=root.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            var otherMover=root.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            Call(otherMover,"ResetExternalMotion",true);
            Call(obj,"SpawnWithOwnership",904UL,true);
            try
            {
                yield return null;yield return null;
                Call(otherMover,"ResetExternalMotion",true);
                yield return null;yield return null;
                var other=root.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                Assert.IsFalse((bool)Get(other,"IsOwner"));
                Assert.AreEqual("None",Get(other,"CurrentJump").ToString());
                Assert.IsTrue((bool)Get(otherMover,"IsAirborne"),"An obsolete, unsimulated local sample is intentionally retained.");
                var view=root.GetComponent(GameType("CurtainCall.Player.HumanoidPlayerPresentation"));
                Assert.That((string)Get(Get(view,"Model"),"Motion"),Is.EqualTo("Idle"),
                    "Only the owner's replicated jump state may select a remote fall animation.");
            }
            finally{Call(obj,"Despawn",true);}
        }

        [UnityTest]
        [Category("CommonMotionRegression")]
        public IEnumerator CircusMotion_LandingYieldsToTheNextOrdinaryJump()
        {
            DisableEnhancementThreats(); var probe=MotionProbe();
            int before=SafeLandings;
            yield return Board(6); yield return ReleaseThenArm(); Input(tapped:true);
            yield return WaitUntil(()=>Flying,2,"launch");
            yield return WaitUntil(()=>SafeLandings>before,8,"actual safe landing");
            yield return WaitUntil(()=>((CharacterController)Get(mover,"Body")).isGrounded,1,"landing support");
            Call(mover,"RequestJump");
            yield return WaitUntil(()=>(bool)Get(mover,"IsAirborne"),1,"next ordinary jump");
            yield return null;
            Assert.That(new[]{"JumpStart","JumpLoop"},Does.Contain((string)Get(Get(probe.View,"Model"),"Motion")),
                "The previous cannon landing must not cover a new ordinary jump.");
            Assert.IsNull(Get(probe.View,"CircusMotion"));
        }

        [UnityTest]
        [Category("CommonMotionRegression")]
        public IEnumerator CircusMotion_RestartClearsPreviousFlightPresentation()
        {
            DisableEnhancementThreats(); var probe=MotionProbe();
            yield return Board(0); yield return ReleaseThenArm(); Input(tapped:true);
            yield return WaitUntil(()=>Flying,2,"launch");
            yield return new WaitForSeconds(.2f);
            Assert.IsNotNull(Get(probe.View,"CircusMotion"));
            Call(player,"RequestRestartAll");
            yield return WaitUntil(()=>!Flying,2,"restart leaves flight");
            yield return null; yield return null;
            Assert.IsNull(Get(probe.View,"CircusMotion"),"Restart is a new pose, not a cannon landing.");
            Assert.That((string)Get(Get(probe.View,"Model"),"Motion"),Is.Not.EqualTo("FlightLand"));
        }

        [UnityTest]
        [Category("CommonMotionRegression")]
        public IEnumerator CircusMotion_TransportDoesNotKeepWalkingInPlace()
        {
            DisableEnhancementThreats(); var probe=MotionProbe();
            yield return WaitUntil(()=>((CharacterController)Get(mover,"Body")).isGrounded,3,"floor");
            for(int i=0;i<3;i++){Call(mover,"SetMoveInput",1f,0f);yield return null;}
            Assert.IsTrue((bool)Get(mover,"IsMoving"));
            Call(pilot,"BeginTransport");
            try
            {
                yield return null; yield return null;
                Assert.That((string)Get(Get(probe.View,"Model"),"Motion"),Is.EqualTo("Idle"),
                    "Standing on a driven platform must not replay the previous walking input.");
            }
            finally{Call(pilot,"EndTransport");}
        }

        [UnityTest]
        [Category("CommonMotionRegression")]
        public IEnumerator CircusMotion_CarryLayerRecoversAfterAnimatorRebind()
        {
            DisableEnhancementThreats(); var probe=MotionProbe();
            var t=(Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline","Current");
            var handle=(Transform)Get(t,"Handle");
            Call(mover,"SetStartPose",handle.position,Quaternion.identity,true);yield return null;
            Call(t,"RequestGrab");yield return WaitUntil(()=>(ulong)Get(t,"Carrier")==Actor,3,"trampoline carry");
            yield return new WaitForSeconds(.25f);
            probe.Animator.Rebind();probe.Animator.Update(0f);
            yield return null;yield return null;
            Assert.IsTrue(probe.Animator.GetCurrentAnimatorStateInfo(1).IsName("TrampolineCarry")
                || probe.Animator.GetNextAnimatorStateInfo(1).IsName("TrampolineCarry"),
                "The carry layer must recover its actual state after rebind, even when its cache is unchanged.");
        }

        [UnityTest, Category("CommonMotionVisual")]
        public IEnumerator CommonMotion_FourCharactersWalkAndJumpThroughEveryLivingInjuryStage()
        {
            DisableEnhancementThreats(); var probe=MotionProbe();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="Common motion audit floor";floor.transform.position=new Vector3(15000,1000,15000);
            floor.transform.localScale=new Vector3(30,1,30);
            var lightRoot=new GameObject("Common motion audit light");
            var light=lightRoot.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2f;
            light.transform.rotation=Quaternion.Euler(50,-30,0);
            try
            {
                for(int character=0;character<4;character++) for(int stage=0;stage<4;stage++)
                {
                    Call(player,"RequestAppearance",character,2);Call(player,"ServerRestoreAll");
                    if(stage>0)Call(player,"ServerCut",stage);
                    Call(mover,"SetStartPose",floor.transform.position+Vector3.up*.55f,Quaternion.identity,true);
                    yield return WaitUntil(()=>((CharacterController)Get(mover,"Body")).isGrounded,3,"audit support");
                    float until=Time.time+.45f;
                    while(Time.time<until){Call(mover,"SetMoveInput",0f,1f);yield return null;}
                    probe.Shot=$"common-walk-{character}-cut{stage}";probe.ShotCenter=player.transform.position+Vector3.up*.65f;probe.ShotSize=1.4f;
                    yield return WaitUntil(()=>probe.Shot==null,1,"walk evidence");
                    yield return new WaitForSeconds(.2f);
                    Call(mover,"RequestJump");
                    yield return WaitUntil(()=>(bool)Get(mover,"IsAirborne"),1,"ordinary jump");
                    yield return new WaitForSeconds(.08f);
                    probe.Shot=$"common-jump-{character}-cut{stage}";probe.ShotCenter=player.transform.position+Vector3.up*.65f;
                    yield return WaitUntil(()=>probe.Shot==null,1,"jump evidence");
                    Assert.Greater(probe.Animator.speed,.01f,"A surviving-limb jump must not freeze in a ground crawl pose.");
                    yield return WaitUntil(()=>!(bool)Get(mover,"IsAirborne"),3,"ordinary landing");
                    yield return new WaitForSeconds(.25f);
                    Assert.AreEqual("Normal",Get(player,"State").ToString());
                    Assert.AreEqual(stage,Convert.ToInt32(Get(player.GetComponent(GameType("CurtainCall.Player.PlayerCondition")),"LostCount")),"An ordinary jump must not add injury.");
                }
            }
            finally{UnityEngine.Object.Destroy(floor);UnityEngine.Object.Destroy(lightRoot);}
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

