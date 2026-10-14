using System;
using System.Collections.Generic;
using CurtainCall.Results;
using NUnit.Framework;

namespace CurtainCall.Reward.Tests
{
    /// <summary>Task-031 순수 보상 모델: 호응·참가 명부·정산·공동 자금·불변 공유 상태.</summary>
    public class PerformanceRewardModelTests
    {
        const string Session = "11111111111111111111111111111111";
        const string OtherSession = "22222222222222222222222222222222";
        static readonly RewardRules DefaultRules = new(10, 5, 100, 0, 0);

        static PerformanceIdentity Identity(int index = 0, int day = 1, int generation = 1,
            string session = Session) => new(session, generation, day, index, "DummyTrick");

        static RewardParticipantSeed[] Pair() => new[]
        {
            new RewardParticipantSeed("p1", 11, 0),
            new RewardParticipantSeed("p2", 22, 1)
        };

        static PerformanceRewardModel BeginPair(RewardRules? rules = null, int day = 1)
        {
            var model = new PerformanceRewardModel(Session, 1);
            Assert.IsTrue(model.Begin(Identity(day: day), Pair(), rules ?? DefaultRules));
            return model;
        }

        static PerformanceResult Settle(PerformanceRewardModel model, bool succeeded = true,
            int minimumSurvivors = 2, RewardRules? rules = null)
        {
            Assert.IsTrue(model.Confirm(succeeded, "DummyEnd", null, minimumSurvivors,
                rules ?? DefaultRules, out var result));
            Assert.IsNotNull(result);
            return result;
        }

        static PerformanceRewardModel FundedPair()
        {
            var model = BeginPair();
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Cheer("p2", true, DefaultRules));
            Assert.AreEqual(20, Settle(model).Income);
            return model;
        }

        [Test]
        public void CheerAndJeer_ClampToCapAndZero_WithoutRevisionAtBounds()
        {
            var rules = new RewardRules(10, 7, 25, 0, 0);
            var model = BeginPair(rules);

            Assert.IsTrue(model.Cheer("p1", true, rules));
            Assert.IsTrue(model.Cheer("p1", true, rules));
            Assert.IsTrue(model.Cheer("p1", true, rules));
            Assert.AreEqual(25, model.Snapshot().Segments[0].Amount);
            long atCap = model.Revision;
            Assert.IsFalse(model.Cheer("p1", true, rules));
            Assert.AreEqual(atCap, model.Revision, "상한에서 그대로인 환호는 상태 변경이 아니다");

            for (int i = 0; i < 4; i++) Assert.IsTrue(model.Cheer("p1", false, rules));
            Assert.AreEqual(0, model.Snapshot().Segments[0].Amount);
            long atZero = model.Revision;
            Assert.IsFalse(model.Cheer("p1", false, rules));
            Assert.AreEqual(atZero, model.Revision, "야유로 음수 보상을 만들지 않는다");
        }

        [Test]
        public void ApplyRules_LowerCap_ClampsEverySegmentImmediately()
        {
            var model = BeginPair();
            for (int i = 0; i < 3; i++) Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            for (int i = 0; i < 2; i++) Assert.IsTrue(model.Cheer("p2", true, DefaultRules));
            var lower = new RewardRules(10, 5, 12, 0, 0);
            long before = model.Revision;

            Assert.IsTrue(model.ApplyRules(lower));
            var snapshot = model.Snapshot();
            Assert.AreEqual(12, snapshot.Segments[0].Amount);
            Assert.AreEqual(12, snapshot.Segments[1].Amount);
            Assert.AreEqual(lower, snapshot.Rules);
            Assert.AreEqual(before + 1, snapshot.Revision);
            Assert.IsFalse(model.ApplyRules(lower));
            Assert.AreEqual(snapshot.Revision, model.Revision, "같은 규칙을 다시 적용해도 변경 번호는 그대로다");

            Assert.IsTrue(model.ApplyRules(DefaultRules));
            Assert.AreEqual(12, model.Snapshot().Segments[0].Amount, "상한을 올려도 잘린 금액을 복원하지 않는다");
        }

        [Test]
        public void Cheer_ChangedRules_UsesCurrentIncrementAndDecrement()
        {
            var model = BeginPair();
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            var changed = new RewardRules(20, 9, 25, 0, 0);

            Assert.IsTrue(model.Cheer("p1", true, changed));
            Assert.AreEqual(25, model.Snapshot().Segments[0].Amount);
            Assert.IsTrue(model.Cheer("p1", false, changed));
            Assert.AreEqual(16, model.Snapshot().Segments[0].Amount);
            Assert.AreEqual(changed, model.Snapshot().Rules);
        }

        [Test]
        public void Observe_CutBonus_AwardsEachNewBitOnlyOnce()
        {
            var rules = new RewardRules(10, 5, 100, 0, 7);
            var model = BeginPair(rules);

            Assert.IsTrue(model.Observe("p1", 1, false, true, rules));
            Assert.AreEqual(7, model.Snapshot().Segments[0].Amount);
            long beforeRepeat = model.Revision;
            Assert.IsFalse(model.Observe("p1", 1, false, true, rules));
            Assert.AreEqual(beforeRepeat, model.Revision);
            Assert.IsTrue(model.Observe("p1", 3, false, true, rules));
            Assert.AreEqual(14, model.Snapshot().Segments[0].Amount);
            Assert.IsTrue(model.Observe("p1", 7, false, true, rules));
            Assert.AreEqual(21, model.Snapshot().Segments[0].Amount);
            Assert.AreEqual(3, model.Snapshot().Segments[0].LostCount);
        }

