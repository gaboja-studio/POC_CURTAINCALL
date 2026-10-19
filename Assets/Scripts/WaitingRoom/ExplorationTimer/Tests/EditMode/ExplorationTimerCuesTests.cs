using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class ExplorationTimerCuesTests
    {
        static ExplorationTimerCues Make() => new ExplorationTimerCues(30f, 10f);

        [Test]
        public void 문턱을_지날_때_한_번씩_방송한다()
        {
            var cues = Make();
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 300f));
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 30.2f));
            Assert.AreEqual(30f, cues.Step(true, 29.9f));
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 29.5f));
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 10.1f));
            Assert.AreEqual(10f, cues.Step(true, 9.95f));
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 5f));
        }

        [Test]
        public void 받는_쪽_보정으로_조금_늘어나도_다시_울리지_않는다()
        {
            var cues = Make();
            cues.Step(true, 31f);
            Assert.AreEqual(30f, cues.Step(true, 29.8f));
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 30.4f)); // 호스트 값이 조금 더 큼
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 29.7f));
        }

        [Test]
        public void 크게_되돌리면_다시_울릴_수_있다()
        {
            var cues = Make();
            cues.Step(true, 31f);
            Assert.AreEqual(30f, cues.Step(true, 29f));
            cues.Step(true, 100f);
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 31f));
            Assert.AreEqual(30f, cues.Step(true, 29f));
        }

        [Test]
        public void 처음_본_값이_이미_문턱_아래면_놓친_방송은_내지_않는다()
        {
            var cues = Make();
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 20f));
            Assert.AreEqual(10f, cues.Step(true, 9.5f));
        }

        [Test]
        public void 한_번에_여러_문턱을_지나면_가장_낮은_것만_방송한다()
        {
            var cues = Make();
            cues.Step(true, 40f);
            Assert.AreEqual(10f, cues.Step(true, 5f));
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 4f));
        }

        [Test]
        public void 영초에_닿으면_방송하지_않는다()
        {
            var cues = Make();
            cues.Step(true, 11f);
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 0f));
        }

        [Test]
        public void 멈춘_동안은_방송하지_않고_다시_흐를_때_기준을_새로_잡는다()
        {
            var cues = Make();
            cues.Step(true, 35f);
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(false, 25f));
            Assert.AreEqual(ExplorationTimerCues.None, cues.Step(true, 25f));
            Assert.AreEqual(10f, cues.Step(true, 9f));
        }

        [Test]
        public void 초기화하면_새_쉬는_시간으로_다시_울린다()
        {
            var cues = Make();
            cues.Step(true, 31f);
            Assert.AreEqual(30f, cues.Step(true, 29f));
            cues.Reset();
            cues.Step(true, 300f);
            Assert.AreEqual(30f, cues.Step(true, 29f));
        }
    }
}
