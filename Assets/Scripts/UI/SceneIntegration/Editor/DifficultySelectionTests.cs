using CurtainCall.Campaign;
using NUnit.Framework;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    /// <summary>M2 난이도: 1일차 1단계, 밤에 고른 단계는 깬 단계의 다음 단계까지만 실제로 쓰고, 새 런에서 처음부터.</summary>
    public sealed class DifficultySelectionTests
    {
        static RunState NightOfDayOne(bool clearRope)
        {
            var run = new RunState();
            run.StartSession();
            Assert.IsTrue(run.EndIntro());
            Assert.AreEqual(1, run.Snapshot.Difficulty, "1일차 첫 공연은 1단계");
            if (clearRope) Assert.IsTrue(run.RecordClear("Tightrope", 1));
            Assert.IsTrue(run.EndPerformance());
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.AreEqual(1, run.Snapshot.Difficulty);
            Assert.IsTrue(run.EndPerformance());
            Assert.AreEqual(CampaignPhase.Night, run.Snapshot.Phase);
            return run;
        }

        static void Morning(RunState run)
        {
            run.SetReady(0, true);
            run.RefreshNight(new ulong[] { 0 });
            Assert.AreEqual(CampaignPhase.RestTime, run.Snapshot.Phase);
        }

        [Test]
        public void ChosenTierIsLimitedToNextAfterCleared()
        {
            var run = NightOfDayOne(clearRope: true);
            Assert.AreEqual(2, run.MaxSelectableTier("Tightrope"));
            Assert.AreEqual(1, run.MaxSelectableTier("HumanCannon"));
            Assert.IsTrue(run.UnlockTrick("HumanCannon"));
            Assert.IsTrue(run.SetSetlist(2, new[] { "Tightrope", "HumanCannon" }, new[] { 3, 2 }));
            Morning(run);
            Assert.AreEqual("Tightrope", run.Snapshot.TrickId);
            Assert.AreEqual(2, run.Snapshot.Difficulty, "3단계를 골라도 1단계만 깼으면 2단계까지");
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.AreEqual(2, run.Snapshot.Difficulty);
            Assert.IsTrue(run.RecordClear("Tightrope", 2));
            Assert.IsFalse(run.RecordClear("Tightrope", 1), "더 낮은 단계 성공은 기록을 내리지 않는다");
            Assert.AreEqual(3, run.MaxSelectableTier("Tightrope"));
            Assert.IsTrue(run.EndPerformance());
            Assert.AreEqual("HumanCannon", run.Snapshot.TrickId);
            Assert.AreEqual(1, run.Snapshot.Difficulty, "못 깬 묘기는 1단계");
        }

        [Test]
        public void HumanCannonNeedsDiscoveryAndResetsWithRun()
        {
            var run = NightOfDayOne(clearRope: false);
            Assert.IsFalse(run.Snapshot.IsTrickUnlocked("HumanCannon"));
            Assert.IsTrue(run.Snapshot.IsTrickUnlocked("Tightrope"));
            Assert.IsTrue(run.SetSetlist(2, new[] { "HumanCannon", "HumanCannon" }));
            Morning(run);
            Assert.AreEqual("Tightrope", run.Snapshot.TrickId, "미발견이면 외줄타기");
            Assert.IsTrue(run.UnlockTrick("HumanCannon"));
            Assert.IsFalse(run.UnlockTrick("HumanCannon"), "한 번만 열린다");
            Assert.AreEqual("HumanCannon", run.Snapshot.TrickId, "발견하면 편성한 공연으로 돌아온다");
            Assert.IsTrue(run.DebugRestartRun());
            Assert.IsFalse(run.Snapshot.CannonUnlocked, "런 초기화 때 다시 닫힌다");
        }

        [Test]
        public void RestTimeAdjustsOnlyNextPerformanceTier()
        {
            var run = NightOfDayOne(clearRope: true);
            Assert.IsFalse(run.AdjustNextTier(2), "밤 정비에는 쉬는 시간 조정을 쓰지 않는다");
            Assert.IsTrue(run.SetSetlist(2, new[] { "Tightrope", "Tightrope" }, new[] { 1, 1 }));
            Morning(run);
            Assert.AreEqual(1, run.Snapshot.Difficulty);
            Assert.IsFalse(run.AdjustNextTier(3), "깬 단계의 다음 단계까지만");
            Assert.IsTrue(run.AdjustNextTier(2));
            Assert.AreEqual(2, run.Snapshot.Difficulty, "아침 쉬는 시간에 다음 공연 단계를 올린다");
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.IsFalse(run.AdjustNextTier(1), "공연 중에는 바꾸지 않는다");
            Assert.IsTrue(run.RecordClear("Tightrope", 2));
            Assert.IsTrue(run.EndPerformance());
            Assert.AreEqual(1, run.Snapshot.Difficulty, "두 번째 공연은 밤에 고른 1단계");
            Assert.IsTrue(run.AdjustNextTier(3), "같은 날 앞 공연에서 연 단계를 바로 쓴다");
            Assert.AreEqual(3, run.Snapshot.Difficulty);
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.AreEqual(3, run.Snapshot.Difficulty);
        }

        [Test]
        public void RestTimeAdjustWorksWithoutChosenTiersAndNotOnDayOne()
        {
            var run = new RunState();
            run.StartSession();
            Assert.IsTrue(run.EndIntro());
            Assert.IsTrue(run.RecordClear("Tightrope", 1));
            Assert.IsTrue(run.EndPerformance());
            Assert.IsFalse(run.AdjustNextTier(1), "1일차는 1단계 고정");
            Assert.IsTrue(run.NotifyAllAtStageEntrance());
            Assert.IsTrue(run.EndPerformance());
            Assert.IsTrue(run.SetSetlist(2, new[] { "Tightrope", "HumanCannon" }));
            Morning(run);
            Assert.IsTrue(run.AdjustNextTier(2), "단계 없이 편성한 날도 조정할 수 있다");
            Assert.AreEqual(2, run.Snapshot.Difficulty);
        }

        [Test]
        public void NewRunForgetsClearedTiers()
        {
            var run = NightOfDayOne(clearRope: true);
            Assert.AreEqual(1, run.ClearedTier("Tightrope"));
            Assert.IsTrue(run.DebugRestartRun());
            Assert.AreEqual(0, run.ClearedTier("Tightrope"));
            Assert.AreEqual(1, run.MaxSelectableTier("Tightrope"));
            Assert.IsFalse(run.RecordClear("Tightrope", 4));
        }
    }
}
