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
        public IEnumerator FinaleSawAppearsEarlierOnThePlannedSurvivingRope()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var fire = (Component)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            yield return Until(() => (bool)Get(fire, "IsPerformanceActive"), 2f, "소각 계획 준비");
            Set(Get(fire, "Settings"), "chaseEnabled", false);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            float stop = ((float[])Get(settings, "cooperativeGateDistances"))[0];
            float lead = (float)Get(settings, "cooperativeGateTriggerLead");
            Assert.That(stop, Is.EqualTo(158f));
            Assert.That(lead, Is.EqualTo(24f));
            int lane = (int)Call(fire, "SurvivingLaneAt", stop);
            Assert.That(lane, Is.GreaterThanOrEqualTo(0));
            Assert.IsFalse((bool)Call(fire, "FinaleReadyAt", stop), "소각 완료 전에도 톱을 미리 보여 준다.");
            Call(saws, "ResetCooperativeGates");
            Quaternion rotation = Quaternion.LookRotation((Vector3)Get(course, "Forward"));
            Call(mover, "SetStartPose", (Vector3)Call(course, "GetLanePosition", lane, stop - lead - .1f), rotation, true);
            Call(saws, "ServerUpdateCooperativeGate");
            Assert.IsFalse((bool)Get(saws, "IsCooperativeGateActive"));
            Call(mover, "SetStartPose", (Vector3)Call(course, "GetLanePosition", lane, stop - lead + .1f), rotation, true);
            Call(saws, "ServerUpdateCooperativeGate");
            Call(saws, "UpdateViews");
            Assert.IsTrue((bool)Get(saws, "IsCooperativeGateActive"), "선두 134m에서 등장한다.");
            Assert.That((int)Get(saws, "CooperativeGateLane"), Is.EqualTo(lane));
            Vector3 bottom = (Vector3)Get(saws, "CurrentCooperativeGateBase");
            Vector3 expected = (Vector3)Call(course, "GetLanePosition", lane, stop + (float)Get(settings, "cooperativeGateApproachDistance"));
            Assert.That(Vector3.Distance(bottom, expected), Is.LessThan(.001f), "남을 줄 위 정면에서 출발한다.");
            float began = (float)Get(saws, "GameElapsed");
            float approach = (float)Get(settings, "cooperativeGateApproachDistance");
            float speed = (float)Get(settings, "cooperativeGateApproachSpeed");
            yield return new WaitForSeconds(approach / speed + .3f);
            float elapsed = (float)Get(saws, "GameElapsed") - began;
            Assert.That((float)Get(saws, "CooperativeGateDistance"), Is.EqualTo(stop + approach - elapsed * speed).Within(.001f));
            Assert.That((float)Get(saws, "CooperativeGateDistance"), Is.LessThan(stop), "158m에서도 정지하지 않는다.");
            Assert.IsFalse((bool)Get(saws, "IsCooperativeGateStopped"));
        }

        [UnityTest]
        public IEnumerator JourneyAssignsNonconsecutiveOwnersAndRejectsThreeParticipants()
        {
            yield return BeginPerformance();
            var probeType = GameType("CurtainCall.Tightrope.Tests.TightropeMultiplayerProbe");
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var currentField = probeType.GetField("current", flags);
            var originalProbe = currentField.GetValue(null);
            var devField = (System.Reflection.FieldInfo)probeType.GetField("DevCache", flags).GetValue(null);
            var originalDev = devField.GetValue(null);
            var probeObject = new GameObject("Journey roster regression probe");
            var probe = probeObject.AddComponent(probeType);
            var others = new Component[3];
            try
            {
                currentField.SetValue(null, probe);
                others[0] = CreatePracticeBotTestPartner(1, 5f, 779UL);
                Assert.IsTrue((bool)probeType.GetMethod("BeginJourney").Invoke(null, null), "0/779처럼 불연속인 소유자도 입력 여정을 시작한다.");
                CollectionAssert.AreEqual(new ulong[] { 0UL, 779UL }, (ulong[])Get(probe, "journeyOwners"));
                Assert.AreSame(others[0], Get(Get(probe, "journeyBrain"), "assignedPartner"));
                Call(probe, "RestoreJourneyDevices");
                others[1] = CreatePracticeBotTestPartner(2, 8f, 781UL);
                Assert.IsFalse((bool)probeType.GetMethod("BeginJourney").Invoke(null, null), "3인은 지원 인원으로 잘못 허용하지 않는다.");
                others[2] = CreatePracticeBotTestPartner(3, 11f, 900UL);
                Assert.IsTrue((bool)probeType.GetMethod("BeginJourney").Invoke(null, null));
                CollectionAssert.AreEqual(new ulong[] { 0UL, 779UL, 781UL, 900UL }, (ulong[])Get(probe, "journeyOwners"));
                Assert.AreSame(others[0], Get(Get(probe, "journeyBrain"), "assignedPartner"));
            }
            finally
            {
                Call(probe, "RestoreJourneyDevices");
                currentField.SetValue(null, originalProbe);
                devField.SetValue(null, originalDev);
                UnityEngine.Object.Destroy(probeObject);
                foreach (var other in others) DestroyPracticeBotTestPartner(other);
            }
        }

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

                Assert.IsTrue((bool)Call(others[0], "ServerKill"));
                Call(stack, "ServerResolveFalls");
                Assert.IsTrue((bool)Call(saws, "ServerApproveCooperativeStack"), "필수 2층 사망은 기존 승인을 해제하고 공유해야 한다.");
                Assert.IsFalse((bool)Get(saws, "IsCooperativeStackApproved"));
                Assert.IsFalse((bool)Call(saws, "ServerApproveCooperativeStack"), "혼자 남은 아래층을 승인하지 않는다.");
                others[2].transform.position = player.transform.position - (Vector3)Get(course, "Forward") * .7f;
                Call(stack, "ServerTryLink", 781UL);
                Assert.AreSame(player, Call(stack, "GetBelow", others[2]));
                Assert.IsTrue((bool)Call(saws, "ServerApproveCooperativeStack"), "남은 동료가 새 2층을 맡아 다시 승인받는다.");
                Assert.That((int)Call(saws, "CooperativeAdmittedFloor", others[2]), Is.EqualTo(2));
                DestroyPracticeBotTestPartner(others[2]);
                others[2] = null;
                Assert.IsTrue((bool)Call(saws, "ServerApproveCooperativeStack"), "필수 담당자의 접속 이탈도 오래된 승인을 해제한다.");
                Assert.IsFalse((bool)Get(saws, "IsCooperativeStackApproved"));
                Call(saws, "ResetCooperativeGates");
                Assert.That((int)Call(saws, "CooperativeAdmittedFloor", others[0]), Is.Zero);
            }
            finally { foreach (var other in others) DestroyPracticeBotTestPartner(other); }
        }

        [UnityTest]
        public IEnumerator FinaleRejectsUnapprovedForwardLaunchWithoutDetachingOrChangingBody()
        {
            yield return BeginPerformance();
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            Set(settings, "finaleFourStackRequired", true);
            float stop = (float)Get(course, "FinishDistance") - 7f;
            ConfigureStationaryGateForTest(saws, settings, stop);
            float launch = (float)Get(saws, "CooperativeJumpLaunchDistance");
            yield return StandOnRopeForSawTest(1, launch - .7f);
            var partner = CreatePracticeBotTestPartner(1, launch);
            var stack = Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
            try
            {
                Call(stack, "ServerTryLink", (ulong)Get(player, "OwnerClientId"));
                yield return Until(() => Call(stack, "GetBelow", player) == partner
                    && player.transform.position.y - partner.transform.position.y > 1.4f, 2f, "실제 목마 위층 배치");
                var follow = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.PiggybackFollow"));
                var body = (CharacterController)Get(mover, "Body");
                bool enabledBefore = body.enabled;
                float launchBefore = (float)Get(follow, "launchedAt");
                Assert.IsFalse((bool)Get(saws, "IsCooperativeStackApproved"));
                Assert.IsFalse((bool)Get(mover, "Simulated"));
                Call(follow, "Launch", player, 2.2f);
                Assert.AreSame(partner, Call(stack, "GetBelow", player), "거절된 도약은 목마를 해제하지 않는다.");
                Assert.IsFalse((bool)Get(mover, "Simulated"));
                Assert.That(body.enabled, Is.EqualTo(enabledBefore));
                Assert.That((float)Get(follow, "launchedAt"), Is.EqualTo(launchBefore));

                Vector3 earlier = (Vector3)Get(course, "Forward") * (launch - .7f - (stop - 13f));
                partner.transform.position -= earlier;
                player.transform.position -= earlier;
                Call(follow, "Launch", player, 2.2f);
                Assert.IsNull(Call(stack, "GetBelow", player), "톱이 미리 보여도 기존 12m 보호 구간 밖에서는 일반 앞점프를 유지한다.");
                Assert.IsTrue((bool)Get(mover, "Simulated"));
            }
            finally { DestroyPracticeBotTestPartner(partner); }
        }
    }
}
