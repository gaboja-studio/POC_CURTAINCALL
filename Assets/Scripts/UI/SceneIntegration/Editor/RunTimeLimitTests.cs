using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Settings;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    /// <summary>실제 제한시간은 일정 표시와 독립이며, 밤 정비 만료는 잡힘 없이 수면으로 넘어간다.</summary>
    public sealed class RunTimeLimitTests
    {
        BaseGameSettings.RunSettings rules;
        string original;

        [SetUp]
        public void SaveRules()
        {
            rules = GameSettings.Base.Run;
            _ = rules.MorningRest;
            original = JsonUtility.ToJson(rules);
        }

        [TearDown]
        public void RestoreRules() => JsonUtility.FromJsonOverwrite(original, rules);

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static void Configure(BaseGameSettings.RunSettings.RestTimeLimitSettings limit, bool unlimited, float seconds)
        {
            Set(limit, "unlimited", unlimited);
            Set(limit, "seconds", seconds);
        }

        static RunState Start(int day, RunStartPhase phase, bool presentation = false)
        {
            var run = new RunState(false, presentation);
            Assert.IsTrue(run.DebugStartAt(new RunStartPoint(day, phase, phase == RunStartPhase.Between ? 1 : 0, "Tightrope")));
            return run;
        }

        [Test]
        public void LegacyDurationsMigrateWithoutLosingTuning()
        {
            var migrated = JsonUtility.FromJson<BaseGameSettings.RunSettings>("{\"restDuration\":123,\"guideRestDuration\":456}");
            Assert.AreEqual(123f, migrated.MorningRest.Seconds);
            Assert.AreEqual(123f, migrated.NoonRest.Seconds);
            Assert.AreEqual(456f, migrated.GuideRest.Seconds);
            Assert.IsTrue(migrated.NightMaintenance.Unlimited);
            Assert.IsFalse(migrated.MorningRest.Unlimited);
            Assert.AreEqual(123f, migrated.RestDuration);
            Assert.AreEqual(456f, migrated.GuideRestDuration);
            var restored = JsonUtility.FromJson<BaseGameSettings.RunSettings>(JsonUtility.ToJson(migrated));
            Assert.AreEqual(456f, restored.GuideRest.Seconds);
            Assert.IsTrue(restored.NightMaintenance.Unlimited);
        }

        [TestCase(2, RunStartPhase.Morning, RestKind.Morning, 111f)]
        [TestCase(2, RunStartPhase.Between, RestKind.Between, 222f)]
        [TestCase(1, RunStartPhase.Between, RestKind.Guide, 333f)]
        public void EachRestUsesItsOwnLimitOnlyOnEntry(int day, RunStartPhase phase, RestKind kind, float expected)
        {
            Configure(rules.MorningRest, false, 111f);
            Configure(rules.NoonRest, false, 222f);
            Configure(rules.GuideRest, false, 333f);
            var run = Start(day, phase);
            Assert.AreEqual(kind, run.Snapshot.Rest);
            Assert.AreEqual(expected, run.Snapshot.TimeRemaining);
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
            Configure(rules.TimeLimit(kind), true, 999f);
            Assert.IsTrue(run.Tick(1f));
            Assert.AreEqual(expected - 1f, run.Snapshot.TimeRemaining);
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
        }

        [TestCase(2, RunStartPhase.Morning, RestKind.Morning)]
        [TestCase(2, RunStartPhase.Between, RestKind.Between)]
        [TestCase(1, RunStartPhase.Between, RestKind.Guide)]
        public void UnlimitedRestNeverExpiresOrAlarmsButCanGather(int day, RunStartPhase phase, RestKind kind)
        {
            Configure(rules.TimeLimit(kind), true, 1f);
            var run = Start(day, phase);
            int expired = 0;
            Assert.AreEqual(RestTimerState.Unlimited, run.Snapshot.Timer);
            Assert.IsFalse(run.Tick(float.MaxValue, _ => expired++));
            Assert.AreEqual(0f, run.Snapshot.TimeRemaining);
            Assert.IsFalse(run.Snapshot.AlarmRaised);
            Assert.AreEqual(0, expired);
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
        }

        [Test]
        public void FinalMorningUnlimitedStillAllowsEscapeButNotStageGathering()
        {
            Configure(rules.MorningRest, true, 1f);
            var run = Start(rules.DayCount + 1, RunStartPhase.Morning);
            Assert.IsTrue(run.Snapshot.IsFinalMorning);
            Assert.IsFalse(run.Tick(float.MaxValue));
            Assert.IsFalse(run.NotifyAllAtStageEntrance());
            Assert.AreEqual(RestTimerState.Unlimited, run.Snapshot.Timer);
            Assert.IsTrue(run.StartEscape());
        }

        [Test]
        public void FiniteRestStillRaisesAlarmAndExpiresBeforePerformance()
        {
            Configure(rules.GuideRest, false, 100f);
            Set(rules, "recallAlarmLeadTime", 10f);
            var run = Start(1, RunStartPhase.Between);
            Assert.IsFalse(run.Snapshot.AlarmRaised);
            Assert.IsTrue(run.Tick(90f));
            Assert.IsTrue(run.Snapshot.AlarmRaised);
            CampaignSnapshot expired = default;
            Assert.IsTrue(run.Tick(10f, snapshot => expired = snapshot));
            Assert.AreEqual(CampaignPhase.RestTime, expired.Phase);
            Assert.AreEqual(0f, expired.TimeRemaining);
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
        }

        [Test]
        public void UnlimitedRestCanEscapeAndDebugRequiresFiniteValue()
        {
            Configure(rules.GuideRest, true, 1f);
            var run = Start(1, RunStartPhase.Between);
            Assert.IsFalse(run.DebugSetRemaining(float.PositiveInfinity));
            Assert.IsFalse(run.DebugSetRemaining(float.NaN));
            Assert.AreEqual(RestTimerState.Unlimited, run.Snapshot.Timer);
            Assert.IsTrue(run.StartEscape());
            Assert.AreEqual(RestTimerState.Stopped, run.Snapshot.Timer);
            Assert.IsTrue(run.EndEscape(true));
            Assert.AreEqual(CampaignPhase.Ending, run.Snapshot.Phase);

            run = Start(1, RunStartPhase.Between);
            Assert.IsTrue(run.DebugSetRemaining(10f));
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
            Assert.AreEqual(10f, run.Snapshot.TimeRemaining);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NightTimeoutUsesReadyCompletionWithoutRestExpired(bool presentation)
        {
            Configure(rules.NightMaintenance, false, 25f);
            var run = Start(1, RunStartPhase.Night, presentation);
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
            Assert.AreEqual(25f, run.Snapshot.TimeRemaining);
            Assert.IsFalse(run.Snapshot.AlarmRaised, "밤에는 소집 경보가 없다");
            run.RefreshNight(new ulong[] { 1, 2 });
            run.SetReady(1, true);
            int expired = 0;
            Assert.IsTrue(run.Tick(25f, _ => expired++));
            Assert.AreEqual(0, expired, "밤에는 RestExpired/잡힘 콜백을 부르지 않는다");
            if (presentation)
            {
                Assert.AreEqual(CampaignPhase.Dawn, run.Snapshot.Phase);
                Assert.AreEqual(RestTimerState.None, run.Snapshot.Timer);
                Assert.AreEqual(0f, run.Snapshot.TimeRemaining);
                Assert.AreEqual(0, run.Snapshot.ReadyCount, "밤 준비를 수면 ACK로 재사용하지 않는다");
                Assert.IsFalse(run.RefreshNight(new ulong[] { 1, 2 }));
                run.SetReady(1, true);
                Assert.IsTrue(run.RefreshNight(new ulong[] { 1, 2 }));
                Assert.AreEqual(CampaignPhase.Dawn, run.Snapshot.Phase);
                run.SetReady(2, true);
                Assert.IsTrue(run.RefreshNight(new ulong[] { 1, 2 }));
            }
            Assert.AreEqual(2, run.Snapshot.Day);
            Assert.AreEqual(RestKind.Morning, run.Snapshot.Rest);
            Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NightCanFinishEarlyWithOrWithoutLimit(bool unlimited)
        {
            Configure(rules.NightMaintenance, unlimited, 25f);
            var run = Start(1, RunStartPhase.Night, true);
            if (unlimited)
            {
                Assert.AreEqual(RestTimerState.Unlimited, run.Snapshot.Timer);
                Assert.IsFalse(run.Tick(float.MaxValue));
                Assert.IsFalse(run.Snapshot.AlarmRaised);
                Assert.AreEqual(0f, run.Snapshot.TimeRemaining);
            }
            run.SetReady(1, true);
            Assert.IsTrue(run.RefreshNight(new ulong[] { 1 }));
            Assert.AreEqual(CampaignPhase.Dawn, run.Snapshot.Phase);
            Assert.AreEqual(RestTimerState.None, run.Snapshot.Timer);
            Assert.AreEqual(0f, run.Snapshot.TimeRemaining);
        }

        [Test]
        public void UnlimitedWireValueAppendsWithoutChangingSnapshotLayout()
        {
            Assert.AreEqual(0, (byte)RestTimerState.None);
            Assert.AreEqual(1, (byte)RestTimerState.WaitingForExit);
            Assert.AreEqual(2, (byte)RestTimerState.Running);
            Assert.AreEqual(3, (byte)RestTimerState.Stopped);
            Assert.AreEqual(4, (byte)RestTimerState.Unlimited);
            var snapshot = new CampaignSnapshot
            {
                Generation = 7, Day = 2, Phase = CampaignPhase.Night, Timer = RestTimerState.Unlimited,
                TimeRemaining = 0f, ReadyCount = 1, RequiredCount = 2, TrickId = "", Difficulty = 3,
            };
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            typeof(CampaignSnapshot).GetMethod("Write", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(snapshot, new object[] { writer });
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            var restored = (CampaignSnapshot)typeof(CampaignSnapshot).GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { reader });
            Assert.IsTrue(snapshot.SameStep(restored));
            Assert.AreEqual(0f, restored.TimeRemaining);
            Assert.AreEqual(writer.Length, reader.Position, "새 필드를 추가하지 않고 기존 포맷 전체를 읽는다");
        }
    }
}
