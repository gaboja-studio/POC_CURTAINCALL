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

        [UnityTest]
        public IEnumerator W2_PurchaseDeliveryLedger_RefusalAndCloseReopen()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
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
        public IEnumerator W2_MedicalReservation_UsesActualLimbsCostAndRoomState()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            SeedShopFunds(1000);
            Call(Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"), "ServerSetOneArm");
            yield return OpenShop();
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            var view = Get(adapter, "View");
            var service = Find("CurtainCall.WaitingRoom.ExpertService");
            long cost = (long)Get(service, "LocalMedicalCost");
            Assert.Greater(cost, 0);
            Assert.AreEqual(cost.ToString(), Get(Entry(adapter, "medical"), "PriceText"));
            Assert.AreEqual(false, Get(Entry(adapter, "facility"), "Available"));
            Call(view, "SelectTab", Enum.Parse(T("CurtainCall.UI.ShopV1.ShopTab"), "Services"));
            Call(view, "SelectEntry", "medical");
            Call(Get(Private(view, "primaryButton"), "onClick"), "Invoke");
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual("Booked", Get(service, "Phase").ToString());
            Assert.AreEqual(1000 - cost, Get(service, "Balance"));
            Assert.AreEqual(false, Get(Entry(adapter, "medical"), "Available"));
            Assert.IsNull(Get(view, "PendingRequest"));
            Assert.IsTrue(Entries(adapter).Any(e => ((string)Get(e, "Id")).StartsWith("fund:")));
        }

        [UnityTest]
        public IEnumerator W2_FacilityAdjustment_DisablesRepeatAndAbsentMedical()
        {
            yield return Launch(2, "Night", 0, "", "WaitingRoomBase");
            SeedShopFunds(1000);
            yield return OpenShop();
            var adapter = Find("CurtainCall.UI.SceneIntegration.WaitingRoomShopUI");
            var view = Get(adapter, "View");
            var service = Find("CurtainCall.WaitingRoom.ExpertService");
            Assert.AreEqual(false, Get(Entry(adapter, "medical"), "Available"));
            Call(view, "SelectTab", Enum.Parse(T("CurtainCall.UI.ShopV1.ShopTab"), "Services"));
            Call(view, "SelectEntry", "facility");
            Call(Get(Private(view, "secondaryButton"), "onClick"), "Invoke");
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual("Weaken", Get(service, "Obstacle").ToString());
            Assert.AreEqual(3, Get(service, "ObstacleDay"));
            Assert.AreEqual(1000 - (long)Get(service, "FacilityPrice"), Get(service, "Balance"));
            Assert.AreEqual(false, Get(Entry(adapter, "facility"), "Available"));
            Assert.IsNull(Get(view, "PendingRequest"));
            Call(view, "RequestAction", "strengthen");
            Assert.AreEqual("Weaken", Get(service, "Obstacle").ToString());
            Assert.AreEqual(1000 - (long)Get(service, "FacilityPrice"), Get(service, "Balance"));
        }
    }
}
