using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CurtainCall.Telemetry.Pipeline.EditorTests
{
    public sealed class EventContractTests
    {
        static LogEvent Valid() => new()
        {
            occurredAt = "2026-10-21T08:00:00.000Z", sequence = 1, monotonicMs = 0,
            environment = "TEST", playerId = "test-user", sceneType = "TITLE", eventType = "GAME_STARTED", authority = "LOCAL"
        };
        [Test] public void BaseEvent_AllowsUnavailableContext() => Assert.That(EventContract.IsValid(Valid()), Is.True);
        [TestCase("0.1.0", true)]
        [TestCase("0.2.0-preview.1", true)]
        [TestCase("", false)]
        [TestCase("=IMPORTXML()", false)]
        public void GameVersion_ValidatesWhenPresent(string version, bool expected)
        {
            var entry = Valid(); entry.gameVersion = version;
            Assert.That(EventContract.IsValid(entry), Is.EqualTo(expected));
        }
        [Test] public void UnknownEvent_AndInvalidField_AreRejectedLocally()
        {
            var e = Valid(); e.eventType = "UNREGISTERED"; Assert.That(EventContract.IsValid(e), Is.False);
            e.eventType = "INJURY_CHANGED"; e.payload = new() { ["beforeLostCount"] = 0, ["afterLostCount"] = 5 };
            Assert.That(EventContract.IsValid(e), Is.False);
        }
        [Test] public void PerformanceRequiresSharedRunAndAttemptIds()
        {
            var e = Valid(); e.eventType = "SECTION_ENTERED"; e.payload = new() { ["sectionId"] = 1, ["lostCount"] = 0 };
            Assert.That(EventContract.IsValid(e), Is.False);
            e.runId = Guid.NewGuid().ToString("D"); e.attemptId = Guid.NewGuid().ToString("D");
            Assert.That(EventContract.IsValid(e), Is.True);
        }
        [Test] public void MicrophoneEnd_CannotPrecedeStart()
        {
            var e = Valid(); e.eventType = "MICROPHONE_INPUT";
            e.payload = new() { ["startedAt"] = "2026-10-21T08:00:02Z", ["endedAt"] = "2026-10-21T08:00:01Z" };
            Assert.That(EventContract.IsValid(e), Is.False);
        }
    }
}
