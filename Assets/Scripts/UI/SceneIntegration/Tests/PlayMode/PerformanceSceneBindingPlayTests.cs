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

        [TestCase(0f, 0f)]
        [TestCase(1920f, 1080f)]
        [TestCase(960f, 540f)]
        public void NearbyTooltipKeepsItsFullFootprintInsideTheScreen(float x, float y)
        {
            var method = GameType("CurtainCall.UI.SceneIntegration.HumanCannonUiBinding").GetMethod("ClampTooltip");
            var size = new Vector2(384, 252.5f);
            var position = (Vector2)method.Invoke(null, new object[] { new Vector2(x, y), size, new Vector2(1920, 1080) });
            Assert.That(position.x, Is.GreaterThanOrEqualTo(24));
            Assert.That(position.y, Is.GreaterThanOrEqualTo(24));
            Assert.That(position.x + size.x, Is.LessThanOrEqualTo(1896));
            Assert.That(position.y + size.y, Is.LessThanOrEqualTo(1056));
        }

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
            var balance = (Component)hud.GetType().GetProperty("Balance").GetValue(hud);
            var settings = GameType("CurtainCall.Settings.GameSettings").GetProperty("Base").GetValue(null);
            var layout = settings.GetType().GetProperty("HudLayout").GetValue(settings);
            var field = layout.GetType().GetField("balancePosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var original = field.GetValue(layout);
            try
            {
                field.SetValue(layout, Enum.ToObject(field.FieldType, 1));
                yield return null;
                var rect = (RectTransform)balance.transform;
                Assert.AreEqual(new Vector2(.5f, 1), rect.anchorMin);
                Assert.AreEqual(new Vector2(0, -40), rect.anchoredPosition);
                Assert.AreEqual(-56, ((RectTransform)rect.Find("60_DwellTrack")).anchoredPosition.y);
                Assert.AreEqual(-60, ((RectTransform)rect.Find("61_DwellFill")).anchoredPosition.y);
                field.SetValue(layout, Enum.ToObject(field.FieldType, 0));
                yield return null;
                Assert.AreEqual(new Vector2(.5f, 0), rect.anchorMin);
                Assert.AreEqual(new Vector2(0, 28), rect.anchoredPosition);
                Assert.AreEqual(13, ((RectTransform)rect.Find("60_DwellTrack")).anchoredPosition.y);
                Assert.AreEqual(9, ((RectTransform)rect.Find("61_DwellFill")).anchoredPosition.y);
                Assert.IsFalse(timer.gameObject.activeSelf);
            }
            finally { field.SetValue(layout, original); }
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
            var ending = Object.FindFirstObjectByType(GameType("CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation")) as Component;
            if (ending != null) Object.Destroy(ending.gameObject);
            var transition = SceneManager.GetSceneByPath("Assets/Scenes/UI/PerformanceTransitionUI.unity");
            if (transition.isLoaded) yield return SceneManager.UnloadSceneAsync(transition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoloProgressUsesRealMarkerAndClearsInvalidData()
        {
            yield return SceneManager.LoadSceneAsync(CommonScene, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(CommonScene);
            var type = GameType("CurtainCall.UI.SceneIntegration.SoloPerformanceProgress");
            var solo = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(type, true)).Single();
            object Field(string name) => type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(solo);
            var show = type.GetMethod("Show");
            Assert.AreEqual(true, show.Invoke(solo, new object[] { 100f, 25f, true, 10f, 30f, null }));
            var player = (RectTransform)Field("playerRoot");
            var fire = (RectTransform)Field("fireRoot");
            Assert.IsTrue(player.gameObject.activeSelf);
            Assert.AreEqual(new Vector2(114, -30), player.anchoredPosition);
            Assert.AreEqual(new Vector2(32, 32), player.sizeDelta);
            Assert.AreEqual(new Vector2(60, -16), fire.anchoredPosition);
            Assert.IsTrue(((Image)Field("fireMarker")).enabled);
            Assert.AreEqual(false, show.Invoke(solo, new object[] { 0f, 25f, true, 10f, null, null }));
            Assert.IsFalse(player.gameObject.activeSelf);
            Assert.IsFalse(fire.gameObject.activeSelf);
            Assert.AreEqual(true, show.Invoke(solo, new object[] { 100f, 60f, false, null, null, 20f }));
            Assert.AreEqual(new Vector2(24, 24), player.sizeDelta);
            Assert.IsFalse(fire.gameObject.activeSelf);
            Assert.IsTrue(((Image)Field("fallMarker")).gameObject.activeSelf);
            type.GetMethod("Clear").Invoke(solo, null);
            Assert.IsFalse(((Image)Field("fallMarker")).gameObject.activeSelf);
            var source = (Component)Field("source");
            var roots = (RectTransform[])source.GetType().GetField("playerRoots", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(source);
            foreach (int count in new[] { 2, 4 })
            {
                var distances = Enumerable.Range(0, count).Select(i => 10f + i * 20).ToArray();
                Assert.AreEqual(true, source.GetType().GetMethod("SetProgress").Invoke(source,
                    new object[] { 100f, distances, count - 1, 5f, null, Array.Empty<float>(), false }));
                Assert.AreEqual(count, roots.Count(r => r.gameObject.activeSelf));
                Assert.AreEqual(new Vector2(32, 32), roots[count - 1].sizeDelta);
                Assert.IsTrue(fire.gameObject.activeSelf);
            }
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
            var guide = (Component)commonType.GetProperty("Guide").GetValue(common);
            var tooltip = (Component)commonType.GetProperty("Tooltip").GetValue(common);
            Assert.That(guide, Is.Not.Null);
            Assert.That(tooltip, Is.Not.Null);
            var textType = GameType("TMPro.TMP_Text");
            foreach (var view in new[] { guide, tooltip })
                foreach (var label in view.GetComponentsInChildren(textType, true))
                {
                    var font = textType.GetProperty("font").GetValue(label);
                    var hasCharacter = font.GetType().GetMethod("HasCharacter", new[] { typeof(char), typeof(bool), typeof(bool) });
                    foreach (char character in "FSpace길게발사내리기트램펄린놓기봉잡기대포타기[]".Distinct())
                        Assert.That(hasCharacter.Invoke(font, new object[] { character, true, true }), Is.EqualTo(true), $"Missing {character} in {label.name}");
                }
            Assert.That(common.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
            Assert.That(common.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), Is.False);
            var one = root.GetComponent(type);
            var two = second.GetComponent(type);
            commonType.GetMethod("ClaimHints").Invoke(common, new object[] { one });
            commonType.GetMethod("ClaimHints").Invoke(common, new object[] { two });
            var width = Enum.ToObject(GameType("CurtainCall.UI.GuidesV1.GuideKeyWidth"), 0);
            Assert.That(guide.GetType().GetMethod("ShowKey").Invoke(guide, new[] { (object)"F", "발사", width }), Is.EqualTo(true));
            commonType.GetMethod("ReleaseHints").Invoke(common, new object[] { one });
            Assert.That(guide.GetType().GetProperty("HasRuntimePresentation").GetValue(guide), Is.EqualTo(true), "Previous owner must not clear the next space's guide");
            commonType.GetMethod("ReleaseHints").Invoke(common, new object[] { two });
            Assert.That(guide.GetType().GetProperty("HasRuntimePresentation").GetValue(guide), Is.EqualTo(false));
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
