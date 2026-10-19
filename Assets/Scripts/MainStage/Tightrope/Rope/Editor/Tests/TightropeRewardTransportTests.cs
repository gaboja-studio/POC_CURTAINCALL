using System;
using System.Reflection;
using CurtainCall.Results;
using CurtainCall.Reward;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    /// <summary>실제 보상 DTO의 JsonUtility 전송 왕복에서 선택적인 공연·결과를 보존한다.</summary>
    public sealed class TightropeRewardTransportTests
    {
        const string Session = "11111111111111111111111111111111";
        static readonly RewardRules Rules = new(10, 5, 100, 5, 7);
        static readonly Type DtoType = typeof(PerformanceRewards).Assembly.GetType("CurtainCall.Reward.RewardStateData", true);
        static readonly MethodInfo From = DtoType.GetMethod("From", BindingFlags.Public | BindingFlags.Static);
        static readonly MethodInfo Read = DtoType.GetMethod("Read", BindingFlags.Public | BindingFlags.Instance);

        static PerformanceRewardModel Begin()
        {
            var model = new PerformanceRewardModel(Session, 1);
            Assert.IsTrue(model.Begin(new PerformanceIdentity(Session, 1, 1, 0, "Tightrope"), new[]
            {
                new RewardParticipantSeed("host", 0, 0),
                new RewardParticipantSeed("client1", 1, 1, 3),
                new RewardParticipantSeed("client2", 2, 2),
            }, Rules));
            return model;
        }

        static PerformanceResult Settle(PerformanceRewardModel model)
        {
            Assert.IsTrue(model.Cheer("host", true, Rules));
            Assert.IsTrue(model.Confirm(true, "Completed", null, 1, Rules, out var result));
            return result;
        }

        static object ToDto(RewardSnapshot snapshot) => From.Invoke(null, new object[] { snapshot });
        static object ThroughJson(object dto) => JsonUtility.FromJson(JsonUtility.ToJson(dto), DtoType);
        static RewardSnapshot RoundTrip(RewardSnapshot snapshot) => (RewardSnapshot)Read.Invoke(ThroughJson(ToDto(snapshot)), null);

        [Test]
        public void IntroWithoutPerformanceOrResultRemainsEmpty()
        {
            var expected = new PerformanceRewardModel(Session, 1).Snapshot();
            var actual = RoundTrip(expected);
            Assert.AreEqual(Session, actual.SessionId);
            Assert.AreEqual(expected.Revision, actual.Revision);
            Assert.IsNull(actual.Performance);
            Assert.IsNull(actual.Latest);
            Assert.IsFalse(actual.IsOpen);
            Assert.IsEmpty(actual.Segments);
            Assert.IsEmpty(actual.Ledger);
        }

        [Test]
        public void FirstOpenPerformanceKeepsRosterWithoutInventingLatestResult()
        {
            var expected = Begin().Snapshot();
            var actual = RoundTrip(expected);
            Assert.AreEqual(expected.Performance.Id, actual.Performance.Id);
            Assert.IsTrue(actual.IsOpen);
            Assert.IsNull(actual.Latest);
            Assert.AreEqual(3, actual.Segments.Count);
            Assert.AreEqual(1ul, actual.Segments[1].ClientId);
            Assert.AreEqual(2, actual.Segments[1].StartingLostCount);
            Assert.IsFalse(actual.Segments[1].Dead);
            Assert.AreEqual(Rules, actual.Rules);
        }

        [Test]
        public void GenerationResetHasNoCurrentPerformanceButPreservesPreviousResult()
        {
            var model = Begin();
            var previous = Settle(model);
            Assert.IsTrue(model.AdvanceGeneration(2, 1));
            var actual = RoundTrip(model.Snapshot());
            Assert.AreEqual(2, actual.Generation);
            Assert.IsNull(actual.Performance);
            Assert.IsFalse(actual.IsOpen);
            Assert.IsEmpty(actual.Segments);
            Assert.AreEqual(0, actual.Balance);
            Assert.AreEqual(previous.Performance.Id, actual.Latest.Performance.Id);
            Assert.AreEqual(previous.Income, actual.Latest.Income);
            Assert.AreEqual(FundEntryKind.GenerationReset, actual.Ledger[1].Kind);
        }

        [Test]
        public void SettledResultPreservesParticipantsIncomeAndLedger()
        {
            var model = Begin();
            var result = Settle(model);
            var actual = RoundTrip(model.Snapshot());
            Assert.IsFalse(actual.IsOpen);
            Assert.AreEqual(result.Performance.Id, actual.Performance.Id);
            Assert.AreEqual(result.Performance.Id, actual.Latest.Performance.Id);
            Assert.AreEqual(result.Income, actual.Balance);
            Assert.AreEqual(result.Income, actual.Latest.Income);
            Assert.AreEqual(3, actual.Latest.Participants.Count);
            Assert.AreEqual(2, actual.Latest.Participants[1].StartingLostCount);
            Assert.AreEqual(actual.Balance, actual.Ledger[0].Balance);
        }

        [TestCase("performance")]
        [TestCase("latest")]
        public void PresentButMalformedIdentityIsStillRejected(string field)
        {
            var model = Begin();
            Settle(model);
            var dto = ToDto(model.Snapshot());
            var identity = DtoType.GetField(field).GetValue(dto);
            if (field == "latest") identity = identity.GetType().GetField("performance").GetValue(identity);
            identity.GetType().GetField("session").SetValue(identity, "invalid-session");

            var received = ThroughJson(dto);
            var error = Assert.Throws<TargetInvocationException>(() => Read.Invoke(received, null));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
        }
    }
}
