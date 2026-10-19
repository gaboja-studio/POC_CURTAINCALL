using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeSwingGapTests
    {
        [Test]
        public void GapOnlyAppliesToCoursesLongEnoughForBothApproaches()
        {
            Assert.IsTrue(SwingPoleMath.HasGap(330f, 128f, 8f));
            Assert.IsFalse(SwingPoleMath.HasGap(100f, 128f, 8f));
            Assert.IsFalse(SwingPoleMath.HasGap(330f, float.NaN, 8f));
            Assert.IsTrue(SwingPoleMath.Contains(128f, 128f, 8f));
            Assert.IsFalse(SwingPoleMath.Contains(136f, 128f, 8f));
        }

        [Test]
        public void ClearingFireDoesNotRestoreTheStructuralGapAndHostShapeKeepsIt()
        {
            string before = GameSettings.Tightrope.Course.ToJson();
            var go = new GameObject("Swing gap query test");
            try
            {
                JsonUtility.FromJsonOverwrite("{\"finishDistance\":330,\"laneCount\":6,\"swingGapEnabled\":true,\"relayGapEnabled\":false,\"trapezeEnabled\":false,\"swingGapLanes\":[2,5],\"swingGapStart\":128,\"swingGapLength\":8}", GameSettings.Tightrope.Course);
                var course = go.AddComponent<TightropeCourse>();
                Assert.IsTrue(course.HasSwingGap);
                Assert.IsFalse(course.IsLaneUsable(1, 132f));
                Assert.IsFalse(course.IsLaneUsable(4, 132f));
                Assert.IsTrue(course.IsLaneUsable(0, 132f), "봉을 배치하지 않은 줄은 끊어지지 않는다.");
                Assert.IsTrue(course.IsLaneUsable(1, 136f));
                course.BlockSegment(1, 10f, 20f);
                course.ClearBlockedSegments();
                Assert.That(course.GetUsableSpans(1), Is.EqualTo(new[] { new Vector2(0f, 128f), new Vector2(136f, 330f) }));
                var shared = TightropeSettings.CourseSettings.FromJson(course.ExportShape());
                Assert.IsTrue(shared.SwingGapEnabled); Assert.That(shared.SwingGapStart, Is.EqualTo(128f));
                Assert.That(shared.SwingGapLength, Is.EqualTo(8f));
                Assert.That(shared.SwingGapLanes, Is.EqualTo(new[] { 2, 5 }));
            }
            finally { Object.DestroyImmediate(go); JsonUtility.FromJsonOverwrite(before, GameSettings.Tightrope.Course); }
        }

        [TestCase(30)]
        [TestCase(120)]
        public void SharedCannonBallisticStepKeepsGravityAndFrameOnlyAirCorrection(int fps)
        {
            Vector3 velocity = new(0f, 6f, 4f), position = Vector3.zero;
            float gravity = GameSettings.Base.Character.Gravity;
            for (int i = 0; i < fps; i++) position += BallisticMotion.Step(ref velocity, Vector3.right * 3f, gravity, 1f / fps);
            Assert.That(position.x, Is.EqualTo(3f).Within(.001f));
            Assert.That(position.y, Is.EqualTo(6f - .5f * gravity).Within(.001f));
            Assert.That(position.z, Is.EqualTo(4f).Within(.001f));
            Assert.That(velocity.x, Is.Zero, "공중 보정을 발사 관성에 누적하지 않는다.");
            Assert.That(GameSettings.Tightrope.RopeMovement.JumpGravityMultiplier, Is.EqualTo(1f));
            Assert.That(GameSettings.Tightrope.RopeMovement.JumpHeight, Is.EqualTo(GameSettings.Base.Character.JumpHeight * 1.25f));
        }
    }
}
