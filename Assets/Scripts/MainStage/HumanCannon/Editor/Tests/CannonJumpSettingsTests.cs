using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonJumpSettingsTests
    {
        [Test]
        public void JumpSettings_AreLinked_AndKeepCommonAssetAtPointEight()
        {
            using var common = new SerializedObject(GameSettings.Base);
            using var game = new SerializedObject(GameSettings.Current);
            Assert.That(common.FindProperty("character.jumpHeight").floatValue, Is.EqualTo(0.8f));
            Assert.That(game.FindProperty("humanCannon").objectReferenceValue, Is.SameAs(GameSettings.HumanCannon));
            Assert.That(GameSettings.HumanCannon.JumpHeight, Is.EqualTo(1.2f));
        }

        [Test]
        public void JumpOverride_FollowsActiveSceneAndOwnerLifetime_WithoutStaleRestoration()
        {
            var original = SceneManager.GetActiveScene();
            // EditMode 러너의 저장하지 않은 빈 씬 옆에는 NewScene 대신 저장된 씬을 연다.
            var first = SceneManager.GetSceneByPath(HumanCannonTestSceneBuilder.ScenePath);
            bool openedHere = !first.IsValid() || !first.isLoaded;
            if (openedHere) first = EditorSceneManager.OpenScene(HumanCannonTestSceneBuilder.ScenePath, OpenSceneMode.Additive);
            var second = original;
            var owner = new GameObject("jump-owner").AddComponent<Animator>();
            SceneManager.MoveGameObjectToScene(owner.gameObject, first);
            var character = new BaseGameSettings.CharacterSettings();
            float height = 1.2f;
            using var earlier = CharacterJumpHeightOverrides.Register(owner, () => height);
            using var later = CharacterJumpHeightOverrides.Register(owner, () => 1.6f);
            try
            {
                Assert.That(first, Is.Not.EqualTo(original), "러너의 임시 씬과 콘텐츠 씬은 분리되어야 한다.");
                SceneManager.SetActiveScene(first);
                Assert.That(character.JumpHeight, Is.EqualTo(1.6f));
                earlier.Dispose();
                Assert.That(character.JumpHeight, Is.EqualTo(1.6f));
                later.Dispose();
                Assert.That(character.JumpHeight, Is.EqualTo(0.8f));
                using var live = CharacterJumpHeightOverrides.Register(owner, () => height);
                Assert.That(character.JumpHeight, Is.EqualTo(1.2f));
                height = 1.1f;
                Assert.That(character.JumpHeight, Is.EqualTo(1.1f), "플레이 중 설정 변경을 읽는다.");
                SceneManager.SetActiveScene(second);
                Assert.That(character.JumpHeight, Is.EqualTo(0.8f), "additive로 남은 인간대포가 다른 공간에 영향을 주지 않는다.");
                SceneManager.SetActiveScene(first);
                owner.enabled = false;
                Assert.That(character.JumpHeight, Is.EqualTo(0.8f));
                owner.enabled = true;
                Assert.That(character.JumpHeight, Is.EqualTo(1.1f));
                Object.DestroyImmediate(owner.gameObject);
                Assert.That(character.JumpHeight, Is.EqualTo(0.8f));
            }
            finally
            {
                if (owner != null) Object.DestroyImmediate(owner.gameObject);
                SceneManager.SetActiveScene(original);
                if (openedHere) EditorSceneManager.CloseScene(first, true);
            }
        }
    }
}
