using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.PerformanceBackground.Tests
{
    public class PerformanceBackgroundPlayTests
    {
        Scene original;
        Scene performance;
        Scene next;
        Scene trigger;
        GameObject networkObject;

        static Scene Background => SceneManager.GetSceneByPath(PerformanceBackgroundLayout.ScenePath);

        [SetUp]
        public void SetUp() => original = SceneManager.GetActiveScene();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (networkObject != null)
            {
                networkObject.GetComponent<Unity.Netcode.NetworkManager>().Shutdown();
                UnityEngine.Object.Destroy(networkObject);
                yield return null;
            }
            if (trigger.IsValid() && trigger.isLoaded) yield return SceneManager.UnloadSceneAsync(trigger);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (performance.IsValid() && performance.isLoaded) yield return SceneManager.UnloadSceneAsync(performance);
            if (next.IsValid() && next.isLoaded) yield return SceneManager.UnloadSceneAsync(next);
            yield return WaitFor(() => !Background.isLoaded && !LoaderBusy());
        }

        [TestCase("Tightrope", 55.833333f)]
        [TestCase("HumanCannon", 54.458333f)]
        [TestCase("proto_0.0.2", 55.833333f)]
        public void ActualPerformancesHavePlacement(string name, float z)
        {
            Assert.IsTrue(PerformanceBackgroundLayout.TryGetPosition(name, out var position));
            Assert.That(position, Is.EqualTo(new Vector3(0f, 0f, z)));
        }

        [TestCase("Lobby")]
        [TestCase("WaitingRoomBase")]
        [TestCase("DummyPerformance")]
        public void NonPerformanceScenesAreExcluded(string name)
        {
            Assert.IsFalse(PerformanceBackgroundLayout.TryGetPosition(name, out _));
        }

        [UnityTest]
        public IEnumerator DirectEntryLoadsOneVisualBackgroundAndKeepsActiveScene()
        {
            performance = SceneManager.CreateScene("Tightrope");
            SceneManager.SetActiveScene(performance);
            // CreateScene은 sceneLoaded를 발행하지 않으므로 빈 additive 씬 로드로 실제 이벤트를 발생시킨다.
            yield return SceneManager.LoadSceneAsync("DummyExploration", LoadSceneMode.Additive);
            next = SceneManager.GetSceneByName("DummyExploration");
            yield return WaitFor(() => Background.isLoaded && Placement() != null);
            Assert.AreEqual(performance, SceneManager.GetActiveScene());
            Assert.That(Placement().position.z, Is.EqualTo(55.833333f).Within(0.001f));
            Assert.That(Placement().localScale.x, Is.EqualTo(1f / 3f).Within(0.0001f));
            Assert.AreEqual(1, BackgroundCount());
            foreach (var root in Background.GetRootGameObjects())
            {
                Assert.IsEmpty(root.GetComponentsInChildren<Camera>(true));
                Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true));
            }
        }

        [UnityTest]
        public IEnumerator ConsecutivePerformancesReuseBackgroundAndFinalExitUnloadsIt()
        {
            performance = SceneManager.CreateScene("Tightrope");
            SceneManager.SetActiveScene(performance);
            yield return SceneManager.LoadSceneAsync("DummyExploration", LoadSceneMode.Additive);
            trigger = SceneManager.GetSceneByName("DummyExploration");
            yield return WaitFor(() => Background.isLoaded && Placement() != null);
            var initialBackground = Background;
            next = SceneManager.CreateScene("HumanCannon");
            SceneManager.SetActiveScene(next);
            yield return SceneManager.UnloadSceneAsync(performance);
            yield return WaitFor(() => Placement() != null && Mathf.Abs(Placement().position.z - 54.458333f) < 0.001f);
            Assert.AreEqual(initialBackground, Background);
            Assert.AreEqual(1, BackgroundCount());
            SceneManager.SetActiveScene(original);
            yield return SceneManager.UnloadSceneAsync(next);
            yield return SceneManager.UnloadSceneAsync(trigger);
            yield return WaitFor(() => !Background.isLoaded && !LoaderBusy());
        }

        [UnityTest]
        public IEnumerator LeavingWhileLoadIsPendingDoesNotLeaveBackgroundBehind()
        {
            performance = SceneManager.CreateScene("HumanCannon");
            yield return SceneManager.LoadSceneAsync("DummyExploration", LoadSceneMode.Additive);
            next = SceneManager.GetSceneByName("DummyExploration");
            yield return null;
            yield return SceneManager.UnloadSceneAsync(performance);
            // 진행 중인 비동기 로드가 끝난 뒤에도 배경이 남지 않는지 확인한다.
            yield return WaitFor(() => !Background.isLoaded && !LoaderBusy());
            yield return null;
            Assert.IsFalse(Background.isLoaded);
        }

        [UnityTest]
        public IEnumerator PerformanceEnvironmentAndCameraAreRestoredOnExit()
        {
            performance = SceneManager.CreateScene("HumanCannon");
            SceneManager.SetActiveScene(performance);
            bool previousFog = RenderSettings.fog;
            var cameraObject = new GameObject("BackgroundCameraTest");
            SceneManager.MoveGameObjectToScene(cameraObject, performance);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            yield return WaitFor(() => Background.isLoaded && Placement() != null);
            yield return null;
            Assert.IsTrue(RenderSettings.fog);
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.00345f).Within(0.00001f));
            Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags);
            var loader = UnityEngine.Object.FindFirstObjectByType<PerformanceBackgroundLoader>();
            UnityEngine.Object.Destroy(loader.gameObject);
            yield return null;
            Assert.AreEqual(previousFog, RenderSettings.fog);
            Assert.AreEqual(CameraClearFlags.Skybox, camera.clearFlags);
            // 다음 테스트에서도 실제 부트스트랩과 같은 관리자를 사용한다.
            var replacement = new GameObject(nameof(PerformanceBackgroundLoader));
            UnityEngine.Object.DontDestroyOnLoad(replacement);
            replacement.AddComponent<PerformanceBackgroundLoader>();
        }

        [UnityTest]
        public IEnumerator HostSynchronizationExcludesLocalBackground()
        {
            networkObject = new GameObject("BackgroundNetworkTest");
            var manager = networkObject.AddComponent<Unity.Netcode.NetworkManager>();
            var transport = networkObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 0);
            manager.NetworkConfig = new Unity.Netcode.NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = true
            };
            Assert.IsTrue(manager.StartHost());
            yield return null;
            Assert.IsNotNull(manager.SceneManager.VerifySceneBeforeLoading);
            Assert.IsFalse(manager.SceneManager.VerifySceneBeforeLoading(0, PerformanceBackgroundLayout.SceneName, LoadSceneMode.Additive));
            Assert.IsTrue(manager.SceneManager.VerifySceneBeforeLoading(0, "HumanCannon", LoadSceneMode.Single));
        }

        static bool LoaderBusy()
        {
            var loader = UnityEngine.Object.FindFirstObjectByType<PerformanceBackgroundLoader>();
            return loader != null && loader.IsTransitioning;
        }

        static Transform Placement()
        {
            if (!Background.isLoaded) return null;
            foreach (var root in Background.GetRootGameObjects())
                if (root.name == "PerformanceBackgroundPlacement") return root.transform;
            return null;
        }

        static int BackgroundCount()
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == PerformanceBackgroundLayout.ScenePath) count++;
            return count;
        }

        static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), "배경 씬 상태 전환 시간 초과");
        }
    }
}
