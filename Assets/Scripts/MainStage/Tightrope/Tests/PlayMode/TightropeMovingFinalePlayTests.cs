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
        public IEnumerator MovingFinalePairCanJumpAndPressTheMovingPlate() => MovingFinaleJump(false, false);

        [UnityTest]
        public IEnumerator MovingFinaleInjuredPairCanJumpWithFourParticipants() => MovingFinaleJump(true, true);

        IEnumerator MovingFinaleJump(bool injured, bool fourParticipants)
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var fire = (Component)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            yield return Until(() => (bool)Get(fire, "IsPerformanceActive"), 2f, "마지막 생존 줄 계획 준비");
            Set(Get(fire, "Settings"), "chaseEnabled", false); // 소각 계획은 유지하고 뒤쪽 추격 불만 생략한다.
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            int lane = (int)Call(fire, "SurvivingLaneAt", 158f);
            Assert.That(lane, Is.GreaterThanOrEqualTo(0));
            Assert.IsFalse((bool)Call(fire, "FinaleReadyAt", 158f));
            var others = new Component[fourParticipants ? 3 : 1];
            try
            {
                for (int i = 0; i < others.Length; i++) others[i] = CreatePracticeBotTestPartner(lane, 5f + i * 3f, (ulong)(779 + i));
                if (injured)
                {
                    Call(player, "ServerCut", 2);
                    Call(others[0], "ServerCut", 2);
                    yield return Until(() => (float)Get(mover, "JumpHeightMultiplier") < 1f
                        && (float)Get(others[0].GetComponent(GameType("CurtainCall.Player.PlayerMover")), "BodyHeight") < 1f,
                        2f, "양다리 부상 반영");
                }
                Set(settings, "cooperativeGates", true);
                Set(settings, "finaleFourStackRequired", true);
                Set(settings, "cooperativeGateDistances", new[] { 158f });
                Set(settings, "cooperativeGateApproachDistance", 6f);
                float approachTime = 6f / (float)Get(settings, "cooperativeGateApproachSpeed");
                yield return Until(() => (float)Get(saws, "GameElapsed") > approachTime, 4f, "기존 접근 구간을 재현할 게임 시계");
                Call(saws, "SetCooperativeGate", 0, true, (float)Get(saws, "GameElapsed") - approachTime, -1f);
                float launch = (float)Get(saws, "CooperativeJumpLaunchDistance");
                yield return StandOnRopeForSawTest(lane, launch - .7f);
                launch = (float)Get(saws, "CooperativeJumpLaunchDistance");
                PlaceMovingFinalePartner(others[0], lane, launch);
                var stack = Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
                Call(stack, "ServerTryLink", (ulong)Get(player, "OwnerClientId"));
                var follow = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.PiggybackFollow"));
                Call(follow, "LateUpdate");
                Assert.AreSame(others[0], Call(stack, "GetBelow", player));
                Assert.IsFalse((bool)Get(saws, "IsCooperativeGateStopped"));
                Assert.IsTrue((bool)Call(saws, "ServerApproveCooperativeStack"), "움직이는 생존 줄의 목마를 승인한다.");
                object[] assisted = { player, 0f, 0f };
                Assert.IsTrue((bool)Call(saws, "TryGetCooperativeLaunch", assisted), "정지 조건 없이 이동 발판으로 도약한다.");
                int lostBefore = Convert.ToInt32(Get(player, "LostParts"));
                int bottomLostBefore = Convert.ToInt32(Get(others[0], "LostParts"));
                Call(follow, "Launch", player, 2.2f);
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "실제 분리 도약");
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "이동 발판 쪽 실제 착지");
                string landed = $"landing={Call(course, "GetDistance", player.transform.position)}, plate={Get(saws, "CooperativePlateDistance")}, "
                    + $"y={player.transform.position.y-course.transform.position.y}, state={Get(player, "State")}, lost={Get(player, "LostParts")}, "
                    + $"netAir={Get(player, "IsAirborne")}, approved={Get(saws, "IsCooperativeStackApproved")}, floor={Call(saws, "CooperativeAdmittedFloor", player)}, "
                    + $"bottom={Get(others[0], "State")}/{Get(others[0], "LostParts")}, "
                    + $"height={assisted[1]}, speed={assisted[2]}, gravity={Get(mover, "JumpGravity")}";
                yield return Until(() => (bool)Get(saws, "IsCooperativeGatePassable"), 2f, "이동 발판을 밟아 톱을 위로 튕김: " + landed);
                Assert.That(Convert.ToInt32(Get(player, "LostParts")), Is.EqualTo(lostBefore));
                float openingPosition = (float)Get(saws, "CooperativeGateDistance");
                object[] releasedPosition = { 0, Vector3.zero };
                Assert.IsTrue((bool)Call(saws, "TryGetReleasedGatePosition", releasedPosition));
                float distance = (float)Call(course, "GetDistance", (Vector3)releasedPosition[1]);
                Assert.That(distance, Is.LessThanOrEqualTo(openingPosition + .01f), "발판 해제 때 158m로 되돌아가지 않는다.");
                object writer = Call(saws, "WriteCooperativeGate");
                byte[] bytes;
                try { bytes = (byte[])Call(writer, "ToArray"); }
                finally { ((IDisposable)writer).Dispose(); }
                Call(saws, "ResetCooperativeGates");
                ReplayCooperativeGate(saws, bytes, 0UL);
                Assert.IsTrue((bool)Call(saws, "TryGetReleasedGatePosition", releasedPosition));
                Assert.That((float)Call(course, "GetDistance", (Vector3)releasedPosition[1]), Is.EqualTo(distance).Within(.001f), "동일 패킷으로 이동 중 위치를 복구한다.");
                float deadline = Time.realtimeSinceStartup + 2f;
                do
                {
                    yield return null;
                    Assert.IsTrue((bool)Call(saws, "TryGetReleasedGatePosition", releasedPosition));
                    float next = (float)Call(course, "GetDistance", (Vector3)releasedPosition[1]);
                    Assert.That(next, Is.LessThan(distance), "도약 중에도 정면 톱은 계속 전진한다.");
                    distance = next;
                } while (!(bool)Get(saws, "AreRequiredGatesCleared") && Time.realtimeSinceStartup < deadline);
                Assert.IsTrue((bool)Get(saws, "AreRequiredGatesCleared"));
                Assert.That(Convert.ToInt32(Get(others[0], "LostParts")), Is.EqualTo(bottomLostBefore), "아래 담당자를 안전하게 넘어간다.");
                Assert.That(Get(others[0], "State").ToString(), Is.EqualTo("Normal"));
            }
            finally
            {
                foreach (var other in others) DestroyPracticeBotTestPartner(other);
                Call(saws, "ResetCooperativeGates");
            }
        }

        void PlaceMovingFinalePartner(Component partner, int lane, float distance)
        {
            Vector3 position = (Vector3)Call(course, "GetLanePosition", lane, distance);
            Quaternion rotation = Quaternion.LookRotation((Vector3)Get(course, "Forward"));
            Call(partner.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "SetStartPose", position, rotation, true);
            var replica = partner.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform"));
            Call(replica, "SetStateInternal", position, rotation, partner.transform.localScale, true);
            Set(replica, "InboundState", Get(replica, "OutboundState"));
            Call(replica, "TransformStateUpdate");
            Physics.SyncTransforms();
        }
    }
}
