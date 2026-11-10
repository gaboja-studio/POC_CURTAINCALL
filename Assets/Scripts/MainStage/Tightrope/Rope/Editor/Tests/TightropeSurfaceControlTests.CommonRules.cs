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
 }
}
