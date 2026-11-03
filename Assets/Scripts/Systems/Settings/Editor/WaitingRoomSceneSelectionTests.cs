using System.Reflection;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using CurtainCall.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Settings.EditorTests
{
    public sealed class WaitingRoomSceneSelectionTests
    {
        [TestCase(WaitingRoomSceneVariant.Base, "WaitingRoomBase")]
        [TestCase(WaitingRoomSceneVariant.Pursuit, "WaitingRoomPursuit")]
        [TestCase(WaitingRoomSceneVariant.Integrated, "WaitingRoom")]
        public void SelectionRoutesRestAndExplorationAndBothAssetsRemainAvailable(WaitingRoomSceneVariant variant, string expected)
        {
            var settings = new BaseGameSettings.SpaceTransitionSettings();
            typeof(BaseGameSettings.SpaceTransitionSettings).GetField("waitingRoomVariant", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, variant);
            Assert.AreEqual(expected, SpaceRoute.SceneName(SpaceId.WaitingRoom, settings));
            Assert.AreEqual(expected, SpaceRoute.SceneName(SpaceId.Exploration, settings));
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(WaitingRoomScenes.Path(variant)));
            Assert.IsTrue(System.Array.Exists(EditorBuildSettings.scenes, s => s.enabled && s.path == WaitingRoomScenes.Path(variant)));
            Assert.IsTrue(VoiceRoomRules.IsVoiceScene(true, expected, new[] { "WaitingRoomBase" }));
            Assert.IsFalse(VoiceRoomRules.IsVoiceScene(false, expected, new[] { "WaitingRoomBase" }));
        }
        [Test] public void CustomDummyRoutesArePreserved()
        {
            Assert.AreEqual("RouteWaitingRoom", WaitingRoomScenes.Resolve("RouteWaitingRoom", WaitingRoomSceneVariant.Pursuit));
            Assert.AreEqual(WaitingRoomSceneVariant.Integrated, new BaseGameSettings.SpaceTransitionSettings().WaitingRoomVariant);
            Assert.IsFalse(WaitingRoomScenes.IsWaitingRoom("CommonWaitingRoomUI"));
        }
    }
}
