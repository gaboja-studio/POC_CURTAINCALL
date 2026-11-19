using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerBossResetTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject root;
        Component boss;
        Transform rig, lever;
        Renderer[] meshes;
        BoxCollider[] hitboxes;
        LineRenderer ring;
        PropertyInfo timerCurrent;
        object originalTimer;
        readonly Vector3 rest = new(0, .2f, 0);
        readonly Quaternion rotation = Quaternion.Euler(0, 12, 0);
        static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);

        [SetUp] public void Setup()
        {
            timerCurrent = TypeOf("CurtainCall.EscapeRoute.EscapeRouteTimer").GetProperty("Current");
            originalTimer = timerCurrent.GetValue(null); timerCurrent.SetValue(null, null);
            root = new GameObject("Boss round reset regression"); root.SetActive(false);
            var chase = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeUnderstageChase"));
            Set(chase, "agent", root.GetComponent(TypeOf("UnityEngine.AI.NavMeshAgent")));
            Set(chase, "body", root.GetComponent<CapsuleCollider>());
            boss = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapePursuerBoss")); Set(boss, "chase", chase);
            rig = new GameObject("Visible rig").transform; rig.SetParent(root.transform);
            rig.localPosition = rest; rig.localRotation = rotation;
            Set(boss, "rig", rig); Set(boss, "rigRest", rest); Set(boss, "rigRotation", rotation);
            lever = new GameObject("Ignition lever").transform; lever.SetParent(root.transform); Set(boss, "ignitionLever", lever);
            ring = new GameObject("Landing warning").AddComponent<LineRenderer>(); ring.transform.SetParent(root.transform); Set(boss, "landingRing", ring);
            var partType = boss.GetType().GetNestedType("SeverablePart"); var parts = Array.CreateInstance(partType, 8);
            meshes = new Renderer[8]; hitboxes = new BoxCollider[8];
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject("Part " + i); go.transform.SetParent(rig);
                meshes[i] = go.AddComponent<SkinnedMeshRenderer>(); hitboxes[i] = go.AddComponent<BoxCollider>();
                var part = Activator.CreateInstance(partType); Set(part, "hitbox", hitboxes[i]); Set(part, "meshes", new[] { meshes[i] }); parts.SetValue(part, i);
            }
            Set(boss, "parts", parts);
        }

        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root); timerCurrent.SetValue(null, originalTimer);
        }

        [Test]
        public void ReinforcementsAreScheduledOneAtATimeAgainstTheOriginalEncounterClock()
        {
            var state = Get(boss, "state");
            Set(state, "EncounterStarted", 10d);
            Set(state, "Started", 15d);
            Set(state, "Reinforcements", (byte)4);
            Set(boss, "state", state);
            Assert.That(Call(boss, "ReinforcementsDue", 12.19d), Is.EqualTo(0));
            Assert.That(Call(boss, "ReinforcementsDue", 12.21d), Is.EqualTo(1));
            Assert.That(Call(boss, "ReinforcementsDue", 14.41d), Is.EqualTo(2));
            Assert.That(Call(boss, "ReinforcementsDue", 16.61d), Is.EqualTo(3));
            Assert.That(Call(boss, "ReinforcementsDue", 18.81d), Is.EqualTo(4));
            Assert.That(Call(boss, "ReinforcementsDue", 40d), Is.EqualTo(4));
            Call(boss, "SetReinforcementVisible", false);
            Assert.That(meshes.All(m => m.forceRenderingOff), Is.True);
            Assert.That(hitboxes.All(h => !h.enabled), Is.True);
            Call(boss, "SetReinforcementVisible", true);
            Assert.That(meshes.All(m => !m.forceRenderingOff), Is.True);
            Assert.That(hitboxes.All(h => h.enabled), Is.True);
        }

        [Test] public void RestartRestoresCutBodyBeforeANewEncounterClockCanPresentIt()
        {
            var state = Get(boss, "state");
            Set(state, "Phase", Enum.Parse(state.GetType().GetField("Phase").FieldType, "Dead"));
            Set(state, "Mask", byte.MaxValue); Set(state, "Round", 100d); Set(state, "Started", 101d); Set(boss, "state", state);
            Call(boss, "Present", 103d);
            Assert.That(meshes.All(mesh => !mesh.enabled), Is.True, "The previous death actually hid every cut part.");
            foreach (var hitbox in hitboxes) hitbox.enabled = false;
            ring.enabled = true; lever.localRotation = Quaternion.Euler(0, 0, 70);
            Set(boss, "landedAt", 101d);
            Call(boss, "Restart", new object[] { null });
            Assert.That(Get(Get(boss, "state"), "Round"), Is.EqualTo(-1d), "No new encounter clock has been accepted.");
            Assert.That(meshes.All(mesh => mesh.enabled) && hitboxes.All(hitbox => hitbox.enabled), Is.True);
            Assert.That(Vector3.Distance(rig.localPosition, rest), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rig.localRotation, rotation), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(lever.localRotation, Quaternion.identity), Is.LessThan(.001f));
            Assert.That(ring.enabled, Is.False);
            Assert.That(Get(boss, "presentedMask"), Is.EqualTo((byte)0));
            Assert.That(Get(boss, "presentedRound"), Is.EqualTo(-1d));
            Assert.That(Get(boss, "landedAt"), Is.EqualTo(-1d));
            var block = new MaterialPropertyBlock(); meshes[0].GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.white));
        }

        [Test] public void RestartRemovesOnlyItsOwnPendingSeveredVisuals()
        {
            var own = new GameObject("Severed old-round arm");
            var other = new GameObject("Severed unrelated scenery");
            try
            {
                var visuals = (IList)Get(boss, "severedVisuals"); visuals.Add(own);
                Call(boss, "Restart", new object[] { null });
                Assert.That(own == null, Is.True);
                Assert.That(visuals.Count, Is.Zero);
                Assert.That(other && other.activeSelf, Is.True);
            }
            finally { if (own) Object.DestroyImmediate(own); Object.DestroyImmediate(other); }
        }

        [Test] public void NewRoundIgnoresOldBossDeathAndCutsUntilItsOwnSnapshotArrives()
        {
            var state = Get(boss, "state");
            Set(state, "Phase", Enum.Parse(state.GetType().GetField("Phase").FieldType, "Dead"));
            Set(state, "Mask", byte.MaxValue); Set(state, "Round", 100d); Set(boss, "state", state);
            Call(boss, "Present", 103d);
            var timer = root.AddComponent(TypeOf("CurtainCall.EscapeRoute.EscapeRouteTimer"));
            Set(timer, "startedAt", 200d); Set(timer, "synchronized", true); timerCurrent.SetValue(null, timer);
            Assert.That(boss.GetType().GetProperty("State").GetValue(boss).ToString(), Is.EqualTo("Waiting"));
            foreach (string flag in new[] { "Defeated", "CombatStarted", "InBattle" })
                Assert.That(boss.GetType().GetProperty(flag).GetValue(boss), Is.False, flag);
            Assert.That(boss.GetType().GetProperty("Cuts").GetValue(boss), Is.EqualTo(0));
            Assert.That(boss.GetType().GetProperty("SpeedScale").GetValue(boss), Is.EqualTo(1f));
            for (int i = 0; i < 8; i++) Assert.That(Call(boss, "IsCut", i), Is.False);
            var motion = TypeOf("CurtainCall.EscapeRoute.EscapePursuerDoll").GetNestedType("Motion");
            object[] pose = { 201d, Enum.Parse(motion, "Idle"), 0f };
            Assert.That(Call(boss, "DollAction", pose), Is.False);
            Call(boss, "LateUpdate");
            Assert.That(meshes.All(mesh => mesh.enabled), Is.True, "A delayed boss message cannot keep the new body invisible.");
            Assert.That(Get(Get(boss, "state"), "Mask"), Is.EqualTo(byte.MaxValue), "Presentation does not rewrite the received authority state.");
            Set(state, "Round", 200d); Set(boss, "state", state);
            Assert.That(boss.GetType().GetProperty("Defeated").GetValue(boss), Is.True);
            Assert.That(Call(boss, "IsCut", 4), Is.True);
            Assert.That(Call(boss, "DollAction", pose), Is.True);
        }
    }
}
