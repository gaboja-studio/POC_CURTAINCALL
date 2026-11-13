using System.Collections;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator NavigationObjectEndpoints_OutlineRealPhoneAndPlayerKeepWaveAndRestorePositionMarker()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            var phone=Find("CurtainCall.WaitingRoom.ShopPhone");Assert.IsNotNull(phone);
            var mover=player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var from=phone.transform.position+new Vector3(-4,-1.4f,0);
            if(Physics.Raycast(from+Vector3.up*2,Vector3.down,out var floor,5,~0,QueryTriggerInteraction.Ignore))from=floor.point+Vector3.up*.05f;
            Call(mover,"SetStartPose",from,Quaternion.identity);
            var targetType=T("CurtainCall.UI.Wayfinding.NavigationTarget");
            var goal=targetType.GetMethod("Object",new[]{typeof(Transform)}).Invoke(null,new object[]{phone.transform});
            var start=targetType.GetMethod("Player").Invoke(null,new object[]{player});
            var handle=T("CurtainCall.UI.Wayfinding.WorldNavigation").GetMethod("Show").Invoke(null,new object[]{goal,start,false,1f,1f,(Color?)Color.cyan});
            try
            {
                float deadline=Time.realtimeSinceStartup+30;
                while(!(bool)Get(handle,"IsVisible")&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue((bool)Get(handle,"IsVisible"),"Object navigation must reach the real phone.");
                var service=Find("CurtainCall.UI.Wayfinding.NavigationService");
                var pulse=service.GetComponentsInChildren(T("CurtainCall.UI.Wayfinding.NavigationPulseSequence")).First(p=>(bool)Get(p,"DestinationBeaconHidden"));
                var outlines=(IEnumerable)T("CurtainCall.UI.SceneIntegration.SilhouetteOutline").GetField("active",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                Assert.IsTrue(outlines.Cast<object>().Any(o=>(Transform)Get(o,"Target")==phone.transform));
                Assert.IsTrue(outlines.Cast<object>().Any(o=>(Transform)Get(o,"Target")==player.transform));
                Call(pulse,"EvaluateAt",(float)Get(pulse,"TravelSeconds")+.15f);
                Assert.Greater((float)Get(Get(pulse,"ArrivalEcho"),"Opacity"),0);
                var camera=Camera.main;
                foreach(Behaviour brain in Object.FindObjectsByType(T("Unity.Cinemachine.CinemachineBrain"),FindObjectsSortMode.None))brain.enabled=false;
                camera.transform.position=phone.transform.position+new Vector3(-5,2.3f,3);
                camera.transform.LookAt(phone.transform.position+Vector3.down*.25f);
                string capture="Tasks/Active/Task-20261113-122-rehearsal-navigation-art/Captures/Outline/real-phone-navigation.png";
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(capture));
                T("CurtainCall.UI.Wayfinding.Editor.NavigationPrefabBuilder").GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{camera,capture});
                var position=targetType.GetMethod("Position").Invoke(null,new object[]{phone.transform.position});
                Call(handle,"Update",position,start,false,1f,1f,(Color?)Color.magenta);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.IsFalse((bool)Get(pulse,"DestinationBeaconHidden"));
                Call(handle,"Hide");yield return null;
                Assert.IsFalse((bool)Get(handle,"IsVisible"));
            }
            finally{((System.IDisposable)handle).Dispose();}
        }

        [UnityTest]
        public IEnumerator NavigationAfterRehearsal_UsesPulsePrefabAndHidesAtGather()
        {
            yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
            var rehearsal = Find("CurtainCall.WaitingRoom.WaitingRoomRehearsal");
            float clearDeadline = Time.realtimeSinceStartup + 60;
            while (!(bool)Static("CurtainCall.WaitingRoom.WaitingRoomRehearsal", "IsCompleted") && Time.realtimeSinceStartup < clearDeadline)
            {
                Call(rehearsal, "TryClearCurrentStage");
                yield return new WaitForSecondsRealtime(.3f);
            }
            Assert.IsTrue((bool)Static("CurtainCall.WaitingRoom.WaitingRoomRehearsal", "IsCompleted"));
            var guide = Find("CurtainCall.WaitingRoom.RehearsalPathGuide");
            float deadline = Time.realtimeSinceStartup + 30;
            while (!(bool)Get(guide, "IsVisible") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue((bool)Get(guide, "IsVisible"), "A complete path must be shown after rehearsal.");
            var path = (Vector3[])Get(guide, "Path");
            Assert.GreaterOrEqual(path.Length, 2);
            var service = Find("CurtainCall.UI.Wayfinding.NavigationService");
            Assert.AreEqual(0, service.GetComponentsInChildren<LineRenderer>(true).Length);
            Assert.Greater(service.GetComponentsInChildren<SpriteRenderer>().Length, 3);
            var reviewCamera = new GameObject("Navigation validation camera").AddComponent<Camera>();
            try
            {
                var heading = Vector3.ProjectOnPlane(path[1] - path[0], Vector3.up).normalized;
                reviewCamera.transform.position = path[0] - heading * 2 + Vector3.up * 3;
                reviewCamera.transform.LookAt(path[0] + heading * 12);
                reviewCamera.fieldOfView = 65; reviewCamera.farClipPlane = 160;
                foreach (var pulse in service.GetComponentsInChildren(T("CurtainCall.UI.Wayfinding.NavigationPulseSequence")))
                    Call(pulse, "EvaluateAt", .65f);
                string capture = "Tasks/Active/Task-20261113-122-rehearsal-navigation-art/Captures/V2/rehearsal-pulse-runtime.png";
                T("CurtainCall.UI.Wayfinding.Editor.NavigationPrefabBuilder").GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { reviewCamera, capture });
                var handle = guide.GetType().GetField("navigation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(guide);
                Call(handle, "SetAppearance", Color.magenta, 1.5f, .8f);
                yield return new WaitForSecondsRealtime(.2f);
                foreach (var pulse in service.GetComponentsInChildren(T("CurtainCall.UI.Wayfinding.NavigationPulseSequence")))
                    Call(pulse, "EvaluateAt", .65f);
                T("CurtainCall.UI.Wayfinding.Editor.NavigationPrefabBuilder").GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { reviewCamera, capture.Replace("runtime.png", "magenta.png") });
            }
            finally { Object.Destroy(reviewCamera.gameObject); }
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            Call(mover, "SetStartPose", path[path.Length - 1], Quaternion.identity);
            deadline = Time.realtimeSinceStartup + 5;
            while ((bool)Get(guide, "IsVisible") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse((bool)Get(guide, "IsVisible"), "Gathering must dispose the local guide.");
        }
    }
}
