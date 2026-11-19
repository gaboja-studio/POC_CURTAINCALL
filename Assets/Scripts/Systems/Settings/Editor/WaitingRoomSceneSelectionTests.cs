using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using NUnit.Framework;
using UnityEditor;
using System.Linq;

namespace CurtainCall.Settings.EditorTests
{
    public sealed class WaitingRoomSceneSelectionTests
    {
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
