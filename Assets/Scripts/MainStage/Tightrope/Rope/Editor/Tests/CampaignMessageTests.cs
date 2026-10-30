using System;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Results;
using CurtainCall.Reward;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    /// <summary>서로 다른 전달 채널의 런·캠페인·정산 스냅샷 도착 순서를 실제 런 수신기로 검사한다.</summary>
    public sealed class CampaignMessageTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject courseObject, sharedObject;
        TightropeRun run;
        TightropeCampaignBridge bridge;
        CampaignRun campaign;
        PerformanceRewards rewards;
        PerformanceIdentity first, next;
        int resets;

        [SetUp]
        public void SetUp()
        {
            resets = 0;
            courseObject = new GameObject("Campaign message course") { hideFlags = HideFlags.DontSave };
            run = courseObject.AddComponent<TightropeRun>();
            bridge = courseObject.AddComponent<TightropeCampaignBridge>();
            var course = courseObject.GetComponent<TightropeCourse>();
            course.Rebuild();
            Set(run, "course", course);
            sharedObject = new GameObject("Campaign message shared state") { hideFlags = HideFlags.DontSave };
            campaign = sharedObject.AddComponent<CampaignRun>();
            rewards = sharedObject.AddComponent<PerformanceRewards>();
            Property(bridge, "Campaign", campaign);
            Property(bridge, "Rewards", rewards);
            string session = Guid.NewGuid().ToString("N");
            first = new PerformanceIdentity(session, 1, 1, 0, "Tightrope");
            next = new PerformanceIdentity(session, 1, 2, 0, "Tightrope");
            run.RunRestarted += () => resets++;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(courseObject);
            Object.DestroyImmediate(sharedObject);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StartBeforeCommonSnapshot_WaitsThenInitializesExactlyOnce(bool externallyManaged)
        {
            run.SetExternallyManaged(externallyManaged);
            Receive(first.Id, TightropeRunState.Running, 7);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Waiting));
            Assert.That(run.Round, Is.Zero);
            Assert.That(resets, Is.Zero);
            Common(first, CampaignPhase.Performance, true);
            Invoke(run, "SynchronizeCampaignState");
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Running));
            Assert.That(run.Round, Is.EqualTo(7));
            Assert.That(resets, Is.EqualTo(1));
            Invoke(run, "SynchronizeCampaignState");
            Receive(first.Id, TightropeRunState.Running, 7);
            Assert.That(resets, Is.EqualTo(1), "동일 공연 스냅샷 재전송은 위치를 다시 초기화하지 않는다.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RewardOpenBeforeCampaignStep_DoesNotStartDuringIntro(bool externallyManaged)
        {
            run.SetExternallyManaged(externallyManaged);
            Common(first, CampaignPhase.Intro, true);
            Receive(first.Id, TightropeRunState.Running, 7);
            Assert.That(run.Round, Is.Zero);
            Assert.That(resets, Is.Zero);
            Common(first, CampaignPhase.Performance, true);
            Invoke(run, "SynchronizeCampaignState");
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Running));
            Assert.That(resets, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SettlementBeforeLateStart_PreservesFailureWithoutCourseReset(bool externallyManaged)
        {
            run.SetExternallyManaged(externallyManaged);
            var result = Result(first, false);
            Common(first, CampaignPhase.RestTime, false, result);
            Receive(first.Id, TightropeRunState.Running, 7);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Failed));
            Assert.That(run.Round, Is.EqualTo(7));
            Assert.That(resets, Is.Zero, "부활 뒤 도착한 시작 패킷으로 목마·위치·균형을 초기화하면 안 된다.");
            Receive(first.Id, TightropeRunState.Running, 7);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Failed));
            Assert.That(resets, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreviousPerformanceMessages_CannotInitializeOrEndNextPerformance(bool externallyManaged)
        {
            run.SetExternallyManaged(externallyManaged);
            Common(next, CampaignPhase.Performance, true);
            Receive(first.Id, TightropeRunState.Running, 7);
            Assert.That(run.Round, Is.Zero);
            Assert.That(resets, Is.Zero);
            Receive(next.Id, TightropeRunState.Running, 8);
            Assert.That(run.Round, Is.EqualTo(8));
            Assert.That(resets, Is.EqualTo(1));
            Receive(first.Id, TightropeRunState.Failed, 7);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Running));
            Assert.That(run.Round, Is.EqualTo(8));
            Assert.That(resets, Is.EqualTo(1));
        }

        [Test]
        public void TerminalPacketBeforeSettlement_WaitsForCommonOutcomeWithoutStarting()
        {
            Common(first, CampaignPhase.Performance, true);
            Receive(first.Id, TightropeRunState.Succeeded, 7);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Waiting));
            Assert.That(run.Round, Is.Zero);
            Common(first, CampaignPhase.RestTime, false, Result(first, true));
            Invoke(run, "SynchronizeCampaignState");
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Succeeded));
            Assert.That(run.Round, Is.EqualTo(7));
            Assert.That(resets, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PublicSettlementEndsRunningClientWithoutWaitingForRunTerminalPacket(bool externallyManaged)
        {
            run.SetExternallyManaged(externallyManaged);
            Common(first, CampaignPhase.Performance, true);
            Receive(first.Id, TightropeRunState.Running, 7);
            var result = Result(first, false);
            Common(first, CampaignPhase.RestTime, false, result);
            Invoke(run, "AcceptCampaignResult", result);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Failed));
            Assert.That(resets, Is.EqualTo(1));
            Receive(first.Id, TightropeRunState.Running, 7);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Failed));
            Assert.That(resets, Is.EqualTo(1));
        }

        [Test]
        public void PracticeMessage_RemainsReadableWithoutCampaignIdentity()
        {
            bridge.enabled = false;
            Receive(null, TightropeRunState.Running, 2);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Running));
            Assert.That(run.Round, Is.EqualTo(2));
            using var writer = (FastBufferWriter)Invoke(run, "WriteState");
            Assert.That(writer.Length, Is.EqualTo(33), "독립 연습도 서버 절대 시각을 공유한다.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DelayedStatePreservesAbsoluteServerEpoch(bool campaignMode)
        {
            bridge.enabled = campaignMode;
            if (campaignMode) Common(first, CampaignPhase.Performance, true);
            double sentAt = run.Clock;
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe(123UL);
            writer.WriteValueSafe((byte)TightropeRunState.Running); writer.WriteValueSafe(9);
            writer.WriteValueSafe(sentAt); writer.WriteValueSafe(-1f);
            writer.WriteValueSafe(sentAt); writer.WriteValueSafe(-1d);
            if (campaignMode) writer.WriteValueSafe(first.Id);
            using var reader = new FastBufferReader(writer, Allocator.Temp); reader.Seek(8);
            Invoke(run, "HandleStateMessage", NetworkManager.ServerClientId, reader);
            Assert.That(run.GetType().GetField("runStartedAt", Members).GetValue(run), Is.EqualTo(sentAt));
            Assert.That(run.GetType().GetField("performanceStartedAt", Members).GetValue(run), Is.EqualTo(sentAt));
            // 수신 시각으로 시작점을 다시 만들지 않는다. 이후 서버 시계가 보정돼도 같은 원점이다.
            Assert.That(run.TimeRemaining, Is.EqualTo(Mathf.Max(0f, run.TimeLimit - (float)(run.Clock - sentAt))).Within(.01f));
        }

        [Test]
        public void ExternalTerminalBeforeSettlement_UsesOnlyCurrentOpenPerformance()
        {
            run.SetExternallyManaged(true);
            Common(first, CampaignPhase.Performance, true);
            Receive(first.Id, TightropeRunState.Running, 7);
            Receive(first.Id, TightropeRunState.Succeeded, 7);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Succeeded));
            Common(next, CampaignPhase.Performance, true);
            Receive(first.Id, TightropeRunState.Succeeded, 7);
            Assert.That(resets, Is.EqualTo(1));
        }

        [Test]
        public void ExternalLateSuccessAfterSettlement_DoesNotGatherRevivedPlayers()
        {
            run.SetExternallyManaged(true);
            Common(first, CampaignPhase.RestTime, false, Result(first, true));
            Receive(first.Id, TightropeRunState.Succeeded, 7);
            Assert.That(run.State, Is.EqualTo(TightropeRunState.Succeeded));
            Assert.That(resets, Is.Zero);
        }

        void Common(PerformanceIdentity identity, CampaignPhase phase, bool open, PerformanceResult result = null)
        {
            Set(campaign, "state", new CampaignSnapshot
            {
                Generation = identity.Generation, Day = identity.Day, PerformanceIndex = identity.Index,
                TrickId = identity.TrickId, Phase = phase
            });
            Set(rewards, "state", new RewardSnapshot(identity.SessionId, identity.Generation, 1, 0, identity,
                open, RewardRules.Temporary(1), Array.Empty<RewardSegment>(), result, 0, 0, Array.Empty<FundEntry>()));
            Property(bridge, "PerformanceId", identity.Id);
            Property(bridge, "Result", result);
        }

        static PerformanceResult Result(PerformanceIdentity identity, bool succeeded) => new(identity,
            succeeded ? PerformanceFailure.None : PerformanceFailure.TrickFailed,
            succeeded ? "Completed" : "TimeExpired", Array.Empty<PerformanceParticipantResult>());

        void Receive(string performanceId, TightropeRunState state, int round)
        {
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe((byte)state);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(run.Clock - 1d);
            writer.WriteValueSafe(-1f);
            writer.WriteValueSafe(-1d);
            writer.WriteValueSafe(-1d);
            if (performanceId != null) writer.WriteValueSafe(performanceId);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Invoke(run, "HandleStateMessage", NetworkManager.ServerClientId, reader);
        }

        static object Invoke(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, Members).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        static void Property(object target, string name, object value) => target.GetType().GetProperty(name, Members).SetValue(target, value);
    }
}
