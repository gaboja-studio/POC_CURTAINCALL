using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator PursuitCheats_TimerStageCaptureCancelAndRepeat()
        {
            yield return PursuitHost();
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var poseType = T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose");
            Assert.IsTrue((bool)Get(director, "CanDebug"));
            Assert.IsTrue((bool)Call(director, "DebugSetTimer", 60f));
            Assert.IsTrue((bool)Call(director, "DebugAdjustTimer", 60f));
            Assert.AreEqual(120f, Field(Get(run, "State"), "TimeRemaining"));
            Assert.IsTrue((bool)Call(director, "DebugAdjustTimer", -60f));
            Assert.AreEqual(60f, Field(Get(run, "State"), "TimeRemaining"));
            Call(run, "SetRestTimerPaused", true);
            Assert.IsTrue((bool)Call(director, "DebugAdjustTimer", -60f));
            Assert.AreEqual("Pursuit", Field(Get(run, "State"), "Timer").ToString());
            Assert.GreaterOrEqual((float)Get(director, "Elapsed"), 0);
            var rules = Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit");
            var stages = (Array)Field(rules, "Stages");
            Assert.IsTrue((bool)Call(director, "DebugSetStage", stages.Length - 1));
            Assert.IsTrue((bool)Get(director, "StageIsForced"));
            Assert.AreSame(stages.GetValue(stages.Length - 1), Get(director, "CurrentStageRules"));
            yield return null;
            Assert.AreEqual(stages.Length - 1, Get(director, "CurrentStageIndex"));
            Assert.IsFalse((bool)Call(director, "DebugSetStage", stages.Length));
            Assert.IsTrue((bool)Call(director, "DebugSetStage", -1));
            Assert.IsFalse((bool)Get(director, "StageIsForced"));
            Assert.AreEqual(Call(rules, "StageAt", Get(director, "Elapsed")), Get(director, "CurrentStageIndex"));

            for (int attempt = 0; attempt < 2; attempt++) {
                Assert.IsTrue((bool)Call(director, "DebugExpireTimer"));
                Call(director, "BeginCapture", actor, player.gameObject);
                Assert.IsNotNull(player.GetComponent(poseType));
                if (attempt == 0) Call(player, "ServerCutPart", Enum.ToObject(T("CurtainCall.Player.BodyPart"), 4));
                else Call(player, "ServerRetireFromPursuit");
                var injury = Get(player, "LostParts");
                Assert.IsTrue((bool)Call(director, "DebugResetTimer"));
                yield return null; yield return null;
                Assert.IsNull(player.GetComponent(poseType));
                Assert.IsNull(player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitCaptureBlood")));
                Assert.AreEqual(injury, Get(player, "LostParts"));
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.AreEqual(-1f, Get(director, "Elapsed"));
                Assert.AreEqual("Display", Get(actor, "State").ToString());
                Assert.IsNull(Get(actor, "Victim"));
                Assert.IsFalse((bool)Get(director, "StageIsForced"));
            }
            Assert.IsTrue((bool)Call(director, "DebugExpireTimer"));
            Assert.IsTrue((bool)Call(director, "DebugSetTimer", 60f));
            Assert.AreEqual(-1f, Get(director, "Elapsed"));
            DevCache.SetValue(null, false);
            Assert.IsFalse((bool)Call(director, "DebugExpireTimer"));
            Assert.IsFalse((bool)Call(director, "DebugSetInvulnerable", true));
            Assert.IsFalse((bool)Call(director, "DebugSetRecognition", false));
            DevCache.SetValue(null, true);
            var hostField = run.GetType().GetField("host", BindingFlags.NonPublic | BindingFlags.Instance);
            var savedHost = hostField.GetValue(run);
            try {
                hostField.SetValue(run, null);
                Assert.IsFalse((bool)Get(director, "CanDebug"));
                Assert.IsFalse((bool)Call(director, "DebugResetTimer"));
                Assert.IsFalse((bool)Call(director, "DebugSetStage", 0));
                Assert.IsFalse((bool)Call(director, "DebugSetInvulnerable", true));
                Assert.IsFalse((bool)Call(director, "DebugSetRecognition", false));
            } finally { hostField.SetValue(run, savedHost); }
        }

        [UnityTest]
        public IEnumerator PursuitCheats_ReceivedCancellationRemovesCapturePresentation()
        {
            yield return PursuitHost();
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            var actors = ((IEnumerable)Get(director, "Actors")).Cast<Component>().ToArray();
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            Assert.IsTrue((bool)Call(director, "DebugExpireTimer"));
            Call(director, "BeginCapture", actors[0], player.gameObject);
            var state = Get(run, "State");
            var type = director.GetType();
            var snapshotType = type.GetNestedType("Snapshot", BindingFlags.NonPublic);
            var snapshot = Activator.CreateInstance(snapshotType);
            Set(snapshot, "generation", Field(state, "Generation"));
            Set(snapshot, "day", Field(state, "Day"));
            Set(snapshot, "performance", Field(state, "PerformanceIndex"));
            Set(snapshot, "elapsed", -1f); Set(snapshot, "stage", -1);
            Set(snapshot, "debugInvulnerable", true); Set(snapshot, "debugIgnorePlayers", true);
            var actorType = type.GetNestedType("ActorState", BindingFlags.NonPublic);
            var states = Array.CreateInstance(actorType, actors.Length);
            for (int i = 0; i < actors.Length; i++) {
                var item = Activator.CreateInstance(actorType);
                Set(item, "position", actors[i].transform.position);
                Set(item, "rotation", actors[i].transform.rotation);
                states.SetValue(item, i);
            }
            Set(snapshot, "actors", states);
            Set(snapshot, "victims", Array.CreateInstance(type.GetNestedType("VictimState", BindingFlags.NonPublic), 0));
            Call(run, "DebugSetRemaining", 60f);
            type.GetMethod("ApplySnapshot", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, new[] { snapshot });
            yield return null; yield return null;
            Assert.IsNull(player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose")));
            Assert.AreEqual(0, ((IDictionary)PursuitPrivate(director, "victims")).Count);
            Assert.AreEqual(-1f, Get(director, "Elapsed"));
            Assert.IsNull(Get(actors[0], "Victim"));
            Assert.IsTrue((bool)Get(director, "DebugInvulnerable"), "The client panel must show the host's protection status.");
            Assert.IsFalse((bool)Get(director, "DebugRecognitionEnabled"));
        }

        [UnityTest]
        public IEnumerator PursuitCheats_ProtectionRecognitionAndPatrolResume()
        {
            yield return PursuitHost();
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var poseType = T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose");
            Assert.IsTrue((bool)Call(director, "DebugSetInvulnerable", true));
            Assert.IsTrue((bool)Call(director, "DebugExpireTimer"));
            Call(director, "BeginCapture", actor, player.gameObject);
            Assert.IsNull(Get(actor, "Victim")); Assert.IsNull(player.GetComponent(poseType));
            Assert.IsTrue((bool)Call(director, "IsAvailable", player.gameObject), "Protected players must still count as living and remain chase targets.");

            Assert.IsTrue((bool)Call(director, "DebugSetInvulnerable", false));
            Call(director, "BeginCapture", actor, player.gameObject);
            Assert.AreSame(player.gameObject, Get(actor, "Victim"), "Turning protection off restores capture.");
            Assert.IsTrue((bool)Call(director, "DebugSetInvulnerable", true));
            Assert.IsTrue((bool)Call(director, "DebugSetRecognition", false));
            Assert.AreSame(player.gameObject, Get(actor, "Victim"), "These toggles do not rescue existing victims.");
            Assert.IsTrue((bool)Call(director, "DebugResetTimer"));
            yield return null; yield return null;
            Assert.IsNull(player.GetComponent(poseType));
            Assert.IsTrue((bool)Get(director, "DebugInvulnerable"));
            Assert.IsFalse((bool)Get(director, "DebugRecognitionEnabled"), "Timer edits must preserve test options.");
            Assert.IsTrue((bool)Call(director, "DebugSetStage", 3));
            float until = Time.time + 8;
            while ((float)Get(director, "Elapsed") < 3 && Time.time < until) yield return null;
            ((Behaviour)director).enabled = false;
            var stage = Get(director, "CurrentStageRules");
            string saved = JsonUtility.ToJson(stage);
            var candidates = (IList)PursuitPrivate(director, "candidates"); candidates.Clear();
            var dummy = new GameObject("RecognitionCheatTarget");
            bool Cheat(string method, bool value) {
                ((Behaviour)director).enabled = true;
                try { return (bool)Call(director, method, value); }
                finally { ((Behaviour)director).enabled = false; }
            }
            try {
                Assert.IsTrue((bool)Field(stage, "AlwaysNearest"), "Exercise the final stage that ignores normal sight restrictions.");
                Set(stage, "PatrolLeashDistance", 0f); Set(stage, "AlertSeconds", 0f); Set(stage, "ChaseSpeed", .1f);
                Set(stage, "WalkHearingRadius", 6f); Set(stage, "InvestigateFraction", .8f); Set(stage, "InvestigateSeconds", 3f);
                Call(actor, "ResetActor"); yield return TickPursuit(actor, "Patrol");
                dummy.transform.position = actor.transform.position + actor.transform.forward * 3;
                candidates.Add(dummy);
                Assert.IsNull(Call(director, "NearestVisible", actor, stage));
                Call(director, "BeginCapture", actor, dummy);
                Assert.IsNull(Get(actor, "Victim"));
                Assert.IsTrue(Cheat("DebugSetRecognition", true));
                yield return TickPursuit(actor, "Chase");
                Assert.AreSame(dummy, Get(actor, "ChasedPlayer"), "Invulnerability must not hide the target.");
                Vector3 position = actor.transform.position;
                int waypoint = (int)PursuitPrivate(actor, "waypoint");
                Assert.IsTrue(Cheat("DebugSetRecognition", false));
                Assert.AreEqual(position, actor.transform.position, "Disabling recognition must not warp the mannequin.");
                Assert.AreEqual(waypoint, PursuitPrivate(actor, "waypoint"), "Keep patrol progress.");
                Assert.IsNull(Get(actor, "ChasedPlayer")); Assert.IsFalse((bool)Get(actor, "HasLook"));
                for (int i = 0; i < 3; i++) {
                    Call(director, "EmitFootstep", actor.transform.position + Vector3.forward, true, null);
                    Call(actor, "Hear", actor.transform.position + Vector3.forward, .1f);
                    yield return TickPursuit(actor, "Patrol");
                    Assert.IsNull(Get(actor, "ChasedPlayer")); Assert.IsFalse((bool)Get(actor, "HasLook"));
                }
                candidates.Clear();
                Assert.IsTrue(Cheat("DebugSetRecognition", true));
                Call(director, "EmitFootstep", actor.transform.position + actor.transform.forward * 2, false, null);
                yield return TickPursuit(actor, "Investigate");
                Assert.IsTrue(Cheat("DebugSetRecognition", false));
                yield return TickPursuit(actor, "Patrol");
                Assert.IsFalse((bool)Get(actor, "HasLook"), "Disabling recognition also clears a remembered sound.");
                Assert.IsTrue(Cheat("DebugSetInvulnerable", false));
                Call(director, "BeginCapture", actor, player.gameObject);
                Assert.IsNull(Get(actor, "Victim"), "Recognition OFF alone must block capture too.");
                Assert.IsTrue(Cheat("DebugSetInvulnerable", true));
                DevCache.SetValue(null, false);
                ((Behaviour)director).enabled = true;
                yield return null; yield return null;
                Assert.IsFalse((bool)Get(director, "DebugInvulnerable"));
                Assert.IsTrue((bool)Get(director, "DebugRecognitionEnabled"), "Leaving Dev mode must restore normal rules.");
            } finally {
                JsonUtility.FromJsonOverwrite(saved, stage); UnityEngine.Object.Destroy(dummy);
                DevCache.SetValue(null, true); ((Behaviour)director).enabled = true;
            }
        }
    }
}
