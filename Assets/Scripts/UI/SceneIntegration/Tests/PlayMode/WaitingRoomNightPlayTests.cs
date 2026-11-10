using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace CurtainCall.UI.SceneIntegration.Tests {
 public sealed partial class DevStartLauncherPlayTests {
  [UnityTest, Category("BreakFollowup")] public IEnumerator NightGuide_CountdownStartsAfterLastCameraReturns() {
   FreshTutorial();
   try {
    yield return Launch(1,"Night",0,"","WaitingRoomBase");
    yield return WaitGuide(TutorialTitle("N01"),10);
    Assert.AreEqual("WaitingForExit",Field(State,"Timer").ToString());
    Assert.AreEqual(300f,Field(State,"TimeRemaining"));
    yield return WaitGuide(TutorialTitle("N05"),65);
    Assert.AreEqual("WaitingForExit",Field(State,"Timer").ToString(),"Last guide is marked seen while its camera is still running");
    yield return CannonWait(()=>Field(State,"Timer").ToString()!="WaitingForExit",18,"Return every guide camera before countdown starts");
    Assert.Greater((float)Field(State,"TimeRemaining"),298f);
    Assert.AreEqual(false,Get(Find("CurtainCall.WaitingRoom.WaitingRoomTutorialCamera"),"IsBusy"));
   } finally {EndTutorial();}
  }

  [UnityTest, Category("BreakFollowup"), Category("BreakFollowupRetry")] public IEnumerator PursuitMaintenance_TimeSpecificWarningsAndCheats() {
   yield return Launch(2,"Night",0,"","WaitingRoomPursuit");
   var run=Static("CurtainCall.Campaign.CampaignRun","Instance");
   var director=Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
   var cheats=T("CurtainCall.Dev.DevCheatPanel").GetMethod("EnsureExists").Invoke(null,null);
   Assert.AreEqual(true,Get(cheats,"ShowsPursuitControls"));
   var settings=Static("CurtainCall.Settings.GameSettings","WaitingRoom");
   string saved=JsonUtility.ToJson(settings);
   var zones=(Behaviour)Find("CurtainCall.WaitingRoom.ExplorationTimerZones");
   bool zoneEnabled=zones.enabled; zones.enabled=false;
   string VisibleText()=>string.Join("\n",UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Where(t=>t.isActiveAndEnabled).Select(t=>t.text));
   try {
    JsonUtility.FromJsonOverwrite("{\"maintenancePresentation\":{\"minute\":{\"title\":\"정비 전용 60\",\"body\":\"정비 1분 본문 확인\"},\"thirtySeconds\":{\"title\":\"정비 전용 30\",\"body\":\"정비 30초 본문 확인\"}}}",settings);
    Assert.AreEqual(true,Call(director,"DebugSetTimer",61f));
    yield return CannonWait(()=>VisibleText().Contains("정비 1분 본문 확인"),4,"Minute banner must use maintenance settings, including its body");
    Assert.AreEqual(true,Call(director,"DebugSetTimer",31f));
    yield return CannonWait(()=>VisibleText().Contains("정비 30초 본문 확인"),4,"Thirty-second banner must use maintenance settings");
    Assert.AreEqual(true,Call(director,"DebugExpireTimer"));
    Assert.AreEqual("Pursuit",Field(State,"Timer").ToString());
    Assert.AreEqual(true,Call(director,"DebugSetStage",1));
    Assert.AreEqual(true,Call(director,"DebugResetTimer"));
   } finally {JsonUtility.FromJsonOverwrite(saved,settings);zones.enabled=zoneEnabled;}
  }

  [UnityTest] [Category("BreakFlow")] public IEnumerator PursuitMaintenance_SleepProtectionAndMorningInjuredReturn() {
   FreshTutorial(); T(TutorialType).GetProperty("Enabled").SetValue(null,false);
   yield return Launch(1,"Night",0,"","WaitingRoomPursuit");
   var run=Static("CurtainCall.Campaign.CampaignRun","Instance");
   var director=Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
   var gate=T("CurtainCall.Campaign.RunState").GetProperty("NightEndGate");
   var previous=gate.GetValue(null);
   try {
    gate.SetValue(null,new System.Func<bool>(()=>false));
    Assert.AreEqual(true,Call(run,"DebugBeginPursuit")); yield return null;
    Assert.AreEqual("Night",Field(State,"Phase").ToString());
    Assert.AreEqual("Pursuit",Field(State,"Timer").ToString());
    Assert.AreEqual(true,Call(run,"SetReady",true));
    Assert.AreEqual(false,Call(director,"IsAvailable",LocalPlayer.gameObject),"A sleeping player cannot be targeted or captured");
    Assert.AreEqual(true,Call(run,"IsPlayerSleeping",LocalId));
    Assert.AreEqual(true,Call(run,"SetReady",false));
    Assert.AreEqual(true,Call(director,"IsAvailable",LocalPlayer.gameObject));
    var mover=LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
    var spawn=(Vector3)Priv(mover,"startPosition");
    Call(mover,"Relocate",spawn+Vector3.right*4,Quaternion.identity);
    Call(LocalPlayer,"ServerRetireFromPursuit");
    Assert.AreEqual("Retired",Get(LocalPlayer,"State").ToString());
    Assert.AreEqual(true,Call(run,"NotifyAllPursuitCaught"));
    float until=Time.realtimeSinceStartup+16;
    while(Field(State,"Phase").ToString()=="Dawn"&&Time.realtimeSinceStartup<until)yield return null;
    Assert.AreEqual(2,Field(State,"Day")); Assert.AreEqual("RestTime",Field(State,"Phase").ToString());
    yield return null;
    Assert.AreEqual("Normal",Get(LocalPlayer,"State").ToString());
    Assert.AreEqual(T("CurtainCall.Player.PlayerCondition").GetField("OneArmParts").GetValue(null),Get(LocalPlayer,"LostParts"));
    Assert.Less(Vector3.Distance(spawn,LocalPlayer.transform.position),1.5f,"Morning revival returns to the waiting-room spawn");
   } finally {gate.SetValue(null,previous);EndTutorial();}
  }
  [UnityTest] [Category("BreakFlow")] public IEnumerator M2_NightPowerGatesAndBedWake() {
   // 밤 연계 튜토리얼(조작 잠금·클로즈업)은 따로 확인한다. 여기서는 끄고 메모리 전용으로 둔다(PC 설정 보존).
   FreshTutorial();T(TutorialType).GetProperty("Enabled").SetValue(null,false);
   yield return Launch(1,"Night",0,"","WaitingRoomBase");
   var power=Find("CurtainCall.WaitingRoom.NightPower");
   Assert.IsNotNull(power);yield return new WaitForSecondsRealtime(.2f);
   Assert.AreEqual(false,Get(power,"IsCut"));
   var root=power.transform;
   var gates=root.GetComponentsInChildren(T("CurtainCall.WaitingRoom.NightDoorLock"));
   Assert.AreEqual(4,gates.Length);
   foreach(var gate in gates) Assert.AreEqual(false,Get(gate,"IsLocked"));
   var lamps=((IEnumerable)Private(power,"circuit")).Cast<Light>().Where(l=>l!=null).ToArray();
   Assert.Greater(lamps.Length,0); Assert.IsTrue(lamps.Any(l=>l.enabled));
   var bed=Find("CurtainCall.WaitingRoom.NightBed");
   Assert.AreEqual(true,Get(bed,"IsAvailable"),"No schedule confirmation is needed to sleep");
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
