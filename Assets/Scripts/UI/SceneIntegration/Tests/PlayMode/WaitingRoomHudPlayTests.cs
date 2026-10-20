using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class WaitingRoomHudPlayTests
    {
        const string Room = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const string Common = "Assets/Scenes/UI/CommonWaitingRoomUI.unity";
        Component campaign, ui, actor, other;
        Behaviour director;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name) is PropertyInfo property ? property.GetValue(target) : target.GetType().GetField(name).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods().Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        static Component Find(string type) => Object.FindFirstObjectByType(T(type)) as Component;
        static Component View(object target, string name) => (Component)Get(target, name);
        static string Text(Component target) => string.Join(" ", target.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text));
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        Component Actor(string name, Vector3 position)
        {
            var root = new GameObject(name); Object.DontDestroyOnLoad(root);
            var mover = root.AddComponent(T("CurtainCall.Player.PlayerMover"));
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            Call(mover, "SetStartPose", position, Quaternion.identity);
            return mover;
        }
        IEnumerator WaitFor(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(predicate(), "HUD scene did not reach the expected state within 30 seconds.");
        }
        [UnitySetUp]
        public IEnumerator Open()
        {
            actor = Actor("LocalHudTest", new Vector3(-2, .1f, 67));
            actor.gameObject.AddComponent(T("CurtainCall.Player.PlayerInputReader"));
            other = Actor("OtherHudTest", new Vector3(2, .1f, 67));
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Room, new LoadSceneParameters(LoadSceneMode.Single));
            director = Find("CurtainCall.SpaceLink.SpaceLinkDirector") as Behaviour;
            if (director != null) director.enabled = false;
            campaign = Find("CurtainCall.Campaign.CampaignRun");
            yield return null;
            Assert.IsTrue((bool)Call(campaign, "StartOfflineRun"));
            Assert.IsTrue((bool)Call(campaign, "EndIntro"));
            Assert.IsTrue((bool)Call(campaign, "EndPerformance"));
            yield return WaitFor(() => Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI") != null);
            ui = Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealRoom_ClockFundsInventoryAndGatherReadGameState()
        {
            var hud = Get(ui, "Hud");
            var clock = View(hud, "Clock");
            var inventory = View(hud, "Inventory");
            Assert.IsTrue(clock.gameObject.activeSelf);
            Assert.AreEqual(true, Get(clock, "HasValidData"));
            Assert.AreEqual(true, Get(View(hud, "Funds"), "HasValidData"));
            var rewards = Find("CurtainCall.Reward.PerformanceRewards");
            StringAssert.Contains(((long)Get(rewards, "Balance")).ToString("N0", System.Globalization.CultureInfo.InvariantCulture), Text(View(hud, "Funds")));
            if (Get(Get(campaign, "State"), "Timer").ToString() == "WaitingForExit")
            {
                StringAssert.Contains("멈춤", Text(clock));
                Assert.IsTrue((bool)Call(campaign, "NotifyLeftWaitingRoom"));
                yield return null;
                StringAssert.Contains("탐색", Text(clock));
            }
            Assert.IsNull(GameObject.Find("ExplorationTimerCanvas"));
            Assert.IsNull(Find("CurtainCall.WaitingRoom.SharedFundsView"));
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            Assert.AreEqual(false, Get(carry, "ShowLegacyHud"));
            Assert.AreEqual(0, inventory.GetComponent<CanvasGroup>().alpha);
            ulong me = (ulong)Get(carry, "LocalClientId");
            var itemType = T("CurtainCall.WaitingRoom.PropItem");
            object Item(int uid, string id) => Activator.CreateInstance(itemType, uid, id, id);
            Assert.AreEqual("None", Call(carry, "Receive", me, Item(7001, "tool-a"), false).ToString());
            yield return null;
            Assert.AreEqual(Call(carry, "Slots", me), Get(inventory, "SlotCount"));
            Assert.AreEqual(1, inventory.GetComponent<CanvasGroup>().alpha);
            var slot = (Component)Call(inventory, "GetSlot", 0);
            Assert.IsTrue(slot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Any(i => i.enabled && i.sprite != null));
            Assert.AreEqual("None", Call(carry, "Receive", me, Item(7002, "backpack"), true).ToString());
            yield return null;
            Assert.AreEqual(Call(carry, "Slots", me), Get(inventory, "SlotCount"));
            Set(ui, "inventoryUntil", Time.unscaledTime - 1);
            yield return null;
            Assert.AreEqual(0, inventory.GetComponent<CanvasGroup>().alpha);
            Call(carry, "RequestDrop"); yield return null;
            Assert.AreEqual(1, inventory.GetComponent<CanvasGroup>().alpha);

            var gather = View(hud, "Gather");
            Assert.AreEqual(0, gather.GetComponent<CanvasGroup>().alpha);
            var zone = Find("CurtainCall.WaitingRoom.StageEntranceGather");
            Call(actor, "SetStartPose", zone.transform.position - Vector3.up, Quaternion.identity);
            Physics.SyncTransforms(); yield return null; yield return null;
            Assert.AreEqual(1, gather.GetComponent<CanvasGroup>().alpha);
            StringAssert.Contains("1 / 2", Text(gather));
            Assert.IsTrue((bool)Call(campaign, "NotifyAllAtStageEntrance"));
            Assert.IsTrue((bool)Call(campaign, "EndPerformance"));
            yield return null;
            Assert.IsFalse(clock.gameObject.activeSelf, "Night must not show a countdown.");
            Assert.IsTrue(View(hud, "Funds").gameObject.activeSelf);
            Assert.AreEqual(0, gather.GetComponent<CanvasGroup>().alpha);
        }

        [UnityTest]
        public IEnumerator AnnouncementsAndRepeatedSpaceRegistration_DoNotDuplicateHud()
        {
            var announcer = Find("CurtainCall.WaitingRoom.ExplorationTimerAnnouncer");
            announcer.GetType().GetMethod("Raise", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(announcer, new object[] {30, false});
            yield return new WaitForSecondsRealtime(.3f);
            var caption = Find("CurtainCall.UI.CaptionsV1.CaptionView");
            Assert.AreEqual(true, Get(caption, "IsVisible"));
            StringAssert.Contains("공연 30초 전입니다", Text(caption));
            announcer.GetType().GetMethod("Raise", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(announcer, new object[] {60, true});
            yield return new WaitForSecondsRealtime(.3f);
            StringAssert.Contains("소집 경보", Text(caption));
            Assert.IsNotNull(announcer.GetComponent<AudioSource>(), "The existing recall bell must survive legacy UI cleanup.");
            var binding = Find("CurtainCall.UI.SceneIntegration.WaitingRoomUiBinding") as Behaviour;
            binding.enabled = false;
            yield return WaitFor(() => !SceneManager.GetSceneByPath(Common).isLoaded);
            binding.enabled = true;
            yield return WaitFor(() => SceneManager.GetSceneByPath(Common).isLoaded && Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI") != null && Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI") != ui);
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType(T("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI"), FindObjectsSortMode.None).Length);
            Assert.AreEqual(true, Get(View(Get(Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI"), "Hud"), "Clock"), "HasValidData"));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (campaign != null) Call(campaign, "StopOfflineRun");
            foreach (string type in new[] { "CurtainCall.WaitingRoom.ScheduleBoard", "CurtainCall.WaitingRoom.WaitingRoomShop", "CurtainCall.WaitingRoom.PropCarry",
                "CurtainCall.Campaign.CampaignRun", "CurtainCall.Reward.PerformanceRewards", "CurtainCall.SpaceLink.SpaceLinkDirector", "CurtainCall.Network.Session.NetworkSessionManager" })
            {
                var target = Find(type);
                if (target != null) Object.Destroy(target.gameObject);
            }
            if (actor != null) Object.Destroy(actor.gameObject);
            if (other != null) Object.Destroy(other.gameObject);
            yield return null;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Start_Lobby/Start.unity", new LoadSceneParameters(LoadSceneMode.Single));
        }
    }
}
