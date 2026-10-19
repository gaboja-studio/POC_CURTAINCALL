using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Network.Session;
using CurtainCall.Tightrope.Fire;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeFireSequenceTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static FireSettings DistanceSettings() => new()
        {
            triggerMode = FireTriggerMode.LeadDistance,
            warningSeconds = 10f,
            safeDistance = 100f,
            segments = new[]
            {
                new FireSegment(1, 0f, 100f, 120f, 30f),
                new FireSegment(4, 0f, 100f, 120f, 50f),
                new FireSegment(3, 0f, 100f, 180f, 70f)
            }
        };

        static FireSettings RandomSixLaneSettings() => new()
        {
            triggerMode = FireTriggerMode.LeadDistance,
            laneOrder = FireLaneOrder.RandomAll,
            warningSeconds = 20f,
            safeDistance = 100f,
            segments = new[]
            {
                new FireSegment(1, 0f, 100f, 30f, 10f),
                new FireSegment(2, 0f, 100f, 50f, 25f),
                new FireSegment(3, 0f, 100f, 70f, 40f),
                new FireSegment(4, 0f, 100f, 90f, 55f),
                new FireSegment(5, 0f, 100f, 110f, 70f)
            }
        };

        [Test]
        public void LeaderDistanceStartsWarningAndOnlyDeadlineActivatesTheLane()
        {
            var settings = DistanceSettings();
            ulong active = 0;
            int pending = -1;
            float deadline = -1f;
            Assert.IsFalse(FireMath.AdvanceSequence(settings, 100f, 29.99f, 4, ref active, ref pending, ref deadline));
            Assert.AreEqual(-1, pending, "시간이 많이 지나도 거리 기준 전에는 경고하지 않는다.");
            Assert.IsTrue(FireMath.AdvanceSequence(settings, 101f, 30f, 4, ref active, ref pending, ref deadline));
            Assert.AreEqual(0, pending);
            Assert.AreEqual(111f, deadline);
            Assert.AreEqual(0UL, active);
            Assert.IsFalse(FireMath.AdvanceSequence(settings, 110.99f, 70f, 4, ref active, ref pending, ref deadline));
            Assert.AreEqual(0UL, active);
            FireMath.AdvanceSequence(settings, 111f, 70f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(1UL, active);
            Assert.AreEqual(1, pending, "첫 줄이 점화된 뒤에만 두 번째 줄을 경고한다.");
            Assert.AreEqual(121f, deadline);
        }

        [Test]
        public void JumpingPastEveryThresholdStillWarnsEachLaneAndLeavesLaneTwoSafe()
        {
            var settings = DistanceSettings();
            ulong active = 0;
            int pending = -1;
            float deadline = -1f;
            FireMath.AdvanceSequence(settings, 0f, 90f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(0UL, active);
            Assert.AreEqual(0, pending);
            FireMath.AdvanceSequence(settings, 10f, 90f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(1UL, active);
            Assert.AreEqual(1, pending);
            FireMath.AdvanceSequence(settings, 20f, 90f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(3UL, active);
            Assert.AreEqual(2, pending);
            FireMath.AdvanceSequence(settings, 30f, 90f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(7UL, active);
            Assert.AreEqual(-1, pending);
            foreach (var segment in settings.segments) Assert.AreNotEqual(2, segment.lane);
        }

        [Test]
        public void RetreatAfterWarningDoesNotCancelOrPostponeIt()
        {
            var settings = DistanceSettings();
            ulong active = 0;
            int pending = -1;
            float deadline = -1f;
            FireMath.AdvanceSequence(settings, 0f, 30f, 4, ref active, ref pending, ref deadline);
            FireMath.AdvanceSequence(settings, 5f, 0f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(0, pending);
            Assert.AreEqual(10f, deadline);
            FireMath.AdvanceSequence(settings, 10f, 0f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(1UL, active);
        }

        [Test]
        public void TimeModeRetainsScheduledIgnitionButSimultaneousEntriesBecomeSequential()
        {
            var settings = DistanceSettings();
            settings.triggerMode = FireTriggerMode.Time;
            ulong active = 0;
            int pending = -1;
            float deadline = -1f;
            Assert.IsFalse(FireMath.AdvanceSequence(settings, 109f, 100f, 4, ref active, ref pending, ref deadline));
            FireMath.AdvanceSequence(settings, 110f, 0f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(120f, deadline);
            FireMath.AdvanceSequence(settings, 120f, 0f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(1UL, active);
            Assert.AreEqual(1, pending);
            Assert.AreEqual(130f, deadline, "두 번째 줄도 독립적인 경고 시간을 받는다.");
            FireMath.AdvanceSequence(settings, 130f, 0f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(3UL, active);
            Assert.AreEqual(-1, pending);
            FireMath.AdvanceSequence(settings, 170f, 0f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(2, pending);
            Assert.AreEqual(180f, deadline);
        }

        [Test]
        public void LongHostFrameDoesNotSkipTheNextWarning()
        {
            var settings = DistanceSettings();
            ulong active = 0;
            int pending = -1;
            float deadline = -1f;
            FireMath.AdvanceSequence(settings, 0f, 90f, 4, ref active, ref pending, ref deadline);
            FireMath.AdvanceSequence(settings, 100f, 90f, 4, ref active, ref pending, ref deadline);
            Assert.AreEqual(1UL, active);
            Assert.AreEqual(1, pending);
            Assert.AreEqual(110f, deadline);
        }

        [Test]
        public void ClientCountdownAtZeroKeepsWarningUntilHostConfirmsIgnition()
        {
            Assert.AreEqual(10f, FireMath.WarningRemaining(1, 1, 20f, 10f));
            Assert.AreEqual(0f, FireMath.WarningRemaining(1, 1, 20f, 25f));
            Assert.AreEqual(-1f, FireMath.WarningRemaining(2, 1, 20f, 25f), "다른 줄은 동시에 경고하지 않는다.");
            Assert.AreEqual(-1f, FireMath.WarningRemaining(1, -1, -1f, 25f));
        }

        [Test]
        public void InvalidOrEmptySegmentsDoNotPreventLaterValidLaneWarnings()
        {
            var settings = DistanceSettings();
            settings.segments = new[]
            {
                new FireSegment(7, 0f, 100f, 0f, 0f),
                new FireSegment(1, 50f, 50f, 0f, 0f),
                new FireSegment(6, 0f, 100f, 0f, 0f)
            };
            ulong active = 0;
            int pending = -1;
            float deadline = -1f;
            FireMath.AdvanceSequence(settings, 0f, 0f, 6, ref active, ref pending, ref deadline);
            Assert.AreEqual(2, pending, "줄 수가 여섯 개여도 같은 순차 규칙을 쓴다.");
        }

        [Test]
        public void SettingsJsonRetainsTriggerModeDistanceAndOriginalTime()
        {
            var settings = FireSettings.FromJson(DistanceSettings().ToJson());
            Assert.AreEqual(FireTriggerMode.LeadDistance, settings.triggerMode);
            Assert.AreEqual(50f, settings.segments[1].triggerDistance);
            Assert.AreEqual(120f, settings.segments[1].igniteAt);
            Assert.AreEqual(4, settings.segments[1].lane);
        }

        [Test]
        public void LegacyTimeSettingsRemainTimeBasedWhenNewFieldsAreMissing()
        {
            var settings = FireSettings.FromJson("{\"warningSeconds\":10,\"segments\":[{\"lane\":1,\"from\":55,\"to\":95,\"igniteAt\":120}]}");
            Assert.AreEqual(FireTriggerMode.Time, settings.triggerMode);
            Assert.AreEqual(FireLaneOrder.Configured, settings.laneOrder);
            Assert.AreEqual(120f, settings.segments[0].igniteAt);
        }

        [Test]
        public void SeededRandomSnapshotChangesOnlyLanesAndDoesNotEditTemplates()
        {
            var source = RandomSixLaneSettings();
            var first = source.Snapshot(100f, 6, 17);
            var repeat = source.Snapshot(100f, 6, 17);
            Assert.AreEqual(5, first.segments.Length);
            for (int i = 0; i < first.segments.Length; i++)
            {
                Assert.AreEqual(first.segments[i].lane, repeat.segments[i].lane);
                Assert.AreEqual(source.segments[i].from, first.segments[i].from);
                Assert.AreEqual(source.segments[i].to, first.segments[i].to);
                Assert.AreEqual(source.segments[i].igniteAt, first.segments[i].igniteAt);
                Assert.AreEqual(source.segments[i].triggerDistance, first.segments[i].triggerDistance);
                Assert.AreEqual(i + 1, source.segments[i].lane, "원본 설정의 줄 번호는 바뀌지 않는다.");
            }
            Assert.AreNotSame(source.segments, first.segments);
        }

        [Test]
        public void SixRandomLanesBurnExactlyFiveUniqueLanesInSequence()
        {
            var settings = RandomSixLaneSettings().Snapshot(100f, 6, 17);
            var remaining = new HashSet<int> { 1, 2, 3, 4, 5, 6 };
            ulong active = 0;
            int pending = -1;
            float deadline = -1f;
            FireMath.AdvanceSequence(settings, 0f, 100f, 6, ref active, ref pending, ref deadline);
            for (int i = 0; i < 5; i++)
            {
                FireMath.AdvanceSequence(settings, (i + 1) * 20f, 100f, 6, ref active, ref pending, ref deadline);
                Assert.IsTrue(remaining.Remove(settings.segments[i].lane), "같은 줄을 두 번 태우지 않는다.");
                Assert.AreEqual(5 - i, remaining.Count);
                Assert.AreEqual((1UL << (i + 1)) - 1UL, active);
            }
            Assert.AreEqual(-1, pending);
            Assert.AreEqual(1, remaining.Count);
        }

        [Test]
        public void EveryLaneCanBeTheFinalSurvivorAcrossDeterministicSeeds()
        {
            var survivors = new HashSet<int>();
            for (int seed = 0; seed < 256; seed++)
            {
                var order = FireMath.RandomLaneOrder(6, seed);
                Assert.AreEqual(6, new HashSet<int>(order).Count);
                survivors.Add(order[5]);
            }
            Assert.That(survivors, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5, 6 }));
        }

        [Test]
        public void SurvivorLookupRejectsMissingOrAmbiguousSafeLanes()
        {
            Assert.AreEqual(2, DistanceSettings().FindSurvivingLane(4));
            Assert.AreEqual(-1, DistanceSettings().FindSurvivingLane(6));
            Assert.AreEqual(-1, RandomSixLaneSettings().FindSurvivingLane(5));
            for (int seed = 0; seed < 32; seed++)
            {
                var settings = RandomSixLaneSettings().Snapshot(100f, 6, seed);
                int survivor = settings.FindSurvivingLane(6);
                Assert.That(survivor, Is.InRange(1, 6));
                foreach (var segment in settings.segments) Assert.AreNotEqual(survivor, segment.lane);
            }
        }

        [Test]
        public void FollowingSafeSideWarningsKeepsEveryStartingLaneConnectedWithoutCrossingBurnedLanes()
        {
            for (int seed = 0; seed < 128; seed++)
            {
                var settings = RandomSixLaneSettings().Snapshot(100f, 6, seed);
                int survivor = settings.FindSurvivingLane(6);
                for (int start = 1; start <= 6; start++)
                {
                    int current = start;
                    var burned = new HashSet<int>();
                    foreach (var segment in settings.segments)
                    {
                        int warningLane = segment.lane;
                        // 예고 줄의 반대편에 있거나 그 줄 위라면, 예고 중 생존줄 쪽으로 건너간다.
                        if ((current - warningLane) * (survivor - warningLane) <= 0)
                        {
                            int direction = survivor > warningLane ? 1 : -1;
                            int destination = warningLane + direction;
                            while (current != destination)
                            {
                                current += direction;
                                Assert.That(current, Is.InRange(1, 6));
                                Assert.IsFalse(burned.Contains(current),
                                    $"seed={seed}, start={start}: 이미 불탄 줄을 건너야 하면 안내가 늦다.");
                            }
                        }
                        burned.Add(warningLane);
                        Assert.IsFalse(burned.Contains(current));
                    }
                    Assert.AreEqual(survivor, current, $"seed={seed}, start={start}");
                }
            }
        }

        [Test]
        public void NewRoundSeedCanDrawANewOrderWithoutReusingThePreviousSnapshot()
        {
            var source = RandomSixLaneSettings();
            var previous = source.Snapshot(100f, 6, 17);
            var next = source.Snapshot(100f, 6, 18);
            Assert.AreNotSame(previous, next);
            Assert.AreNotSame(previous.segments, next.segments);
            bool different = false;
            for (int i = 0; i < previous.segments.Length; i++) different |= previous.segments[i].lane != next.segments[i].lane;
            Assert.IsTrue(different, "새 회차의 난수 seed가 새로운 순서를 선택한다.");
        }

        [Test]
        public void ConfiguredOrderKeepsLegacyLanesRegardlessOfSeedOrCourseLaneCount()
        {
            var settings = DistanceSettings().Snapshot(100f, 6, 17);
            Assert.AreEqual(FireLaneOrder.Configured, settings.laneOrder);
            Assert.AreEqual(3, settings.segments.Length);
            Assert.AreEqual(1, settings.segments[0].lane);
            Assert.AreEqual(4, settings.segments[1].lane);
            Assert.AreEqual(3, settings.segments[2].lane);
        }

        [TestCase(1, 0)]
        [TestCase(4, 3)]
        public void ExtraRandomTemplatesCannotBurnTheLastLane(int laneCount, int expectedCount)
        {
            var settings = RandomSixLaneSettings().Snapshot(100f, laneCount, 17);
            Assert.AreEqual(expectedCount, settings.segments.Length);
            var burned = new HashSet<int>();
            foreach (var segment in settings.segments)
            {
                Assert.That(segment.lane, Is.InRange(1, laneCount));
                Assert.IsTrue(burned.Add(segment.lane));
            }
            Assert.AreEqual(1, laneCount - burned.Count);
        }

        [Test]
        public void SnapshotClampsDistanceAndCopiesWithoutEditingTheSourceSettings()
        {
            var source = DistanceSettings();
            source.segments[0].triggerDistance = -5f;
            source.segments[1].triggerDistance = 150f;
            source.segments[2].triggerDistance = float.PositiveInfinity;
            var copy = source.Snapshot(100f);
            Assert.AreEqual(0f, copy.segments[0].triggerDistance);
            Assert.AreEqual(100f, copy.segments[1].triggerDistance);
            Assert.AreEqual(0f, copy.segments[2].triggerDistance);
            Assert.AreEqual(-5f, source.segments[0].triggerDistance);
            Assert.AreEqual(150f, source.segments[1].triggerDistance);
            Assert.AreNotSame(source.segments, copy.segments);
        }

        [Test]
        public void FullLaneFireStopsAtTheFinishBoundary()
        {
            var segment = DistanceSettings().segments[0];
            Assert.IsTrue(FireMath.SegmentContains(segment, 0f, -3f, -3f, 0.4f));
            Assert.IsTrue(FireMath.SegmentContains(segment, 99.99f, -3f, -3f, 0.4f));
            Assert.IsFalse(FireMath.SegmentContains(segment, 100f, -3f, -3f, 0.4f));
            Assert.IsFalse(FireMath.SegmentContains(segment, 50f, 0f, -3f, 0.4f));
        }

        [Test]
        public void HostStateRoundTripRetainsPendingWarningWithoutClientIgnition()
        {
            var go = NewInactiveFixture(out var fire, out _);
            try
            {
                var expected = RandomSixLaneSettings().Snapshot(100f, 6, 17);
                Set(fire, "snapshot", expected);
                Set(fire, "round", 3);
                Set(fire, "activeMask", 1UL);
                Set(fire, "pendingSegment", 1);
                Set(fire, "pendingIgniteAt", 30f);
                Set(fire, "furthestDistance", 50f);
                Set(fire, "elapsed", 35f); // 수신 시 이미 경고 시각을 지났어도 호스트의 활성 mask만 쓴다.
                using var writer = (FastBufferWriter)Invoke(fire, "WriteState");
                Set(fire, "round", -1);
                Set(fire, "activeMask", 0UL);
                Set(fire, "pendingSegment", -1);
                using var reader = new FastBufferReader(writer, Allocator.Temp);
                Invoke(fire, "HandleState", NetworkManager.ServerClientId, reader);
                Assert.AreEqual(3, fire.Round);
                Assert.IsTrue(fire.IsSegmentActive(0));
                Assert.IsFalse(fire.IsSegmentActive(1));
                Assert.AreEqual(1, fire.PendingSegment);
                Assert.AreEqual(30f, fire.PendingIgniteAt);
                Assert.AreEqual(50f, fire.FurthestDistance);
                Assert.AreEqual(FireTriggerMode.LeadDistance, fire.Settings.triggerMode);
                Assert.AreEqual(FireLaneOrder.RandomAll, fire.Settings.laneOrder);
                Assert.AreEqual(expected.segments.Length, fire.Settings.segments.Length);
                for (int i = 0; i < expected.segments.Length; i++)
                    Assert.AreEqual(expected.segments[i].lane, fire.Settings.segments[i].lane,
                        "클라이언트는 별도로 섞지 않고 호스트의 확정 순서를 받는다.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NewHostRoundClearsWarningBurningAndPreviousLeaderProgress()
        {
            var go = NewInactiveFixture(out var fire, out var session);
            var randomState = Random.state;
            try
            {
                typeof(NetworkSessionManager).GetProperty("ConnectionState", Members).SetValue(session, SessionConnectionState.Host);
                Set(fire, "snapshot", DistanceSettings());
                Set(fire, "round", 3);
                Set(fire, "activeMask", 1UL);
                Set(fire, "pendingSegment", 1);
                Set(fire, "pendingIgniteAt", 30f);
                Set(fire, "furthestDistance", 70f);
                Set(fire, "elapsed", 20f);
                Invoke(fire, "HandleRound");
                Assert.AreEqual(0, fire.Round);
                Assert.IsFalse(fire.IsSegmentActive(0));
                Assert.AreEqual(-1, fire.PendingSegment);
                Assert.AreEqual(-1f, fire.PendingIgniteAt);
                Assert.AreEqual(0f, fire.FurthestDistance);
                Assert.AreEqual(0f, fire.Elapsed);
            }
            finally { Random.state = randomState; Object.DestroyImmediate(go); }
        }

        static GameObject NewInactiveFixture(out TightropeFire fire, out NetworkSessionManager session)
        {
            var go = new GameObject("Fire sequence fixture") { hideFlags = HideFlags.DontSave };
            go.SetActive(false);
            var run = go.AddComponent<TightropeRun>();
            session = go.AddComponent<NetworkSessionManager>();
            fire = go.AddComponent<TightropeFire>();
            Set(fire, "run", run);
            Set(fire, "course", go.GetComponent<TightropeCourse>());
            Set(fire, "session", session);
            return go;
        }

        static void Set(object target, string field, object value) => target.GetType().GetField(field, Members).SetValue(target, value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Members).Invoke(target, args);
    }
}
