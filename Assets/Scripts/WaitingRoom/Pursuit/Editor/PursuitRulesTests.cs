using CurtainCall.Campaign;
using CurtainCall.Settings;
using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitRulesTests
    {
        [Test]
        public void PatrolBoundaryUsesSegmentsHeightAndFallbackInsteadOfWaypointRadius()
        {
            var a = new UnityEngine.GameObject("RouteA");
            var b = new UnityEngine.GameObject("RouteB");
            try {
                a.transform.position = new UnityEngine.Vector3(-10, 2, 0);
                b.transform.position = new UnityEngine.Vector3(10, 2, 0);
                var point = new UnityEngine.Vector3(0, 7, 0);
                var nearest = PursuitActor.ClosestPatrolPosition(point, new[] { a.transform, null, b.transform }, UnityEngine.Vector3.one);
                Assert.AreEqual(new UnityEngine.Vector3(0, 2, 0), nearest);
                Assert.AreEqual(5, UnityEngine.Vector3.Distance(point, nearest), .001f, "Height separates floors.");
                Assert.AreEqual(a.transform.position, PursuitActor.ClosestPatrolPosition(point, new[] { a.transform }, UnityEngine.Vector3.one));
                Assert.AreEqual(UnityEngine.Vector3.one, PursuitActor.ClosestPatrolPosition(point, null, UnityEngine.Vector3.one));
                Assert.AreEqual(UnityEngine.Vector3.one, PursuitActor.ClosestPatrolPosition(point, new UnityEngine.Transform[] { null }, UnityEngine.Vector3.one));
                var stages = new PursuitSettings().Stages;
                foreach (var stage in stages) Assert.AreEqual(0, stage.PatrolLeashDistance, "Existing stages remain unrestricted.");
                stages[1].PatrolLeashDistance = 8;
                Assert.AreEqual(0, stages[0].PatrolLeashDistance);
            } finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }

        static RunState Start(int day = 2)
        {
            var run = new RunState(true, false, true);
            Assert.IsTrue(run.DebugStartAt(new RunStartPoint(day, RunStartPhase.Morning, 0, "Tightrope")));
            return run;
        }
        [Test]
        public void DebugTimerResetAndExpiryWorkWhilePausedAndRejectOtherPhases()
        {
            var run = Start();
            Assert.IsTrue(run.SetRestTimerPaused(true));
            Assert.IsTrue(run.DebugBeginPursuit());
            Assert.AreEqual(RestTimerState.Pursuit, run.Snapshot.Timer);
            Assert.AreEqual(0, run.Snapshot.TimeRemaining);
            Assert.IsFalse(run.SetRestTimerPaused(true));
            Assert.IsTrue(run.DebugResetRestTimer());
            var limit = GameSettings.Base.Run.TimeLimit(run.Snapshot.Rest);
            Assert.AreEqual(limit.Unlimited ? RestTimerState.Unlimited : RestTimerState.Running, run.Snapshot.Timer);
            Assert.AreEqual(limit.Unlimited ? 0 : limit.Seconds, run.Snapshot.TimeRemaining);
            Assert.IsTrue(run.DebugSetRemaining(60));
            Assert.AreEqual(60, run.Snapshot.TimeRemaining);
            Assert.IsTrue(run.StartEscape());
            Assert.IsFalse(run.DebugResetRestTimer());
            Assert.IsFalse(run.DebugBeginPursuit());
        }
        [Test]
        public void TimeoutStaysInRestAndEmitsOnce()
        {
            var run = Start(); int notifications = 0;
            run.DebugSetRemaining(.01f);
            Assert.IsTrue(run.Tick(1, s => { notifications++; Assert.AreEqual(RestTimerState.Pursuit, s.Timer); }));
            Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
            Assert.AreEqual(RestTimerState.Pursuit, run.Snapshot.Timer);
            Assert.IsFalse(run.Tick(1, _ => notifications++));
            Assert.IsFalse(run.SetRestTimerPaused(false));
            Assert.AreEqual(1, notifications);
        }
        [Test]
        public void SurvivorsMayAssembleForPerformanceOrEscape()
        {
            var stage = Start(); stage.DebugSetRemaining(.01f); stage.Tick(1);
            Assert.IsTrue(stage.NotifyAllAtStageEntrance());
            Assert.AreEqual(CampaignPhase.Performance, stage.Snapshot.Phase);
            var escape = Start(); escape.DebugSetRemaining(.01f); escape.Tick(1);
            Assert.IsTrue(escape.StartEscape());
            Assert.AreEqual(CampaignPhase.Escape, escape.Snapshot.Phase);
        }
        [Test]
        public void AllCaughtOnOrdinaryDayStartsPerformance()
        {
            var run = Start(); run.DebugSetRemaining(.01f); run.Tick(1);
            Assert.IsTrue(run.NotifyAllPursuitCaught());
            Assert.AreEqual(CampaignPhase.Performance, run.Snapshot.Phase);
            Assert.AreEqual(1, run.Snapshot.Generation);
        }
        [Test]
        public void FinalMorningTimeoutEndsTheRunBeforePursuit()
        {
            var run = Start(4);
            Assert.IsTrue(run.Snapshot.IsFinalMorning);
            Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
            run.DebugSetRemaining(.01f); run.Tick(1);
            Assert.AreEqual(1, run.Snapshot.Generation);
            Assert.IsFalse(run.NotifyAllAtStageEntrance());
            Assert.IsFalse(run.NotifyAllPursuitCaught());
            Assert.AreEqual(CampaignPhase.Ending, run.Snapshot.Phase);
            Assert.AreEqual(4, run.Snapshot.Day);
            Assert.AreEqual(1, run.Snapshot.Generation);
            Assert.AreEqual(RunEndReason.Deadline, run.Snapshot.LastEnd);
        }
        [Test]
        public void StageThresholdsAndSpeedSettingsAreIndependent()
        {
            var settings = new PursuitSettings();
            Assert.AreEqual(0, settings.StageAt(59.99f));
            Assert.AreEqual(1, settings.StageAt(60));
            Assert.IsTrue(settings.Stage(120).SeeThroughWalls);
            Assert.IsTrue(settings.Stage(180).AlwaysNearest);
            settings.Stages[1].StartsAt = 15;
            settings.Stages[1].ChaseSpeedMultiplier = 1.7f;
            Assert.AreEqual(1.7f, settings.Stage(15).ChaseSpeedMultiplier);
            Assert.AreEqual(1.2f, settings.Stage(0).ChaseSpeedMultiplier);
        }
        [Test]
        public void CapturedArrivalIsInsideAssemblyInsteadOfOriginalDistantRoom()
        {
            var go = new UnityEngine.GameObject("AssemblyFixture");
            try {
                var area = go.AddComponent<UnityEngine.BoxCollider>();
                area.center = new UnityEngine.Vector3(2, 1.5f, -3); area.size = new UnityEngine.Vector3(3, 3, 5);
                for (int i = 0; i < 4; i++) {
                    var p = PursuitDirector.ArrivalLocal(area, i, 4);
                    Assert.Less(UnityEngine.Mathf.Abs(p.x - area.center.x), area.size.x * .5f);
                    Assert.AreEqual(.1f, p.y, .001f);
                    Assert.AreEqual(area.center.z, p.z);
                }
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
