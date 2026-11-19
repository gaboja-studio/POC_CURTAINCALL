using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// 2026-11-17 사용자 보고 구역을 실제 통합 대기실에서 추격한다: 숙소 환풍구→지하 계단, 계단실 경사로,
    /// 무대 장치실 화물 통로가 꺾이는 곳의 1.1m 난간(바닥으로 내려가지 않고 바로 넘기).
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        static readonly (string name, Vector3 from, Vector3 to, float minY)[] CoverageRoutes = {
            ("vent-utility-stairs", new Vector3(22.8f, 2.32f, 71.2f), new Vector3(25.3f, -4.78f, 73.4f), float.NegativeInfinity),
            ("stairwell-flight-ramp", new Vector3(7.0f, .04f, 104.8f), new Vector3(3.3f, -4.96f, 102.0f), float.NegativeInfinity),
            ("cargo-northwest-rail", new Vector3(-58.6f, 5.64f, 74.0f), new Vector3(-53f, 5.64f, 78.6f), 5f),
            ("cargo-southeast-rail", new Vector3(-42f, 5.64f, 29.8f), new Vector3(-28.5f, 5.64f, 36f), 5f),
        };

        [UnityTest]
        public IEnumerator PursuitCoverage_StairsVentAndRailCornersReachTarget()
        {
            // 실제 게임이 쓰는 통합 대기실(굽힌 NavMesh 포함). 옛 추격 테스트 씬에는 NavMesh가 없다.
            yield return PursuitHost();
            Assert.IsTrue(SceneManager.GetSceneByPath("Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity").isLoaded);
            // The upper wall guards the drop, but must stop where the south deck meets the east deck.
            bool Blocked(float height, float z) => Physics.Linecast(new Vector3(-30.5f, height, z), new Vector3(-29.35f, height, z),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Assert.IsFalse(Blocked(6.1f, 30.6f) || Blocked(7.6f, 30.6f), "The shared deck needs an open passage at body and head height.");
            Assert.IsTrue(Blocked(6.1f, 32f) && Blocked(7.6f, 32f), "The guard above the actual drop must stay intact.");
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance"); Call(run, "DebugSetRemaining", .01f);
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            float until = Time.time + 8;
            while ((float)Get(director, "Elapsed") < 3 && Time.time < until) yield return null;
            var rules = Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit");
            var stages = (System.Array)Field(rules, "Stages");
            Assert.IsTrue((bool)Call(director, "DebugSetStage", stages.Length - 1));
            yield return null; yield return null;
            ((Behaviour)director).enabled = false;
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var candidates = (IList)PursuitPrivate(director, "candidates");
            var agent = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var target = new GameObject("CoverageTargetFixture");
            var report = new System.Text.StringBuilder();
            int failures = 0;
            try {
                foreach (var route in CoverageRoutes) {
                    candidates.Clear(); Call(actor, "ResetActor");
                    agent.enabled = false; actor.transform.position = route.from;
                    target.transform.position = route.to; candidates.Add(target);
                    float minY = float.PositiveInfinity, closest = float.PositiveInfinity;
                    bool vaulted = false, reached = false;
                    until = Time.time + 30;
                    while (Time.time < until) {
                        Call(actor, "ServerTick", 600f, false);
                        yield return null;
                        var p = actor.transform.position;
                        minY = Mathf.Min(minY, p.y);
                        vaulted |= (bool)Get(actor, "IsVaulting");
                        var d = route.to - p; float flat = new Vector2(d.x, d.z).magnitude;
                        closest = Mathf.Min(closest, flat);
                        // 겹친 계단참(1.4m 위아래)을 도착으로 보지 않도록 같은 층만 인정한다.
                        if (flat < 1.6f && Mathf.Abs(d.y) < .6f) { reached = true; break; }
                    }
                    bool ok = reached && minY >= route.minY - .3f;
                    if (!ok) failures++;
                    report.AppendLine($"{route.name}: reached={reached} closest={closest:0.00} minY={minY:0.00} vaulted={vaulted} end={actor.transform.position} motion={Get(actor, "State")} behaviour={Get(actor, "Behaviour")} agent={agent.enabled}/{(agent.enabled && agent.isOnNavMesh)} activated={Get(actor, "IsActivated")} navFailed={Get(actor, "NavigationFailed")} target={actor.GetType().GetField("target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(actor) != null}");
                }
            } finally { candidates.Clear(); Object.Destroy(target); }
            Debug.Log("[PursuitCoverage]\n" + report);
            Assert.AreEqual(0, failures, report.ToString());
        }

        [UnityTest]
        public IEnumerator PursuitRendered_MannequinCrouchesThroughTheDormDuct()
        {
            yield return EnterIntegratedPursuitRoom();
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            var exploration = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            Call(exploration, "RequestDebugOpen", "dorm-vent:dorm-cover");
            Call(exploration, "RequestDebugOpen", "dorm-vent:restroom-cover");
            Assert.IsTrue((bool)Call(run, "DebugSetRemaining", .05f));
            float until = Time.realtimeSinceStartup + 15f;
            while ((float)Get(director, "Elapsed") < 3 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That((float)Get(director, "Elapsed"), Is.GreaterThanOrEqualTo(3f));
            var stages = (System.Array)Field(Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit"), "Stages");
            Assert.IsTrue((bool)Call(director, "DebugSetStage", stages.Length - 1));
            yield return null;
            ((Behaviour)director).enabled = false;

            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var candidates = (IList)PursuitPrivate(director, "candidates");
            Call(actor, "ResetActor");
            actor.transform.position = new Vector3(23f, 2.32f, 71.2f); // junction at the dorm-side mouth
            var motion = (Animation)Field(actor, "Motion");
            var lane = GameObject.Find("ToRestroomLane").transform;
            var roof = lane.Find("Top").GetComponent<Collider>().bounds;
            var target = new GameObject("Duct pursuit test target");
            var mesh = new Mesh();
            try
            {
                target.transform.position = new Vector3(23f, 2.32f, 65f);
                candidates.Clear(); candidates.Add(target);
                float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity, highest = float.NegativeInfinity;
                int crouchFrames = 0;
                until = Time.realtimeSinceStartup + 22f;
                while (Time.realtimeSinceStartup < until)
                {
                    Call(actor, "ServerTick", 600f, false);
                    yield return null;
                    var feet = actor.transform.position;
                    if (feet.y < roof.min.y && feet.y > roof.min.y - 1.5f && feet.x > roof.min.x && feet.x < roof.max.x
                        && feet.z > roof.min.z + .3f && feet.z < roof.max.z - .3f)
                    {
                        var gait = Get(actor, "State").ToString();
                        Assert.That(gait, Is.EqualTo("CrouchWalk").Or.EqualTo("Crouch"), $"Standing inside the duct at {feet}");
                        if (gait == "CrouchWalk" && motion.IsPlaying("CrouchWalk"))
                        {
                            minZ = Mathf.Min(minZ, feet.z); maxZ = Mathf.Max(maxZ, feet.z);
                            if (++crouchFrames % 8 == 1)
                            {
                                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                                {
                                    skin.BakeMesh(mesh);
                                    // BakeMesh already applies the renderer scale; add only rotation and translation.
                                    foreach (var v in mesh.vertices)
                                        highest = Mathf.Max(highest, (skin.transform.position + skin.transform.rotation * v).y);
                                }
                            }
                        }
                    }
                    if (Vector3.Distance(feet, target.transform.position) < 1.25f) break;
                }
                Debug.Log($"[PursuitRendered] duct crouch frames={crouchFrames}, travel={maxZ-minZ:0.00}m, skin={highest:0.00}, roof={roof.min.y:0.00}, end={actor.transform.position}");
                Assert.Greater(maxZ - minZ, 3f, "Traverse the real duct in the crouch gait for several meters.");
                Assert.LessOrEqual(highest, roof.min.y + .05f, "The animated body must fit under the actual roof collider.");
                Assert.Less(Vector3.Distance(actor.transform.position, target.transform.position), 1.25f);
            }
            finally { candidates.Clear(); Object.Destroy(target); Object.Destroy(mesh); }
        }

        [UnityTest]
        public IEnumerator PursuitRendered_RoutesFollowThePeriodAndEscapePrerequisites()
        {
            yield return EnterIntegratedPursuitRoom();
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
            ((Behaviour)Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector")).enabled = false; // isolate route display from capture
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var planner = Find("CurtainCall.UI.Wayfinding.NavigationPathPlanner");
            var quest = Find("CurtainCall.WaitingRoom.Pursuit.PursuitQuest");
            var stage = Find("CurtainCall.WaitingRoom.StageEntranceGather");
            var stageGoal = (Vector3)Get(stage,"GuideDestination");
            var stageBounds = stage.GetComponent<BoxCollider>().bounds;
            var escape = Find("CurtainCall.EscapeRoute.EscapeEntrance");
            var escapeBounds = ((BoxCollider)Get(escape, "AssemblyArea")).bounds;
            var escapeGoal = escapeBounds.center;
            var discovery = GameObject.Find("Area_B1_EscapeEntrance").GetComponent<BoxCollider>();
            var exploration = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");

            Assert.IsTrue((bool)Call(run, "DebugStartAt", Point(1, "Rehearsal", 0, "Tightrope")));
            float until = Time.realtimeSinceStartup + 25;
            var rehearsal = Find("CurtainCall.WaitingRoom.WaitingRoomRehearsal");
            while (!(bool)Static("CurtainCall.WaitingRoom.WaitingRoomRehearsal", "IsCompleted") && Time.realtimeSinceStartup < until)
            {
                Call(rehearsal, "TryClearCurrentStage");
                yield return new WaitForSecondsRealtime(.25f);
            }
            Assert.IsTrue((bool)Static("CurtainCall.WaitingRoom.WaitingRoomRehearsal", "IsCompleted"));
            var stageFeet = Vector3.zero;
            yield return FindEntranceProbe(planner, stageGoal, stageBounds, p => stageFeet = p);
            Call(mover, "SetStartPose", stageFeet, Quaternion.identity);
            yield return WaitForVisibleRoute(() => PursuitPrivate(Find("CurtainCall.WaitingRoom.RehearsalPathGuide"), "navigation"), stageGoal, "rehearsal complete → stage");

            // The normal day-one rehearsal has no countdown and leads directly to the first performance.
            Assert.IsTrue((bool)Call(run, "DebugStartAt", Point(2, "Between", 1, "Tightrope")));
            yield return null;
            Call(mover, "SetStartPose", stageFeet, Quaternion.identity);
            Assert.IsTrue((bool)Call(run, "DebugSetRemaining", 30f));
            yield return new WaitForSecondsRealtime(.5f);
            Assert.IsNull(PursuitPrivate(quest, "guide"), "No pursuit route while the timer is counting.");
            Assert.IsTrue((bool)Call(run, "DebugSetRemaining", .05f));
            yield return WaitForVisibleRoute(() => PursuitPrivate(quest, "guide"), stageGoal, "break pursuit → stage");
            Call(exploration, "MarkVisited", "visit:B1_EscapeEntrance");
            Assert.IsFalse((bool)Get(escape,"IsPassageOpen"));
            var escapeFeet = Vector3.zero;
            yield return FindEntranceProbe(planner, escapeGoal, escapeBounds, p => escapeFeet = p, requireRoute: false);
            Call(mover, "SetStartPose", escapeFeet, Quaternion.identity);
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsNull(PursuitPrivate(quest, "guide"), "Discovery alone must not enable an escape guide.");
            Call(exploration, "RequestDebugOpen", "final:escape-route");
            until = Time.realtimeSinceStartup + 25;
            while (!(bool)Get(escape, "IsPassageOpen") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Get(escape, "IsPassageOpen"), "Integrated passage state must open the escape assembly.");
            yield return new WaitForSecondsRealtime(.6f);
            until = Time.realtimeSinceStartup + 18;
            while ((bool)Static("CurtainCall.WaitingRoom.Pursuit.PursuitDirector", "CutsceneHold") && Time.realtimeSinceStartup < until)
                yield return null;
            yield return FindEntranceProbe(planner, escapeGoal, escapeBounds, p => escapeFeet = p, discovery.bounds);
            Call(mover, "SetStartPose", escapeFeet, Quaternion.identity);
            yield return WaitForVisibleRoute(() => PursuitPrivate(quest, "guide"), escapeGoal, "break pursuit, discovered/open → escape");
            Call(mover, "SetStartPose", stageFeet, Quaternion.identity);
            yield return WaitForVisibleRoute(() => PursuitPrivate(quest, "guide"), stageGoal, "break pursuit near stage with both destinations open → stage");

            Assert.IsTrue((bool)Call(run, "DebugStartAt", Point(2, "Night", 0, "")));
            yield return null;
            var dorm = Find("CurtainCall.WaitingRoom.DormEntryEvent");
            var dormGoalMethod = dorm.GetType().GetMethod("EntranceGuideDestination", BindingFlags.Static | BindingFlags.NonPublic);
            var dormGoal = (Vector3)dormGoalMethod.Invoke(null, null);
            var dormFeet = Vector3.zero;
            yield return FindEntranceProbe(planner, dormGoal, (Bounds)PursuitPrivate(dorm, "inside"), p => dormFeet = p);
            Call(mover, "SetStartPose", dormFeet, Quaternion.identity);
            Assert.IsTrue((bool)Call(run, "DebugSetRemaining", .05f));
            yield return WaitForVisibleRoute(() => PursuitPrivate(quest, "guide"), dormGoal, "maintenance pursuit → dorm");

            Call(exploration, "RequestDebugOpen", "final:escape-route");
            until = Time.realtimeSinceStartup + 25;
            while (!(bool)Get(escape, "IsPassageOpen") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Get(escape, "IsPassageOpen"));
            Assert.IsFalse((bool)Call(exploration,"HasVisited","visit:B1_EscapeEntrance"));
            Call(mover, "SetStartPose", escapeFeet, Quaternion.identity);
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsFalse((bool)Call(exploration,"HasVisited","visit:B1_EscapeEntrance"), "Probe must stay outside the discovery volume.");
            Assert.IsNull(PursuitPrivate(quest, "guide"), "An open passage alone must not reveal an undiscovered escape route.");
            Call(exploration, "MarkVisited", "visit:B1_EscapeEntrance");
            yield return WaitForVisibleRoute(() => PursuitPrivate(quest, "guide"), escapeGoal, "maintenance pursuit, discovered/open → escape");
            Call(mover, "SetStartPose", dormFeet, Quaternion.identity);
            yield return WaitForVisibleRoute(() => PursuitPrivate(quest, "guide"), dormGoal, "maintenance pursuit near dorm with both destinations open → dorm");
        }

        IEnumerator EnterIntegratedPursuitRoom()
        {
            Assert.IsFalse(Application.isBatchMode, "The curtain requires rendered frames in an interactive Game View.");
            yield return PursuitHost();
            Assert.IsTrue(SceneManager.GetSceneByPath("Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity").isLoaded);
            Assert.IsFalse((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"));
        }

        static IEnumerator FindEntranceProbe(Component planner, Vector3 goal, Bounds arrival, System.Action<Vector3> found,
            Bounds? discovery = null, bool requireRoute = true)
        {
            var path = new List<Vector3>();
            var sampled = new HashSet<Vector3>();
            int possible = 0;
            string goalMesh = "none";
            float until = Time.realtimeSinceStartup + 28;
            while (Time.realtimeSinceStartup < until)
            {
                // Warm the same player-nav mesh as the guide, then choose a real walkable point outside the entrance.
                Call(planner, "TryCalculate", goal + Vector3.left * 6, goal, path);
                if ((bool)PursuitPrivate(planner, "ready"))
                {
                    var filter = (NavMeshQueryFilter)PursuitPrivate(planner, "filter");
                    if (NavMesh.SamplePosition(goal, out var goalHit, 3, filter)) goalMesh = goalHit.position.ToString();
                    for (float radius = 3; radius <= 11; radius += .5f)
                        for (int i = 0; i < 32; i++)
                        {
                            float a = i * Mathf.PI / 16;
                            var probe = new Vector3(goal.x, arrival.min.y, goal.z) + radius * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                            if (!NavMesh.SamplePosition(probe, out var hit, 1.25f, filter)) continue;
                            var feet = hit.position;
                            sampled.Add(feet);
                            if (arrival.Contains(feet + Vector3.up * .5f) || (discovery.HasValue && discovery.Value.Contains(feet + Vector3.up * .35f))) continue;
                            if (!requireRoute) { found(feet); yield break; }
                            if (!(bool)Call(planner,"TryCalculate",feet,goal,path)) continue;
                            possible++;
                            float d = 0;
                            for (int n = 1; n < path.Count; n++) d += Vector3.Distance(path[n-1],path[n]);
                            if (d < 3f || d > 10.5f) continue;
                            found(feet);
                            yield break;
                        }
                }
                yield return new WaitForSecondsRealtime(.25f);
            }
            Assert.Fail($"No valid integrated-scene probe at {goal} (arrival={arrival}, discovery={discovery}, goal mesh={goalMesh}, samples={sampled.Count}, routes={possible}).");
        }

        static IEnumerator WaitForVisibleRoute(System.Func<object> handle, Vector3 goal, string scenario)
        {
            float until = Time.realtimeSinceStartup + 25;
            while (Time.realtimeSinceStartup < until)
            {
                var route = handle();
                if (route != null && (bool)Get(route, "IsVisible"))
                {
                    var path = (Vector3[])Get(route, "Path");
                    if (path.Length >= 2 && Vector3.Distance(path[^1], goal) <= 2.7f)
                    {
                        Debug.Log($"[PursuitRendered] {scenario}: {path[0]} → {path[^1]} ({path.Length} corners)");
                        yield break;
                    }
                }
                yield return null;
            }
            Assert.Fail($"{scenario}: no visible guide ending at {goal}.");
        }
    }
}
