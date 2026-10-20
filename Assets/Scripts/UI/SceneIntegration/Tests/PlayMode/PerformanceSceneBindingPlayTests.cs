using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class PerformanceSceneBindingPlayTests
    {
        GameObject root, legacy, second;
        const string CommonScene = "Assets/Scenes/UI/CommonPerformanceUI.unity";
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);

        [TestCase(-6d, 94d, 2d, 10d, 0d, 60d)]
        [TestCase(-6d, 94d, 2d, 10d, 20d, 40d)]
        [TestCase(-6d, 94d, 2d, 10d, 60d, 0d)]
        [TestCase(-6d, 94d, 2d, 10d, 80d, 0d)]
        public void FireCountdownUsesBoundaryAndNeverBecomesNegative(double start, double end, double speed, double delay, double elapsed, double expected)
        {
            var method = GameType("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding").GetMethod("FireBoundarySeconds");
            Assert.That(method.Invoke(null, new object[] { start, end, speed, delay, elapsed }), Is.EqualTo(expected));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        public void StationaryOrInvalidFireIsUnavailable(double speed)
        {
            var method = GameType("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding").GetMethod("FireBoundarySeconds");
            Assert.That(method.Invoke(null, new object[] { 0d, 100d, speed, 10d, 0d }), Is.Null);
        }

        [UnityTest]
        public IEnumerator NoGameDataHidesHudAndDisableRestoresLegacyCanvas()
        {
            root = Object.Instantiate(Resources.Load<GameObject>("Prefabs/UIs/PerformanceHud/PerformanceHud"));
            root.SetActive(false);
            var cheerRoot = Object.Instantiate(Resources.Load<GameObject>("Prefabs/UIs/CheerGauge/C1CheerGauge"), root.transform);
            var stock = (Behaviour)cheerRoot.GetComponent(GameType("CurtainCall.CheerGauge.C1CheerGaugeBinding"));
            stock.enabled = false;
            legacy = new GameObject("Legacy", typeof(RectTransform), typeof(Canvas));
            var old = (Behaviour)legacy.AddComponent(GameType("CurtainCall.Player.BalanceGaugeView"));
            var binding = (Behaviour)root.AddComponent(GameType("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding"));
            var hud = root.GetComponent(GameType("CurtainCall.UI.PerformanceHudV1.PerformanceHUDView"));
            var cheer = cheerRoot.GetComponent(GameType("CurtainCall.CheerGauge.C1CheerGaugeView"));
            binding.GetType().GetMethod("Configure").Invoke(binding, new object[] { hud, cheer, old });
            root.SetActive(true);
            yield return null;
            Assert.That(old.enabled, Is.False);
            Assert.That(legacy.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(cheerRoot.activeSelf, Is.False);
            var timer = (Component)hud.GetType().GetProperty("Timer").GetValue(hud);
            var progress = (Component)hud.GetType().GetProperty("Progress").GetValue(hud);
            Assert.That(timer.gameObject.activeSelf, Is.False);
            Assert.That(progress.gameObject.activeSelf, Is.False);
            Assert.That(root.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), Is.False);
            binding.enabled = false;
            Assert.That(old.enabled, Is.True);
            Assert.That(legacy.GetComponent<Canvas>().enabled, Is.True);
            binding.enabled = true;
            yield return null;
            Assert.That(old.enabled, Is.False);
            Assert.That(cheerRoot.activeSelf, Is.False);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            if (legacy != null) Object.Destroy(legacy);
            if (second != null) Object.Destroy(second);
            yield return null;
            var scene = SceneManager.GetSceneByPath(CommonScene);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            var loader = Object.FindFirstObjectByType(GameType("CurtainCall.UI.SceneIntegration.CommonUiSceneLoader")) as Component;
            if (loader != null) Object.Destroy(loader.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedSceneLoadsOnceSurvivesOneOwnerAndUnloadsAfterLastOwner()
        {
            root = new GameObject("SpaceOne");
            root.SetActive(false);
            var type = GameType("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding");
            root.AddComponent(type);
            second = new GameObject("SpaceTwo");
            second.SetActive(false);
            second.AddComponent(type);
            root.SetActive(true);
            second.SetActive(true);
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (!SceneManager.GetSceneByPath(CommonScene).isLoaded && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(SceneManager.GetSceneByPath(CommonScene).isLoaded, Is.True, "Common UI was not loaded");
            yield return null;
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).path == CommonScene) count++;
            Assert.That(count, Is.EqualTo(1));
            var commonType = GameType("CurtainCall.UI.SceneIntegration.CommonPerformanceUI");
            var common = (Component)commonType.GetProperty("Current").GetValue(null);
            Assert.That(common, Is.Not.Null);
            var hud = commonType.GetProperty("Hud").GetValue(common);
            Assert.That((Object)hud.GetType().GetProperty("Balance").GetValue(hud) == null, Is.True, "P1 must not belong to the shared scene");
            second.SetActive(false);
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetSceneByPath(CommonScene).isLoaded, Is.True);
            root.SetActive(false);
            deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (SceneManager.GetSceneByPath(CommonScene).isLoaded && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(SceneManager.GetSceneByPath(CommonScene).isLoaded, Is.False, "Last owner must release the shared scene");
        }
    }
}
