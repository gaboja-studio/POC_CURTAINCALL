using System.Reflection;
using CurtainCall.Player;
using CurtainCall.Tightrope.Control;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeInputBufferTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void EarlyPressKeepsDirectionAndFiresOnlyOnceAfterLanding()
        {
            var buffer = new TightropeJumpBuffer();
            Assert.IsFalse(buffer.Resolve(true, -1, false, 10, 0.15f, out _));
            Assert.IsTrue(buffer.Resolve(false, 1, true, 10.1, 0.15f, out int direction));
            Assert.AreEqual(-1, direction);
            Assert.IsFalse(buffer.Resolve(false, 1, true, 10.11, 0.15f, out _));
        }

        [Test]
        public void ExpiredPressDoesNotJumpOnLanding()
        {
            var buffer = new TightropeJumpBuffer();
            buffer.Resolve(true, 1, false, 10, 0.15f, out _);
            Assert.IsFalse(buffer.Resolve(false, 1, true, 10.16, 0.15f, out _));
        }

        [Test]
        public void NewPressReplacesOldDirection()
        {
            var buffer = new TightropeJumpBuffer();
            buffer.Resolve(true, -1, false, 10, 0.15f, out _);
            buffer.Resolve(true, 0, false, 10.1, 0.15f, out _);
            Assert.IsTrue(buffer.Resolve(false, 1, true, 10.2, 0.15f, out int direction));
            Assert.AreEqual(0, direction);
        }

        [Test]
        public void DisabledWindowStillAllowsImmediateGroundedPress()
        {
            var buffer = new TightropeJumpBuffer();
            Assert.IsFalse(buffer.Resolve(true, 0, false, 10, 0, out _));
            Assert.IsFalse(buffer.Resolve(false, 0, true, 10, 0, out _));
            Assert.IsTrue(buffer.Resolve(true, 0, true, 10, 0, out _));
        }

        [TestCase("exit")]
        [TestCase("teleport")]
        [TestCase("control")]
        [TestCase("simulation")]
        [TestCase("death")]
        public void SchemeInvalidatesBufferedPress(string reason)
        {
            var go = new GameObject("Buffer lifecycle test");
            var scheme = ScriptableObject.CreateInstance<TightropeControlScheme>();
            PlayerControlContext context = null;
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                var condition = go.AddComponent<PlayerCondition>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                context = new PlayerControlContext(go);
                typeof(PlayerMover).GetField("<IsAirborne>k__BackingField", Members).SetValue(mover, true);
                scheme.Tick(context, new PlayerInputFrame { ActionPressed = true, AuxDirection = 1 });
                Assert.AreEqual(0, typeof(PlayerMover).GetField("laneJumpRequested", Members).GetValue(mover));
                switch (reason)
                {
                    case "exit": scheme.Exit(context); break;
                    case "teleport": mover.ResetToStart(); break;
                    case "control": mover.SetControlEnabled(false); scheme.Tick(context, default); mover.SetControlEnabled(true); break;
                    case "simulation": mover.Simulated = false; scheme.Tick(context, default); mover.Simulated = true; break;
                    case "death": condition.SetLostParts(PlayerCondition.AllLimbs); scheme.Tick(context, default); condition.RestoreAll(); break;
                }
                typeof(PlayerMover).GetField("<IsAirborne>k__BackingField", Members).SetValue(mover, false);
                scheme.Tick(context, default);
                Assert.AreEqual(0, typeof(PlayerMover).GetField("laneJumpRequested", Members).GetValue(mover));
            }
            finally
            {
                if (context != null) scheme.Exit(context);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(scheme);
            }
        }

        [Test]
        public void PassengerCanStillBalanceAndRequestDismount()
        {
            var go = new GameObject("Passenger input test");
            var scheme = ScriptableObject.CreateInstance<TightropeControlScheme>();
            PlayerControlContext context = null;
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                var balance = go.AddComponent<PlayerBalance>();
                var interaction = go.AddComponent<PlayerInteraction>();
                int releases = 0;
                interaction.ReleaseRequested += () => releases++;
                context = new PlayerControlContext(go);
                mover.Simulated = false;
                scheme.Tick(context, new PlayerInputFrame { Posture = -1f, ActionPressed = true, AuxDirection = 1, InteractLongPressed = true });
                Assert.AreEqual(-1f, typeof(PlayerBalance).GetField("correctionInput", Members).GetValue(balance));
                Assert.AreEqual(1, releases);
                Assert.AreEqual(0, typeof(PlayerMover).GetField("laneJumpRequested", Members).GetValue(mover));
            }
            finally
            {
                if (context != null) scheme.Exit(context);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(scheme);
            }
        }

        [Test]
        public void SharedSchemeKeepsEachPlayersPressSeparate()
        {
            var first = new GameObject("First buffer player");
            var second = new GameObject("Second buffer player");
            var scheme = ScriptableObject.CreateInstance<TightropeControlScheme>();
            PlayerControlContext a = null, b = null;
            try
            {
                var moverA = first.AddComponent<PlayerMover>();
                var moverB = second.AddComponent<PlayerMover>();
                a = new PlayerControlContext(first);
                b = new PlayerControlContext(second);
                var airborne = typeof(PlayerMover).GetField("<IsAirborne>k__BackingField", Members);
                airborne.SetValue(moverA, true);
                scheme.Tick(a, new PlayerInputFrame { ActionPressed = true, AuxDirection = -1 });
                scheme.Tick(b, default);
                Assert.AreEqual(0, typeof(PlayerMover).GetField("laneJumpRequested", Members).GetValue(moverB));
                airborne.SetValue(moverA, false);
                scheme.Tick(a, new PlayerInputFrame { AuxDirection = 1 });
                Assert.AreEqual(-1, typeof(PlayerMover).GetField("laneJumpRequested", Members).GetValue(moverA));
                moverA.ClearPendingInput();
                scheme.Tick(a, default);
                Assert.AreEqual(0, typeof(PlayerMover).GetField("laneJumpRequested", Members).GetValue(moverA));
            }
            finally
            {
                if (a != null) scheme.Exit(a);
                if (b != null) scheme.Exit(b);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(scheme);
            }
        }
    }
}
