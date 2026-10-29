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
        static Component ClueRouteFixture(string name, Vector3 at)
        {
            var go = new GameObject(name); go.SetActive(false); go.transform.position = at;
            var clue = go.AddComponent(T("CurtainCall.WaitingRoom.ClueInspectable"));
            ExplorationSet(clue, "targetId", "before-" + name); ExplorationSet(clue, "title", name); return clue;
        }
        static bool ApplyClueRoute(Component route) => (bool)route.GetType().GetMethod("TryApply").Invoke(route, new object[] { null });
        [UnityTest]
        public IEnumerator M2_ClueRoute_NotesUvMonitorAndPendingHint()
        {
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            DisablePlacedRoutesForFixture();
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var at = player.transform.position + Vector3.forward * .5f;
            var owner = new GameObject("stage5-clue-route"); owner.SetActive(false);
            var route = owner.AddComponent(T("CurtainCall.WaitingRoom.StructureAClueRoute"));
            string Id(string name) => (string)route.GetType().GetField(name).GetRawConstantValue();
            string[] fields = { "graffiti1", "graffiti2", "graffiti4", "oldNotebook", "performanceRecord", "newspaper", "confiscatedNotebook" };
            string[] names = { "Graffiti1", "Graffiti2", "Graffiti4", "OldNotebook", "PerformanceRecord", "Newspaper", "ConfiscatedNotebook" };
            var notes = new List<Component>(); var all = new List<Component>();
            for (int i = 0; i < fields.Length; i++)
            { var clue = ClueRouteFixture(fields[i], at); ExplorationSet(route, fields[i], clue); notes.Add(clue); all.Add(clue); }
            Component Uv(string field)
            {
                var go = new GameObject(field); go.SetActive(false); go.transform.position = at;
                var mark = go.AddComponent(T("CurtainCall.WaitingRoom.UvMark")); ExplorationSet(route, field, mark); all.Add(mark); return mark;
            }
            var uv1 = Uv("restroomUv"); var uv2 = Uv("passwordPrefixUv");
            var detail1 = ClueRouteFixture("uv-detail-1", at); var detail2 = ClueRouteFixture("uv-prefix-detail", at);
            ExplorationSet(route, "restroomUvClue", detail1); ExplorationSet(route, "passwordPrefixClue", detail2); all.Add(detail1); all.Add(detail2);
            var hidden = ClueRouteFixture("hidden-hint", at); ExplorationSet(route, "hiddenGraffiti3", hidden); all.Add(hidden);
            var monitor = ExplorationTestGate("before-monitor", "Monitor", at); all.Add(monitor); ExplorationSet(route, "monitor", monitor);
            ExplorationSet(monitor, "requiredTargets", new[] { Id("Graffiti1") }); ExplorationSet(monitor, "monitorText", "fixture-monitor-text");
            IEnumerator Read(Component clue)
            {
                yield return new WaitForSecondsRealtime(.12f); Call(clue, "Interact"); yield return null;
                Assert.AreEqual(true, Get(clue, "IsOpen")); Assert.AreEqual(true, Get(clue, "Discovered"));
                Assert.AreEqual(true, Static("CurtainCall.Player.InputGate", "IsRequested"));
                Call(clue, "SetOpen", false); yield return null;
                Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));
            }
            try
            {
                owner.SetActive(true); foreach (var clue in notes) clue.gameObject.SetActive(true);
                uv1.gameObject.SetActive(true); uv2.gameObject.SetActive(true); monitor.gameObject.SetActive(true);
                Assert.AreEqual(false, Get(monitor, "ConditionsMet"), "기존 모니터 조건 보존");
                Assert.IsFalse(hidden.gameObject.activeSelf); Assert.AreEqual(true, Get(hidden, "RevealPending"));
                Call(hidden, "SetOpen", true); Assert.AreEqual(false, Get(hidden, "IsOpen"));
                for (int i = 0; i < notes.Count; i++)
                {
                    Assert.AreEqual(Id(names[i]), Get(notes[i], "TargetId")); Assert.AreEqual(fields[i], Get(notes[i], "Title"));
                    yield return Read(notes[i]); Assert.AreEqual(1, Call(state, "Hits", Id(names[i])));
                }
                yield return Read(notes[0]); Assert.AreEqual(1, Call(state, "Hits", Id("Graffiti1")));
                ExplorationSet(route, "hiddenGraffiti3Condition", Id("Monitor")); Assert.IsTrue(ApplyClueRoute(route));
                Assert.IsFalse(hidden.gameObject.activeSelf); Assert.AreEqual(false, Get(hidden, "RevealPending"));
                yield return new WaitForSecondsRealtime(.12f); Call(monitor, "Interact"); yield return null;
                Assert.AreEqual(true, Static("CurtainCall.Player.InputGate", "IsRequested")); Assert.AreEqual(1, Call(state, "Hits", Id("Monitor")));
                monitor.GetType().GetMethod("ClosePanel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(monitor, null);
                Assert.IsTrue(hidden.gameObject.activeSelf); yield return Read(hidden);
                Call(monitor, "Interact"); Assert.AreEqual(1, Call(state, "Hits", Id("Monitor")));
                monitor.GetType().GetMethod("ClosePanel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(monitor, null);
                Assert.IsFalse(detail1.gameObject.activeSelf); Assert.IsFalse(detail2.gameObject.activeSelf);
                int basis = (int)T("CurtainCall.WaitingRoom.ItemUses").GetField("FoundUidBase").GetRawConstantValue() + (int)Field(State, "Generation") * 100;
                var lamp = Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"), basis + 93, "uv-lamp", "fixture-uv", 2);
                Call(carry, "PlaceFound", lamp, player.transform.position); Call(carry, "RequestPickUp", basis + 93);
                ulong me = (ulong)Get(carry, "LocalClientId");
                foreach (var pair in new[] { new[] { uv1, detail1 }, new[] { uv2, detail2 } })
                {
                    Call(pair[1], "SetOpen", true); Assert.AreEqual(false, Get(pair[1], "IsOpen"));
                    yield return new WaitForSecondsRealtime(.12f); Call(pair[0], "Interact"); yield return null;
                    Assert.IsTrue(pair[1].gameObject.activeSelf); yield return Read(pair[1]);
                    Assert.AreEqual(1, Call(state, "Hits", Get(pair[0], "TargetId")));
                }
                Assert.AreEqual(false, Call(carry, "Exists", basis + 93), "UV는 새로운 두 단서 발견 때만 소모");
                uv2.gameObject.SetActive(false); uv2.gameObject.SetActive(true); yield return null;
                yield return Read(detail2); Assert.AreEqual(1, Call(state, "Hits", Id("PasswordPrefixUv")));
                Call(detail2, "SetOpen", true); yield return null;
                var book = state.GetType().GetField("book", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state); Call(book, "Clear");
                yield return null; yield return null;
                Assert.IsFalse(detail2.gameObject.activeSelf); Assert.IsFalse(hidden.gameObject.activeSelf);
                Assert.AreEqual(false, Get(detail2, "IsOpen")); Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));
            }
            finally { foreach (var obj in all) if (obj != null) Object.Destroy(obj.gameObject); Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator M2_ClueRoute_InvalidBindingsPreservePriorConfiguration()
        {
            var owner = new GameObject("clue-binding-config"); owner.SetActive(false);
            var route = owner.AddComponent(T("CurtainCall.WaitingRoom.StructureAClueRoute"));
            var clue = ClueRouteFixture("existing-note", Vector3.zero);
            var monitor = ExplorationTestGate("existing-gate", "Open", Vector3.zero);
            try
            {
                ExplorationSet(route, "oldNotebook", clue); ExplorationSet(route, "performanceRecord", clue);
                Assert.IsFalse(ApplyClueRoute(route)); Assert.AreEqual("before-existing-note", Get(clue, "TargetId"));
                ExplorationSet(route, "oldNotebook", null); ExplorationSet(route, "performanceRecord", null);
                Call(clue, "ConfigureRoute", "existing-note", "existing-condition"); ExplorationSet(route, "newspaper", clue);
                Assert.IsTrue(ApplyClueRoute(route)); Assert.AreEqual("existing-condition", Get(clue, "UnlockTarget"));
                ExplorationSet(route, "monitor", monitor); Assert.IsFalse(ApplyClueRoute(route)); Assert.AreEqual("existing-gate", Get(monitor, "TargetId"));
                ExplorationSet(route, "monitor", null); ExplorationSet(route, "newspaper", null); ExplorationSet(route, "passwordPrefixClue", clue);
                Assert.IsFalse(ApplyClueRoute(route)); Assert.AreEqual("existing-condition", Get(clue, "UnlockTarget"));
            }
            finally { Object.Destroy(clue.gameObject); Object.Destroy(monitor.gameObject); Object.Destroy(owner); }
            yield return null;
        }
    }
}
