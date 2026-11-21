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
        static bool Apply(object timer,uint version,int token,int round,double epoch)
            =>(bool)Call(timer,"ApplyState",version,token,round,epoch);

        [TestCase(90,100,0)] [TestCase(100,100,0)] [TestCase(160,100,60)]
        [TestCase(580,100,480)] [TestCase(1000,100,900)] [TestCase(86500,100,86400)] [TestCase(1000,-1,0)]
        [TestCase(double.NaN,100,0)] [TestCase(1000,double.PositiveInfinity,0)]
        public void EncounterClockContinuesPastFormerDeadlineWithoutCountingDown(double now,double epoch,double expected)
        {
            double elapsed=(double)Timer.GetMethod("ElapsedAt").Invoke(null,new object[]{now,epoch});
            Assert.That(elapsed,Is.EqualTo(expected).Within(.00001));
        }

        [TestCase(true, 2, 5, true)]
        [TestCase(true, 5, 5, false)]
        [TestCase(false, 2, 5, false)]
        public void TransitionAudit_ClockPlacementTracksTheActualAvatarAndExpires(bool sameAvatar, double now, double deadline, bool waiting)
        {
            var probe = Timer.GetMethod("WaitForPlacement", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(probe.Invoke(null, new object[] { sameAvatar, Vector3.right * 12f, Vector3.zero, now, deadline }),
                Is.EqualTo(waiting));
        }

        [Test]
        public void TransitionAudit_EncounterClockStartsFromTheLatestOwnerSample()
        {
            var root = new GameObject("Entrance sample");
            var avatar = new GameObject("Interpolated remote sample");
            avatar.SetActive(false);
            try
            {
                var area = root.AddComponent<BoxCollider>();
                area.size = Vector3.one * 3f;
                var timer = root.AddComponent(Timer);
                Set(timer, "startArea", area);
                var player = avatar.AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var network = avatar.AddComponent(T("Unity.Netcode.Components.NetworkTransform"));
                avatar.transform.position = Vector3.forward * -10f;
                Set(network, "m_InternalCurrentPosition", Vector3.zero);
                Set(player, "networkTransform", network);
                Set(player, "IsSpawned", true);
                Set(Get(player, "slot"), "m_InternalValue", 0);
                Assert.That(Call(timer, "CanStart", player), Is.True);
                Set(network, "m_InternalCurrentPosition", Vector3.forward * -10f);
                Assert.That(Call(timer, "CanStart", player), Is.False);
                Set(player, "IsSpawned", false);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(avatar); }
        }

        [Test]
        public void TraversalAudit_AnUnassignedAvatarCannotStartTheClock()
        {
            var root = new GameObject("Clock start fixture");
            root.SetActive(false);
            try
            {
                var area = new GameObject("Entrance area");
                area.transform.SetParent(root.transform, false);
                var box = area.AddComponent<BoxCollider>();
                box.size = Vector3.one * 4;
                var timer = root.AddComponent(Timer);
                Set(timer, "startArea", box);
                var avatar = new GameObject("Avatar awaiting slot");
                avatar.transform.SetParent(root.transform, false);
                var player = avatar.AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                root.SetActive(true);
                Set(player, "IsSpawned", true);
                var slot = Get(player, "slot");
                Set(slot, "m_InternalValue", -1);
                Assert.That(Call(timer, "CanStart", player), Is.False);
                Set(slot, "m_InternalValue", 0);
                Assert.That(Call(timer, "CanStart", player), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TraversalAudit_AnOldEncounterCannotRunDuringTheNextCampaignGeneration()
        {
            var root = new GameObject("Encounter generation fixture");
            root.SetActive(false);
            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var instance = T("CurtainCall.Campaign.CampaignRun").GetProperty("Instance", statics);
            var previous = instance.GetValue(null);
            try
            {
                var content = root.AddComponent(T("CurtainCall.EscapeRoute.EscapeCampaignContent"));
                Set(content, "IsCampaign", true);
                var run = root.AddComponent(instance.PropertyType);
                var state = Get(run, "state");
                Set(state, "Generation", 3);
                Set(run, "state", state);
                instance.SetValue(null, run);
                var timer = root.AddComponent(Timer);
                Set(timer, "content", content);
                Assert.That(Apply(timer, 4, 0, 2, 100), Is.True);
                Assert.That(Get(timer, "HasStarted"), Is.False);
                Assert.That(Get(timer, "ElapsedSeconds"), Is.EqualTo(0d));
                Assert.That(Apply(timer, 5, 0, 3, 200), Is.True);
                Assert.That(Get(timer, "HasStarted"), Is.True);
            }
            finally
            {
                instance.SetValue(null, previous);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LateClientKeepsTheSameEncounterEpochWithDuplicateOrOlderSnapshot()
        {
            var go=new GameObject("Offline timer snapshot fixture");go.SetActive(false);
            try
            {
                var timer=go.AddComponent(Timer);
                Assert.That(Apply(timer,5,0,2,100),Is.True);
                Assert.That(Apply(timer,5,0,2,100),Is.True);
                Assert.That(Apply(timer,4,0,2,160),Is.False);
                Assert.That(Apply(timer,5,0,2,160),Is.False);
                Assert.That(Apply(timer,5,0,3,100),Is.False);
                Assert.That(Get(timer,"StartedAt"),Is.EqualTo(100d));
                Assert.That(Get(timer,"HasStarted"),Is.True);
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
        public void InvalidClockPacketsNeverActivateEncounter()
        {
            var go=new GameObject("Offline timer validation fixture");go.SetActive(false);
            try
            {
                var timer=go.AddComponent(Timer);
                Assert.That(Apply(timer,1,-1,0,100),Is.False);
                Assert.That(Apply(timer,1,0,-1,100),Is.False);
                foreach(double epoch in new[]{-2d,double.NaN,double.PositiveInfinity})Assert.That(Apply(timer,1,0,0,epoch),Is.False);
                Assert.That(Get(timer,"HasStarted"),Is.False);
            }
            finally {Object.DestroyImmediate(go);}
        }

        [Test]
        public void CopiedSceneWaitsForEntranceAcknowledgmentsBeforeStartingEncounterClock()
        {
            const BindingFlags statics=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            var development=T("CurtainCall.EscapeRoute.EscapeDevelopmentSession");
            var entrance=development.GetField("entranceSession",statics);
            var sessionType=T("CurtainCall.Network.Session.NetworkSessionManager");
            var instance=sessionType.GetProperty("Instance",statics);
            var curtain=T("CurtainCall.UI.SceneIntegration.SceneCurtainTransition").GetField("instance",statics);
            var playerType=T("CurtainCall.Network.PlayerSync.NetworkPlayer");
            var players=(System.Collections.IList)playerType.GetField("all",statics).GetValue(null);
            var previousPlayers=players.Cast<object>().ToArray();
            var previousEntrance=entrance.GetValue(null);
            var previousSession=instance.GetValue(null);
            var previousCurtain=curtain.GetValue(null);
            var root=new GameObject("Offline copied-scene entrance fixture");root.SetActive(false);
            var actor=new GameObject("Already placed entrance participant");actor.SetActive(false);
            var volume=new GameObject("Active copied content and start area");
            Component player=null;
            try
            {
                // Keep fixture behaviours dormant: only the timer's Update is invoked, with no network session started.
                instance.SetValue(null,null);curtain.SetValue(null,null);players.Clear();
                root.transform.position=actor.transform.position=volume.transform.position=new Vector3(9300,40,9300);
                var content=root.AddComponent(T("CurtainCall.EscapeRoute.EscapeCampaignContent"));
                Set(content,"content",volume);Set(content,"IsCampaign",false);
                var session=root.AddComponent(sessionType);
                Set(session,"ConnectionState",Enum.Parse(T("CurtainCall.Network.Session.SessionConnectionState"),"Host"));
                Set(session,"GameState",Enum.Parse(T("CurtainCall.Network.Session.GameSessionState"),"Playing"));
                var waiting=root.AddComponent(development);entrance.SetValue(null,waiting);
                var timer=root.AddComponent(Timer);Set(timer,"content",content);Set(timer,"session",session);
                var area=volume.AddComponent<BoxCollider>();area.isTrigger=true;area.size=Vector3.one*4;
                Set(timer,"startArea",area);
                player=actor.AddComponent(playerType);Set(player,"IsSpawned",true);Set(player,"OwnerClientId",69UL);
                Set(Get(player,"slot"),"m_InternalValue",0);
                Set(Get(player,"state"),"m_InternalValue",Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"),"Normal"));
                players.Add(player);
                Assert.That(Apply(timer,1,0,0,-1),Is.True);
                Assert.That(Get(content,"IsActive"),Is.True);
                Assert.That(Get(content,"IsCampaign"),Is.False);
                Assert.That(Call(timer,"CanStart",player),Is.True,"The placed entrant already meets the ordinary start-area rules.");

                Set(waiting,"Ready",false);
                Assert.That(Get(timer,"RouteReady"),Is.False,"Playing alone must not release the copied scene's entrance barrier.");
                Call(timer,"Update");
                Assert.That(Get(timer,"StartedAt"),Is.EqualTo(-1d));
                Assert.That(Get(timer,"HasStarted"),Is.False,"Loading must not consume the pursuer's opening.");

                Set(waiting,"Ready",true);
                Assert.That(Get(timer,"RouteReady"),Is.True,"The same scene is eligible once all entrance acknowledgments arrive.");
                Call(timer,"Update");
                Assert.That(Get(timer,"StartedAt"),Is.GreaterThanOrEqualTo(0d));
                Assert.That(Get(timer,"HasStarted"),Is.True,"The ordinary entrant starts the clock after the barrier releases.");
            }
            finally
            {
                try
                {
                    if(player!=null)Set(player,"IsSpawned",false);
                    Object.DestroyImmediate(actor);Object.DestroyImmediate(root);Object.DestroyImmediate(volume);
                }
                finally
                {
                    players.Clear();foreach(var previous in previousPlayers)players.Add(previous);
                    entrance.SetValue(null,previousEntrance);instance.SetValue(null,previousSession);curtain.SetValue(null,previousCurtain);
                }
            }
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
                Set(Get(player,"slot"),"m_InternalValue",0);
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
        public void OldSceneCountdownIsHiddenWhenEncounterClockActivates()
        {
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/Retained/RestTimerV28/W1_RestTimer_v2_8.prefab");
                var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview);
                var view=root.GetComponent(T("CurtainCall.UI.RestTimerV28.RestTimerView"));
                var bridge=root.AddComponent(T("CurtainCall.UI.SceneIntegration.RestTimerView"));
                var snapshotType=T("CurtainCall.UI.RestTimerV28.RestTimerSnapshot");
                object Snapshot(double remaining)=>Activator.CreateInstance(snapshotType,new[]{(object)true,1,
                    Enum.Parse(T("CurtainCall.UI.RestTimerV28.RestTimeOfDay"),"Unknown"),Enum.Parse(T("CurtainCall.UI.RestTimerV28.RestTimerMode"),"Limited"),remaining,480d,false});
                Call(view,"SetSnapshot",Snapshot(480),false);
                Assert.That(Get(view,"CountdownText"),Is.EqualTo("8:00"));
                var clockObject=new GameObject("Legacy scene clock");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clockObject,preview);
                clockObject.SetActive(false);
                var timer=clockObject.AddComponent(Timer);Set(timer,"display",bridge);
                Call(timer,"OnEnable");
                Assert.That(Get(view,"RequestedVisible"),Is.False);
                Assert.That(Apply(timer,1,0,0,100),Is.True);
                Assert.That(Get(view,"RequestedVisible"),Is.False);
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
