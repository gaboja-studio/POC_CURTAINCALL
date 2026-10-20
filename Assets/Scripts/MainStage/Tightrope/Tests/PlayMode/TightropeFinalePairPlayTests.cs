using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator FinaleAcceptsPairWithFourParticipantsAndPreservesGrowingStackSnapshot()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var fire = (Component)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            yield return Until(() => (bool)Get(fire, "IsPerformanceActive"), 2f, "화재 회차와 소각 계획 준비");
            Set(Get(fire, "Settings"), "chaseEnabled", false);
            var settings = Get(saws, "Settings");
            Set(settings, "finaleFourStackRequired", true);
            float stop = (float)Get(course, "FinishDistance") - 7f;
            int lane = (int)Call(fire, "SurvivingLaneAt", stop);
            Assert.That(lane, Is.GreaterThanOrEqualTo(0));
            for (int i = 0; i < (int)Get(course, "LaneCount"); i++)
                if (i != lane) Call(course, "BlockSegment", i, stop - 10f, stop + 5f, false);
            Assert.IsTrue((bool)Call(fire, "FinaleReadyAt", stop));
            ConfigureStationaryGateForTest(saws, settings, stop);
            Set(saws, "cooperativeGateLane", lane);
            float launch = (float)Get(saws, "CooperativeJumpLaunchDistance");
            yield return StandOnRopeForSawTest(lane, launch);
            var others = new Component[3];
            var stack = Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
            try
            {
                for (int i = 0; i < 3; i++) others[i] = CreatePracticeBotTestPartner(lane, 5f + i * 3f, (ulong)(779 + i));
                Assert.That((int)Get(saws, "CooperativeRequiredStackSize"), Is.EqualTo(2));
                Assert.IsFalse((bool)Call(saws, "ServerApproveCooperativeStack"), "One player must not unlock the finale.");
                for (int count = 2; count <= 4; count++)
                {
                    others[count - 2].transform.position = player.transform.position - (Vector3)Get(course, "Forward") * .7f;
                    Call(stack, "ServerTryLink", (ulong)(777 + count));
                    Assert.That((int)Call(stack, "GetStackSize", player), Is.EqualTo(count));
                    Assert.IsTrue((bool)Call(saws, "ServerApproveCooperativeStack"),
                        "Two are sufficient even with four participants; later members may join before launch.");
                    Assert.IsTrue((bool)Get(saws, "IsCooperativeStackApproved"));
                    Assert.That((int)Call(saws, "CooperativeAdmittedFloor", others[count - 2]), Is.EqualTo(count));
                    object writer = Call(saws, "WriteCooperativeGate");
                    byte[] snapshot;
                    try { snapshot = (byte[])Call(writer, "ToArray"); }
                    finally { ((IDisposable)writer).Dispose(); }
                    Call(saws, "ResetCooperativeAdmission");
                    var invalid = (byte[])snapshot.Clone();
                    Array.Copy(BitConverter.GetBytes(1), 0, invalid, 37, 4);
                    ReplayCooperativeGate(saws, invalid, 0UL);
                    Assert.IsFalse((bool)Get(saws, "IsCooperativeStackApproved"), "Reject a one-person approval packet.");
                    ReplayCooperativeGate(saws, snapshot, 777UL);
                    Assert.IsFalse((bool)Get(saws, "IsCooperativeStackApproved"), "Only the host may approve.");
                    ReplayCooperativeGate(saws, snapshot, 0UL);
                    Assert.IsTrue((bool)Get(saws, "IsCooperativeStackApproved"));
                    Assert.That((int)Call(saws, "CooperativeAdmittedFloor", others[count - 2]), Is.EqualTo(count));
                }
                Call(stack, "ServerTryUnlink", 781UL);
                Assert.IsFalse((bool)Call(saws, "ServerApproveCooperativeStack"), "A departure must not overwrite the original roster.");
                Assert.That((int)Call(saws, "CooperativeAdmittedFloor", others[2]), Is.EqualTo(4));
                Call(saws, "ResetCooperativeGates");
                Assert.That((int)Call(saws, "CooperativeAdmittedFloor", others[0]), Is.Zero);
            }
            finally { foreach (var other in others) DestroyPracticeBotTestPartner(other); }
        }
    }
}
