using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class EscapeRouteTimerTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        static Type Timer => T("CurtainCall.EscapeRoute.EscapeRouteTimer");
        static object Get(object o,string n) => o.GetType().GetProperty(n,Flags)?.GetValue(o) ?? o.GetType().GetField(n,Flags)?.GetValue(o);
        static void Set(object o,string n,object v)
        {var p=o.GetType().GetProperty(n,Flags);if(p!=null)p.SetValue(o,v);else o.GetType().GetField(n,Flags).SetValue(o,v);}
        static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
        static bool Apply(object timer,uint version,int token,int round,double epoch,double duration=480)
            =>(bool)Call(timer,"ApplyState",version,token,round,epoch,duration);

        [TestCase(90,100,480)] [TestCase(100,100,480)] [TestCase(160,100,420)]
        [TestCase(579.9,100,.1)] [TestCase(580,100,0)] [TestCase(1000,100,0)] [TestCase(1000,-1,480)]
        public void SharedServerEpochCountsEightMinutesWithoutNegativeOrEarlyTime(double now,double epoch,double expected)
        {
            double remaining=(double)Timer.GetMethod("RemainingAt").Invoke(null,new object[]{now,epoch,480d});
            Assert.That(remaining,Is.EqualTo(expected).Within(.00001));
        }

        [Test]
        public void LateClientCannotExtendDeadlineWithDuplicateOrOlderSnapshot()
        {
            var go=new GameObject("Offline timer snapshot fixture");go.SetActive(false);
            try
            {
                var timer=go.AddComponent(Timer);
                Assert.That(Apply(timer,5,0,2,100),Is.True);
                Assert.That(Apply(timer,5,0,2,100),Is.True);
                Assert.That(Apply(timer,4,0,2,160),Is.False);
                Assert.That(Apply(timer,5,0,2,160),Is.False);
                Assert.That(Apply(timer,5,0,2,100,600),Is.False);
                Assert.That(Get(timer,"StartedAt"),Is.EqualTo(100d));
                Assert.That(Get(timer,"DurationSeconds"),Is.EqualTo(480d));
            }
            finally {Object.DestroyImmediate(go);}
        }

        [Test]
        public void RestartAcknowledgmentRejectsOldRunningClockBeforeAndAfterReset()
        {
            var go=new GameObject("Offline timer restart fixture");go.SetActive(false);
            try
            {
                var timer=go.AddComponent(Timer);Assert.That(Apply(timer,8,0,1,100),Is.True);
                int token=(int)Call(Get(timer,"resetBarrier"),"Begin");
                Assert.That(Get(timer,"HasStarted"),Is.False);
                Assert.That(Apply(timer,8,0,1,100),Is.False);
                Assert.That(Apply(timer,9,token,1,-1),Is.True);
                Assert.That(Get(timer,"HasStarted"),Is.False);
                Assert.That(Apply(timer,8,0,1,100),Is.False);
                Assert.That(Apply(timer,10,0,1,500),Is.True);
                Assert.That(Get(timer,"StartedAt"),Is.EqualTo(500d));
            }
            finally {Object.DestroyImmediate(go);}
        }

        [Test]
        public void InvalidDeadlinePacketsNeverActivateCountdown()
        {
            var go=new GameObject("Offline timer validation fixture");go.SetActive(false);
            try
            {
                var timer=go.AddComponent(Timer);
                Assert.That(Apply(timer,1,-1,0,100),Is.False);
                Assert.That(Apply(timer,1,0,-1,100),Is.False);
                foreach(double epoch in new[]{-2d,double.NaN,double.PositiveInfinity})Assert.That(Apply(timer,1,0,0,epoch),Is.False);
                foreach(double duration in new[]{0d,-1d,double.NaN,double.PositiveInfinity})Assert.That(Apply(timer,1,0,0,100,duration),Is.False);
                Assert.That(Get(timer,"HasStarted"),Is.False);
            }
            finally {Object.DestroyImmediate(go);}
        }

        [Test]
        public void RemoteInjuredPlayerCanEnterRotatedBarredHallWithoutJumpOrCrewRequirements()
        {
            var root=new GameObject("Rotated hall fixture");root.SetActive(false);
            var actor=new GameObject("Remote entrant fixture");actor.SetActive(false);Component player=null;
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(-200,-16,118.3f),Quaternion.Euler(0,180,0));
                var timer=root.AddComponent(Timer);
                var volume=new GameObject("Barred hall");volume.transform.SetParent(root.transform,false);
                volume.transform.localPosition=new Vector3(15,2.5f,11.25f);
                var box=volume.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(30,5,22.5f);
                Set(timer,"startArea",box);root.SetActive(true);
                player=actor.AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                Set(player,"IsSpawned",true);Set(player,"OwnerClientId",69UL);Set(player,"IsOwner",false);
                actor.transform.position=root.transform.TransformPoint(new Vector3(-.5f,0,11));actor.SetActive(true);
                Assert.That(Call(timer,"CanStart",player),Is.False,"Stairs and approach corridor must not start the clock.");
                actor.transform.position=root.transform.TransformPoint(new Vector3(1,0,11));
                var lost=Get(player,"lostParts");Set(lost,"m_InternalValue",Enum.ToObject(T("CurtainCall.Player.BodyPart"),5));
                Assert.That(Call(timer,"CanStart",player),Is.True,"An injured remote entrant counts immediately.");
                Set(player,"IsSpawned",false);Assert.That(Call(timer,"CanStart",player),Is.False);Set(player,"IsSpawned",true);
                var state=Get(player,"state");
                foreach(var name in new[]{"Arrived","Retired","Fallen"})
                {Set(state,"m_InternalValue",Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"),name));Assert.That(Call(timer,"CanStart",player),Is.False);}
                Set(state,"m_InternalValue",Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"),"Normal"));
                box.enabled=false;Assert.That(Call(timer,"CanStart",player),Is.False);
            }
            finally
            {
                if(player!=null)Set(player,"IsSpawned",false);
                Object.DestroyImmediate(actor);Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExistingWaitingRoomDisplayShowsEightMinutesAndItsWarningStates()
        {
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/Retained/RestTimerV28/W1_RestTimer_v2_8.prefab");
                var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview);
                var view=root.GetComponent(T("CurtainCall.UI.RestTimerV28.RestTimerView"));
                var snapshotType=T("CurtainCall.UI.RestTimerV28.RestTimerSnapshot");
                object Snapshot(double remaining)=>Activator.CreateInstance(snapshotType,new[]{(object)true,1,
                    Enum.Parse(T("CurtainCall.UI.RestTimerV28.RestTimeOfDay"),"Unknown"),Enum.Parse(T("CurtainCall.UI.RestTimerV28.RestTimerMode"),"Limited"),remaining,480d,false});
                Call(view,"SetSnapshot",Snapshot(480),false);
                Assert.That(Get(view,"CountdownText"),Is.EqualTo("8:00"));
                Assert.That(Get(view,"FillAmount"),Is.EqualTo(1f));
                foreach(var item in new[]{(30d,"Warning"),(10d,"Critical"),(0d,"Expired")})
                {Call(view,"SetSnapshot",Snapshot(item.Item1),false);Assert.That(Get(view,"VisualState").ToString(),Is.EqualTo(item.Item2));}
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