        [Test]
        public void Observe_RestoreAndRecut_DoesNotAwardPreviouslySeenBits()
        {
            var rules = new RewardRules(10, 5, 100, 0, 7);
            var model = BeginPair(rules);
            Assert.IsTrue(model.Observe("p1", 3, false, true, rules));
            Assert.AreEqual(14, model.Snapshot().Segments[0].Amount);

            Assert.IsTrue(model.Observe("p1", 0, false, true, rules));
            Assert.AreEqual(0, model.Snapshot().Segments[0].LostCount);
            Assert.AreEqual(14, model.Snapshot().Segments[0].Amount, "복구는 이미 받은 절단 보너스를 회수하지 않는다");
            Assert.IsTrue(model.Observe("p1", 3, false, true, rules));
            Assert.AreEqual(14, model.Snapshot().Segments[0].Amount, "복구 뒤 같은 부위의 재절단은 중복 지급하지 않는다");
            Assert.IsTrue(model.Observe("p1", 11, false, true, rules));
            Assert.AreEqual(21, model.Snapshot().Segments[0].Amount, "처음 잘린 다른 비트만 추가 지급한다");
        }

        [Test]
        public void Observe_Death_RemovesSegmentAndCannotReviveDuringPerformance()
        {
            var model = new PerformanceRewardModel(Session, 1);
            var people = new[]
            {
                new RewardParticipantSeed("p1", 11, 0),
                new RewardParticipantSeed("p2", 22, 1),
                new RewardParticipantSeed("p3", 33, 2)
            };
            Assert.IsTrue(model.Begin(Identity(), people, DefaultRules));
            foreach (var person in people) Assert.IsTrue(model.Cheer(person.Id, true, DefaultRules));

            Assert.IsTrue(model.Observe("p1", 1, true, true, DefaultRules));
            Assert.AreEqual(0, model.Snapshot().Segments[0].Amount);
            Assert.IsTrue(model.Observe("p1", 0, false, true, DefaultRules));
            Assert.IsTrue(model.Snapshot().Segments[0].Dead, "몸이 복구되어도 이 공연의 사망 판정은 되돌리지 않는다");
            Assert.IsFalse(model.Cheer("p1", true, DefaultRules));
            var result = Settle(model);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(20, result.Income);
            Assert.AreEqual(1, result.Deaths.Count);
            Assert.AreEqual("p1", result.Deaths[0].ParticipantId);
            Assert.AreEqual(0, result.Deaths[0].Segment);
            Assert.AreEqual(0, result.Deaths[0].Settlement);
            Assert.AreEqual("Dead", result.Deaths[0].Judgement);
            Assert.IsFalse(result.AllSurvived);
        }

        [Test]
        public void Observe_AllFourLostBits_ImplyDeathIncludingStartingRoster()
        {
            var model = new PerformanceRewardModel(Session, 1);
            var people = new[]
            {
                new RewardParticipantSeed("p1", 11, 0, 15),
                new RewardParticipantSeed("p2", 22, 1),
                new RewardParticipantSeed("p3", 33, 2)
            };
            Assert.IsTrue(people[0].Dead, "사망 플래그가 없어도 네 부위 손실은 사망이다");
            Assert.IsTrue(model.Begin(Identity(), people, DefaultRules));
            Assert.IsFalse(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Cheer("p2", true, DefaultRules));
            Assert.IsTrue(model.Observe("p2", 15, false, true, DefaultRules));
            Assert.IsTrue(model.Snapshot().Segments[1].Dead);
            Assert.AreEqual(0, model.Snapshot().Segments[1].Amount);
            Assert.IsTrue(model.Cheer("p3", true, DefaultRules));

            var result = Settle(model, minimumSurvivors: 1);
            Assert.AreEqual(2, result.Deaths.Count);
            Assert.AreEqual(10, result.Income);
        }

        [Test]
        public void Observe_Disconnect_ExcludesSurvivalAndSettlementAndCannotReconnect()
        {
            var rules = new RewardRules(10, 5, 100, 20, 0);
            var model = new PerformanceRewardModel(Session, 1);
            var people = new[]
            {
                new RewardParticipantSeed("p1", 11, 0),
                new RewardParticipantSeed("p2", 22, 1),
                new RewardParticipantSeed("p3", 33, 2)
            };
            Assert.IsTrue(model.Begin(Identity(), people, rules));
            foreach (var person in people) Assert.IsTrue(model.Cheer(person.Id, true, rules));

            Assert.IsTrue(model.Observe("p1", 0, false, false, rules));
            Assert.IsFalse(model.Observe("p1", 0, false, true, rules));
            Assert.IsFalse(model.Cheer("p1", true, rules));
            var segment = model.Snapshot().Segments[0];
            Assert.IsFalse(segment.Connected);
            Assert.IsFalse(segment.Dead, "이탈을 사망 명부에 넣지 않는다");
            Assert.AreEqual(0, segment.Amount);

            var result = Settle(model, rules: rules);
            Assert.IsTrue(result.Succeeded, "연결된 생존자 두 명은 남아 있다");
            Assert.AreEqual(20, result.Income, "이탈자가 있으면 전원 생존 보너스도 지급하지 않는다");
            Assert.AreEqual(0, result.Deaths.Count);
            Assert.IsFalse(result.AllSurvived);
            Assert.IsFalse(result.Participants[0].Eligible);
            Assert.AreEqual("Disconnected", result.Participants[0].Judgement);
            Assert.AreEqual(0, result.Participants[0].Settlement);
        }

