using System;
using System.Collections;
using System.Reflection;
using CurtainCall.Results;
using CurtainCall.Reward;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class PerformanceFeedbackTests
    {
        [Test]
        public void OnlyLocalExplicitReactionsDisplayAcrossRevisionGaps()
        {
            var go = new GameObject("Feedback test");
            try
            {
                var feedback = go.AddComponent<PerformanceFeedback>();
                var id = new PerformanceIdentity("11111111111111111111111111111111", 1, 1, 0, "Tightrope");
                var rules = new RewardRules(10, 5, 100, 0, 0);
                RewardSnapshot State(long revision, long score, long sequence = 0, int delta = 0, int actual = 0,
                    long remoteSequence = 0, bool open = true)
                    => new(id.SessionId, 1, revision, 0, id, open, rules, new[] {
                        new RewardSegment("p", 1, 0, 0, 0, false, true, score, sequence, delta, actual),
                        new RewardSegment("q", 2, 1, 0, 0, false, true, 30, remoteSequence, 10, 10)
                    }, null, 0, 0, Array.Empty<FundEntry>());
                int Count() => ((ICollection)typeof(PerformanceFeedback).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback)).Count;
                feedback.SetContext(true);
                feedback.Observe(State(1, 20), 1); Assert.AreEqual(0, Count());
                feedback.Observe(State(2, 19, remoteSequence: 1), 1);
                Assert.AreEqual(0, Count(), "Teammate cheer and natural decay never display.");
                feedback.Observe(State(5, 29, 1, 10, 10, 1), 1);
                Assert.AreEqual(1, Count(), "A revision gap must not drop the local success.");
                feedback.Observe(State(5, 29, 1, 10, 10, 1), 1); Assert.AreEqual(1, Count());
                feedback.Observe(State(6, 24, 2, -5, -5, 1), 1); Assert.AreEqual(2, Count());
                feedback.Observe(State(7, 0, 3, -10, 0, 1), 1);
                Assert.AreEqual(2, Count(), "Zero-score boo has no effect and is not shown.");
                feedback.Observe(State(8, 100, 4, 10, 0, 1), 1);
                Assert.AreEqual(3, Count(), "Success at the cap still cheers.");
                feedback.Observe(State(9, 100, 4, 10, 0, 2), 2);
                Assert.AreEqual(0, Count(), "Changing the observed local client seeds a new baseline.");
                feedback.Observe(State(10, 100, open: false), 2); Assert.AreEqual(0, Count());
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
