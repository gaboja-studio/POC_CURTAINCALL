using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools.Tests
{
    public sealed class EscapeFinaleFlowTests
    {
        const string ScenePath = "Assets/Scenes/Levels/escape_route.unity";
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Type StateType = typeof(EscapePursuerBoss).GetNestedType("BossState", BindingFlags.NonPublic);

        static object Snapshot(double round, uint revision, EscapePursuerBoss.Phase phase, byte mask, byte count)
        {
            var state = Activator.CreateInstance(StateType);
            StateType.GetField("Round").SetValue(state, round);
            StateType.GetField("Started").SetValue(state, round);
            StateType.GetField("Revision").SetValue(state, revision);
            StateType.GetField("Phase").SetValue(state, phase);
            StateType.GetField("Mask").SetValue(state, mask);
            StateType.GetField("Reinforcements").SetValue(state, count);
            return state;
        }

        internal static bool Apply(EscapePursuerBoss boss, double round, uint revision, EscapePursuerBoss.Phase phase,
            byte mask = 0, byte count = 0) => (bool)typeof(EscapePursuerBoss).GetMethod("AcceptSnapshot", Instance)
                .Invoke(boss, new[] { Snapshot(round, revision, phase, mask, count), (object)round });

        [Test]
        public void FirstBridgeStartsTheThreeSecondFireAndFinaleLeapsKeepSeparateLanes()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Physics.SyncTransforms();
                var frame = scene.GetRootGameObjects().Single().transform;
                var boss = frame.GetComponentInChildren<EscapePursuerBoss>(true);
                var route = frame.GetComponentInChildren<EscapePursuerRoute>(true);
                var fire = frame.GetComponentInChildren<EscapePursuitFire>(true);
                var fireData = new SerializedObject(fire);
                var bridge = (EscapeCooperativeSpan)fireData.FindProperty("ignitionBridge").objectReferenceValue;
                Assert.That(bridge.name, Is.EqualTo("목마 반출교"));
                Assert.That(fireData.FindProperty("startDelay").floatValue, Is.EqualTo(3f));
                typeof(EscapePursuitFire).GetField("armedAt", Instance).SetValue(fire, 100d);
                Assert.That(fire.PhaseAt(0, 102.9), Is.EqualTo(EscapePursuitFire.FirePhase.Warning));
                Assert.That(fire.PhaseAt(0, 103), Is.EqualTo(EscapePursuitFire.FirePhase.Burst));
                Assert.That(fire.PhaseAt(1, 103), Is.EqualTo(EscapePursuitFire.FirePhase.Dormant));
                var filter = typeof(EscapePursuitFire).GetMethod("ShowBay", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(filter.Invoke(null, new object[] { 354f, 350f, false }), Is.True);
                Assert.That(filter.Invoke(null, new object[] { 354f, 150f, true }), Is.False);

                var bossData = new SerializedObject(boss);
                int point = bossData.FindProperty("leapRoutePoint").intValue;
                var takeoff = route.Points[point];
                Assert.That(takeoff.jump && takeoff.exposed && takeoff.finalSprint, Is.True);
                Assert.That(EscapePursuerNavigation.ClearJump(boss.transform,
                    route.Points[point - 1].feet.position, takeoff.feet.position), Is.True);
                var center = (Transform)bossData.FindProperty("landing").objectReferenceValue;
                Assert.That(frame.InverseTransformPoint(center.position).z, Is.EqualTo(78f).Within(.01f));
                Assert.That(boss.InsideArena(center.position), Is.True);
                var fixture = typeof(EscapePursuerBoss).GetMethod("ReinforcementLeap", Instance);
                var starts = new System.Collections.Generic.List<Vector3> { takeoff.feet.position };
                var ends = new System.Collections.Generic.List<Vector3> { center.position };
                for (int index = 1; index <= 4; ++index)
                {
                    object[] args = { index, Vector3.zero, Vector3.zero };
                    fixture.Invoke(boss, args);
                    starts.Add((Vector3)args[1]);
                    ends.Add((Vector3)args[2]);
                }
                for (int lane = 0; lane < starts.Count; ++lane)
                {
                    Assert.That(EscapePursuerNavigation.Supported(starts[lane]), Is.True);
                    Assert.That(EscapePursuerNavigation.Supported(ends[lane]), Is.True);
                    Vector3 last = starts[lane];
                    for (int i = 1; i <= 80; ++i)
                    {
                        float t = i / 80f;
                        Vector3 next = Vector3.Lerp(starts[lane], ends[lane], t)
                            + Vector3.up * (4 * (lane == 0 ? Settings.GameSettings.EscapeRoute.Pursuer.bossLeapHeight : 4f) * t * (1 - t));
                        Assert.That(EscapePursuerNavigation.Clear(boss.transform, last, next), Is.True,
                            $"Lane {lane} blocked at {frame.InverseTransformPoint(next)}");
                        last = next;
                    }
                    for (int previous = 0; previous < lane; ++previous)
                    {
                        Assert.That(Vector3.Distance(starts[previous], starts[lane]), Is.GreaterThan(5f));
                        Assert.That(Vector3.Distance(ends[previous], ends[lane]), Is.GreaterThan(5f));
                    }
                }
                var before = boss.transform.position;
                typeof(EscapePursuerBoss).GetMethod("StartFromLauncher", Instance).Invoke(boss, null);
                Assert.That(boss.transform.position, Is.EqualTo(before));
                Assert.That(boss.CombatStarted, Is.False);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void LateJoinGetsTheOpenedBreachWithoutMovingTheArenaActor()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var breach = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeUnderstageBreach>(true)).Single();
                var type = typeof(EscapeUnderstageBreach);
                var data = new SerializedObject(breach);
                var actor = (GameObject)data.FindProperty("pursuer").objectReferenceValue;
                var panel = (BoxCollider)data.FindProperty("closedPanel").objectReferenceValue;
                var clock = type.GetNestedType("EntranceClock", BindingFlags.NonPublic);
                var pose = new Vector3(10, 25, -40);
                actor.transform.position = pose;
                bool Receive(double round, bool handoff, double current)
                {
                    var snapshot = Activator.CreateInstance(clock);
                    clock.GetField("Round").SetValue(snapshot, round);
                    clock.GetField("Started").SetValue(snapshot, -1d);
                    clock.GetField("ArenaHandoff").SetValue(snapshot, handoff);
                    return (bool)type.GetMethod("AcceptEntrance", Instance).Invoke(breach, new[] { snapshot, (object)current, current + 1 });
                }
                Assert.That(Receive(10, true, 10), Is.True);
                type.GetMethod("CompleteArenaHandoff", Instance).Invoke(breach, null);
                Assert.That(actor.activeSelf, Is.True);
                Assert.That(actor.transform.position, Is.EqualTo(pose));
                Assert.That(panel.enabled, Is.False);
                Assert.That(Receive(10, false, 10), Is.False);
                Assert.That(Receive(10, true, 20), Is.False);
                Assert.That(Receive(20, false, 20), Is.True);
                Assert.That(type.GetField("arenaHandoff", Instance).GetValue(breach), Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void FinaleExitRequiresBothCutWiresAndTheEntireEncounter()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                var boss = roots.SelectMany(r => r.GetComponentsInChildren<EscapePursuerBoss>(true)).Single();
                var saw = roots.SelectMany(r => r.GetComponentsInChildren<EscapeExitSaw>(true))
                    .First(s => s.FinaleBoss == boss && s.RequiredCableCount > 0);
                var sawData = typeof(EscapeExitSaw);
                var required = sawData.GetProperty("RequiredMask", Instance);
                uint mask = (uint)required.GetValue(saw);
                Assert.That(Apply(boss, 10, 1, EscapePursuerBoss.Phase.Battle, count: 1), Is.True);
                sawData.GetField("cutMask", Instance).SetValue(saw, mask);
                Assert.That(saw.DoorOpen, Is.False);
                Assert.That(Apply(boss, 10, 2, EscapePursuerBoss.Phase.Dead, byte.MaxValue, 1), Is.True);
                Assert.That(saw.DoorOpen, Is.False, "The leader falling cannot unlock the exit while another pursuer remains.");
                typeof(EscapePursuerBoss).GetMethod("EnsureReinforcements", Instance).Invoke(boss, new object[] { 1 });
                var pack = (IList)typeof(EscapePursuerBoss).GetField("reinforcements", Instance).GetValue(boss);
                var extra = (EscapePursuerBoss)pack[0];
                Assert.That(Apply(extra, 10, 1, EscapePursuerBoss.Phase.Dead, byte.MaxValue), Is.True);
                Assert.That(saw.DoorOpen, Is.True);
                sawData.GetField("cutMask", Instance).SetValue(saw, 0u);
                Assert.That(saw.DoorOpen, Is.False, "Defeating the pack cannot replace cutting the wires.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ADefeatedPursuerCannotBeResurrectedByAConflictingSnapshot()
        {
            var root = new GameObject("Pursuer state fixture");
            try
            {
                var boss = root.AddComponent<EscapePursuerBoss>();
                Assert.That(Apply(boss, 10, 3, EscapePursuerBoss.Phase.Dying, byte.MaxValue, 4), Is.True);
                Assert.That(Apply(boss, 10, 3, EscapePursuerBoss.Phase.Dead, byte.MaxValue, 4), Is.False);
                Assert.That(Apply(boss, 10, 4, EscapePursuerBoss.Phase.Battle, byte.MaxValue, 4), Is.False);
                Assert.That(Apply(boss, 10, 4, EscapePursuerBoss.Phase.Dead, byte.MaxValue, 4), Is.True);
                Assert.That(Apply(boss, 10, 5, EscapePursuerBoss.Phase.Dead, byte.MaxValue, 3), Is.False);
                Assert.That(boss.Defeated, Is.True);
                Assert.That(Apply(boss, 20, 1, EscapePursuerBoss.Phase.Waiting), Is.True);
                Assert.That(boss.Defeated, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ARemotePlayersLateRestartDoesNotWipeTheNewPack()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            var guest = new GameObject("Delayed remote player restart");
            guest.SetActive(false);
            try
            {
                var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapePursuerBoss>(true)).Single();
                var type = typeof(EscapePursuerBoss);
                Assert.That(Apply(boss, 20, 1, EscapePursuerBoss.Phase.Battle, count: 1), Is.True);
                type.GetMethod("EnsureReinforcements", Instance).Invoke(boss, new object[] { 1 });
                var pack = (IList)type.GetField("reinforcements", Instance).GetValue(boss);
                var player = guest.AddComponent<NetworkPlayer>();
                Assert.That(player.IsLocal || player.IsServer, Is.False);
                type.GetMethod("OnPlayerRestarted", Instance).Invoke(boss, new object[] { player });
                Assert.That(boss.EncounterSize, Is.EqualTo(2));
                Assert.That(pack.Count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(guest);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void NewBossEpochRemovesDetachedVisualsFromThePreviousPack()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            var dropped = new GameObject("Detached reinforcement effect");
            try
            {
                var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapePursuerBoss>(true)).Single();
                var type = typeof(EscapePursuerBoss);
                Assert.That(Apply(boss, 10, 1, EscapePursuerBoss.Phase.Battle, count: 1), Is.True);
                type.GetMethod("EnsureReinforcements", Instance).Invoke(boss, new object[] { 1 });
                var pack = (IList)type.GetField("reinforcements", Instance).GetValue(boss);
                var detached = (IList)type.GetField("severedVisuals", Instance).GetValue(pack[0]);
                detached.Add(dropped);
                Assert.That(Apply(boss, 20, 1, EscapePursuerBoss.Phase.Waiting), Is.True);
                Assert.That(dropped == null, Is.True);
                Assert.That(pack.Count, Is.Zero);
            }
            finally
            {
                if (dropped) UnityEngine.Object.DestroyImmediate(dropped);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void HorizontalSawCanReachEveryExposedLimbInTheArena()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapePursuerBoss>(true)).Single();
                var hit = typeof(EscapePursuerBoss).GetMethod("ConsiderSawHit", Instance);
                Assert.That(boss.PartCount, Is.EqualTo(8));
                for (int part = 0; part < boss.PartCount; part++)
                {
                    byte alreadyCut = (byte)(byte.MaxValue & ~(1 << part));
                    Assert.That(Apply(boss, 10 + part, 1, EscapePursuerBoss.Phase.Battle, alreadyCut, 1), Is.True);
                    var box = boss.Hitbox(part);
                    Assert.That(box, Is.Not.Null);
                    var center = box.transform.TransformPoint(box.center);
                    var projected = new Vector3(center.x, boss.transform.position.y + 1.05f, center.z);
                    object[] args = { projected - Vector3.forward * 8f, projected + Vector3.forward * 8f,
                        Vector3.up, .4f, .12f, null, -1, float.PositiveInfinity };
                    hit.Invoke(boss, args);
                    Assert.That(args[5], Is.SameAs(boss), $"Horizontal hit missed limb {part}");
                    Assert.That(args[6], Is.EqualTo(part));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OnlyThePhysicalExitRecordsOpeningOnceInEitherCompletionOrder(bool cutWiresFirst)
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                var boss = roots.SelectMany(r => r.GetComponentsInChildren<EscapePursuerBoss>(true)).Single();
                var launchers = roots.SelectMany(r => r.GetComponentsInChildren<EscapeExitSaw>(true))
                    .Where(s => s.FinaleBoss == boss && s.RequiredCableCount > 0).ToArray();
                var sawType = typeof(EscapeExitSaw);
                var record = sawType.GetMethod("TryConsumeFinaleDoorOpened", Instance);
                var required = sawType.GetProperty("RequiredMask", Instance);
                var cuts = sawType.GetField("cutMask", Instance);
                var owner = launchers.Single(s => s.name == "40_AExitCableSaw");
                Assert.That(launchers.Length, Is.GreaterThan(1));
                bool Record(EscapeExitSaw launcher) => (bool)record.Invoke(launcher, null);
                void CutAll()
                {
                    foreach (var launcher in launchers) cuts.SetValue(launcher, required.GetValue(launcher));
                }
                void Defeat() => Assert.That(Apply(boss, 10, 2, EscapePursuerBoss.Phase.Dead, byte.MaxValue), Is.True);

                Assert.That(Apply(boss, 10, 1, EscapePursuerBoss.Phase.Battle), Is.True);
                if (cutWiresFirst) CutAll(); else Defeat();
                Assert.That(launchers.Any(Record), Is.False);
                if (cutWiresFirst) Defeat(); else CutAll();
                Assert.That(launchers.Where(s => s != owner).Any(Record), Is.False);
                Assert.That(Record(owner), Is.True);
                Assert.That(Record(owner), Is.False);

                Assert.That(Apply(boss, 20, 1, EscapePursuerBoss.Phase.Waiting), Is.True);
                foreach (var launcher in launchers) sawType.GetMethod("ResetProjectileCourse", Instance).Invoke(launcher, null);
                Assert.That(Record(owner), Is.False);
                Assert.That(Apply(boss, 20, 2, EscapePursuerBoss.Phase.Dead, byte.MaxValue), Is.True);
                CutAll();
                Assert.That(Record(owner), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(1)]
        [TestCase(4)]
        public void FinaleSnapshotKeepsIndependentPursuersAndResetsTheNextRound(int participants)
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapePursuerBoss>(true)).Single();
                var methods = typeof(EscapePursuerBoss);
                Assert.That(Apply(boss, 10, 1, EscapePursuerBoss.Phase.Battle, count: (byte)participants), Is.True);
                methods.GetMethod("EnsureReinforcements", Instance).Invoke(boss, new object[] { participants });
                var pack = (IList)methods.GetField("reinforcements", Instance).GetValue(boss);
                Assert.That(pack.Count, Is.EqualTo(participants));
                Assert.That(boss.EncounterSize, Is.EqualTo(participants + 1));
                var all = new[] { boss }.Concat(pack.Cast<EscapePursuerBoss>());
                Assert.That(all.Select(b => methods.GetProperty("Message", Instance).GetValue(b)).Distinct().Count(),
                    Is.EqualTo(participants + 1));
                var extra = (EscapePursuerBoss)pack[0];
                Assert.That(Apply(extra, 10, 4, EscapePursuerBoss.Phase.Hit, 1), Is.True);
                Assert.That(Apply(extra, 10, 3, EscapePursuerBoss.Phase.Battle), Is.False);
                Assert.That(extra.Cuts, Is.EqualTo(1));
                Assert.That(boss.Cuts, Is.Zero);
                Assert.That(Apply(boss, 20, 1, EscapePursuerBoss.Phase.Waiting), Is.True);
                Assert.That(pack.Count, Is.Zero);
                Assert.That(boss.EncounterDefeated, Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
