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
                Assert.AreEqual(2, Get(inventory, "SlotCount"), "소지 UI는 기획에 맞게 1–2번만 표시한다.");
                var inventoryRect = (RectTransform)inventory.transform;
                Assert.AreEqual(new Vector2(1, 0), inventoryRect.anchorMin);
                Assert.AreEqual(new Vector2(1, 0), inventoryRect.pivot);
                var carryState = Find("CurtainCall.WaitingRoom.PropCarry");
                ulong localId = (ulong)Get(carryState, "LocalClientId");
                Assert.AreEqual(heldCount, ((IEnumerable)Call(carryState, "Held", localId)).Cast<object>().Count());
                int capacity = (int)Call(carryState, "Slots", localId);
                for (int i = 0; i < 2; i++)
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
                Assert.AreEqual(selectedName == null ? "빈손 · 1–2 기물 선택" : $"{selectedName} · 손에 듦", ((TMP_Text)Get(inventoryUi, "selectedLabel")).text);
            }
            // v2.8: 실제 남은 시간과 D-N/시간대만 표시하고 기존 W1은 숨긴다.
            Assert.IsFalse(clock.gameObject.activeSelf, "옛 W1 시계는 숨긴다");
            var dayClock = Get(ui, "RestTimerDisplay");
            Assert.IsNotNull(dayClock);
            Assert.AreEqual(true, Get(dayClock, "IsShown"));
            Assert.AreEqual("D-3", (string)Get(dayClock, "DayText"));
            StringAssert.Contains(":", (string)Get(dayClock, "RemainingText"));
            Assert.AreEqual("Limited", Get(Get(dayClock, "Snapshot"), "mode").ToString());
            Assert.IsNotNull(((Component)dayClock).GetComponent(T("CurtainCall.UI.RestTimerV28.RestTimerView")), "제공 패키지 프리팹을 사용한다");
            Assert.IsFalse(((Component)Get(ui, "DayClockDisplay")).gameObject.activeSelf);
            Assert.AreSame(clock.transform.parent, ((Component)dayClock).transform.parent, "같은 HUD 자리에 붙인다");
            Assert.AreEqual(true, Get(View(hud, "Funds"), "HasValidData"));
            var rewards = Find("CurtainCall.Reward.PerformanceRewards");
            StringAssert.Contains(((long)Get(rewards, "Balance")).ToString("N0", System.Globalization.CultureInfo.InvariantCulture), Text(View(hud, "Funds")));
            Assert.AreEqual("Running", Get(Get(campaign, "State"), "Timer").ToString(), "M2: 쉬는 시간 시작부터 흐른다");
            Assert.IsNull(GameObject.Find("ExplorationTimerCanvas"));
            Assert.IsNull(Find("CurtainCall.WaitingRoom.SharedFundsView"));
            yield return new WaitForSecondsRealtime(.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/resttimer-day.png"));
            yield return null;
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
            var nightClock = Get(ui, "RestTimerDisplay");
            Assert.AreEqual(true, Get(nightClock, "IsShown"), "밤 정비에도 타이머를 보인다");
            Assert.AreEqual("", Get(nightClock, "RemainingText"), "무제한은 문자열 대신 중앙 무한대 아이콘이다");
            Assert.AreEqual(true, Get(Get(nightClock, "View"), "InfinityVisible"));
            Assert.AreEqual("Unlimited", Get(Get(nightClock, "Snapshot"), "mode").ToString());
            Assert.AreEqual("Night", Get(Get(nightClock, "Snapshot"), "timeOfDay").ToString());
            var nightBanner = (RectTransform)View(Private(ui, "notifications"), "Banner").transform;
            var timerBounds = new Vector3[4]; var bannerBounds = new Vector3[4];
            ((RectTransform)((Component)nightClock).transform).GetWorldCorners(timerBounds);
            nightBanner.GetWorldCorners(bannerBounds);
            Assert.IsTrue(bannerBounds[0].x > timerBounds[2].x || bannerBounds[2].x < timerBounds[0].x
                || bannerBounds[1].y < timerBounds[0].y || bannerBounds[0].y > timerBounds[2].y,
                "밤 정비 알림이 타이머를 가리지 않는다.");
            yield return new WaitForSecondsRealtime(.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/resttimer-night.png"));
            yield return null;
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
            Assert.AreEqual(true, Get(Get(registeredUi, "RestTimerDisplay"), "IsShown"), "다시 붙은 HUD도 타이머를 보인다");
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
