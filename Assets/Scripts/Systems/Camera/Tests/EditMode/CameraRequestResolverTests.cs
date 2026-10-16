using CurtainCall.Cameras.Requests;
using NUnit.Framework;

namespace CurtainCall.Cameras.Tests
{
    public sealed class CameraRequestResolverTests
    {
        CameraRequestResolver<object, string> resolver;
        readonly object a = new();
        readonly object b = new();

        [SetUp]
        public void SetUp() => resolver = new CameraRequestResolver<object, string>();

        [Test]
        public void EmptyUsesFallback()
        {
            Assert.That(resolver.TryResolve(out _, out _), Is.False);
            Assert.That(resolver.Count, Is.Zero);
        }

        [Test]
        public void HigherPriorityWinsEvenWhenRegisteredEarlier()
        {
            resolver.Set(a, "a", 10);
            resolver.Set(b, "b", 0);
            AssertWinner(a, "a");
        }

        [Test]
        public void EqualPriorityUsesMostRecentRegistration()
        {
            resolver.Set(a, "a", 10);
            resolver.Set(b, "b", 10);
            AssertWinner(b, "b");
        }

        [Test]
        public void UpdatingSourceKeepsRegistrationOrderAndOneEntry()
        {
            resolver.Set(a, "a", 10);
            resolver.Set(b, "b", 10);
            resolver.Set(a, "changed", 10);
            AssertWinner(b, "b");
            Assert.That(resolver.Count, Is.EqualTo(2));
            resolver.Remove(b);
            AssertWinner(a, "changed");
        }

        [Test]
        public void UpdatingPriorityChangesWinner()
        {
            resolver.Set(a, "a", 0);
            resolver.Set(b, "b", 10);
            resolver.Set(a, "a", 20);
            AssertWinner(a, "a");
        }

        [Test]
        public void RemovingWinnerRestoresRemainingRequest()
        {
            resolver.Set(a, "a", 0);
            resolver.Set(b, "b", 10);
            resolver.Remove(b);
            AssertWinner(a, "a");
            resolver.Remove(a);
            Assert.That(resolver.TryResolve(out _, out _), Is.False);
        }

        [Test]
        public void RemovingNonWinnerKeepsWinner()
        {
            resolver.Set(a, "a", 0);
            resolver.Set(b, "b", 10);
            resolver.Remove(a);
            AssertWinner(b, "b");
        }

        [Test]
        public void ReRegisteringSourceMakesItRecent()
        {
            resolver.Set(a, "a", 10);
            resolver.Set(b, "b", 10);
            resolver.Remove(a);
            resolver.Set(a, "a", 10);
            AssertWinner(a, "a");
        }

        [Test]
        public void PruningInvalidSourcesRestoresRemainingRequest()
        {
            resolver.Set(a, "a", -2);
            resolver.Set(b, "b", -1);
            resolver.RemoveWhere((source, value) => source == b);
            AssertWinner(a, "a");
        }

        [Test]
        public void FilteringFirstPersonForSpectatingKeepsRequestForRecovery()
        {
            resolver.Set(a, "third", 0);
            resolver.Set(b, "first", 10);
            Assert.That(resolver.TryResolve(out var source, out var value, v => v != "first"), Is.True);
            Assert.That(source, Is.SameAs(a));
            Assert.That(value, Is.EqualTo("third"));
            AssertWinner(b, "first");
        }

        [Test]
        public void ClearRemovesAllRequests()
        {
            resolver.Set(a, "a", 0);
            resolver.Clear();
            Assert.That(resolver.Count, Is.Zero);
            Assert.That(resolver.TryResolve(out _, out _), Is.False);
        }

        void AssertWinner(object expectedSource, string expectedValue)
        {
            Assert.That(resolver.TryResolve(out var source, out var value), Is.True);
            Assert.That(source, Is.SameAs(expectedSource));
            Assert.That(value, Is.EqualTo(expectedValue));
        }
    }
}
