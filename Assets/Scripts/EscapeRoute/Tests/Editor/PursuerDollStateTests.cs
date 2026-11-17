using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class PursuerDollStateTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute." + name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Phase(object state, string name) => Set(state, "Phase", Enum.Parse(state.GetType().GetField("Phase").FieldType, name));
        GameObject root;
        Component chase, timer;
        PropertyInfo currentTimer;
        object previousTimer;

        [SetUp] public void Setup()
        {
            root = new GameObject("Offline pursuer round presentation"); root.SetActive(false);
            timer = root.AddComponent(TypeOf("EscapeRouteTimer"));
            currentTimer = timer.GetType().GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            previousTimer = currentTimer.GetValue(null); currentTimer.SetValue(null, timer);
            var clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), root.transform);
            chase = clone.GetComponentInChildren(TypeOf("EscapeUnderstageChase"), true);
            Set(chase, "breach", null);
            Set(chase, "boss", null);
            Set(chase, "walls", null);
            Assert.That(Call(timer, "ApplyState", 1u, 0, 1, 100d), Is.True);
        }

        [TearDown] public void Cleanup()
        {
            currentTimer?.SetValue(null, previousTimer);
            Object.DestroyImmediate(root);
        }

        object Snapshot(double round, string attackPhase = "None", string threatPhase = "None")
        {
            var snapshot = Activator.CreateInstance(chase.GetType().GetNestedType("Snapshot", BindingFlags.NonPublic));
            Set(snapshot, "Round", round); Set(snapshot, "Time", round + 1); Set(snapshot, "Rotation", Quaternion.identity);
            var attack = Get(snapshot, "Attack"); Phase(attack, attackPhase);
            Set(attack, "Actor", 72UL); Set(attack, "Origin", new Vector3(1, 2, 3));
            Set(attack, "Aim", new Vector3(1, 2, 15)); Set(attack, "Started", -1d); Set(snapshot, "Attack", attack);
            var threat = Get(snapshot, "Threat"); Phase(threat, threatPhase); Set(threat, "Duration", 1f);
            Set(threat, "Started", -1d); Set(snapshot, "Threat", threat);
            return snapshot;
        }

        object Pose() => Call(chase, "DollPoseAt", timer, true);
        Transform Prop(string name) => (Transform)Get(chase, name);
        LineRenderer Line(string name) => (LineRenderer)Get(chase, name);

        void ShowOldIndicators()
        {
            foreach (var name in new[] { "graspingHand", "extendedForearm", "thrownDebris" }) Prop(name).gameObject.SetActive(true);
            foreach (var name in new[] { "captureCable", "aimRing" }) Line(name).enabled = true;
        }

        void AssertIndicatorsCleared()
        {
            foreach (var name in new[] { "graspingHand", "extendedForearm", "thrownDebris" })
                Assert.That(Prop(name).gameObject.activeSelf, Is.False, name);
            foreach (var name in new[] { "captureCable", "aimRing" }) Assert.That(Line(name).enabled, Is.False, name);
        }

        [TestCase("Preparing", "None")][TestCase("Warning", "None")][TestCase("Strike", "None")][TestCase("Grab", "None")]
        [TestCase("Throw", "None")][TestCase("None", "DebrisWarning")][TestCase("None", "DebrisFlight")]
        public void NewClockBeforeNewPursuerSnapshotDropsPreviousRoundPoseAndIndicators(string attackPhase, string threatPhase)
        {
            var old = Snapshot(100, attackPhase, threatPhase);
            Assert.That(Call(chase, "Accept", old, 100d), Is.True);
            Assert.That((int)Get(Pose(), "Action"), Is.GreaterThanOrEqualTo(0), "The old round really had an attack pose.");
            ShowOldIndicators();
            // Timer reset/start can both arrive between two LateUpdates; no stopped-clock frame is required.
            Assert.That(Call(timer, "ApplyState", 2u, 0, 1, 200d), Is.True);
            Assert.That(Get(Pose(), "Action"), Is.EqualTo(-1));
            Assert.That(Get(Pose(), "Resting"), Is.True);
            var localAttack = Get(chase, "attack"); Phase(localAttack, "Grab"); Set(chase, "attack", localAttack);
            var localThreat = Get(chase, "threat"); Phase(localThreat, "DebrisFlight"); Set(chase, "threat", localThreat);
            var victim = Get(chase, "victim");
            Call(chase, "PresentRemoteCues", timer);
            AssertIndicatorsCleared();
            Assert.That(Get(chase, "attack"), Is.EqualTo(localAttack));
            Assert.That(Get(chase, "threat"), Is.EqualTo(localThreat));
            Assert.That(Get(chase, "victim"), Is.SameAs(victim), "Client cleanup only changes presentation.");
            Assert.That(Get(Get(chase, "latest"), "Round"), Is.EqualTo(100d));
        }

        [Test] public void ResetBarrierHidesTheOldPoseEvenBeforeItsEpochChanges()
        {
            Assert.That(Call(chase, "Accept", Snapshot(100, "Grab"), 100d), Is.True);
            ShowOldIndicators();
            Call(Get(timer, "resetBarrier"), "Begin");
            Assert.That(Get(Pose(), "Action"), Is.EqualTo(-1));
            Call(chase, "PresentRemoteCues", timer);
            AssertIndicatorsCleared();
        }

        [Test] public void CurrentRoundSnapshotRestoresTheNewStrikeAfterStaleVisualsWereCleared()
        {
            Assert.That(Call(chase, "Accept", Snapshot(100, "Grab"), 100d), Is.True);
            ShowOldIndicators();
            Assert.That(Call(timer, "ApplyState", 2u, 0, 1, 200d), Is.True);
            Call(chase, "PresentRemoteCues", timer);
            AssertIndicatorsCleared();
            var current = Snapshot(200, "Strike");
            Assert.That(Call(chase, "Accept", current, 200d), Is.True);
            Assert.That((int)Get(Pose(), "Action"), Is.GreaterThanOrEqualTo(0));
            Call(chase, "PresentRemoteCues", timer);
            Assert.That(Prop("graspingHand").gameObject.activeSelf, Is.True);
            Assert.That(Prop("extendedForearm").gameObject.activeSelf, Is.True);
            // This fixture deliberately keeps the owner inactive. Its pose may be sampled,
            // but neither the legacy warnings nor the new world UI should render.
            Assert.That(Line("captureCable").enabled, Is.False);
            Assert.That(Line("aimRing").enabled, Is.False);
            Assert.That(Prop("thrownDebris").gameObject.activeSelf, Is.False);
            Assert.That(Vector3.Distance(Prop("graspingHand").position, (Vector3)Get(Get(current, "Attack"), "Aim")), Is.LessThan(.001f));
        }

        [TestCase("Dead", 255)][TestCase("Battle", 48)]
        public void NewRoundCannotResampleOldBossDeathOrMaimedPoseEvenWhenChaseSnapshotArrivesFirst(string phase, byte mask)
        {
            Assert.That(Call(chase, "Accept", Snapshot(100), 100d), Is.True);
            var boss = chase.GetComponent(TypeOf("EscapePursuerBoss")); Set(chase, "boss", boss);
            var state = Get(boss, "state"); Phase(state, phase); Set(state, "Mask", mask);
            Set(state, "Round", 100d); Set(boss, "state", state);
            Assert.That(Call(timer, "ApplyState", 2u, 0, 1, 200d), Is.True);
            Assert.That(Get(Pose(), "Action"), Is.EqualTo(-1));
            Assert.That(Call(chase, "Accept", Snapshot(200), 200d), Is.True);
            Assert.That(Get(Pose(), "Action"), Is.EqualTo(-1), "Boss snapshots may arrive after the new chase snapshot.");
        }
    }
}
