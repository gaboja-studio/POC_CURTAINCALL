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
        [Test] public void OfficialRoster_PreservesFourClientsAndUnknownSlotWithoutInventingAnEvent()
        {
            var e = Valid(); e.runId = Guid.NewGuid().ToString("D"); e.attemptId = Guid.NewGuid().ToString("D");
            e.eventType = "PERFORMANCE_ROSTER"; e.authority = "HOST";
            var people = new List<Dictionary<string, object>>();
            for (int i = 0; i < 4; i++) people.Add(new() { ["playerId"] = "p" + i, ["networkClientId"] = i.ToString(), ["playerNumber"] = i == 3 ? null : i });
            e.payload = new() { ["trickType"] = "CANNON", ["difficulty"] = 1, ["playerCount"] = 4, ["participants"] = people };
            Assert.That(EventContract.IsValid(e), Is.True);
            people[3]["networkClientId"] = "0"; Assert.That(EventContract.IsValid(e), Is.False);
            people[3]["networkClientId"] = "3"; people[3]["playerNumber"] = 3; Assert.That(EventContract.IsValid(e), Is.True);
            people[3]["playerNumber"] = 4; Assert.That(EventContract.IsValid(e), Is.False);
        }
        [Test] public void Arrival_RequiresActualPositionAndPerformanceContext()
        {
            var e = Valid(); e.eventType = "PLAYER_ARRIVED"; e.authority = "HOST";
            e.payload = new() { ["sectionId"] = null, ["position"] = new { x = 0, y = 1, z = 50 }, ["lostCount"] = 0 };
            Assert.That(EventContract.IsValid(e), Is.False);
            e.runId = Guid.NewGuid().ToString("D"); e.attemptId = Guid.NewGuid().ToString("D");
            Assert.That(EventContract.IsValid(e), Is.True);
            e.payload["position"] = null; Assert.That(EventContract.IsValid(e), Is.False);
        }
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
