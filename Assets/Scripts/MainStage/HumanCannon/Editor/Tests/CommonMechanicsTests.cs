using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CommonMechanicsTests
    {
        static object Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target,args);

        [Test]
        public void RuntimePoleNetworkIdsDoNotDependOnRegistrationOrderOrRebuildHistory()
        {
            var first = new GameObject("Same generated course"); first.SetActive(false);
            var second = new GameObject("Same generated course"); second.SetActive(false);
            try
            {
                var host = first.AddComponent<HumanCannonPoleSystem>();
                var client = second.AddComponent<HumanCannonPoleSystem>();
                HumanCannonPole Pole(GameObject parent, string name)
                { var child=new GameObject(name); child.transform.SetParent(parent.transform,false); return child.AddComponent<HumanCannonPole>(); }
                int hostA=host.Register(Pole(first,"A")), hostB=host.Register(Pole(first,"B"));
                int clientB=client.Register(Pole(second,"B")), clientA=client.Register(Pole(second,"A"));
                Assert.That(Call(client,"LocalIndex",Call(host,"WireIndex",hostA)),Is.EqualTo(clientA));
                Assert.That(Call(client,"LocalIndex",Call(host,"WireIndex",hostB)),Is.EqualTo(clientB));
                uint previous=host.State(hostA).Revision;
                Assert.That(host.Register(Pole(first,"A")),Is.EqualTo(hostA));
                Assert.That(host.PoleCount,Is.EqualTo(2),"단계 재생성이 봉 번호를 무한히 추가하지 않는다.");
                Assert.That(host.State(hostA).Revision,Is.GreaterThan(previous),"재생성 이전 메시지를 거부한다.");
                Assert.That(Call(client,"LocalIndex",int.MaxValue),Is.EqualTo(-1));
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }
        static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test, Category("CommonRulesRegression")]
        public void RemoteCannonOccupancyExcludesPiggybackWithoutAnOwnerPilot()
        {
            var current = typeof(HumanCannonSystem).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = current.GetValue(null);
            var root = new GameObject("cannon occupancy"); root.SetActive(false);
            var actor = new GameObject("remote actor without pilot"); actor.SetActive(false);
            try
            {
                var station = root.AddComponent<HumanCannonStation>();
                var cannon = root.AddComponent<HumanCannonSystem>();
                cannon.ConfigurePractice(new[] { station }, -10f);
                Call(cannon, "ResetSeats");
                current.SetValue(null, cannon);
                var player = actor.AddComponent<NetworkPlayer>();
                Assert.IsNull(actor.GetComponent<HumanCannonPilot>());
                var occupied = typeof(PiggybackSystem).GetMethod("UsingDevice", BindingFlags.Static | BindingFlags.NonPublic);
                var following = typeof(PiggybackFollow).GetMethod("IsCrossing", BindingFlags.Static | BindingFlags.NonPublic);
                var seats = (System.Array)typeof(HumanCannonSystem).GetField("seats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cannon);
                var runtime = seats.GetValue(0);
                var seat = (CannonSeatState)runtime.GetType().GetField("State").GetValue(runtime);
                Assert.IsFalse((bool)occupied.Invoke(null, new object[] { player }));
                Assert.IsTrue(seat.TryApply(CannonAction.Board, player.OwnerClientId, seat.Revision));
                Assert.IsTrue((bool)occupied.Invoke(null, new object[] { player }), "Remote seat occupancy must block a new shoulder mount.");
                Assert.IsTrue((bool)following.Invoke(null, new object[] { player }), "Seat movement must take precedence over an older piggyback snapshot.");
                Assert.IsTrue(seat.TryApply(CannonAction.Launch, player.OwnerClientId, seat.Revision));
                Call(cannon, "Apply", 0, CannonAction.Launch, player.OwnerClientId, Vector3.zero, Quaternion.identity, Vector3.up);
                Assert.IsTrue((bool)occupied.Invoke(null, new object[] { player }), "Remote cannon flight must remain mutually exclusive with piggyback.");
                Assert.IsTrue((bool)following.Invoke(null, new object[] { player }));
                Call(cannon, "Apply", 0, CannonAction.Release, player.OwnerClientId, Vector3.zero, Quaternion.identity, Vector3.zero);
                Assert.IsFalse((bool)occupied.Invoke(null, new object[] { player }));
            }
            finally
            {
                current.SetValue(null, previous);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
        }

        [Test, Category("CommonRulesRegression")]
        public void PiggybackDeviceEligibilityDoesNotGrantClientTransferAuthority()
        {
            var root = new GameObject("client piggyback query"); root.SetActive(false);
            var actor = new GameObject("client rider"); actor.SetActive(false);
            NetworkPlayer player = null;
            try
            {
                var stack = root.AddComponent<PiggybackSystem>();
                player = actor.AddComponent<NetworkPlayer>();
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
                var below = (System.Collections.Generic.Dictionary<ulong, ulong>)typeof(PiggybackSystem)
                    .GetField("belowOf", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stack);
                below[player.OwnerClientId] = 1UL;
                Assert.IsTrue((bool)Call(stack, "CanReleaseForDevice", player), "The owner can see an available pole while mounted, without host authority.");
                Assert.IsFalse((bool)Call(stack, "TryReleaseForDevice", player, null), "Only the host may mutate the shared links.");
                Assert.IsTrue(stack.IsInStack(player), "A client-side query or transfer attempt must leave the link intact.");
            }
            finally
            {
                if (player != null) typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LegacyTrampolineValuesCannotOverrideCommonBounceCarryAndCarrierRequirement()
        {
            var root = new GameObject("common trampoline"); root.SetActive(false);
            try
            {
                var trampoline = root.AddComponent<HumanCannonTrampoline>();
                Assert.IsNull(trampoline.GetType().GetField("upwardSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)); Assert.IsNull(trampoline.GetType().GetField("groundBounceAirControlSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
                Assert.IsNull(trampoline.GetType().GetField("requireCarrierForBounce", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
                Assert.That(trampoline.UpwardSpeed, Is.EqualTo(12f));
                Assert.That(trampoline.CarryMoveSpeed, Is.EqualTo(4.5f));
                Assert.IsFalse(trampoline.RequireCarrierForBounce, "마지막 운반자도 내려놓고 반동할 수 있다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LegacyStationValuesCannotOverrideCommonReachAndAirControl()
        {
            var root = new GameObject("common station"); root.SetActive(false);
            try
            {
                var station = root.AddComponent<HumanCannonStation>();
                Assert.IsNull(station.GetType().GetField("boardingDistance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)); Assert.IsNull(station.GetType().GetField("airControlSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
                Assert.That(station.BoardingDistance, Is.EqualTo(2.5f));
                Assert.That(station.AirControlSpeed, Is.EqualTo(3f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(.35f, 45f, true)]
        [TestCase(70f, 45f, false)]
        public void WalkableLandingAcceptsFlatObjectsAndRejectsSteepFaces(float angle, float slopeLimit, bool expected)
        {
            var root = new GameObject("support fixture");
            root.transform.position = new Vector3(3500,3500,3500);
            var actor = new GameObject("actor"); actor.SetActive(false);
            try
            {
                var surface = root.AddComponent<BoxCollider>(); surface.size = new Vector3(8,.2f,8);
                root.transform.rotation = Quaternion.Euler(angle,0,0);
                var mover = actor.AddComponent<PlayerMover>();
                actor.GetComponent<CharacterController>().slopeLimit = slopeLimit;
                Physics.SyncTransforms();
                Assert.That(WalkableSupport.Contains(mover, root.transform.position + Vector3.up*.1f), Is.EqualTo(expected));
                surface.isTrigger = true; Physics.SyncTransforms();
                Assert.IsFalse(WalkableSupport.Contains(mover,root.transform.position + Vector3.up*.1f));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(root); }
        }

        [Test]
        public void FloorAndRopeReadOneJumpBufferAndAirSettingsChangeAllDevices()
        {
            var input = GameSettings.Base.Input;
            var air = GameSettings.Base.AirControl;
            string savedInput = JsonUtility.ToJson(input), savedAir = JsonUtility.ToJson(air);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"jumpInputBuffer\":0.08}",input);
                JsonUtility.FromJsonOverwrite("{\"speed\":2.25}",air);
                Assert.That(GameSettings.Tightrope.RopeMovement.JumpInputBuffer, Is.EqualTo(.08f));
                Assert.That(GameSettings.Poles.AirControlSpeed, Is.EqualTo(2.25f));
                Assert.That(GameSettings.Trampoline.AirControlSpeed, Is.EqualTo(2.25f));
            }
            finally { JsonUtility.FromJsonOverwrite(savedInput,input); JsonUtility.FromJsonOverwrite(savedAir,air); }
        }
    }
}
