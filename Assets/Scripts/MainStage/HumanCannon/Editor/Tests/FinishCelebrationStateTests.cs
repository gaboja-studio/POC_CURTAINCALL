using NUnit.Framework;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class FinishCelebrationStateTests
    {
        [Test]
        public void Performance_CelebratesOnlyOnFirstArrival()
        {
            var state = new FinishCelebrationState();
            Assert.IsFalse(state.TryCelebrate("show-1", false), "도착자가 없으면 터지지 않는다");
            Assert.IsTrue(state.TryCelebrate("show-1", true));
            Assert.IsFalse(state.TryCelebrate("show-1", true), "같은 공연의 다음 도착은 다시 터지지 않는다");
        }

        [Test]
        public void NewPerformance_CelebratesAgain()
        {
            var state = new FinishCelebrationState();
            Assert.IsTrue(state.TryCelebrate("show-1", true));
            Assert.IsTrue(state.TryCelebrate("show-2", true));
        }

        [Test]
        public void Restart_OnlyRearmsPractice()
        {
            var state = new FinishCelebrationState();
            Assert.IsTrue(state.TryCelebrate("show-1", true));
            state.RestartPractice("show-1");
            Assert.IsFalse(state.TryCelebrate("show-1", true), "공연 중 R은 다시 터뜨리지 않는다");

            Assert.IsTrue(state.TryCelebrate(null, true));
            Assert.IsFalse(state.TryCelebrate(null, true));
            state.RestartPractice(null);
            Assert.IsTrue(state.TryCelebrate("", true), "연습은 R로 다시 시작하면 다시 터진다");
        }
    }
}
