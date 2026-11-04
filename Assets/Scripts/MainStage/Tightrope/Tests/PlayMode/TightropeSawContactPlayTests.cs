using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator SawOwnerContactVerticalAndBothGatePhasesValidateHistoricalBoundary()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(0, 4f);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            yield return Until(() => (bool)Get(saws, "CanJudgeContact"), 3, "Contact clock ready");
            ((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"))).enabled = false;
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            var rootSettings = Static("CurtainCall.Settings.GameSettings", "Tightrope");
            var originalSettings = Get(saws, "Settings");
            // These optional gate types are disabled in the authored performance.
            // Temporarily swap a value copy; never save or re-enable them in the asset.
            var settings = JsonUtility.FromJson(JsonUtility.ToJson(originalSettings), originalSettings.GetType());
            Set(settings, "cooperativeGates", true);
            Set(settings, "verticalSpawnDistance", 8f);
            Set(settings, "cooperativeGateDistances", new[] {12f}); // Inside the current test course, unlike the removed 158m finale.
            Set(rootSettings, "saws", settings);
            uint serial = 10;
            try {
                for (int phase = 0; phase < 3; phase++) {
                    if (phase > 0) yield return new WaitForSeconds((float)Get(settings, "hitCooldown") + .1f);
                    Call(mover, "SetStartPose", player.transform.position + Vector3.up * 8, Quaternion.identity, true);
                    if (phase == 0) Call(saws, "SetVertical", true, 0, 700, 0f, -1f);
                    else {
                        Call(saws, "SetVertical", false, 0, 700, 0f, -1f);
                        Call(saws, "SetCooperativeGate", 0, true, (float)Get(saws, "GameElapsed"), -1f);
                        if (phase == 2) {
                            Call(saws, "ReleaseCooperativeGate");
                            Call(saws, "SetCooperativeGate", 0, false, (float)Get(saws, "GameElapsed"), -1f);
                        }
                    }
                    // Let the normal Update record the new source on a new clock tick.
                    // Two manual samples in one frame have the same timestamp; the earlier
                    // one legitimately still describes the pre-activation scene.
                    yield return null;
                    if (phase == 2) Call(saws, "SetCooperativeGate", 0, false, (float)Get(saws, "GameElapsed"), -1f);
                    var shapes = (IEnumerable)Get(saws, "shownContactShapes");
                    Call(saws, "CollectContactShapes", shapes);
                    object selected = null;
                    foreach (var shape in shapes)
                        if (Get(shape, "Kind").ToString() == (phase == 0 ? "Vertical" : "CooperativeGate")) { selected = shape; break; }
                    Assert.IsNotNull(selected, "Visible shape exists for phase " + phase);
                    var kind = Get(selected, "Kind"); int index = (int)Get(selected, "Index");
                    var disc = Get(selected, "Disc"); float height = (float)Get(mover, "BodyHeight");
                    var feet = (Vector3)Get(disc, "Center") - Vector3.up * (height * .5f);
                    var avoided = feet + (Vector3)Get(disc, "Normal") * 5;
                    Call(mover, "SetStartPose", avoided, Quaternion.identity, true);
                    Set(saws, "nextShapeSample", 0d); Call(saws, "RememberContactShapes");
                    double at = (double)Get(saws, "SawClock"); int round = (int)Get(run, "Round"), restart = (int)Get(player, "RestartVersion");
                    ulong owner = (ulong)Get(player, "OwnerClientId"); int before = Convert.ToInt32(Get(player, "LostParts"));
                    yield return new WaitForSeconds(.3f);
                    Assert.IsTrue((bool)Get(saws, "CanJudgeContact"), $"phase {phase}: run={Get(run, "State")}");
                    object nearest = null; double nearestAge = .075;
                    foreach (var frame in (Array)Get(saws, "contactHistory")) {
                        if (frame == null) continue;
                        double age = at - (double)Get(frame, "At");
                        if (age >= 0 && age < nearestAge) { nearest = frame; nearestAge = age; }
                    }
                    Assert.IsNotNull(nearest, "Historical sample exists at phase " + phase);
                    object historicalDisc = null;
                    foreach (var shape in (IEnumerable)Get(nearest, "Shapes"))
                        if (Get(shape, "Kind").Equals(kind) && (int)Get(shape, "Index") == index) historicalDisc = Get(shape, "Disc");
                    Assert.IsNotNull(historicalDisc, "Historical shape identity exists at phase " + phase);
                    var body = Activator.CreateInstance(GameType("CurtainCall.Tightrope.Saws.BodyCapsule"), feet, height, (float)Get(mover, "BodyRadius"));
                    Assert.IsTrue((bool)GameType("CurtainCall.Tightrope.Saws.SawMath").GetMethod("TryGetContact")
                        .Invoke(null, new[] {historicalDisc, body, (object)Vector3.zero}), "Historical geometry matches at phase " + phase);
                    Assert.IsFalse((bool)Call(saws, "ValidateOwnerContact", owner, ++serial, round, restart, at, avoided, height, kind, index, true));
                    Assert.AreEqual(before, Convert.ToInt32(Get(player, "LostParts")), "Visible avoidance remains safe");
                    Assert.IsTrue((bool)Call(saws, "ValidateOwnerContact", owner, ++serial, round, restart, at, feet, height, kind, index, true),
                        $"phase={phase}, kind={kind}/{index}, state={Get(player, "State")}, ready={Call(run, "IsParticipantReady", player)}, age={(double)Get(saws, "SawClock") - at}, drift={Vector3.Distance(feet, player.transform.position)}, height={height}, nowHeight={Get(mover, "BodyHeight")}");
                    int after = Convert.ToInt32(Get(player, "LostParts"));
                    Assert.AreEqual(CountBits(before) + 1, CountBits(after), "One historical contact cuts one limb");
                    Assert.IsTrue((bool)Call(saws, "ValidateOwnerContact", owner, ++serial, round, restart, at, feet, height, kind, index, true));
                    Assert.AreEqual(after, Convert.ToInt32(Get(player, "LostParts")));
                    Assert.IsTrue((bool)Call(saws, "ValidateOwnerContact", owner, ++serial, round, restart, at, feet, height, kind, index, false));
                }
            } finally { Set(rootSettings, "saws", originalSettings); }
        }

        [UnityTest]
        public IEnumerator SawOwnerContactUsesHistoryAndRejectsAvoidanceAndDuplicateCut()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(0, 4f);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            yield return Until(() => (bool)Get(saws, "CanJudgeContact"), 3, "Performance clock starts before contact sampling");
            int index = ActiveHorizontalSaw(saws);
            Call(((Array)Get(saws, "tracks")).GetValue(index), "Start", (float)Get(saws, "GameElapsed"));
            var center = (Vector3)Call(saws, "GetHorizontalPosition", index);
            float height = (float)Get(mover, "BodyHeight");
            var feet = center - Vector3.up * (height * .5f);
            ((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"))).enabled = false;
            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
            Call(mover, "SetStartPose", feet + Vector3.up * 5, Quaternion.identity, true);
            Set(saws, "nextShapeSample", 0d); Call(saws, "RememberContactShapes");
            double at = (double)Get(saws, "SawClock");
            int round = (int)Get(run, "Round"), restart = (int)Get(player, "RestartVersion");
            ulong owner = (ulong)Get(player, "OwnerClientId");
            var kind = Enum.Parse(GameType("CurtainCall.Tightrope.Saws.SawKind"), "Horizontal");
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.IsTrue((bool)Get(saws, "CanJudgeContact"));
            Assert.IsTrue((bool)Call(run, "IsParticipantReady", player));
            Assert.IsFalse((bool)Call(saws, "IsHorizontalSafeForTeamPassage", index));
            Assert.That((double)Get(saws, "SawClock") - at, Is.InRange(0d, 1.25d), "Report age");
            Assert.AreEqual(restart, (int)Get(player, "RestartVersion"));
            var history = (Array)Get(saws, "contactHistory");
            object sample = null; double closest = .075;
            foreach (var item in history) {
                if (item == null) continue;
                double delta = at - (double)Get(item, "At");
                if (delta >= 0 && delta < closest) { sample = item; closest = delta; }
            }
            Assert.IsNotNull(sample, "A sample at the reported time must remain in the ring.");
            object disc = null;
            foreach (var shape in (IEnumerable)Get(sample, "Shapes"))
                if ((int)Get(shape, "Index") == index && Get(shape, "Kind").ToString() == "Horizontal") disc = Get(shape, "Disc");
            Assert.IsNotNull(disc, "Reported horizontal saw must exist in history.");
            var body = Activator.CreateInstance(GameType("CurtainCall.Tightrope.Saws.BodyCapsule"), feet, height, (float)Get(mover, "BodyRadius"));
            Assert.IsTrue((bool)GameType("CurtainCall.Tightrope.Saws.SawMath").GetMethod("TryGetContact").Invoke(null, new[] {disc, body, (object)Vector3.zero}), "Reported body must touch historical shape.");
            Assert.IsFalse((bool)Call(saws, "ValidateOwnerContact", owner, 1u, round, restart, at, feet + Vector3.up * 4, height, kind, index, true));
            Assert.IsFalse((bool)Call(saws, "ValidateOwnerContact", owner, 1u, round - 1, restart, at, feet, height, kind, index, true));
            Assert.IsFalse((bool)Call(saws, "ValidateOwnerContact", owner, 1u, round, restart - 1, at, feet, height, kind, index, true));
            Assert.IsFalse((bool)Call(saws, "ValidateOwnerContact", owner, 1u, round, restart, at - 2, feet, height, kind, index, true));
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            Assert.IsTrue((bool)Call(saws, "ValidateOwnerContact", owner, 2u, round, restart, at, feet, height, kind, index, true));
            int cut = Convert.ToInt32(Get(player, "LostParts"));
            Assert.AreEqual(1, CountBits(cut));
            Assert.IsFalse((bool)Call(saws, "ValidateOwnerContact", owner, 2u, round, restart, at, feet, height, kind, index, true));
            Assert.IsTrue((bool)Call(saws, "ValidateOwnerContact", owner, 3u, round, restart, at, feet, height, kind, index, true));
            Assert.AreEqual(cut, Convert.ToInt32(Get(player, "LostParts")), "Retries while touching must not cut another limb.");
            Assert.IsTrue((bool)Call(saws, "ValidateOwnerContact", owner, 4u, round, restart, at, feet, height, kind, index, false));
        }

        [UnityTest]
        public IEnumerator SawHostCannotCutRemoteWithoutOwnerContact()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(0, 4f);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            yield return Until(() => (bool)Get(saws, "CanJudgeContact"), 3, "Performance starts before remote damage check");
            int index = ActiveHorizontalSaw(saws);
            var center = (Vector3)Call(saws, "GetHorizontalPosition", index);
            Call(mover, "SetStartPose", center + Vector3.up * 5, Quaternion.identity, true);
            var remote = Object.Instantiate((GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab"));
            var netObject = remote.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            try {
                Call(netObject, "SpawnWithOwnership", 777UL, true);
                var remotePlayer = remote.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var remoteMover = remote.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                Call(remoteMover, "SetStartPose", center - Vector3.up * ((float)Get(remoteMover, "BodyHeight") * .5f), Quaternion.identity, true);
                Set(run, "externallyManaged", true);
                Assert.IsTrue((bool)Call(run, "IsParticipantReady", remotePlayer));
                Call(saws, "ServerJudgeHits");
                Assert.AreEqual(0, Convert.ToInt32(Get(remotePlayer, "LostParts")));
            } finally { Call(netObject, "Despawn", true); }
        }
    }
}
