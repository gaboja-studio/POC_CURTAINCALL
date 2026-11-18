using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object cheerSettingsToRestore;
        bool cheerEnabledToRestore;
        [TearDown]
        public void RestoreCheerSettingAfterInterruptedTest()
        {
            if(cheerSettingsToRestore==null) return;
            cheerSettingsToRestore.GetType().GetField("cheerPointsEnabled").SetValue(cheerSettingsToRestore,cheerEnabledToRestore);
            cheerSettingsToRestore=null;
        }
        object CarryItem(object carry, ulong me, string id) => ((IEnumerable)Call(carry,"Held",me)).Cast<object>().FirstOrDefault(i=>(string)Get(i,"ItemId")==id);
        int UsesOf(object carry,ulong me,string id) { var item=CarryItem(carry,me,id); return item==null ? 0 : (int)Get(item,"Uses"); }
        void Give(object carry,ulong me,int uid,string id,int uses) => Assert.AreEqual("None",Call(carry,"Receive",me,Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"),uid,id,id,uses)).ToString());

        [UnityTest,Category("PerformanceEquipment")]
        public IEnumerator Performance_RescueRequiresSafeTargetAndMovesItWithoutInjury()
        {
            yield return Launch(2,"Performance",1,"HumanCannon","HumanCannon");
            var target=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            var network=Static("Unity.Netcode.NetworkManager","Singleton");
            var carry=Find("CurtainCall.WaitingRoom.PropCarry"); ulong me=(ulong)Get(target,"OwnerClientId");
            Give(carry,me,9801,"rescue-gloves",2);
            Assert.IsFalse((bool)Call(target,"CanUseEquipmentSlot",0));
            Call(target,"TryUseEquipmentSlot",0); yield return null;
            Assert.AreEqual(2,UsesOf(carry,me,"rescue-gloves"));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(0,39.5f,0); floor.transform.localScale=new Vector3(12,1,12);
            var prefab=(GameObject)Field(Field(network,"NetworkConfig"),"PlayerPrefab");
            var helper=UnityEngine.Object.Instantiate(prefab); var net=helper.GetComponent(T("Unity.Netcode.NetworkObject"));
            // Two host-owned actors exercise the real server/owner RPC route without a second process.
            Call(net,"SpawnWithOwnership",me,true);
            var caster=helper.GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var mover=(Behaviour)target.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var previousCamera=Camera.main;
            var testCamera=new GameObject("Rescue test view").AddComponent<Camera>();testCamera.tag="MainCamera";
            testCamera.transform.position=new Vector3(4,43,-6);testCamera.transform.LookAt(new Vector3(0,41,1));
            if(previousCamera)previousCamera.enabled=false;
            try
            {
                var helperMover=helper.GetComponent(T("CurtainCall.Player.PlayerMover"));
                yield return new WaitForSecondsRealtime(.5f); // Let the campaign spawn placement finish first.
                Call(helperMover,"SetStartPose",new Vector3(0,40.03f,0),Quaternion.identity,true);
                yield return null;
                Call(mover,"SetStartPose",new Vector3(0,41.1f,2.5f),Quaternion.identity,true);
                Call(mover,"LaunchBounce",1f); mover.enabled=false;
                Physics.SyncTransforms(); yield return null;
                Assert.AreSame(target,Get(caster,"RescuePreviewTarget"));
                var marker=caster.GetComponent(T("CurtainCall.MainStage.CheerPoints.RescueTargetIndicator"));
                yield return null;Assert.AreSame(target,Get(marker,"VisibleTarget"));
                var rotation=testCamera.transform.rotation;testCamera.transform.rotation=Quaternion.Euler(0,180,0);
                Assert.IsNull(Get(caster,"RescuePreviewTarget"),"Offscreen targets are not advertised.");testCamera.transform.rotation=rotation;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,41.3f,1.25f);wall.transform.localScale=new Vector3(1,3,.2f);
                Physics.SyncTransforms();Assert.IsNull(Get(caster,"RescuePreviewTarget"),"Occluded teammates are not advertised.");
                Assert.IsFalse((bool)Call(caster,"TryUseEquipmentSlot",0));Assert.AreEqual(2,UsesOf(carry,me,"rescue-gloves"));
                UnityEngine.Object.Destroy(wall);yield return null;
                var find=caster.GetType().GetMethod("FindEquipmentRescue",AnyInstance); object[] landing={Vector3.zero}; var found=find.Invoke(caster,landing);
                Assert.IsTrue((bool)Call(caster,"CanUseEquipmentSlot",0),$"caster={helper.transform.position} state={Get(caster,"State")} airborne={Get(caster,"IsAirborne")} target={target.transform.position} state={Get(target,"State")} air={Get(target,"IsAirborne")} found={found} gate={Static("CurtainCall.Player.InputGate","IsBlocked")}");
                Vector3 before=target.transform.position;
                Assert.IsTrue((bool)Call(caster,"TryUseEquipmentSlot",0));
                yield return new WaitForSecondsRealtime(.3f);
                Assert.AreEqual(1,UsesOf(carry,me,"rescue-gloves"));
                Assert.IsTrue((bool)Get(target,"IsBeingRescued"));
                Assert.Greater(Vector3.Distance(before,target.transform.position),.1f,"The pull has started moving.");
                Assert.Greater(Vector3.Distance(helper.transform.position,target.transform.position),1.6f,"Midway is not an instant teleport.");
                Assert.IsFalse((bool)Call(caster,"TryUseEquipmentSlot",0),"Cannot start another pull during this one.");
                yield return new WaitForSecondsRealtime(.8f);
                Assert.IsFalse((bool)Get(target,"IsBeingRescued"));
                Assert.Less(Vector3.Distance(helper.transform.position,target.transform.position),1.6f);
                Assert.Greater(Vector3.Distance(before,target.transform.position),.5f);
                Assert.AreEqual(0,Convert.ToInt32(Get(target,"LostParts")));
                Assert.IsFalse((bool)Get(target,"IsDroppingFromSupport"));
                // 2026-11-18 사용자: 땅에 선(달리기·앉기 포함) 동료도 범위 안이면 당길 수 있다. 상태만으로 제외되지 않는지 본다.
                Assert.IsTrue((bool)Get(target,"CanBeRescuePulled"),"A grounded teammate stays a valid pull target after the rescue.");
            }
            finally { mover.enabled=true; if(helper) UnityEngine.Object.Destroy(helper); UnityEngine.Object.Destroy(floor);UnityEngine.Object.Destroy(testCamera.gameObject);if(previousCamera)previousCamera.enabled=true; }
        }

        [UnityTest,Category("PerformanceEquipment")]
        public IEnumerator Performance_SlotBindingsPersistAndDriveReader()
        {
            yield return Launch(2,"Performance",1,"HumanCannon","HumanCannon");
            const string key="CurtainCall.Input.CommonBindings.v1";
            bool had=PlayerPrefs.HasKey(key); string saved=PlayerPrefs.GetString(key,"");
            var preferences=T("CurtainCall.Player.InputBindingPreferences");
            var original=(UnityEngine.InputSystem.InputActionAsset)preferences.GetMethod("CreateDraft").Invoke(null,null);
            var draft=(UnityEngine.InputSystem.InputActionAsset)preferences.GetMethod("CreateDefaultDraft").Invoke(null,null);
            var reader=((Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local")).GetComponent(T("CurtainCall.Player.PlayerInputReader"));
            try
            {
                Assert.AreEqual("<Keyboard>/q",draft.FindAction("Common/UseSlotOne",true).bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/e",draft.FindAction("Common/UseSlotTwo",true).bindings[0].effectivePath);
                UnityEngine.InputSystem.InputActionRebindingExtensions.ApplyBindingOverride(draft.FindAction("Common/UseSlotOne"),0,"<Keyboard>/g");
                object[] args={draft,null}; Assert.IsTrue((bool)preferences.GetMethod("TryApply").Invoke(null,args),args[1]?.ToString());
                var loaded=(UnityEngine.InputSystem.InputActionAsset)preferences.GetMethod("CreateDraft").Invoke(null,null);
                Assert.AreEqual("<Keyboard>/g",loaded.FindAction("Common/UseSlotOne").bindings[0].effectivePath);
                UnityEngine.Object.Destroy(loaded);
                CursorTestKey(null); yield return null; yield return null;
                CursorTestKey("G"); yield return null;
                Assert.IsTrue((bool)Field(Get(reader,"Current"),"UseSlotOnePressed"),"The rebound action reaches gameplay input.");
                Assert.AreEqual("G",Call(reader,"BindingLabel","UseSlotOne","Q"));
                var labels=T("CurtainCall.UI.SceneIntegration.CommonSettingsUI").GetMethod("ActionLabel",BindingFlags.NonPublic|BindingFlags.Static);
                StringAssert.Contains("1",(string)labels.Invoke(null,new object[]{"UseSlotOne",""}));
                StringAssert.Contains("2",(string)labels.Invoke(null,new object[]{"UseSlotTwo",""}));
            }
            finally
            {
                CursorTestKey(null); object[] restore={original,null}; preferences.GetMethod("TryApply").Invoke(null,restore);
                if(had) PlayerPrefs.SetString(key,saved); else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
                UnityEngine.Object.Destroy(original); UnityEngine.Object.Destroy(draft);
            }
        }

        [UnityTest,Category("PerformanceEquipment")]
        public IEnumerator Performance_ManualAirItemsChargeOnceAndBalloonCanFold()
        {
            yield return Launch(2,"Performance",1,"HumanCannon","HumanCannon");
            var carry=Find("CurtainCall.WaitingRoom.PropCarry");
            var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            ulong me=(ulong)Get(player,"OwnerClientId");
            var mover=player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var pilot=player.GetComponent(T("CurtainCall.HumanCannon.HumanCannonPilot"));
            Give(carry,me,9201,"spring-shoes",2); Give(carry,me,9301,"balloon-pants",2);
            yield return new WaitForSecondsRealtime(.3f);
            T("CurtainCall.MainStage.CheerPoints.Editor.PerformanceEquipmentAuthoring").GetMethod("CaptureLive").Invoke(null,new object[]{"HumanCannon"});
            Assert.IsFalse((bool)Call(player,"TryUseEquipmentSlot",0),"Ground use is invalid.");
            Assert.AreEqual(2,UsesOf(carry,me,"spring-shoes"));
            var launch=pilot.GetType().GetMethod("Launch",AnyInstance);
            launch.Invoke(pilot,new object[]{-1,950u,player.transform.position+Vector3.up*20,Quaternion.identity,Vector3.down*8});
            yield return null;
            Assert.IsFalse((bool)Get(pilot,"BalloonPantsActive"),"Carrying pants does not auto-activate.");
            Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",0));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(1,UsesOf(carry,me,"spring-shoes"));
            Assert.Greater(((Vector3)pilot.GetType().GetField("velocity",AnyInstance).GetValue(pilot)).y,0,"The extra jump replaces downward momentum.");
            Assert.IsFalse((bool)Call(player,"TryUseEquipmentSlot",0),"No third jump before landing.");
            Assert.AreEqual(1,UsesOf(carry,me,"spring-shoes"));
            Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",1));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsTrue((bool)Get(player,"BalloonEquipmentActive"));
            Assert.AreEqual(1,UsesOf(carry,me,"balloon-pants"));
            pilot.GetType().GetField("velocity",AnyInstance).SetValue(pilot,Vector3.down*15);
            yield return null;
            Assert.GreaterOrEqual(((Vector3)pilot.GetType().GetField("velocity",AnyInstance).GetValue(pilot)).y,-2.01f);
            Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",1),"Same slot folds the balloon.");
            yield return null;
            Assert.IsFalse((bool)Get(player,"BalloonEquipmentActive"));
            Assert.IsFalse((bool)Call(player,"TryUseEquipmentSlot",1),"Folding does not restore the airtime use.");
            Assert.AreEqual(1,UsesOf(carry,me,"balloon-pants"));
            // A confirmed landing starts a new air cycle, including the last-charge case.
            Call(player,"ResetEquipmentAirCycle");
            Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",1));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(0,UsesOf(carry,me,"balloon-pants"));
            Assert.IsTrue((bool)Get(player,"BalloonEquipmentActive"),"Last durability keeps its granted effect.");
            Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",1),"The now-empty original slot can still fold.");
            yield return null;
            Assert.IsFalse((bool)Get(player,"BalloonEquipmentActive"));
        }

        [UnityTest,Category("PerformanceEquipment")]
        public IEnumerator Performance_StickyPausesTimerOncePerHang()
        {
            yield return Launch(3,"Performance",0,"Tightrope","Tightrope");
            var carry=Find("CurtainCall.WaitingRoom.PropCarry");
            var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            ulong me=(ulong)Get(player,"OwnerClientId");
            Give(carry,me,9401,"sticky-gloves",3);
            var hangs=Static("CurtainCall.Tightrope.TightropeHangSystem","Current");
            var course=Static("CurtainCall.Tightrope.TightropeCourse","Current");
            var mover=player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover,"SetStartPose",(Vector3)Call(course,"GetLanePosition",1,6f),Quaternion.identity,true);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue((bool)Call(hangs,"ServerTryHang",player,6f,1));
            yield return null;
            var hang=((IEnumerable)Get(hangs,"Hangs")).Cast<object>().Single(h=>(ulong)h.GetType().GetField("Id").GetValue(h)==me);
            double Remaining()=>(double)hang.GetType().GetField("Remaining").GetValue(hang);
            Assert.AreEqual(0d,hang.GetType().GetField("Extra").GetValue(hang),"No automatic extension.");
            Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",0));
            yield return new WaitForSecondsRealtime(.1f);
            double before=Remaining();
            Assert.AreEqual(2,UsesOf(carry,me,"sticky-gloves"));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(Remaining(),Is.EqualTo(before).Within(.12),"The hold countdown pauses.");
            Assert.IsFalse((bool)Call(player,"TryUseEquipmentSlot",0));
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.Less(Remaining(),before-.1,"Countdown resumes after 3 seconds.");
            Call(player,"TryUseEquipmentSlot",0);
            yield return null;
            Assert.AreEqual(2,UsesOf(carry,me,"sticky-gloves"),"Same hold cannot spend again.");
        }

        [UnityTest,Category("PerformanceEquipment"),Category("CheerPointToggle")]
        public IEnumerator Performance_HoopAwardsOnceOnlyThroughOpening()
            => VerifyCheerPointToggle("Tightrope",0);

        [UnityTest,Category("PerformanceEquipment"),Category("CheerPointToggle")]
        public IEnumerator Performance_HumanCannonHoopToggleHidesAndBlocksRewards()
            => VerifyCheerPointToggle("HumanCannon",1);

        IEnumerator VerifyCheerPointToggle(string trick,int slot)
        {
            var settings=Static("CurtainCall.Settings.GameSettings","PerformanceEquipment");
            var enabled=settings.GetType().GetField("cheerPointsEnabled");
            bool original=(bool)enabled.GetValue(settings);
            cheerSettingsToRestore=settings; cheerEnabledToRestore=original;
            Assert.IsTrue(original,"The hanging rings ship enabled.");
            yield return Launch(3,"Performance",slot,trick,trick);
            // Ring rewards are measured independently of obstacle reactions and the +10 limb-cut bonus.
            var observers=UnityEngine.Object.FindObjectsByType(T("CurtainCall.Reward.PerformanceAudience"),FindObjectsSortMode.None).Cast<Behaviour>()
                .Concat(UnityEngine.Object.FindObjectsByType(T("CurtainCall.Tightrope.Saws.TightropeSaws"),FindObjectsSortMode.None).Cast<Behaviour>()).ToArray();
            var observerEnabled=observers.Select(a=>a.enabled).ToArray();
            foreach(var observer in observers) observer.enabled=false;
            var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            var mover=(Behaviour)player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var points=UnityEngine.Object.FindObjectsByType(T("CurtainCall.MainStage.CheerPoints.PerformanceCheerPoint"),FindObjectsSortMode.None).Cast<Component>().ToArray();
            Assert.AreEqual(5,points.Length);
            var point=points.First(p=>!(bool)Field(p,"equipmentRoute"));
            var mask=point.GetType().GetField("difficultyMask"); int originalMask=(int)mask.GetValue(point);
            var body=player.GetComponent<CharacterController>();
            Call(mover,"SetStartPose",point.transform.position-player.transform.TransformVector(body.center)-point.transform.forward,Quaternion.identity,true);
            mover.enabled=false;
            var rewards=Static("CurtainCall.Reward.PerformanceRewards","Instance");
            long Amount()=>Convert.ToInt64(Get(((IEnumerable)Get(Get(rewards,"State"),"Segments")).Cast<object>().First(),"Amount"));
            try
            {
                enabled.SetValue(settings,false);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.IsTrue(points.All(p=>p.GetComponentsInChildren<Renderer>().Length==0),"OFF hides all ring VFX.");
                long before=Amount();
                body.Move(point.transform.forward*2f);
                yield return null; yield return null;
                Assert.IsFalse((bool)Get(point,"Collected"),"An OFF crossing cannot claim a point.");
                Assert.AreEqual(before,Amount(),"An OFF crossing cannot award applause.");

                enabled.SetValue(settings,true);
                body.Move(-point.transform.forward*2f);
                yield return null; yield return null;
                Assert.Greater(point.GetComponentsInChildren<Renderer>().Length,0,"ON restores the authored visual.");
                Assert.IsFalse((bool)Get(point,"Collected"),"Enabling must not score the path travelled while OFF.");
                Assert.AreEqual(before,Amount());

                enabled.SetValue(settings,false);
                yield return null; yield return null;
                Assert.IsTrue(points.All(p=>p.GetComponentsInChildren<Renderer>().Length==0),"OFF hides existing visuals too.");
                body.Move(point.transform.forward*2f);
                yield return null; yield return null;
                Assert.AreEqual(before,Amount());
                enabled.SetValue(settings,true);
                body.Move(-point.transform.forward*2f);
                yield return null; yield return null;
                Assert.IsFalse((bool)Get(point,"Collected"));
                int level=(int)Get(rewards,"Difficulty");
                mask.SetValue(point,1 << (level%3));
                yield return null; yield return null;
                Assert.AreEqual(0,point.GetComponentsInChildren<Renderer>().Length,"A different difficulty's ring stays hidden.");
                body.Move(point.transform.forward*2f); yield return null; yield return null;
                Assert.AreEqual(before,Amount(),"A different difficulty cannot award this ring.");
                body.Move(-point.transform.forward*2f); yield return null; yield return null;
                mask.SetValue(point,originalMask); yield return null; yield return null;
                body.Move(point.transform.forward*2f);
                yield return null; yield return null;
                Assert.IsTrue((bool)Get(point,"Collected"),"A new ON crossing claims the point.");
                Assert.Greater(Amount(),before,"ON restores the reward.");
                long after=Amount();
                enabled.SetValue(settings,false); yield return null; yield return null;
                enabled.SetValue(settings,true); yield return null; yield return null;
                body.Move(-point.transform.forward*2f); yield return null; yield return null;
                Assert.IsTrue((bool)Get(point,"Collected"),"ON/OFF must preserve claims in the same attempt.");
                Assert.AreEqual(after,Amount(),"Toggling cannot award a claimed point twice.");
                Assert.AreEqual(0,point.GetComponentsInChildren<Collider>(true).Length,"Fire has no damage or physical collision.");
            }
            finally { enabled.SetValue(settings,original); mask.SetValue(point,originalMask); mover.enabled=true; for(int i=0;i<observers.Length;i++) if(observers[i]) observers[i].enabled=observerEnabled[i]; }
        }
    }
}
