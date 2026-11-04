using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator PursuitEyes_LocalTargetTransitionsRangeAndCleanup()
        {
            yield return SceneManager.LoadSceneAsync("Start");
            yield return IsolatedMenuHost();
            Assert.IsTrue((bool)Call(launcher, "TryLaunch", Point(2, "Morning", 0, "Tightrope")));
            float until = Time.realtimeSinceStartup + 75;
            while ((bool)Get(launcher, "IsBusy") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Get(launcher, "LastLaunchSucceeded"));
            yield return MenuReady();
            var director = (Component)Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var eye = (Behaviour)director.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitEyeFeedback"));
            Assert.IsNotNull(eye, "Director must attach local eye feedback in the actual waiting room.");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var actors = ((IEnumerable)Get(director, "Actors")).Cast<Component>().ToArray();
            Assert.IsNotEmpty(actors);
            var actor = actors[0];
            var room = Get(actor, "Room");
            bool roam = (bool)Field(room, "RoamAcrossRooms");
            var stage = Get(director, "CurrentStageRules");
            float leash = (float)Field(stage, "PatrolLeashDistance");
            var other = new GameObject("Other observer");
            var motion = T("CurtainCall.WaitingRoom.Pursuit.PursuitMotion");
            var behaviour = T("CurtainCall.WaitingRoom.Pursuit.PursuitBehaviour");
            var p = player.transform.position + Vector3.right * 2;
            void Sample(string state, string move, GameObject target, Vector3 position) {
                actor.transform.position = position;
                Call(actor, "Receive", position, Quaternion.identity, Enum.Parse(motion, move), null, 0f, player.transform.position, true, target);
                Call(actor, "ReceiveBehaviour", Enum.Parse(behaviour, state));
            }
            SpriteRenderer Sprite() => director.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r => r.name == "Local eye - " + actor.name);
            var props = new MaterialPropertyBlock();
            var rules = Field(Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit"), "Eyes");
            void CheckColor(string name) => Assert.Less(((Vector4)(Color)Field(rules,name) - (Vector4)props.GetColor("_Tint")).magnitude, .0001f,
                "MaterialPropertyBlock colors allow floating-point serialization roundoff.");
            try {
                room.GetType().GetField("RoamAcrossRooms").SetValue(room, true);
                stage.GetType().GetField("PatrolLeashDistance").SetValue(stage, 0f);
                foreach (var a in actors) a.transform.position = p + Vector3.right * 100;
                ((Behaviour)director).enabled = false;
                Sample("Patrol", "Walk", null, p);
                yield return new WaitForSecondsRealtime(.6f);
                Assert.AreEqual(0, Get(eye, "VisibleCount"), "No eyes before activation.");
                ((Behaviour)director).enabled = true;
                Assert.IsTrue((bool)Call(director, "DebugExpireTimer"));
                ((Behaviour)director).enabled = false;
                Sample("Patrol", "Walk", null, p);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.AreEqual(1, Get(eye, "VisibleCount"));
                Assert.AreEqual("Eye00", Sprite().sprite.name);
                Sample("Prepare", "Awaken", player.gameObject, p);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.AreEqual("Eye07", Sprite().sprite.name);
                Sprite().GetPropertyBlock(props);
                CheckColor("AlertColor");
                Sample("Chase", "Run", player.gameObject, p);
                yield return new WaitForSecondsRealtime(.65f);
                Sprite().GetPropertyBlock(props);
                CheckColor("ChaseColor");
                Assert.Greater(props.GetFloat("_Emission"), 0);
                Sample("Search", "Walk", null, p);
                yield return new WaitForSecondsRealtime(.65f);
                Sprite().GetPropertyBlock(props);
                CheckColor("AlertColor");
                Sample("Chase", "Run", other, p);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.AreEqual("Eye00", Sprite().sprite.name, "A teammate's chase is not our detection.");
                Sample("Patrol", "Walk", null, p + Vector3.right * 100);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.AreEqual(0, Get(eye, "VisibleCount"));
                Sample("Patrol", "Walk", null, p);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.AreEqual(1, Get(eye, "VisibleCount"));
                eye.enabled = false; yield return null;
                Assert.IsNull(Sprite(), "Local views must be destroyed when disabled.");
            } finally {
                room.GetType().GetField("RoamAcrossRooms").SetValue(room, roam);
                stage.GetType().GetField("PatrolLeashDistance").SetValue(stage, leash);
                UnityEngine.Object.Destroy(other);
            }
        }
    }
}
