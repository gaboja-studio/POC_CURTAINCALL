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
        static object Private(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
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
            var legacyInventory = View(hud, "Inventory");
            yield return WaitFor(() => Find("CurtainCall.UI.SceneIntegration.CommonInventoryUI") != null);
            var inventoryUi = Find("CurtainCall.UI.SceneIntegration.CommonInventoryUI");
            var inventory = View(inventoryUi, "slots");
            void AssertInventory(int heldCount, string selectedName)
            {
                Assert.AreEqual(0, legacyInventory.GetComponent<CanvasGroup>().alpha, "구형 HUD와 실제 소지 UI가 중복 표시되면 안 된다.");
                Assert.AreEqual(1, ((CanvasGroup)Get(inventoryUi, "group")).alpha);
                Assert.AreEqual(1, inventory.GetComponent<CanvasGroup>().alpha);
                Assert.AreEqual(5, Get(inventory, "SlotCount"), "현재 소지 UI는 잠긴 칸을 포함해 1–5번을 표시한다.");
                var carryState = Find("CurtainCall.WaitingRoom.PropCarry");
                ulong localId = (ulong)Get(carryState, "LocalClientId");
                Assert.AreEqual(heldCount, ((IEnumerable)Call(carryState, "Held", localId)).Cast<object>().Count());
                int capacity = (int)Call(carryState, "Slots", localId);
                for (int i = 0; i < 5; i++)
                {
                    var slot = (Component)Call(inventory, "GetSlot", i);
                    Assert.AreEqual(i < capacity ? 1f : .35f, slot.GetComponent<CanvasGroup>().alpha);
                    Assert.AreEqual(i < capacity ? (i + 1).ToString() : $"{i + 1} 잠금", ((TMP_Text)Private(slot, "keyText")).text);
                    var icon = (UnityEngine.UI.Image)Private(slot, "itemIcon");
                    Assert.AreEqual(i < heldCount, icon.enabled);
                    if (i < heldCount) Assert.IsNotNull(icon.sprite);
                    Assert.AreEqual(i < heldCount ? "1" : "", ((TMP_Text)Private(slot, "quantityText")).text);
                    Assert.AreEqual(i < heldCount && i == heldCount - 1, ((UnityEngine.UI.Image)Private(slot, "selectedAccent")).enabled);
                }
                Assert.AreEqual(selectedName == null ? "빈손 · 1–5 기물 선택" : $"{selectedName} · 손에 듦", ((TMP_Text)Get(inventoryUi, "selectedLabel")).text);
            }
            // M2: 옛 남은 시간 시계 대신 같은 자리에 원형 하루 시계를 보인다.
            Assert.IsFalse(clock.gameObject.activeSelf, "옛 W1 시계는 숨긴다");
            var dayClock = Get(ui, "DayClockDisplay");
            Assert.IsNotNull(dayClock);
            Assert.AreEqual(true, Get(dayClock, "IsShown"));
            StringAssert.Contains("쉬는 시간", (string)Get(dayClock, "DayText"));
            StringAssert.StartsWith("남은 시간", (string)Get(dayClock, "RemainingText"));
            Assert.Greater((float)Get(dayClock, "TimerFill"), 0f);
            Assert.AreSame(clock.transform.parent, ((Component)dayClock).transform.parent, "같은 HUD 자리에 붙인다");
            Assert.AreEqual(true, Get(View(hud, "Funds"), "HasValidData"));
            var rewards = Find("CurtainCall.Reward.PerformanceRewards");
            StringAssert.Contains(((long)Get(rewards, "Balance")).ToString("N0", System.Globalization.CultureInfo.InvariantCulture), Text(View(hud, "Funds")));
            Assert.AreEqual("Running", Get(Get(campaign, "State"), "Timer").ToString(), "M2: 쉬는 시간 시작부터 흐른다");
            Assert.IsNull(GameObject.Find("ExplorationTimerCanvas"));
            Assert.IsNull(Find("CurtainCall.WaitingRoom.SharedFundsView"));
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            Assert.AreEqual(false, Get(carry, "ShowLegacyHud"));
            AssertInventory(0, null);
            ulong me = (ulong)Get(carry, "LocalClientId");
            var itemType = T("CurtainCall.WaitingRoom.PropItem");
            object Item(int uid, string id) => Activator.CreateInstance(itemType, uid, id, id);
            Assert.AreEqual("None", Call(carry, "Receive", me, Item(7001, "tool-a")).ToString());
            yield return null;
            AssertInventory(1, "tool-a");
            Assert.AreEqual("None", Call(carry, "Receive", me, Item(7002, "tool-b")).ToString());
            yield return null;
            AssertInventory(2, "tool-b");
            // 실제 소지 UI는 구형 HUD의 4초 자동 숨김 대신 계속 표시된다.
            yield return new WaitForSecondsRealtime(4.1f);
            AssertInventory(2, "tool-b");
            Call(carry, "RequestDrop"); yield return null;
            AssertInventory(1, "tool-a");

            var gather = View(hud, "Gather");
            Assert.AreEqual(0, gather.GetComponent<CanvasGroup>().alpha);
            var zone = Find("CurtainCall.WaitingRoom.StageEntranceGather");
            var bounds = zone.GetComponent<BoxCollider>().bounds;
            Call(actor, "SetStartPose", bounds.center - Vector3.up * .5f, Quaternion.identity);
            Physics.SyncTransforms();
            yield return WaitFor(() => gather.GetComponent<CanvasGroup>().alpha == 1 && Text(gather).Contains("1 / 2"));
            Assert.AreEqual(1, gather.GetComponent<CanvasGroup>().alpha);
            StringAssert.Contains("1 / 2", Text(gather));
            Assert.IsTrue((bool)Call(campaign, "NotifyAllAtStageEntrance"));
            Assert.IsTrue((bool)Call(campaign, "EndPerformance"));
            yield return null;
            Assert.IsFalse(clock.gameObject.activeSelf, "Night must not show a countdown.");
            var nightClock = Get(ui, "DayClockDisplay");
            Assert.AreEqual(true, Get(nightClock, "IsShown"), "밤 정비에도 하루 시각은 보인다");
            Assert.AreEqual("", Get(nightClock, "RemainingText"), "밤 정비에는 타이머가 없다");
            StringAssert.Contains("밤 정비", (string)Get(nightClock, "DayText"));
            Assert.AreEqual("4:00", Get(nightClock, "TimeText"));
            Assert.IsTrue(View(hud, "Funds").gameObject.activeSelf);
            Assert.AreEqual(0, gather.GetComponent<CanvasGroup>().alpha);
        }

        [UnityTest]
        public IEnumerator AnnouncementsAndRepeatedSpaceRegistration_DoNotDuplicateHud()
        {
            var announcer = Find("CurtainCall.WaitingRoom.ExplorationTimerAnnouncer");
            announcer.GetType().GetMethod("Raise", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(announcer, new object[] {30, false});
            yield return new WaitForSecondsRealtime(.3f);
            // 탐색 방송/소집 경보는 현재 캡션(C7)이 아니라 공통 알림 배너(C8)로 전달된다.
            var banner = View(Private(ui, "notifications"), "Banner");
            Assert.AreEqual(true, Get(banner, "IsVisible"));
            StringAssert.Contains("방송", Text(banner));
            StringAssert.Contains("공연 30초 전입니다", Text(banner));
            announcer.GetType().GetMethod("Raise", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(announcer, new object[] {60, true});
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(true, Get(banner, "IsVisible"));
            StringAssert.Contains("소집 경보", Text(banner));
            StringAssert.Contains("무대 입구로 모이세요", Text(banner));
            Assert.IsNotNull(announcer.GetComponent<AudioSource>(), "The existing recall bell must survive legacy UI cleanup.");
            var binding = Find("CurtainCall.UI.SceneIntegration.WaitingRoomUiBinding") as Behaviour;
            binding.enabled = false;
            yield return WaitFor(() => !SceneManager.GetSceneByPath(Common).isLoaded);
            binding.enabled = true;
            yield return WaitFor(() => SceneManager.GetSceneByPath(Common).isLoaded && Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI") != null && Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI") != ui);
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType(T("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI"), FindObjectsSortMode.None).Length);
            var registeredUi = Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI");
            Assert.AreEqual(true, Get(Get(registeredUi, "DayClockDisplay"), "IsShown"), "다시 붙은 HUD도 하루 시계를 보인다");
            Assert.AreEqual(1, Object.FindObjectsByType(T("CurtainCall.UI.SceneIntegration.CommonInventoryUI"), FindObjectsSortMode.None).Length);
            announcer.GetType().GetMethod("Raise", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(announcer, new object[] {30, false});
            yield return new WaitForSecondsRealtime(.3f);
            var registeredBanner = View(Private(registeredUi, "notifications"), "Banner");
            Assert.AreEqual(true, Get(registeredBanner, "IsVisible"));
            StringAssert.Contains("공연 30초 전입니다", Text(registeredBanner));
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
