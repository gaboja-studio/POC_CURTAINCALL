using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitStimuliSafetyTests
    {
        GameObject root, actor;
        CapsuleCollider body;
        static readonly Vector3 Origin = new(28000, 0, 28000);
        [SetUp] public void Setup()
        {
            root = new GameObject("stimuli safety fixture");
            actor = new GameObject("actor"); actor.transform.SetParent(root.transform); actor.transform.position = Origin;
            body = actor.AddComponent<CapsuleCollider>(); body.enabled = false;
            body.radius = .24f; body.height = 1.72f; body.center = Vector3.up * .94f;
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        Collider Box(Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(root.transform);
            go.transform.position = Origin + position; go.transform.localScale = size; Physics.SyncTransforms();
            return go.GetComponent<Collider>();
        }
        bool Resolve(out Vector3 result) => PursuitSeparation.TryResolve(body, Origin, actor.transform, 3, false, out result);
        [Test] public void DoorNearWallUsesFreeSideWithoutChangingFloor()
        {
            Box(new Vector3(0, -.25f, 0), new Vector3(10, .5f, 10));
            Box(new Vector3(0, 1, 0), new Vector3(4, 2, .2f));
            Box(new Vector3(0, 1, -.4f), new Vector3(6, 2, .1f));
            Assert.IsTrue(Resolve(out var result));
            Assert.Greater(result.z, Origin.z); Assert.AreEqual(Origin.y, result.y);
            Assert.IsTrue(PursuitSeparation.Clear(body, result, actor.transform));
        }
        [Test] public void WideTrampolineWithVerticalPenetrationUsesHorizontalExit()
        {
            Box(new Vector3(0, -.25f, 0), new Vector3(10, .5f, 10));
            Box(new Vector3(0, 1, 0), new Vector3(3, .3f, 3));
            Assert.IsTrue(Resolve(out var result)); Assert.AreEqual(0, result.y);
            Assert.Greater((result - Origin).magnitude, 1.5f);
            Assert.IsTrue(PursuitSeparation.Clear(body, result, actor.transform));
        }
        [Test] public void EnclosedPursuerCannotTeleportAcrossSeparateWall()
        {
            Box(new Vector3(0, -.25f, 0), new Vector3(10, .5f, 10));
            Box(new Vector3(0, 1, 0), new Vector3(1.6f, 2, 1.6f));
            Box(new Vector3(0, 1, 1), new Vector3(2.2f, 2, .1f));
            Box(new Vector3(0, 1, -1), new Vector3(2.2f, 2, .1f));
            Box(new Vector3(1, 1, 0), new Vector3(.1f, 2, 2.2f));
            Box(new Vector3(-1, 1, 0), new Vector3(.1f, 2, 2.2f));
            Assert.IsFalse(Resolve(out var result)); Assert.AreEqual(Origin, result);
        }
        [Test] public void GroundedPursuerCannotBePushedOffUnsupportedPlatform()
        {
            Box(new Vector3(0, -.25f, 0), new Vector3(.5f, .5f, .5f));
            Box(new Vector3(0, 1, 0), new Vector3(2, .3f, 2));
            Assert.IsFalse(Resolve(out _));
        }
        [TestCase(PursuitNoiseKind.SawFire, 40)]
        [TestCase(PursuitNoiseKind.CannonFire, 40)]
        [TestCase(PursuitNoiseKind.Door, 40)]
        [TestCase(PursuitNoiseKind.SawGrab, 24)]
        [TestCase(PursuitNoiseKind.TrampolineBounce, 24)]
        [TestCase(PursuitNoiseKind.VentOpen, 24)]
        [TestCase(PursuitNoiseKind.Television, 24)]
        [TestCase(PursuitNoiseKind.None, 0)]
        public void EventsUseConfiguredNoiseTier(PursuitNoiseKind kind, float expected)
        {
            var rules = new CurtainCall.Settings.PursuitSettings { LoudNoiseRadius = 24, MaximumNoiseRadius = 40 };
            Assert.AreEqual(expected, PursuitStimuli.Radius(kind, rules));
        }
    }
}
