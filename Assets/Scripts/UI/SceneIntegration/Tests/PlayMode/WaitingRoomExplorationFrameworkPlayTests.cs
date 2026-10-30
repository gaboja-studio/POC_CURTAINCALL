using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        // Canonical IDs also exist in the saved map now. Isolated route fixtures own those IDs during their tests.
        static void DisablePlacedRoutesForFixture()
        {
            var root = GameObject.Find("WaitingRoom_LayoutA"); if (root == null) return;
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                var id = component.GetType().GetProperty("TargetId")?.GetValue(component) as string;
                if (component.GetType().Name.StartsWith("StructureA") || (id != null && id.StartsWith("structure-a:"))) component.enabled = false;
            }
        }
        static void ExplorationSet(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);

        static Component ExplorationTestGate(string id, string kind, Vector3 at)
        {
            var go = new GameObject(id); go.SetActive(false); go.transform.position = at;
            var gate = go.AddComponent(T("CurtainCall.WaitingRoom.ExplorationGate"));
            ExplorationSet(gate, "targetId", id);
            ExplorationSet(gate, "kind", Enum.Parse(T("CurtainCall.WaitingRoom.ExplorationGateKind"), kind));
            return gate;
        }

        [UnityTest]
        public IEnumerator M2_ExplorationFramework_ConditionsCodeMonitorAndDrops()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var point = player.transform.position + Vector3.forward * .5f;
            var key = ExplorationTestGate("test-key", "Open", point);
            var pad = ExplorationTestGate("test-pad", "Keypad", point);
            var door = ExplorationTestGate("test-door", "Open", point);
            var monitor = ExplorationTestGate("test-monitor", "Monitor", point);
            try
            {
                ExplorationSet(pad, "password", "0123");
                ExplorationSet(door, "requiredTargets", new[] { "test-key", "test-pad" });
                ExplorationSet(door, "revealTarget", "test-monitor");
                var blocker = door.gameObject.AddComponent<BoxCollider>(); blocker.isTrigger = true;
                ExplorationSet(door, "blocker", blocker);
                // Empty reward override: a configured fallback item must not appear.
                ExplorationSet(key, "rewardItemId", "stake-puller");
                ExplorationSet(key, "rewardUidOffset", 96);
                key.gameObject.AddComponent(T("CurtainCall.WaitingRoom.ExplorationDrops"));
                ExplorationSet(door, "rewardItemId", "stake-puller");
                ExplorationSet(door, "rewardUidOffset", 97);
                foreach (var target in new[] { key, pad, door, monitor }) target.gameObject.SetActive(true);
                yield return null;
                Assert.AreEqual(false, Get(door, "Visible"));
                Call(state, "RequestUse", "test-door");
                Assert.AreEqual(false, Call(state, "IsOpen", "test-door"));
                Call(state, "RequestCode", "test-pad", "1234");
                Assert.AreEqual(false, Call(state, "IsOpen", "test-pad"));
                yield return new WaitForSecondsRealtime(.12f);
                Call(state, "RequestCode", "test-pad", "0123");
                Assert.AreEqual(true, Call(state, "IsOpen", "test-pad"));
                yield return new WaitForSecondsRealtime(.12f);
                Call(key, "Interact");
                Assert.AreEqual(true, Call(state, "IsOpen", "test-key"));
                yield return new WaitForSecondsRealtime(.12f);
                Call(monitor, "Interact");
                Assert.AreEqual(true, Call(state, "IsOpen", "test-monitor"));
                Assert.AreEqual(true, Static("CurtainCall.Player.InputGate", "IsRequested"));
                monitor.gameObject.SetActive(false);
                Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));
                yield return new WaitForSecondsRealtime(.12f);
                Assert.AreEqual(true, Get(door, "ConditionsMet"));
                Call(door, "Interact");
                Assert.AreEqual(true, Call(state, "IsOpen", "test-door"));
                yield return new WaitForSecondsRealtime(.8f);
                Assert.IsFalse(blocker.enabled);
                int generation = (int)Field(State, "Generation");
                int basis = (int)T("CurtainCall.WaitingRoom.ItemUses").GetField("FoundUidBase").GetRawConstantValue();
                Assert.AreEqual(false, Call(carry, "Exists", basis + generation * 100 + 96), "빈 보상은 fallback을 지급하지 않음");
                int uid = basis + generation * 100 + 97;
                Assert.AreEqual(true, Call(carry, "Exists", uid));
                // Pick up and exhaust the reward, then verify the opened object never re-seeds it.
                Call(carry, "RequestPickUp", uid);
                ulong me = (ulong)Get(carry, "LocalClientId");
                int uses = (int)Get(Call(carry, "Selected", me), "Uses");
                var use = carry.GetType().GetMethod("UseItem");
                for (int i = 0; i < uses; i++) use.Invoke(carry, new object[] { me, uid, null, null });
                Assert.AreEqual(false, Call(carry, "Exists", uid));
                Call(state, "RequestUse", "test-door");
                yield return new WaitForSecondsRealtime(.3f);
                Assert.AreEqual(false, Call(carry, "Exists", uid), "사용 완료된 드랍은 재지급하지 않음");
            }
            finally
            {
                foreach (var target in new[] { key, pad, door, monitor }) if (target != null) Object.Destroy(target.gameObject);
            }
        }
        [UnityTest]
        public IEnumerator M2_ExplorationRoute_TwoKeysAndPadInEitherOrder()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            for (int order = 0; order < 2; order++)
            {
                string prefix = "route-order-" + order;
                var at = player.transform.position + Vector3.forward * .5f;
                var first = ExplorationTestGate(prefix + "-key1", "Open", at);
                var second = ExplorationTestGate(prefix + "-key2", "Open", at);
                var pad = ExplorationTestGate(prefix + "-pad", "Keypad", at);
                var door = ExplorationTestGate(prefix + "-door", "Cooperative", at);
                try
                {
                    ExplorationSet(pad, "password", "0123");
                    ExplorationSet(door, "requiredTargets", new[] { prefix + "-key1", prefix + "-key2", prefix + "-pad" });
                    foreach (var target in new[] { first, second, pad, door }) target.gameObject.SetActive(true);
                    var sequence = order == 0 ? new[] { first, pad, second } : new[] { second, first, pad };
                    for (int i = 0; i < sequence.Length; i++)
                    {
                        yield return new WaitForSecondsRealtime(.12f);
                        string id = (string)Get(sequence[i], "TargetId");
                        if (sequence[i] == pad) Call(state, "RequestCode", id, "0123");
                        else Call(state, "RequestUse", id);
                        Assert.AreEqual(true, Call(state, "IsOpen", id));
                        Assert.AreEqual(i == 2, Get(door, "ConditionsMet"), "조건은 획득 순서와 무관하게 모두 필요");
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        yield return new WaitForSecondsRealtime(.12f);
                        Call(door, "Interact");
                        Assert.AreEqual(false, Get(door, "Opened"), "열쇠와 패드가 있어도 한 사람이 연타하면 열리지 않음");
                    }
                }
                finally { foreach (var target in new[] { first, second, pad, door }) if (target != null) Object.Destroy(target.gameObject); }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator M2_ExplorationBreakable_MultipleDropsOnlyAfterLastHit()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var target = ExplorationTestGate("test-breakable-drops", "Breakable", player.transform.position + Vector3.forward * .5f);
            int generation = (int)Field(State, "Generation");
            int basis = (int)T("CurtainCall.WaitingRoom.ItemUses").GetField("FoundUidBase").GetRawConstantValue() + generation * 100;
            try
            {
                ExplorationSet(target, "toolId", "stake-puller");
                ExplorationSet(target, "requiredHits", 3);
                var drops = target.gameObject.AddComponent(T("CurtainCall.WaitingRoom.ExplorationDrops"));
                var entryType = T("CurtainCall.WaitingRoom.ExplorationDrops+Entry");
                var entries = Array.CreateInstance(entryType, 2);
                for (int i = 0; i < 2; i++)
                {
                    var entry = Activator.CreateInstance(entryType);
                    ExplorationSet(entry, "itemId", i == 0 ? "magician-hairpin" : "uv-lamp");
                    ExplorationSet(entry, "uidOffset", 94 + i);
                    entries.SetValue(entry, i);
                }
                ExplorationSet(drops, "entries", entries);
                target.gameObject.SetActive(true);
                var tool = Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"), basis + 93, "stake-puller", "test-tool", 2);
                Call(carry, "PlaceFound", tool, player.transform.position);
                Call(carry, "RequestPickUp", basis + 93);
                ulong me = (ulong)Get(carry, "LocalClientId");
                Assert.AreEqual(basis + 93, Get(Call(carry, "Selected", me), "Uid"));
                for (int hit = 1; hit <= 3; hit++)
                {
                    yield return new WaitForSecondsRealtime(.12f);
                    Call(target, "Interact");
                    Assert.AreEqual(hit, Call(state, "Hits", "test-breakable-drops"));
                    Assert.AreEqual(hit == 3 ? 1 : 2, Get(Call(carry, "Selected", me), "Uses"));
                    if (hit < 3)
                        for (int offset = 94; offset <= 95; offset++) Assert.AreEqual(false, Call(carry, "Exists", basis + offset));
                }
                yield return new WaitForSecondsRealtime(.7f);
                for (int offset = 94; offset <= 95; offset++) Assert.AreEqual(true, Call(carry, "Exists", basis + offset));
                Call(state, "RequestUse", "test-breakable-drops");
                Assert.AreEqual(1, Get(Call(carry, "Selected", me), "Uses"), "완료된 상자는 재차감하지 않음");
            }
            finally { if (target != null) Object.Destroy(target.gameObject); }
        }
        [UnityTest]
        public IEnumerator M2_StructureAKeyRoute_UnlockPickupAndToolChain()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            DisablePlacedRoutesForFixture();
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var at = player.transform.position + Vector3.forward * .5f;
            var owner = new GameObject("stage3-route-test"); owner.SetActive(false);
            var route = owner.AddComponent(T("CurtainCall.WaitingRoom.StructureAKeyRoute"));
            var routeType = route.GetType();
            string Id(string name) => (string)routeType.GetField(name).GetRawConstantValue();
            var objects = new System.Collections.Generic.List<Component>();
            Component Gate(string field, string kind = "Open")
            {
                var target = ExplorationTestGate("before-" + field, kind, at);
                objects.Add(target); ExplorationSet(route, field, target); return target;
            }
            Component Tool(string field, string kind)
            {
                var go = new GameObject(field); go.SetActive(false); go.transform.position = at;
                var target = go.AddComponent(T("CurtainCall.WaitingRoom.ToolTarget"));
                ExplorationSet(target, "kind", Enum.Parse(T("CurtainCall.WaitingRoom.ToolTargetKind"), kind));
                objects.Add(target); ExplorationSet(route, field, target); return target;
            }
            var key1 = Gate("key1"); var box = Gate("steelBox"); var managementKey = Gate("managementKey");
            var locked = Tool("lockedBox1", "HairpinLock"); var stairsKey = Gate("stairsKey"); var stairsDoor = Gate("stairsDoor");
            var cabinet = Tool("cabinet3", "HairpinLock"); var drawer = Tool("managementDrawer", "Pry");
            var escape1 = Gate("escapeKey1"); var escape2 = Gate("escapeKey2"); var exit = Gate("escapeDoor", "Cooperative");
            ExplorationSet(exit, "requiredTargets", new[] { "stage3-pad" });
            var pad = ExplorationTestGate("stage3-pad", "Open", at); objects.Add(pad);
            var floorObject = new GameObject("stage3-conditional-tool"); floorObject.SetActive(false); floorObject.transform.position = at;
            var spot = floorObject.AddComponent(T("CurtainCall.WaitingRoom.FoundItemSpot"));
            ExplorationSet(spot, "spotId", "stage3-conditional-tool"); ExplorationSet(spot, "uidOffset", 92);
            Call(spot, "UnlockAfter", Id("LockedBox1"));
            int basis = (int)T("CurtainCall.WaitingRoom.ItemUses").GetField("FoundUidBase").GetRawConstantValue() + (int)Field(State, "Generation") * 100;
            ulong me = (ulong)Get(carry, "LocalClientId");
            IEnumerator Use(string id)
            { yield return new WaitForSecondsRealtime(.12f); Call(state, "RequestUse", id); }
            IEnumerator PickTool(string item, int offset, int uses)
            {
                var value = Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"), basis + offset, item, item, uses);
                Call(carry, "PlaceFound", value, player.transform.position); Call(carry, "RequestPickUp", basis + offset);
                yield return null; Assert.AreEqual(basis + offset, Get(Call(carry, "Selected", me), "Uid"));
            }
            try
            {
                owner.SetActive(true);
                foreach (var target in objects) target.gameObject.SetActive(true);
                floorObject.SetActive(true);
                yield return new WaitForSecondsRealtime(.7f);
                Assert.AreEqual(false, Get(spot, "CanSeed")); Assert.AreEqual(false, Call(carry, "Exists", basis + 92));
                yield return Use(Id("SteelBox")); Assert.AreEqual(false, Get(box, "Opened"));
                yield return Use(Id("ManagementKey")); Assert.AreEqual(false, Get(managementKey, "Opened"));
                yield return Use(Id("StairsDoor")); Assert.AreEqual(false, Get(stairsDoor, "Opened"));
                yield return Use(Id("Key1")); yield return Use(Id("SteelBox"));
                Assert.AreEqual(true, Get(box, "Opened")); Assert.AreEqual(true, Get(managementKey, "Visible"));
                Assert.AreEqual(false, Get(managementKey, "Opened"), "상자 개방과 열쇠 획득은 별도 F");
                yield return Use(Id("ManagementKey")); Assert.AreEqual(true, Get(managementKey, "Opened"));
                yield return PickTool("magician-hairpin", 90, 2);
                foreach (var target in new[] { locked, cabinet })
                {
                    string id = (string)Get(target, "TargetId");
                    for (int attempt = 0; attempt < 3 && !(bool)Call(state, "IsOpen", id); attempt++) yield return Use(id);
                    Assert.AreEqual(true, Call(state, "IsOpen", id));
                }
                Assert.AreEqual(false, Call(carry, "Exists", basis + 90), "머리핀은 두 개방 성공 뒤 소진");
                yield return Use(Id("StairsKey")); yield return Use(Id("StairsDoor"));
                Assert.AreEqual(true, Get(stairsDoor, "Opened"));
                yield return Use(Id("EscapeKey1"));
                yield return PickTool("stake-puller", 91, 1);
                yield return Use(Id("ManagementDrawer"));
                Assert.AreEqual(false, Call(carry, "Exists", basis + 91));
                Assert.AreEqual(true, Get(escape2, "Visible"));
                Assert.AreEqual(false, Get(escape2, "Opened")); yield return Use(Id("EscapeKey2"));
                Assert.AreEqual(false, Get(exit, "ConditionsMet"), "경로 연결이 기존 패드 조건을 지우지 않음");
                yield return Use("stage3-pad"); Assert.AreEqual(true, Get(exit, "ConditionsMet"));
                yield return Use(Id("EscapeDoor")); Assert.AreEqual(false, Get(exit, "Opened"), "두 열쇠를 얻어도 2인 협력은 유지");
                Assert.AreEqual(true, Call(carry, "Exists", basis + 92), "개방 후에만 도구 생성");
                floorObject.SetActive(false); floorObject.SetActive(true); yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(1, ((IEnumerable)Get(carry, "Floor")).Cast<object>().Count(x => (int)Get(Get(x, "Item"), "Uid") == basis + 92));
                ExplorationSet(route, "stairsDoor", box);
                var args = new object[] { null };
                Assert.AreEqual(false, routeType.GetMethod("TryApply").Invoke(route, args));
                Assert.AreEqual(Id("SteelBox"), Get(box, "TargetId"), "중복 참조 시 기존 경로를 부분 변경하지 않음");
            }
            finally
            {
                foreach (var target in objects) if (target != null) Object.Destroy(target.gameObject);
                Object.Destroy(floorObject); Object.Destroy(owner);
            }
        }
    }
}
