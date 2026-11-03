using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator PursuitInitialization_SplitsStableOrderAndCancelsWithSceneObject()
        {
            yield return PursuitHost();
            var source = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            Assert.IsTrue((bool)Get(source, "IsInitialized"), "The transition curtain must wait for every actor.");
            var first = ((IEnumerable)Get(source, "Actors")).Cast<Component>().First();
            var prefab = (GameObject)Field(Get(first, "Placement"), "Prefab");
            var root = new GameObject("PursuitInitializationFixture"); root.SetActive(false);
            try {
                var room = root.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitRoom"));
                Set(room, "UsePlacements", false); Set(room, "Count", 9);
                var points = Enumerable.Range(0, 9).Select(i => {
                    var point = new GameObject("Point" + i).transform; point.SetParent(root.transform);
                    point.position = new Vector3(300 + i, 50, 300); return point;
                }).ToArray();
                Set(room, "SpawnPoints", points);
                var director = (Component)root.AddComponent(source.GetType());
                var rooms = System.Array.CreateInstance(room.GetType(), 1); rooms.SetValue(room, 0);
                Set(director, "Rooms", rooms); Set(director, "Mannequins", new[] { prefab });
                Set(director, "BehaviourGraph", Field(source, "BehaviourGraph"));
                root.SetActive(true);
                var actors = (IList)PursuitPrivate(director, "actors");
                Assert.AreEqual(0, actors.Count, "Awake only queues work.");
                var emptySnapshot = System.Activator.CreateInstance(director.GetType().GetNestedType("Snapshot"));
                director.GetType().GetMethod("ApplySnapshot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(director, new[] { emptySnapshot });
                Assert.IsFalse((bool)Get(director, "IsInitialized"), "An early snapshot cannot complete or reset initialization.");
                int before = 0, frames = 0;
                while (!(bool)Get(director, "IsInitialized") && frames++ < 12) {
                    Assert.IsFalse((bool)Get(director, "CanDebug"));
                    Assert.IsNull(PursuitPrivate(director, "messaging"), "No ready handshake or partial snapshot before all actors exist.");
                    yield return null;
                    Assert.That(actors.Count - before, Is.InRange(0, 4), "Each frame has a bounded batch of prefabs/graphs.");
                    before = actors.Count;
                }
                Assert.IsTrue((bool)Get(director, "IsInitialized"));
                ((Behaviour)director).enabled = false; // A fixture must never replace the live server's message handlers.
                Assert.AreEqual(9, actors.Count); Assert.That((int)Get(director, "InitializationFrames"), Is.InRange(3, 9));
                for (int i = 0; i < 9; i++) {
                    var actor = (Component)actors[i];
                    Assert.AreEqual(points[i].position, actor.transform.position);
                    Assert.IsTrue((bool)Get(actor, "GraphReady"));
                    Assert.IsNull(Get(actor, "Victim"));
                }
                // Start a second initialization and destroy it before its queue completes.
                var second = new GameObject("CancelledPursuitInitialization"); second.SetActive(false);
                second.transform.SetParent(root.transform);
                var secondDirector = second.AddComponent(source.GetType());
                Set(secondDirector, "Rooms", rooms); Set(secondDirector, "Mannequins", new[] { prefab });
                Set(secondDirector, "BehaviourGraph", Field(source, "BehaviourGraph"));
                second.SetActive(true);
                var partial = (IList)PursuitPrivate(secondDirector, "actors");
                for (int wait = 0; partial.Count == 0 && wait < 4; wait++) yield return null;
                Assert.That(partial.Count, Is.InRange(1, 4));
                var spawned = partial.Cast<Component>().ToArray();
                Object.Destroy(second); yield return null; yield return null;
                Assert.IsTrue(second == null && secondDirector == null);
                Assert.IsTrue(spawned.All(actor => actor == null), "No delayed spawn survives scene-object destruction.");
            } finally {
                Object.DestroyImmediate(root);
                source.GetType().GetProperty("Current").SetValue(null, source);
            }
        }

        [UnityTest]
        public IEnumerator PursuitActivation_StaggeredStagesDormantSensingAndReplicaAppearance()
        {
            yield return PursuitHost();
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var actors = ((IEnumerable)Get(director, "Actors")).Cast<Component>().Take(4).ToArray();
            Assert.AreEqual(4, actors.Length);
            var placements = actors.Select(a => Get(a, "Placement")).ToArray();
            var previous = placements.Select(p => Field(p, "ActivationStage")).ToArray();
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var rules = Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit");
            var replicaObject = Object.Instantiate((GameObject)Field(placements[3], "Prefab"));
            var replica = replicaObject.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitActor"))
                ?? replicaObject.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitActor"));
            Call(replica, "Initialize", director, Get(actors[3], "Room"), placements[3]);
            bool Cheat(string method, params object[] arguments) {
                ((Behaviour)director).enabled = true;
                try { return (bool)Call(director, method, arguments); }
                finally { ((Behaviour)director).enabled = false; }
            }
            void Tick() { foreach (var a in actors) Call(a, "ServerTick", (float)Get(director, "Elapsed"), false); }
            try {
                Assert.IsTrue(Cheat("DebugSetInvulnerable", true));
                for (int i = 0; i < 4; i++) Set(placements[i], "ActivationStage", i + 1);
                ((IList)PursuitPrivate(director, "candidates")).Clear();
                foreach (var a in actors) Call(a, "ResetActor");
                for (int stage = 0; stage < 4; stage++) {
                    Assert.IsTrue(Cheat("DebugSetStage", stage));
                    var before = actors.Select(a => a.transform.position).ToArray();
                    Tick();
                    Assert.AreEqual("Awaken", Get(actors[stage], "State").ToString(), "Each new group gets its own awakening, even late in the pursuit.");
                    for (int i = 0; i < 4; i++) {
                        Assert.AreEqual(i <= stage, Get(actors[i], "IsActivated"), $"Start {i + 1}, current {stage + 1}");
                        if (i <= stage) continue;
                        Call(actors[i], "Hear", actors[i].transform.position + Vector3.forward, .1f);
                        Assert.IsFalse((bool)Get(actors[i], "HasLook"));
                        Assert.IsNull(Call(director, "NearestVisible", actors[i], Get(director, "CurrentStageRules")));
                        Assert.IsTrue(Cheat("DebugSetInvulnerable", false));
                        Call(director, "BeginCapture", actors[i], player.gameObject);
                        Assert.IsNull(Get(actors[i], "Victim"), "An inactive mannequin cannot capture, even without invulnerability.");
                        Assert.IsTrue(Cheat("DebugSetInvulnerable", true));
                        Assert.AreEqual("Display", Get(actors[i], "State").ToString());
                        Assert.AreEqual(before[i], actors[i].transform.position);
                    }
                    Call(replica, "Receive", actors[3].transform.position, actors[3].transform.rotation, Get(actors[3], "State"), null, 0f, Vector3.zero, false, null);
                    yield return null; yield return null;
                    Assert.AreEqual(stage == 3, Get(replica, "IsActivated"));
                    foreach (var a in actors.Concat(new[] { replica })) {
                        var appearance = a.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitAppearance"));
                        bool active = (bool)Get(a, "IsActivated");
                        Assert.AreEqual(active && stage >= 2, Get(appearance, "IsAdvanced"));
                        if (!active) Assert.IsFalse((bool)Get(appearance, "CueVisible"), "Dormant mannequins must not reveal themselves with a direction cue.");
                    }
                    yield return new WaitForSeconds((float)Field(rules, "AwakeningSeconds") + .1f);
                    Tick();
                    for (int i = 0; i <= stage; i++)
                        Assert.IsTrue(actors[i].GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "The activated group must start navigation after awakening.");
                }
                Assert.IsTrue(Cheat("DebugSetStage", 0));
                var positions = actors.Select(a => a.transform.position).ToArray(); Tick();
                for (int i = 1; i < 4; i++) {
                    Assert.IsFalse((bool)Get(actors[i], "IsActivated"));
                    Assert.IsFalse(actors[i].GetComponent<UnityEngine.AI.NavMeshAgent>().enabled);
                    Assert.AreEqual(positions[i], actors[i].transform.position, "Lowering the debug stage must stop in place, without warping.");
                }
                Assert.IsTrue(Cheat("DebugResetTimer"));
                Assert.IsTrue(Cheat("DebugSetStage", 3)); Tick();
                foreach (var a in actors) Assert.AreEqual("Awaken", Get(a, "State").ToString(), "A new pursuit must awaken again.");
                Assert.IsTrue(Cheat("DebugResetTimer"));
                yield return null;
                foreach (var a in actors) Assert.IsFalse((bool)Get(a, "IsActivated"));
            } finally {
                for (int i = 0; i < placements.Length; i++) Set(placements[i], "ActivationStage", previous[i]);
                Object.Destroy(replicaObject); ((Behaviour)director).enabled = true;
            }
        }
    }
}