        [Test]
        public void Confirm_Success_SumsIndividualSettlementsIntoOneLedgerEntry()
        {
            var model = BeginPair(day: 2);
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Cheer("p2", true, DefaultRules));
            var judgements = new Dictionary<string, string> { { "p1", "Arrived" } };

            Assert.IsTrue(model.Confirm(true, "Completed", judgements, 2, DefaultRules, out var result));
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(PerformanceFailure.None, result.Failure);
            Assert.AreEqual("Completed", result.EndReason);
            Assert.IsTrue(result.AllSurvived);
            Assert.AreEqual(20, result.Participants[0].Settlement);
            Assert.AreEqual(10, result.Participants[1].Settlement);
            Assert.AreEqual("Arrived", result.Participants[0].Judgement);
            Assert.AreEqual("Survived", result.Participants[1].Judgement);
            Assert.AreEqual(30, result.Income);
            Assert.AreEqual(30, model.Balance);
            Assert.IsFalse(model.IsOpen);
            Assert.AreSame(result, model.Latest);
            Assert.AreSame(result, model.History[0]);
            Assert.AreEqual(1, model.Ledger.Count);
            var entry = model.Ledger[0];
            Assert.AreEqual(1, entry.Number);
            Assert.AreEqual(1, entry.Generation);
            Assert.AreEqual(2, entry.Day);
            Assert.AreEqual(FundEntryKind.PerformanceIncome, entry.Kind);
            Assert.AreEqual(30, entry.Amount);
            Assert.AreEqual(30, entry.Balance);
            Assert.AreEqual(result.Performance.Id, entry.PerformanceId);
        }

        [Test]
        public void Confirm_TrickFailure_PaysZeroAndKeepsExistingBalance()
        {
            var model = FundedPair();
            Assert.IsTrue(model.Begin(Identity(index: 1), Pair(), DefaultRules));
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Cheer("p2", true, DefaultRules));

            var result = Settle(model, succeeded: false);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(PerformanceFailure.TrickFailed, result.Failure);
            Assert.AreEqual(0, result.Income);
            foreach (var person in result.Participants)
            {
                Assert.AreEqual(10, person.Segment, "실패 결과에도 끝날 때 남아 있던 구간은 기록한다");
                Assert.AreEqual(0, person.Settlement);
            }
            Assert.AreEqual(20, model.Balance);
            Assert.AreEqual(2, model.Ledger.Count);
            Assert.AreEqual(0, model.Ledger[1].Amount);
            Assert.AreEqual(20, model.Ledger[1].Balance);
        }

        [Test]
        public void Confirm_AllSurvivedBonus_IsPerParticipantAndInsideCurrentCap()
        {
            var initial = new RewardRules(10, 5, 100, 0, 8);
            var model = BeginPair(initial);
            Assert.IsTrue(model.Cheer("p1", true, initial));
            Assert.IsTrue(model.Cheer("p1", true, initial));
            Assert.IsTrue(model.Observe("p2", 3, false, true, initial));
            var settlementRules = new RewardRules(10, 5, 25, 20, 8);

            var result = Settle(model, rules: settlementRules);
            Assert.IsTrue(result.AllSurvived);
            Assert.AreEqual(25, result.Participants[0].Segment);
            Assert.AreEqual(25, result.Participants[1].Segment);
            Assert.AreEqual(50, result.Income, "절단·전원 생존 보너스를 합쳐도 개인 상한을 넘지 않는다");
            Assert.AreEqual(settlementRules, model.Snapshot().Rules);
            Assert.AreEqual(25, model.Snapshot().Segments[1].Amount);
        }

        [Test]
        public void Observe_LargeCutBonus_ClampsWithoutIntegerOverflow()
        {
            var rules = new RewardRules(10, 5, 25, 0, int.MaxValue);
            var model = BeginPair(rules);

            Assert.IsTrue(model.Observe("p1", 3, false, true, rules));
            Assert.AreEqual(25, model.Snapshot().Segments[0].Amount, "두 비트의 곱은 int 범위를 넘지만 상한으로 정산한다");
            Assert.IsTrue(model.Observe("p1", 7, false, true, rules));
            Assert.AreEqual(25, model.Snapshot().Segments[0].Amount);
            Assert.AreEqual(25, Settle(model, rules: rules).Income);
        }

        [Test]
        public void Confirm_MinimumTwo_SingleSurvivorFailsEvenWhenTrickSucceeded()
        {
            var model = BeginPair();
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Observe("p2", 0, true, true, DefaultRules));

            var result = Settle(model);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(PerformanceFailure.InsufficientSurvivors, result.Failure);
            Assert.AreEqual(10, result.Participants[0].Segment);
            Assert.AreEqual(0, result.Participants[0].Settlement);
            Assert.AreEqual(0, result.Income);
            Assert.AreEqual(0, model.Balance);
        }

        [Test]
        public void Confirm_MinimumOne_SingleSurvivorCanSucceed()
        {
            var model = BeginPair();
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Observe("p2", 0, true, true, DefaultRules));

            var result = Settle(model, minimumSurvivors: 1);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(PerformanceFailure.None, result.Failure);
            Assert.AreEqual(10, result.Income);
            Assert.AreEqual(10, model.Balance);
            Assert.IsFalse(result.AllSurvived);
        }

        [Test]
        public void Confirm_MinimumOne_ZeroConnectedSurvivorsStillFails()
        {
            var model = BeginPair();
            Assert.IsTrue(model.Observe("p1", 0, true, true, DefaultRules));
            Assert.IsTrue(model.Observe("p2", 0, false, false, DefaultRules));

            var result = Settle(model, minimumSurvivors: 1);
            Assert.AreEqual(PerformanceFailure.InsufficientSurvivors, result.Failure);
            Assert.AreEqual(0, result.Income);
            Assert.AreEqual(1, result.Deaths.Count, "이탈자는 생존에서 빠지지만 사망자는 아니다");
        }

        [Test]
        public void Begin_RosterAndStartingLostCount_StayFixedAcrossSourceChangesAndRetry()
        {
            var rules = new RewardRules(10, 5, 100, 0, 9);
            var identity = Identity();
            var people = new List<RewardParticipantSeed>
            {
                new("p1", 11, 0, 3),
                new("p2", 22, 1)
            };
            var model = new PerformanceRewardModel(Session, 1);
            Assert.IsTrue(model.Begin(identity, people, rules));
            long beforeRetry = model.Revision;
            people[0] = new RewardParticipantSeed("late", 33, 2, 7);
            people.Add(new RewardParticipantSeed("later", 44, 3));

            Assert.IsTrue(model.Begin(identity, people, DefaultRules));
            Assert.AreEqual(beforeRetry, model.Revision, "같은 공연의 Begin 재전송은 명부·규칙을 다시 만들지 않는다");
            Assert.AreEqual(rules, model.Snapshot().Rules);
            Assert.AreEqual(2, model.Snapshot().Segments.Count);
            Assert.AreEqual("p1", model.Snapshot().Segments[0].ParticipantId);
            Assert.AreEqual(2, model.Snapshot().Segments[0].StartingLostCount);
            Assert.AreEqual(0, model.Snapshot().Segments[0].Amount, "시작 전에 잃은 부위에는 보너스를 주지 않는다");
            Assert.IsFalse(model.Cheer("late", true, rules));
            Assert.IsTrue(model.Observe("p1", 0, false, true, rules));
            Assert.IsTrue(model.Observe("p1", 3, false, true, rules));
            Assert.AreEqual(0, model.Snapshot().Segments[0].Amount, "시작 때 없던 부위를 복구·재절단해도 새 절단이 아니다");
            Assert.IsTrue(model.Observe("p1", 4, false, true, rules));

            var result = Settle(model, rules: rules);
            Assert.AreEqual(2, result.Participants.Count);
            var person = result.Participants[0];
            Assert.AreEqual("p1", person.ParticipantId);
            Assert.AreEqual(11ul, person.ClientId);
            Assert.AreEqual(0, person.Slot);
            Assert.AreEqual(2, person.StartingLostCount);
            Assert.AreEqual(1, person.EndingLostCount);
            Assert.AreEqual(9, person.Settlement);
        }

        [Test]
        public void Begin_InvalidRosterOrIdentity_IsRejectedWithoutPartialState()
        {
            var model = new PerformanceRewardModel(Session, 1);
            var identity = Identity();
            long before = model.Revision;
            Assert.IsFalse(model.Begin(null, Pair(), DefaultRules));
            Assert.IsFalse(model.Begin(Identity(session: OtherSession), Pair(), DefaultRules));
            Assert.IsFalse(model.Begin(Identity(generation: 2), Pair(), DefaultRules));
            Assert.IsFalse(model.Begin(identity, null, DefaultRules));
            Assert.IsFalse(model.Begin(identity, new RewardParticipantSeed[] { Pair()[0], null }, DefaultRules));
            Assert.IsFalse(model.Begin(identity, new[]
            {
                new RewardParticipantSeed("p1", 11, 0), new RewardParticipantSeed("p1", 22, 1)
            }, DefaultRules));
            Assert.IsFalse(model.Begin(identity, new[]
            {
                new RewardParticipantSeed("p1", 11, 0), new RewardParticipantSeed("p2", 11, 1)
            }, DefaultRules));
            var tooMany = new RewardParticipantSeed[65];
            for (int i = 0; i < tooMany.Length; i++) tooMany[i] = new RewardParticipantSeed("p" + i, (ulong)i, -1);
            Assert.IsFalse(model.Begin(identity, tooMany, DefaultRules));
            Assert.AreEqual(before, model.Revision);
            Assert.IsNull(model.Performance);
            Assert.IsFalse(model.IsOpen);
            Assert.AreEqual(0, model.Snapshot().Segments.Count);

            Assert.IsTrue(model.Begin(identity, Pair(), DefaultRules), "거절된 Begin이 같은 ID를 사용 처리하지 않는다");
            long afterBegin = model.Revision;
            Assert.IsFalse(model.Begin(Identity(index: 1), Pair(), DefaultRules));
            Assert.AreSame(identity, model.Performance);
            Assert.AreEqual(afterBegin, model.Revision, "열린 공연을 다른 공연으로 덮어쓰지 않는다");
        }

        [Test]
        public void Cheer_InvalidParticipant_DoesNotApplyRulesOrChangeRevision()
        {
            var model = BeginPair();
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            var before = model.Snapshot();
            var lower = new RewardRules(10, 5, 3, 0, 0);

            // 잘못된 참가자 이벤트가 다른 사람의 상한·규칙·변경 번호까지 바꾸면 안 된다.
            Assert.IsFalse(model.Cheer("missing", true, lower));
            Assert.AreEqual(before.Revision, model.Revision);
            Assert.AreEqual(before.Rules, model.Snapshot().Rules);
            Assert.AreEqual(10, model.Snapshot().Segments[0].Amount);
            Assert.IsFalse(model.Cheer(null, true, lower));
            Assert.AreEqual(before.Revision, model.Revision);
        }

        [Test]
        public void Observe_InvalidParticipantOrLostBits_DoesNotApplyRulesOrChangeRevision()
        {
            var model = BeginPair();
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            var before = model.Snapshot();
            var lower = new RewardRules(10, 5, 3, 0, 7);
            Assert.IsFalse(model.Observe("p1", 16, false, true, lower));
            Assert.IsFalse(model.Observe("p1", -1, false, true, lower));
            Assert.AreEqual(before.Revision, model.Revision);

            // 부위뿐 아니라 참가자 검증도 규칙 적용보다 먼저 끝나야 한다.
            Assert.IsFalse(model.Observe(null, 1, false, true, lower));
            Assert.AreEqual(before.Revision, model.Revision);
            Assert.AreEqual(before.Rules, model.Snapshot().Rules);
            Assert.AreEqual(10, model.Snapshot().Segments[0].Amount);
            Assert.AreEqual(0, model.Snapshot().Segments[0].LostCount);
            Assert.IsFalse(model.Observe("missing", 1, false, true, lower));
            Assert.AreEqual(before.Revision, model.Revision);
        }

        [Test]
        public void Confirm_InvalidInputAndDuplicate_DoNotPayOrMutateState()
        {
            var model = BeginPair();
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Cheer("p2", true, DefaultRules));
            long before = model.Revision;
            Assert.IsFalse(model.Confirm(true, "End", null, 0, DefaultRules, out var rejected));
            Assert.IsNull(rejected);
            Assert.IsFalse(model.Confirm(true, "End", null, 65, DefaultRules, out rejected));
            Assert.IsFalse(model.Confirm(true, null, null, 2, DefaultRules, out rejected));
            Assert.IsFalse(model.Confirm(true, " ", null, 2, DefaultRules, out rejected));
            Assert.IsFalse(model.Confirm(true, new string('x', 101), null, 2, DefaultRules, out rejected));
            Assert.IsFalse(model.Confirm(true, "End", new Dictionary<string, string> { { "missing", "Arrived" } },
                2, DefaultRules, out rejected));
            Assert.IsFalse(model.Confirm(true, "End", new Dictionary<string, string> { { "p1", " " } },
                2, DefaultRules, out rejected));
            Assert.IsNull(rejected);
            Assert.IsTrue(model.IsOpen);
            Assert.AreEqual(before, model.Revision);
            Assert.AreEqual(0, model.Balance);
            Assert.AreEqual(0, model.Ledger.Count);
            Assert.AreEqual(0, model.History.Count);

            var result = Settle(model);
            long after = model.Revision;
            Assert.IsTrue(model.Begin(result.Performance, Pair(), DefaultRules), "현재 ID의 Begin 재전송은 멱등이다");
            Assert.IsFalse(model.IsOpen, "Begin 재전송으로 확정 공연을 다시 열지 않는다");
            Assert.IsFalse(model.Confirm(true, "End", null, 2, DefaultRules, out rejected));
            Assert.IsNull(rejected);
            Assert.AreEqual(after, model.Revision);
            Assert.AreEqual(20, model.Balance);
            Assert.AreEqual(1, model.Ledger.Count);
            Assert.AreEqual(1, model.History.Count);
            Assert.AreSame(result, model.Latest);
            Assert.IsFalse(model.Cheer("p1", true, DefaultRules));
            Assert.IsFalse(model.Observe("p1", 1, false, true, DefaultRules));
            Assert.IsFalse(model.ApplyRules(new RewardRules(1, 1, 1, 1, 1)));
            Assert.AreEqual(after, model.Revision);
        }

        [Test]
        public void Begin_EvictedHistoryId_IsStillRejectedAfterTenResultLimit()
        {
            var model = new PerformanceRewardModel(Session, 1);
            var first = Identity();
            for (int i = 0; i < 11; i++)
            {
                Assert.IsTrue(model.Begin(Identity(index: i), Pair(), DefaultRules));
                Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
                Assert.IsTrue(model.Cheer("p2", true, DefaultRules));
                Settle(model);
            }
            Assert.AreEqual(10, model.History.Count);
            Assert.AreEqual(1, model.History[0].Performance.Index);
            Assert.AreEqual(10, model.Latest.Performance.Index);
            Assert.AreEqual(220, model.Balance);
            Assert.AreEqual(11, model.Ledger.Count, "결과 이력 제한으로 자금 장부를 지우지 않는다");
            long before = model.Revision;

            Assert.IsFalse(model.Begin(first, Pair(), DefaultRules), "화면 이력에서 빠진 공연도 다시 지급할 수 없다");
            Assert.IsFalse(model.Begin(Identity(day: 1, index: 9), Pair(), DefaultRules));
            Assert.AreEqual(before, model.Revision);
            Assert.AreEqual(220, model.Balance);
            Assert.AreEqual(10, model.History.Count);
        }

        [Test]
        public void Spend_ValidExpense_UpdatesSharedBalanceAndSignedLedger()
        {
            var model = FundedPair();
            long before = model.Revision;

            Assert.IsTrue(model.Spend(7, "신체 복구", 2));
            Assert.AreEqual(13, model.Balance);
            Assert.AreEqual(before + 1, model.Revision);
            Assert.AreEqual(2, model.Ledger.Count);
            var entry = model.Ledger[1];
            Assert.AreEqual(2, entry.Number);
            Assert.AreEqual(1, entry.Generation);
            Assert.AreEqual(2, entry.Day);
            Assert.AreEqual(FundEntryKind.Expense, entry.Kind);
            Assert.AreEqual(-7, entry.Amount);
            Assert.AreEqual(13, entry.Balance);
            Assert.AreEqual("신체 복구", entry.Description);
            Assert.AreEqual("", entry.PerformanceId);
            Assert.IsTrue(model.Spend(13, "남은 자금 사용", 2));
            Assert.AreEqual(0, model.Balance, "잔액과 같은 지출도 허용한다");
        }

        [Test]
        public void Spend_InsufficientNegativeOrInvalidInput_LeavesEverythingUnchanged()
        {
            var model = FundedPair();
            var before = model.Snapshot();
            Assert.IsFalse(model.Spend(21, "부족", 1));
            Assert.IsFalse(model.Spend(long.MaxValue, "부족", 1));
            Assert.IsFalse(model.Spend(-1, "음수", 1));
            Assert.IsFalse(model.Spend(long.MinValue, "음수", 1));
            Assert.IsFalse(model.Spend(0, "영", 1));
            Assert.IsFalse(model.Spend(1, null, 1));
            Assert.IsFalse(model.Spend(1, " ", 1));
            Assert.IsFalse(model.Spend(1, new string('x', 101), 1));
            Assert.IsFalse(model.Spend(1, "잘못된 일차", 0));

            Assert.AreEqual(before.Revision, model.Revision);
            Assert.AreEqual(before.Balance, model.Balance);
            Assert.AreEqual(before.LedgerCount, model.Ledger.Count);
            Assert.AreSame(before.Latest, model.Latest);
            Assert.AreEqual(1, model.History.Count);
        }

        [Test]
        public void Begin_NextDay_KeepsFundsAndStartsFreshSegments()
        {
            var model = FundedPair();
            Assert.IsTrue(model.Begin(Identity(day: 2), Pair(), DefaultRules));
            Assert.AreEqual(20, model.Balance, "일차 전환은 공동 자금 초기화 경계가 아니다");
            Assert.AreEqual(0, model.Snapshot().Segments[0].Amount);
            Assert.AreEqual(0, model.Snapshot().Segments[1].Amount);
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Cheer("p2", true, DefaultRules));

            Assert.AreEqual(20, Settle(model).Income);
            Assert.AreEqual(40, model.Balance);
            Assert.AreEqual(1, model.Ledger[0].Day);
            Assert.AreEqual(2, model.Ledger[1].Day);
            Assert.AreEqual(40, model.Ledger[1].Balance);
        }

        [Test]
        public void AdvanceGeneration_ResetsFundsAndOpenRosterExactlyOnce()
        {
            var model = FundedPair();
            var previous = model.Latest;
            Assert.IsTrue(model.Begin(Identity(index: 1), Pair(), DefaultRules));
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            long before = model.Revision;

            Assert.IsTrue(model.AdvanceGeneration(2, 1));
            Assert.AreEqual(2, model.Generation);
            Assert.AreEqual(0, model.Balance);
            Assert.IsFalse(model.IsOpen);
            Assert.IsNull(model.Performance);
            Assert.AreEqual(0, model.Snapshot().Segments.Count);
            Assert.AreSame(previous, model.Latest, "이전 세대 확정 결과는 초기화 장부와 별개로 남는다");
            Assert.AreEqual(before + 1, model.Revision);
            Assert.AreEqual(2, model.Ledger.Count);
            var reset = model.Ledger[1];
            Assert.AreEqual(FundEntryKind.GenerationReset, reset.Kind);
            Assert.AreEqual(2, reset.Generation);
            Assert.AreEqual(1, reset.Day);
            Assert.AreEqual(-20, reset.Amount);
            Assert.AreEqual(0, reset.Balance);
            long after = model.Revision;
            Assert.IsFalse(model.AdvanceGeneration(2, 1));
            Assert.IsFalse(model.AdvanceGeneration(1, 1));
            Assert.IsFalse(model.AdvanceGeneration(3, 0));
            Assert.AreEqual(after, model.Revision);
            Assert.AreEqual(2, model.Ledger.Count, "같은 세대 초기화 재전송은 장부에 중복 기록하지 않는다");
            Assert.IsFalse(model.Begin(Identity(), Pair(), DefaultRules));

            Assert.IsTrue(model.Begin(Identity(generation: 2), Pair(), DefaultRules));
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            Assert.IsTrue(model.Cheer("p2", true, DefaultRules));
            var current = Settle(model);
            Assert.AreNotEqual(previous.Performance.Id, current.Performance.Id);
            Assert.AreEqual(20, model.Balance);
            Assert.AreEqual(3, model.Ledger.Count);
            Assert.AreEqual(3, model.Ledger[2].Number);
        }

        [Test]
        public void Snapshots_CopyCallerCollectionsAndExposeReadOnlyLists()
        {
            var identity = Identity();
            var alive = new PerformanceParticipantResult("p1", 11, 0, 1, 2, false, true, "Survived", 10, 10);
            var dead = new PerformanceParticipantResult("p2", 22, 1, 0, 4, true, true, "Dead", 0, 0);
            var people = new[] { alive, dead };
            var result = new PerformanceResult(identity, PerformanceFailure.None, "End", people);
            var segment = new RewardSegment("p1", 11, 0, 1, 2, false, true, 10);
            var segments = new[] { segment };
            var entry = new FundEntry(1, 1, 1, FundEntryKind.PerformanceIncome, 10, 10, "정산", identity.Id);
            var entries = new[] { entry };
            var snapshot = new RewardSnapshot(Session, 1, 1, 10, identity, false, DefaultRules,
                segments, result, 1, 0, entries);

            people[0] = dead;
            segments[0] = new RewardSegment("replacement", 33, 2, 0, 0, false, true, 99);
            entries[0] = new FundEntry(1, 1, 1, FundEntryKind.PerformanceIncome, 99, 99, "교체");
            Assert.AreSame(alive, result.Participants[0]);
            Assert.AreSame(dead, result.Deaths[0]);
            Assert.AreEqual(10, result.Income);
            Assert.AreSame(segment, snapshot.Segments[0]);
            Assert.AreSame(entry, snapshot.Ledger[0]);
            Assert.AreEqual(10, snapshot.Balance);
            Assert.AreSame(result, snapshot.Latest);

            var readOnlyPeople = (IList<PerformanceParticipantResult>)result.Participants;
            var readOnlyDeaths = (IList<PerformanceParticipantResult>)result.Deaths;
            var readOnlySegments = (IList<RewardSegment>)snapshot.Segments;
            var readOnlyLedger = (IList<FundEntry>)snapshot.Ledger;
            Assert.IsTrue(readOnlyPeople.IsReadOnly);
            Assert.IsTrue(readOnlyDeaths.IsReadOnly);
            Assert.IsTrue(readOnlySegments.IsReadOnly);
            Assert.IsTrue(readOnlyLedger.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => readOnlyPeople[0] = dead);
            Assert.Throws<NotSupportedException>(() => readOnlyDeaths.Clear());
            Assert.Throws<NotSupportedException>(() => readOnlySegments[0] = segments[0]);
            Assert.Throws<NotSupportedException>(() => readOnlyLedger.Add(entries[0]));
        }

        [Test]
        public void Snapshot_ModelChanges_DoNotRewriteEarlierSnapshots()
        {
            var model = BeginPair();
            var empty = model.Snapshot();
            Assert.IsTrue(model.Cheer("p1", true, DefaultRules));
            var cheered = model.Snapshot();
            var result = Settle(model);
            var settled = model.Snapshot();
            Assert.IsTrue(model.Spend(5, "사용", 1));

            Assert.AreEqual(0, empty.Segments[0].Amount);
            Assert.AreEqual(0, empty.Balance);
            Assert.IsNull(empty.Latest);
            Assert.AreEqual(0, empty.Ledger.Count);
            Assert.IsTrue(empty.IsOpen);
            Assert.AreEqual(10, cheered.Segments[0].Amount);
            Assert.IsNull(cheered.Latest);
            Assert.AreEqual(0, cheered.LedgerCount);
            Assert.IsTrue(cheered.IsOpen);
            Assert.AreEqual(10, settled.Balance);
            Assert.AreEqual(1, settled.Ledger.Count);
            Assert.AreEqual(1, settled.LedgerCount);
            Assert.AreSame(result, settled.Latest);
            Assert.IsFalse(settled.IsOpen);
            Assert.AreEqual(5, model.Balance);
            Assert.IsTrue(cheered.IsNewerThan(empty));
            Assert.IsTrue(settled.IsNewerThan(cheered));
            Assert.IsFalse(empty.IsNewerThan(settled));
            Assert.Throws<NotSupportedException>(() => ((IList<PerformanceResult>)model.History).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<FundEntry>)model.Ledger).Clear());
        }

        [Test]
        public void Snapshot_IsNewerThan_RejectsStaleOtherSessionAndOlderGeneration()
        {
            RewardSnapshot Make(string session, int generation, long revision) => new(session, generation,
                revision, 0, null, false, DefaultRules, Array.Empty<RewardSegment>(), null,
                0, 0, Array.Empty<FundEntry>());
            var previous = Make(Session, 2, 10);

            Assert.IsTrue(previous.IsNewerThan(null));
            Assert.IsFalse(previous.IsNewerThan(previous));
            Assert.IsFalse(Make(Session, 2, 9).IsNewerThan(previous));
            Assert.IsTrue(Make(Session, 2, 11).IsNewerThan(previous));
            Assert.IsFalse(Make(OtherSession, 2, 11).IsNewerThan(previous));
            Assert.IsFalse(Make(Session, 1, 11).IsNewerThan(previous));
            Assert.IsTrue(Make(Session, 3, 11).IsNewerThan(previous));
            Assert.IsFalse(Make(Session, 3, 9).IsNewerThan(previous), "세대가 커도 변경 번호가 낡으면 적용하지 않는다");
        }

        [Test]
        public void RewardSnapshot_InvalidIdentityRosterOrLedger_IsRejected()
        {
            var identity = Identity();
            var emptySegments = Array.Empty<RewardSegment>();
            var emptyLedger = Array.Empty<FundEntry>();
            var segment = new RewardSegment("p1", 11, 0, 0, 0, false, true, 10);
            var entry = new FundEntry(1, 1, 1, FundEntryKind.PerformanceIncome, 10, 10, "정산");

            Assert.Throws<ArgumentException>(() => new RewardSnapshot("bad", 1, 1, 0, null, false,
                DefaultRules, emptySegments, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 0, 1, 0, null, false,
                DefaultRules, emptySegments, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 0, 0, null, false,
                DefaultRules, emptySegments, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, -1, null, false,
                DefaultRules, emptySegments, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 0, null, true,
                DefaultRules, emptySegments, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 0, Identity(session: OtherSession), true,
                DefaultRules, emptySegments, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 0, identity, true,
                DefaultRules, null, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 0, identity, true,
                DefaultRules, new[] { segment, segment }, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 0, identity, true,
                DefaultRules, new[] { new RewardSegment("p1", 11, 0, 0, 0, false, true, 101) }, null, 0, 0, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 0, null, false,
                DefaultRules, emptySegments, null, 0, 0, null));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 0, null, false,
                DefaultRules, emptySegments, null, 0, 1, emptyLedger));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 11, null, false,
                DefaultRules, emptySegments, null, 1, 0, new[] { entry }));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 10, null, false,
                DefaultRules, emptySegments, null, 1, 0, new[]
                {
                    new FundEntry(2, 1, 1, FundEntryKind.PerformanceIncome, 10, 10, "잘못된 순번")
                }));
            Assert.Throws<ArgumentException>(() => new RewardSnapshot(Session, 1, 1, 20, null, false,
                DefaultRules, emptySegments, null, 2, 0, new[]
                {
                    entry, new FundEntry(2, 1, 1, FundEntryKind.PerformanceIncome, 5, 20, "잘못된 연속 잔액")
                }));
        }

        [Test]
        public void PublicValueObjects_InvalidRulesAndSettlement_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardRules(-1, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardRules(0, -1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardRules(0, 0, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardRules(0, 0, 0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardRules(0, 0, 0, 0, -1));
            Assert.Throws<ArgumentException>(() => new PerformanceRewardModel("bad", 1));
            Assert.Throws<ArgumentException>(() => new PerformanceRewardModel(Session, 0));
            Assert.Throws<ArgumentException>(() => new PerformanceIdentity(Session, 1, 0, 0, "Trick"));
            Assert.Throws<ArgumentException>(() => new PerformanceIdentity(Session, 1, 1, -1, "Trick"));
            Assert.Throws<ArgumentException>(() => new RewardParticipantSeed(" ", 11, 0));
            Assert.Throws<ArgumentException>(() => new RewardParticipantSeed("p1", 11, 64));
            Assert.Throws<ArgumentException>(() => new RewardParticipantSeed("p1", 11, 0, 16));
            Assert.Throws<ArgumentException>(() => new RewardSegment("p1", 11, 0, 0, 0, true, true, 1));
            Assert.Throws<ArgumentException>(() => new RewardSegment("p1", 11, 0, 0, 0, false, false, 1));
            Assert.Throws<ArgumentException>(() => new FundEntry(1, 1, 1, FundEntryKind.Expense, 1, 1, "양수 지출"));
            Assert.Throws<ArgumentException>(() => new FundEntry(1, 1, 1, FundEntryKind.GenerationReset, -1, 1, "초기화 잔액"));
            Assert.Throws<ArgumentException>(() => new PerformanceParticipantResult("p1", 11, 0, 0, 0,
                false, true, "Survived", 1, 2));
            var paid = new PerformanceParticipantResult("p1", 11, 0, 0, 0, false, true, "Survived", 10, 10);
            var unpaid = new PerformanceParticipantResult("p1", 11, 0, 0, 0, false, true, "Survived", 10, 0);
            Assert.Throws<ArgumentException>(() => new PerformanceResult(Identity(), PerformanceFailure.TrickFailed,
                "End", new[] { paid }));
            Assert.Throws<ArgumentException>(() => new PerformanceResult(Identity(), PerformanceFailure.None,
                "End", new[] { unpaid }));
            Assert.Throws<ArgumentException>(() => new PerformanceResult(Identity(), PerformanceFailure.None,
                "End", new[] { paid, paid }));
            Assert.Throws<ArgumentException>(() => new PerformanceResult(Identity(), PerformanceFailure.None,
                "End", new PerformanceParticipantResult[] { null }));
        }
    }
}
