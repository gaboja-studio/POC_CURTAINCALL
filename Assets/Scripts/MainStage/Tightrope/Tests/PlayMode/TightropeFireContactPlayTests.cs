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
        Component PrepareContactFire()
        {
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            var fire = (Component)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            var original = Get(fire, "Settings");
            var settings = JsonUtility.FromJson(JsonUtility.ToJson(original), original.GetType());
            Set(fire, "snapshot", settings); // A new snapshot also rebuilds FireView's segment cache.
            Set(settings, "chaseEnabled", true); Set(settings, "chaseDelay", 0f);
            Set(settings, "chaseStart", 8f); Set(settings, "chaseSpeed", 0f);
            Set(settings, "segments", Array.CreateInstance(GameType("CurtainCall.Tightrope.Fire.FireSegment"), 0));
            return fire;
        }

        [UnityTest, Category("CommonRulesFollowup")]
        public IEnumerator FireHostCannotKillRemoteWithoutDisplayedContact()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(0, 12f);
            var fire = PrepareContactFire();
            var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
            var remote = Object.Instantiate(prefab);
            var netObject = remote.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            try {
                Call(netObject, "SpawnWithOwnership", 777UL, true);
                var remotePlayer = remote.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var remoteMover = remote.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                var feet = (Vector3)Call(course, "GetLanePosition", 1, 6f);
                Call(remoteMover, "SetStartPose", feet, Quaternion.identity, true);
                // The owner gate must be tested independently of placement readiness.
                Set(run, "externallyManaged", true);
                Assert.IsTrue((bool)Call(run, "IsParticipantReady", remotePlayer));
                Call(fire, "Judge");
                Assert.AreEqual("Normal", Get(remotePlayer, "State").ToString());
                Assert.AreEqual(0, Convert.ToInt32(Get(remotePlayer, "LostParts")));
            } finally { Call(netObject, "Despawn", true); }
        }

        [UnityTest, Category("CommonRulesFollowup")]
        public IEnumerator FireContactRejectsAvoidanceOldRoundRestartAndStaleReport()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(0, 12f);
            var fire = PrepareContactFire();
            int round = (int)Get(fire, "Round"), restart = (int)Get(player, "RestartVersion");
            ulong owner = (ulong)Get(player, "OwnerClientId");
            var kind = Enum.Parse(GameType("CurtainCall.Tightrope.Fire.FireKind"), "Chase");
            var contact = (Vector3)Call(course, "GetLanePosition", 0, 6f);
            double at = (float)Get(fire, "Elapsed");
            Assert.IsFalse((bool)Call(fire, "ValidateContact", owner, 1u, round, restart, at, player.transform.position, kind, -1, true), "Safe owner pose must not be killed.");
            Assert.IsFalse((bool)Call(fire, "ValidateContact", owner, 1u, round - 1, restart, at, contact, kind, -1, true));
            Assert.IsFalse((bool)Call(fire, "ValidateContact", owner, 1u, round, restart - 1, at, contact, kind, -1, true));
            Assert.IsFalse((bool)Call(fire, "ValidateContact", owner, 1u, round, restart, at - 2, contact, kind, -1, true));
            Assert.IsFalse((bool)Call(fire, "ValidateContact", owner, 1u, round, restart, double.NaN, contact, kind, -1, true));
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            // Report-time contact is valid even if the owner has moved out before receipt.
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue((bool)Call(fire, "ValidateContact", owner, 2u, round, restart, at, contact, kind, -1, true));
            Assert.AreEqual("Fallen", Get(player, "State").ToString());
            Assert.IsFalse((bool)Call(fire, "ValidateContact", owner, 2u, round, restart, at, contact, kind, -1, true));
        }

        [UnityTest, Category("CommonRulesFollowup")]
        public IEnumerator FireSegmentRejectsContactBeforeIgnition()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(0, 12f);
            var fire = PrepareContactFire();
            Set(Get(fire, "Settings"), "chaseEnabled", false);
            var segment = Activator.CreateInstance(GameType("CurtainCall.Tightrope.Fire.FireSegment"));
            Set(segment, "lane", 1); Set(segment, "from", 6f); Set(segment, "to", 8f);
            var segments = Array.CreateInstance(segment.GetType(), 1); segments.SetValue(segment, 0);
            Set(Get(fire, "Settings"), "segments", segments);
            Set(fire, "activeMask", 1UL);
            float activated = (float)Get(fire, "Elapsed");
            ((float[])Get(fire, "segmentActivatedAt"))[0] = activated;
            var kind = Enum.Parse(GameType("CurtainCall.Tightrope.Fire.FireKind"), "Segment");
            var feet = (Vector3)Call(course, "GetLanePosition", 0, 7f);
            int round = (int)Get(fire, "Round"), restart = (int)Get(player, "RestartVersion");
            ulong owner = (ulong)Get(player, "OwnerClientId");
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse((bool)Call(fire, "ValidateContact", owner, 1u, round, restart, (double)activated - .01, feet, kind, 0, true));
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.IsTrue((bool)Call(fire, "ValidateContact", owner, 2u, round, restart, (double)activated, feet, kind, 0, true));
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.AreNotEqual(0, Convert.ToInt32(Get(player, "LostParts")));
        }
    }
}
