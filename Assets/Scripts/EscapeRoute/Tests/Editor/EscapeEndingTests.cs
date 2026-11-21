using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class EscapeEndingTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static Type Ending => T("CurtainCall.EscapeRoute.EscapeEndingPresentation");
        static object State(string name) => Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), name);
        static bool Reveal(string state, bool open, bool inside, bool inspecting) => (bool)Ending.GetMethod("CanReveal")
            .Invoke(null, new[] { State(state), open, inside, inspecting });

        [Test]
        public void OfficialInteractionAudit_ResultRestoresInputWhenTheGameHudEventSystemDisappears()
        {
            var systemType = T("UnityEngine.EventSystems.EventSystem");
            var current = systemType.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
            object saved = current.GetValue(null);
            var root = new GameObject("Ending result input audit");
            var hud = new GameObject("Prior gameplay HUD events");
            try
            {
                var resultsType = T("CurtainCall.EscapeRoute.EscapeEndingResults");
                var results = root.AddComponent(resultsType);
                var display = new GameObject("Result view");
                display.SetActive(false);
                display.transform.SetParent(root.transform);
                resultsType.GetField("view", Flags).SetValue(results, display.AddComponent(T("CurtainCall.UI.EscapeResultsKit.EscapeResultsView")));
                var previous = hud.AddComponent(systemType);
                hud.AddComponent(T("UnityEngine.InputSystem.UI.InputSystemUIInputModule"));
                systemType.GetMethod("OnEnable", Flags).Invoke(previous, null);
                current.SetValue(null, previous);
                var ensure = resultsType.GetMethod("EnsureResultEvents", Flags);
                ensure.Invoke(results, null);
                Assert.That(current.GetValue(null), Is.SameAs(previous));
                systemType.GetMethod("OnDisable", Flags).Invoke(previous, null);
                hud.SetActive(false);
                display.SetActive(true);
                ensure.Invoke(results, null);
                var replacement = resultsType.GetField("resultEvents", Flags).GetValue(results);
                Assert.That(replacement, Is.Not.Null);
                systemType.GetMethod("OnEnable", Flags).Invoke(replacement, null);
                Assert.That(current.GetValue(null), Is.SameAs(replacement));
                ensure.Invoke(results, null);
                Assert.That(current.GetValue(null), Is.SameAs(replacement));
                systemType.GetMethod("OnDisable", Flags).Invoke(replacement, null);
            }
            finally
            {
                Object.DestroyImmediate(hud);
                Object.DestroyImmediate(root);
                if (saved != null) current.SetValue(null, saved);
            }
        }

        [TestCase(false, "EscapeSucceeded")]
        [TestCase(true, "EscapeAllRetired")]
        public void OfficialInteractionAudit_OnlyTheEndingAuthorityCanRebuildAResetResultsSnapshot(bool failure, string reason)
        {
            var root = new GameObject("Ending snapshot audit");
            root.SetActive(false);
            try
            {
                var type = T("CurtainCall.EscapeRoute.EscapeEndingResults");
                var result = root.AddComponent(type);
                type.GetField("failureMode", Flags).SetValue(result, failure);
                var state = Activator.CreateInstance(T("CurtainCall.Campaign.CampaignSnapshot"));
                var phase = state.GetType().GetField("Phase");
                var end = state.GetType().GetField("LastEnd");
                phase.SetValue(state, Enum.Parse(phase.FieldType, "Ending"));
                end.SetValue(state, Enum.Parse(end.FieldType, reason));
                var check = type.GetMethod("ShouldRefreeze", Flags);
                Assert.That(check.Invoke(result, new[] { state, (object)true }), Is.True);
                Assert.That(check.Invoke(result, new[] { state, (object)false }), Is.False);
                var snapshot = type.GetNestedType("Snapshot", BindingFlags.NonPublic);
                type.GetField("snapshot", Flags).SetValue(result, Activator.CreateInstance(snapshot, true));
                Assert.That(check.Invoke(result, new[] { state, (object)true }), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FirstArrivalShowsTitleWithoutWaitingForPartyOrRevealingThroughLockedDoor()
        {
            Assert.IsFalse(Reveal("Normal", true, false, false));
            Assert.IsFalse(Reveal("Normal", false, true, false));
            Assert.IsFalse(Reveal("Normal", true, true, true));
            Assert.IsTrue(Reveal("Normal", true, true, false));
            Assert.IsTrue(Reveal("Arrived", true, false, false), "Arrival remains visible after the server has frozen the player.");
            foreach (string terminal in new[] { "Fallen", "Retired" }) Assert.IsFalse(Reveal(terminal, true, true, false));
        }

        [Test]
        public void OnlySuccessfulEscapeKeepsTheMeadowForTheEnding()
        {
            var snapshot = Activator.CreateInstance(T("CurtainCall.Campaign.CampaignSnapshot"));
            var phase = snapshot.GetType().GetField("Phase"); var reason = snapshot.GetType().GetField("LastEnd");
            foreach (string p in new[] { "Escape", "Intro", "DemoEnding", "Ending" })
                foreach (string r in new[] { "None", "EscapeAllRetired", "EscapeSucceeded" })
                {
                    phase.SetValue(snapshot, Enum.Parse(phase.FieldType, p)); reason.SetValue(snapshot, Enum.Parse(reason.FieldType, r));
                    Assert.That(Ending.GetMethod("IsSuccessfulEnding").Invoke(null, new[] { snapshot }), Is.EqualTo(p == "Ending" && r == "EscapeSucceeded"));
                }
        }

        [Test]
        public void FadeWaitsThenRisesSmoothlyAndNeverProducesInvalidAlpha()
        {
            float Alpha(double elapsed) => (float)Ending.GetMethod("RevealAlpha").Invoke(null, new object[] { elapsed, 1f, 1.8f });
            Assert.That(Alpha(0), Is.Zero); Assert.That(Alpha(1), Is.Zero);
            float previous = 0;
            for (double t = 1; t <= 4; t += .1) { float a = Alpha(t); Assert.That(a, Is.InRange(previous, 1)); previous = a; }
            Assert.That(Alpha(1.9), Is.EqualTo(.5f).Within(.001)); Assert.That(Alpha(3), Is.EqualTo(1));
            foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity }) Assert.That(Alpha(invalid), Is.Zero);
        }

        [Test]
        public void EndingCameraAndInputAreIdempotentAndRestartClearsTheTitle()
        {
            var go = new GameObject("Ending presentation fixture"); go.SetActive(false);
            var component = go.AddComponent(Ending);
            var title = go.AddComponent<CanvasGroup>(); title.alpha = 1; title.blocksRaycasts = title.interactable = true;
            var camera = (Behaviour)go.AddComponent(T("Unity.Cinemachine.CinemachineCamera")); camera.enabled = false;
            Ending.GetField("title", Flags).SetValue(component, title); Ending.GetField("endingCamera", Flags).SetValue(component, camera);
            var blockers = (HashSet<object>)T("CurtainCall.Player.InputGate").GetField("blockers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var gateFields = new[] { "rearming", "checkedFrame", "blockedFrame" }.Select(n =>
                T("CurtainCall.Player.InputGate").GetField(n, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
            var previousGate = gateFields.Select(f => f.GetValue(null)).ToArray();
            try
            {
                Ending.GetMethod("BeginEnding", Flags).Invoke(component, null);
                var at = Ending.GetField("endingAt", Flags).GetValue(component);
                Assert.IsTrue(camera.enabled); Assert.IsTrue(blockers.Contains(component));
                Ending.GetMethod("BeginEnding", Flags).Invoke(component, null);
                Assert.That(Ending.GetField("endingAt", Flags).GetValue(component), Is.EqualTo(at));
                Ending.GetMethod("ResetPresentation", Flags).Invoke(component, null);
                Assert.IsFalse(camera.enabled); Assert.IsFalse(blockers.Contains(component)); Assert.That(title.alpha, Is.Zero);
                Assert.IsFalse(title.interactable); Assert.IsFalse(title.blocksRaycasts);
            }
            finally
            {
                Ending.GetMethod("ResetPresentation", Flags).Invoke(component, null); Object.DestroyImmediate(go);
                for (int i = 0; i < gateFields.Length; i++) gateFields[i].SetValue(null, previousGate[i]);
            }
        }
    }
}
