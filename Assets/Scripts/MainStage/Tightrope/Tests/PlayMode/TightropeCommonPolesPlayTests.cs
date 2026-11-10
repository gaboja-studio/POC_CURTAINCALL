using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest, Category("CommonInspectorSettings"), NUnit.Framework.Property("PoleCapacity", 4)]
        public IEnumerator CommonPoles_TrapezeAcceptsConfiguredFourRiders()
        {
            var remotes = new Component[3];
            Component common = null;
            int shared = -1;
            try
            {
                EnableTrapezeForEntryTest();
                yield return BeginPerformance(); DisableSwingHazards();
                var trapeze = (Component)Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
                common = (Component)Static("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
                yield return Until(() => (bool)Get(trapeze, "IsActive"), 2, "공중그네 준비");
                var pilot = player.GetComponent(GameType("CurtainCall.HumanCannon.HumanCannonPilot"));
                // 첫 봉은 바닥 바로 위여서 3~4번째 몸이 바닥 아래에 놓인다. 공간이 있는 틈 위 봉을 사용한다.
                Call(mover, "SetStartPose", (Vector3)Call(trapeze, "PoleFeet", 1, false), course.transform.rotation, true);
                Physics.SyncTransforms();
                Assert.IsTrue((bool)Call(pilot, "TryRequestNearbyPole"));
                Assert.AreEqual(1, Call(trapeze, "HeldPole", player));
                shared = (int)Call(common, "HeldBy", (ulong)Get(player, "OwnerClientId"));
                Assert.AreEqual(4, Get(Call(common, "State", shared), "Capacity"), "씬을 불러오기 전에 정원 적용");
                var pole = (Component)Call(common, "Pole", shared);
                var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
                for (int seat = 1; seat < 4; seat++)
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    remotes[seat - 1] = instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                    ulong actor = (ulong)(776 + seat);
                    Call(remotes[seat - 1], "SpawnWithOwnership", actor, true);
                    var remotePlayer = instance.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                    var remoteMover = instance.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                    Call(remoteMover, "SetStartPose", (Vector3)Call(pole, "HangingPosition", 0f, seat), course.transform.rotation, true);
                    Physics.SyncTransforms();
                    Assert.IsTrue((bool)Call(trapeze, "CanAttachToPole", 1, remotePlayer), "설정 정원 안의 " + (seat + 1) + "번째 사람도 접근 가능");
                    var kind = common.GetType().GetNestedType("PoleAction", BindingFlags.NonPublic);
                    var state = Call(common, "State", shared);
                    Call(common, "HandleRequest", actor, Enum.Parse(kind, "PlatformGrab"), shared,
                        (uint)Get(state, "Revision"), 0, 0u, 0u, 0f, 0u, -1d);
                    Assert.IsTrue((bool)Call(trapeze, "IsHeld", remotePlayer));
                }
                Assert.AreEqual(4, Call(common, "OccupantCount", shared));
                Assert.IsFalse((bool)Call(common, "HasFreeSeat", shared));
            }
            finally
            {
                if (common != null && shared >= 0) Call(common, "Deactivate", shared);
                foreach (var remote in remotes) if (remote != null) Call(remote, "Despawn", true);
            }
        }

        [UnityTest]
        public IEnumerator CommonPoles_GeneratedTrapezeUsesSharedGrabFlightAndDisableCleanup()
        {
            EnableTrapezeForEntryTest();
            yield return BeginPerformance(); DisableSwingHazards();
            var trapeze = (Component)Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
            var common = (Component)Static("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var flight = (Component)Static("CurtainCall.HumanCannon.HumanCannonSystem", "Current");
            Assert.IsNotNull(common); Assert.IsNotNull(flight);
            Assert.That(UnityEngine.Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonPoleSystem"), FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return Until(() => (bool)Get(trapeze, "IsActive"), 2, "공중그네 준비");
            var point = (Vector3)Call(trapeze, "PoleFeet", 0, false);
            Call(mover, "SetStartPose", point, course.transform.rotation, true);
            Physics.SyncTransforms();
            var pilot = player.GetComponent(GameType("CurtainCall.HumanCannon.HumanCannonPilot"));
            Assert.IsNotNull(pilot);
            Assert.IsTrue((bool)Call(trapeze, "CanGrabPole", 0, player), "첫 봉도 공통 거리 안이면 목마 없이 잡는다.");
            Assert.IsTrue((bool)Call(pilot, "TryRequestNearbyPole"));
            yield return Until(() => (bool)Call(trapeze, "IsHeld", player), 2, "공통 봉 잡기");
            int shared = (int)Call(common, "HeldBy", (ulong)Get(player, "OwnerClientId"));
            Assert.That((double)Call(common, "HoldUntil", shared) - (double)Get(common, "Clock"), Is.InRange(29d, 30.01d));
            Assert.That(Get(Call(common, "Pole", shared), "RiderCapacity"), Is.EqualTo(2));
            Call(pilot, "ReadInput", CatchFrame(jump: true));
            Assert.IsFalse((bool)Call(trapeze, "IsHeld", player));
            Assert.IsTrue((bool)Call(trapeze, "IsInFlight", player), "별도 외줄 비행이 아닌 공통 비행으로 이어진다.");
            Call(pilot, "ClearMotionForInspection");
            Call(common, "Restart", player);
            Call(mover, "SetStartPose", point, course.transform.rotation, true);
            Assert.IsTrue((bool)Call(pilot, "TryRequestNearbyPole"));
            yield return null;
            ((Behaviour)trapeze).enabled = false;
            yield return null;
            Assert.IsFalse((bool)Get(pilot, "IsHoldingPole"));
            Assert.IsTrue((bool)Get(mover, "Simulated"));
            Assert.IsFalse((bool)Get(Call(common, "Pole", shared), "PresentationVisible"));
        }

        [UnityTest]
        public IEnumerator CommonPoles_RelayUsesSharedSwingAndClosesOnUpperRelease()
        {
            const string shape = "{\"relayGapEnabled\":true,\"relayRequiredHolders\":1,\"trapezeEnabled\":false}";
            JsonUtility.FromJsonOverwrite(shape, legacyCourseSettings);
            JsonUtility.FromJsonOverwrite(shape, Get(course, "Shape"));
            Call(course, "Rebuild");
            yield return BeginPerformance(); DisableSwingHazards();
            var relay = (Component)Static("CurtainCall.Tightrope.TightropeRelayGate", "Current");
            var common = (Component)Static("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            yield return Until(() => (bool)Get(relay, "IsActive"), 2, "스위치 봉 준비");
            var pole = (Component)Call(relay, "PhysicalPole", 0);
            Call(mover, "SetStartPose", (Vector3)Call(pole, "HangingPosition", 0f), pole.transform.rotation, true);
            Physics.SyncTransforms();
            var pilot = player.GetComponent(GameType("CurtainCall.HumanCannon.HumanCannonPilot"));
            Assert.IsTrue((bool)Call(pilot, "TryRequestNearbyPole"));
            yield return Until(() => (bool)Call(relay, "IsHeld", player), 2, "스위치 봉 잡기");
            Assert.IsTrue((bool)Get(relay, "OutboundOpen"));
            Assert.IsFalse((bool)Call(relay, "IsUnlimitedHold", 0));
            int index = (int)Call(common, "IndexOf", pole);
            for (int frame = 0; frame < 20; frame++) { Call(pilot, "ReadInput", CatchFrame(move: 1)); yield return null; }
            Assert.That(Mathf.Abs((float)Call(common, "AngleAt", index)), Is.GreaterThan(.1f));
            Call(pilot, "ReadInput", CatchFrame(jump: true));
            Assert.IsFalse((bool)Get(relay, "OutboundOpen"), "손을 놓은 같은 프레임에 줄 연결이 꺼진다.");
            Assert.IsTrue((bool)Get(pilot, "IsFlying"));
            Call(pilot, "ClearMotionForInspection");
        }
    }
}
