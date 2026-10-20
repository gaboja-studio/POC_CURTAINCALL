using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class InteractionPresentationPlayTests
    {
        readonly List<GameObject> created = new();
        Component actor, ui;
        Camera camera;
        object keyboard;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        static Component Find(string type) => Object.FindFirstObjectByType(T(type)) as Component;
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods().Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        Transform[] Highlighted => ((IEnumerable)Get(ui, "Highlighted")).Cast<Transform>().ToArray();
        GameObject New(string name) { var go = new GameObject(name); created.Add(go); return go; }
        void Place(Vector3 position) => Call(actor, "SetStartPose", position, Quaternion.identity);
        IEnumerator Settle() { Physics.SyncTransforms(); yield return null; yield return null; }
        void Key(string name)
        {
            var input = T("UnityEngine.InputSystem.InputSystem");
            keyboard ??= input.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) }).Invoke(null, new object[] { "Keyboard", null, null });
            var keys = Array.CreateInstance(T("UnityEngine.InputSystem.Key"), name == null ? 0 : 1);
            if (name != null) keys.SetValue(Enum.Parse(T("UnityEngine.InputSystem.Key"), name), 0);
            var stateType = T("UnityEngine.InputSystem.LowLevel.KeyboardState");
            var state = Activator.CreateInstance(stateType, new object[] { keys });
            input.GetMethods().Single(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition).MakeGenericMethod(stateType)
                .Invoke(null, new[] { keyboard, state, -1d });
        }
        Component Phone(string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(go); go.name = name;
            go.transform.position = position; go.transform.localScale = Vector3.one * .3f;
            return go.AddComponent(T("CurtainCall.WaitingRoom.ShopPhone"));
        }
        [UnitySetUp]
        public IEnumerator Open()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var director = Find("CurtainCall.SpaceLink.SpaceLinkDirector") as Behaviour;
            if (director != null) director.enabled = false;
            actor = New("InteractionTestPlayer").AddComponent(T("CurtainCall.Player.PlayerMover"));
            actor.GetType().GetProperty("Simulated").SetValue(actor, false);
            Place(new Vector3(0, 100, 0));
            actor.gameObject.AddComponent(T("CurtainCall.Player.PlayerInputReader"));
            foreach (Behaviour brain in Object.FindObjectsByType(T("Unity.Cinemachine.CinemachineBrain"), FindObjectsSortMode.None)) brain.enabled = false;
            camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0, 101, -2), Quaternion.identity);
            Key(null);
            float deadline = Time.realtimeSinceStartup + 30;
            while ((ui = Find("CurtainCall.UI.SceneIntegration.CommonInteractionUI")) == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(ui);
            yield return Settle();
        }
        [UnityTest]
        public IEnumerator NearestTwo_ReevaluateMotionViewOcclusionAndDestruction()
        {
            var a = Phone("Near", new Vector3(-.5f, 101, 1));
            var b = Phone("Middle", new Vector3(.5f, 101, 2));
            var c = Phone("Far", new Vector3(1, 101, 3));
            var originalMaterial = a.GetComponent<Renderer>().sharedMaterial;
            yield return Settle();
            CollectionAssert.AreEquivalent(new[] { a.transform, b.transform }, Highlighted);
            Assert.AreEqual(2, Get(ui, "HighlightCount"));
            Assert.AreSame(originalMaterial, a.GetComponent<Renderer>().sharedMaterial);
            Assert.IsNotNull(a.transform.Find("__InteractionOutline"));
            a.transform.position += Vector3.right * 20;
            yield return Settle();
            CollectionAssert.AreEquivalent(new[] { b.transform, c.transform }, Highlighted);
            camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            yield return Settle();
            Assert.IsEmpty(Highlighted, "Behind-camera objects must disappear immediately.");
            camera.transform.rotation = Quaternion.identity;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(wall);
            wall.transform.position = new Vector3(0, 101, .3f); wall.transform.localScale = new Vector3(5, 4, .1f);
            yield return Settle();
            Assert.IsEmpty(Highlighted, "An opaque wall must hide interaction indications.");
            wall.SetActive(false);
            Object.Destroy(b.gameObject);
            yield return Settle();
            CollectionAssert.AreEquivalent(new[] { c.transform }, Highlighted);
            Place(new Vector3(0, 100, -10));
            yield return Settle();
            Assert.IsEmpty(Highlighted, "Camera visibility alone must not bypass player range.");
        }
        [UnityTest]
        public IEnumerator ActualPhone_UsesC6AndF_ThenClosesAndReloadsWithoutDuplicates()
        {
            var phone = Find("CurtainCall.WaitingRoom.ShopPhone");
            Assert.IsNotNull(phone.transform.Find("Handset"), "A real visible handset must replace the greybox cube.");
            Assert.IsNull(phone.GetComponent<MeshRenderer>());
            Assert.IsTrue(phone.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.bounds.size.y > .3f));
            Place(phone.transform.position + new Vector3(1.2f, -1.4f, 0));
            camera.transform.position = phone.transform.position + new Vector3(2.2f, .35f, .4f);
            camera.transform.LookAt(phone.transform);
            yield return Settle();
            Assert.Contains(phone.transform, Highlighted);
            var prompt = (Component)Get(ui, "Prompt");
            StringAssert.Contains("수화기 들기", string.Join(" ", prompt.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text)));
            Assert.AreSame(phone, T("CurtainCall.WaitingRoom.ShopReach").GetProperty("Focused").GetValue(null));
            Key("F"); yield return new WaitForSecondsRealtime(.05f);
            Key(null); yield return new WaitForSecondsRealtime(.05f);
            Assert.IsTrue((bool)phone.GetType().GetField("open", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(phone));
            Assert.IsTrue((bool)T("CurtainCall.Player.InputGate").GetProperty("IsBlocked").GetValue(null));
            Assert.IsEmpty(Highlighted, "An open shop window suppresses world hints.");
            Key("Escape"); yield return new WaitForSecondsRealtime(.05f);
            Key(null); yield return Settle();
            Assert.IsFalse((bool)phone.GetType().GetField("open", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(phone));
            var binding = (Behaviour)Find("CurtainCall.UI.SceneIntegration.InteractionSceneBinding");
            binding.enabled = false;
            float deadline = Time.realtimeSinceStartup + 15;
            while (SceneManager.GetSceneByName("CommonInteractionUI").isLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(SceneManager.GetSceneByName("CommonInteractionUI").isLoaded);
            binding.enabled = true;
            deadline = Time.realtimeSinceStartup + 15;
            while ((Find("CurtainCall.UI.SceneIntegration.CommonInteractionUI") == null || Find("CurtainCall.UI.SceneIntegration.CommonInteractionUI") == ui) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Settle();
            Assert.AreEqual(1, Object.FindObjectsByType(T("CurtainCall.UI.SceneIntegration.CommonInteractionUI"), FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator WorldLabels_FollowDeliveryHeldFloorShelfAndVisibility()
        {
            var campaign = Find("CurtainCall.Campaign.CampaignRun");
            Assert.IsTrue((bool)Call(campaign, "StartOfflineRun"));
            Call(campaign, "EndIntro"); Call(campaign, "EndPerformance");
            yield return Settle();
            var binding = Find("CurtainCall.UI.SceneIntegration.WaitingRoomWorldLabels");
            var rewards = Find("CurtainCall.Reward.PerformanceRewards");
            var model = rewards.GetType().GetField("model", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(rewards);
            // Seed only test funds; purchase, delivery, claim, drop and shelf use the production request path.
            model.GetType().GetProperty("Balance").SetValue(model, 1000L);
            var shop = Find("CurtainCall.WaitingRoom.WaitingRoomShop");
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            var catalog = ((IEnumerable)Get(shop, "Catalog")).Cast<object>().First();
            object Member(object o, string n) => o.GetType().GetField(n)?.GetValue(o) ?? Get(o, n);
            string id = (string)Member(catalog, "Id"), name = (string)Member(catalog, "Name");
            Call(shop, "RequestPurchase", id);
            yield return new WaitForSecondsRealtime(.75f);
            var order = ((IEnumerable)Get(shop, "Waiting")).Cast<object>().Single();
            Assert.AreEqual(1, Get(binding, "ItemLabelCount"));
            var label = binding.transform.Find("Items").GetChild(0);
            var view = label.GetComponent(T("CurtainCall.UI.WorldLabelsV2.WorldLabelView"));
            Assert.AreEqual(name, Get(view, "Title"));
            Place(label.position + new Vector3(1, -label.position.y + .1f, 0));
            Assert.Greater(label.position.x, -18f, "Delivery must land inside the west wall, not behind it.");
            camera.transform.position = label.position + new Vector3(2, 1, 0); camera.transform.LookAt(label);
            yield return Settle();
            Assert.AreEqual(true, Get(view, "IsVisible"));
            var promptText = ((Component)Get(ui, "Prompt")).GetComponentsInChildren<TMP_Text>(true);
            Assert.IsTrue(promptText.Any(t => t.text.Contains("#") && t.text.Contains("주문")), "Order details belong in C6's helper line.");
            camera.transform.rotation = Quaternion.Euler(0, 180, 0); yield return Settle();
            Assert.AreEqual(false, Get(view, "IsVisible"));
            camera.transform.LookAt(label);
            Place(actor.transform.position + Vector3.right * 8); yield return Settle();
            Assert.AreEqual(false, Get(view, "IsVisible"));
            Call(shop, "RequestClaim", (int)Member(order, "Order"));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.AreEqual(1, Get(binding, "ItemLabelCount"), "Claim removes the delivery label and creates the held label.");
            // Stay inside: the production exit watcher intentionally rejects re-entry after leaving a rest period.
            Place(new Vector3(-5, .1f, 67));
            Call(carry, "RequestDrop"); yield return Settle();
            Assert.AreEqual(1, Get(binding, "ItemLabelCount"));
            Assert.IsTrue(binding.transform.Find("Items").Cast<Transform>().Any(t => t.name.Contains("FloorProp")));
            var floorItem = ((IEnumerable)Get(carry, "Floor")).Cast<object>().Single();
            int uid = (int)Member(Member(floorItem, "Item"), "Uid");
            Call(carry, "RequestPickUp", uid); yield return new WaitForSecondsRealtime(.6f);
            Call(carry, "RequestShelf"); yield return Settle();
            Assert.AreEqual(1, Get(binding, "ItemLabelCount"));
            Assert.IsTrue(binding.transform.Find("Items").Cast<Transform>().Any(t => t.name.Contains("ShelfProp")));
            Assert.AreEqual(4, binding.transform.Cast<Transform>().Count(t => t.name.StartsWith("W5_ShelfSlot_")));
            Place(new Vector3(-1.65f, .1f, 56f));
            camera.transform.position = new Vector3(-1.65f, 1.6f, 57.4f);
            camera.transform.LookAt(new Vector3(-1.65f, 1.2f, 54.72f));
            yield return Settle(); yield return Settle();
            foreach (Transform slot in binding.transform.Cast<Transform>().Where(t => t.name.StartsWith("W5_ShelfSlot_")))
                Assert.AreEqual(true, Get(slot.GetComponent(T("CurtainCall.UI.WorldLabelsV2.WorldLabelView")), "IsVisible"),
                    $"Shelf's own collider must not hide {slot.name}; viewport={camera.WorldToViewportPoint(slot.position)}; actor={actor.transform.position}; near={Get(slot.GetComponent(T("CurtainCall.UI.WorldLabelsV2.WorldLabelProximity")), "IsNear")}");
            Call(carry, "RequestShelf"); yield return new WaitForSecondsRealtime(.6f);
            Assert.AreEqual(1, Get(binding, "ItemLabelCount"));
            ((Behaviour)binding).enabled = false; yield return null;
            Assert.AreEqual(0, binding.transform.Find("Items").childCount);
            ((Behaviour)binding).enabled = true; yield return Settle();
            Assert.AreEqual(1, Get(binding, "ItemLabelCount"));
        }

        [UnityTest]
        public IEnumerator ExpertLabel_ReadsActualServiceStateWithoutLegacySign()
        {
            var campaign = Find("CurtainCall.Campaign.CampaignRun");
            Call(campaign, "StartOfflineRun"); Call(campaign, "EndIntro"); Call(campaign, "EndPerformance");
            yield return Settle();
            var binding = Find("CurtainCall.UI.SceneIntegration.WaitingRoomWorldLabels");
            var label = Get(binding, "ExpertLabel");
            Assert.AreEqual("밤 정비 때 영업", Get(label, "Title"));
            var sign = ((Component)label).transform;
            var origin = new Vector3(6.6f, 2f, 76.5f);
            Assert.IsFalse(Physics.Raycast(origin, (sign.position - origin).normalized, Vector3.Distance(origin, sign.position) - .02f, ~0, QueryTriggerInteraction.Ignore), "W4 must be in front of the wall and valance.");
            Assert.IsNull(GameObject.Find("ExpertRoomSign"));
            Call(campaign, "NotifyAllAtStageEntrance"); Call(campaign, "EndPerformance");
            yield return Settle();
            Assert.AreEqual("의료 전문가 대기 중", Get(label, "Title"));
            var service = Find("CurtainCall.WaitingRoom.ExpertService");
            var book = service.GetType().GetField("book", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(service);
            // Simulate a received authoritative service state; W4 must reflect all phases without controlling them.
            foreach (var pair in new[] { ("Booked", "입장 대기"), ("Working", "이용 중"), ("Exiting", "복구 완료") })
            {
                book.GetType().GetProperty("Phase").SetValue(book, Enum.Parse(T("CurtainCall.WaitingRoom.ExpertRoomPhase"), pair.Item1));
                ((Behaviour)service).enabled = false;
                yield return Settle();
                StringAssert.Contains(pair.Item2, (string)Get(label, "Title"));
            }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null) T("UnityEngine.InputSystem.InputSystem").GetMethod("RemoveDevice").Invoke(null, new[] { keyboard });
            keyboard = null;
            foreach (var go in created) if (go != null) Object.Destroy(go);
            created.Clear();
            var loader = Find("CurtainCall.UI.SceneIntegration.CommonUiSceneLoader");
            if (loader != null) Object.Destroy(loader.gameObject);
            yield return null;
            // Unload scene-owned services first, so ExpertService cannot recreate a reward manager during teardown.
            var empty = SceneManager.CreateScene("InteractionTestCleanup");
            SceneManager.SetActiveScene(empty);
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != empty).ToArray();
            foreach (var scene in loaded) if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (string type in new[] { "CurtainCall.WaitingRoom.ScheduleBoard", "CurtainCall.WaitingRoom.WaitingRoomShop", "CurtainCall.WaitingRoom.PropCarry",
                "CurtainCall.Campaign.CampaignRun", "CurtainCall.Reward.PerformanceRewards", "CurtainCall.SpaceLink.SpaceLinkDirector", "CurtainCall.Network.Session.NetworkSessionManager",
                "CurtainCall.UI.SceneIntegration.CommonUiSceneLoader", "CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation" })
            {
                var target = Find(type); if (target != null) Object.Destroy(target.gameObject);
            }
            yield return null;
        }
    }
}
