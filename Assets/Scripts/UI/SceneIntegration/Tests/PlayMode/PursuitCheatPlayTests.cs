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
            DevCache.SetValue(null, true);
            var hostField = run.GetType().GetField("host", BindingFlags.NonPublic | BindingFlags.Instance);
            var savedHost = hostField.GetValue(run);
            try {
                hostField.SetValue(run, null);
                Assert.IsFalse((bool)Get(director, "CanDebug"));
                Assert.IsFalse((bool)Call(director, "DebugResetTimer"));
                Assert.IsFalse((bool)Call(director, "DebugSetStage", 0));
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
            var snapshotType = type.GetNestedType("Snapshot", BindingFlags.Public | BindingFlags.NonPublic);
            var snapshot = Activator.CreateInstance(snapshotType);
            Set(snapshot, "generation", Field(state, "Generation"));
            Set(snapshot, "day", Field(state, "Day"));
            Set(snapshot, "performance", Field(state, "PerformanceIndex"));
            Set(snapshot, "elapsed", -1f); Set(snapshot, "stage", -1);
            var actorType = type.GetNestedType("ActorState", BindingFlags.Public | BindingFlags.NonPublic);
            var states = Array.CreateInstance(actorType, actors.Length);
            for (int i = 0; i < actors.Length; i++) {
                var item = Activator.CreateInstance(actorType);
                Set(item, "position", actors[i].transform.position);
                Set(item, "rotation", actors[i].transform.rotation);
                states.SetValue(item, i);
            }
            Set(snapshot, "actors", states);
            Set(snapshot, "victims", Array.CreateInstance(type.GetNestedType("VictimState", BindingFlags.Public | BindingFlags.NonPublic), 0));
            Call(run, "DebugSetRemaining", 60f);
            type.GetMethod("ApplySnapshot", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, new[] { snapshot });
            yield return null; yield return null;
            Assert.IsNull(player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose")));
            Assert.AreEqual(0, ((IDictionary)PursuitPrivate(director, "victims")).Count);
            Assert.AreEqual(-1f, Get(director, "Elapsed"));
            Assert.IsNull(Get(actors[0], "Victim"));
        }
    }
}
