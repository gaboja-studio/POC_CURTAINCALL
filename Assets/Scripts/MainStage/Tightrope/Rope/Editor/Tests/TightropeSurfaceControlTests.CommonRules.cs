using System.Reflection;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
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

  [TestCase(false, false, false)] [TestCase(false, true, false)]
  [TestCase(true, false, false)] [TestCase(true, true, false)]
  [TestCase(false, false, true)] [TestCase(true, true, true)]
  [Category("PiggybackRopeFlow")]
  public void FallenRiderRegainsSoloRopeInputBeforeOldMountLinkArrives(bool tickWhileFalling, bool reverse, bool carrierDied)
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

    const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
    var localDrop = typeof(NetworkPlayer).GetField("localSupportDrop", fields);
    if (carrierDied)
    {
     var spawned = typeof(NetworkPlayer).GetProperty("IsSpawned");
     spawned.SetValue(player, false);
     ((Unity.Netcode.NetworkVariable<PlayerState>)typeof(NetworkPlayer).GetField("state", fields).GetValue(player)).Value = PlayerState.Fallen;
     spawned.SetValue(player, true);
     Invoke(follow, "LateUpdate");
    }
    else
    {
     Invoke(follow, "ReleaseForFall", rider);
     localDrop.SetValue(rider, true);
    }
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

  [TestCase(1f, -1, -1)] [TestCase(-1f, -1, 1)] [TestCase(-1f, 1, -1)]
  public void LaneClickUsesTheDirectionOfTravelWithoutSpace(float move, int click, int expected)
  {
   mover.FreeMovement=false; mover.CourseForwardOverride=Vector3.forward;
   var scheme=Track(ScriptableObject.CreateInstance<TightropeControlScheme>());
   var context=new PlayerControlContext(player.gameObject);
   scheme.Tick(context,new PlayerInputFrame{Move=move, AuxDirection=click, LaneChangePressed=true});
   Assert.That(mover.RopeTravelForward,Is.EqualTo(Vector3.forward*move));
   Assert.That(typeof(PlayerMover).GetField("laneJumpRequested",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(mover),
    Is.EqualTo(expected));
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

  [TestCase(2, 0, PlayerState.Fallen)]
  [TestCase(3, 1, PlayerState.Fallen)]
  [TestCase(4, 2, PlayerState.Fallen)]
  [TestCase(4, 3, PlayerState.Fallen)]
  [TestCase(4, 0, PlayerState.Retired)]
  public void InterruptedStackStopsFollowingBeforeTheHostUnlinks(int count, int interrupted, PlayerState state)
  {
   const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
   var all = (System.Collections.Generic.List<NetworkPlayer>)NetworkPlayer.All;
   var oldPlayers = all.ToArray();
   var localProperty = typeof(NetworkPlayer).GetProperty("Local");
   var currentProperty = typeof(PiggybackSystem).GetProperty("Current");
   var previousLocal = localProperty.GetValue(null);
   var previousStack = currentProperty.GetValue(null);
   var stack = MakeObject("Stack pending host unlink").AddComponent<PiggybackSystem>();
   var follow = stack.gameObject.AddComponent<PiggybackFollow>();
   var below = (System.Collections.Generic.Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("belowOf", fields).GetValue(stack);
   var above = (System.Collections.Generic.Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("aboveOf", fields).GetValue(stack);
   var actors = new NetworkPlayer[count];
   try
   {
    all.Clear(); currentProperty.SetValue(null, stack);
    for (int i = 0; i < count; i++)
    {
     var actor = i == count - 1 ? player : MakeObject("Stack participant").AddComponent<NetworkPlayer>();
     if (actor != player)
     {
      Invoke(actor.GetComponent<PlayerMover>(), "Awake");
      Invoke(actor, "Awake");
     }
     typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(actor, (ulong)i);
     actors[i] = actor;
     all.Add(actor);
     if (i > 0) { below[(ulong)i] = (ulong)(i - 1); above[(ulong)(i - 1)] = (ulong)i; }
    }
    localProperty.SetValue(null, player);
    typeof(NetworkPlayer).GetProperty("IsOwner").SetValue(player, true);
    Invoke(follow, "Awake");
    Invoke(follow, "LateUpdate");
    Assert.IsFalse(mover.Simulated);

    var removed = actors[interrupted];
    ((Unity.Netcode.NetworkVariable<PlayerState>)typeof(NetworkPlayer).GetField("state", fields).GetValue(removed)).Value = state;
    var fallenFeet = origin + new Vector3(1, -2, 3);
    removed.transform.position = fallenFeet;
    if (removed != player) player.transform.position = fallenFeet + Vector3.right;
    var previousFeet = player.transform.position;

    Invoke(follow, "LateUpdate");
    Assert.AreEqual(fallenFeet, removed.transform.position);
    Assert.AreEqual(previousFeet, player.transform.position);
    Assert.IsNotNull(stack.GetBelow(player), "The host unlink is intentionally delayed.");
    Assert.IsFalse((bool)Invoke(player, "HeldAboveGround"));
    Assert.IsTrue(mover.Simulated, "No passenger may keep the movement lock from the old stack.");

    Invoke(stack, "ServerResolveFalls");
    Assert.IsNull(stack.GetBelow(player));
    for (int i = 1; i < interrupted; i++)
     Assert.AreSame(actors[i - 1], stack.GetBelow(actors[i]), "The surviving lower part of the stack stays attached.");
   }
   finally
   {
    all.Clear(); all.AddRange(oldPlayers);
    localProperty.SetValue(null, previousLocal); currentProperty.SetValue(null, previousStack);
   }
  }

  [TestCase(1)] [TestCase(2)] [TestCase(3)]
  public void NonfatalLimbLossKeepsMountedSupportAndRiderJumpInput(int lost)
  {
   WithMountedPair((stack, follow, passenger) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    var condition = rider.gameObject.AddComponent<PlayerCondition>();
    condition.SetLostParts(PlayerCondition.PartsForLostCount(lost));
    var damage = rider.gameObject.AddComponent<CurtainCall.Damage.BodyDamageEffects>();
    Invoke(damage, "Awake");
    Invoke(damage, "Update");
    var reader = rider.GetComponent<PlayerInputReader>() ?? rider.gameObject.AddComponent<PlayerInputReader>();
    var field = typeof(PlayerInputReader).GetProperty("Current");
    field.SetValue(reader, new PlayerInputFrame { ActionPressed = true });
    Invoke(follow, "LateUpdate");
    Assert.AreEqual(PlayerState.Normal, rider.State);
    Assert.AreSame(player, stack.GetBelow(rider));
    Assert.IsTrue((bool)Invoke(rider, "HeldAboveGround"));
    Assert.IsFalse(passenger.Simulated);
    Assert.AreEqual(passenger.JumpAllowed ? JumpKind.InPlace : JumpKind.None, follow.PresentationJump(rider));
    Assert.AreEqual(GameSettings.Base.BodyDamage.JumpHeightForLostLegs(condition.LostLegCount), passenger.JumpHeightMultiplier);
   });
  }

  [TestCase(0)]
  [TestCase(1)]
  public void MountedJumpHonorsTheSameInjuryInputLocksAsTheGround(int direction)
  {
   WithMountedPair((_, follow, passenger) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    var reader = rider.GetComponent<PlayerInputReader>() ?? rider.gameObject.AddComponent<PlayerInputReader>();
    passenger.FreeMovement = direction == 0;
    passenger.LaneJumpFilter = _ => true;
    passenger.JumpAllowed = passenger.LaneJumpAllowed = false;
    typeof(PlayerInputReader).GetProperty("Current").SetValue(reader,
     new PlayerInputFrame { ActionPressed = true, AuxDirection = direction });
    Invoke(follow, "UpdateHop", rider);
    Assert.AreEqual(JumpKind.None, follow.PresentationJump(rider));
    Assert.IsFalse(passenger.Simulated);
    passenger.JumpAllowed = passenger.LaneJumpAllowed = true;
    Invoke(follow, "UpdateHop", rider);
    Assert.AreEqual(direction == 0 ? JumpKind.InPlace : JumpKind.Lane, follow.PresentationJump(rider));
   });
  }

  [TestCase(0)]
  [TestCase(1)]
  public void MountedCrouchBlocksJumpUntilTheRiderCanStand(int direction)
  {
   WithMountedPair((stack, follow, passenger) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    var reader = rider.GetComponent<PlayerInputReader>() ?? rider.gameObject.AddComponent<PlayerInputReader>();
    var current = typeof(PlayerInputReader).GetProperty("Current");
    passenger.FreeMovement = direction == 0;
    passenger.LaneJumpFilter = _ => true;
    passenger.SetRemoteCrouched(true);
    Invoke(passenger, "ApplyBodySize", true);
    float standing = GameSettings.Base.Character.CapsuleHeight;
    var ceiling = AddBox("Mounted headroom", rider.transform.position - origin + Vector3.up * standing, new Vector3(1f, .3f, 1f));

    current.SetValue(reader, new PlayerInputFrame { ActionPressed = true, AuxDirection = direction, CrouchHeld = true });
    Invoke(follow, "UpdateHop", rider);
    Assert.AreEqual(JumpKind.None, follow.PresentationJump(rider));
    Assert.IsTrue(passenger.IsCrouched);
    Assert.AreSame(player, stack.GetBelow(rider));

    current.SetValue(reader, new PlayerInputFrame { ActionPressed = true, AuxDirection = direction });
    Invoke(follow, "UpdateHop", rider);
    Assert.AreEqual(JumpKind.None, follow.PresentationJump(rider));
    Assert.IsTrue(passenger.IsCrouched);

    ceiling.enabled = false;
    Physics.SyncTransforms();
    Invoke(follow, "UpdateHop", rider);
    Assert.IsFalse(passenger.IsCrouched);
    Assert.AreEqual(direction == 0 ? JumpKind.InPlace : JumpKind.Lane, follow.PresentationJump(rider));
   });
  }

  [Test]
  public void ConfirmedRopeGripDropsTheCarriedTrampolineBeforeTakingMovement()
  {
   WithMountedPair((_, _, _) => WithTrampoline((trampoline, ownership) =>
   {
    GiveTrampolineHostAuthority(trampoline);
    trampoline.transform.position = origin + Vector3.up * 3f;
    Assert.IsTrue(ownership.TryApply(CannonAction.Board, player.OwnerClientId, ownership.Revision));

    var hangs = MakeObject("Rope grip handoff").AddComponent<TightropeHangSystem>();
    Invoke(hangs, "BeginHang", player.OwnerClientId, 0, 0f);

    Assert.IsNull(HumanCannonCarry.Held(player.OwnerClientId));
    Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
    Assert.IsTrue(hangs.IsHanging(player));
    Assert.IsTrue((bool)typeof(HumanCannonTrampoline).GetField("dropping", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trampoline));
   }));
  }

  [TestCase(false)]
  [TestCase(true)]
  public void RopeEntryWaitsForHostTrampolineDropForSoloPlayerAndStack(bool mounted)
  {
   AuthoredRopes();
   WithMountedPair((_, _, passenger) => WithTrampoline((trampoline, ownership) =>
   {
    GiveTrampolineHostAuthority(trampoline);
    AddBox("Parking floor below the rope", new Vector3(1f, -1.1f, 7f), new Vector3(3f, .2f, 2f));
    ConfigureSmallTrampoline(trampoline, origin + new Vector3(1f, .5f, 7f));
    MovePlayer(new Vector3(0f, 0f, 7f));
    Assert.IsTrue(course.TryGetSupportingRope(mover, out _));
    Assert.IsTrue(ownership.TryApply(CannonAction.Board, player.OwnerClientId, ownership.Revision));
    var rider = mounted ? passenger.GetComponent<NetworkPlayer>() : player;
    if (!rider.TryGetComponent(out PlayerBalance riderBalance))
    {
     riderBalance = rider.gameObject.AddComponent<PlayerBalance>();
     Invoke(riderBalance, "Awake");
    }
    Invoke(switcher, "Bind", rider);

    Invoke(switcher, "UpdateSurfaceMode");
    Assert.IsFalse(switcher.IsLocalOnRope);
    Assert.IsTrue((bool)typeof(HumanCannonTrampoline).GetMethod("WaitingForRopeDrop", BindingFlags.Static | BindingFlags.NonPublic)
     .Invoke(null, new object[] { rider.GetComponent<PlayerMover>() }));

    Invoke(trampoline, "Update");
    Assert.IsNull(HumanCannonCarry.Held(player.OwnerClientId));
    const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
    var target = (Vector3)typeof(HumanCannonTrampoline).GetField("dropTarget", fields).GetValue(trampoline);
    Assert.That(target.y, Is.EqualTo(origin.y - 1f).Within(.002f));
    Assert.That(target.x, Is.EqualTo(origin.x + 1f).Within(.002f));
    Assert.IsFalse(trampoline.BounceReady);

    Invoke(switcher, "UpdateSurfaceMode");
    Assert.IsTrue(switcher.IsLocalOnRope);
    Assert.IsTrue(riderBalance.IsActive);
   }), mount: mounted);
  }

  [TestCase(false)]
  [TestCase(true)]
  public void CarryingOverARopeKeepsOwnershipUntilItActuallySupportsThePlayer(bool platform)
  {
   AuthoredRopes();
   WithMountedPair((_, _, _) => WithTrampoline((trampoline, ownership) =>
   {
    GiveTrampolineHostAuthority(trampoline);
    if (platform)
    {
     AddBox("Overlapping platform", new Vector3(0f, .25f, 7f), new Vector3(3f, .2f, 1f));
     MovePlayer(new Vector3(0f, .35f, 7f));
     Assert.IsFalse(course.TryGetSupportingRope(mover, out _));
    }
    else
    {
     MovePlayer(new Vector3(0f, 0f, 7f));
     typeof(NetworkPlayer).GetProperty("IsOwner").SetValue(player, true);
     mover.ResetExternalMotion(true);
     Assert.IsTrue(mover.IsAirborne);
    }
    Assert.IsTrue(ownership.TryApply(CannonAction.Board, player.OwnerClientId, ownership.Revision));
    Invoke(trampoline, "Update");
    Assert.AreEqual(player.OwnerClientId, trampoline.Carrier);
   }), mount: false);
  }

  [TestCase(false)]
  [TestCase(true)]
  public void RopeReleaseParksBesideTheRopeOrUsesTheRecoveryOrigin(bool hasSideFloor)
  {
   AuthoredRopes();
   WithMountedPair((_, _, _) => WithTrampoline((trampoline, ownership) =>
   {
    GiveTrampolineHostAuthority(trampoline);
    MovePlayer(new Vector3(0f, 0f, 7f));
    ConfigureSmallTrampoline(trampoline, origin + new Vector3(0f, .6f, 7f));
    var recovery = origin + Vector3.back;
    typeof(HumanCannonTrampoline).GetField("initialPosition", BindingFlags.Instance | BindingFlags.NonPublic)
     .SetValue(trampoline, recovery);
    if (hasSideFloor)
     AddBox("Side parking platform", new Vector3(1.25f, -.1f, 7f), new Vector3(1f, .2f, .85f));
    Assert.IsTrue(ownership.TryApply(CannonAction.Board, player.OwnerClientId, ownership.Revision));

    Invoke(trampoline, "Update");
    Assert.AreEqual(CannonSeatState.Nobody, trampoline.Carrier);
    if (hasSideFloor)
    {
     Assert.That(trampoline.transform.position.x, Is.EqualTo(origin.x + GameSettings.Trampoline.GrabDistance * .5f).Within(.002f));
     Assert.That(trampoline.transform.position.y, Is.EqualTo(origin.y).Within(.002f));
    }
    else Assert.AreEqual(recovery, trampoline.transform.position);
   }), mount: false, groundDepth: 4f);
  }

  [Test]
  public void TrampolineCanBePickedUpBesideTheRopeButNotFromItsSupport()
  {
   AuthoredRopes();
   WithMountedPair((_, _, _) => WithTrampoline((trampoline, _) =>
   {
    AddBox("Adjacent parking platform", new Vector3(2.5f, -.1f, 7f), new Vector3(3.6f, .2f, 3f));
    ConfigureSmallTrampoline(trampoline, origin + new Vector3(1.75f, 0f, 7.8f));
    var cannonRoot = MakeObject("Trampoline input system");
    cannonRoot.SetActive(false);
    var cannon = cannonRoot.AddComponent<HumanCannonSystem>();
    Invoke(cannon, "ResetSeats");
    var current = typeof(HumanCannonSystem).GetProperty("Current");
    var previous = current.GetValue(null);
    try
    {
     current.SetValue(null, cannon);
     balance.SetBalanceActive(false);
     MovePlayer(new Vector3(1.75f, 0f, 6.9f));
     Assert.IsTrue(trampoline.CanGrab(player));
     MovePlayer(new Vector3(0f, 0f, 7f));
     Assert.IsTrue(course.TryGetSupportingRope(mover, out int lane));
     Assert.AreEqual(0, lane);
     Assert.IsFalse(trampoline.CanGrab(player));
    }
    finally { current.SetValue(null, previous); }
   }), mount: false);
  }

  [Test]
  public void CarriedTrampolineColliderDoesNotMaskRopeSupport()
  {
   AuthoredRopes();
   WithMountedPair((_, _, _) => WithTrampoline((trampoline, ownership) =>
   {
    var overlap = AddBox("Held prop overlapping support", new Vector3(0f, -.09f, 7f), new Vector3(1f, .2f, 1f));
    overlap.transform.SetParent(trampoline.transform, true);
    MovePlayer(new Vector3(0f, .01f, 7f));
    Assert.IsFalse(course.TryGetSupportingRope(mover, out _));
    Assert.IsTrue(ownership.TryApply(CannonAction.Board, player.OwnerClientId, ownership.Revision));
    Assert.IsTrue(course.TryGetSupportingRope(mover, out _));
   }), mount: false);
  }

  void GiveTrampolineHostAuthority(HumanCannonTrampoline trampoline)
  {
   var host = MakeObject("Grip authority");
   host.SetActive(false);
   var session = host.AddComponent<NetworkSessionManager>();
   const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
   typeof(NetworkSessionManager).GetField("_networkManager", fields).SetValue(session, host.GetComponent<Unity.Netcode.NetworkManager>());
   typeof(NetworkSessionManager).GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
   typeof(HumanCannonTrampoline).GetField("session", fields).SetValue(trampoline, session);
  }

  static void ConfigureSmallTrampoline(HumanCannonTrampoline trampoline, Vector3 position)
  {
   trampoline.transform.position = position;
   trampoline.gameObject.AddComponent<BoxCollider>();
   typeof(HumanCannonTrampoline).GetField("halfSize", BindingFlags.Instance | BindingFlags.NonPublic)
    .SetValue(trampoline, new Vector2(.2f, .2f));
   Invoke(trampoline, "RefreshCarryBody", new object[] { null });
   Physics.SyncTransforms();
  }

  [Test]
  public void CarriedTrampolineUsesTheReversedRopeHeading()
  {
   WithMountedPair((_, _, passenger) => WithTrampoline((trampoline, ownership) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    passenger.FreeMovement = false;
    passenger.CourseForwardOverride = Vector3.forward;
    trampoline.transform.position = rider.transform.position + Vector3.forward * 2f;
    passenger.ReverseCourseDirection();

    Assert.IsTrue(HumanCannonCarry.TryFollowPose(rider, trampoline.transform, trampoline.Handle, 1f, false,
     out _, out _, out var carrierRotation));
    Assert.That(Vector3.Dot(carrierRotation * Vector3.forward, passenger.CourseForward), Is.EqualTo(1f).Within(.001f));
   }));
  }

  [Test]
  public void PendingCarryOwnershipInterruptsRescueAndDoesNotResetItsTimer()
  {
   AuthoredRopes();
   WithMountedPair((_, _, passenger) => WithTrampoline((_, ownership) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    MovePlayer(Vector3.forward * 4f);
    var hangs = MakeObject("Rescue handoff").AddComponent<TightropeHangSystem>();
    const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
    typeof(TightropeHangSystem).GetField("course", fields).SetValue(hangs, course);
    var book = (TightropeHangBook)typeof(TightropeHangSystem).GetField("book", fields).GetValue(hangs);
    var hang = book.Begin(rider.OwnerClientId, 0, 4f, 7d);
    Invoke(hangs, "ServerRescue", player.OwnerClientId, rider.OwnerClientId);
    Assert.IsTrue(hang.IsPulling, "Rescue is valid before the delayed device approval.");

    Assert.IsTrue(ownership.TryApply(CannonAction.Board, player.OwnerClientId, ownership.Revision));
    Invoke(hangs, "ServerTick", .25f);
    Assert.IsFalse(hang.IsPulling);
    Assert.That(hang.Remaining, Is.EqualTo(6.75d).Within(.001d));
    Invoke(hangs, "ServerRescue", player.OwnerClientId, rider.OwnerClientId);
    Assert.IsFalse(hang.IsPulling);
   }), mount: false);
  }

  [Test]
  public void CannonBoardingRechecksTheActorAfterRescueStarts()
  {
   WithMountedPair((_, _, passenger) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    var station = MakeObject("Cannon beside rope").AddComponent<HumanCannonStation>();
    station.transform.position = PlayerInteraction.Center(rider) + Vector3.forward * .5f;
    var cannonRoot = MakeObject("Cannon recheck");
    cannonRoot.SetActive(false);
    var cannon = cannonRoot.AddComponent<HumanCannonSystem>();
    cannon.ConfigurePractice(new[] { station }, -10f);
    Invoke(cannon, "ResetSeats");
    Physics.SyncTransforms();

    var hangs = MakeObject("Rescue before cannon approval").AddComponent<TightropeHangSystem>();
    var current = typeof(TightropeHangSystem).GetProperty("Current");
    var previous = current.GetValue(null);
    try
    {
     current.SetValue(null, hangs);
     Assert.AreEqual(0, cannon.FindBoardableStation(rider));
     var book = (TightropeHangBook)typeof(TightropeHangSystem).GetField("book", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hangs);
     book.Begin(player.OwnerClientId, 0, 4f, 7d);
     Assert.IsTrue(book.StartPull(player.OwnerClientId, rider.OwnerClientId, 0, 2d));
     Assert.AreEqual(-1, cannon.FindBoardableStation(rider));
     book.CancelPull(player.OwnerClientId);
     Assert.AreEqual(0, cannon.FindBoardableStation(rider));
    }
    finally { current.SetValue(null, previous); }
   }, mount: false);
  }

  [TestCase(false)]
  [TestCase(true)]
  public void GroundMountRequestHonorsWhoIsHoldingTheTrampoline(bool riderHoldsIt)
  {
   WithMountedPair((stack, _, passenger) => WithTrampoline((_, ownership) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    ulong carrier = riderHoldsIt ? rider.OwnerClientId : player.OwnerClientId;
    Assert.IsTrue(ownership.TryApply(CannonAction.Board, carrier, ownership.Revision));
    Assert.AreSame(riderHoldsIt ? null : player, stack.FindMountTarget(rider));
    Invoke(stack, "ServerTryLink", rider.OwnerClientId);
    Assert.AreSame(riderHoldsIt ? null : player, stack.GetBelow(rider));
   }), mount: false);
  }

  void WithTrampoline(System.Action<HumanCannonTrampoline, CannonSeatState> test)
  {
   var root = MakeObject("Carry handoff trampoline");
   var handle = MakeObject("Carry handoff handle").transform;
   handle.SetParent(root.transform, false);
   handle.localPosition = Vector3.back;
   var trampoline = root.AddComponent<HumanCannonTrampoline>();
   const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
   typeof(HumanCannonTrampoline).GetField("handle", fields).SetValue(trampoline, handle);
   var ownership = (CannonSeatState)typeof(HumanCannonTrampoline).GetField("ownership", fields).GetValue(trampoline);
   var current = typeof(HumanCannonTrampoline).GetProperty("Current");
   var previous = current.GetValue(null);
   current.SetValue(null, trampoline);
   HumanCannonCarry.Register(trampoline);
   try { test(trampoline, ownership); }
   finally
   {
    HumanCannonCarry.Unregister(trampoline);
    current.SetValue(null, previous);
   }
  }

  [Test]
  public void RelocatingAHangingPlayerClearsTheGripWithoutStartingAnotherFall()
  {
   var root = MakeObject("Previous rope grip");
   root.SetActive(false);
   var hangs = root.AddComponent<TightropeHangSystem>();
   const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
   var book = (TightropeHangBook)typeof(TightropeHangSystem).GetField("book", fields).GetValue(hangs);
   book.Begin(player.OwnerClientId, 0, 4f, 7d, 0d);
   Invoke(hangs, "BindLocal", player, mover, balance);
   Assert.IsTrue(hangs.IsHanging(player));
   Assert.IsFalse(mover.Body.enabled);

   var checkpoint = origin + new Vector3(2, 0, 3);
   mover.Relocate(checkpoint, Quaternion.identity);
   Assert.IsFalse(hangs.IsHanging(player));
   Assert.IsFalse(mover.IsAirborne);
   Assert.IsTrue(mover.Simulated);
   Assert.IsTrue(mover.Body.enabled);
   Assert.IsFalse(balance.IsSuspended);
   Assert.AreEqual(checkpoint, player.transform.position);
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

  [Test, Category("CommonProgressionRegression")]
  public void DetachedLimbCannotPreventStandingAndJumpingButASolidCeilingCan()
  {
   AddBox("Floor", new Vector3(0, -.5f, 0), new Vector3(12, 1, 12));
   MovePlayer(Vector3.zero); mover.FreeMovement = true;
   mover.SetLocomotionInput(false, true);
   Invoke(mover, "UpdatePosture"); Invoke(mover, "ApplyBodySize", false);
   float crouched = mover.BodyHeight;
   var obstacle = AddBox("Head obstruction", Vector3.up * ((crouched + GameSettings.Base.Character.CapsuleHeight) * .5f), new Vector3(.2f, .1f, .2f));
   Assert.IsFalse(mover.TryStand(), "A real ceiling must still block standing.");
   obstacle.gameObject.AddComponent<CurtainCall.Damage.DismemberedLimb>();
   Assert.IsTrue(mover.TryStand(), "Detached limbs must not pin a crouched player.");
   mover.SetLocomotionInput(false, false);
   mover.Body.Move(Vector3.down * .05f);
   mover.RequestJump(); Invoke(mover, "Update");
   Assert.AreEqual(JumpKind.InPlace, mover.CurrentJump);
  }

  [Test, Category("CommonProgressionRegression")]
  public void DelayedMountRequestCannotRebuildTheStackDuringCannonPracticeReturn()
  {
   WithMountedPair((stack, _, passenger) =>
   {
    var rider = passenger.GetComponent<NetworkPlayer>();
    Assert.AreSame(player, stack.FindMountTarget(rider));
    var type = typeof(CurtainCall.WaitingRoom.WaitingRoomCannonRehearsal);
    var singleton = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    var previous = singleton.GetValue(null);
    var root = MakeObject("Ending cannon practice"); root.SetActive(false);
    var rehearsal = root.AddComponent<CurtainCall.WaitingRoom.WaitingRoomCannonRehearsal>();
    var shared = type.GetField("shared", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rehearsal);
    try
    {
     singleton.SetValue(null, rehearsal);
     shared.GetType().GetField("active").SetValue(shared, true);
     shared.GetType().GetField("ending").SetValue(shared, true);
     Invoke(stack, "ServerTryLink", rider.OwnerClientId);
     Assert.IsNull(stack.GetBelow(rider), "A pre-ending request cannot recreate the released link.");
     shared.GetType().GetField("active").SetValue(shared, false);
     Invoke(stack, "ServerTryLink", rider.OwnerClientId);
     Assert.AreSame(player, stack.GetBelow(rider), "A new request after returning must work.");
    }
    finally { singleton.SetValue(null, previous); }
   }, mount: false);
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

  void WithMountedPair(System.Action<PiggybackSystem, PiggybackFollow, PlayerMover> test, bool mount = true, float groundDepth = 12f)
  {
   AddBox("Common ground", new Vector3(0, -.5f, 0), new Vector3(12, 1, groundDepth));
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
    if (mount)
    {
     string response = (string)Invoke(stack, "ServerTryLink", 1UL);
     Assert.AreSame(player, stack.GetBelow(rider), response);
     Invoke(follow, "LateUpdate");
     Assert.IsFalse(passenger.Simulated);
    }
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
