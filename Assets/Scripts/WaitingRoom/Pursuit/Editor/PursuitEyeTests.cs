using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitEyeTests
    {
        [Test]
        public void AwarenessUsesLocalThreeDimensionalDistanceAndHasFiniteRangeInAlwaysNearestStage()
        {
            var stage = new PursuitStage { SightRadius = 10, WalkHearingRadius = 6, RunHearingRadius = 14, AlwaysNearest = true };
            Assert.IsTrue(PursuitEyeTransition.InRange(Vector3.zero, Vector3.right * 14, stage));
            Assert.IsFalse(PursuitEyeTransition.InRange(Vector3.zero, Vector3.up * 14.01f, stage));
            Assert.IsFalse(PursuitEyeTransition.InRange(Vector3.zero, Vector3.forward * 100, stage));
        }

        [Test]
        public void PrepareIsYellowAndOnlyTheLocalTargetsChaseIsRed()
        {
            Assert.AreEqual(PursuitEyeState.Alert, PursuitEyeTransition.Resolve(PursuitBehaviour.Prepare, true, false));
            Assert.AreEqual(PursuitEyeState.Chase, PursuitEyeTransition.Resolve(PursuitBehaviour.Chase, true, false));
            Assert.AreEqual(PursuitEyeState.Normal, PursuitEyeTransition.Resolve(PursuitBehaviour.Chase, false, true));
            Assert.AreEqual(PursuitEyeState.Normal, PursuitEyeTransition.Resolve(PursuitBehaviour.Prepare, false, true));
            foreach (var behaviour in new[] { PursuitBehaviour.Listen, PursuitBehaviour.Investigate, PursuitBehaviour.Search })
                Assert.AreEqual(PursuitEyeState.Alert, PursuitEyeTransition.Resolve(behaviour, false, false));
            Assert.AreEqual(PursuitEyeState.Normal, PursuitEyeTransition.Resolve(PursuitBehaviour.Return, false, false));
        }

        [Test]
        public void FadeOpeningClosingAndInterruptedTransitionsStayContinuous()
        {
            var s = new PursuitEyeSettings(); var t = new PursuitEyeTransition();
            t.Tick(true, PursuitEyeState.Normal, s, .25f); Assert.AreEqual(.5f, t.Alpha, .0001f); Assert.AreEqual(0, t.Openness);
            t.Tick(true, PursuitEyeState.Normal, s, .25f); Assert.AreEqual(1, t.Alpha);
            t.Tick(true, PursuitEyeState.Alert, s, .25f); Assert.AreEqual(.5f, t.Openness, .0001f);
            var middleColor = t.Color;
            t.Tick(true, PursuitEyeState.Chase, s, 0); Assert.AreEqual(middleColor, t.Color, "Interrupted color must not snap.");
            t.Tick(true, PursuitEyeState.Chase, s, .5f); Assert.AreEqual(s.ChaseColor, t.Color); Assert.AreEqual(s.ChaseEmission, t.Emission);
            t.Tick(true, PursuitEyeState.Alert, s, .5f); Assert.AreEqual(s.AlertColor, t.Color); Assert.AreEqual(1, t.Openness);
            t.Tick(true, PursuitEyeState.Normal, s, .5f); Assert.AreEqual(s.NormalColor, t.Color); Assert.AreEqual(0, t.Openness);
            t.Tick(false, PursuitEyeState.Normal, s, .25f); Assert.AreEqual(.5f, t.Alpha, .0001f);
            t.Tick(false, PursuitEyeState.Normal, s, .25f); Assert.AreEqual(0, t.Alpha);
            t.Reset(); Assert.AreEqual(0, t.Alpha); Assert.AreEqual(0, t.Openness);
        }

        [Test]
        public void DesignerChangesUpdateHeldStatesAndZeroDurationIsSupported()
        {
            var s = new PursuitEyeSettings { TransitionSeconds = 0, FadeSeconds = 0 };
            var t = new PursuitEyeTransition(); t.Tick(true, PursuitEyeState.Chase, s, 0);
            Assert.AreEqual(1, t.Alpha); Assert.AreEqual(1, t.Openness);
            s.ChaseColor = Color.magenta; s.ChaseEmission = 2.5f;
            t.Tick(true, PursuitEyeState.Chase, s, 0);
            Assert.AreEqual(s.ChaseColor, t.Color); Assert.AreEqual(2.5f, t.Emission);
        }

        [Test]
        public void ImportedFramesAndShaderAreAvailableInPlayerResources()
        {
            var sprites = Resources.LoadAll<Sprite>("PursuitEye/Frames"); Assert.AreEqual(8, sprites.Length);
            foreach (var sprite in sprites) { Assert.AreEqual(new Vector2(256,256), sprite.rect.size); Assert.AreEqual(new Vector2(128,128), sprite.pivot); }
            var shader = Resources.Load<Shader>("PursuitEye/PursuitEye"); Assert.IsNotNull(shader);
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader), "Eye shader must compile for the active pipeline.");
        }
    }
}
