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
        public IEnumerator PursuitFeedback_ProximityLocalTargetAndCleanup()
        {
            yield return SceneManager.LoadSceneAsync("Start");
            yield return IsolatedMenuHost();
            Assert.IsTrue((bool)Call(launcher, "TryLaunch", Point(2, "Morning", 0, "Tightrope")));
            float until = Time.realtimeSinceStartup + 75;
            while ((bool)Get(launcher, "IsBusy") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Get(launcher, "LastLaunchSucceeded"));
            yield return MenuReady();
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            ((Behaviour)director).enabled = false; // deterministic replica samples, no host AI capture while inspecting
            var feedback = ((Component)director).GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitFeedback"));
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var actors = ((IEnumerable)Get(director, "Actors")).Cast<Component>().ToArray();
            var rooms = ((IEnumerable)Field(director, "Rooms")).Cast<Component>();
            int expected = rooms.Sum(room => ((IEnumerable)Field(room, "Placements")).Cast<Component>().Count(p => p != null && ((Behaviour)p).enabled && p.gameObject.activeInHierarchy));
            Assert.AreEqual(expected, actors.Length, "Every enabled placement must spawn regardless of Awake/OnEnable order.");
            Assert.Greater(expected, 0, "The current designer layout must have placements.");
            Assert.IsTrue(actors.All(a => Get(a, "Placement") != null));
            foreach (var a in actors) a.transform.position = player.transform.position + Vector3.right * 100;
            var actor = actors[0];
            var p = player.transform.position + Vector3.right * 2;
            var motion = T("CurtainCall.WaitingRoom.Pursuit.PursuitMotion");
            Call(actor, "Receive", p, Quaternion.identity, Enum.Parse(motion, "Display"), null, 0f, Vector3.zero, false, null);
            yield return null; yield return null;
            Assert.Less((float)Get(director, "Elapsed"), 0);
            Assert.AreEqual(0, Get(feedback, "HighlightCount"), "A nearby decorative mannequin must not reveal itself before pursuit.");
            Assert.IsFalse(actor.GetComponentsInChildren<Renderer>().Any(r => r.name == "__InteractionOutline"));
            ((Behaviour)director).enabled = true;
            Assert.IsTrue((bool)Call(director, "DebugExpireTimer"));
            ((Behaviour)director).enabled = false;
            yield return null; yield return null;
            Assert.AreEqual(1, Get(feedback, "HighlightCount"));
            Assert.IsFalse((bool)Get(feedback, "IsChased"));
            var shells = actor.GetComponentsInChildren<Renderer>().Where(r => r.name == "__InteractionOutline").ToArray();
            Assert.IsTrue(shells.Any(r => r.enabled));
            Assert.Greater(shells[0].sharedMaterial.GetColor("_OutlineColor").r, shells[0].sharedMaterial.GetColor("_OutlineColor").g * 5);
            var other = new GameObject("OtherPlayerFixture");
            try {
                Call(actor, "Receive", p, Quaternion.identity, Enum.Parse(motion, "Run"), null, 0f, player.transform.position, true, other);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.IsFalse((bool)Get(feedback, "IsChased"), "Another player's chase must not trigger our border.");
                Call(actor, "Receive", p, Quaternion.identity, Enum.Parse(motion, "Run"), null, 0f, player.transform.position, true, player.gameObject);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.IsTrue((bool)Get(feedback, "IsChased")); Assert.Greater((float)Get(feedback, "Intensity"), .1f);
                var edge = ((Component)feedback).GetComponentInChildren(T("CurtainCall.WaitingRoom.Pursuit.PursuitEdgeGraphic")) as UnityEngine.UI.Graphic;
                Assert.IsNotNull(edge); Assert.IsFalse(edge.raycastTarget);
                Call(actor, "Receive", p + Vector3.right * 100, Quaternion.identity, Enum.Parse(motion, "Walk"), null, 0f, Vector3.zero, false, null);
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.AreEqual(0, Get(feedback, "HighlightCount")); Assert.Less((float)Get(feedback, "Intensity"), .01f);
                Call(actor, "Receive", p, Quaternion.identity, Enum.Parse(motion, "Display"), null, 0f, Vector3.zero, false, null);
                yield return null; yield return null;
                Assert.AreEqual(1, Get(feedback, "HighlightCount"));
                ((Behaviour)director).enabled = true;
                Assert.IsTrue((bool)Call(director, "DebugResetTimer"));
                ((Behaviour)director).enabled = false;
                Call(actor, "Receive", p, Quaternion.identity, Enum.Parse(motion, "Display"), null, 0f, Vector3.zero, false, null);
                yield return null; yield return null;
                Assert.AreEqual(0, Get(feedback, "HighlightCount"), "Cancelling pursuit must remove the outline even while still nearby.");
                Assert.IsFalse(actor.GetComponentsInChildren<Renderer>().Any(r => r.name == "__InteractionOutline"));
                ((Behaviour)feedback).enabled = false; yield return null;
                Assert.IsNull(((Component)feedback).transform.Find("PursuitThreatEdges"));
                Assert.IsFalse(actor.GetComponentsInChildren<Renderer>().Any(r => r.name == "__InteractionOutline"));
            } finally { UnityEngine.Object.Destroy(other); }
        }
    }
}
