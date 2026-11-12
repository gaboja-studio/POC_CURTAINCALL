using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Tests.HumanoidPresentation
{
    public sealed class HumanoidAssetsTests
    {
        static Type TypeFor(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

        static object Call(Component component, string method, params object[] args) =>
            component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(component, args);

        static void RequestItemMotion(Component presentation, int kind) =>
            presentation.GetType().GetMethod("RequestItemMotion", new[] { typeof(int), typeof(Vector3) })
                .Invoke(presentation, new object[] { kind, Vector3.forward });

        static void WithPresentation(int lostParts, Action<Component, Component, Component, Animator> test)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            Component presentation = null;
            try
            {
                var root = new GameObject("Player presentation");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<CharacterController>();
                var mover = root.AddComponent(TypeFor("CurtainCall.Player.PlayerMover"));
                var condition = root.AddComponent(TypeFor("CurtainCall.Player.PlayerCondition"));
                var slot = root.AddComponent(TypeFor("CurtainCall.Player.PlayerModelSlot"));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab");
                var properties = new SerializedObject(slot);
                properties.FindProperty("modelPrefab").objectReferenceValue = prefab;
                properties.FindProperty("fitToHeight").boolValue = false;
                properties.ApplyModifiedPropertiesWithoutUndo();
                Call(mover, "Awake");
                if (lostParts > 0)
                {
                    var parts = condition.GetType().GetMethod("PartsForLostCount").Invoke(null, new object[] { lostParts });
                    Call(condition, "SetLostParts", parts);
                }
                Call(slot, "Awake");
                presentation = root.AddComponent(TypeFor("CurtainCall.Player.HumanoidPlayerPresentation"));
                Call(presentation, "OnEnable");
                var model = (Component)presentation.GetType().GetProperty("Model").GetValue(presentation);
                var animator = (Animator)model.GetType().GetField("Animator").GetValue(model);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
                test(presentation, mover, model, animator);
            }
            finally
            {
                if (presentation != null) Call(presentation, "OnDisable");
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test, Category("CommonProgressionRegression")]
        public void RelocationClearsThePreviousFallInsteadOfPlayingAnImpactAtSpawn()
        {
            WithPresentation(0, (presentation, mover, model, animator) =>
            {
                Call(mover, "ResetExternalMotion", true);
                Call(presentation, "Update");
                animator.Update(.25f);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Fall"));
                Call(mover, "Relocate", Vector3.right * 3f, Quaternion.identity);
                Call(presentation, "Update");
                animator.Update(.25f);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),
                    (string)model.GetType().GetProperty("Motion").GetValue(model));
            });
        }

        [Test, Category("CommonProgressionRegression")]
        public void StationaryCrawlKeepsUpperBodyInteractionPlaying()
        {
            WithPresentation(2, (presentation, _, _, animator) =>
            {
                Call(presentation, "ReceiveItemMotion", 0, Vector3.forward, true);
                Call(presentation, "Update");
                int layer = animator.GetLayerIndex("ItemRightHand");
                Assert.That(animator.GetLayerWeight(layer), Is.EqualTo(1f));
                animator.Update(0f);
                var crawl = animator.GetCurrentAnimatorStateInfo(0);
                var hands = animator.GetCurrentAnimatorStateInfo(layer);
                Assert.IsTrue(crawl.IsName("ItemCrawl_Cut2"));
                Assert.IsTrue(hands.IsName("ItemStrike_Cut2"));
                animator.Update(.25f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(crawl.normalizedTime).Within(.001f));
                Assert.That(animator.GetCurrentAnimatorStateInfo(layer).normalizedTime, Is.GreaterThan(hands.normalizedTime + .01f));
            });
        }

        [Test, Category("CommonProgressionRegression")]
        public void AToolCanBlockNextInteractionWhileAllowingTheConfiguredJump()
        {
            WithPresentation(0, (presentation, mover, _, _) =>
            {
                var controller = mover.gameObject.AddComponent(TypeFor("CurtainCall.Player.PlayerController"));
                Call(controller, "Awake");
                var reader = mover.GetComponent(TypeFor("CurtainCall.Player.PlayerInputReader"));
                var frame = Activator.CreateInstance(TypeFor("CurtainCall.Player.PlayerInputFrame"));
                frame.GetType().GetField("ActionPressed").SetValue(frame, true);
                reader.GetType().GetProperty("Current").SetValue(reader, frame);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var settings = TypeFor("CurtainCall.Settings.GameSettings").GetProperty("Base").GetValue(null);
                var policies = settings.GetType().GetProperty("Presentation").GetValue(settings);
                var policy = policies.GetType().GetMethod("InteractionPolicy").Invoke(policies, new object[] { 1 });
                var movement = policy.GetType().GetField("allowMovement", fields);
                var previous = movement.GetValue(policy);
                var gate = TypeFor("CurtainCall.Player.InputGate");
                var rearming = gate.GetField("rearming", BindingFlags.Static | BindingFlags.NonPublic);
                var blockedFrame = gate.GetField("blockedFrame", BindingFlags.Static | BindingFlags.NonPublic);
                var blockers = (HashSet<object>)gate.GetField("blockers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var previousBlockers = blockers.ToArray();
                var interceptor = controller.GetType().GetField("InputInterceptor");
                var previousInterceptor = interceptor.GetValue(null);
                var wasRearming = rearming.GetValue(null);
                var previousBlockedFrame = blockedFrame.GetValue(null);
                var jumpRequested = mover.GetType().GetField("jumpRequested", fields);
                try
                {
                    rearming.SetValue(null, false);
                    blockedFrame.SetValue(null, -1);
                    blockers.Clear();
                    interceptor.SetValue(null, null);
                    Call(controller, "Update");
                    Assert.IsTrue((bool)jumpRequested.GetValue(mover), "Fixture accepts ordinary jump input.");
                    Call(mover, "ClearPendingInput");
                    movement.SetValue(policy, true);
                    RequestItemMotion(presentation, 1);
                    Assert.IsTrue((bool)presentation.GetType().GetProperty("InteractionActionLocked").GetValue(presentation));
                    Assert.IsFalse((bool)presentation.GetType().GetProperty("InteractionMovementLocked").GetValue(presentation));
                    Call(controller, "Update");
                    Assert.IsTrue((bool)jumpRequested.GetValue(mover));
                }
                finally
                {
                    movement.SetValue(policy, previous);
                    blockers.UnionWith(previousBlockers);
                    interceptor.SetValue(null, previousInterceptor);
                    rearming.SetValue(null, wasRearming);
                    blockedFrame.SetValue(null, previousBlockedFrame);
                }
            });
        }

        [Test, Category("CommonProgressionRegression")]
        public void CheckpointRelocationClearsUnfinishedToolLocks()
        {
            WithPresentation(0, (presentation, mover, _, _) =>
            {
                RequestItemMotion(presentation, 1);
                Assert.IsTrue((bool)presentation.GetType().GetProperty("InteractionMovementLocked").GetValue(presentation));
                Call(mover, "Relocate", Vector3.right * 3f, Quaternion.identity);
                Assert.IsFalse((bool)presentation.GetType().GetProperty("InteractionMovementLocked").GetValue(presentation));
                Assert.IsNull(presentation.GetType().GetProperty("ItemMotion").GetValue(presentation));
            });
        }

        [Test, Category("CommonProgressionRegression")]
        public void PhysicalCarryCancelsThePreviousToolActionAndHidesItsHandProp()
        {
            WithPresentation(0, (presentation, mover, _, animator) =>
            {
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var player = mover.gameObject.AddComponent(TypeFor("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                Call(player, "Awake");
                var spawned = player.GetType().GetProperty("IsSpawned");
                var carry = TypeFor("CurtainCall.HumanCannon.HumanCannonCarry");
                var deviceObject = new GameObject("Carried device");
                SceneManager.MoveGameObjectToScene(deviceObject, mover.gameObject.scene);
                var device = deviceObject.AddComponent(TypeFor("CurtainCall.HumanCannon.HumanCannonTrampoline"));
                var ownership = device.GetType().GetField("ownership", fields).GetValue(device);
                var handProp = new GameObject("Selected inventory tool");
                handProp.transform.SetParent(presentation.transform, false);
                var visual = presentation.GetType().GetField("heldItemVisual", fields);
                try
                {
                    RequestItemMotion(presentation, 1);
                    Assert.IsTrue((bool)presentation.GetType().GetProperty("InteractionMovementLocked").GetValue(presentation));
                    spawned.SetValue(player, true);
                    player.GetType().GetProperty("IsOwner").SetValue(player, true);
                    player.GetType().GetProperty("OwnerClientId").SetValue(player, 713UL);
                    presentation.GetType().GetField("network", fields).SetValue(presentation, player);
                    visual.SetValue(presentation, handProp);
                    carry.GetMethod("Register").Invoke(null, new object[] { device });
                    ownership.GetType().GetMethod("Receive").Invoke(ownership, new object[] { 1u, 713UL });
                    Call(presentation, "Update");
                    Call(presentation, "ApplyItemAlignment");
                    Assert.IsFalse(handProp.activeSelf);
                    Assert.IsFalse((bool)presentation.GetType().GetProperty("InteractionMovementLocked").GetValue(presentation));
                    Assert.IsNull(presentation.GetType().GetProperty("ItemMotion").GetValue(presentation));
                    Assert.That(animator.GetLayerWeight(1), Is.EqualTo(1f));
                }
                finally
                {
                    spawned.SetValue(player, false);
                    carry.GetMethod("Unregister").Invoke(null, new object[] { device });
                    visual.SetValue(presentation, null);
                }
            });
        }

        [TestCase("walk", 0, "Walk")]
        [TestCase("sprint", 0, "Run")]
        [TestCase("crouch walk", 0, "CrouchWalk")]
        [TestCase("jump", 0, "JumpStart")]
        [TestCase("fall", 0, "Fall")]
        [TestCase("rope", 0, "Tightrope")]
        [TestCase("walk", 2, "ItemCrawl_Cut2")]
        [TestCase("fall", 2, "PanicFall_Cut2")]
        [Category("CommonProgressionRegression")]
        public void MovementAllowedToolActionKeepsTheActualLocomotionPose(string activity, int lostParts, string pose)
        {
            WithPresentation(lostParts, (presentation, mover, _, animator) =>
            {
                bool moving = activity is "walk" or "sprint" or "crouch walk" or "rope";
                mover.GetType().GetProperty("IsMoving").SetValue(mover, moving);
                if (activity == "sprint") mover.GetType().GetProperty("IsSprinting").SetValue(mover, true);
                if (activity == "crouch walk") mover.GetType().GetProperty("IsCrouched").SetValue(mover, true);
                if (activity == "jump") Call(mover, "LaunchJump", 0f);
                if (activity == "fall") Call(mover, "ResetExternalMotion", true);
                if (activity == "rope")
                {
                    var balance = mover.gameObject.AddComponent(TypeFor("CurtainCall.Player.PlayerBalance"));
                    Call(balance, "SetBalanceActive", true);
                    presentation.GetType().GetField("balance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(presentation, balance);
                }
                Call(presentation, "ReceiveItemMotion", 1, Vector3.forward, true);
                Call(presentation, "Update");
                animator.Update(.25f);
                int hands = animator.GetLayerIndex("ItemRightHand");
                Assert.That(animator.GetLayerWeight(hands), Is.EqualTo(1f));
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(pose), pose);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(hands).IsName(lostParts == 0 ? "ItemManipulate" : "ItemManipulate_Cut2"));
            });
        }

        [Test]
        public void ActualPlayerReferencesRabbitWithProductionHumanoidContract()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Characters/Players/Player.prefab");
            var slot = player.GetComponent(TypeFor("CurtainCall.Player.PlayerModelSlot"));
            var properties = new SerializedObject(slot);
            var model = properties.FindProperty("modelPrefab").objectReferenceValue as GameObject;
            Assert.That(AssetDatabase.GetAssetPath(model), Is.EqualTo("Assets/Resources/Prefabs/Characters/Players/Models/Rabbit/Rabbit.prefab"));
            Assert.That(properties.FindProperty("fitToHeight").boolValue, Is.False, "귀를 포함한 bounds로 정수리 신장을 축소하지 않는다.");
            Assert.That(properties.FindProperty("squashWhenCrouched").boolValue, Is.False);
            Assert.That(player.GetComponent(TypeFor("CurtainCall.Player.HumanoidPlayerPresentation")), Is.Not.Null);
            Assert.That(model.GetComponent(TypeFor("CurtainCall.Player.HumanoidModel")), Is.Not.Null);
            Assert.That(model.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null && c.GetType().Namespace == "CurtainCall.Player.Validation"), Is.False);
            var animator = model.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal), Is.Not.Null);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.LeftEye), Is.Null, "토끼 귀를 눈으로 매핑하지 않는다.");
            var presentation = model.GetComponent(TypeFor("CurtainCall.Player.HumanoidModel"));
            var original = (SkinnedMeshRenderer)presentation.GetType().GetField("Original").GetValue(presentation);
            var bodies = (SkinnedMeshRenderer[])presentation.GetType().GetField("Bodies").GetValue(presentation);
            var limbs = (SkinnedMeshRenderer[])presentation.GetType().GetField("Limbs").GetValue(presentation);
            Assert.That(original.sharedMesh.triangles.Length / 3, Is.LessThanOrEqualTo(10000));
            Assert.That(original.sharedMesh.subMeshCount, Is.EqualTo(2));
            Assert.That((int[])presentation.GetType().GetField("PlayerTintMaterialSlots").GetValue(presentation), Is.EqualTo(new[] { 1 }));
            // Updating a native Mesh through CopySerialized can retain its old vertex
            // buffer while replacing indices. Validate all production buffers together.
            foreach (var skin in new[] { original }.Concat(bodies).Concat(limbs))
            {
                var mesh = skin.sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), skin.name);
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount), skin.name);
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), skin.name);
                Assert.That(mesh.boneWeights.Length, Is.EqualTo(mesh.vertexCount), skin.name);
                Assert.That(mesh.triangles.All(index => index >= 0 && index < mesh.vertexCount), Is.True, skin.name);
                Assert.That(mesh.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), Is.True, skin.name);
                foreach (var weight in mesh.boneWeights)
                {
                    Assert.That(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3, Is.EqualTo(1f).Within(.0001f), skin.name);
                    Assert.That(new[] { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 }.All(index => index >= 0 && index < skin.bones.Length), Is.True, skin.name);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                foreach (var skin in new[] { bodies[i], limbs[i] })
                {
                    Assert.That(skin.bones, Is.EqualTo(original.bones));
                    Assert.That(skin.sharedMesh.bindposes, Is.EqualTo(original.sharedMesh.bindposes));
                    Assert.That(skin.sharedMesh.subMeshCount, Is.EqualTo(3 + i));
                    Assert.That(skin.sharedMesh.GetTriangles(2 + i).Length, Is.GreaterThan(0));
                }
            }
        }

        [Test]
        public void ProductionControllerContainsMotionAndCarryLayerContracts()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_ThirdParty/Task062/PlayerPresentation/Player.controller");
            Assert.That(controller.layers.Length, Is.EqualTo(2));
            var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
            foreach (string name in new[] { "Idle", "Walk", "Run", "Crouch", "CrouchWalk", "JumpStart", "JumpLoop", "JumpLand", "Death", "Crawl", "Tightrope", "Flying", "Fall" })
            {
                var clip = states.Single(s => s.name == name).motion as AnimationClip;
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.humanMotion, Is.True, name);
            }
            Assert.That(controller.layers[1].avatarMask, Is.Not.Null);
            Assert.That(controller.layers[1].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
            Assert.That(controller.layers[1].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.True);
            Assert.That(controller.layers[1].stateMachine.defaultState.motion, Is.Not.Null);
        }
    }
}
