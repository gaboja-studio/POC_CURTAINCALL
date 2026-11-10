using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Settings;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    /// <summary>일정과 독립인 제한시간, 정비 추격과 수면·부활 전환을 검증한다.</summary>
    [Category("BreakFlow")]
    [Category("BreakFollowup")]
    public sealed class RunTimeLimitTests
    {
        [Test, Category("WaitingRoomCommonRecovery")]
        public void NightPreparationExcludesDeadButDawnStillWaitsForEveryConnectedScreen()
        {
            var oldGate = RunState.NightEndGate;
            try
            {
                RunState.NightEndGate = () => true;
                var run = Start(1, RunStartPhase.Night, true);
                var connected = new ulong[] { 1, 2 };
                run.SetReady(1, true);
                Assert.IsTrue(run.RefreshNight(connected, new ulong[] { 1 }));
                Assert.AreEqual(CampaignPhase.Dawn, run.Snapshot.Phase);
                run.SetReady(1, true);
                run.RefreshNight(connected, new ulong[] { 1 });
                Assert.AreEqual(CampaignPhase.Dawn, run.Snapshot.Phase);
                Assert.AreEqual(2, run.Snapshot.RequiredCount);
                run.SetReady(2, true);
                run.RefreshNight(connected, new ulong[] { 1 });
                Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
            }
            finally { RunState.NightEndGate = oldGate; }
        }

        [Test, Category("WaitingRoomCommonRecovery")]
        public void AllDeadNightCompletesButLivingPartyWaitsForRehearsalGate()
        {
            var oldGate = RunState.NightEndGate;
            try
            {
                RunState.NightEndGate = () => false;
                var run = Start(1, RunStartPhase.Night, true);
                run.SetReady(1, true);
                run.RefreshNight(new ulong[] { 1, 2 }, new ulong[] { 1 });
                Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase, "생존자가 있으면 리허설 종료를 기다린다.");
                run.RefreshNight(System.Array.Empty<ulong>(), System.Array.Empty<ulong>());
                Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase, "빈 세션은 자동 진행하지 않는다.");
                run.RefreshNight(new ulong[] { 1, 2 }, System.Array.Empty<ulong>());
                Assert.AreEqual(CampaignPhase.Dawn, run.Snapshot.Phase, "전원 사망은 기존 시간 만료와 같은 밤 종료");
                Assert.AreEqual(1, run.Snapshot.Generation);
            }
            finally { RunState.NightEndGate = oldGate; }
        }
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
            if (run.IsGuideRestWaiting) { run.SetTutorialDone(0); run.RefreshGuideRest(new ulong[] { 0 }); }
            return run;
        }

        [Test]
        public void LegacyDurationsMigrateWithoutLosingTuning()
        {
            var migrated = JsonUtility.FromJson<BaseGameSettings.RunSettings>("{\"restDuration\":123,\"guideRestDuration\":456}");
            Assert.AreEqual(123f, migrated.MorningRest.Seconds);
            Assert.AreEqual(123f, migrated.NoonRest.Seconds);
            Assert.AreEqual(456f, migrated.GuideRest.Seconds);
            Assert.IsFalse(migrated.NightMaintenance.Unlimited);
            Assert.AreEqual(300, migrated.NightMaintenance.Seconds);
            Assert.IsFalse(migrated.MorningRest.Unlimited);
            Assert.AreEqual(123f, migrated.RestDuration);
            Assert.AreEqual(456f, migrated.GuideRestDuration);
            var restored = JsonUtility.FromJson<BaseGameSettings.RunSettings>(JsonUtility.ToJson(migrated));
            Assert.AreEqual(456f, restored.GuideRest.Seconds);
            Assert.IsFalse(restored.NightMaintenance.Unlimited);
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
        public void FinalMorningSupportsConfiguredSafetyAndResumesItsDeadline()
        {
            // 2026-10-31 버그: 생활 구역에 머물면 마지막 아침 타이머가 멈춰 4일차 아침에서 런이 끝나지 않았다.
            Configure(rules.MorningRest, false, 300f);
            var run = Start(rules.DayCount + 1, RunStartPhase.Morning);
            Assert.IsTrue(run.Snapshot.IsFinalMorning);
            int generation = run.Snapshot.Generation;
            run.SetRestTimerPaused(true);
            Assert.AreEqual(RestTimerState.Stopped, run.Snapshot.Timer);
            Assert.IsFalse(run.Tick(301f));
            Assert.IsTrue(run.SetRestTimerPaused(false));
            Assert.IsTrue(run.Tick(301f));
            Assert.AreEqual(generation + 1, run.Snapshot.Generation, "기한이 끝나면 런 실패 → 세대 교체");
            Assert.AreEqual(RunEndReason.Deadline, run.Snapshot.LastEnd);
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
        public void NightTimeoutStartsPursuitAndPartialSleepWaitsForEveryone(bool presentation)
        {
            Configure(rules.NightMaintenance, false, 25f);
            var run = new RunState(false, presentation, true);
            run.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
            Assert.AreEqual(25f, run.Snapshot.TimeRemaining);
            Assert.IsTrue(run.Snapshot.AlarmRaised, "정비도 1분 이하 유한 타이머에서 경보를 표시한다");
            run.RefreshNight(new ulong[] { 1, 2 });
            run.SetReady(1, true);
            int expired = 0;
            Assert.IsTrue(run.Tick(25f, _ => expired++));
            Assert.AreEqual(1, expired);
            Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase);
            Assert.AreEqual(RestTimerState.Pursuit, run.Snapshot.Timer);
            Assert.IsFalse(run.Tick(10, _ => expired++), "Expiry is emitted only once");
            Assert.IsTrue(run.IsReady(1));
            run.RefreshNight(new ulong[] { 1, 2 });
            Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase, "Partial sleep only updates the ready count");
            Assert.IsFalse(run.RefreshNight(new ulong[] { 1, 2 }));
            run.SetReady(2, true);
            Assert.IsTrue(run.RefreshNight(new ulong[] { 1, 2 }));
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

        [Test]
        public void MaintenanceSupportsSafetyPauseEscapeAndNextScheduledPerformance()
        {
            var run = new RunState(false, false, true);
            run.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
            run.SetSetlist(2, new[] { "Tightrope", "Tightrope" });
            Assert.IsTrue(run.SetRestTimerPaused(true));
            Assert.IsFalse(run.Tick(500));
            Assert.IsTrue(run.SetRestTimerPaused(false));
            run.DebugSetRemaining(.1f); run.Tick(1);
            Assert.AreEqual(RestTimerState.Pursuit, run.Snapshot.Timer);
            Assert.IsFalse(run.SetRestTimerPaused(true), "Safety cannot cancel an active pursuit");
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
            Assert.AreEqual(2, run.Snapshot.Day);
            Assert.AreEqual(0, run.Snapshot.PerformanceIndex);
            run = new RunState(false, false, true);
            run.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
            Assert.IsTrue(run.StartEscape());
            Assert.AreEqual(CampaignPhase.Escape, run.Snapshot.Phase);
        }

        [Test]
        public void MaintenanceAllCaughtAdvancesAndLivingSleepersCanFinishWithoutDeadPlayers()
        {
            var run = new RunState(false, false, true);
            run.DebugStartAt(new RunStartPoint(1, RunStartPhase.Night, 0, "Tightrope"));
            run.DebugBeginPursuit();
            run.SetReady(1, true);
            run.RefreshNight(new ulong[] { 1, 2 });
            Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase);
            Assert.IsTrue(run.RefreshNight(new ulong[] { 1 })); // Dead/disconnected participant removed by CampaignRun.
            Assert.AreEqual(2, run.Snapshot.Day);
            run.DebugStartAt(new RunStartPoint(2, RunStartPhase.Night, 0, "Tightrope"));
            run.DebugBeginPursuit();
            Assert.IsTrue(run.NotifyAllPursuitCaught());
            Assert.AreEqual(3, run.Snapshot.Day);
            Assert.AreEqual(RestKind.Morning, run.Snapshot.Rest);
        }

        [Test]
        public void ExplorationLockCanBeChangedIndependentlyForEachRestKind()
        {
            var s = new CampaignSnapshot { Phase = CampaignPhase.Night };
            Assert.IsFalse(rules.IsExplorationLocked(s));
            Set(rules.NightMaintenance, "explorationLocked", true);
            Assert.IsTrue(rules.IsExplorationLocked(s));
            s.Phase = CampaignPhase.RestTime; s.Rest = RestKind.Morning;
            Assert.IsFalse(rules.IsExplorationLocked(s));
            Set(rules.MorningRest, "explorationLocked", true);
            Assert.IsTrue(rules.IsExplorationLocked(s));
            s.Rest = RestKind.Between;
            Assert.IsFalse(rules.IsExplorationLocked(s));
        }

        [TestCase(RunStartPhase.Morning)]
        [TestCase(RunStartPhase.Between)]
        [TestCase(RunStartPhase.Night)]
        public void RuntimeCountdownWaitsForEveryPlayersGuideIncludingNight(RunStartPhase phase)
        {
            var run = new RunState(false, true, true, true);
            run.DebugStartAt(new RunStartPoint(2, phase, phase == RunStartPhase.Between ? 1 : 0, "Tightrope"));
            float seconds = run.Snapshot.TimeRemaining;
            Assert.AreEqual(RestTimerState.WaitingForExit, run.Snapshot.Timer);
            Assert.IsFalse(run.Tick(200));
            Assert.IsTrue(run.SetTutorialDone(1));
            run.RefreshGuideRest(new ulong[] { 1, 2 });
            Assert.AreEqual(RestTimerState.WaitingForExit, run.Snapshot.Timer);
            Assert.AreEqual(seconds, run.Snapshot.TimeRemaining);
            Assert.IsFalse(run.SetRestTimerPaused(false), "Zone updates cannot bypass the guide barrier");
            run.SetTutorialDone(2); run.RefreshGuideRest(new ulong[] { 1, 2 });
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
            Assert.IsTrue(run.Tick(1));
            Assert.AreEqual(seconds - 1, run.Snapshot.TimeRemaining);
        }

        [Test]
        public void LeavingPlayerCannotKeepTheGuideBarrierClosed()
        {
            var run = new RunState(false, true, true, true);
            run.DebugStartAt(new RunStartPoint(2, RunStartPhase.Night, 0, "Tightrope"));
            run.SetTutorialDone(1); run.RefreshGuideRest(new ulong[] { 1, 2 });
            Assert.IsTrue(run.RefreshGuideRest(new ulong[] { 1 }));
            Assert.AreEqual(RestTimerState.Running, run.Snapshot.Timer);
        }
    }
}
