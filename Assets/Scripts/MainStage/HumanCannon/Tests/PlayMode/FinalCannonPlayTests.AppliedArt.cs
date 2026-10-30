using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator AppliedRoundTrampoline_ActualCenterJumpBouncesWithOriginalSpeedAndHeight() =>
            ProbeAppliedGroundBounce(Vector2.zero, true);

        [UnityTest]
        public IEnumerator AppliedRoundTrampoline_CurrentWholeDeviceContactIncludesOldFabricCorner() =>
            ProbeAppliedGroundBounce(new Vector2(1.9f, 1.9f), true);

        IEnumerator ProbeAppliedGroundBounce(Vector2 offset, bool expectBounce)
        {
            DisableEnhancementThreats();
            var trampoline = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current");
            Assert.IsTrue((bool)Get(trampoline, "CircularSurface"));
            trampoline.transform.SetPositionAndRotation(new Vector3(0, 0, 57), Quaternion.identity);
            Call(trampoline, "ResetPreviousPose");
            Call(mover, "SetStartPose", new Vector3(offset.x, .01f, 57 + offset.y), Quaternion.identity, true);
            yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "round trampoline probe grounded");
            yield return null;
            int before = (int)Get(trampoline, "LocalBounceCount");
            Call(mover, "RequestJump");
            yield return WaitUntil(() => (bool)Get(mover, "IsAirborne"), 2, "actual CharacterController jump");
            if (expectBounce)
            {
                yield return WaitUntil(() => (int)Get(trampoline, "LocalBounceCount") > before, 4, "real descent detected and accepted by LAN host");
                Assert.IsTrue(Flying);
                Assert.That(((Vector3)Get(pilot, "velocity")).y, Is.InRange(10.5f, 12.01f));
                float peak = player.transform.position.y;
                float deadline = Time.realtimeSinceStartup + 4;
                while (Flying && ((Vector3)Get(pilot, "velocity")).y > 0 && Time.realtimeSinceStartup < deadline)
                { peak = Mathf.Max(peak, player.transform.position.y); yield return null; }
                float gravity = (float)Get(Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "Character"), "Gravity");
                float expected = (float)Get(trampoline, "UpwardSpeed");
                expected = expected * expected / (2 * gravity);
                // Current TryTouch launches at the top of the device (including the grip),
                // rather than the old cloth-only crossing plane. The original art also did this.
                var contactBounds = (Bounds)Call(trampoline, "ContactBounds");
                float launchHeight = ((Vector3)Get(trampoline, "Surface")).y + contactBounds.max.y;
                Assert.That(peak - launchHeight, Is.EqualTo(expected).Within(.2f), "unchanged bounce trajectory above the actual device contact");
            }
            else
            {
                yield return WaitUntil(() => !(bool)Get(mover, "IsAirborne"), 4, "old square corner returns to ordinary floor");
                Assert.IsFalse(Flying);
                Assert.AreEqual(before, Get(trampoline, "LocalBounceCount"));
                AssertBody(0);
            }
        }

        [UnityTest]
        public IEnumerator AppliedMetalTransports_FitRoundLoadAndOpenBothVisualAndPhysicalDoors()
        {
            DisableEnhancementThreats();
            var trampoline = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current");
            for (int i = 0; i < 3; i++)
            {
                var transport = (Component)Call(route, "Transport", i);
                Call(transport, "Present", 0f);
                trampoline.transform.SetPositionAndRotation((Vector3)Get(transport, "StartPosition"), (Quaternion)Get(transport, "Rotation"));
                Call(trampoline, "ResetPreviousPose");
                Assert.IsTrue((bool)Call(transport, "ContainsLoad", trampoline), transport.name + " actual round frame and handle fit");
                var floors = ((Array)Get(transport, "trapColliders")).Cast<Collider>().ToArray();
                Assert.That(floors.Length, Is.EqualTo(2));
                foreach (var floor in floors) Assert.IsNotNull(floor.transform.Find("AppliedMetalFloor"));
                Call(transport, "PresentTrap", true, 0d, 1d);
                // The route owns the next frame's state; inspect this view command in the same frame.
                Assert.IsTrue(floors.All(f => !f.enabled));
                Call(transport, "PresentTrap", false, 0d, 1d);
                yield return null;
                Assert.IsTrue(floors.All(f => f.enabled));
                foreach (var floor in floors)
                {
                    var renderer = floor.transform.Find("AppliedMetalFloor").GetComponentInChildren<Renderer>();
                    Assert.That(Mathf.Abs(renderer.bounds.max.y - floor.bounds.max.y), Is.LessThan(.001f), "closed metal floor matches collider top");
                }
            }
        }
    }
}
