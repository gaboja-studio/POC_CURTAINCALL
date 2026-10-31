using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace CurtainCall.UI.SceneIntegration.Tests {
 public sealed partial class DevStartLauncherPlayTests {
  [UnityTest] public IEnumerator M2_NightPowerGatesAndBedWake() {
   // 밤 연계 튜토리얼(조작 잠금·클로즈업)은 따로 확인한다. 여기서는 끄고 메모리 전용으로 둔다(PC 설정 보존).
   FreshTutorial();T(TutorialType).GetProperty("Enabled").SetValue(null,false);
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
   var bed=Find("CurtainCall.WaitingRoom.NightBed");
   // 2026-10-31: 스케줄표 확정 전에는 침대에 누울 수 없고(진행 멈춤 버그) 가이드 패널로 안내한다.
   Assert.AreEqual(false,Get(bed,"IsAvailable"),"스케줄표 미확정이면 침대를 쓸 수 없다");
   Call(bed,"Interact");yield return null;
   Assert.IsNull(bed.GetComponent(T("CurtainCall.WaitingRoom.BedRestView")),"미확정이면 눕지 않는다");
   Assert.AreEqual(false,Get(Find("CurtainCall.Campaign.CampaignRun"),"IsLocalReady"));
   Assert.AreEqual("잠자기",T("CurtainCall.WaitingRoom.WaitingRoomTutorialPanel").GetProperty("CurrentTitle").GetValue(null));
   ConfirmTonight();yield return null;
   Assert.AreEqual(true,Get(bed,"IsAvailable"));
   Call(bed,"Interact");yield return null;
   var rest=bed.GetComponent(T("CurtainCall.WaitingRoom.BedRestView"));Assert.AreEqual(true,Get(rest,"IsResting"));
   float until=Time.realtimeSinceStartup+16;
   while((Field(State,"Phase").ToString()=="Dawn" || Field(State,"Phase").ToString()=="Night") && Time.realtimeSinceStartup<until)yield return null;
   Assert.AreEqual(2,Field(State,"Day"));yield return new WaitForSecondsRealtime(3);
   Assert.AreEqual(false,Get(power,"IsCut"));Assert.IsTrue(lamps.Any(l=>l.enabled));
   foreach(var gate in gates) Assert.AreEqual(false,Get(gate,"IsLocked"));
   Assert.AreEqual(false,Get(rest,"IsResting"));
   EndTutorial();
  }
 }
}
