using CurtainCall.Campaign;
using NUnit.Framework;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public sealed class FailureEndingStateTests
    {
        [TestCase(1,false)] [TestCase(2,false)] [TestCase(3,false)] [TestCase(4,false)]
        [TestCase(1,true)] [TestCase(2,true)] [TestCase(3,true)] [TestCase(4,true)]
        public void EscapeOutcomePreservesDayGenerationAndUnlocks(int day,bool success)
        {
            var run=new RunState(false,false,true);
            Assert.IsTrue(run.DebugStartAt(new RunStartPoint(day,RunStartPhase.Escape,0,CampaignSnapshot.TightropeId),true,(2,1)));
            var before=run.Snapshot;
            Assert.IsTrue(run.EndEscape(success));
            Assert.AreEqual(CampaignPhase.Ending,run.Snapshot.Phase);
            Assert.AreEqual(day,run.Snapshot.Day);
            Assert.AreEqual(before.Generation,run.Snapshot.Generation);
            Assert.AreEqual(success?RunEndReason.EscapeSucceeded:RunEndReason.EscapeAllRetired,run.Snapshot.LastEnd);
            Assert.AreEqual(2,run.ClearedTier(CampaignSnapshot.TightropeId));
            Assert.IsTrue(run.Snapshot.CannonUnlocked);
            Assert.IsFalse(run.Tick(100000));
            Assert.IsFalse(run.EndEscape(success));
        }
        [TestCase(false)] [TestCase(true)]
        public void DeadlineKeepsFinalDayAndNeverStartsPursuit(bool pursuit)
        {
            var run=new RunState(false,false,pursuit);
            Assert.IsTrue(run.DebugStartAt(new RunStartPoint(4,RunStartPhase.Morning,0,CampaignSnapshot.TightropeId)));
            run.DebugSetRemaining(.01f);
            bool pursuitStarted=false;
            Assert.IsTrue(run.Tick(1,_=>pursuitStarted=true));
            Assert.IsFalse(pursuitStarted);
            Assert.IsTrue(FailureEndingPresentation.IsFailureEnding(run.Snapshot));
            Assert.AreEqual(4,run.Snapshot.Day); Assert.AreEqual(1,run.Snapshot.Generation);
            Assert.AreEqual(RestTimerState.None,run.Snapshot.Timer);
        }
        [Test]
        public void FailureCheatCannotMutateRunWhenDevIsDisabled()
        {
            var field=typeof(CurtainCall.Dev.DevMode).GetField("_enabled",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var before=field.GetValue(null);
            try
            {
                field.SetValue(null,false);
                var run=new RunState();run.DebugStartAt(new RunStartPoint(2,RunStartPhase.Escape,0,CampaignSnapshot.TightropeId));
                Assert.IsFalse(run.DebugFailureEnding(RunEndReason.Deadline));
                Assert.AreEqual(CampaignPhase.Escape,run.Snapshot.Phase);
            }
            finally {field.SetValue(null,before);}
        }
        [Test]
        public void OnlySpecifiedFailuresUseTheStage()
        {
            foreach(RunEndReason reason in System.Enum.GetValues(typeof(RunEndReason)))
                foreach(CampaignPhase phase in System.Enum.GetValues(typeof(CampaignPhase)))
                    Assert.AreEqual(phase==CampaignPhase.Ending && reason is RunEndReason.Deadline or RunEndReason.EscapeAllRetired,
                        FailureEndingPresentation.IsFailureEnding(new CampaignSnapshot{Phase=phase,LastEnd=reason}));
        }
        [Test]
        public void BulbHasSparseDipsAndNeverNegativeOrBrighterThanBaseline()
        {
            int low=0;
            for(int i=0;i<1970;i++) {float v=FailureEndingStage.Flicker(i*.01);Assert.That(v,Is.InRange(0f,1f));if(v<.6f)low++;}
            Assert.That(low,Is.InRange(20,100));
        }
    }
}
