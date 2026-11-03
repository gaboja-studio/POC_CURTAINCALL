using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        static void AssertPursuitBody(Component model, int stage)
        {
            Assert.AreEqual(stage, Get(model, "Stage"));
            Assert.AreEqual(stage == 0, ((Renderer)Field(model, "Original")).enabled,
                "The intact body must stay hidden after capture releases an injured player.");
            var bodies = (Renderer[])Field(model, "Bodies");
            for (int i = 0; i < bodies.Length; i++) Assert.AreEqual(stage == i + 1, bodies[i].enabled);
            foreach (var limb in (Renderer[])Field(model, "Limbs")) Assert.IsFalse(limb.enabled);
        }

        [UnityTest]
        public IEnumerator PursuitCapture_HideAndReleasePreserveCurrentInjury()
        {
            var player = Object.Instantiate(Resources.Load<GameObject>("Prefabs/Characters/Players/Player"));
            try
            {
                ((Behaviour)player.GetComponent(T("CurtainCall.Player.PlayerController"))).enabled = false;
                yield return null;
                var condition = player.GetComponent(T("CurtainCall.Player.PlayerCondition"));
                var model = (Component)Get(player.GetComponent(T("CurtainCall.Player.HumanoidPlayerPresentation")), "Model");
                // Exercise models with a non-zero crawl offset, too (the rabbit uses zero).
                model.GetType().GetField("CrawlHeightOffset").SetValue(model, -.05f);
                var original = (Renderer)Field(model, "Original");
                var bodies = (Renderer[])Field(model, "Bodies");
                var parts = T("CurtainCall.Player.BodyPart");
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Call(condition, "SetLostParts", Enum.ToObject(parts, 0));
                    Vector3 standingPosition = model.transform.localPosition;
                    var pose = player.AddComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose"));
                    yield return null;
                    Call(condition, "SetLostParts", Enum.ToObject(parts, 4)); // left leg
                    yield return null;
                    AssertPursuitBody(model, 1);
                    // Preserve another system's culling override on an inactive injury mesh.
                    bodies[0].forceRenderingOff = true;
                    pose.GetType().GetField("Hidden").SetValue(pose, true);
                    yield return null; yield return null;
                    Assert.IsTrue(bodies[0].forceRenderingOff);
                    Call(condition, "SetLostParts", Enum.ToObject(parts, 13)); // both legs + left arm
                    yield return null; yield return null;
                    AssertPursuitBody(model, 3);
                    Assert.IsTrue(bodies[2].forceRenderingOff);
                    Object.Destroy(pose);
                    yield return null; yield return null;
                    AssertPursuitBody(model, 3);
                    Assert.IsFalse(original.forceRenderingOff);
                    Assert.That(model.transform.localPosition.y, Is.EqualTo(standingPosition.y +
                        (float)Field(model, "CrawlHeightOffset")).Within(.001f),
                        "Capture release must preserve the current injury's model height.");
                    Assert.IsFalse(bodies[2].forceRenderingOff, "The remaining body must be drawn again.");
                    Assert.IsTrue(bodies[0].forceRenderingOff, "Keep the pre-existing culling override.");
                    bodies[0].forceRenderingOff = false;
                }
            }
            finally { Object.Destroy(player); }
        }

        [UnityTest]
        public IEnumerator PursuitCapture_OneArmMeshSurvivesPerformanceTransfer()
        {
            yield return SceneManager.LoadSceneAsync("Start");
            yield return IsolatedMenuHost();
            yield return new WaitForSecondsRealtime(.5f);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.IsTrue((bool)Call(launcher, "TryLaunch", Point(2, "Morning", 0, "Tightrope")));
                float until = Time.realtimeSinceStartup + 75;
                while ((bool)Get(launcher, "IsBusy") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Get(launcher, "LastLaunchSucceeded"), (string)Get(launcher, "Status"));
                var run = Static("CurtainCall.Campaign.CampaignRun", "Instance");
                Assert.IsTrue((bool)Call(run, "DebugSetRemaining", .01f));
                var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
                Assert.IsNotNull(director);
                until = Time.realtimeSinceStartup + 5;
                while ((float)Get(director, "Elapsed") < 0 && Time.realtimeSinceStartup < until) yield return null;
                Assert.GreaterOrEqual((float)Get(director, "Elapsed"), 0);
                var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                var actor = ((IEnumerable)Get(director, "Actors")).Cast<object>().First();
                Call(director, "BeginCapture", actor, player.gameObject);
                Assert.IsNotNull(player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose")));
                until = Time.realtimeSinceStartup + 45;
                while (!SceneManager.GetSceneByName("Tightrope").isLoaded && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue(SceneManager.GetSceneByName("Tightrope").isLoaded);
                yield return new WaitForSecondsRealtime(.5f);
                player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                Assert.AreEqual(13, Convert.ToInt32(Get(player, "LostParts")));
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.IsNull(player.GetComponent(T("CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose")));
                var model = (Component)Get(player.GetComponent(T("CurtainCall.Player.HumanoidPlayerPresentation")), "Model");
                AssertPursuitBody(model, 3);
                Assert.IsFalse(((Renderer[])Field(model, "Bodies"))[2].forceRenderingOff);
                var animator = (Animator)Field(model, "Animator");
                Debug.Log($"Capture transfer {attempt}: root={player.transform.position}, model={model.transform.localPosition}, " +
                    $"motion={Get(model, "Motion")}, speed={animator.speed}, transition={animator.IsInTransition(0)}, " +
                    $"hips={animator.GetBoneTransform(HumanBodyBones.Hips).position}");
                Assert.IsFalse(animator.IsInTransition(0),
                    "A stationary injured player must reach the crawl pose without waiting for movement.");
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("ItemCrawl_Cut3"));
                Assert.IsTrue(player.GetComponent<CharacterController>().isGrounded,
                    "The physical player must be on the performance platform.");
                Assert.Less(animator.GetBoneTransform(HumanBodyBones.Hips).position.y - player.transform.position.y, .45f,
                    "The remaining body must be lowered to the platform, not suspended at standing hip height.");
            }
        }
    }
}
