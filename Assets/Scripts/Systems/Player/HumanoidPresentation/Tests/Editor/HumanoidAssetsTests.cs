using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CurtainCall.Tests.HumanoidPresentation
{
    public sealed class HumanoidAssetsTests
    {
        static Type TypeFor(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

        [Test]
        public void ActualPlayerReferencesPrivateModelWithoutValidationComponents()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Characters/Players/Player.prefab");
            var slot = player.GetComponent(TypeFor("CurtainCall.Player.PlayerModelSlot"));
            var properties = new SerializedObject(slot);
            var model = properties.FindProperty("modelPrefab").objectReferenceValue as GameObject;
            Assert.That(AssetDatabase.GetAssetPath(model), Does.StartWith("Assets/_ThirdParty/Task062/PlayerPresentation/"));
            Assert.That(properties.FindProperty("squashWhenCrouched").boolValue, Is.False);
            Assert.That(player.GetComponent(TypeFor("CurtainCall.Player.HumanoidPlayerPresentation")), Is.Not.Null);
            Assert.That(model.GetComponent(TypeFor("CurtainCall.Player.HumanoidModel")), Is.Not.Null);
            Assert.That(model.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null && c.GetType().Namespace == "CurtainCall.Player.Validation"), Is.False);
            var animator = model.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
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
