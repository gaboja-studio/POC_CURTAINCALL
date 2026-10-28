using CurtainCall.Campaign;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    /// <summary>M2 하루 시계: 8~10 아침 쉬는 시간, 10 오전 공연, 12~14 낮 쉬는 시간, 14 오후 공연, 16 밤 정비, 20 수면.</summary>
    public sealed class DayClockTests
    {
        static DayClockReading Read(CampaignSnapshot s, float total = 300f)
        {
            Assert.IsTrue(DayClock.TryRead(s, total, out var reading));
            return reading;
        }

        [Test]
        public void RestMinutesMapToTwoClockHours()
        {
            var morning = new CampaignSnapshot { Phase = CampaignPhase.RestTime, Rest = RestKind.Morning, Day = 2, Timer = RestTimerState.Running, TimeRemaining = 300f };
            var start = Read(morning);
            Assert.AreEqual(8f, start.Hours, .001f);
            Assert.AreEqual("오전", start.Meridiem);
            Assert.AreEqual("8:00", start.TimeText);
            Assert.AreEqual("5:00", start.RemainingText);
            Assert.IsTrue(start.HasTimer);
            morning.TimeRemaining = 150f;
            Assert.AreEqual("9:00", Read(morning).TimeText, "5분 중 절반 = 1시간");
            var between = new CampaignSnapshot { Phase = CampaignPhase.RestTime, Rest = RestKind.Between, Day = 2, Timer = RestTimerState.Running, TimeRemaining = 75f, AlarmRaised = true };
            var late = Read(between);
            Assert.AreEqual("1:30", late.TimeText);
            Assert.AreEqual("오후", late.Meridiem);
            Assert.AreEqual(.25f, late.RemainingRatio, .001f);
            Assert.IsTrue(late.Alarm);
        }

        [Test]
        public void PerformanceNightAndSleepHaveFixedTimesWithoutTimer()
        {
            var show = Read(new CampaignSnapshot { Phase = CampaignPhase.Performance, PerformanceIndex = 1, Day = 1 });
            Assert.AreEqual(14f, show.Hours);
            Assert.AreEqual("오후 공연", show.Period);
            Assert.IsFalse(show.HasTimer);
            var night = Read(new CampaignSnapshot { Phase = CampaignPhase.Night, Day = 3 });
            Assert.AreEqual("4:00", night.TimeText);
            Assert.AreEqual("밤 정비", night.Period);
            Assert.IsFalse(night.HasTimer, "밤 정비에는 타이머가 없다");
            Assert.AreEqual("수면", Read(new CampaignSnapshot { Phase = CampaignPhase.Dawn, Day = 3 }).Period);
            Assert.IsFalse(DayClock.TryRead(new CampaignSnapshot { Phase = CampaignPhase.Escape }, 300f, out _));
        }

        [Test]
        public void BandsCoverTwelveHourDialOnce()
        {
            float covered = 0f;
            foreach (var (start, end, _) in DayClock.Bands) covered += end - start;
            Assert.AreEqual(12f, covered, .001f);
            Assert.AreEqual(240f, DayClock.Angle(8f), .001f);
            Assert.AreEqual(240f, DayClock.Angle(20f), .001f, "오후 8시와 오전 8시는 같은 자리");
        }
    }
}
