using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerThreatInterruptTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Phase(object state, string name) => Set(state, "Phase", Enum.Parse(state.GetType().GetField("Phase").FieldType, name));
        GameObject root;
        Component chase, boss, timer;
        PropertyInfo currentTimer;
        object previousTimer;
        Transform debris;
        LineRenderer line, ring;

        [SetUp] public void Setup()
        {
            root = new GameObject("Offline boss threat interruption"); root.SetActive(false);
            chase = root.AddComponent(TypeOf("EscapeUnderstageChase"));
            boss = root.AddComponent(TypeOf("EscapePursuerBoss")); Set(chase, "boss", boss);
            timer = root.AddComponent(TypeOf("EscapeRouteTimer"));
            currentTimer = timer.GetType().GetProperty("Current"); previousTimer = currentTimer.GetValue(null);
            currentTimer.SetValue(null, timer);
            Assert.That(Call(timer, "ApplyState", 1u, 0, 1, 100d), Is.True);
            Set(chase, "threatRound", 100d);
            line = new GameObject("Old attack guide").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform);
            ring = new GameObject("Old impact warning").AddComponent<LineRenderer>(); ring.transform.SetParent(root.transform);
            debris = new GameObject("Object waiting in hand").transform; debris.SetParent(root.transform);
            Set(chase, "captureCable", line); Set(chase, "aimRing", ring); Set(chase, "thrownDebris", debris);
        }

        [TearDown] public void Cleanup()
        {
            currentTimer?.SetValue(null, previousTimer);
            Object.DestroyImmediate(root);
        }

        void SetBossPhaseWithRightHandIntact(byte cuts, string phase)
        {
            var state = Get(boss, "state"); Phase(state, phase); Set(state, "Round", 100d); Set(state, "Mask", cuts);
            Set(boss, "state", state);
            Assert.That(chase.GetType().GetProperty("CanUseAttackHand", Flags).GetValue(chase), Is.True,
                "This cut must bypass the existing missing-right-hand safeguard.");
        }

        object SetThreat(string phase)
        {
            var cue = Get(chase, "threat"); Phase(cue, phase);
            Set(cue, "Started", -10d); Set(cue, "Duration", 1f);
            Set(cue, "From", new Vector3(1, 2, 3)); Set(cue, "To", new Vector3(20, 2, 3));
            Set(chase, "threat", cue); Set(chase, "previousFlight", .42f);
            Set(chase, "nextDebris", 250d); Set(chase, "nextSend", 250d);
            return cue;
        }

        [TestCase(1, "DebrisWarning")][TestCase(4, "DebrisWarning")][TestCase(17, "DebrisWarning")]
        [TestCase(4, "ChargeWarning")]
        public void NonThrowingLimbHitCancelsUnreleasedAttackBeforeHitPoseCanHideItsRelease(int cuts, string phase)
        {
            SetBossPhaseWithRightHandIntact((byte)cuts, "Battle");
            SetThreat(phase);
            line.enabled = ring.enabled = true; debris.gameObject.SetActive(true);
            // TryHitSaw calls this before entering Hit, regardless of which exposed limb was cut.
            Call(chase, "CancelBossAttack");
            SetBossPhaseWithRightHandIntact((byte)cuts, "Hit");
            Assert.That(Get(Get(chase, "threat"), "Phase").ToString(), Is.EqualTo("None"));
            Assert.That(line.enabled || ring.enabled || debris.gameObject.activeSelf, Is.False);
            Assert.That((double)Get(chase, "nextDebris"), Is.GreaterThanOrEqualTo(250d), "Do not erase the scheduled throw cooldown.");
            Assert.That(Get(chase, "nextSend"), Is.EqualTo(0d));
            Assert.That(Call(chase, "AdvanceThreat"), Is.False,
                "Even after the former release deadline, the cancelled threat cannot preempt the boss hit recovery.");
            Assert.That(Get(Get(chase, "threat"), "Phase").ToString(), Is.EqualTo("None"));
        }

        [TestCase("DebrisFlight")][TestCase("Impact")]
        public void HitDoesNotEraseAlreadyReleasedProjectileOrItsImpact(string phase)
        {
            SetBossPhaseWithRightHandIntact(4, "Battle");
            var original = SetThreat(phase);
            bool visible = phase == "DebrisFlight"; debris.gameObject.SetActive(visible);
            Call(chase, "CancelBossAttack");
            SetBossPhaseWithRightHandIntact(4, "Hit");
            Assert.That(Get(chase, "threat"), Is.EqualTo(original));
            Assert.That(Get(chase, "previousFlight"), Is.EqualTo(.42f));
            Assert.That(Get(chase, "nextDebris"), Is.EqualTo(250d));
            Assert.That(debris.gameObject.activeSelf, Is.EqualTo(visible));
            Assert.That(Get(chase, "nextSend"), Is.EqualTo(0d));
        }
    }
}
