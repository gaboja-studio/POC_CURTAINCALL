using System.Reflection;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
 public sealed partial class TightropeSurfaceControlTests
 {
  [Test]
  public void CommonRules_HangTargetUsesRealEndpointAndDoesNotTreatTheRopeAsAWall()
  {
   var ropes=AuthoredRopes();
   MovePlayer(new Vector3(0,-1.1f,4));
   Physics.SyncTransforms();
   Assert.IsTrue(course.TryGetHangTarget(0,player.transform.position,out _,out var point));
   Assert.That(point,Is.EqualTo(origin+Vector3.forward*4));
   Assert.IsTrue(PlayerInteraction.ClearPath(player,point,course.HangTargetRoot(0)));
   Assert.IsTrue(course.TryGetHangTarget(0,origin+new Vector3(0,0,10),out var distance,out point));
   Assert.That(distance,Is.EqualTo(8f).Within(.001f));
   Assert.That(Vector3.Distance(point,origin+new Vector3(0,0,10)),Is.EqualTo(2f).Within(.001f));
   AddBox("hang blocker",new Vector3(0,-.05f,4),new Vector3(2,.05f,2));
   Physics.SyncTransforms();
   Assert.IsFalse(PlayerInteraction.ClearPath(player,origin+Vector3.forward*4,course.HangTargetRoot(0)));
  }
  [TestCase(2f,0f,0f,true)] [TestCase(3f,0f,0f,true)] [TestCase(3.01f,0f,0f,false)]
  [TestCase(2.4f,1.8f,180f,false)] [TestCase(2.4f,1.81f,0f,false)]
  [TestCase(3f,.02f,0f,false)] [TestCase(2f,-.02f,0f,false)]
  [TestCase(2f,0f,180f,true)] [TestCase(2f,0f,90f,true)]
  public void CommonRules_SideTransferUsesSameHeightAndThreeMetreHorizontalDistance(float side,float height,float yaw,bool allowed)
  {
   var ropes=AuthoredRopes();
   ropes[1].transform.position=origin+new Vector3(side,height-.1f,4);
   ropes[1].transform.rotation=Quaternion.Euler(0,yaw,0);
   if(yaw==90) ropes[1].size=new Vector3(.4f,.2f,.5f);
   course.Rebuild(); Physics.SyncTransforms();
   Assert.That(course.TryFindLaneLanding(origin+Vector3.forward*4,Vector3.forward,1,out int lane,out var target),Is.EqualTo(allowed));
   if(allowed) { Assert.That(lane,Is.EqualTo(1)); Assert.That(Vector3.Distance(target,origin+Vector3.forward*4),Is.LessThanOrEqualTo(3.001f)); }
   Assert.That(course.TryFindLaneLanding(origin+Vector3.forward*4,Vector3.back,-1,out _,out _),Is.EqualTo(allowed));
   ropes[1].isTrigger=true;
   Assert.That(course.TryFindLaneLanding(origin+Vector3.forward*4,Vector3.forward,1,out _,out _),Is.False);
   ropes[1].isTrigger=false;
   ropes[1].enabled=false;
   Assert.That(course.TryFindLaneLanding(origin+Vector3.forward*4,Vector3.forward,1,out _,out _),Is.False);
  }

  [Test]
  public void CommonRules_SceneShapeCannotOverrideWidthOrHeightTolerance()
  {
   string saved=JsonUtility.ToJson(GameSettings.Tightrope);
   try {
   JsonUtility.FromJsonOverwrite("{\"ropeMovement\":{\"matchVisualSupportWidth\":false}}",GameSettings.Tightrope);
   var ropes=AuthoredRopes();
   using(var data=new SerializedObject(course))
   {
    Assert.IsNull(data.FindProperty("sceneShape.ropeWalkWidth"));
    Assert.IsNull(data.FindProperty("sceneShape.ropeHeightTolerance"));
    data.ApplyModifiedPropertiesWithoutUndo();
   }
   ropes[0].size=new Vector3(1.85f,.2f,8);
   Invoke(course,"ApplyCommonRopeWidths"); Physics.SyncTransforms();
   Assert.That(course.RopeWalkWidth,Is.EqualTo(.55f));
   Assert.That(course.RopeHeightTolerance,Is.EqualTo(.3f));
   Assert.That(ropes[0].size.x,Is.EqualTo(.55f));
   Assert.That(ropes[0].size.z,Is.EqualTo(8));
   MovePlayer(new Vector3(0,.31f,4));
   Assert.That(course.IsOnRope(player.transform.position,0,out _),Is.False);
   MovePlayer(new Vector3(.8f,0,4));
   Assert.That(course.TryGetSupportingRope(mover,out _),Is.False);
   } finally { JsonUtility.FromJsonOverwrite(saved,GameSettings.Tightrope); }
  }

  [TestCase(1,1)] [TestCase(1,-1)] [TestCase(-1,1)] [TestCase(-1,-1)]
  public void EnteringEitherEndWithWOrSDoesNotReverseHeldMovement(int travel, int key)
  {
   var ropes=AuthoredRopes(); ropes[1].enabled=false; Physics.SyncTransforms();
   Invoke(switcher,"Bind",player);
   MovePlayer(new Vector3(0,0,travel>0?.1f:7.9f));
   // The visual facing may still be rotating toward travel, including during an S entry.
   player.transform.forward=Vector3.back*travel;
   typeof(PlayerMover).GetField("groundVelocity",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(mover,Vector3.forward*travel);
   var reader=player.GetComponent<PlayerInputReader>();
   typeof(PlayerInputReader).GetProperty("Current").SetValue(reader,new PlayerInputFrame {Move=key});
   Invoke(switcher,"UpdateSurfaceMode");
   Assert.IsTrue(switcher.IsLocalOnRope);
   Assert.That(Vector3.Dot(mover.CourseForward*key,Vector3.forward*travel),Is.GreaterThan(.999f));
   var first=mover.CourseForward;
   Invoke(switcher,"UpdateSurfaceMode");
   Assert.That(mover.CourseForward,Is.EqualTo(first));
   var context=new PlayerControlContext(player.gameObject);
   controller.Scheme.Tick(context,new PlayerInputFrame {Move=-key});
   float input=(float)typeof(PlayerMover).GetField("moveInput",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(mover);
   Assert.That(Vector3.Dot(mover.CourseForward*input,Vector3.forward*travel),Is.LessThan(-.999f));
  }

  [Test]
  public void VisualSupportUsesGeneratedThicknessAndAuthoredMeshWithoutChangingLength()
  {
   string saved=JsonUtility.ToJson(GameSettings.Tightrope);
   try {
    JsonUtility.FromJsonOverwrite("{\"ropeMovement\":{\"matchVisualSupportWidth\":true}}",GameSettings.Tightrope);
    Assert.That(course.RopeWalkWidth,Is.EqualTo(course.Shape.RopeThickness));
    var ropes=AuthoredRopes();
    var mesh=Track(new Mesh());
    mesh.vertices=new[]{new Vector3(-.1f,-.1f,-4),new Vector3(.1f,.1f,4)}; mesh.RecalculateBounds();
    ropes[0].gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
    Invoke(course,"ApplyCommonRopeWidths");
    Assert.That(ropes[0].size.x,Is.EqualTo(.2f).Within(.001f));
    Assert.That(ropes[0].size.z,Is.EqualTo(8f));
    Assert.That(course.RopeWidthAt(0),Is.EqualTo(.2f).Within(.001f));
   } finally { JsonUtility.FromJsonOverwrite(saved,GameSettings.Tightrope); }
  }

  [Test]
  public void LandedRopeCanResumeControlsBeforeHostClearsTheFallFlag()
  {
   AuthoredRopes(); MovePlayer(new Vector3(0,0,4));
   var flag=(Unity.Netcode.NetworkVariable<bool>)typeof(CurtainCall.Network.PlayerSync.NetworkPlayer)
    .GetField("droppingFromSupport",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);
   flag.Value=true;
   Invoke(switcher,"Bind",player); Invoke(switcher,"UpdateSurfaceMode");
   Assert.IsTrue(switcher.IsLocalOnRope);
   Assert.IsFalse(mover.FreeMovement);
  }

  [TestCase(.08f)] [TestCase(.2f)] [TestCase(.35f)]
  public void NarrowRopeEdgeIsAlsoARealLandingForTheCommonFallTracker(float offset)
  {
   var ropes=AuthoredRopes(); ropes[0].size=new Vector3(.2f,.2f,8f);
   MovePlayer(new Vector3(offset,0,4));
   Assert.IsTrue(course.TryGetSupportingRope(mover,out _));
   Assert.IsTrue(WalkableSupport.Contains(mover,player.transform.position));
   MovePlayer(new Vector3(2,0,4));
   Assert.IsFalse(WalkableSupport.Contains(mover,player.transform.position));
  }

  [Test]
  public void CommonRules_TurnInputReversesImmediatelyButKeepsAirborneTarget()
  {
   mover.FreeMovement=false; mover.CourseForwardOverride=Vector3.forward;
   mover.CourseJumpHeightOverride=1.5f;
   var target=mover.transform.position+Vector3.right*2;
   mover.LaneLandingTarget=_=>target;
   Invoke(mover,"StartJump",JumpKind.Lane,1);
   var velocity=typeof(PlayerMover).GetField("airVelocity",BindingFlags.Instance|BindingFlags.NonPublic);
   var before=(Vector3)velocity.GetValue(mover);
   var scheme=Track(ScriptableObject.CreateInstance<TightropeControlScheme>());
   var context=new PlayerControlContext(player.gameObject);
   scheme.Tick(context,new PlayerInputFrame{TurnAroundPressed=true});
   Assert.That(mover.CourseForward,Is.EqualTo(Vector3.back));
   Assert.That(Vector3.Dot(player.transform.forward,Vector3.back),Is.GreaterThan(.999f));
   Assert.That((Vector3)velocity.GetValue(mover),Is.EqualTo(before));
   Assert.That((Vector3)Invoke(mover,"LimitToLaneTarget",Vector3.right*10),Is.EqualTo(Vector3.right*2));
   scheme.Tick(context,default);
   Assert.That(mover.CourseForward,Is.EqualTo(Vector3.back));
   scheme.Tick(context,new PlayerInputFrame{TurnAroundPressed=true});
   Assert.That(mover.CourseForward,Is.EqualTo(Vector3.forward));
  }

  [TestCase(2,false)] [TestCase(3,false)] [TestCase(4,false)]
  [TestCase(2,true)] [TestCase(3,true)] [TestCase(4,true)]
  public void FallingRiderDoesNotSnapBackWhileAnOldStackSnapshotIsStillPresent(int count, bool catchesRope)
  {
   const BindingFlags members=BindingFlags.Instance|BindingFlags.NonPublic;
   var current=typeof(PiggybackSystem).GetProperty("Current");
   var previous=current.GetValue(null);
   var local=typeof(CurtainCall.Network.PlayerSync.NetworkPlayer).GetProperty("Local");
   var previousLocal=local.GetValue(null);
   var hangCurrent=typeof(TightropeHangSystem).GetProperty("Current");
   var previousHang=hangCurrent.GetValue(null);
   var all=(System.Collections.Generic.List<CurtainCall.Network.PlayerSync.NetworkPlayer>)typeof(CurtainCall.Network.PlayerSync.NetworkPlayer)
    .GetField("all",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
   var added=new System.Collections.Generic.List<CurtainCall.Network.PlayerSync.NetworkPlayer>();
   var stack=MakeObject("Pending fall unlink").AddComponent<PiggybackSystem>();
   var follow=stack.gameObject.AddComponent<PiggybackFollow>();
   var below=(System.Collections.Generic.Dictionary<ulong,ulong>)typeof(PiggybackSystem).GetField("belowOf",members).GetValue(stack);
   var above=(System.Collections.Generic.Dictionary<ulong,ulong>)typeof(PiggybackSystem).GetField("aboveOf",members).GetValue(stack);
   try
   {
    current.SetValue(null,stack); local.SetValue(null,player);
    typeof(CurtainCall.Network.PlayerSync.NetworkPlayer).GetProperty("IsOwner").SetValue(player,true);
    typeof(CurtainCall.Network.PlayerSync.NetworkPlayer).GetProperty("OwnerClientId").SetValue(player,(ulong)(count-1));
    for(int i=0;i<count;i++)
    {
     var rider=i==count-1?player:MakeObject("Stack rider "+i).AddComponent<CurtainCall.Network.PlayerSync.NetworkPlayer>();
     if(rider!=player) { Invoke(rider.GetComponent<PlayerMover>(),"Awake"); Invoke(rider,"Awake"); }
     typeof(CurtainCall.Network.PlayerSync.NetworkPlayer).GetProperty("OwnerClientId").SetValue(rider,(ulong)i);
     if(!all.Contains(rider)) { all.Add(rider); added.Add(rider); }
     if(i>0) { below[(ulong)i]=(ulong)(i-1); above[(ulong)(i-1)]=(ulong)i; }
    }
    Invoke(follow,"Awake");
    typeof(PiggybackFollow).GetField("localMover",members).SetValue(follow,mover);
    mover.Simulated=false;
    if(catchesRope)
    {
     var go=MakeObject("Catch preceding unlink"); go.SetActive(false);
     var hangs=go.AddComponent<TightropeHangSystem>();
     var book=typeof(TightropeHangSystem).GetField("book",members).GetValue(hangs);
     book.GetType().GetMethod("Begin").Invoke(book,new object[]{player.OwnerClientId,0,4f,7d,0d});
     hangCurrent.SetValue(null,hangs);
    }
    else Invoke(follow,"ReleaseForFall",player);
    mover.ResetExternalMotion(true);
    var fallingPosition=origin+new Vector3(2,-1,4);
    player.transform.position=fallingPosition;
    for(int frame=0;frame<3;frame++) Invoke(follow,"LateUpdate");
    Assert.That(player.transform.position,Is.EqualTo(fallingPosition),"A stale parent must never pull a falling owner back onto a head.");
    Assert.That(mover.Simulated,Is.EqualTo(!catchesRope),"A rope catch keeps movement owned by the hang system.");
    Assert.IsTrue(mover.IsAirborne);
    Assert.IsNotNull(stack.GetBelow(player),"The test intentionally delays the unlink snapshot.");
    below.Clear(); above.Clear(); Invoke(follow,"LateUpdate");
    Assert.IsNull(typeof(PiggybackFollow).GetField("fallingLocal",members).GetValue(follow));
    Assert.That(player.transform.position,Is.EqualTo(fallingPosition));
   }
   finally
   {
    foreach(var rider in added) all.Remove(rider);
    hangCurrent.SetValue(null,previousHang);
    local.SetValue(null,previousLocal); current.SetValue(null,previous);
   }
  }
 }
}
