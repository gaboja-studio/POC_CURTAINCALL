using System;
using System.Collections;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using NUnit.Framework;
using Unity.Netcode;
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
        public void OfficialHandoff_TransientBusyEventRetriesUntilTheSceneStarts()
        {
            var sceneResponses = new Queue<SceneEventProgressStatus>(new[]
            {
                SceneEventProgressStatus.SceneEventInProgress,
                SceneEventProgressStatus.SceneEventInProgress,
                SceneEventProgressStatus.Started
            });
            var status = SceneEventProgressStatus.None;
            var attempt = BeginSceneEvent(() => sceneResponses.Dequeue(), () => true, () => 0d, value => status = value);
            Assert.That(attempt.MoveNext(), Is.True);
            Assert.That(attempt.MoveNext(), Is.True);
            Assert.That(attempt.MoveNext(), Is.False);
            Assert.That(status, Is.EqualTo(SceneEventProgressStatus.Started));
            Assert.That(sceneResponses, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OfficialHandoff_BusyEventStopsOnSupersedingPhaseOrDeadline(bool cancel)
        {
            int calls = 0;
            double time = 0d;
            bool current = true;
            var reply = SceneEventProgressStatus.None;
            var attempt = BeginSceneEvent(() => { calls++; return SceneEventProgressStatus.SceneEventInProgress; },
                () => current, () => time, value => reply = value);
            Assert.That(attempt.MoveNext(), Is.True);
            if (cancel) current = false;
            else time = 31d;
            Assert.That(attempt.MoveNext(), Is.False);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(reply, Is.EqualTo(SceneEventProgressStatus.SceneEventInProgress));
        }

        [Test]
        public void OfficialHandoff_ConfiguredLongerSceneDeadlineKeepsRequestRecoverable()
        {
            double time = 0d;
            var reply = SceneEventProgressStatus.None;
            var attempt = BeginSceneEvent(() => time >= 110d ? SceneEventProgressStatus.Started
                : SceneEventProgressStatus.SceneEventInProgress, () => true, () => time, value => reply = value, 121d);
            Assert.That(attempt.MoveNext(), Is.True);
            time = 31d;
            Assert.That(attempt.MoveNext(), Is.True);
            time = 110d;
            Assert.That(attempt.MoveNext(), Is.False);
            Assert.That(reply, Is.EqualTo(SceneEventProgressStatus.Started));
        }

        [Test]
        public void OfficialHandoff_PermanentVerificationFailureIsNotRetried()
        {
            var reply = SceneEventProgressStatus.None;
            var attempt = BeginSceneEvent(() => SceneEventProgressStatus.SceneFailedVerification,
                () => throw new InvalidOperationException("A permanent failure must finish immediately"),
                () => 0d, value => reply = value);
            Assert.That(attempt.MoveNext(), Is.False);
            Assert.That(reply, Is.EqualTo(SceneEventProgressStatus.SceneFailedVerification));
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void OfficialHandoff_NextWaitingRoomAndEndingIgnoreDepartureReceipts(bool escapeSuccess, bool debugReload)
        {
            var model = new RunState();
            model.StartSession();
            Assert.That(model.EndIntro(), Is.True);
            Assert.That(model.EndPerformance(), Is.True);
            if (escapeSuccess)
            {
                if (model.IsGuideRestWaiting)
                {
                    model.SetTutorialDone(0);
                    model.RefreshGuideRest(new[] { 0UL });
                }
                Assert.That(model.StartEscape(), Is.True);
                Assert.That(model.EndEscape(true), Is.True);
            }
            var state = model.Snapshot;
            var space = SpaceRoute.ForPhase(state.Phase);
            var scene = SpaceRoute.SceneName(space, GameSettings.Base.SpaceTransition, state.TrickId);
            Assert.That(scene, Is.EqualTo(escapeSuccess ? GameSettings.Base.SpaceTransition.HubScene : WaitingRoomScenes.Name));
            var type = typeof(SpaceLinkDirector).GetNestedType("TransitionRequest", BindingFlags.NonPublic);
            var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Single();
            var request = constructor.Invoke(new object[] { space, scene, state, debugReload });
            Assert.That(type.GetProperty("IgnoresDisconnected").GetValue(request), Is.EqualTo(!debugReload));
            Assert.That(type.GetProperty("AllowsRecovery").GetValue(request), Is.EqualTo(!debugReload));
        }

        static IEnumerator BeginSceneEvent(Func<SceneEventProgressStatus> start, Func<bool> canRetry,
            Func<double> now, Action<SceneEventProgressStatus> done, double timeout = 30d)
        {
            var method = typeof(SpaceLinkDirector).GetMethod("BeginNetworkSceneEvent", BindingFlags.Static | BindingFlags.NonPublic);
            return (IEnumerator)method.Invoke(null, new object[] { start, canRetry, now, timeout, done });
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
