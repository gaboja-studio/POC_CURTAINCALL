using System.Collections.Generic;
using CurtainCall.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeFramingTests
    {
        static readonly TightropeFramingMath.Frame Baseline = new(3.75f, 4.5f, 1.2f, 1.75f);
        static readonly Bounds Solo = new(new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 1.7f, 0.6f));

        [Test]
        public void IsolatedPlayer_KeepsExistingFraming()
        {
            var frame = Fit(Solo);
            Assert.That(frame.Distance, Is.EqualTo(Baseline.Distance));
            Assert.That(frame.Height, Is.EqualTo(Baseline.Height));
            Assert.That(frame.LookHeight, Is.EqualTo(Baseline.LookHeight));
            Assert.That(frame.LookAhead, Is.EqualTo(Baseline.LookAhead));
        }

        [Test]
        public void TallStack_RaisesLookAndFitsAllBodies()
        {
            var stack = new Bounds(new Vector3(0f, 3.4f, 0f), new Vector3(0.6f, 6.8f, 0.6f));
            var frame = Fit(stack);
            Assert.That(frame.LookHeight, Is.GreaterThan(Baseline.LookHeight));
            Assert.That(frame.Distance, Is.GreaterThan(Baseline.Distance));
            stack.Expand(1f);
            Assert.That(TightropeFramingMath.Fits(stack, Vector3.zero, Quaternion.identity, Vector3.forward, frame, 60f, 16f / 9f), Is.True);
        }

        [Test]
        public void TopRider_CanLookDownAtTheSameStack()
        {
            var stack = new Bounds(new Vector3(0f, 3.4f, 0f), new Vector3(0.6f, 6.8f, 0.6f));
            var bottom = Fit(stack);
            var top = TightropeFramingMath.Fit(stack, Vector3.up * 4.5f, Quaternion.identity, Vector3.forward,
                Baseline, 60f, 16f / 9f, 12f, 0.5f, 2f);
            Assert.That(top.LookHeight, Is.LessThan(0f), "맨 위 사람만 보고 아래층을 자르지 않는다");
            Assert.That(top.Distance, Is.EqualTo(bottom.Distance).Within(0.001f));
            Assert.That(top.Height + 4.5f, Is.EqualTo(bottom.Height).Within(0.001f));
        }

        [Test]
        public void NarrowAspect_WidensEnoughForNearbyLaneCompanion()
        {
            var group = Solo;
            group.Encapsulate(new Bounds(new Vector3(3f, 0.85f, 0f), Solo.size));
            var wide = Fit(group, 16f / 9f);
            var narrow = Fit(group, 0.65f);
            Assert.That(narrow.Distance, Is.GreaterThan(wide.Distance));
            group.Expand(1f);
            Assert.That(TightropeFramingMath.Fits(group, Vector3.zero, Quaternion.identity, Vector3.forward, narrow, 60f, 0.65f), Is.True);
        }

        [Test]
        public void ForwardObstacle_LookAheadIsLimited()
        {
            var group = Solo;
            group.Encapsulate(new Bounds(new Vector3(0f, 0.5f, 8f), Vector3.one * 1.2f));
            var frame = Fit(group);
            Assert.That(frame.LookAhead, Is.InRange(Baseline.LookAhead, Baseline.LookAhead + 2f));
            Assert.That(frame.Distance, Is.LessThanOrEqualTo(12f));
        }

        [Test]
        public void NarrowFieldOfView_FitsActualCameraViewport()
        {
            var bounds = new Bounds(new Vector3(1.5f, 0.85f, 0f), new Vector3(3.6f, 1.7f, 0.6f));
            var frame = TightropeFramingMath.Fit(bounds, Vector3.zero, Quaternion.identity, Vector3.forward,
                Baseline, 10f, 16f / 9f, 50f, 0.5f, 2f);
            bounds.Expand(1f);
            var go = new GameObject("Framing viewport test");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 10f;
                camera.aspect = 16f / 9f;
                go.transform.position = Vector3.up * frame.Height - Vector3.forward * frame.Distance;
                go.transform.LookAt(new Vector3(0f, frame.LookHeight, frame.LookAhead));
                for (int i = 0; i < 8; i++)
                {
                    var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                    var viewport = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                    Assert.That(viewport.z, Is.GreaterThan(0f));
                    Assert.That(viewport.x, Is.InRange(0f, 1f));
                    Assert.That(viewport.y, Is.InRange(0f, 1f));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DistantSubjects_CannotForceUnlimitedZoom()
        {
            var frame = Fit(new Bounds(Vector3.zero, Vector3.one * 100f));
            Assert.That(frame.Distance, Is.EqualTo(12f));
            Assert.That(TightropeFramingMath.IsNearby(Vector3.zero, Vector3.right * 20f, 6f, false), Is.False);
            Assert.That(TightropeFramingMath.IsNearby(Vector3.zero, Vector3.up * 20f, 6f, false), Is.False);
        }

        [Test]
        public void NearbyFilter_IncludesAdjacentLaneAndWholeStack()
        {
            Assert.That(TightropeFramingMath.IsNearby(Vector3.zero, Vector3.right * 3f, 6f, false), Is.True);
            Assert.That(TightropeFramingMath.IsNearby(Vector3.zero, Vector3.up * 6.5f, 6f, true), Is.True);
        }

        [Test]
        public void RotatedCourse_UsesTheSameRelativeFraming()
        {
            var group = new Bounds(new Vector3(1.5f, 0.85f, 0f), new Vector3(3.6f, 1.7f, 0.6f));
            var rotation = Quaternion.Euler(0f, 90f, 0f);
            var rotated = new Bounds(rotation * group.center, new Vector3(group.size.z, group.size.y, group.size.x));
            var forward = Fit(group, 0.65f);
            var sideways = TightropeFramingMath.Fit(rotated, Vector3.zero, rotation, rotation * Vector3.forward,
                Baseline, 60f, 0.65f, 12f, 0.5f, 2f);
            Assert.That(sideways.Distance, Is.EqualTo(forward.Distance).Within(0.001f));
            Assert.That(sideways.Height, Is.EqualTo(forward.Height).Within(0.001f));
        }

        [Test]
        public void Smoothing_IsGradualAndIndependentOfFrameRate()
        {
            var target = new TightropeFramingMath.Frame(6f, 10f, 3f, 3.75f);
            var once = TightropeFramingMath.Smooth(Baseline, target, 0.45f, 0.1f);
            var twice = TightropeFramingMath.Smooth(TightropeFramingMath.Smooth(Baseline, target, 0.45f, 0.05f), target, 0.45f, 0.05f);
            Assert.That(once.Distance, Is.GreaterThan(Baseline.Distance).And.LessThan(target.Distance));
            Assert.That(twice.Distance, Is.EqualTo(once.Distance).Within(0.0001f));
            Assert.That(twice.LookHeight, Is.EqualTo(once.LookHeight).Within(0.0001f));
        }

        [Test]
        public void Smoothing_ZeroElapsedTimeDoesNotAdvance()
        {
            var target = new TightropeFramingMath.Frame(6f, 10f, 3f, 3.75f);
            var frame = TightropeFramingMath.Smooth(Baseline, target, 0.45f, 0f);
            Assert.That(frame.Distance, Is.EqualTo(Baseline.Distance));
            Assert.That(frame.LookHeight, Is.EqualTo(Baseline.LookHeight));
        }

        [Test]
        public void SameLayer_NearbyPlayersShareGroupCenterRegardlessOfLocalPlayerOrInputOrder()
        {
            var players = new[]
            {
                new TightropeFramingMath.LayerSubject(30, 2, new Vector3(3f, 1.5f, 4f)),
                new TightropeFramingMath.LayerSubject(10, 2, new Vector3(0f, 1.5f, 2f)),
                new TightropeFramingMath.LayerSubject(20, 1, new Vector3(0f, 0f, 2f))
            };
            var first = new List<ulong>();
            var second = new List<ulong>();
            Assert.That(TightropeFramingMath.SharedLayerGroup(players, 10, 6f, first, out var a), Is.True);
            System.Array.Reverse(players);
            Assert.That(TightropeFramingMath.SharedLayerGroup(players, 30, 6f, second, out var b), Is.True);
            Assert.That(first, Is.EqualTo(new ulong[] { 10, 30 }));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(a, Is.EqualTo(new Vector3(1.5f, 1.5f, 3f)));
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void SharedLayer_DoesNotChainNearbyPlayersAcrossTheWholeCourse()
        {
            var players = new[]
            {
                new TightropeFramingMath.LayerSubject(1, 1, Vector3.zero),
                new TightropeFramingMath.LayerSubject(2, 1, Vector3.forward * 5f),
                new TightropeFramingMath.LayerSubject(3, 1, Vector3.forward * 10f)
            };
            var members = new List<ulong>();
            Assert.That(TightropeFramingMath.SharedLayerGroup(players, 2, 6f, members, out _), Is.True);
            Assert.That(members, Is.EqualTo(new ulong[] { 1, 2 }));
            Assert.That(TightropeFramingMath.SharedLayerGroup(players, 3, 6f, members, out var solo), Is.False);
            Assert.That(members, Is.EqualTo(new ulong[] { 3 }));
            Assert.That(solo, Is.EqualTo(Vector3.forward * 10f));
        }

        [Test]
        public void SharedLayer_DifferentFloorsAndDistantCompanionsKeepSoloAnchor()
        {
            var players = new[]
            {
                new TightropeFramingMath.LayerSubject(1, 1, Vector3.zero),
                new TightropeFramingMath.LayerSubject(2, 2, Vector3.up * 1.5f),
                new TightropeFramingMath.LayerSubject(3, 1, Vector3.right * 20f)
            };
            var members = new List<ulong>();
            Assert.That(TightropeFramingMath.SharedLayerGroup(players, 1, 6f, members, out var anchor), Is.False);
            Assert.That(members, Is.EqualTo(new ulong[] { 1 }));
            Assert.That(anchor, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void SharedLayer_DespawnedOrFilteredFocusDoesNotReusePreviousGroup()
        {
            var members = new List<ulong> { 1, 2 };
            Assert.That(TightropeFramingMath.SharedLayerGroup(new[]
            {
                new TightropeFramingMath.LayerSubject(2, 1, Vector3.right * 3f)
            }, 1, 6f, members, out _), Is.False);
            Assert.That(members, Is.Empty);
        }

        [Test]
        public void SharedLayer_UsesSameWorldPoseAndLookTargetForBothMembers()
        {
            var members = new List<ulong>();
            var players = new[]
            {
                new TightropeFramingMath.LayerSubject(1, 2, new Vector3(0f, 1.5f, 2f)),
                new TightropeFramingMath.LayerSubject(2, 2, new Vector3(3f, 1.5f, 4f))
            };
            var bounds = new Bounds(new Vector3(1.5f, 2.35f, 3f), new Vector3(3.6f, 1.7f, 2.6f));
            TightropeFramingMath.SharedLayerGroup(players, 1, 6f, members, out var first);
            TightropeFramingMath.SharedLayerGroup(players, 2, 6f, members, out var second);
            var a = TightropeFramingMath.Fit(bounds, first, Quaternion.identity, Vector3.forward,
                Baseline, 60f, 16f / 9f, 12f, 0.5f, 2f);
            var b = TightropeFramingMath.Fit(bounds, second, Quaternion.identity, Vector3.forward,
                Baseline, 60f, 16f / 9f, 12f, 0.5f, 2f);
            var poseA = TightropeFramingMath.CameraPose(first, Quaternion.identity, Vector3.forward, a, out var lookA);
            var poseB = TightropeFramingMath.CameraPose(second, Quaternion.identity, Vector3.forward, b, out var lookB);
            Assert.That(poseA.position, Is.EqualTo(poseB.position));
            Assert.That(Quaternion.Angle(poseA.rotation, poseB.rotation), Is.LessThan(0.001f));
            Assert.That(lookA, Is.EqualTo(lookB));
            bounds.Expand(1f);
            Assert.That(TightropeFramingMath.Fits(bounds, first, Quaternion.identity, Vector3.forward, a, 60f, 16f / 9f), Is.True);
        }

        [Test]
        public void SharedLayer_SpectatorFocusSelectsItsLayerRatherThanTheLocalPlayersLayer()
        {
            var players = new[]
            {
                new TightropeFramingMath.LayerSubject(1, 1, Vector3.zero),
                new TightropeFramingMath.LayerSubject(2, 2, new Vector3(0f, 1.5f, 0f)),
                new TightropeFramingMath.LayerSubject(3, 2, new Vector3(3f, 1.5f, 0f))
            };
            var members = new List<ulong>();
            Assert.That(TightropeFramingMath.SharedLayerGroup(players, 2, 6f, members, out var anchor), Is.True);
            Assert.That(members, Is.EqualTo(new ulong[] { 2, 3 }));
            Assert.That(anchor, Is.EqualTo(new Vector3(1.5f, 1.5f, 0f)));
        }

        static TightropeFramingMath.Frame Fit(Bounds group, float aspect = 16f / 9f) => TightropeFramingMath.Fit(
            group, Vector3.zero, Quaternion.identity, Vector3.forward, Baseline, 60f, aspect, 12f, 0.5f, 2f);
    }
}
