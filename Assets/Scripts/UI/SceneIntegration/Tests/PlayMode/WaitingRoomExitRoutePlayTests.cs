using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        static bool ApplyExitRoute(Component route) => (bool)route.GetType().GetMethod("TryApply").Invoke(route, new object[] { null });
        static Collider ExitTestBlocker(Component gate)
        {
            var collider = gate.gameObject.AddComponent<BoxCollider>(); collider.isTrigger = true;
            ExplorationSet(gate, "blocker", collider); return collider;
        }
        [UnityTest]
        public IEnumerator M2_ExitRoute_PendingValuesKeysPadAndSoloCooperation()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            DisablePlacedRoutesForFixture();
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var at = player.transform.position + Vector3.forward * .5f;
            var owner = new GameObject("stage6-route"); owner.SetActive(false);
            var route = owner.AddComponent(T("CurtainCall.WaitingRoom.StructureAExitRoute"));
            string Id(string name) => (string)route.GetType().GetField(name).GetRawConstantValue();
            string Key(string name) => (string)T("CurtainCall.WaitingRoom.StructureAKeyRoute").GetField(name).GetRawConstantValue();
            var key2 = ExplorationTestGate("before-machine-key", "Open", at);
            var machine = ExplorationTestGate("before-machine-door", "Open", at);
            var pad = ExplorationTestGate("before-exit-pad", "Keypad", at);
            var exit = ExplorationTestGate("before-exit-door", "Cooperative", at);
            var escapeKey1 = ExplorationTestGate(Key("EscapeKey1"), "Open", at);
            var escapeKey2 = ExplorationTestGate(Key("EscapeKey2"), "Open", at);
            var reveal = ExplorationTestGate("fixture-machine-reveal", "Open", at);
            var extra = ExplorationTestGate("fixture-extra-exit-lock", "Open", at);
            var all = new[] { key2, machine, pad, exit, escapeKey1, escapeKey2, reveal, extra };
            ExplorationSet(route, "key2", key2); ExplorationSet(route, "machineDoor", machine);
            ExplorationSet(route, "passwordPad", pad); ExplorationSet(route, "escapeDoor", exit);
            var wall = Find("CurtainCall.WaitingRoom.BreakableWall"); Assert.IsNotNull(wall);
            ExplorationSet(route, "restroomBreach", wall);
            string oldWallId = (string)Get(wall, "TargetId");
            ExplorationSet(exit, "requiredTargets", new[] { "fixture-extra-exit-lock" });
            var machineBlock = ExitTestBlocker(machine); var exitBlock = ExitTestBlocker(exit);
            var visuals = new GameObject("hidden-door-visual"); visuals.transform.SetParent(machine.transform, false);
            ExplorationSet(machine, "hiddenVisuals", new[] { visuals });
            IEnumerator Use(string id) { yield return new WaitForSecondsRealtime(.12f); Call(state, "RequestUse", id); }
            try
            {
                owner.SetActive(true); foreach (var item in all) item.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(true, Get(pad, "PasswordPending")); Assert.AreEqual(false, Call(pad, "MatchesPassword", "0000"));
                Call(state, "RequestCode", Id("PasswordPad"), "0000"); Assert.AreEqual(false, Get(pad, "Opened"));
                Assert.AreEqual(false, Get(machine, "Visible")); Assert.IsFalse(visuals.activeSelf); Assert.IsTrue(machineBlock.enabled);
                yield return Use(Id("Key2")); yield return Use(Id("MachineDoor"));
                Assert.AreEqual(false, Get(machine, "Opened"), "열쇠2가 있어도 미정 공개 조건을 임의 해제하지 않음");
                ExplorationSet(route, "escapePassword", "0123"); ExplorationSet(route, "machineDoorRevealCondition", "fixture-machine-reveal");
                Assert.IsTrue(ApplyExitRoute(route));
                yield return Use("fixture-machine-reveal"); yield return Use(Id("MachineDoor")); yield return null;
                Assert.AreEqual(true, Get(machine, "Opened")); Assert.IsTrue(visuals.activeSelf); Assert.IsFalse(machineBlock.enabled);
                yield return new WaitForSecondsRealtime(.12f); Call(state, "RequestCode", Id("PasswordPad"), "9999");
                Assert.AreEqual(false, Get(pad, "Opened")); Assert.AreEqual(0, Call(state, "Hits", Id("PasswordPad")));
                yield return new WaitForSecondsRealtime(.12f); Call(state, "RequestCode", Id("PasswordPad"), "0123");
                Assert.AreEqual(true, Get(pad, "Opened")); Assert.AreEqual(false, Get(exit, "ConditionsMet"));
                yield return Use(Key("EscapeKey1")); Assert.AreEqual(false, Get(exit, "ConditionsMet"));
                yield return Use(Key("EscapeKey2")); Assert.AreEqual(false, Get(exit, "ConditionsMet"), "기존 추가 조건 보존");
                yield return Use("fixture-extra-exit-lock"); Assert.AreEqual(true, Get(exit, "ConditionsMet"));
                for (int i = 0; i < 3; i++) yield return Use(Key("EscapeDoor"));
                Assert.AreEqual(false, Get(exit, "Opened")); Assert.IsTrue(exitBlock.enabled);
                yield return Use(Id("MachineDoor")); Assert.AreEqual(1, Call(state, "Hits", Id("MachineDoor")));
                Assert.AreEqual(oldWallId, Get(wall, "TargetId")); Assert.AreEqual(6, Get(wall, "RequiredHits"));
                Assert.AreEqual("stake-puller", Get(wall, "ToolId")); Assert.AreEqual("Night", Field(State, "Phase").ToString());
            }
            finally { foreach (var item in all) if (item != null) Object.Destroy(item.gameObject); Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator M2_ExitRoute_InvalidConfigurationAndExplicitPasswordArePreserved()
        {
            var owner = new GameObject("exit-config"); owner.SetActive(false);
            var route = owner.AddComponent(T("CurtainCall.WaitingRoom.StructureAExitRoute"));
            var pad = ExplorationTestGate("existing-pad", "Keypad", Vector3.zero);
            var wrong = ExplorationTestGate("existing-open", "Open", Vector3.zero);
            try
            {
                Call(pad, "ConfigurePassword", "0000"); ExplorationSet(route, "passwordPad", pad);
                ExplorationSet(route, "escapePassword", "１２３４"); Assert.IsFalse(ApplyExitRoute(route));
                Assert.AreEqual("existing-pad", Get(pad, "TargetId")); Assert.AreEqual(true, Call(pad, "MatchesPassword", "0000"));
                ExplorationSet(route, "escapePassword", ""); Assert.IsTrue(ApplyExitRoute(route));
                Assert.AreEqual(false, Get(pad, "PasswordPending")); Assert.AreEqual(true, Call(pad, "MatchesPassword", "0000"));
                ExplorationSet(route, "escapeDoor", wrong); Assert.IsFalse(ApplyExitRoute(route)); Assert.AreEqual("existing-open", Get(wrong, "TargetId"));
                ExplorationSet(route, "escapeDoor", pad); Assert.IsFalse(ApplyExitRoute(route));
                Assert.AreEqual(true, Call(pad, "MatchesPassword", "0000"));
            }
            finally { Object.Destroy(pad.gameObject); Object.Destroy(wrong.gameObject); Object.Destroy(owner); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator M2_ExitRoute_OpenedSnapshotRestoresCollisionWithoutEnding()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            DisablePlacedRoutesForFixture();
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var owner = new GameObject("exit-snapshot"); owner.SetActive(false);
            var route = owner.AddComponent(T("CurtainCall.WaitingRoom.StructureAExitRoute"));
            var door = ExplorationTestGate("before-door", "Cooperative", player.transform.position + Vector3.forward * .5f);
            ExplorationSet(route, "escapeDoor", door); var block = ExitTestBlocker(door);
            string Key(string name) => (string)T("CurtainCall.WaitingRoom.StructureAKeyRoute").GetField(name).GetRawConstantValue();
            int generation = (int)Field(State, "Generation");
            var book = state.GetType().GetField("book", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            try
            {
                owner.SetActive(true); door.gameObject.SetActive(true); yield return null; Assert.IsTrue(block.enabled);
                // authoritative state presentation simulation, not a two-client interaction test.
                Call(book, "Load", new string[0], new int[0], new[] { Key("EscapeKey1"), Key("EscapeKey2"), "structure-a:escape-pad", Key("EscapeDoor") });
                yield return null; yield return null;
                Assert.AreEqual(true, Get(door, "Opened")); Assert.IsFalse(block.enabled);
                Assert.AreEqual("Night", Field(State, "Phase").ToString()); Assert.AreEqual(generation, Field(State, "Generation"));
                Call(book, "Clear"); yield return null; yield return null;
                Assert.AreEqual(false, Get(door, "Opened")); Assert.IsTrue(block.enabled);
                Assert.AreEqual(false, Get(door, "ConditionsMet")); Assert.AreEqual("Night", Field(State, "Phase").ToString());
            }
            finally { Object.Destroy(door.gameObject); Object.Destroy(owner); }
        }
    }
}
