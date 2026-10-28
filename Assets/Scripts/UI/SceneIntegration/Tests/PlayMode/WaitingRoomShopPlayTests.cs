using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        static object Private(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static object Member(object target, string name) => target.GetType().GetField(name)?.GetValue(target) ?? Get(target, name);
        static object[] Entries(object adapter) => ((IEnumerable)Get(Call(adapter, "Build"), "Entries")).Cast<object>().ToArray();
        static object Entry(object adapter, string id) => Entries(adapter).Single(e => (string)Get(e, "Id") == id);
        static void SeedShopFunds(long amount)
        {
            // Fixture funds only; all debits, orders, service reservations and responses use production paths.
            var rewards = Find("CurtainCall.Reward.PerformanceRewards");
            var model = Private(rewards, "model");
            var ledger = (IList)Private(model, "ledger");
            long delta = amount - (long)Get(model, "Balance");
            ledger.Add(Activator.CreateInstance(T("CurtainCall.Reward.FundEntry"), ledger.Count + 1, Get(model, "Generation"),
                Field(Static("CurtainCall.Campaign.CampaignRun", "Instance").GetType().GetProperty("State").GetValue(Static("CurtainCall.Campaign.CampaignRun", "Instance")), "Day"),
                Enum.Parse(T("CurtainCall.Reward.FundEntryKind"), delta >= 0 ? "PerformanceIncome" : "Expense"), delta, amount, "테스트 자금", ""));
            model.GetType().GetProperty("Balance").SetValue(model, amount);
            rewards.GetType().GetMethod("Publish", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rewards, null);
        }
        IEnumerator OpenShop()
        {
            var phone = Find("CurtainCall.WaitingRoom.ShopPhone");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            Vector3 point = (Vector3)Get(phone, "PresentationPoint");
            Call(player.GetComponent(T("CurtainCall.Player.PlayerMover")), "SetStartPose", new Vector3(point.x + 1, .1f, point.z), Quaternion.identity);
            Call(phone, "Interact");
            yield return new WaitForSecondsRealtime(.35f);
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            Assert.IsNotNull(adapter);
            Assert.AreEqual(true, Get(Get(adapter, "View"), "IsVisible"));
            Assert.AreEqual(false, Private(phone, "showLegacyUi"));
            Assert.IsNull(Find("CurtainCall.WaitingRoom.ExpertServicePhonePanel"));
            Assert.AreEqual(1, Object.FindObjectsByType(T("UnityEngine.EventSystems.EventSystem"), FindObjectsSortMode.None).Length);
        }

        [TestCase(0, 0)]
        [TestCase(4, 4)]
        [TestCase(-1, 0)]
        public void W2_CarrySettings_UseConfiguredLimitAndPreserveLegacyField(int value, int expected)
        {
            var type = T("CurtainCall.Settings.BaseGameSettings+ItemCarrySettings");
            var settings = Activator.CreateInstance(type);
            Assert.AreEqual(2, Get(settings, "BaseSlots"));
            var field = type.GetField("baseSlots", BindingFlags.Instance | BindingFlags.NonPublic);
            var legacy = field.GetCustomAttributes(false).OfType<UnityEngine.Serialization.FormerlySerializedAsAttribute>().Single();
            Assert.AreEqual("twoArmSlots", legacy.oldName);
            field.SetValue(settings, value);
            Assert.AreEqual(expected, Get(settings, "BaseSlots"));
        }

        [UnityTest]
        public IEnumerator W2_CarrySlots_KeepTwoAfterInjuryAndRejectFullDelivery()
        {
            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            SeedShopFunds(100);
            var shop = Find("CurtainCall.WaitingRoom.WaitingRoomShop");
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            var player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            ulong me = (ulong)Get(player, "OwnerClientId");
            var catalog = ((IEnumerable)Get(shop, "Catalog")).Cast<object>().ToArray();
            Assert.IsFalse(catalog.Any(i => (string)Member(i, "Id") == "backpack"), "기존 씬의 배낭 품목도 제외한다");
            Assert.AreEqual(2, Call(carry, "Slots", me));
            for (int i = 0; i < 2; i++)
            {
                var prop = Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"), 9001 + i, "tool-a", "테스트 기물");
                Assert.AreEqual("None", Call(carry, "Receive", me, prop).ToString());
            }
            Call(player, "ServerSetOneArm");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(2, Call(carry, "Slots", me));
            var inventory = Find("CurtainCall.UI.SceneIntegration.CommonInventoryUI");
            Assert.IsNotNull(inventory);
            var slots = Member(inventory, "slots");
            Assert.AreEqual(1f, ((Component)Call(slots, "GetSlot", 0)).GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(1f, ((Component)Call(slots, "GetSlot", 1)).GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(.35f, ((Component)Call(slots, "GetSlot", 2)).GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(2, ((IEnumerable)Call(carry, "Held", me)).Cast<object>().Count());
            Assert.AreEqual(0, ((IEnumerable)Get(carry, "Floor")).Cast<object>().Count());
            Call(shop, "RequestPurchase", (string)Member(catalog[0], "Id"));
            yield return new WaitForSecondsRealtime(.3f);
            var delivery = ((IEnumerable)Get(shop, "Waiting")).Cast<object>().Single();
            long balance = (long)Get(shop, "Balance");
            Call(shop, "RequestClaim", (int)Member(delivery, "Order"));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(1, ((IEnumerable)Get(shop, "Waiting")).Cast<object>().Count());
            Assert.AreEqual(0, ((IEnumerable)Get(shop, "Receipts")).Cast<object>().Count());
            Assert.AreEqual(balance, Get(shop, "Balance"));
            Assert.AreEqual(2, ((IEnumerable)Call(carry, "Held", me)).Cast<object>().Count());
        }

        [UnityTest]
        public IEnumerator W2_MorningPurchase_RejectsUiAndDirectRequestWithoutSpending()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            SeedShopFunds(100);
            yield return OpenShop();
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            var shop = Find("CurtainCall.WaitingRoom.WaitingRoomShop");
            var item = ((IEnumerable)Get(shop, "Catalog")).Cast<object>().First();
            string id = (string)Member(item, "Id");
            Assert.AreEqual(false, Get(Entry(adapter, "item:" + id), "Available"));
            Assert.AreEqual("기물은 밤 정비에만 구매할 수 있습니다.", Get(Entry(adapter, "item:" + id), "UnavailableReason"));
            int ledgerCount = ((IEnumerable)Get(Call(adapter, "Build"), "Entries")).Cast<object>()
                .Count(e => ((string)Get(e, "Id")).StartsWith("fund:"));
            // UI를 거치지 않는 요청도 자금 차감·배송 생성 전에 거절한다.
            Call(shop, "RequestPurchase", id);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(100L, Get(shop, "Balance"));
            Assert.AreEqual(0, ((IEnumerable)Get(shop, "Waiting")).Cast<object>().Count());
            Assert.AreEqual(0, ((IEnumerable)Get(shop, "Receipts")).Cast<object>().Count());
            Assert.AreEqual("기물은 밤 정비에만 구매할 수 있습니다.", Get(shop, "LastMessage"));
            Assert.AreEqual(ledgerCount, Entries(adapter).Count(e => ((string)Get(e, "Id")).StartsWith("fund:")));
        }

        [UnityTest]
        public IEnumerator W2_PurchaseDeliveryLedger_RefusalAndCloseReopen()
        {
            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            SeedShopFunds(100);
            yield return OpenShop();
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            var view = Get(adapter, "View");
            var shop = Find("CurtainCall.WaitingRoom.WaitingRoomShop");
            var item = ((IEnumerable)Get(shop, "Catalog")).Cast<object>().First();
            string id = (string)Member(item, "Id");
            long price = Convert.ToInt64(Member(item, "Price"));
            Call(view, "SelectEntry", "item:" + id);
            // Invoke the prefab's wired button; a second request in the same frame must not double-charge.
            var button = Private(view, "primaryButton");
            Call(Get(button, "onClick"), "Invoke");
            Call(Get(button, "onClick"), "Invoke");
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(100 - price, Get(shop, "Balance"));
            Assert.AreEqual(1, ((IEnumerable)Get(shop, "Waiting")).Cast<object>().Count());
            Assert.IsNull(Get(view, "PendingRequest"));
            Assert.IsTrue(Entries(adapter).Any(e => ((string)Get(e, "Id")).StartsWith("fund:")));
            Assert.IsTrue(Entries(adapter).Any(e => ((string)Get(e, "Id")).StartsWith("delivery:")));
            var delivery = ((IEnumerable)Get(shop, "Waiting")).Cast<object>().Single();
            Call(shop, "RequestClaim", (int)Member(delivery, "Order"));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.IsTrue(Entries(adapter).Any(e => ((string)Get(e, "Id")).StartsWith("receipt:")));
            Assert.IsFalse(Entries(adapter).Any(e => ((string)Get(e, "Id")).StartsWith("delivery:")));
            // UI still shows sufficient funds, but host changes before the click: reject and unlock pending.
            SeedShopFunds(0);
            Call(view, "RequestAction", "buy");
            Assert.IsNull(Get(view, "PendingRequest"));
            Assert.IsFalse(string.IsNullOrEmpty((string)Private(view, "message")));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(false, Get(Entry(adapter, "item:" + id), "Available"));
            Assert.AreEqual(0L, Get(shop, "Balance"));
            Call(Get(Private(view, "closeButton"), "onClick"), "Invoke");
            yield return null;
            Assert.AreEqual(false, Get(Find("CurtainCall.WaitingRoom.ShopPhone"), "IsOpen"));
            Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));
            yield return OpenShop();
            Assert.AreEqual("", Private(view, "message"));
            // An old wire response must not alter a newly opened panel.
            adapter.GetType().GetMethod("Answer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, new object[] { "old-request", true, "stale" });
            Assert.AreEqual("", Private(view, "message"));
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var point = (Vector3)Get(Find("CurtainCall.WaitingRoom.ShopPhone"), "PresentationPoint");
            Call(player.GetComponent(T("CurtainCall.Player.PlayerMover")), "SetStartPose", new Vector3(point.x + 4, .1f, point.z), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(false, Get(view, "IsVisible"));
        }

        [UnityTest]
        public IEnumerator W2_BodyRestore_ChoosesPartsAndRaisesTeamPrice()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            SeedShopFunds(1000);
            Call(Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"), "ServerSetOneArm");
            yield return OpenShop();
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            var view = Get(adapter, "View");
            var service = Find("CurtainCall.WaitingRoom.ExpertService");
            // 외팔(잃은 팔다리 3)이면 1~3개 항목이 있고, 처음 1개당 가격은 기본가다. 시설 서비스는 없다.
            long unit = (long)Get(service, "MedicalPrice");
            Assert.AreEqual(100L, unit);
            for (int parts = 1; parts <= 3; parts++)
                Assert.AreEqual((unit * parts).ToString(), Get(Entry(adapter, "medical:" + parts), "PriceText"));
            Assert.IsFalse(Entries(adapter).Any(e => ((string)Get(e, "Id")) == "facility"));
            Call(view, "SelectTab", Enum.Parse(T("CurtainCall.UI.ShopV1.ShopTab"), "Services"));
            Call(view, "SelectEntry", "medical:2");
            Call(Get(Private(view, "primaryButton"), "onClick"), "Invoke");
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual("Booked", Get(service, "Phase").ToString());
            Assert.AreEqual(2, Get(service, "BookedLimbs"));
            Assert.AreEqual(1000 - unit * 2, Get(service, "Balance"));
            Assert.AreEqual(1, Get(service, "TeamUses"), "2개를 고쳐도 팀 이용은 1회");
            Assert.AreEqual(unit + 25, Get(service, "MedicalPrice"), "다음 1개당 가격 +25");
            Assert.AreEqual(unit * 2, Get(service, "BookedCost"));
            Assert.AreEqual(false, Get(Entry(adapter, "medical:1"), "Available"));
            Assert.IsNull(Get(view, "PendingRequest"));
            Assert.IsTrue(Entries(adapter).Any(e => ((string)Get(e, "Id")).StartsWith("fund:")));
        }

        [UnityTest]
        public IEnumerator W2_BodyRestore_IsAvailableDuringRestTimeWithoutExpertSchedule()
        {
            // M2: 전문가 일정 삭제. 2일차(옛 시설 담당자 날) 아침 쉬는 시간에도 신체 복구를 쓴다.
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            SeedShopFunds(1000);
            var local = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            Call(local, "ServerCut", 1);
            yield return OpenShop();
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            var view = Get(adapter, "View");
            var service = Find("CurtainCall.WaitingRoom.ExpertService");
            Assert.AreEqual(true, Get(Entry(adapter, "medical:1"), "Available"), (string)Get(Entry(adapter, "medical:1"), "UnavailableReason"));
            Assert.IsFalse(Entries(adapter).Any(e => ((string)Get(e, "Id")) == "medical:2"), "잃은 수(1)보다 많이 고를 수 없다");
            Call(view, "SelectTab", Enum.Parse(T("CurtainCall.UI.ShopV1.ShopTab"), "Services"));
            Call(view, "SelectEntry", "medical:1");
            Call(Get(Private(view, "primaryButton"), "onClick"), "Invoke");
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual("Booked", Get(service, "Phase").ToString());
            Assert.AreEqual(900L, Get(service, "Balance"));
        }    }
}
