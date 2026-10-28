using System.Reflection;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeMovementFeedbackTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void RopeTuningUsesConfiguredHeightAndDamageThenRestoresOnExit()
        {
            var go = new GameObject("Rope movement test");
            var scheme = ScriptableObject.CreateInstance<TightropeControlScheme>();
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = false;
                mover.SpeedMultiplier = 0.8f;
                mover.JumpHeightMultiplier = 0.75f;
                float previousAirTime = mover.AirTime;
                var context = new PlayerControlContext(go);
                scheme.Enter(context);
                float gravity = GameSettings.Base.Character.Gravity * GameSettings.Tightrope.RopeMovement.JumpGravityMultiplier;
                float height = GameSettings.Tightrope.RopeMovement.JumpHeight;
                if (height <= 0f) height = GameSettings.Base.Character.JumpHeight;
                Assert.That(mover.CourseSpeedOverride, Is.EqualTo(GameSettings.Tightrope.RopeMovement.MoveSpeed));
                Assert.That(mover.CourseJumpHeightOverride, Is.EqualTo(GameSettings.Tightrope.RopeMovement.JumpHeight));
                Assert.That(mover.SpeedMultiplier, Is.EqualTo(0.8f), "부상 이동 배율은 보존한다.");
                Assert.That(mover.JumpHeightMultiplier, Is.EqualTo(0.75f), "외줄 높이 설정이 부상 배율을 덮지 않는다.");
                Assert.That(mover.JumpSpeed * mover.JumpSpeed / (2f * gravity),
                    Is.EqualTo(height * 0.75f).Within(0.0001f));
                Assert.That(mover.AirTime, Is.EqualTo(2f * Mathf.Sqrt(2f * height * 0.75f / gravity)).Within(0.0001f));
                scheme.Exit(context);
                Assert.That(mover.CourseSpeedOverride, Is.Zero);
                Assert.That(mover.CourseJumpHeightOverride, Is.Zero);
                Assert.That(mover.CourseJumpGravityMultiplier, Is.EqualTo(1f));
                Assert.That(mover.AirTime, Is.EqualTo(previousAirTime).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(scheme); }
        }

        [Test]
        public void AirborneSchemeAndDamageChangesDoNotAlterHeightGravityOrLaneTravelTime()
        {
            var go = new GameObject("Frozen trajectory test");
            var scheme = ScriptableObject.CreateInstance<TightropeControlScheme>();
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = false;
                var context = new PlayerControlContext(go);
                scheme.Enter(context);
                mover.CourseJumpGravityMultiplier = 1.35f;
                mover.CourseJumpHeightOverride = 1.2f;
                mover.JumpHeightMultiplier = 0.75f;
                float plannedAirTime = mover.AirTime;
                float plannedSpeed = mover.JumpSpeed;
                float plannedGravity = mover.JumpGravity;
                typeof(PlayerMover).GetMethod("StartJump", Members).Invoke(mover, new object[] { JumpKind.Lane, 1 });
                scheme.Exit(context);
                mover.CourseJumpHeightOverride = 2f;
                mover.JumpHeightMultiplier = 0.25f;
                mover.FreeMovement = true;
                Assert.That(mover.AirTime, Is.EqualTo(plannedAirTime).Within(0.0001f));
                Assert.That(mover.JumpSpeed, Is.EqualTo(plannedSpeed).Within(0.0001f));
                Assert.That(mover.JumpGravity, Is.EqualTo(plannedGravity));
                Assert.That(mover.JumpSpeed * mover.JumpSpeed / (2f * mover.JumpGravity), Is.EqualTo(0.9f).Within(0.0001f));
                Vector3 velocity = (Vector3)typeof(PlayerMover).GetField("airVelocity", Members).GetValue(mover);
                Assert.That(Vector3.Dot(velocity, mover.CourseRight) * mover.AirTime, Is.EqualTo(mover.LaneSpacing).Within(0.0001f));
                typeof(PlayerMover).GetMethod("ClearJump", Members).Invoke(mover, null);
                Assert.That(mover.JumpGravity, Is.EqualTo(GameSettings.Base.Character.Gravity));
                Assert.That(mover.JumpSpeed * mover.JumpSpeed / (2f * mover.JumpGravity),
                    Is.EqualTo(GameSettings.Base.Character.JumpHeight * 0.25f).Within(0.0001f), "착지 후 플랫폼은 공용 높이와 현재 부상 배율을 쓴다.");
                mover.FreeMovement = false;
                Assert.That(mover.JumpSpeed * mover.JumpSpeed / (2f * mover.JumpGravity),
                    Is.EqualTo(2f * 0.25f).Within(0.0001f), "다음 외줄 점프에는 변경된 높이가 반영된다.");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(scheme); }
        }

        [Test]
        public void PlatformJumpIgnoresRopeHeightAndGravityWhileKeepingInjuryMultiplier()
        {
            var go = new GameObject("Platform jump test") { hideFlags = HideFlags.DontSave };
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = true;
                mover.CourseJumpHeightOverride = 4f;
                mover.CourseJumpGravityMultiplier = 3f;
                mover.JumpHeightMultiplier = 0.5f;
                mover.LaunchJump(0f);
                Assert.That(mover.JumpGravity, Is.EqualTo(GameSettings.Base.Character.Gravity));
                Assert.That(mover.JumpSpeed * mover.JumpSpeed / (2f * mover.JumpGravity),
                    Is.EqualTo(GameSettings.Base.Character.JumpHeight * 0.5f).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void UnsetRopeHeightUsesCommonHeight(float heightOverride)
        {
            var go = new GameObject("Common jump fallback test") { hideFlags = HideFlags.DontSave };
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = false;
                mover.CourseJumpHeightOverride = heightOverride;
                mover.JumpHeightMultiplier = 0.75f;
                Assert.That(mover.JumpSpeed * mover.JumpSpeed / (2f * mover.JumpGravity),
                    Is.EqualTo(GameSettings.Base.Character.JumpHeight * 0.75f).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PiggybackHopAppliesConfiguredHeightMultiplierAndRopeGravity()
        {
            var go = new GameObject("Piggyback hop test");
            var system = new GameObject("Piggyback hop follow");
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                var player = go.AddComponent<CurtainCall.Network.PlayerSync.NetworkPlayer>();
                mover.FreeMovement = false;
                mover.CourseJumpGravityMultiplier = 1.35f;
                mover.CourseJumpHeightOverride = 1.2f;
                mover.JumpHeightMultiplier = 0.75f;
                var follow = system.AddComponent<PiggybackFollow>();
                typeof(PiggybackFollow).GetField("localMover", Members).SetValue(follow, mover);
                typeof(PiggybackFollow).GetMethod("StartHop", Members).Invoke(follow, new object[] { player });
                float speed = (float)typeof(PiggybackFollow).GetField("hopSpeed", Members).GetValue(follow);
                float gravity = (float)typeof(PiggybackFollow).GetField("hopGravity", Members).GetValue(follow);
                Assert.That(speed * speed / (2f * gravity), Is.EqualTo(1.2f * 0.75f * GameSettings.Tightrope.Piggyback.JumpHeightMultiplier).Within(0.0001f));
                Assert.That(gravity, Is.EqualTo(mover.JumpGravity));
            }
            finally { Object.DestroyImmediate(system); Object.DestroyImmediate(go); }
        }
    }
}
