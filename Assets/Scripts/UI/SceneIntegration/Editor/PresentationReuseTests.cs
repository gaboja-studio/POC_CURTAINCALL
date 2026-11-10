using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Cameras;
using CurtainCall.CheerGauge;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.PerformanceHudV1;
using CurtainCall.Survival;
using Unity.Cinemachine;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class PresentationReuseTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        GameObject root;
        CommonUiSceneLoader previousLoader;
        CommonPerformanceUI previousCommon;
        PerformanceSceneBinding binding;

        [SetUp]
        public void Setup()
        {
            previousLoader = (CommonUiSceneLoader)typeof(CommonUiSceneLoader).GetField("instance", Static).GetValue(null);
            previousCommon = CommonPerformanceUI.Current;
            root = new GameObject("Presentation reuse fixture"); root.SetActive(false);
            var loader = root.AddComponent<CommonUiSceneLoader>();
            binding = root.AddComponent<PerformanceSceneBinding>();
            binding.ConfigurePresentation(null);
            ((List<PerformanceSceneBinding>)typeof(CommonUiSceneLoader).GetField("spaces", Instance).GetValue(loader)).Add(binding);
            typeof(CommonUiSceneLoader).GetField("instance", Static).SetValue(null, loader);
        }
        [TearDown]
        public void Cleanup()
        {
            InputGate.SetBlocking(this, false);
            Object.DestroyImmediate(root);
            typeof(CommonUiSceneLoader).GetField("instance", Static).SetValue(null, previousLoader);
            typeof(CommonPerformanceUI).GetProperty("Current").SetValue(null, previousCommon);
        }
        [Test]
        public void EscapePresentationSelectsSharedUiWithoutSelectingPerformanceEnding()
        {
            root.SetActive(true);
            Assert.That(CommonUiSceneLoader.CurrentSpace, Is.SameAs(binding));
            Assert.IsFalse(CommonUiSceneLoader.HasPerformanceSpace);
            binding.ConfigureSpace(null, null);
            Assert.IsTrue(CommonUiSceneLoader.HasPerformanceSpace, "An actual performance keeps its original ending path.");
        }
        [Test]
        public void PresentationHidesScoresWithoutOverwritingLocalBalanceOrSubscribingToRewards()
        {
            var asset = Resources.Load<GameObject>("Prefabs/UIs/Roles/Retained/PerformanceHud/PerformanceHud");
            var hud = Object.Instantiate(asset, root.transform).GetComponent<PerformanceHUDView>();
            var local = Object.Instantiate(asset, root.transform).GetComponent<PerformanceHUDView>().Balance;
            var cheer = Object.Instantiate(Resources.Load<GameObject>("Prefabs/UIs/Roles/Retained/CheerGauge/C1CheerGauge"), root.transform).GetComponent<C1CheerGaugeView>();
            var common = root.AddComponent<CommonPerformanceUI>(); common.Configure(hud, cheer);
            binding.ConfigurePresentation(local);
            root.SetActive(true);
            typeof(CommonPerformanceUI).GetProperty("Current").SetValue(null, common);
            local.SetContext(true, false);
            local.SetBalance(0, -.4f, .4f, -.7f, .7f, .2f, "A", "D", false, false);
            cheer.gameObject.SetActive(true);
            typeof(PerformanceSceneBinding).GetMethod("LateUpdate", Instance).Invoke(binding, null);
            Assert.IsFalse(cheer.gameObject.activeSelf);
            Assert.IsTrue(local.HasValidData);
            Assert.IsNull(typeof(PerformanceSceneBinding).GetField("rewards", Instance).GetValue(binding));
        }
        [Test]
        public void SpectatorDirectionDoesNotConsumeMovementWhileMenuBlocksInput()
        {
            var player = root.AddComponent<NetworkPlayer>();
            var reader = root.GetComponent<PlayerInputReader>() ?? root.AddComponent<PlayerInputReader>();
            typeof(PlayerInputReader).GetField("<Current>k__BackingField", Instance).SetValue(reader, new PlayerInputFrame { Posture = 1 });
            var method = typeof(CameraSpectator).GetMethod("ReadDirection", Static);
            Assert.That(method.Invoke(null, new object[] { player }), Is.EqualTo(1));
            InputGate.SetBlocking(this, true);
            Assert.That(method.Invoke(null, new object[] { player }), Is.EqualTo(0));
        }

        [TestCase(PlayerState.Retired, true)]
        [TestCase(PlayerState.Fallen, true)]
        [TestCase(PlayerState.Retired, false)]
        public void FireDeathAndLimbDeathSelectSurvivorAfterDeathViewAndRecoverOnRestart(PlayerState death, bool hasSurvivor)
        {
            Assert.That(SurvivalRules.DeathState(SurvivalPlace.EscapeRoute), Is.EqualTo(PlayerState.Retired));
            var previousLocal = NetworkPlayer.Local;
            var previousDirector = CameraViewDirector.Current;
            var localRoot = new GameObject("Fire victim"); localRoot.SetActive(false); localRoot.transform.SetParent(root.transform);
            var local = localRoot.AddComponent<NetworkPlayer>();
            var survivorRoot = new GameObject("Fire survivor"); survivorRoot.SetActive(false); survivorRoot.transform.SetParent(root.transform);
            var survivor = survivorRoot.AddComponent<NetworkPlayer>();
            // Spectating now excludes inactive/pooled actors. Model a live survivor, rather
            // than an inactive object inserted into the roster, without spawning a session.
            root.SetActive(true);
            survivorRoot.SetActive(true);
            var players = (IList<NetworkPlayer>)NetworkPlayer.All;
            var rig = new GameObject("Fire spectator rig");
            try
            {
                var camera = rig.AddComponent<CinemachineCamera>();
                var director = rig.AddComponent<CameraViewDirector>();
                var spectator = rig.AddComponent<CameraSpectator>();
                typeof(CameraViewDirector).GetMethod("Awake", Instance).Invoke(director, null);
                typeof(CameraSpectator).GetMethod("Awake", Instance).Invoke(spectator, null);
                typeof(NetworkPlayer).GetProperty("Local").SetValue(null, local);
                players.Add(local); if (hasSurvivor) players.Add(survivor);
                director.SetLocalPlayer(local);
                var state = typeof(NetworkPlayer).GetField("state", Instance).GetValue(local);
                state.GetType().GetField("m_InternalValue", Instance).SetValue(state, death);
                var refresh = typeof(CameraViewDirector).GetMethod("RefreshDeathView", Instance);
                var update = typeof(CameraSpectator).GetMethod("UpdateSpectating", Instance);
                refresh.Invoke(director, null); update.Invoke(spectator, null);
                Assert.IsTrue(CameraSpectator.IsSpectating);
                Assert.IsNull(CameraSpectator.Target, "The death view delays survivor selection.");
                typeof(CameraViewDirector).GetField("deathStarted", Instance).SetValue(director, Time.unscaledTime - 1.6f);
                update.Invoke(spectator, null);
                if (hasSurvivor)
                {
                    Assert.That(CameraSpectator.Target, Is.SameAs(survivor));
                    Assert.That(camera.Target.TrackingTarget, Is.SameAs(survivor.transform));
                    refresh.Invoke(director, null); update.Invoke(spectator, null);
                    Assert.That(CameraSpectator.Target, Is.SameAs(survivor), "The same fire death cannot restart the death-view delay.");
                }
                else Assert.IsNull(CameraSpectator.Target, "No surviving teammate is a valid no-target state.");
                state.GetType().GetField("m_InternalValue", Instance).SetValue(state, PlayerState.Normal);
                refresh.Invoke(director, null); update.Invoke(spectator, null);
                Assert.IsFalse(CameraSpectator.IsSpectating);
                Assert.That(camera.Target.TrackingTarget, Is.SameAs(local.transform));
            }
            finally
            {
                players.Remove(local); players.Remove(survivor);
                Object.DestroyImmediate(rig);
                typeof(NetworkPlayer).GetProperty("Local").SetValue(null, previousLocal);
                typeof(CameraViewDirector).GetProperty("Current").SetValue(null, previousDirector);
            }
        }
    }
}
