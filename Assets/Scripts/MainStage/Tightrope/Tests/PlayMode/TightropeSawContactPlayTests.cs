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
