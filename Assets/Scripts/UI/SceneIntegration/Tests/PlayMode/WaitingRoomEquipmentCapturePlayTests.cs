using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        void Film(string method, params object[] args) => T("CurtainCall.MainStage.CheerPoints.Editor.EquipmentUseRecording").GetMethod(method).Invoke(null,args);
        int LiveFx() => UnityEngine.Object.FindObjectsByType(T("CurtainCall.MainStage.CheerPoints.EquipmentUseVisual"),FindObjectsSortMode.None).Length;

        [UnityTest, Category("PerformanceEquipmentCapture")]
        public IEnumerator CaptureEquipment_Spring()
        {
            if(!System.IO.File.Exists("Temp/PerformanceEquipment/record-use-videos.flag")) Assert.Ignore("Opt-in gameplay recording");
            yield return Launch(2,"Performance",1,"HumanCannon","HumanCannon");
            var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            var mover=player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var carry=Find("CurtainCall.WaitingRoom.PropCarry");ulong me=(ulong)Get(player,"OwnerClientId");Give(carry,me,9901,"spring-shoes",2);
            Film("Begin","01-spring-shoes",player.gameObject,player.transform.position+Vector3.up*2f,new Vector3(4,2.5f,-7.5f),false,"스프링 신발 · 공중에서 한 번 더 도약");
            try
            {
                while(Time.timeScale == 0) yield return null;
                yield return new WaitForSecondsRealtime(1);
                Call(mover,"LaunchBounce",4.5f);Film("Caption","첫 점프 · 아직 내구도 소모 없음");
                yield return new WaitForSecondsRealtime(.45f);
                Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",0));Film("Caption","1슬롯 사용 · 발밑 충격파 / 내구도 2 → 1");
                yield return new WaitForSecondsRealtime(.12f);Assert.Greater(LiveFx(),0);Film("Snapshot","use");
                Assert.AreEqual(1,UsesOf(carry,me,"spring-shoes"));Assert.IsFalse((bool)Call(player,"TryUseEquipmentSlot",0));
                yield return new WaitForSecondsRealtime(.5f);Film("Caption","상승 후 착지 · 같은 체공에서 추가 사용 불가");
                yield return new WaitForSecondsRealtime(2.4f);Assert.AreEqual(0,LiveFx());Film("Snapshot","end");
                yield return new WaitForSecondsRealtime(.7f);
            }
            finally {Film("End");}
        }

        [UnityTest, Category("PerformanceEquipmentCapture")]
        public IEnumerator CaptureEquipment_Balloon()
        {
            if(!System.IO.File.Exists("Temp/PerformanceEquipment/record-use-videos.flag")) Assert.Ignore("Opt-in gameplay recording");
            yield return Launch(2,"Performance",1,"HumanCannon","HumanCannon");
            var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            var pilot=player.GetComponent(T("CurtainCall.HumanCannon.HumanCannonPilot"));
            var carry=Find("CurtainCall.WaitingRoom.PropCarry");ulong me=(ulong)Get(player,"OwnerClientId");Give(carry,me,9902,"spring-shoes",2);Give(carry,me,9903,"balloon-pants",1);
            pilot.GetType().GetMethod("Launch",AnyInstance).Invoke(pilot,new object[]{-1,995u,player.transform.position+Vector3.up*14,Quaternion.identity,new Vector3(0,-3,1)});
            Film("Begin","04-balloon-pants",player.gameObject,Vector3.zero,new Vector3(3,1.5f,-5),true,"벌룬 바지 · 하강 감속과 공중 조작");
            try
            {
                while(Time.timeScale == 0) yield return null;
                yield return new WaitForSecondsRealtime(.65f);
                Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",1));Film("Caption","2슬롯 사용 · 바지 팽창 / 마지막 내구도 소진");
                yield return new WaitForSecondsRealtime(.5f);Assert.Greater(LiveFx(),0);Film("Snapshot","use");
                Assert.AreEqual(0,UsesOf(carry,me,"balloon-pants"));Assert.IsTrue((bool)Get(player,"BalloonEquipmentActive"));
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",1));Film("Caption","같은 슬롯 재입력 → 접기 / 다시 빠르게 하강");
                float fadedBy=Time.realtimeSinceStartup+1.2f;
                while(LiveFx()>0 && Time.realtimeSinceStartup<fadedBy)yield return null;
                Film("Snapshot","fold");
                Assert.AreEqual(0,LiveFx(),"Folding removes the world effect after its short fade.");
                Assert.IsFalse((bool)Get(player,"BalloonEquipmentActive"));
                yield return new WaitForSecondsRealtime(1.2f);
            }
            finally {Film("End");}
        }

        [UnityTest, Category("PerformanceEquipmentCapture")]
        public IEnumerator CaptureEquipment_Sticky()
        {
            if(!System.IO.File.Exists("Temp/PerformanceEquipment/record-use-videos.flag")) Assert.Ignore("Opt-in gameplay recording");
            yield return Launch(3,"Performance",0,"Tightrope","Tightrope");
            var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            var carry=Find("CurtainCall.WaitingRoom.PropCarry");ulong me=(ulong)Get(player,"OwnerClientId");Give(carry,me,9904,"sticky-gloves",3);
            var hangs=Static("CurtainCall.Tightrope.TightropeHangSystem","Current");var course=Static("CurtainCall.Tightrope.TightropeCourse","Current");
            var mover=player.GetComponent(T("CurtainCall.Player.PlayerMover"));Call(mover,"SetStartPose",(Vector3)Call(course,"GetLanePosition",1,6f),Quaternion.identity,true);
            yield return new WaitForSecondsRealtime(.3f);Assert.IsTrue((bool)Call(hangs,"ServerTryHang",player,6f,1));yield return null;
            var hang=((IEnumerable)Get(hangs,"Hangs")).Cast<object>().Single(h=>(ulong)Field(h,"Id")==me);
            double Remaining()=>(double)Field(hang,"Remaining");
            Film("Begin","03-sticky-gloves",player.gameObject,player.transform.position+Vector3.up*.85f,new Vector3(2,1,-4),false,"끈끈이 장갑 · 매달림 시간 3초 정지");
            try
            {
                while(Time.timeScale == 0) yield return null;
                yield return new WaitForSecondsRealtime(.7f);
                Assert.IsTrue((bool)Call(player,"TryUseEquipmentSlot",0));yield return new WaitForSecondsRealtime(.1f);
                double before=Remaining();Assert.Greater(LiveFx(),0);Film("Snapshot","use");
                float pausedUntil=Time.realtimeSinceStartup+1.5f;
                while(Time.realtimeSinceStartup<pausedUntil) {Film("Caption",$"접착 광채 유지 · 남은 매달림 {Remaining():0.0}초 / 내구도 3 → 2");yield return null;}
                Assert.That(Remaining(),Is.EqualTo(before).Within(.12));
                while((bool)Get(player,"StickyEquipmentActive"))yield return null;
                float resumedUntil=Time.realtimeSinceStartup+1.4f;
                while(Time.realtimeSinceStartup<resumedUntil) {Film("Caption",$"광채 종료 · 남은 시간 {Remaining():0.0}초, 감소 재개");yield return null;}
                Film("Snapshot","end");
                Assert.AreEqual(0,LiveFx());Assert.AreEqual(2,UsesOf(carry,me,"sticky-gloves"));Assert.Less(Remaining(),before-.7);
            }
            finally {Film("End");}
        }

        [UnityTest, Category("PerformanceEquipmentCapture")]
        public IEnumerator CaptureEquipment_Rescue()
        {
            if(!System.IO.File.Exists("Temp/PerformanceEquipment/record-use-videos.flag")) Assert.Ignore("Opt-in gameplay recording");
            yield return Launch(2,"Performance",1,"HumanCannon","HumanCannon");
            var target=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            var carry=Find("CurtainCall.WaitingRoom.PropCarry");ulong me=(ulong)Get(target,"OwnerClientId");Give(carry,me,9905,"rescue-gloves",2);
            var network=Static("Unity.Netcode.NetworkManager","Singleton");var prefab=(GameObject)Field(Field(network,"NetworkConfig"),"PlayerPrefab");
            var helper=UnityEngine.Object.Instantiate(prefab);Call(helper.GetComponent(T("Unity.Netcode.NetworkObject")),"SpawnWithOwnership",me,true);
            var caster=helper.GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var mover=target.GetComponent(T("CurtainCall.Player.PlayerMover"));var helperMover=helper.GetComponent(T("CurtainCall.Player.PlayerMover"));
            yield return new WaitForSecondsRealtime(.6f);
            Vector3 home=new Vector3(4.8f,5.05f,-3.5f);Call(helperMover,"SetStartPose",home,Quaternion.identity,true);
            Call(mover,"SetStartPose",home+Vector3.forward*2.5f,Quaternion.identity,true);yield return null;
            Film("Begin","02-rescue-gloves",helper,home+new Vector3(0,1.6f,1.2f),new Vector3(5.5f,2.8f,-6),false,"구조 장갑 · 동료를 안전한 내 곁으로");
            try
            {
                while(Time.timeScale == 0) yield return null;
                yield return new WaitForSecondsRealtime(.8f);
                Call(mover,"LaunchBounce",5.5f);Film("Caption","앞쪽 동료가 공중에 있음 · 안전 지지면 확인");
                yield return new WaitForSecondsRealtime(.3f);
                Film("Caption","장갑 소지자 화면에만 현재 사용키와 구조 표식 표시");Film("Snapshot","target");
                Assert.AreSame(target,Get(caster,"RescuePreviewTarget"));
                yield return new WaitForSecondsRealtime(.25f);
                Assert.IsTrue((bool)Call(caster,"CanUseEquipmentSlot",0));Vector3 before=target.transform.position;
                Assert.IsTrue((bool)Call(caster,"TryUseEquipmentSlot",0));Film("Caption","1슬롯 사용 · 금빛 줄로 서서히 끌어당김 / 내구도 2 → 1");
                yield return new WaitForSecondsRealtime(.12f);Assert.Greater(LiveFx(),0);Film("Snapshot","use");
                Assert.IsTrue((bool)Get(target,"IsBeingRescued"));
                yield return new WaitForSecondsRealtime(.9f);
                Assert.Less(Vector3.Distance(helper.transform.position,target.transform.position),1.6f);Assert.Greater(Vector3.Distance(before,target.transform.position),.5f);
                yield return new WaitForSecondsRealtime(1);Film("Caption","구조 완료 · 부상 없음 / 지상의 동료에게 재사용 불가");
                Assert.AreEqual(1,UsesOf(carry,me,"rescue-gloves"));Assert.AreEqual(0,Convert.ToInt32(Get(target,"LostParts")));
                yield return new WaitForSecondsRealtime(2);Assert.AreEqual(0,LiveFx());Film("Snapshot","end");
            }
            finally {Film("End");UnityEngine.Object.Destroy(helper);}
        }
    }
}
