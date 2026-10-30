using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    /// <summary>Shared time, consumed volume and original entry eligibility without Play or network startup.</summary>
    public sealed class PursuitFireTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const string FireType = "CurtainCall.EscapeRoute.EscapePursuitFire";
        static readonly Vector3 Origin = new(9400, 100, 9400);
        readonly List<GameObject> roots = new();
        readonly List<(Type type, string name, object value)> statics = new();
        Component player;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target)
            ?? target.GetType().GetField(name, Flags)?.GetValue(target);
        static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Flags);
            if (property != null) property.SetValue(target, value); else target.GetType().GetField(name, Flags).SetValue(target, value);
        }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static object Static(string name, params object[] args) => T(FireType).GetMethod(name, StaticFlags).Invoke(null, args);
        static void SetStatic(Type type, string name, object value)
        {
            var property = type.GetProperty(name, StaticFlags);
            if (property != null) property.SetValue(null, value); else type.GetField(name, StaticFlags).SetValue(null, value);
        }
        void Isolate(string typeName, string name = "Current")
        {
            var type = T(typeName);
            object value = type.GetProperty(name, StaticFlags)?.GetValue(null) ?? type.GetField(name, StaticFlags)?.GetValue(null);
            statics.Add((type, name, value)); SetStatic(type, name, null);
        }
        GameObject Root(string name, bool active = false)
        {
            var root = new GameObject(name) { hideFlags = HideFlags.DontSave };
            root.SetActive(active); root.transform.position = Origin; roots.Add(root); return root;
        }
        Component Fire(int count = 13)
        {
            var fire = Root("Offline pursuit clock").AddComponent(T(FireType));
            Type bayType = fire.GetType().GetNestedType("Bay");
            var bays = Array.CreateInstance(bayType, count);
            for (int i = 0; i < count; i++) bays.SetValue(Activator.CreateInstance(bayType), i);
            Set(fire, "bays", bays); return fire;
        }

        [SetUp]
        public void SetUp()
        {
            Isolate(FireType); Isolate("CurtainCall.HumanCannon.HumanCannonSystem");
            Isolate("CurtainCall.HumanCannon.HumanCannonPoleSystem");
            Isolate("CurtainCall.Tightrope.TightropeCourse", "current");
            Isolate("CurtainCall.Network.PlayerSync.NetworkPlayer", "ServerCanChangeState");
        }
        [TearDown]
        public void TearDown()
        {
            if (player != null) Set(player, "IsSpawned", false);
            for (int i = roots.Count - 1; i >= 0; i--) if (roots[i] != null) Object.DestroyImmediate(roots[i]);
            roots.Clear(); player = null;
            foreach (var value in statics) SetStatic(value.type, value.name, value.value);
            statics.Clear(); Physics.SyncTransforms();
        }

        [TestCase(116.999, 0, "Dormant")]
        [TestCase(117, 0, "Warning")]
        [TestCase(119.999, 0, "Warning")]
        [TestCase(120, 0, "Burst")]
        [TestCase(122.999, 0, "Burst")]
        [TestCase(123, 0, "Burning")]
        [TestCase(129, 1, "Warning")]
        [TestCase(132, 1, "Burst")]
        [TestCase(1000, 0, "Burning")]
        public void ScheduleKeepsWarningBurstAndPermanentBurnSeparate(double now, int bay, string phase)
        {
            Assert.That(Static("SchedulePhase", now, 100d, bay, 13, 20f, 12f, 3f, 3f).ToString(), Is.EqualTo(phase));
        }

        [Test]
        public void InvalidAndFutureClocksNeverConsumeBays()
        {
            foreach (double now in new[] { -1d, 99d, double.NaN, double.PositiveInfinity })
            {
                Assert.That(Static("ConsumedCount", now, 100d, 13, 20f, 12f), Is.EqualTo(0));
                Assert.That(Static("SchedulePhase", now, 100d, 0, 13, 20f, 12f, 3f, 3f).ToString(), Is.EqualTo("Dormant"));
            }
            Assert.That(Static("ConsumedCount", 1000d, -1d, 13, 20f, 12f), Is.EqualTo(0));
            Assert.That(Static("ConsumedCount", 1000d, 100d, 13, 20f, 0f), Is.EqualTo(0));
            Assert.That(Static("SchedulePhase", 1000d, 100d, 13, 13, 20f, 12f, 3f, 3f).ToString(), Is.EqualTo("Dormant"));
            Assert.That(Static("SchedulePhase", 1000d, 100d, 0, 13, 20f, 12f, float.NaN, 3f).ToString(), Is.EqualTo("Dormant"));
        }

        [Test]
        public void LateJoinUsesOriginalEpochAndCannotRestartTheGracePeriod()
        {
            var fire = Fire();
            Assert.That(Call(fire, "ApplyState", 5U, 0, 100d), Is.True);
            Assert.That(Call(fire, "PhaseAt", 0, 160d).ToString(), Is.EqualTo("Burning"));
            Assert.That(Call(fire, "PhaseAt", 3, 160d).ToString(), Is.EqualTo("Burning"));
            Assert.That(Static("ConsumedCount", 160d, Get(fire, "ArmedAt"), 13, 20f, 12f), Is.EqualTo(4));
            Assert.That(Call(fire, "ApplyState", 4U, 0, 160d), Is.False);
            Assert.That(Call(fire, "ApplyState", 5U, 0, 160d), Is.False);
            Assert.That(Get(fire, "ArmedAt"), Is.EqualTo(100d));
            Assert.That(Static("ConsumedCount", 10000d, Get(fire, "ArmedAt"), 13, 20f, 12f), Is.EqualTo(13));
        }

        [Test]
        public void RestartBarrierRejectsDelayedFireAndInvalidAcknowledgmentBeforeNewEpoch()
        {
            var fire = Fire(); Call(fire, "ApplyState", 8U, 0, 100d);
            int token = (int)Call(Get(fire, "resetBarrier"), "Begin");
            Assert.That(Call(fire, "ApplyState", 9U, 0, 100d), Is.False);
            Assert.That(Call(fire, "ApplyState", 9U, token, double.NaN), Is.False);
            Assert.That(Get(Get(fire, "resetBarrier"), "Waiting"), Is.True);
            Assert.That(Call(fire, "ApplyState", 9U, token, -1d), Is.True);
            Assert.That(Get(fire, "IsArmed"), Is.False);
            Assert.That(Call(fire, "ApplyState", 8U, 0, 100d), Is.False);
            Assert.That(Call(fire, "ApplyState", 10U, 0, 500d), Is.True);
            Assert.That(Call(fire, "PhaseAt", 0, 516d).ToString(), Is.EqualTo("Dormant"));
        }

        [Test]
        public void ConsumedVolumeCoversBothSidesAndRemainsLethalButExitIsAbsolutelySafe()
        {
            var fire = Fire(2); var bays = (Array)Get(fire, "bays");
            for (int i = 0; i < 2; i++)
            {
                var coverage = Root("Whole-width bay " + i, true).AddComponent<BoxCollider>();
                coverage.isTrigger = true; coverage.center = new Vector3(i * 20 + 10, 3, 0); coverage.size = new Vector3(20, 102, 42);
                Set(bays.GetValue(i), "coverage", coverage);
            }
            Set(fire, "safeExitX", Origin.x + 40); Call(fire, "ApplyState", 1U, 0, 100d);
            bool Threat(Vector3 point, double now) => (bool)Call(fire, "Threatens", Origin + point, .3f, 1.5f, now);
            Assert.That(Threat(new Vector3(10, 0, -20), 119.9), Is.False);
            Assert.That(Threat(new Vector3(10, 0, -20), 120), Is.True);
            Assert.That(Threat(new Vector3(10, 0, 20), 120), Is.True, "Changing sides cannot avoid a consumed whole-width section.");
            Assert.That(Threat(new Vector3(30, 0, 0), 131.9), Is.False);
            Assert.That(Threat(new Vector3(30, 0, 0), 132), Is.True);
            Assert.That(Threat(new Vector3(10, -40, 0), 500), Is.True);
            Assert.That(Threat(new Vector3(10, 40, 0), 500), Is.True);
            Assert.That(Threat(new Vector3(39.99f, 0, 0), 500), Is.True);
            Assert.That(Threat(new Vector3(40, 0, 0), 500), Is.False, "No trailing capsule-radius damage past the exit.");
            Assert.That(Threat(new Vector3(41, 0, 0), 500), Is.False);
            Assert.That(Threat(new Vector3(10, 60, 0), 500), Is.False);
            Call(fire, "ApplyState", 2U, 0, -1d);
            Assert.That(Threat(new Vector3(10, 0, 0), 500), Is.False);
        }

        [Test]
        public void EntryRequiresActualGroundSupportAndHonorsInspectionDamageGuardForRemoteOwner()
        {
            var fire = Fire();
            Set(fire, "safeExitX", Origin.x + 100);
            var volume = Root("Finale entry", true).AddComponent<BoxCollider>();
            volume.isTrigger = true; volume.center = Vector3.up * .5f; volume.size = new Vector3(4, 2, 4); Set(fire, "trigger", volume);
            var actor = Root("Remote finale entrant");
            player = actor.AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var mover = actor.GetComponent(T("CurtainCall.Player.PlayerMover")); Call(mover, "Awake");
            Set(player, "IsSpawned", true); Set(player, "OwnerClientId", 69UL); Set(player, "IsOwner", false); Set(mover, "Simulated", false);
            actor.SetActive(true); Physics.SyncTransforms();
            Assert.That(Call(fire, "CanArm", player), Is.False, "A free-flight body inside the volume has no actual footing.");
            var support = Root("Original entry floor", true).AddComponent<BoxCollider>();
            support.center = Vector3.down * .15f; support.size = new Vector3(4, .3f, 4); Physics.SyncTransforms();
            Assert.That(Call(fire, "CanArm", player), Is.True, "A remote owner is not locally simulated, but can trigger the shared clock.");
            var guard = player.GetType().GetProperty("ServerCanChangeState", StaticFlags);
            var parameters = guard.PropertyType.GetMethod("Invoke").GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
            guard.SetValue(null, Expression.Lambda(guard.PropertyType, Expression.Constant(false), parameters).Compile());
            Assert.That(Call(fire, "CanArm", player), Is.False, "Host-approved inspection is excluded even while near a floor.");
            guard.SetValue(null, null);
            var jump = Get(player, "jump"); Type jumpType = Get(jump, "m_InternalValue").GetType();
            Set(jump, "m_InternalValue", Enum.Parse(jumpType, "Fall"));
            Assert.That(Call(fire, "CanArm", player), Is.False);
        }
    }
}
