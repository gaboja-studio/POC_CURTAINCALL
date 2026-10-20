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
        public void SnapshotFeedback_SkipsInitialRepeatGapsDeathDisconnectAndClosedState()
        {
            var go = new GameObject("Feedback test");
            try
            {
                var feedback = go.AddComponent<PerformanceFeedback>();
                var id = new PerformanceIdentity("11111111111111111111111111111111", 1, 1, 0, "Tightrope");
                var rules = new RewardRules(10, 5, 100, 0, 0);
                RewardSnapshot State(long revision, long score, bool dead = false, bool connected = true, bool open = true)
                    => new(id.SessionId, 1, revision, 0, id, open, rules,
                        new[] { new RewardSegment("p", 0, 0, 0, 0, dead, connected, score) }, null, 0, 0, Array.Empty<FundEntry>());
                int Count() => ((ICollection)typeof(PerformanceFeedback).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback)).Count;
                feedback.SetContext(true);
                feedback.Observe(State(1, 20)); Assert.AreEqual(0, Count());
                feedback.Observe(State(2, 30)); Assert.AreEqual(1, Count());
                feedback.Observe(State(2, 30)); Assert.AreEqual(1, Count());
                feedback.Observe(State(4, 60)); Assert.AreEqual(0, Count(), "Skipped revision is resync, not a new reaction.");
                feedback.Observe(State(5, 50)); Assert.AreEqual(1, Count());
                feedback.SetContext(false); feedback.SetContext(true);
                feedback.Observe(State(6, 0, dead: true)); Assert.AreEqual(0, Count());
                feedback.Observe(State(7, 0, connected: false)); Assert.AreEqual(0, Count());
                feedback.Clear(); feedback.Observe(State(8, 10)); Assert.AreEqual(0, Count());
                feedback.Observe(State(9, 20)); Assert.AreEqual(1, Count());
                feedback.Observe(State(10, 20, open: false)); Assert.AreEqual(0, Count());
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
