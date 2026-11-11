using System.Reflection;
using CurtainCall.Network.PlayerSync;
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
  [Category("PiggybackRopeFlow")]
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
  [Category("PiggybackRopeFlow")]
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

  [Test, Category("PiggybackRopeFlow")]
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

  [TestCase(false, false)] [TestCase(false, true)]
  [TestCase(true, false)] [TestCase(true, true)]
  [Category("PiggybackRopeFlow")]
  public void FallenRiderRegainsSoloRopeInputBeforeOldMountLinkArrives(bool tickWhileFalling, bool reverse)
  {
   WithMountedPair((stack, follow, passenger) =>
   {
    AuthoredRopes();
    MovePlayer(new Vector3(0, 0, 7));
    Assert.IsTrue(course.TryGetSupportingRope(mover, out _), "The initial rope sits beyond the fixture's floor.");
    Invoke(follow, "LateUpdate");
    var rider = passenger.GetComponent<NetworkPlayer>();
    var riderBalance = rider.gameObject.AddComponent<PlayerBalance>();
    var riderController = rider.gameObject.AddComponent<PlayerController>();
    Invoke(riderBalance, "Awake"); Invoke(riderController, "Awake");
    Invoke(switcher, "Bind", rider); Invoke(switcher, "UpdateSurfaceMode");
    Assert.IsTrue(switcher.IsLocalOnRope, "A rider on a carrier standing on the rope starts with rope controls.");
    if (reverse) passenger.ReverseCourseDirection();
    var originalForward = passenger.CourseForward;

    Invoke(follow, "ReleaseForFall", rider);
    const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
    var localDrop = typeof(NetworkPlayer).GetField("localSupportDrop", fields);
    localDrop.SetValue(rider, true);
    passenger.FreeMovement = true;
    riderBalance.SetBalanceActive(false);
    passenger.ResetExternalMotion(true);
    if (tickWhileFalling)
    {
     Invoke(switcher, "UpdateSurfaceMode");
     Assert.IsFalse(switcher.IsLocalOnRope, "The real fall runs with the normal air controls.");
     rider.transform.forward = -originalForward;
    }
    // The carrier can leave this rope before the old link snapshot is cleared.
    MovePlayer(new Vector3(0, 0, -2));
    rider.transform.position = origin + Vector3.forward * 7;
    passenger.ResetExternalMotion(false);
    Physics.SyncTransforms();
    typeof(PlayerInputReader).GetProperty("Current").SetValue(rider.GetComponent<PlayerInputReader>(), new PlayerInputFrame { Move = -1f });
    if (reverse) localDrop.SetValue(rider, false); // ACK can arrive before the old stack link is removed.
    Assert.AreSame(player, stack.GetBelow(rider), "The old link is deliberately still present.");
    Invoke(stack, "UpdateStackBalance");
    Invoke(switcher, "UpdateSurfaceMode");
    Assert.IsInstanceOf<TightropeControlScheme>(riderController.Scheme);
    Assert.IsTrue(riderBalance.IsActive);
    Assert.IsTrue(passenger.Simulated);
    Assert.IsFalse(passenger.FreeMovement, "Side input must return to balance rather than platform movement.");
    Assert.IsFalse(passenger.FreeAirControl);
    Assert.That(Vector3.Dot(originalForward, passenger.CourseForward), Is.GreaterThan(.999f), "Holding S must not flip the traversal axis.");
    Assert.AreEqual(1, (int)typeof(PlayerBalance).GetField("stackSize", fields).GetValue(riderBalance));
    Invoke(follow, "LateUpdate");
    Assert.That(rider.transform.position, Is.EqualTo(origin + Vector3.forward * 7));
   });
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
  [Category("PiggybackRopeFlow")]
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
    if (!catchesRope)
     Assert.AreEqual(JumpKind.Fall, follow.PresentationJump(player), "The old carrier must not override the owner's fall pose.");
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

  public enum RecoverySurface { RopeStart, RopeEnd, AdjacentRope, Platform }

  [TestCase(false), TestCase(true)]
  public void FreeMovementStopsTheStackAtAnObstacleOnlyThePassengerHits(bool jump)
  {
   WithMountedPair((stack, follow, passenger) =>
   {
    float height = mover.BodyHeight;
    Vector3 step = jump ? Vector3.up * .8f : Vector3.forward * 2f;
    var obstacle = jump
        ? AddBox("Ceiling over the passenger", Vector3.up * (height + passenger.BodyHeight + .25f), new Vector3(4, .1f, 4))
        : AddBox("Thin wall at passenger height", new Vector3(0, height + .5f, 1), new Vector3(4, .6f, .05f));
    Vector3 before = player.transform.position;
    Invoke(mover, "MoveWithPassengers", step);
    Invoke(follow, "LateUpdate");
    Assert.That(Vector3.Distance(player.transform.position, before), Is.LessThan(step.magnitude - .2f));
    if (jump)
     Assert.That(passenger.transform.position.y + passenger.BodyHeight, Is.LessThanOrEqualTo(obstacle.bounds.min.y + passenger.Body.skinWidth + .01f));
    else
     Assert.That(passenger.transform.position.z + passenger.BodyRadius, Is.LessThanOrEqualTo(obstacle.bounds.min.z + passenger.Body.skinWidth + .01f));
    Assert.AreSame(player, stack.GetBelow(passenger.GetComponent<NetworkPlayer>()));
    obstacle.enabled = false;
    Physics.SyncTransforms();
    before = player.transform.position;
    Invoke(mover, "MoveWithPassengers", step);
    Assert.That(Vector3.Distance(player.transform.position, before), Is.EqualTo(step.magnitude).Within(.02f));
   });
  }

  [Test]
  public void MountedCarrierCanCrouchButCannotStandIntoThePassengersCeiling()
  {
   WithMountedPair((stack, follow, passenger) =>
   {
    float standing = mover.BodyHeight;
    mover.SetLocomotionInput(true, true);
    Invoke(mover, "UpdatePosture"); Invoke(mover, "ApplyBodySize", false);
    Invoke(follow, "LateUpdate");
    Assert.IsTrue(mover.IsCrouched);
    Assert.IsFalse(mover.IsSprinting, "Crouch takes priority over sprint.");
    float crouched = mover.BodyHeight;
    float ceilingHeight = (standing + crouched) * .5f + passenger.BodyHeight;
    var ceiling = AddBox("Low passage over the pair", Vector3.up * ceilingHeight, new Vector3(4, .05f, 4));
    mover.SetLocomotionInput(true, false);
    Invoke(mover, "UpdatePosture"); Invoke(mover, "ApplyBodySize", false);
    Assert.IsTrue(mover.IsCrouched);
    Assert.That(mover.BodyHeight, Is.EqualTo(crouched));
    ceiling.enabled = false;
    Physics.SyncTransforms();
    Invoke(mover, "UpdatePosture"); Invoke(mover, "ApplyBodySize", false);
    Assert.IsFalse(mover.IsCrouched);
    Assert.IsTrue(mover.IsSprinting);
    Assert.That(mover.BodyHeight, Is.EqualTo(standing));
   });
  }

  [Test]
  public void SecondMountInTheSameFrameUsesTheWholeStackHeight()
  {
   WithMountedPair((stack, follow, passenger) =>
   {
    var root = MakeObject("Next passenger");
    root.transform.position = origin + Vector3.back * (GameSettings.Tightrope.Piggyback.LinkDistance * .8f);
    var next = root.AddComponent<NetworkPlayer>();
    var nextMover = root.GetComponent<PlayerMover>();
    Invoke(nextMover, "Awake"); Invoke(next, "Awake");
    nextMover.FreeMovement = true;
    var net = typeof(Unity.Netcode.NetworkBehaviour);
    net.GetProperty("IsSpawned").SetValue(next, true);
    net.GetProperty("OwnerClientId").SetValue(next, 2UL);
    try
    {
     ((System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All).Add(next);
     // The previous link has arrived, but the first rider has not followed the carrier yet.
     passenger.Body.enabled = false;
     passenger.transform.position = origin + Vector3.back;
     passenger.Body.enabled = true;
     float roofHeight = mover.BodyHeight + passenger.BodyHeight + nextMover.BodyHeight * .5f;
     var roof = AddBox("Room for only two riders", Vector3.up * roofHeight, new Vector3(4, .1f, 4));
     Assert.IsNull(stack.FindMountTarget(next));
     roof.enabled = false;
     Physics.SyncTransforms();
     Assert.AreSame(player, stack.FindMountTarget(next));
     Invoke(stack, "ServerTryLink", 2UL);
     Assert.AreSame(passenger.GetComponent<NetworkPlayer>(), stack.GetBelow(next));
    }
    finally { net.GetProperty("IsSpawned").SetValue(next, false); }
   });
  }

  [Test]
  public void OrdinaryMovementCanSprintThenCrouchThenJumpAfterStanding()
  {
   AddBox("Ordinary floor", new Vector3(0, -.5f, 0), new Vector3(12, 1, 12));
   MovePlayer(Vector3.zero);
   mover.FreeMovement = true;
   mover.Body.Move(Vector3.down * .05f);
   Assert.IsTrue(mover.Body.isGrounded);
   var rules = GameSettings.Base.Character;

   mover.SetMoveInput(1f);
   mover.SetLocomotionInput(true, false);
   Invoke(mover, "Update");
   Assert.That(mover.GroundVelocity.magnitude, Is.EqualTo(rules.FreeMoveSpeed * rules.SprintSpeedMultiplier).Within(.001f));

   mover.Body.Move(Vector3.down * .05f);
   mover.SetMoveInput(1f);
   mover.SetLocomotionInput(true, true);
   mover.RequestJump();
   Invoke(mover, "Update");
   Assert.IsTrue(mover.IsCrouched);
   Assert.IsFalse(mover.IsSprinting);
   Assert.AreEqual(JumpKind.None, mover.CurrentJump);
   Assert.That(mover.GroundVelocity.magnitude, Is.EqualTo(rules.FreeMoveSpeed * rules.CrouchSpeedMultiplier).Within(.001f));

   mover.Body.Move(Vector3.down * .05f);
   mover.SetLocomotionInput(false, false);
   mover.RequestJump();
   Invoke(mover, "Update");
   Assert.IsFalse(mover.IsCrouched);
   Assert.AreEqual(JumpKind.InPlace, mover.CurrentJump);
  }

  [Test]
  public void ActiveSceneChangeClearsMountedFollowWithoutRepositioningThePassenger()
  {
   WithMountedPair((stack, follow, passenger) =>
   {
    var next = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
    using var oldLinks = (Unity.Netcode.FastBufferWriter)Invoke(stack, "WriteLinks", null, null, 0f, null);
    string oldName = testScene.name;
    try
    {
     stack.ReleaseForSuccess();
     using var current = new Unity.Netcode.FastBufferReader(oldLinks, Unity.Collections.Allocator.Temp);
     Invoke(stack, "HandleLinksMessage", Unity.Netcode.NetworkManager.ServerClientId, current);
     Assert.AreSame(passenger.GetComponent<NetworkPlayer>(), stack.GetAbove(player), "A snapshot still restores a link in its own scene.");
     Vector3 before = passenger.transform.position;
     typeof(PiggybackSystem).GetMethod("OnActiveSceneChanged", BindingFlags.Static | BindingFlags.NonPublic)
         .Invoke(null, new object[] { testScene, next });
     Assert.IsNull(stack.GetAbove(player));
     Assert.IsTrue(passenger.Simulated);
     Assert.That(passenger.transform.position, Is.EqualTo(before));
     testScene.name = "Destination scene fixture";
     using var delayed = new Unity.Netcode.FastBufferReader(oldLinks, Unity.Collections.Allocator.Temp);
     Invoke(stack, "HandleLinksMessage", Unity.Netcode.NetworkManager.ServerClientId, delayed);
     Assert.IsNull(stack.GetAbove(player), "A delayed snapshot from the departed scene cannot remount the rider.");
     Invoke(follow, "LateUpdate");
     Assert.That(passenger.transform.position, Is.EqualTo(before), "The old carrier cannot take movement back.");
    }
    finally
    {
     testScene.name = oldName;
     UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(next);
    }
   });
  }

  void WithMountedPair(System.Action<PiggybackSystem, PiggybackFollow, PlayerMover> test)
  {
   AddBox("Common ground", new Vector3(0, -.5f, 0), new Vector3(12, 1, 12));
   MovePlayer(Vector3.zero);
   mover.FreeMovement = true;
   var riderRoot = MakeObject("Passenger");
   riderRoot.transform.position = origin + Vector3.back;
   var rider = riderRoot.AddComponent<NetworkPlayer>();
   var passenger = riderRoot.GetComponent<PlayerMover>();
   Invoke(passenger, "Awake"); Invoke(rider, "Awake");
   passenger.FreeMovement = true;
   var all = (System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All;
   var oldPlayers = all.ToArray();
   var current = typeof(PiggybackSystem).GetProperty("Current");
   var local = typeof(NetworkPlayer).GetProperty("Local");
   var oldStack = current.GetValue(null);
   var oldLocal = local.GetValue(null);
   bool oldSuppressed = PiggybackSystem.Suppressed;
   var stack = MakeObject("Common piggyback").AddComponent<PiggybackSystem>();
   var follow = stack.gameObject.AddComponent<PiggybackFollow>();
   var net = typeof(Unity.Netcode.NetworkBehaviour);
   try
   {
    all.Clear(); all.Add(player); all.Add(rider);
    net.GetProperty("IsSpawned").SetValue(player, true); net.GetProperty("IsSpawned").SetValue(rider, true);
    net.GetProperty("OwnerClientId").SetValue(player, 0UL); net.GetProperty("OwnerClientId").SetValue(rider, 1UL);
    net.GetProperty("IsOwner").SetValue(rider, true);
    PiggybackSystem.Suppressed = false;
    current.SetValue(null, stack); local.SetValue(null, rider);
    Invoke(follow, "Awake");
    Physics.SyncTransforms();
    string response = (string)Invoke(stack, "ServerTryLink", 1UL);
    Assert.AreSame(player, stack.GetBelow(rider), response);
    Invoke(follow, "LateUpdate");
    Assert.IsFalse(passenger.Simulated);
    test(stack, follow, passenger);
   }
   finally
   {
    net.GetProperty("IsSpawned").SetValue(player, false); net.GetProperty("IsSpawned").SetValue(rider, false);
    all.Clear(); all.AddRange(oldPlayers);
    current.SetValue(null, oldStack); local.SetValue(null, oldLocal);
    PiggybackSystem.Suppressed = oldSuppressed;
   }
  }

  [Test, Category("PiggybackRopeFlow")]
  public void RiderAnimationKeepsOwnJumpWhileReleaseIsPending()
  {
   WithMountedPair((stack, follow, passenger) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    var local = typeof(NetworkPlayer).GetProperty("Local");
    var oldLocal = local.GetValue(null);
    const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
    var jump = (Unity.Netcode.NetworkVariable<JumpKind>)typeof(NetworkPlayer).GetField("jump", fields).GetValue(rider);
    var dropping = (Unity.Netcode.NetworkVariable<bool>)typeof(NetworkPlayer).GetField("droppingFromSupport", fields).GetValue(rider);
    Assert.AreEqual(JumpKind.None, follow.PresentationJump(rider));
    try
    {
     passenger.LaunchJump(0f);
     typeof(PiggybackFollow).GetField("launchedAt", fields).SetValue(follow, Time.time);
     Assert.AreSame(player, stack.GetBelow(rider), "Host unlink has not arrived yet.");
     Assert.AreEqual(JumpKind.InPlace, follow.PresentationJump(rider), "A local dismount jump takes over immediately.");

     local.SetValue(null, player);
     typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsOwner").SetValue(rider, false);
     typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(rider, false);
     jump.Value = JumpKind.Fall;
     dropping.Value = true;
     typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(rider, true);
     Assert.AreEqual(JumpKind.Fall, follow.PresentationJump(rider), "Observers use the rider's replicated fall even before unlink.");
    }
    finally { local.SetValue(null, oldLocal); }
   });
  }

  [Test, Category("PiggybackRopeFlow")]
  public void MountedJumpUsesTheSameToolMovementLockAsTheGround()
  {
   WithMountedPair((_, follow, passenger) =>
   {
    const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
    var rider = passenger.GetComponent<NetworkPlayer>();
    var reader = rider.GetComponent<PlayerInputReader>() ?? rider.gameObject.AddComponent<PlayerInputReader>();
    var presentation = rider.gameObject.AddComponent<HumanoidPlayerPresentation>();
    var movementLock = typeof(HumanoidPlayerPresentation).GetField("interactionMoveLockUntil", fields);
    var hopping = typeof(PiggybackFollow).GetField("hopping", fields);
    typeof(PlayerInputReader).GetProperty("Current").SetValue(reader, new PlayerInputFrame { ActionPressed = true });
    movementLock.SetValue(presentation, Time.time + 1f);
    Invoke(follow, "UpdateHop", rider);
    Assert.IsFalse((bool)hopping.GetValue(follow), "A locked tool gesture cannot be bypassed by a mounted jump.");
    movementLock.SetValue(presentation, 0f);
    Invoke(follow, "UpdateHop", rider);
    Assert.IsTrue((bool)hopping.GetValue(follow), "The same rider can jump when movement unlocks.");
   });
  }

  [Test, Category("PiggybackRopeFlow")]
  public void MountedRiderFallsLandsAndCanMountAgain(
      [Values(2, 3, 4)] int count, [Values] RecoverySurface surface)
  {
   var ropes = AuthoredRopes();
   ropes[1].transform.SetPositionAndRotation(origin + new Vector3(2, -.1f, 4), Quaternion.identity);
   course.Rebuild();
   Physics.SyncTransforms();
   Vector3 landing = surface switch {
    RecoverySurface.RopeStart => new Vector3(0, 0, .25f),
    RecoverySurface.RopeEnd => new Vector3(0, 0, 7.75f),
    RecoverySurface.AdjacentRope => new Vector3(2, 0, 4),
    _ => new Vector3(0, 0, -2.5f)
   };
   if (surface == RecoverySurface.AdjacentRope)
   {
    Assert.IsTrue(course.TryFindLaneLanding(origin + Vector3.forward * 4, Vector3.forward, 1, out _, out var target));
    landing = target - origin;
   }
   if (surface == RecoverySurface.Platform) AddBox("Practice floor", new Vector3(0, -.5f, -2), new Vector3(4, 1, 4));
   var carrierFeet = landing + Vector3.forward * (surface == RecoverySurface.RopeEnd ? -1.1f : 1.1f);

   var current = typeof(PiggybackSystem).GetProperty("Current");
   var local = typeof(CurtainCall.Network.PlayerSync.NetworkPlayer).GetProperty("Local");
   var previousStack = current.GetValue(null);
   var previousLocal = local.GetValue(null);
   bool previousSuppressed = PiggybackSystem.Suppressed;
   var all = (System.Collections.Generic.List<CurtainCall.Network.PlayerSync.NetworkPlayer>)
       CurtainCall.Network.PlayerSync.NetworkPlayer.All;
   var previousPlayers = all.ToArray();
   var riders = new CurtainCall.Network.PlayerSync.NetworkPlayer[count];
   var stack = MakeObject("Fall and remount stack").AddComponent<PiggybackSystem>();
   var follow = stack.gameObject.AddComponent<PiggybackFollow>();
   const BindingFlags privateFields = BindingFlags.Instance | BindingFlags.NonPublic;
   var drop = (Unity.Netcode.NetworkVariable<bool>)player.GetType()
       .GetField("droppingFromSupport", privateFields).GetValue(player);
   try
   {
    all.Clear();
    current.SetValue(null, stack);
    local.SetValue(null, player);
    PiggybackSystem.Suppressed = false;
    typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsOwner").SetValue(player, true);
    Invoke(follow, "Awake");
    for (int i = 0; i < count; i++)
    {
     var rider = i == count - 1 ? player : MakeObject("Rider " + i).AddComponent<CurtainCall.Network.PlayerSync.NetworkPlayer>();
     riders[i] = rider;
     if (rider != player)
     {
      rider.transform.position = origin + carrierFeet;
      Invoke(rider.GetComponent<PlayerMover>(), "Awake");
      Invoke(rider, "Awake");
     }
     else MovePlayer(landing);
     typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(rider, true);
     typeof(Unity.Netcode.NetworkBehaviour).GetProperty("OwnerClientId").SetValue(rider, (ulong)i);
     if (!all.Contains(rider)) all.Add(rider);
     Physics.SyncTransforms();
     if (i > 0)
     {
      string response = (string)Invoke(stack, "ServerTryLink", (ulong)i);
      Assert.AreSame(riders[i - 1], stack.GetBelow(rider), response);
     }
    }
    Invoke(follow, "LateUpdate");
    Assert.IsFalse(mover.Simulated, "The rider follows the carrier while mounted.");
    Vector3 forwardStep = (landing - carrierFeet).normalized * .25f;
    riders[0].transform.position += forwardStep;
    Physics.SyncTransforms();
    Invoke(follow, "LateUpdate");
    Assert.AreEqual(riders[0].transform.position.z, player.transform.position.z, "Moving along the surface must retain the mounted pose.");

    Invoke(follow, "ReleaseForFall", player);
    Invoke(player, "PrepareNaturalFall");
    player.GetType().GetField("localSupportDrop", privateFields).SetValue(player, true);
    mover.ResetExternalMotion(true);
    drop.Value = true;
    Invoke(stack, "ServerResolveFalls");
    Invoke(follow, "LateUpdate");
    Assert.IsNull(stack.GetBelow(player), "The falling rider must leave the stack.");
    Assert.IsTrue(mover.Simulated);
    if (count > 2) Assert.AreSame(riders[count - 3], stack.GetBelow(riders[count - 2]), "The remaining riders stay linked.");
    Assert.IsNull(stack.FindMountTarget(player), "A rider cannot mount in midair.");

    MovePlayer(landing);
    mover.ResetExternalMotion(false);
    Invoke(switcher, "Bind", player);
    Invoke(switcher, "UpdateSurfaceMode");
    Assert.AreEqual(surface != RecoverySurface.Platform, switcher.IsLocalOnRope, "Landing must restore the controls for this surface.");
    Assert.IsNull(stack.FindMountTarget(player), "Do not offer a link while the previous fall still owns the player.");
    Invoke(stack, "ServerTryLink", player.OwnerClientId);
    Assert.IsNull(stack.GetBelow(player), "The host must also reject an early mount request.");
    Invoke(player, "QueueFallLanding", player.transform.position.y + 1f, player.transform.position);
    uint contact = (uint)player.GetType().GetField("fallSequence", privateFields).GetValue(player);
    drop.Value = false;
    Assert.IsNull(stack.FindMountTarget(player), "The owner's landing acknowledgement can arrive after the host flag.");
    Invoke(player, "AcknowledgeFallLanding", contact, 0, 0);
    Assert.IsFalse(player.IsDroppingFromSupport);
    Assert.AreSame(riders[0], stack.FindMountTarget(player));
    string remount = (string)Invoke(stack, "ServerTryLink", player.OwnerClientId);
    Assert.AreSame(riders[count - 2], stack.GetBelow(player), remount);
    Invoke(follow, "LateUpdate");
    Assert.IsFalse(mover.Simulated, "The new link must take control after the old fall ends.");
   }
   finally
   {
    foreach (var rider in riders)
     if (rider != null) typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(rider, false);
    all.Clear();
    all.AddRange(previousPlayers);
    current.SetValue(null, previousStack);
    local.SetValue(null, previousLocal);
    PiggybackSystem.Suppressed = previousSuppressed;
   }
  }
 }
}
