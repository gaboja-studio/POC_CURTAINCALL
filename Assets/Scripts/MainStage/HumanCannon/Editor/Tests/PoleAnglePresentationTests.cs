using System;
using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public class PoleAnglePresentationTests
    {
        [TestCase(.05f)] [TestCase(.1f)]
        public void CorrectionsPreserveTheCurrentPoseAndConvergeWithoutChangingPhysics(float interval)
        {
            var view = new PoleAnglePresentation();
            view.Receive(0, 10, 1, false, interval);
            Assert.That(view.Sample(10, 1), Is.EqualTo(10));
            view.Receive(12, 18, 1.1, true, interval);
            Assert.That(view.Sample(18, 1.1), Is.EqualTo(12));
            Assert.That(view.Sample(20, 1.1 + interval / 2), Is.EqualTo(17).Within(.001f));
            Assert.That(view.Sample(22, 1.1 + interval), Is.EqualTo(22).Within(.001f));
            // An early packet continues from the actual displayed pose.
            view.Receive(17, 21, 1.12, true, interval);
            Assert.That(view.Sample(21, 1.12), Is.EqualTo(17));
            view.Receive(17, -20, 2, true, interval);
            Assert.That(view.Sample(-20, 2), Is.EqualTo(-20), "Reconnect snaps to current state.");
            view.Receive(-20, 0, 2.01, false, interval);
            Assert.That(view.Sample(0, 2.01), Is.Zero, "Release/regrab must not inherit correction.");
        }

        [Test]
        public void PerFramePresentationAllocatesNoManagedMemory()
        {
            var view = new PoleAnglePresentation();
            view.Receive(0, 10, 1, false, .05f);
            view.Sample(10, 1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            float angle = 0;
            for (int i = 0; i < 10000; i++)
            {
                double now = 1 + i / 120d;
                if (i % 6 == 0) view.Receive(angle, 10, now, true, .05f);
                angle = view.Sample(10, now);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(float.IsFinite(angle), Is.True);
        }
    }
}
