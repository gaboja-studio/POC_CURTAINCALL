using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        static object PursuitPrivate(object obj, string name) => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name).SetValue(obj, value);
        IEnumerator PursuitHost()
        {
            yield return SceneManager.LoadSceneAsync("Start"); yield return IsolatedMenuHost();
            Assert.IsTrue((bool)Call(launcher, "TryLaunch", Point(2, "Morning", 0, "Tightrope")));
            float until = Time.realtimeSinceStartup + 75;
            while ((bool)Get(launcher, "IsBusy") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Get(launcher, "LastLaunchSucceeded")); yield return MenuReady();
        }
        IEnumerator TickPursuit(Component actor, string state, float seconds = 3)
        {
            float until = Time.time + seconds;
            do { Call(actor, "ServerTick", 4f, false); yield return null; }
            while (Get(actor, "Behaviour").ToString() != state && Time.time < until);
            Assert.AreEqual(state, Get(actor, "Behaviour").ToString());
        }
        [UnityTest]
        public IEnumerator PursuitBehaviour_GraphHearingChaseSearchAndCulling()
        {
            yield return PursuitHost();
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance"); Call(run, "DebugSetRemaining", .01f);
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            float until = Time.time + 8;
            while ((float)Get(director, "Elapsed") < 3 && Time.time < until) yield return null;
            ((Behaviour)director).enabled = false;
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var candidates = (IList)PursuitPrivate(director, "candidates"); candidates.Clear();
            var rules = Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit");
            var stage = Call(rules, "Stage", 4f); string saved = JsonUtility.ToJson(stage);
            var dummy = new GameObject("VisibleTargetFixture");
            try {
                Set(stage, "AlertSeconds", .35f); Set(stage, "ListenSeconds", .3f); Set(stage, "InvestigateSeconds", .5f); Set(stage, "SearchSeconds", .25f);
                Set(stage, "SeeThroughWalls", true); Set(stage, "AlwaysNearest", false); Set(stage, "SightRadius", 10f); Set(stage, "TrackingRadius", 15f);
                Set(stage, "ChaseSpeed", .5f); Set(stage, "WalkHearingRadius", 6f); Set(stage, "RunHearingRadius", 14f);
                // 대상을 100m 밖으로 옮겨 수색을 확인하므로 설정 에셋의 순찰 이탈 제한(1단계 30m)은 끈다. 이탈 복귀는 PatrolLeash 테스트가 따로 본다.
                Set(stage, "PatrolLeashDistance", 0f);
                Assert.IsTrue((bool)Get(actor, "GraphReady"));
                Call(actor, "ResetActor"); yield return TickPursuit(actor, "Patrol");
                var point = actor.transform.position + Vector3.right * 10;
                Call(director, "EmitFootstep", point, false, null); yield return TickPursuit(actor, "Patrol");
                Call(director, "EmitFootstep", point, true, null); yield return TickPursuit(actor, "Listen");
                yield return new WaitForSeconds(.85f);
                Call(director, "EmitFootstep", actor.transform.position + actor.transform.forward * 2, false, null);
                yield return TickPursuit(actor, "Investigate");
                yield return new WaitForSeconds(.6f); yield return TickPursuit(actor, "Patrol");
                dummy.transform.position = actor.transform.position + actor.transform.forward * 4;
                candidates.Add(dummy); yield return TickPursuit(actor, "Prepare"); yield return TickPursuit(actor, "Chase");
                dummy.transform.position = actor.transform.position + Vector3.right * 100;
                yield return TickPursuit(actor, "Search"); candidates.Clear(); yield return TickPursuit(actor, "Patrol");
                var renderer = actor.GetComponentsInChildren<Renderer>().First(r => r.enabled);
                Call(actor, "SetRendered", false); Assert.IsTrue(renderer.forceRenderingOff);
                Assert.IsTrue(actor.gameObject.activeInHierarchy); Assert.IsTrue((bool)Get(actor, "GraphReady"));
                var before = actor.transform.position;
                until = Time.time + 1;
                while (Time.time < until) { Call(actor, "ServerTick", 4f, false); yield return null; }
                Assert.Greater(Vector3.Distance(before, actor.transform.position), .05f, "Hidden renderers must keep navigating.");
                Call(actor, "SetRendered", true); Assert.IsFalse(renderer.forceRenderingOff);
                renderer.forceRenderingOff = true; Call(actor, "SetRendered", false); Call(actor, "SetRendered", true);
                Assert.IsTrue(renderer.forceRenderingOff, "Keep preexisting render suppression."); renderer.forceRenderingOff = false;
                Call(actor, "ResetActor"); yield return TickPursuit(actor, "Patrol");
            } finally { JsonUtility.FromJsonOverwrite(saved, stage); Object.Destroy(dummy); }
        }
        [UnityTest]
        public IEnumerator PursuitBehaviour_PatrolLeashReturnsWithoutReacquiringAndZeroDisablesIt()
        {
            yield return PursuitHost();
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance"); Call(run, "DebugSetRemaining", .01f);
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            float until = Time.time + 8;
            while ((float)Get(director, "Elapsed") < 3 && Time.time < until) yield return null;
            ((Behaviour)director).enabled = false;
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var candidates = (IList)PursuitPrivate(director, "candidates"); candidates.Clear();
            var stage = Get(director, "CurrentStageRules"); string saved = JsonUtility.ToJson(stage);
            var placement = (Component)Get(actor, "Placement");
            var route = (Transform[])Field(placement, "PatrolPoints"); bool useRoom = (bool)Field(placement, "UseRoomPatrol");
            var dummy = new GameObject("LeashTarget");
            try {
                Set(placement, "UseRoomPatrol", false); Set(placement, "PatrolPoints", Array.Empty<Transform>());
                Set(stage, "AlwaysNearest", true); Set(stage, "PatrolLeashDistance", 5f);
                Set(stage, "AlertSeconds", 0f); Set(stage, "ChaseSpeed", .1f); Set(stage, "PatrolSpeed", 3f);
                Call(actor, "ResetActor"); yield return TickPursuit(actor, "Patrol");
                var home = actor.transform.position;
                var agent = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
                // Pick a reachable nearby point from the real room NavMesh.
                Vector3 away = home; var path = new UnityEngine.AI.NavMeshPath();
                foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back }) {
                    if (!UnityEngine.AI.NavMesh.SamplePosition(home + direction * 3, out var hit, 1, agent.areaMask)) continue;
                    float distance = Vector3.Distance(home, hit.position);
                    if (distance < 2 || distance > 4 || !UnityEngine.AI.NavMesh.CalculatePath(home, hit.position, agent.areaMask, path)
                        || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;
                    away = hit.position; break;
                }
                Assert.Greater(Vector3.Distance(home, away), 2);
                dummy.transform.position = home + Vector3.up * 4; candidates.Add(dummy);
                yield return TickPursuit(actor, "Chase");
                Assert.IsTrue(agent.Warp(away));
                dummy.transform.position = home + Vector3.up * 10;
                yield return TickPursuit(actor, "Return");
                Assert.IsNull(Get(actor, "ChasedPlayer"));
                Assert.IsTrue((bool)Get(actor, "ReturningToPatrol"));
                Assert.IsNull(Call(director, "NearestVisible", actor, stage), "Even AlwaysNearest respects the patrol boundary.");
                // Bring a visible target back inside the boundary during the return.
                dummy.transform.position = home + Vector3.up * 4;
                Call(actor, "Hear", away, 0f);
                Call(actor, "ServerTick", 4f, false); yield return null;
                Assert.IsNull(Get(actor, "ChasedPlayer"), "Do not ping-pong between chasing and returning.");
                candidates.Clear();
                yield return TickPursuit(actor, "Patrol", 10);
                Assert.Less(Vector3.Distance(home, actor.transform.position), .9f);
                Assert.IsFalse((bool)Get(actor, "ReturningToPatrol"));
                candidates.Add(dummy); yield return TickPursuit(actor, "Chase");
                Set(stage, "PatrolLeashDistance", 0f);
                dummy.transform.position = home + Vector3.up * 100;
                Call(actor, "ServerTick", 4f, false); yield return null;
                Assert.AreEqual(dummy, Get(actor, "ChasedPlayer"), "Zero restores unrestricted final-stage pursuit.");
                // The actor crossing the limit also cancels pursuit, even if the target stays inside.
                Set(stage, "PatrolLeashDistance", 1f); dummy.transform.position = home + Vector3.up * .5f;
                Assert.IsTrue(agent.Warp(away)); yield return TickPursuit(actor, "Return");
                Assert.IsNull(Get(actor, "ChasedPlayer"));
            } finally {
                JsonUtility.FromJsonOverwrite(saved, stage); Set(placement, "PatrolPoints", route); Set(placement, "UseRoomPatrol", useRoom);
                Object.Destroy(dummy);
            }
        }

        [UnityTest]
        public IEnumerator PursuitBehaviour_CaptureFourDirectionsBloodAndCleanup()
        {
            yield return PursuitHost();
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector"); ((Behaviour)director).enabled = false;
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var model = (Component)Get(player.GetComponent(T("CurtainCall.Player.HumanoidPlayerPresentation")), "Model");
            var bloodSetting = T("CurtainCall.StartLobby.ClientSettings").GetProperty("ShowBloodEffect");
            bool previous = (bool)bloodSetting.GetValue(null);
            Component pose = null;
            try {
                bloodSetting.SetValue(null, true);
                for (int direction = 0; direction < 4; direction++) {
                    pose = player.gameObject.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose"));
                    var anchor = model.transform.position;
                    var upright = Quaternion.Euler(0, 90 * direction, 0);
                    Call(pose, "SetCaptureFrame", anchor, upright); Set(pose, "Elapsed", 2f);
                    actor.transform.position = player.transform.position + Vector3.right * 1.1f;
                    Call(actor, "Capture", player.gameObject, 2f);
                    yield return null; yield return null; yield return null;
                    var head = (Vector3)Get(pose, "HeadDirection");
                    Assert.Greater(Vector3.Dot(actor.transform.forward, head), .999f);
                    Assert.Less(Vector3.Distance(actor.transform.position, (Vector3)Get(pose, "AttackerPosition")), .002f);
                    var animator = (Animator)Field(model, "Animator");
                    var actualAxis = Vector3.ProjectOnPlane(animator.GetBoneTransform(HumanBodyBones.Head).position - animator.GetBoneTransform(HumanBodyBones.Hips).position, Vector3.up).normalized;
                    Assert.Greater(Vector3.Dot(actor.transform.forward, actualAxis), .9f, "Face the actual head, not the character root.");
                    var blood = player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitCaptureBlood"));
                    Assert.IsTrue((bool)Get(blood, "Visible")); Assert.Greater((int)Get(blood, "EmittedBursts"), 0);
                    var spray = player.GetComponentsInChildren<ParticleSystem>().First(p => p.name.Contains("CaptureBloodSpray"));
                    Assert.Greater(spray.particleCount, 0);
                    Assert.IsNotNull(GameObject.Find("PursuitBloodOnLens"));
                    bloodSetting.SetValue(null, false); yield return null;
                    Assert.IsFalse((bool)Get(blood, "Visible")); Assert.AreEqual(0, spray.particleCount);
                    bloodSetting.SetValue(null, true); Set(pose, "Elapsed", 2.5f); yield return null;
                    Assert.Greater(spray.particleCount, 0);
                    Set(pose, "Hidden", true); yield return null; Assert.AreEqual(0, spray.particleCount);
                    Call(actor, "FinishCapture"); Object.Destroy(pose); pose = null; yield return null; yield return null;
                    Assert.IsNull(GameObject.Find("PursuitBloodOnLens"));
                    Assert.IsNull(player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitCaptureBlood")));
                }
            } finally { if (pose != null) Object.Destroy(pose); bloodSetting.SetValue(null, previous); }
        }
    }
}
