using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.UI.RestTimerV28;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class RestTimerTests
    {
        [TestCase(1, "D-3")]
        [TestCase(2, "D-2")]
        [TestCase(3, "D-1")]
        [TestCase(4, "D-0")]
        [TestCase(0, "D-?")]
        [TestCase(5, "D-?")]
        public void ActualDayMapsToApprovedCountdownLabel(int day, string label)
            => Assert.AreEqual(label, RestTimerPresentation.FormatDay(day));

        [Test]
        public void CampaignTimerDistinguishesZeroUnlimitedAndMissingData()
        {
            var state = new CampaignSnapshot { Day = 2, Phase = CampaignPhase.RestTime, Rest = RestKind.Morning,
                Timer = RestTimerState.Running, TimeRemaining = 0 };
            var frame = RestTimer.Read(state, 180);
            Assert.IsTrue(frame.visible);
            Assert.AreEqual(RestTimeOfDay.Morning, frame.timeOfDay);
            Assert.AreEqual(RestTimerVisualState.Expired, RestTimerPresentation.Evaluate(frame));
            Assert.AreEqual("0:00", RestTimerPresentation.FormatRemaining(frame));
            state.Timer = RestTimerState.None;
            Assert.AreEqual(RestTimerVisualState.Unavailable, RestTimerPresentation.Evaluate(RestTimer.Read(state, 180)));
            state.Phase = CampaignPhase.Night; state.Timer = RestTimerState.Unlimited;
            frame = RestTimer.Read(state, 0);
            Assert.AreEqual(RestTimerVisualState.Unlimited, RestTimerPresentation.Evaluate(frame));
            Assert.AreEqual(RestTimeOfDay.Night, frame.timeOfDay);
            state.Phase = CampaignPhase.Performance;
            Assert.IsFalse(RestTimer.Read(state, 180).visible);
            state.Phase = CampaignPhase.Dawn;
            Assert.IsFalse(RestTimer.Read(state, 180).visible);
        }

        [TestCase(31, RestTimerVisualState.Normal)]
        [TestCase(30, RestTimerVisualState.Warning)]
        [TestCase(10, RestTimerVisualState.Critical)]
        [TestCase(0, RestTimerVisualState.Expired)]
        public void RealSecondsDriveThresholdsAndRing(int seconds, RestTimerVisualState expected)
        {
            var frame = new RestTimerSnapshot(true, 1, RestTimeOfDay.Day, RestTimerMode.Limited, seconds, 180);
            Assert.AreEqual(expected, RestTimerPresentation.Evaluate(frame));
            Assert.AreEqual(seconds / 180f, RestTimerPresentation.Fill(frame), .0001f);
            frame.isPaused = true;
            Assert.AreEqual(RestTimerVisualState.Paused, RestTimerPresentation.Evaluate(frame));
        }

        [Test]
        public void InvalidTimesAreUnavailableAndFiniteRatioIsClamped()
        {
            var frame = new RestTimerSnapshot(true, 1, RestTimeOfDay.Day, RestTimerMode.Limited, double.NaN, 180);
            Assert.AreEqual("--:--", RestTimerPresentation.FormatRemaining(frame));
            Assert.AreEqual(0, RestTimerPresentation.Fill(frame));
            frame.remainingSeconds = 240;
            Assert.AreEqual("4:00", RestTimerPresentation.FormatRemaining(frame));
            Assert.AreEqual(1, RestTimerPresentation.Fill(frame));
            frame.durationSeconds = 0;
            Assert.AreEqual(RestTimerVisualState.Unavailable, RestTimerPresentation.Evaluate(frame));
        }

        [Test]
        public void PrefabRestoresFiniteLayoutAfterUnlimitedAndKeepsOnlyTwoTextLabels()
        {
            var prefab = Resources.Load<RestTimerView>(RestTimerView.PrefabPath);
            Assert.IsNotNull(prefab);
            var instance = Object.Instantiate(prefab);
            try
            {
                var state = new CampaignSnapshot { Day = 1, Phase = CampaignPhase.RestTime, Rest = RestKind.Guide,
                    Timer = RestTimerState.Running, TimeRemaining = 135 };
                instance.Show(state, 180);
                Assert.AreEqual("2:15", instance.RemainingText);
                Assert.AreEqual("D-3", instance.DayText);
                Assert.AreEqual(.75f, instance.View.FillAmount);
                Assert.AreEqual(RestTimeOfDay.Day, instance.Snapshot.timeOfDay);
                state.Timer = RestTimerState.Unlimited;
                instance.Show(state, 0);
                Assert.IsTrue(instance.View.InfinityVisible);
                Assert.AreEqual("", instance.RemainingText);
                Assert.AreEqual(0, instance.View.FillAmount);
                state.Day = 2; state.Timer = RestTimerState.Running; state.TimeRemaining = 60;
                instance.Show(state, 60);
                Assert.IsFalse(instance.View.InfinityVisible);
                Assert.AreEqual("1:00", instance.RemainingText);
                Assert.AreEqual("D-2", instance.DayText);
                Assert.AreEqual(1, instance.View.FillAmount);
                var fill = instance.GetComponentsInChildren<Image>(true).Single(x => x.name == "30_Fill");
                Assert.AreEqual(Image.FillMethod.Radial360, fill.fillMethod);
                Assert.AreEqual((int)Image.Origin360.Top, fill.fillOrigin);
                Assert.IsTrue(fill.fillClockwise);
                Assert.AreEqual(2, instance.GetComponentsInChildren<TMP_Text>(true).Length);
                Assert.IsTrue(instance.GetComponentsInChildren<Image>(true).All(x => x.sprite != null && !x.raycastTarget));
                Assert.IsFalse(instance.GetComponent<CanvasGroup>().blocksRaycasts);
                Assert.AreEqual(new Vector2(192, 220), ((RectTransform)instance.transform).sizeDelta);
                Assert.AreEqual(new Vector2(48, -64), ((RectTransform)instance.transform).anchoredPosition);
                Assert.AreEqual(new Vector2(0, 1), ((RectTransform)instance.transform).anchorMin);
                instance.Hide(); Assert.IsFalse(instance.IsShown);
                Assert.AreEqual(0, instance.GetComponent<CanvasGroup>().alpha);
            }
            finally { Object.DestroyImmediate(instance.gameObject); }
        }

        [Test]
        public void WaitingRoomSceneSerializesSingleNewPrefabAndDisablesOldClocks()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CommonWaitingRoomUI.ScenePath);
            try
            {
                var ui = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CommonWaitingRoomUI>(true)).Single();
                Assert.IsNotNull(ui.RestTimerDisplay);
                Assert.AreEqual(RestTimerProjectBuilder.Prefab, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ui.RestTimerDisplay));
                Assert.IsFalse(ui.DayClockDisplay.gameObject.activeSelf);
                Assert.IsFalse(ui.Hud.Clock.gameObject.activeSelf);
                Assert.AreSame(ui.Hud.Clock.transform.parent, ui.RestTimerDisplay.transform.parent);
                Assert.AreEqual(1, scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RestTimerView>(true)).Count());
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
