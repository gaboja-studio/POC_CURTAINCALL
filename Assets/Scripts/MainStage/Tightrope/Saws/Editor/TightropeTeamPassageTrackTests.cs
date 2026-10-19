using NUnit.Framework;

namespace CurtainCall.Tightrope.Saws.EditorTools.Tests
{
    public sealed class TightropeTeamPassageTrackTests
    {
        [Test]
        public void TeamPassagePauseFreezesMotionAndResumesWithoutPositionJump()
        {
            var track = new HorizontalSawTrack(3f, 10f, true);
            track.Start(0f);
            track.Pause(2f, 6f);
            Assert.That(track.GetSide(2f), Is.EqualTo(-4f).Within(0.0001f));
            Assert.That(track.GetSide(7.99f), Is.EqualTo(-4f).Within(0.0001f));
            Assert.IsFalse(track.IsMoving(7.99f));
            Assert.That(track.GetSide(8f), Is.EqualTo(-4f).Within(0.0001f));
            Assert.IsTrue(track.IsMoving(8f));
            Assert.That(track.GetSide(8.1f), Is.EqualTo(-3.7f).Within(0.0001f));
        }

        [Test]
        public void TeamPassageOverlappingPausesDoNotDoubleSubtractTime()
        {
            var track = new HorizontalSawTrack(3f, 10f, true);
            track.Start(0f);
            track.Pause(2f, 6f);
            track.Pause(4f, 6f);
            Assert.That(track.GetElapsed(10f), Is.EqualTo(2f));
            Assert.That(track.GetSide(10.1f), Is.EqualTo(-3.7f).Within(0.0001f));
        }

        [Test]
        public void TeamPassageReconnectReplayMatchesLiveCommandsDuringAndAfterPause()
        {
            var live = new HorizontalSawTrack(3f, 10f, true);
            live.Start(0f);
            live.Pause(2f, 6f);
            live.Stop(5f);
            live.Start(10f);
            var replay = new HorizontalSawTrack(3f, 10f, true);
            replay.Pause(2f, 6f);
            replay.Start(0f);
            replay.Stop(5f);
            replay.Start(10f);
            foreach (float time in new[] { 0f, 2f, 5f, 8f, 9f, 10f, 12f, 16f })
            {
                Assert.That(replay.GetSide(time), Is.EqualTo(live.GetSide(time)).Within(0.0001f));
                Assert.That(replay.IsMoving(time), Is.EqualTo(live.IsMoving(time)));
            }
            replay.Clear();
            Assert.IsFalse(replay.IsMoving(16f));
            Assert.That(replay.GetSide(16f), Is.EqualTo(-10f));
            Assert.That(replay.GetElapsed(16f), Is.EqualTo(16f));
        }
    }
}
