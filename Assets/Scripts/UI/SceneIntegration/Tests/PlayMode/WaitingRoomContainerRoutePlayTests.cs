using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        static object ContainerDrop(int offset, string item)
        {
            var entry = Activator.CreateInstance(T("CurtainCall.WaitingRoom.ExplorationDrops+Entry"));
            ExplorationSet(entry, "uidOffset", offset); ExplorationSet(entry, "itemId", item); return entry;
        }
        static Array ContainerDrops(params object[] entries)
        {
            var array = Array.CreateInstance(T("CurtainCall.WaitingRoom.ExplorationDrops+Entry"), entries.Length);
            for (int i = 0; i < entries.Length; i++) array.SetValue(entries[i], i); return array;
        }
        [UnityTest]
        public IEnumerator M2_ContainerRoute_EightBoxesEmptyMapAndPendingReward()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            DisablePlacedRoutesForFixture();
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var at = player.transform.position + Vector3.forward * .5f;
            var owner = new GameObject("stage4-containers"); owner.SetActive(false);
            var route = owner.AddComponent(T("CurtainCall.WaitingRoom.StructureAContainerRoute"));
            var objects = new List<Component>(); var boxes = new List<Component>();
            string[] fields = { "dormitoryBox1", "dormitoryBox2", "pipeBox1", "cargoBox1", "cargoBox2", "cargoBox3", "loungeBox1", "loungeBox2" };
            string[] ids = { "dormitory-box-1", "dormitory-box-2", "pipe-box-1", "cargo-box-1", "cargo-box-2", "cargo-box-3", "lounge-box-1", "lounge-box-2" };
            for (int i = 0; i < fields.Length; i++)
            {
                var box = ExplorationTestGate("unbound-" + fields[i], "Breakable", at);
                ExplorationSet(box, "requiredHits", 2); ExplorationSet(box, "toolId", "stake-puller");
                var drops = box.gameObject.AddComponent(T("CurtainCall.WaitingRoom.ExplorationDrops"));
                ExplorationSet(drops, "entries", ContainerDrops(ContainerDrop(80 + i, "uv-lamp")));
                ExplorationSet(route, fields[i], box); objects.Add(box); boxes.Add(box);
            }
            Component Cabinet(string field, int offset)
            {
                var go = new GameObject(field); go.SetActive(false); go.transform.position = at;
                var target = go.AddComponent(T("CurtainCall.WaitingRoom.ToolTarget"));
                ExplorationSet(target, "rewardItemId", "stake-puller"); ExplorationSet(target, "rewardUidOffset", offset);
                ExplorationSet(route, field, target); objects.Add(target); return target;
            }
            var empty = Cabinet("emptyCabinet", 95); var mapCabinet = Cabinet("mapCabinet", 96); var pending = Cabinet("lockedBox2", 97);
            var mapObject = new GameObject("map-test"); mapObject.SetActive(false); mapObject.transform.position = at;
            var map = mapObject.AddComponent(T("CurtainCall.WaitingRoom.ClueInspectable")); ExplorationSet(route, "mapClue", map);
            int basis = (int)T("CurtainCall.WaitingRoom.ItemUses").GetField("FoundUidBase").GetRawConstantValue() + (int)Field(State, "Generation") * 100;
            ulong me = (ulong)Get(carry, "LocalClientId");
            IEnumerator Use(string id) { yield return new WaitForSecondsRealtime(.12f); Call(state, "RequestUse", id); }
            IEnumerator Pick(string item, int offset, int uses)
            {
                var value = Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"), basis + offset, item, item, uses);
                Call(carry, "PlaceFound", value, player.transform.position); Call(carry, "RequestPickUp", basis + offset);
                yield return null; Assert.AreEqual(basis + offset, Get(Call(carry, "Selected", me), "Uid"));
            }
            try
            {
                owner.SetActive(true); foreach (var target in objects) target.gameObject.SetActive(true);
                Assert.IsFalse(mapObject.activeSelf); Assert.AreEqual(false, Get(map, "Revealed"));
                Call(map, "SetOpen", true); Assert.AreEqual(false, Get(map, "IsOpen"));
                yield return Pick("stake-puller", 89, 8);
                for (int i = 0; i < boxes.Count; i++)
                {
                    string id = "structure-a:" + ids[i]; Assert.AreEqual(id, Get(boxes[i], "TargetId"));
                    yield return Use(id); Assert.AreEqual(false, Get(boxes[i], "Opened"));
                    Assert.AreEqual(8 - i, Get(Call(carry, "Selected", me), "Uses"));
                    Assert.AreEqual(false, Call(carry, "Exists", basis + 80 + i));
                    yield return Use(id); Assert.AreEqual(true, Get(boxes[i], "Opened"));
                    if (i < 7) Assert.AreEqual(7 - i, Get(Call(carry, "Selected", me), "Uses"));
                    Assert.AreEqual(true, Call(carry, "Exists", basis + 80 + i));
                }
                Assert.AreEqual(false, Call(carry, "Exists", basis + 89));
                yield return Pick("magician-hairpin", 88, 3);
                foreach (var cabinet in new[] { empty, mapCabinet, pending })
                {
                    string id = (string)Get(cabinet, "TargetId");
                    for (int attempt = 0; attempt < 3 && !(bool)Call(state, "IsOpen", id); attempt++) yield return Use(id);
                    Assert.AreEqual(true, Call(state, "IsOpen", id));
                }
                yield return new WaitForSecondsRealtime(.3f);
                for (int offset = 95; offset <= 97; offset++) Assert.AreEqual(false, Call(carry, "Exists", basis + offset), "빈/지도/미정은 fallback 기물을 지급하지 않음");
                Assert.IsTrue(mapObject.activeSelf); Assert.AreEqual(true, Get(map, "Revealed"));
                Call(map, "SetOpen", true); yield return null;
                Assert.AreEqual(true, Get(map, "IsOpen")); Assert.AreEqual(true, Get(map, "Discovered")); Call(map, "SetOpen", false);
                var rewards = pending.GetComponent(T("CurtainCall.WaitingRoom.ExplorationDrops"));
                Assert.AreEqual(true, Get(rewards, "Pending"));
                var configured = new object[] { ContainerDrops(ContainerDrop(97, "uv-lamp")), null };
                Assert.AreEqual(true, rewards.GetType().GetMethod("TryConfigure").Invoke(rewards, configured));
                yield return new WaitForSecondsRealtime(.3f); Assert.AreEqual(true, Call(carry, "Exists", basis + 97));
                Call(carry, "RequestPickUp", basis + 97); int uses = (int)Get(Call(carry, "Selected", me), "Uses");
                for (int i = 0; i < uses; i++) carry.GetType().GetMethod("UseItem").Invoke(carry, new object[] { me, basis + 97, null, null });
                yield return new WaitForSecondsRealtime(.2f); Assert.AreEqual(false, Call(carry, "Exists", basis + 97), "미정 보상 확정 후에도 소모품 재지급 없음");
                // Duplicate reference is rejected before identities or rewards are changed.
                ExplorationSet(route, "cargoBox1", boxes[0]);
                var apply = new object[] { null }; Assert.AreEqual(false, route.GetType().GetMethod("TryApply").Invoke(route, apply));
                Assert.AreEqual("structure-a:dormitory-box-1", Get(boxes[0], "TargetId"));
            }
            finally { foreach (var target in objects) if (target != null) Object.Destroy(target.gameObject); Object.Destroy(mapObject); Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator M2_ContainerRoute_InvalidDropConfigurationIsAtomic()
        {
            var go = new GameObject("drop-config-test");
            var drops = go.AddComponent(T("CurtainCall.WaitingRoom.ExplorationDrops"));
            try
            {
                Call(drops, "ConfigurePending");
                var invalid = new object[] { ContainerDrops(ContainerDrop(94, "uv-lamp"), ContainerDrop(94, "magician-hairpin")), null };
                Assert.AreEqual(false, drops.GetType().GetMethod("TryConfigure").Invoke(drops, invalid));
                Assert.AreEqual(true, Get(drops, "Pending")); Assert.IsNotEmpty((string)invalid[1]);
                var valid = new object[] { ContainerDrops(ContainerDrop(94, "uv-lamp"), ContainerDrop(95, "magician-hairpin")), null };
                Assert.AreEqual(true, drops.GetType().GetMethod("TryConfigure").Invoke(drops, valid));
                Assert.AreEqual(false, Get(drops, "Pending"));
            }
            finally { Object.Destroy(go); }
            yield return null;
        }
        [UnityTest]
        public IEnumerator M2_ContainerRoute_SharedUidWaitsForCorrection()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            DisablePlacedRoutesForFixture();
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var a = ExplorationTestGate("stage4-uid-a", "Open", player.transform.position + Vector3.forward * .5f);
            var b = ExplorationTestGate("stage4-uid-b", "Open", a.transform.position);
            var da = a.gameObject.AddComponent(T("CurtainCall.WaitingRoom.ExplorationDrops"));
            var db = b.gameObject.AddComponent(T("CurtainCall.WaitingRoom.ExplorationDrops"));
            ExplorationSet(da, "entries", ContainerDrops(ContainerDrop(94, "uv-lamp")));
            ExplorationSet(db, "entries", ContainerDrops(ContainerDrop(94, "magician-hairpin")));
            int basis = (int)T("CurtainCall.WaitingRoom.ItemUses").GetField("FoundUidBase").GetRawConstantValue() + (int)Field(State, "Generation") * 100;
            try
            {
                a.gameObject.SetActive(true); b.gameObject.SetActive(true);
                Call(state, "RequestUse", "stage4-uid-a"); yield return new WaitForSecondsRealtime(.15f);
                Call(state, "RequestUse", "stage4-uid-b"); yield return new WaitForSecondsRealtime(.3f);
                Assert.AreEqual(false, Call(carry, "Exists", basis + 94), "다른 상자의 UID 충돌도 지급 완료로 오인하지 않음");
                var corrected = new object[] { ContainerDrops(ContainerDrop(95, "magician-hairpin")), null };
                Assert.AreEqual(true, db.GetType().GetMethod("TryConfigure").Invoke(db, corrected));
                yield return new WaitForSecondsRealtime(.3f);
                Assert.AreEqual(true, Call(carry, "Exists", basis + 94)); Assert.AreEqual(true, Call(carry, "Exists", basis + 95));
            }
            finally { Object.Destroy(a.gameObject); Object.Destroy(b.gameObject); }
        }
    }
}
