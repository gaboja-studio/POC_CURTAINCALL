using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
 public sealed partial class FinalCannonPlayTests
 {
  [UnityTest, Category("CommonRulesFollowup")]
  public IEnumerator CommonRopeRules_GroundPiggybackWorksWithoutRopeCourse()
  {
   DisableEnhancementThreats();
   Assert.IsNull(StaticGet("CurtainCall.Tightrope.TightropeCourse","Current"));
   var stack=(Component)StaticGet("CurtainCall.Tightrope.PiggybackSystem","Current");
   Assert.IsNotNull(stack,"모든 씬에서 공통 목마가 생성되어야 한다.");
   var authoredRoot=new GameObject("Authored piggyback lifetime fixture");authoredRoot.SetActive(false);
   var authored=authoredRoot.AddComponent(GameType("CurtainCall.Tightrope.PiggybackSystem"));
   authoredRoot.AddComponent(GameType("CurtainCall.Tightrope.PiggybackFollow"));
   authoredRoot.SetActive(true);yield return null;
   Assert.That(StaticGet("CurtainCall.Tightrope.PiggybackSystem","Current"),Is.SameAs(authored));
   authoredRoot.SetActive(false);yield return null;yield return null;
   Assert.That(StaticGet("CurtainCall.Tightrope.PiggybackSystem","Current"),Is.Not.SameAs(authored));
   authoredRoot.SetActive(true);yield return null;
   Assert.That(StaticGet("CurtainCall.Tightrope.PiggybackSystem","Current"),Is.SameAs(authored));
   Object.Destroy(authoredRoot);yield return null;yield return null;
   stack=(Component)StaticGet("CurtainCall.Tightrope.PiggybackSystem","Current");
   Assert.IsNotNull(stack);
   var prefab=(GameObject)Get(Get(network,"NetworkConfig"),"PlayerPrefab");
   var instance=Object.Instantiate(prefab);
   var remoteObject=instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
   try
   {
    Call(remoteObject,"SpawnWithOwnership",777UL,true);
    var remote=instance.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
    var remoteMover=instance.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
    var position=new Vector3(1.1f,.035f,57);
    Call(remoteMover,"SetStartPose",position,Quaternion.identity,true);
    var replica=instance.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform"));
    Call(replica,"SetStateInternal",position,Quaternion.identity,Vector3.one,true);
    Put(replica,"InboundState",Get(replica,"OutboundState"));
    Call(replica,"TransformStateUpdate");
    Call(mover,"SetStartPose",new Vector3(0,.035f,57),Quaternion.identity,true);
    yield return WaitUntil(()=>((CharacterController)Get(mover,"Body")).isGrounded,3,"바닥 접지");
    yield return null;
    foreach(var actor in new[]{player,remote})
    {
     var support=stack.GetType().GetMethod("GroundSupport",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actor});
     Assert.IsNotNull(support,$"ground support: {Get(actor,"OwnerClientId")} pos={actor.transform.position} state={Get(actor,"State")} jump={Get(actor,"CurrentJump")} local={Get(actor,"IsLocal")}");
    }
    Assert.That(Call(stack,"FindMountTarget",player),Is.SameAs(remote),$"local={player.transform.position}, remote={remote.transform.position}, active={Get(stack,"IsGameplayActive")}, pilot={Get(pilot,"IsDriving")}");
    var scheme=Get(controller,"Scheme");
    var context=Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"),new object[]{player.gameObject});
    var frame=Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
    Put(frame,"InteractTapped",true);
    Call(scheme,"Tick",context,frame);
    yield return null; yield return null;
    Assert.That(Call(stack,"GetBelow",player),Is.SameAs(remote),"일반 바닥 F 탑승");
    var balance=player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
    Assert.IsFalse((bool)Get(balance,"IsActive"));
    Assert.That(player.transform.position.y,Is.GreaterThan(remote.transform.position.y+.5f));
    yield return new WaitForSeconds(.3f);
    Assert.IsTrue((bool)stack.GetType().GetMethod("IsInStack",new[]{player.GetType()}).Invoke(stack,new object[]{player}),"바닥에서 새로 탄 목마는 자동으로 풀리지 않는다.");
    frame=Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));Put(frame,"InteractLongPressed",true);
    Call(scheme,"Tick",context,frame);
    yield return null; yield return null;
    Assert.IsFalse((bool)stack.GetType().GetMethod("IsInStack",new[]{player.GetType()}).Invoke(stack,new object[]{player}));
    Assert.IsTrue((bool)Get(mover,"Simulated"));
    Assert.IsFalse((bool)Get(balance,"IsActive"));
    Assert.That(Mathf.Abs(player.transform.position.y-remote.transform.position.y),Is.LessThan(.15f));
    Assert.That(Vector3.ProjectOnPlane(player.transform.position-remote.transform.position,Vector3.up).magnitude,Is.GreaterThan(.5f));
   }
   finally { if(remoteObject!=null) Call(remoteObject,"Despawn",true); }
  }

  [UnityTest, Category("CommonRulesFollowup")]
  public IEnumerator CommonRopeRules_ActualJumpAndLevelTransferKeepTargetAfterTurn()
  {
   DisableEnhancementThreats();
   var root=new GameObject("Common rope physics fixture");root.SetActive(false);
   var basePoint=new Vector3(9000,9000,9000);
   var first=RopeBox(root,new Vector3(0,-.1f,4)+basePoint);
   var second=RopeBox(root,new Vector3(2,-.1f,4)+basePoint);
   var high=RopeBox(root,new Vector3(-2,.9f,4)+basePoint);
   var course=root.AddComponent(GameType("CurtainCall.Tightrope.TightropeCourse"));
   Put(course,"useSceneGeometry",true);Put(course,"independentSceneRopes",true);
   Put(course,"sceneRopes",new Collider[]{first,second,high});
   JsonUtility.FromJsonOverwrite("{\"laneCount\":3,\"laneEndDistances\":[8,8,8]}",Get(course,"sceneShape"));
   root.AddComponent(GameType("CurtainCall.Tightrope.CourseControlSwitcher"));
   root.AddComponent(GameType("CurtainCall.Tightrope.RopeLaneJumpRules"));
   var balance=(Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
   bool balanceEnabled=balance.enabled;
   try
   {
    root.SetActive(true);balance.enabled=false;
    Call(mover,"SetStartPose",basePoint+new Vector3(0,.035f,4),Quaternion.identity,true);
    yield return WaitUntil(()=>((CharacterController)Get(mover,"Body")).isGrounded,3,"외줄 접지");
    yield return null;yield return null;
    Assert.IsFalse((bool)Get(mover,"FreeMovement"));
    Assert.That(first.size.x,Is.EqualTo(.55f));
    float startY=player.transform.position.y,highest=startY;
    Call(mover,"RequestJump");
    yield return WaitUntil(()=>(bool)Get(mover,"IsAirborne"),2,"공통 외줄 점프 시작");
    float deadline=Time.realtimeSinceStartup+4;
    while((bool)Get(mover,"IsAirborne")&&Time.realtimeSinceStartup<deadline)
    { highest=Mathf.Max(highest,player.transform.position.y);yield return null; }
    Assert.That(highest-startY,Is.EqualTo(1.5f).Within(.08f));
    Call(mover,"RequestLaneJump",-1);
    yield return null;yield return null;
    Assert.IsFalse((bool)Get(mover,"IsAirborne"),"3m 이내여도 높이가 다른 줄로는 이동하지 않는다.");
    Assert.That(player.transform.position.x,Is.EqualTo(basePoint.x).Within(.06f));
    Call(mover,"RequestLaneJump",1);
    yield return WaitUntil(()=>(bool)Get(mover,"IsAirborne"),2,"같은 높이의 옆줄 이동 시작");
    Call(mover,"ReverseCourseDirection");
    yield return WaitUntil(()=>!(bool)Get(mover,"IsAirborne"),5,"T 전환 후 목표 줄 착지");
    Assert.That(player.transform.position.x,Is.EqualTo(basePoint.x+2).Within(.06f));
    Assert.That(player.transform.position.y,Is.EqualTo(basePoint.y).Within(.12f));
    Assert.That(Vector3.Dot((Vector3)Get(mover,"CourseForward"),Vector3.back),Is.GreaterThan(.99f));
    Assert.That(Get(player,"State").ToString(),Is.EqualTo("Normal"));
    Call(mover,"RequestLaneJump",1); // 뒤돌아섰으므로 오른쪽은 원래 줄
    yield return WaitUntil(()=>(bool)Get(mover,"IsAirborne"),2,"원래 줄 이동 시작");
    yield return WaitUntil(()=>!(bool)Get(mover,"IsAirborne"),5,"원래 줄 착지");
    Assert.That(player.transform.position.x,Is.EqualTo(basePoint.x).Within(.06f));
    Assert.That(player.transform.position.y,Is.EqualTo(basePoint.y).Within(.12f));
    Assert.That(Get(player,"State").ToString(),Is.EqualTo("Normal"));
   }
   finally { balance.enabled=balanceEnabled;Object.Destroy(root); }
  }

  [UnityTest, Category("CommonRulesFollowup")]
  public IEnumerator CommonRopeRules_BothEndsAllowForwardBackwardAndTurnOnEveryRopeLayout()
  {
   DisableEnhancementThreats();
   var balance=(Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
   bool balanceEnabled=balance.enabled;
   GameObject root=null;
   try
   {
    balance.enabled=false;
    foreach(bool authored in new[]{true,false})
    {
     var origin=new Vector3(9000,9000,9000);
     root=new GameObject("Two-way rope fixture");root.SetActive(false);root.transform.position=origin;
     var course=root.AddComponent(GameType("CurtainCall.Tightrope.TightropeCourse"));
     Put(course,"useSceneGeometry",authored);Put(course,"independentSceneRopes",authored);
     var shape=Get(course,"sceneShape");
     JsonUtility.FromJsonOverwrite("{\"laneCount\":1,\"finishDistance\":20,\"laneEndDistances\":[20],\"swingGapEnabled\":false,\"relayGapEnabled\":false,\"trapezeEnabled\":false}",shape);
     Put(course,"shape",shape);
     if(authored)
     {
      var rope=RopeBox(root,origin+new Vector3(0,-.1f,10));rope.size=new Vector3(.55f,.2f,20);
      Put(course,"sceneRopes",new Collider[]{rope});
      foreach(float z in new[]{-1.5f,21.5f})
      {
       var deck=new GameObject("End platform",typeof(BoxCollider));deck.transform.SetParent(root.transform,false);
       deck.transform.localPosition=new Vector3(0,-.25f,z);deck.GetComponent<BoxCollider>().size=new Vector3(5,.5f,3);
      }
     }
     root.AddComponent(GameType("CurtainCall.Tightrope.CourseControlSwitcher"));
     root.SetActive(true);
     foreach(int direction in new[]{1,-1})
     {
      var forward=Vector3.forward*direction;
      // 플랫폼에서 실제 이동해 양끝으로 진입한다. 테스트 입력은 카메라 대신 이 축을 제공한다.
      Call(mover,"SetStartPose",origin+new Vector3(0,.035f,direction>0?-.8f:20.8f),Quaternion.LookRotation(forward),true);
      yield return WaitUntil(()=>((CharacterController)Get(mover,"Body")).isGrounded,3,"끝 플랫폼 접지");
      yield return null;yield return null;
      Assert.IsTrue((bool)Get(mover,"FreeMovement"));
      Put(mover,"CourseForwardOverride",forward);
      float timeout=Time.realtimeSinceStartup+4;
      while((bool)Get(mover,"FreeMovement")&&Time.realtimeSinceStartup<timeout)
      { Call(mover,"SetMoveInput",1f);yield return null; }
      Assert.IsFalse((bool)Get(mover,"FreeMovement"),$"{authored}/{direction}: 양끝 진입");
      Assert.That(Vector3.Dot((Vector3)Get(mover,"CourseForward"),forward),Is.GreaterThan(.99f));
      var scheme=Get(controller,"Scheme");
      var context=Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"),new object[]{player.gameObject});
      var input=Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
      Put(input,"Move",1f);
      Vector3 before=player.transform.position;
      float until=Time.realtimeSinceStartup+1.2f;
      while(Time.realtimeSinceStartup<until){Call(scheme,"Tick",context,input);yield return null;}
      Assert.That(Vector3.Dot(player.transform.position-before,forward),Is.GreaterThan(.4f),"전진");
      Put(input,"Move",-1f);before=player.transform.position;until=Time.realtimeSinceStartup+.6f;
      while(Time.realtimeSinceStartup<until){Call(scheme,"Tick",context,input);yield return null;}
      Assert.That(Vector3.Dot(player.transform.position-before,forward),Is.LessThan(-.2f),"T 없이 후진");
      Put(input,"Move",0f);Put(input,"TurnAroundPressed",true);Call(scheme,"Tick",context,input);
      Put(input,"TurnAroundPressed",false);Put(input,"Move",1f);
      timeout=Time.realtimeSinceStartup+4;
      while(!(bool)Get(mover,"FreeMovement")&&Time.realtimeSinceStartup<timeout)
      {Call(scheme,"Tick",context,input);yield return null;}
      Assert.IsTrue((bool)Get(mover,"FreeMovement"),"T 후 W로 진입했던 끝 플랫폼까지 돌아온다.");
      Assert.That(Get(player,"State").ToString(),Is.EqualTo("Normal"));
     }
     Object.Destroy(root);root=null;yield return null;yield return null;
    }
   }
   finally{balance.enabled=balanceEnabled;if(root!=null)Object.Destroy(root);}
  }

  static BoxCollider RopeBox(GameObject root,Vector3 center)
  {
   var go=new GameObject("Rope");go.transform.SetParent(root.transform);go.transform.position=center;
   var box=go.AddComponent<BoxCollider>();box.size=new Vector3(1.85f,.2f,8);return box;
  }
  static void Put(object target,string name,object value)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
   if(target.GetType().GetProperty(name,flags) is PropertyInfo property) property.SetValue(target,value);
   else target.GetType().GetField(name,flags).SetValue(target,value);
  }
 }
}
