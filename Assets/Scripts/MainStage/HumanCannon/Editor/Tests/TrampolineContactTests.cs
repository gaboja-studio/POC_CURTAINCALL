using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class TrampolineContactTests
    {
        [Test]
        public void FastDescent_CatchesCrossingBetweenFrames()
        {
            Assert.IsTrue(TrampolineContact.TryCross(new Vector3(0, 20, 0), new Vector3(0, -20, 0),
                Vector3.zero, Vector3.zero, Vector2.one, 0.3f, out var contact));
            Assert.That(contact, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MovingSurface_UsesBothPositionsAtTimeOfCrossing()
        {
            Assert.IsTrue(TrampolineContact.TryCross(new Vector3(0, 4, 0), new Vector3(4, -4, 0),
                Vector3.zero, new Vector3(4, 0, 0), Vector2.one * 0.5f, 0f, out var contact));
            Assert.That(contact, Is.EqualTo(new Vector3(2, 0, 0)));
        }

        [Test]
        public void RisingSurface_CatchesDescendingPlayerAboveItsPreviousHeight()
        {
            Assert.IsTrue(TrampolineContact.TryCross(new Vector3(0, 3, 0), new Vector3(0, 1, 0),
                Vector3.zero, new Vector3(0, 2, 0), Vector2.one, 0.3f, out var contact));
            Assert.That(contact, Is.EqualTo(new Vector3(0, 1.5f, 0)));
        }

        [TestCase(1.25f, true)]
        [TestCase(1.26f, false)]
        public void Edge_IncludesBodyRadiusButRejectsOutside(float offset, bool expected)
        {
            Assert.That(TrampolineContact.TryCross(new Vector3(offset, 1, offset), new Vector3(offset, -1, offset),
                Vector3.zero, Vector3.zero, Vector2.one, 0.25f, out _), Is.EqualTo(expected));
        }

        [Test]
        public void HorizontalExitBeforeCrossing_DoesNotCatch()
        {
            Assert.IsFalse(TrampolineContact.TryCross(new Vector3(0, 2, 0), new Vector3(4, -2, 0),
                Vector3.zero, Vector3.zero, Vector2.one * 0.5f, 0.25f, out _));
        }

        [Test]
        public void UpwardPassOrRemainingBelow_DoesNotCatchAgain()
        {
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.down, Vector3.up,
                Vector3.zero, Vector3.zero, Vector2.one, 0.3f, out _));
            Assert.IsTrue(TrampolineContact.TryCross(Vector3.up, Vector3.down,
                Vector3.zero, Vector3.zero, Vector2.one, 0.3f, out _));
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.down, Vector3.down * 2f,
                Vector3.zero, Vector3.zero, Vector2.one, 0.3f, out _));
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.zero, Vector3.zero,
                Vector3.zero, Vector3.zero, Vector2.one, 0.3f, out _));
        }

        [Test]
        public void EmptySurfaceOrNonFiniteInput_IsRejected()
        {
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.up, Vector3.down,
                Vector3.zero, Vector3.zero, Vector2.zero, 0.3f, out _));
            Assert.IsFalse(TrampolineContact.TryCross(new Vector3(float.NaN, 1, 0), Vector3.down,
                Vector3.zero, Vector3.zero, Vector2.one, 0.3f, out _));
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.up, Vector3.down,
                Vector3.zero, new Vector3(0, float.PositiveInfinity, 0), Vector2.one, 0.3f, out _));
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.up, Vector3.down,
                Vector3.zero, Vector3.zero, Vector2.one, float.NaN, out _));
        }

        [Test]
        public void RotatedRectangle_UsesDeviceAxesInsteadOfWorldAxes()
        {
            Vector3 from = new Vector3(0, 1, 1.5f);
            Vector3 to = new Vector3(0, -1, 1.5f);
            Vector2 size = new Vector2(2f, 0.5f);
            Assert.IsFalse(TrampolineContact.TryCross(from, to, Vector3.zero, Vector3.zero, size, 0f, out _));
            Quaternion rotation = Quaternion.Euler(0, 90, 0);
            Assert.IsTrue(TrampolineContact.TryCross(from, to, Vector3.zero, Vector3.zero,
                rotation, rotation, size, 0f, out var contact));
            Assert.That(contact, Is.EqualTo(new Vector3(0, 0, 1.5f)));
        }

        [Test]
        public void TurningSurface_UsesRotationAtCrossingTime()
        {
            Vector3 point = Quaternion.Euler(0, 45, 0) * new Vector3(1.5f, 0, 0);
            Assert.IsTrue(TrampolineContact.TryCross(point + Vector3.up, point + Vector3.down,
                Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.Euler(0, 90, 0),
                new Vector2(2f, 0.5f), 0f, out var contact));
            Assert.That(Vector3.Distance(contact, point), Is.LessThan(0.0001f));
        }

        [Test]
        public void InvalidRotation_IsRejected()
        {
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.up, Vector3.down,
                Vector3.zero, Vector3.zero, new Quaternion(0, 0, 0, 0), Quaternion.identity,
                Vector2.one, 0f, out _));
            Assert.IsFalse(TrampolineContact.TryCross(Vector3.up, Vector3.down,
                Vector3.zero, Vector3.zero, Quaternion.identity, new Quaternion(0, float.NaN, 0, 1),
                Vector2.one, 0f, out _));
        }
    }
}
