using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using NUnit.Framework;
using UnityEditor;
using System.Linq;
using System.Reflection;

namespace CurtainCall.Settings.EditorTests
{
    public sealed class WaitingRoomSceneSelectionTests
    {
        [TestCase(0, true, true)]
        [TestCase(15, true, true)]
        [TestCase(42, true, false)]
        [TestCase(42, false, true)]
        [Category("PerformanceTransitionDisconnect")]
        public void SceneTransitionKeepsTimeoutsForTheHostAndConnectedGuests(int clientId, bool enteringPerformance, bool shouldBlock)
        {
            var timedOut = new[] { (ulong)clientId };
            var connected = enteringPerformance ? new[] { 0UL, 15UL } : null;
            var filter = typeof(SpaceLinkDirector).GetMethod("RelevantSceneTimeouts", BindingFlags.Static | BindingFlags.NonPublic);
            var blocked = (System.Collections.Generic.IReadOnlyList<ulong>)filter.Invoke(null, new object[] { timedOut, connected });
            Assert.That(blocked.Count, Is.EqualTo(shouldBlock ? 1 : 0));
            if (shouldBlock) Assert.That(blocked[0], Is.EqualTo((ulong)clientId));
            Assert.That(timedOut, Is.EqualTo(new[] { (ulong)clientId }), "Do not mutate the NGO event payload.");
        }

        [Test]
        public void RestAndExplorationRouteToThePlayableWaitingRoom()
        {
            var settings = GameSettings.Base.SpaceTransition;
            Assert.AreEqual(WaitingRoomScenes.Name, SpaceRoute.SceneName(SpaceId.WaitingRoom, settings));
            Assert.AreEqual(WaitingRoomScenes.Name, SpaceRoute.SceneName(SpaceId.Exploration, settings));
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(WaitingRoomScenes.Path));
            var roomPaths = EditorBuildSettings.scenes.Where(s => s.enabled &&
                System.IO.Path.GetFileName(s.path).StartsWith("WaitingRoom", System.StringComparison.Ordinal)).Select(s => s.path);
            CollectionAssert.AreEqual(new[] { WaitingRoomScenes.Path }, roomPaths);
            Assert.IsFalse(WaitingRoomScenes.IsWaitingRoom("CommonWaitingRoomUI"));
        }
    }
}
