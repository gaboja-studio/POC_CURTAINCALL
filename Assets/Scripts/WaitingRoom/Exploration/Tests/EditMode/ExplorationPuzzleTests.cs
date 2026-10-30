using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    public sealed class ExplorationPuzzleTests
    {
        [TestCase("0000", true)]
        [TestCase("0123", true)]
        [TestCase("123", false)]
        [TestCase("12345", false)]
        [TestCase("12a4", false)]
        [TestCase("１２３４", false)]
        [TestCase(null, false)]
        public void PasswordRequiresFourAsciiDigits(string input, bool expected) =>
            Assert.AreEqual(expected, ExplorationPuzzleRules.IsFourDigits(input));

        [Test] public void RepeatedPressesFromOnePlayerDoNotOpen()
        {
            var coop = new ExplorationCooperation();
            Assert.IsFalse(coop.Press("door", 1, 1, .75, _ => true));
            Assert.IsFalse(coop.Press("door", 1, 1.1, .75, _ => true));
            Assert.IsTrue(coop.Press("door", 2, 1.2, .75, _ => true));
        }
        [Test] public void ExpiredOrDisconnectedPartnerDoesNotCount()
        {
            var coop = new ExplorationCooperation();
            coop.Press("door", 1, 1, .75, _ => true);
            Assert.IsFalse(coop.Press("door", 2, 2, .75, _ => true));
            Assert.IsFalse(coop.Press("door", 3, 2.1, .75, id => id != 2));
            Assert.IsTrue(coop.Press("door", 4, 2.2, .75, _ => true));
        }
        [Test] public void TargetsAndResetDoNotSharePresses()
        {
            var coop = new ExplorationCooperation();
            coop.Press("a", 1, 1, .75, _ => true);
            Assert.IsFalse(coop.Press("b", 2, 1.1, .75, _ => true));
            coop.Clear();
            Assert.IsFalse(coop.Press("a", 2, 1.2, .75, _ => true));
        }
        [Test] public void SuccessfulPairIsCleared()
        {
            var coop = new ExplorationCooperation();
            coop.Press("a", 1, 1, .75, _ => true);
            Assert.IsTrue(coop.Press("a", 2, 1.1, .75, _ => true));
            Assert.IsFalse(coop.Press("a", 3, 1.2, .75, _ => true));
        }
    }
}
