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
            Assert.AreEqual(0, Get(feedback, "HighlightCount"), "Silhouette outlines were removed; direction cues replace them.");
            Assert.IsFalse((bool)Get(feedback, "IsChased"));
            var shells = actor.GetComponentsInChildren<Renderer>().Where(r => r.name == "__InteractionOutline").ToArray();
            Assert.IsEmpty(shells);
            var appearance = actor.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitAppearance"));
            Assert.IsNotNull(appearance);
            var behaviour = T("CurtainCall.WaitingRoom.Pursuit.PursuitBehaviour");
            var rule = Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit");
            var stages = ((IEnumerable)Field(rule, "Stages")).Cast<object>().ToArray();
            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++) {
                ((Behaviour)director).enabled = true; Assert.IsTrue((bool)Call(director, "DebugSetStage", stageIndex)); ((Behaviour)director).enabled = false;
                yield return new WaitForSecondsRealtime(.16f);
                float fov = (float)Field(stages[stageIndex], "FieldOfView");
                Assert.AreEqual(fov, Get(appearance, "CueAngle"), "The cue must follow stage changes.");
                var floorMesh = actor.transform.Find("Pursuit facing light").GetComponent<MeshFilter>().sharedMesh;
                var uv = floorMesh.uv; var vertices = floorMesh.vertices;
                foreach (float side in new[] { 0f, 1f }) {
                    int edge = Array.FindIndex(uv, v => Mathf.Approximately(v.x, side) && v.y > 0);
                    Vector3 ray = Vector3.ProjectOnPlane(vertices[edge], Vector3.up);
                    Assert.AreEqual((side - .5f) * fov, Vector3.SignedAngle(Vector3.forward, ray, Vector3.up), .1f, "Rendered left/right boundaries must match the sight angle.");
                }
                float length = (float)Field(rule, "DirectionCueLength");
                Assert.LessOrEqual(vertices.Max(v => Vector3.ProjectOnPlane(actor.transform.TransformPoint(v) - actor.transform.position, Vector3.up).magnitude), length + .01f, "Cue length must remain independent of sight distance.");
            }
            foreach (string name in new[]{"Patrol","Listen","Prepare","Chase"}) {
                Call(actor,"ReceiveBehaviour",Enum.Parse(behaviour,name));
                yield return new WaitForSecondsRealtime(.16f);
                Assert.IsTrue((bool)Get(appearance,"CueVisible"),name+" should show a floor direction cue.");
                Color color=(Color)Get(appearance,"CueColor");
                string colorField = name == "Patrol" ? "PatrolCueColor" : name == "Listen" ? "AlertCueColor" : "ChaseCueColor";
                Assert.AreEqual((Color)Field(rule, colorField), color, "The chosen state must use its designer-configured color.");
            }
            ((Behaviour)director).enabled=true;Assert.IsTrue((bool)Call(director,"DebugSetStage",2));((Behaviour)director).enabled=false;
            Call(appearance,"SetCaptureCount",2);
            yield return null;yield return null;
            Assert.IsTrue((bool)Get(appearance,"IsAdvanced"));Assert.AreEqual(2,Get(appearance,"CaptureCount"));
            Assert.IsTrue(((GameObject[])Field(appearance,"AdvancedDetails")).All(go=>go.activeSelf));
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
                Assert.AreEqual(0, Get(feedback, "HighlightCount"), "Silhouette outlines were removed; direction cues replace them.");
                ((Behaviour)director).enabled = true;
                Assert.IsTrue((bool)Call(director, "DebugResetTimer"));
                ((Behaviour)director).enabled = false;
                Call(actor, "Receive", p, Quaternion.identity, Enum.Parse(motion, "Display"), null, 0f, Vector3.zero, false, null);
                yield return null; yield return null;
                Assert.AreEqual(0, Get(feedback, "HighlightCount"), "Cancelling pursuit must remove the outline even while still nearby.");
                Assert.IsFalse((bool)Get(appearance,"CueVisible"));
                Assert.IsFalse((bool)Get(appearance,"IsAdvanced"));Assert.AreEqual(0,Get(appearance,"CaptureCount"));
                Assert.IsFalse(actor.GetComponentsInChildren<Renderer>().Any(r => r.name == "__InteractionOutline"));
                ((Behaviour)feedback).enabled = false; yield return null;
                Assert.IsNull(((Component)feedback).transform.Find("PursuitThreatEdges"));
                Assert.IsFalse(actor.GetComponentsInChildren<Renderer>().Any(r => r.name == "__InteractionOutline"));
            } finally { UnityEngine.Object.Destroy(other); }
        }
    }
}
