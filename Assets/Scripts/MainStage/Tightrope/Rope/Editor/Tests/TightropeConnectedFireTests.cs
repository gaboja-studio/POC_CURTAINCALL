using System.Collections.Generic;
using System.Linq;
using CurtainCall.Tightrope.Fire;
using NUnit.Framework;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeConnectedFireTests
    {
        [Test]
        public void EveryBurnLeavesAdjacentEscapeRouteAndAnyLaneMaySurvive()
        {
            var survivors = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                var order = FireMath.RandomConnectedLaneOrder(6, seed);
                CollectionAssert.AreEquivalent(Enumerable.Range(1, 6), order);
                var remaining = new SortedSet<int>(order);
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(order[i] == remaining.Min || order[i] == remaining.Max,
                        "중간 줄이 먼저 사라져 건널 수 없는 틈이 생기면 안 된다.");
                    remaining.Remove(order[i]);
                    Assert.That(remaining.Max - remaining.Min + 1, Is.EqualTo(remaining.Count));
                }
                survivors.Add(order[5]);
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 6), survivors);
        }

        [Test]
        public void FastestWalkStillReceivesFiveFullWarningsBeforeFinalSection()
        {
            var settings = new FireSettings
            {
                warningSeconds = 20, triggerMode = FireTriggerMode.LeadDistance,
                segments = Enumerable.Range(0, 5).Select(i => new FireSegment(i + 1, 0, 330, 0, 15 + i * 60)).ToArray()
            };
            ulong active = 0; int pending = -1; float ignite = -1;
            for (int i = 0; i < 5; i++)
            {
                float startTime = 5 + i * 20;
                FireMath.AdvanceSequence(settings, startTime, startTime * 3, 6, ref active, ref pending, ref ignite);
                Assert.That(pending, Is.EqualTo(i));
                Assert.That(ignite, Is.EqualTo(startTime + 20));
                FireMath.AdvanceSequence(settings, ignite, ignite * 3, 6, ref active, ref pending, ref ignite);
                Assert.That(active & (1UL << i), Is.Not.Zero);
            }
            Assert.That(active, Is.EqualTo(31UL));
        }
    }
}
