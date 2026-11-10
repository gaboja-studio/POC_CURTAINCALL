using System.Reflection;
using CurtainCall.Player;
using CurtainCall.Settings;
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
