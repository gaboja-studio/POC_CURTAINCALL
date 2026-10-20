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
            Place(new Vector3(-16.4f, .1f, 70));
            camera.transform.position = new Vector3(-14.5f, 2.4f, 71.3f);
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
