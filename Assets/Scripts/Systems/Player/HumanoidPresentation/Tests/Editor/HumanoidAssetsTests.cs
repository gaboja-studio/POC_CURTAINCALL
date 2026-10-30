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
