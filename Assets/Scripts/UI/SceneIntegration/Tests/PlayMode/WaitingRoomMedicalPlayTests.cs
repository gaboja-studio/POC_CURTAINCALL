using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace CurtainCall.UI.SceneIntegration.Tests {
 public sealed partial class DevStartLauncherPlayTests {
  [UnityTest] public IEnumerator M2_BodyRestore_ElevatorRoundTripAndAbort() {
   yield return Launch(1,"Night",0,"","WaitingRoomBase");SeedShopFunds(1000);
   var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
   Call(player,"ServerSetOneArm");yield return null;
   var service=Find("CurtainCall.WaitingRoom.ExpertService");var room=Find("CurtainCall.WaitingRoom.ExpertRoomView");
   var mover=player.GetComponent(T("CurtainCall.Player.PlayerMover"));
   var outside=(Transform)Private(room,"outside");var inside=(Transform)Private(room,"inside");
   Call(service,"RequestMedical",2);yield return null;
   Assert.AreEqual("Booked",Get(service,"Phase").ToString());Assert.AreEqual(800L,Get(service,"Balance"));Assert.AreEqual(125L,Get(service,"MedicalPrice"));
   Call(mover,"SetStartPose",outside.position,outside.rotation);yield return new WaitForSecondsRealtime(.4f);
   Call(room,"Interact");yield return new WaitForSecondsRealtime(.15f);
   Assert.AreEqual("Working",Get(service,"Phase").ToString());Assert.Less(Vector3.Distance(player.transform.position,inside.position),.6f);
   Assert.AreEqual(true,Static("CurtainCall.Player.InputGate","IsBlocked"));
   float until=Time.realtimeSinceStartup+9;
   while(Get(service,"Phase").ToString()!="Idle"&&Time.realtimeSinceStartup<until)yield return null;
   Assert.AreEqual("Idle",Get(service,"Phase").ToString());
   int lost=(int)T("CurtainCall.Player.PlayerCondition").GetMethod("CountLimbs").Invoke(null,new[]{Get(player,"LostParts")});Assert.AreEqual(1,lost);
   Assert.Less(Vector3.Distance(player.transform.position,outside.position),.6f);yield return new WaitForSecondsRealtime(.2f);
   Assert.AreEqual(false,Static("CurtainCall.Player.InputGate","IsBlocked"));
   // Clearing a service while working must release movement and return the buyer.
   Call(service,"RequestMedical",1);yield return null;Call(room,"Interact");yield return new WaitForSecondsRealtime(.15f);
   Assert.AreEqual("Working",Get(service,"Phase").ToString());
   Call(Private(service,"book"),"Clear");yield return new WaitForSecondsRealtime(.25f);
   Assert.AreEqual(false,Static("CurtainCall.Player.InputGate","IsBlocked"));Assert.Less(Vector3.Distance(player.transform.position,outside.position),.6f);
  }
 }
}
