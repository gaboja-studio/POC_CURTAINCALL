using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 2026-11-26 Task-205(M3 피드백 7): 무대 입구 집결이 목마 3층 이상을 세지 못해 게임 시작이 2명 목마까지만 됐다.
    /// 프리팹과 같은 판정 상자(중심 y 1.5, 높이 3m)에 실제 목마 연결 사전을 넣어 확인한다. 여러 PC 플레이 검증은 아니다.
    /// </summary>
    [Category("CommonProgressionRegression")]
    public sealed class StageEntranceGatherStackTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static object Get(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);
        static void Call(object target, string name) => target.GetType().GetMethod(name, Any).Invoke(target, null);

        readonly List<GameObject> objects = new();
        readonly List<NetworkPlayer> crew = new();
        readonly Vector3 floor = new(7600, 100, 7600);
        PiggybackSystem stack;
        StageEntranceGather gather;
        object savedStack;
        bool savedSuppressed;

        GameObject Make(string name) { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }

        [SetUp]
        public void SetUp()
        {
            savedStack = typeof(PiggybackSystem).GetProperty("Current").GetValue(null);
            savedSuppressed = PiggybackSystem.Suppressed;
            PiggybackSystem.Suppressed = false;
            stack = Make("Gather stack").AddComponent<PiggybackSystem>();
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, stack);

            var area = Make("Stage entrance");
            area.transform.position = floor;
            gather = area.AddComponent<StageEntranceGather>();
            Set(gather, "showStatus", false);
            Set(gather, "areaCenter", new Vector3(0, 1.5f, 0));
            Set(gather, "areaSize", new Vector3(6, 3, 12));
            Call(gather, "Awake");
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, savedStack);
            PiggybackSystem.Suppressed = savedSuppressed;
            var all = (List<NetworkPlayer>)NetworkPlayer.All;
            foreach (var player in crew) { typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, false); all.Remove(player); }
            crew.Clear();
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        NetworkPlayer Player(ulong id, Vector3 feet)
        {
            var go = Make("Crew " + id);
            var mover = go.AddComponent<PlayerMover>(); Call(mover, "Awake");
            var player = go.AddComponent<NetworkPlayer>(); Set(player, "mover", mover);
            typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, id);
            player.transform.position = feet;
            crew.Add(player);
            ((List<NetworkPlayer>)NetworkPlayer.All).Add(player);
            return player;
        }

        NetworkPlayer[] Stack(int count, Vector3 feet)
        {
            var members = new NetworkPlayer[count];
            float height = GameSettings.Base.Character.CapsuleHeight;
            var below = (Dictionary<ulong, ulong>)Get(stack, "belowOf");
            var above = (Dictionary<ulong, ulong>)Get(stack, "aboveOf");
            for (int i = 0; i < count; i++)
            {
                members[i] = Player((ulong)(2300 + i), feet + Vector3.up * height * i);
                if (i == 0) continue;
                below[members[i].OwnerClientId] = members[i - 1].OwnerClientId;
                above[members[i - 1].OwnerClientId] = members[i].OwnerClientId;
            }
            return members;
        }

        int Inside()
        {
            Call(gather, "Count");
            return gather.InsideCount;
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void PiggybackStackInsideEntrance_CountsEveryFloor(int size)
        {
            var members = Stack(size, floor);
            Assert.That(Inside(), Is.EqualTo(size), "목마 전원이 집결로 세져야 한다");
            Assert.That(gather.AliveCount, Is.EqualTo(size));
            foreach (var member in members) Assert.IsTrue(gather.IsInside(member), "쉬는 시간 잡힘 판정도 같은 기준");
        }

        [Test]
        public void PiggybackStackOutsideEntrance_CountsNobody()
        {
            Stack(4, floor + new Vector3(20, 0, 0));
            Assert.That(Inside(), Is.EqualTo(0));
        }

        [Test]
        public void LonePlayerAboveEntrance_IsStillOutside()
        {
            // 목마가 아닌 사람은 기존처럼 발 높이로 판정한다(위층 통로·구조물 위는 집결이 아니다).
            Player(2390, floor + Vector3.up * 4.5f);
            Assert.That(Inside(), Is.EqualTo(0));
        }
    }
}
