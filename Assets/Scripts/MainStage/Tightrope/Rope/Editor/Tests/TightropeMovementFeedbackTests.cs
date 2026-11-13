using System.Reflection;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    [Category("CommonProgressionRegression")]
    public sealed class TightropeMovementFeedbackTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(false)] [TestCase(true)]
        public void FreeJumpFollowsCurrentMovementAndSprintInputs(bool releaseMovement)
        {
            var go = new GameObject("Running jump momentum") { hideFlags = HideFlags.DontSave };
            go.transform.position = new Vector3(8000, 100, 8000);
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = mover.FreeAirControl = true;
                mover.CourseForwardOverride = Vector3.forward;
                float speed = GameSettings.Base.AirControl.Speed;
                float takeoffSpeed = speed * GameSettings.Base.Character.SprintSpeedMultiplier;
                typeof(PlayerMover).GetField("groundVelocity", Members).SetValue(mover, Vector3.forward * takeoffSpeed);
                typeof(PlayerMover).GetMethod("StartJump", Members).Invoke(mover, new object[] { JumpKind.InPlace, 0 });
                for (int frame = 0; frame < 3; frame++)
                {
                    mover.SetMoveInput(0, releaseMovement ? 0 : 1);
                    mover.SetLocomotionInput(false, false);
                    typeof(PlayerMover).GetMethod("Update", Members).Invoke(mover, null);
                    var velocity = (Vector3)typeof(PlayerMover).GetField("airVelocity", Members).GetValue(mover);
                    Assert.That(Vector3.Distance(velocity, releaseMovement ? Vector3.zero : Vector3.forward * speed), Is.LessThan(.0001f));
                }
                mover.SetMoveInput(1, 0);
                typeof(PlayerMover).GetMethod("Update", Members).Invoke(mover, null);
                var turned = (Vector3)typeof(PlayerMover).GetField("airVelocity", Members).GetValue(mover);
                Assert.That(Vector3.Distance(turned, Vector3.right * speed), Is.LessThan(.0001f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(false)] [TestCase(true)]
        public void WalkingOffAnEdgeMovesOnlyWithHeldInput(bool held)
        {
            var go = new GameObject("Walk off edge momentum") { hideFlags = HideFlags.DontSave };
            go.transform.position = new Vector3(8000, 100, 8000);
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = mover.FreeAirControl = true;
                mover.CourseForwardOverride = Vector3.forward;
                var velocity = Vector3.forward * GameSettings.Base.AirControl.Speed;
                typeof(PlayerMover).GetField("groundVelocity", Members).SetValue(mover, velocity * GameSettings.Base.Character.SprintSpeedMultiplier);
                mover.SetMoveInput(0, held ? 1 : 0);
                typeof(PlayerMover).GetMethod("Update", Members).Invoke(mover, null);
                Assert.That(mover.CurrentJump, Is.EqualTo(JumpKind.Fall));
                Assert.That((Vector3)typeof(PlayerMover).GetField("airVelocity", Members).GetValue(mover), Is.EqualTo(held ? velocity : Vector3.zero));
                mover.ResetExternalMotion(true);
                Assert.That((Vector3)typeof(PlayerMover).GetField("airVelocity", Members).GetValue(mover), Is.EqualTo(Vector3.zero));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AirShiftDoesNotBoostSharedSpeedWithInjuryAndDiagonalLimit()
        {
            var go = new GameObject("Stationary jump steering") { hideFlags = HideFlags.DontSave };
            go.transform.position = new Vector3(8000, 100, 8000);
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = mover.FreeAirControl = true;
                mover.CourseForwardOverride = Vector3.forward;
                mover.SpeedMultiplier = .5f;
                mover.LaunchJump(0);
                mover.SetMoveInput(0, 1);
                typeof(PlayerMover).GetMethod("Update", Members).Invoke(mover, null);
                float speed = GameSettings.Base.AirControl.Speed * .5f;
                Assert.That(((Vector3)typeof(PlayerMover).GetField("airVelocity", Members).GetValue(mover)).magnitude, Is.EqualTo(speed).Within(.0001f));
                mover.SetMoveInput(1, 1); mover.SetLocomotionInput(true, false);
                typeof(PlayerMover).GetMethod("Update", Members).Invoke(mover, null);
                var velocity = (Vector3)typeof(PlayerMover).GetField("airVelocity", Members).GetValue(mover);
                Assert.That(velocity.magnitude, Is.EqualTo(speed).Within(.0001f));
                Assert.That(velocity.y, Is.Zero);
                Assert.That(mover.IsSprinting, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(1f)]
        [TestCase(.5f)]
        public void SprintJumpCarriesTakeoffSpeedWithoutAirShift(float injuryMultiplier)
        {
            var go = new GameObject("Sprint takeoff speed") { hideFlags = HideFlags.DontSave };
            go.transform.position = new Vector3(8000, 100, 8000);
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = mover.FreeAirControl = true;
                mover.CourseForwardOverride = Vector3.forward;
                mover.SpeedMultiplier = injuryMultiplier;
                mover.SetMoveInput(0, 1); mover.SetLocomotionInput(true, false);
                typeof(PlayerMover).GetMethod("UpdatePosture", Members).Invoke(mover, null);
                var ground = (Vector3)typeof(PlayerMover).GetMethod("ReadFreeMovement", Members).Invoke(mover, new object[] { Vector3.forward, 1f, 0f });
                typeof(PlayerMover).GetField("groundVelocity", Members).SetValue(mover, ground);
                typeof(PlayerMover).GetMethod("StartJump", Members).Invoke(mover, new object[] { JumpKind.InPlace, 0 });
                var velocity = typeof(PlayerMover).GetField("airVelocity", Members);
                var vertical = typeof(PlayerMover).GetField("verticalSpeed", Members);
                var update = typeof(PlayerMover).GetMethod("Update", Members);
                float baseAirSpeed = GameSettings.Base.AirControl.Speed * injuryMultiplier;
                Assert.That(((Vector3)velocity.GetValue(mover)).z, Is.EqualTo(ground.z).Within(.0001f));
                Assert.That(mover.IsSprinting, Is.False);

                vertical.SetValue(mover, mover.JumpSpeed * .5f);
                mover.SetMoveInput(0, 1); mover.SetLocomotionInput(false, false);
                update.Invoke(mover, null);
                float risingSpeed = ((Vector3)velocity.GetValue(mover)).z;
                Assert.That(risingSpeed, Is.GreaterThan(baseAirSpeed).And.LessThan(ground.z));

                vertical.SetValue(mover, mover.JumpSpeed * .5f);
                mover.SetMoveInput(0, 1); mover.SetLocomotionInput(true, false);
                update.Invoke(mover, null);
                Assert.That(((Vector3)velocity.GetValue(mover)).z, Is.EqualTo(risingSpeed).Within(.0001f));

                vertical.SetValue(mover, mover.JumpSpeed * .5f);
                mover.SetMoveInput(1, 0);
                update.Invoke(mover, null);
                Assert.That(((Vector3)velocity.GetValue(mover)).x, Is.EqualTo(baseAirSpeed).Within(.0001f));

                vertical.SetValue(mover, 0f);
                mover.SetMoveInput(0, 1); mover.SetLocomotionInput(true, false);
                update.Invoke(mover, null);
                Assert.That(((Vector3)velocity.GetValue(mover)).z, Is.EqualTo(baseAirSpeed).Within(.0001f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DirectionalDismountKeepsItsFirstStepThenUsesNormalAirInput()
        {
            var go = new GameObject("Directional dismount") { hideFlags = HideFlags.DontSave };
            go.transform.position = new Vector3(8000f, 100f, 8000f);
            try
            {
                var mover = go.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetMethod("Awake", Members).Invoke(mover, null);
                mover.FreeMovement = mover.FreeAirControl = true;
                mover.CourseForwardOverride = Vector3.forward;
                var launch = Vector3.right * 2f;
                typeof(PlayerMover).GetMethod("StartDirectionalJump", Members).Invoke(mover, new object[] { launch, true });
                typeof(PlayerMover).GetMethod("Update", Members).Invoke(mover, null);
                var velocity = typeof(PlayerMover).GetField("airVelocity", Members);
                Assert.That((Vector3)velocity.GetValue(mover), Is.EqualTo(launch));
                mover.SetMoveInput(-1f, 0f);
                typeof(PlayerMover).GetMethod("Update", Members).Invoke(mover, null);
                Assert.That((Vector3)velocity.GetValue(mover), Is.EqualTo(Vector3.left * GameSettings.Base.AirControl.Speed));
            }
            finally { Object.DestroyImmediate(go); }
        }

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
