using System;
using System.Linq;
using CurtainCall.Damage;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.Settings.EditorTools.Tests
{
    public sealed class CommonRulesFollowupTests
    {
        class ReachTarget : CurtainCall.WaitingRoom.IShopTarget
        {
            public float Distance;
            public float Reach(Vector3 feet, Component actor = null) => Distance;
            public void Interact() { }
        }

        sealed class BareHandReachTarget : ReachTarget, CurtainCall.WaitingRoom.IBareHandTarget
        {
            public bool AllowsBareHands { get; set; }
            public string TargetId => "bare-hand-selection-regression";
            public string ToolId => "missing-bare-hand-selection-tool";
            public string ToolName => ToolId;
            public string Label => TargetId;
            public int BareHits => 5;
            public int ToolHits => 1;
            public string RewardItemId => "";
            public Vector3 RewardPoint => Vector3.zero;
            public int RewardUidOffset => 0;
            public bool Resolve(int attemptNumber, System.Random rng) => true;
            public string Describe(bool success, int attemptNumber) => "";
            public string DescribeStrike(bool opened, int done, int total, bool withTool) => "";
        }

        [TestCase(true)]
        [TestCase(false)]
        [Category("BareHandInteractionSelection")]
        public void BareHandPolicyControlsFSelectionWithoutBypassingSingleConsumer(bool allowsBareHands)
        {
            var actor = new GameObject("Bare-hand F arbitration actor");
            actor.transform.position = new Vector3(4000, 100, 4000);
            var toolTarget = new BareHandReachTarget { Distance = .2f, AllowsBareHands = allowsBareHands };
            var fallback = new ReachTarget { Distance = .4f };
            bool locked = CurtainCall.WaitingRoom.ShopReach.Locked;
            try
            {
                CurtainCall.WaitingRoom.ShopReach.Locked = false;
                CurtainCall.WaitingRoom.ShopReach.Add(toolTarget);
                CurtainCall.WaitingRoom.ShopReach.Add(fallback);
                var reader = actor.AddComponent<PlayerInputReader>();
                typeof(InputGate).GetMethod("Reset", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
                typeof(PlayerInputReader).GetProperty("Current").SetValue(reader,
                    new PlayerInputFrame { InteractHeld = true, InteractTapped = true });
                Assert.IsFalse(CurtainCall.WaitingRoom.ExplorationTargets.LocalHolds(toolTarget.ToolId));
                object selected = allowsBareHands ? toolTarget : fallback;
                object rejected = allowsBareHands ? fallback : toolTarget;
                Assert.AreSame(selected, CommonInteractionSelection.Selected(reader),
                    "맨손 허용 대상만 도구 없이 선택하고, 도구가 필수인 대상은 제외한다.");
                Assert.IsFalse(CommonInteractionSelection.Consume(reader, rejected));
                Assert.IsTrue(CommonInteractionSelection.Consume(reader, selected));
                Assert.IsFalse(CommonInteractionSelection.Consume(reader, selected));
                reader.ClearPendingInput();
                Assert.IsFalse(CommonInteractionSelection.Consume(reader, toolTarget));
                Assert.IsFalse(CommonInteractionSelection.Consume(reader, fallback));
            }
            finally
            {
                CurtainCall.WaitingRoom.ShopReach.Remove(toolTarget);
                CurtainCall.WaitingRoom.ShopReach.Remove(fallback);
                CurtainCall.WaitingRoom.ShopReach.Locked = locked;
                Object.DestroyImmediate(actor);
                typeof(InputGate).GetMethod("Reset", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            }
        }

        [Test]
        public void OneGestureChoosesOneTargetAndClearingInputDoesNotGiveItToAnotherConsumer()
        {
            var actor = new GameObject("F arbitration actor");
            actor.transform.position = new Vector3(4000, 100, 4000);
            var a = new ReachTarget { Distance = .4f };
            var b = new ReachTarget { Distance = .2f };
            bool locked = CurtainCall.WaitingRoom.ShopReach.Locked;
            try
            {
                CurtainCall.WaitingRoom.ShopReach.Locked = false;
                CurtainCall.WaitingRoom.ShopReach.Add(a); CurtainCall.WaitingRoom.ShopReach.Add(b);
                var reader = actor.AddComponent<PlayerInputReader>();
                typeof(InputGate).GetMethod("Reset", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
                typeof(PlayerInputReader).GetProperty("Current").SetValue(reader, new PlayerInputFrame { InteractHeld = true });
                Assert.AreSame(b, CommonInteractionSelection.Selected(reader));
                a.Distance = .01f;
                Assert.IsFalse(CommonInteractionSelection.Consume(reader, a));
                Assert.IsTrue(CommonInteractionSelection.Consume(reader, b));
                reader.ClearPendingInput();
                Assert.IsFalse(CommonInteractionSelection.Consume(reader, a));
                Assert.IsFalse(CommonInteractionSelection.Consume(reader, b));
            }
            finally
            {
                CurtainCall.WaitingRoom.ShopReach.Remove(a); CurtainCall.WaitingRoom.ShopReach.Remove(b);
                CurtainCall.WaitingRoom.ShopReach.Locked = locked;
                Object.DestroyImmediate(actor);
                typeof(InputGate).GetMethod("Reset", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            }
        }
        [TestCase(4.999f, FallOutcome.Safe)]
        [TestCase(5f, FallOutcome.Injured)]
        [TestCase(10f, FallOutcome.Injured)]
        [TestCase(14.999f, FallOutcome.Injured)]
        [TestCase(15f, FallOutcome.Dead)]
        [TestCase(50f, FallOutcome.Dead)]
        [Category("TutorialCommonRules")]
        public void FallThresholdsIncludeTheirLowerBoundary(float height, FallOutcome expected)
            => Assert.That(CommonDamageRules.Fall(height), Is.EqualTo(expected));

        [TestCase(.999, false)] [TestCase(1, true)] [TestCase(1.001, true)]
        public void EveryCutHazardUsesTheSawOneSecondWindow(double delta, bool expected)
            => Assert.That(CommonDamageRules.CanHit(10 + delta, 10, GameSettings.Base.BodyDamage.HazardHitCooldown), Is.EqualTo(expected));

        [TestCase(HazardDamageKind.OrdinaryFire, false)]
        [TestCase(HazardDamageKind.FatalFire, true)]
        [TestCase(HazardDamageKind.Crushing, true)]
        [TestCase(HazardDamageKind.CutLimb, false)]
        public void FireTypesHaveExplicitDamage(HazardDamageKind kind, bool fatal)
            => Assert.That(CommonDamageRules.IsFatal(kind), Is.EqualTo(fatal));

        [Test]
        public void CommonReachUsesThreeDimensionsAndBlocksWallsFloorsAndSolidStartingOverlap()
        {
            var actor = new GameObject("reach actor");
            var wall = new GameObject("blocking wall");
            try
            {
                actor.transform.position = new Vector3(3000, 100, 3000);
                Vector3 center = PlayerInteraction.Center(actor.transform);
                var box = wall.AddComponent<BoxCollider>(); box.size = new Vector3(.1f, 3, 3);
                wall.transform.position = center + Vector3.right;
                Physics.SyncTransforms();
                Assert.IsFalse(PlayerInteraction.InRange(actor.transform, center + Vector3.right * 2, 2.5f));
                Assert.IsTrue(PlayerInteraction.InRange(actor.transform, wall.transform.position, 2.5f, wall.transform), "대상 자체는 가림에서 제외");
                box.isTrigger = true; Physics.SyncTransforms();
                Assert.IsTrue(PlayerInteraction.InRange(actor.transform, center + Vector3.right * 2.5f, 2.5f));
                Assert.IsFalse(PlayerInteraction.InRange(actor.transform, center + new Vector3(2.5f, .1f, 0), 2.5f));
                box.isTrigger = false; box.size = new Vector3(5, .1f, 5); wall.transform.position = center + Vector3.up;
                Physics.SyncTransforms();
                Assert.IsFalse(PlayerInteraction.InRange(actor.transform, center + Vector3.up * 2, 2.5f));
                wall.transform.position = center; Physics.SyncTransforms();
                Assert.IsFalse(PlayerInteraction.ClearPath(actor.transform, center + Vector3.forward));
            }
            finally { Object.DestroyImmediate(wall); Object.DestroyImmediate(actor); }
        }

        [Test]
        public void InteractionPriorityIsIndependentOfProviderOrderAndDistanceBreaksTies()
        {
            var near = new CommonInteractionSelection.Candidate(new object(), 2, .1f, "z");
            var rescue = new CommonInteractionSelection.Candidate(new object(), 1, 2, "r");
            var held = new CommonInteractionSelection.Candidate(new object(), 0, 3, "h");
            var closer = new CommonInteractionSelection.Candidate(new object(), 2, .05f, "c");
            var tied = new CommonInteractionSelection.Candidate(new object(), 2, .1f, "a");
            Assert.IsTrue(CommonInteractionSelection.Better(rescue, near));
            Assert.IsTrue(CommonInteractionSelection.Better(held, rescue));
            Assert.IsFalse(CommonInteractionSelection.Better(near, held));
            Assert.IsTrue(CommonInteractionSelection.Better(closer, near));
            Assert.IsTrue(CommonInteractionSelection.Better(tied, near));
            Assert.IsFalse(CommonInteractionSelection.Better(near, tied));
        }

        [Test]
        public void MissingSharedAssetLinkFailsValidationWithoutFallingBack()
        {
            var copy = Object.Instantiate(GameSettings.Current);
            try
            {
                var data = new SerializedObject(copy);
                data.FindProperty("poles").objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(CommonSettingsValidation.Validate(copy, false, false), Has.Some.Contains("poles"));
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void RopeRescueDistanceChangesWithCommonInteractionSettings()
        {
            string saved = JsonUtility.ToJson(GameSettings.Base);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"interaction\":{\"distance\":2.2}}", GameSettings.Base);
                Assert.That(GameSettings.Tightrope.Hang.RescueDistance, Is.EqualTo(2.2f));
                JsonUtility.FromJsonOverwrite("{\"interaction\":{\"distance\":3.1}}", GameSettings.Base);
                Assert.That(GameSettings.Tightrope.Hang.RescueDistance, Is.EqualTo(3.1f));
            }
            finally { JsonUtility.FromJsonOverwrite(saved, GameSettings.Base); }
        }

        [Test]
        public void DevicesInDifferentScenesAndRecreationReadLiveSharedSettings()
        {
            var first = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var second = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            string saved = JsonUtility.ToJson(GameSettings.Poles);
            try
            {
                foreach (var scene in new[] { first, second, first })
                {
                    var root = new GameObject("recreated pole"); root.SetActive(false);
                    SceneManager.MoveGameObjectToScene(root, scene);
                    var pole = root.AddComponent<HumanCannonPole>();
                    JsonUtility.FromJsonOverwrite("{\"holdSeconds\":17}", GameSettings.Poles);
                    Assert.That(pole.HoldSeconds, Is.EqualTo(17));
                    JsonUtility.FromJsonOverwrite("{\"holdSeconds\":30}", GameSettings.Poles);
                    Assert.That(pole.HoldSeconds, Is.EqualTo(30));
                    Object.DestroyImmediate(root);
                }
                foreach (var rule in CommonSettingsValidation.Retired)
                    foreach (string field in rule.fields.Split(' '))
                        Assert.IsNull(rule.type.GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public), rule.type.Name + "." + field);
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(saved, GameSettings.Poles);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(first);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(second);
            }
        }
    }
}
