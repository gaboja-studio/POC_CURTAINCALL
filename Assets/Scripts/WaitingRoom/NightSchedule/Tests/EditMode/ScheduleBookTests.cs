using NUnit.Framework;

namespace CurtainCall.WaitingRoom.Tests
{
    [Category("BreakFlow")]
    public sealed class ScheduleBookTests
    {
        const string Rope = "Tightrope", Cannon = "HumanCannon";
        static ScheduleBook NewBook() => new(new[] { Rope, Rope }, new[] { Rope, Cannon });

        [Test]
        public void EveryPerformanceDefaultsToTightropeAndDayFourHasNoShows()
        {
            var book = NewBook();
            for (int day = 1; day <= 3; day++)
                CollectionAssert.AreEqual(new[] { Rope, Rope }, book.Setlist(day));
            CollectionAssert.AreEqual(new[] { "", "" }, book.Setlist(4));
            Assert.AreEqual(ScheduleRefusal.NotEditable, book.Set(4, 0, Rope));
        }

        [Test]
        public void FirstDaySecondChoiceCanChangeRepeatedlyWithoutConfirmation()
        {
            var book = NewBook();
            Assert.AreEqual(ScheduleRefusal.NotEditable, book.Set(1, 1, Cannon));
            book.SetFirstDayChoiceOpen(true);
            Assert.AreEqual(ScheduleRefusal.BadSlot, book.Set(1, 0, Cannon));
            Assert.AreEqual(ScheduleRefusal.None, book.Set(1, 1, Cannon));
            book.ConfirmFirstDay();
            Assert.AreEqual(ScheduleRefusal.None, book.Set(1, 1, Rope));
            Assert.IsFalse(book.FirstDayConfirmed);
            Assert.IsTrue(book.IsFirstDaySecondEditable);
        }

        [Test]
        public void ChoiceWorksDuringRestAndNeverCreatesALock()
        {
            var book = NewBook();
            Assert.AreEqual(ScheduleRefusal.None, book.Set(2, 0, Cannon));
            book.BeginNight(1); book.Confirm(); book.EndNight();
            Assert.AreEqual(ScheduleRefusal.None, book.Set(2, 0, Rope));
            Assert.IsFalse(book.IsLocked(2));
            Assert.IsFalse(book.IsConfirmedTonight);
            Assert.AreEqual(ScheduleRefusal.UnknownTrick, book.Set(2, 0, ""));
            Assert.AreEqual(Rope, book.Get(2, 0), "No empty schedule option");
            Assert.AreEqual(ScheduleRefusal.UnknownTrick, book.Set(2, 0, "Juggling"));
            Assert.AreEqual(ScheduleRefusal.BadSlot, book.Set(2, 2, Rope));
        }

        [Test]
        public void RehearsedDefaultOnlyChangesUpcomingUnchosenSlots()
        {
            var book = NewBook();
            book.Set(2, 1, Rope); // Explicitly choosing the current default still preserves the choice.
            Assert.IsTrue(book.FillUpcomingDefaults(Cannon, 2, 0));
            Assert.AreEqual(Rope, book.Get(1, 1), "Past performance is preserved");
            Assert.AreEqual(Cannon, book.Get(2, 0));
            Assert.AreEqual(Rope, book.Get(2, 1), "Player choice is preserved");
            Assert.AreEqual(Cannon, book.Get(3, 0));
            Assert.IsFalse(book.FillUpcomingDefaults(Cannon, 2, 0));
            book.Clear();
            Assert.AreEqual(Rope, book.Get(3, 0));
            Assert.IsTrue(book.FillUpcomingDefaults(Cannon, 3, 0), "A fresh run resets manual choices");
        }

        [Test]
        public void TiersKeepClearProgressLimitsAndRoundTripWithoutConfirmation()
        {
            var book = NewBook();
            Assert.AreEqual(ScheduleRefusal.BadTier, book.Set(2, 0, Rope, 2, 1));
            Assert.AreEqual(ScheduleRefusal.None, book.Set(2, 0, Rope, 2, 2));
            Assert.AreEqual(ScheduleRefusal.BadTier, book.Set(2, 0, Rope, 4, 9));
            var copy = NewBook();
            Assert.IsTrue(copy.Load(book.Night, false, book.ExportSlots(), book.ExportLocked(), book.ExportTiers()));
            CollectionAssert.AreEqual(book.Setlist(2), copy.Setlist(2));
            Assert.AreEqual(2, copy.Difficulty(2, 0));
            Assert.AreEqual(ScheduleRefusal.None, copy.AdjustTier(2, 0, 3, 3));
            Assert.AreEqual(3, copy.Difficulty(2, 0));
        }

        [Test]
        public void OlderEmptyOrLockedSnapshotsCannotRestoreEmptySchedulesOrLocks()
        {
            var book = NewBook();
            Assert.IsFalse(book.Load(1, true, new[] { Rope }, new bool[4]));
            Assert.IsTrue(book.Load(1, true, new[] { Cannon, Cannon, "", Rope, "Unknown", "", Cannon, Rope }, new[] { true, true, true, true }));
            Assert.AreEqual(Rope, book.Get(1, 0));
            Assert.AreEqual(Cannon, book.Get(1, 1));
            Assert.AreEqual(Rope, book.Get(2, 0));
            Assert.AreEqual(Rope, book.Get(3, 0));
            Assert.AreEqual("", book.Get(4, 0));
            Assert.IsFalse(book.IsLocked(2));
            Assert.IsFalse(book.IsConfirmedTonight);
        }
    }
}
