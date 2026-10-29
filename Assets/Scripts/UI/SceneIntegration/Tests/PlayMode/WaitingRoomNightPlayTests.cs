using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace CurtainCall.UI.SceneIntegration.Tests {
 public sealed partial class DevStartLauncherPlayTests {
  [UnityTest] public IEnumerator M2_NightPowerGatesAndBedWake() {
   yield return Launch(1,"Night",0,"","WaitingRoomBase");
   var power=Find("CurtainCall.WaitingRoom.NightPower");
   Assert.IsNotNull(power);yield return new WaitForSecondsRealtime(.2f);
   Assert.AreEqual(true,Get(power,"IsCut"));
   var root=power.transform;
   var gates=root.GetComponentsInChildren(T("CurtainCall.WaitingRoom.NightDoorLock"));
   Assert.AreEqual(4,gates.Length);
   yield return new WaitForSecondsRealtime(3);
   foreach(var gate in gates){Assert.AreEqual(true,Get(gate,"IsFullyClosed"));var c=gate.GetComponentsInChildren<Collider>().Single(c=>c.name=="GateCollision");Assert.Greater(c.bounds.size.y,4);}
   var lamps=((IEnumerable)Private(power,"circuit")).Cast<Light>().Where(l=>l!=null).ToArray();
   Assert.Greater(lamps.Length,0);Assert.IsTrue(lamps.All(l=>!l.enabled));
   var bed=Find("CurtainCall.WaitingRoom.NightBed");Call(bed,"Interact");yield return null;
   var rest=bed.GetComponent(T("CurtainCall.WaitingRoom.BedRestView"));Assert.AreEqual(true,Get(rest,"IsResting"));
   float until=Time.realtimeSinceStartup+16;
   while((Field(State,"Phase").ToString()=="Dawn" || Field(State,"Phase").ToString()=="Night") && Time.realtimeSinceStartup<until)yield return null;
   Assert.AreEqual(2,Field(State,"Day"));yield return new WaitForSecondsRealtime(3);
   Assert.AreEqual(false,Get(power,"IsCut"));Assert.IsTrue(lamps.Any(l=>l.enabled));
   foreach(var gate in gates) Assert.AreEqual(false,Get(gate,"IsLocked"));
   Assert.AreEqual(false,Get(rest,"IsResting"));
  }
 }
}
