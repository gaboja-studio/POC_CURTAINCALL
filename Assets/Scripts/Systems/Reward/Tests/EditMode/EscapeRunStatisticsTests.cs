using System;
using System.Collections.Generic;
using CurtainCall.Results;
using NUnit.Framework;

namespace CurtainCall.Reward.Tests
{
    public sealed class EscapeRunStatisticsTests
    {
        readonly string session = Guid.NewGuid().ToString("N");
        PerformanceResult Result(int index, bool success, long score) => new(
            new PerformanceIdentity(session,1,index+1,0,"Tightrope"),
            success ? PerformanceFailure.None : PerformanceFailure.TrickFailed,"test",
            new[] { new PerformanceParticipantResult("p",0,0,0,0,false,true,"clear",score,score*2) });

        [Test]
        public void MoreThanTenResultsRemainInTotalsAndRepeatedPublishDoesNotDoubleCount()
        {
            var stats = new EscapeRunStatistics(); stats.Reset(1,0);
            for (int i = 0; i < 15; i++)
            {
                var result = Result(i,i%3 != 0,i+1);
                Assert.IsTrue(stats.Record(result,2,_ => 3));
                Assert.IsFalse(stats.Record(result,2,_ => 3));
            }
            Assert.That(stats.rows.Count,Is.EqualTo(1));
            Assert.That(stats.rows[0].attempts,Is.EqualTo(15));
            Assert.That(stats.rows[0].clears,Is.EqualTo(10));
            Assert.That(stats.rows[0].cheer,Is.EqualTo(120));
            Assert.That(stats.personalBest,Is.EqualTo(15)); Assert.That(stats.personalModel,Is.EqualTo(3));
            stats.Validate(1);
        }
        [Test]
        public void UnresolvedPortraitDoesNotReplaceHigherPersonalRecord()
        {
            var stats = new EscapeRunStatistics(); stats.Reset(1,0);
            stats.Record(Result(0,true,100),1,_ => -1);
            stats.Record(Result(1,true,10),1,_ => 2);
            Assert.That(stats.personalBest,Is.EqualTo(100)); Assert.That(stats.personalModel,Is.EqualTo(-1));
        }
        [Test]
        public void LedgerTotalsIgnoreGenerationResetAndGenerationAdvanceClearsTheRun()
        {
            var stats = new EscapeRunStatistics(); stats.Reset(1,0);
            var ledger = new List<FundEntry> {
                new(1,1,1,FundEntryKind.PerformanceIncome,100,100,"income"),
                new(2,1,1,FundEntryKind.Expense,-30,70,"purchase"),
                new(3,2,1,FundEntryKind.GenerationReset,-70,0,"reset") };
            stats.ReadLedger(ledger); stats.ReadLedger(ledger);
            Assert.That(stats.earned,Is.EqualTo(100)); Assert.That(stats.spent,Is.EqualTo(30));
            stats.Record(Result(0,true,50),1,_=>0);
            stats.Reset(2,ledger.Count); stats.ReadLedger(ledger);
            Assert.That(stats.rows,Is.Empty); Assert.That(stats.earned,Is.Zero); Assert.That(stats.personalBest,Is.Zero);
            Assert.IsFalse(stats.Record(Result(1,true,50),1,_=>0));
        }
    }
}
