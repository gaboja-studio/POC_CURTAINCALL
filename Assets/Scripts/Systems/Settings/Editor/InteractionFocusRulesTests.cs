using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Settings.Tests
{
    /// <summary>2026-11-16 상호작용 고도화: 우선순위 표·기준점 거리·동순위 처리.</summary>
    public sealed class InteractionFocusRulesTests
    {
        [Test]
        public void SettingsAssetExistsAndFollowsConfirmedOrder()
        {
            var settings = Resources.Load<InteractionFocusSettings>(InteractionFocusSettings.ResourcePath);
            Assert.IsNotNull(settings, "Resources/Settings/Interaction/InteractionFocusSettings.asset must exist for planners.");
            InteractionCategory[] waitingRoom =
            {
                InteractionCategory.Unlockable, InteractionCategory.PerformanceDevice, InteractionCategory.FirstClue,
                InteractionCategory.GrabObject, InteractionCategory.PickupItem, InteractionCategory.Service,
                InteractionCategory.Piggyback, InteractionCategory.Locked, InteractionCategory.PickupBlocked,
            };
            for (int i = 1; i < waitingRoom.Length; i++)
                Assert.Less(settings.Rank(waitingRoom[i - 1], true), settings.Rank(waitingRoom[i], true), waitingRoom[i].ToString());
            Assert.Less(settings.Rank(InteractionCategory.PerformanceDevice, false), settings.Rank(InteractionCategory.Piggyback, false));
            Assert.Less(settings.Rank(InteractionCategory.Piggyback, false), settings.Rank(InteractionCategory.PickupItem, false));
        }

        [Test]
        public void SameRankPrefersCrosshairThenPlayerDistance()
        {
            var aimed = new CommonInteractionSelection.Candidate(new object(), 1, 2f, "b", .01f, null);
            var closerBody = new CommonInteractionSelection.Candidate(new object(), 1, .5f, "a", .05f, null);
            var tiedScreen = new CommonInteractionSelection.Candidate(new object(), 1, .4f, "c", .01f, null);
            var higherRank = new CommonInteractionSelection.Candidate(new object(), 0, 3f, "z", .07f, null);
            Assert.IsTrue(CommonInteractionSelection.Better(aimed, closerBody), "Crosshair distance decides before body distance.");
            Assert.IsTrue(CommonInteractionSelection.Better(tiedScreen, aimed), "Equal crosshair distance falls back to the closer body.");
            Assert.IsTrue(CommonInteractionSelection.Better(higherRank, aimed), "Category order decides first.");
        }

        [Test]
        public void ScreenDistanceIsZeroUnderCrosshairAndGrowsOffCenter()
        {
            var cameraObject = new GameObject("FocusTestCamera");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 60f; camera.pixelRect = new Rect(0, 0, 1920, 1080);
                cameraObject.transform.SetPositionAndRotation(new Vector3(0, 0, -5), Quaternion.identity);
                cube.transform.position = Vector3.zero;
                Assert.AreEqual(0f, InteractionFocus.ScreenDistance(camera, cube.transform, cube.transform.position), 1e-4f);
                cube.transform.position = new Vector3(2f, 0, 0);
                Physics.SyncTransforms();
                float off = InteractionFocus.ScreenDistance(camera, null, cube.transform.position);
                Assert.Greater(off, .1f, "A point two meters to the side is far from the crosshair.");
                cube.transform.position = new Vector3(0, 0, -10);
                Assert.IsTrue(float.IsPositiveInfinity(InteractionFocus.ScreenDistance(camera, null, cube.transform.position)), "Behind the camera.");
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
