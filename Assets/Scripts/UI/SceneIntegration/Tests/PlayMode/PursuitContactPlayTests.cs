using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator WaitingRoom_RunAndJumpThroughActualKeyboardInput()
        {
            yield return PursuitHost();
            var settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            var previousKeyboard = Keyboard.current;
            Keyboard keyboard = null;
            try {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>("Waiting room input test"); keyboard.MakeCurrent();
                var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (int i = 0; i < 10; i++) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                for (int i = 0; i < 10; i++) yield return null;
                Assert.IsTrue((bool)Get(mover, "IsSprinting"), "Run input should reach the actual waiting-room player.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift, Key.Space));
                yield return null; yield return null;
                Assert.AreEqual("InPlace", Get(mover, "CurrentJump").ToString(), "Jump while running in the actual waiting room.");
            } finally {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }
        static object ContactCall(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static void ContactField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void ContactProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        object ContactReport(Component director, Component actor, Component player, double stamp, uint sequence = 1)
        {
            var report = Activator.CreateInstance(T("CurtainCall.WaitingRoom.Pursuit.PursuitContact"));
            foreach (var pair in new[] { ("Generation", "generation"), ("Day", "day"), ("Performance", "performance"), ("Epoch", "poseEpoch") })
                Set(report, pair.Item1, PursuitPrivate(director, pair.Item2));
            Set(report, "Actor", 0); Set(report, "Sequence", sequence); Set(report, "PoseTime", stamp);
            Set(report, "ActorPosition", actor.transform.position); Set(report, "PlayerPosition", player.transform.position);
            return report;
        }
        void ContactChase(Component director, Component actor, Component player)
        {
            ((Behaviour)director).enabled = true;
            Assert.IsTrue((bool)Call(director, "DebugSetStage", 3));
            ((Behaviour)director).enabled = false;
            Call(actor, "ResetActor");
            actor.transform.position = player.transform.position + Vector3.right * .3f;
            ContactField(actor, "target", player.transform);
            ContactField(actor, "visible", true);
            ContactProperty(actor, "Behaviour", Enum.Parse(T("CurtainCall.WaitingRoom.Pursuit.PursuitBehaviour"), "Chase"));
            Call(actor, "SetMotion", Enum.Parse(T("CurtainCall.WaitingRoom.Pursuit.PursuitMotion"), "Run"), true);
        }

        [UnityTest]
        public IEnumerator PursuitContact_HostOldPositionCannotCaptureRemoteOwnerWithoutContactReport()
        {
            yield return PursuitHost();
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var remoteObject = Object.Instantiate(Resources.Load<GameObject>("Prefabs/Characters/Players/Player"));
            var networkObject = remoteObject.GetComponent(T("Unity.Netcode.NetworkObject"));
            try {
                // A spawned object with a distinct owner exercises the real remote-player gate;
                // it is not a second machine or a Relay latency measurement.
                Call(networkObject, "SpawnWithOwnership", 777UL, true);
                var remote = remoteObject.GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                ContactChase(director, actor, remote);
                Assert.IsTrue((bool)Call(director, "RequiresOwnerContact", remoteObject));
                for (int i = 0; i < 10; i++) Call(director, "BeginCapture", actor, remoteObject);
                Assert.IsNull(Get(actor, "Victim"));
                Assert.AreEqual(0, Get(director, "AcceptedContacts"));
                Assert.IsNull(remoteObject.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose")));
            } finally {
                if ((bool)Get(networkObject, "IsSpawned")) Call(networkObject, "Despawn", true);
                else Object.DestroyImmediate(remoteObject);
                ((Behaviour)director).enabled = true;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PursuitContact_RejectsOwnerEpochStaleAvoidanceAndWallBeforeAnyPresentation()
        {
            yield return PursuitHost();
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            ContactChase(director, actor, player);
            double now = Time.realtimeSinceStartupAsDouble;
            ContactCall(director, "RecordContactHistory", now);
            object Report() => ContactReport(director, actor, player, now);
            var owner = (ulong)Get(player, "OwnerClientId");
            Assert.IsFalse((bool)ContactCall(director, "TryCommitContact", owner + 999, Report(), now + .2));
            Assert.AreEqual("Owner", Get(director, "LastContactRejection").ToString());
            var wrongEpoch = Report(); Set(wrongEpoch, "Epoch", (int)Field(wrongEpoch, "Epoch") - 1);
            Assert.IsFalse((bool)ContactCall(director, "TryCommitContact", owner, wrongEpoch, now + .2));
            Assert.AreEqual("Epoch", Get(director, "LastContactRejection").ToString());
            Assert.IsFalse((bool)ContactCall(director, "TryCommitContact", owner, Report(), now + 2));
            Assert.AreEqual("Expired", Get(director, "LastContactRejection").ToString());
            var avoided = Report(); Set(avoided, "PlayerPosition", actor.transform.position + Vector3.right * 3);
            Assert.IsFalse((bool)ContactCall(director, "TryCommitContact", owner, avoided, now + .2));
            Assert.AreEqual("NotTouching", Get(director, "LastContactRejection").ToString());
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try {
                wall.transform.position = (actor.transform.position + player.transform.position) * .5f + Vector3.up;
                wall.transform.localScale = new Vector3(.05f, 2, 2); Physics.SyncTransforms();
                Assert.IsFalse((bool)ContactCall(director, "TryCommitContact", owner, Report(), now + .2));
                Assert.AreEqual("Occluded", Get(director, "LastContactRejection").ToString());
            } finally { Object.DestroyImmediate(wall); }
            Assert.AreEqual(0, Get(director, "AcceptedContacts"));
            Assert.IsNull(Get(actor, "Victim"));
            Assert.IsNull(player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose")));
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            Assert.IsTrue((bool)Call(run, "DebugResetRestTimer"));
            Assert.IsFalse((bool)ContactCall(director, "TryCommitContact", owner, Report(), now + .2));
            Assert.AreEqual("State", Get(director, "LastContactRejection").ToString(), "A cached active director cannot accept a report after the authoritative timer resets.");
            ((Behaviour)director).enabled = true;
        }

        [UnityTest]
        public IEnumerator PursuitContact_DelayedConfirmationKeepsCutRetireLightingSnapshotAndReset()
        {
            yield return PursuitHost();
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            ContactChase(director, actor, player);
            double stamp = Time.realtimeSinceStartupAsDouble - .3;
            ContactCall(director, "RecordContactHistory", stamp);
            var report = ContactReport(director, actor, player, stamp);
            var position = player.transform.position + Vector3.right * .6f;
            Set(report, "PlayerPosition", position);
            var visual = (Component)Get(player.GetComponent(T("CurtainCall.Player.HumanoidPlayerPresentation")), "Model");
            var expectedAnchor = position + visual.transform.position - player.transform.position;
            actor.transform.position += Vector3.right * 3; // Already elsewhere on the host by receipt.
            var owner = (ulong)Get(player, "OwnerClientId");
            Assert.IsTrue((bool)ContactCall(director, "TryCommitContact", owner, report, stamp + .6), Get(director, "LastContactRejection").ToString());
            Assert.AreEqual(player.gameObject, Get(actor, "Victim"));
            Assert.AreEqual("Grab", Get(actor, "State").ToString());
            Assert.IsFalse((bool)ContactCall(director, "TryCommitContact", owner, report, stamp + .7));
            Assert.AreEqual(1, Get(director, "AcceptedContacts"), "Reliable retry/duplicate must not restart capture or cut twice.");
            var snapshot = ContactCall(director, "BuildSnapshot");
            var victims = (Array)Field(snapshot, "victims"); Assert.AreEqual(1, victims.Length);
            var victim = victims.GetValue(0);
            Assert.Less(Vector3.Distance(expectedAnchor, (Vector3)Field(victim, "anchor")), .001f, "Use the contact point, not the host's stale victim position.");
            var rooms = (Array)Field(director, "Rooms");
            Assert.IsTrue((bool)Get(rooms.GetValue((int)Field(victim, "room")), "CaptureFlickering"));
            Assert.AreEqual(5, Field(((Array)Field(snapshot, "actors")).GetValue(0), "motion"));
            yield return null;
            var pose = player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose")); Assert.IsNotNull(pose);
            var rules = Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit");
            ContactProperty(actor, "CaptureElapsed", (float)Field(rules, "KnockdownSeconds") + 3 * (float)Field(rules, "CutInterval") + .1f);
            Call(director, "AdvanceCapture", actor);
            Assert.AreEqual(13, Convert.ToInt32(Get(player, "LostParts")));
            Assert.AreEqual("Retired", Get(player, "State").ToString());
            Assert.IsTrue((bool)Field(pose, "Hidden"));
            snapshot = ContactCall(director, "BuildSnapshot");
            Assert.IsTrue((bool)Field(((Array)Field(snapshot, "victims")).GetValue(0), "retired"));
            Assert.AreEqual(1, Field(((Array)Field(snapshot, "actors")).GetValue(0), "captures"));
            ((Behaviour)director).enabled = true;
            Assert.IsTrue((bool)Call(director, "DebugResetTimer"));
            Assert.IsTrue((bool)Call(director, "DebugExpireTimer"));
            ((Behaviour)director).enabled = false;
            Assert.IsFalse((bool)ContactCall(director, "TryCommitContact", owner, report, stamp + .8));
            Assert.AreEqual("Epoch", Get(director, "LastContactRejection").ToString());
            yield return null; yield return null;
            Assert.IsNull(player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose")));
            ((Behaviour)director).enabled = true;
        }
    }
}
