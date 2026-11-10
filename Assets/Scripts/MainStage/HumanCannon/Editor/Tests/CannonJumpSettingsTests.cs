using CurtainCall.Settings;
using CurtainCall.Player;
using NUnit.Framework;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonJumpSettingsTests
    {
        [Test]
        public void FloorAndRopeUseIndependentCommonSettingsAcrossActiveScenes()
        {
            var original = SceneManager.GetActiveScene();
            var cannon = SceneManager.GetSceneByPath(HumanCannonTestSceneBuilder.ScenePath);
            bool opened = !cannon.IsValid() || !cannon.isLoaded;
            if (opened) cannon = EditorSceneManager.OpenScene(HumanCannonTestSceneBuilder.ScenePath, OpenSceneMode.Additive);
            var go = new GameObject("Common jump fixture");
            var mover = go.AddComponent<PlayerMover>();
            typeof(PlayerMover).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mover, null);
            var character = GameSettings.Base.Character;
            var field = character.GetType().GetField("jumpHeight", BindingFlags.Instance | BindingFlags.NonPublic);
            float saved = character.JumpHeight;
            try
            {
                foreach (var scene in new[] { cannon, original, cannon })
                {
                    SceneManager.SetActiveScene(scene);
                    foreach (float height in new[] { .8f, .95f })
                    {
                        field.SetValue(character, height);
                        mover.FreeMovement = true;
                        mover.CourseJumpHeightOverride = 99;
                        mover.CourseJumpGravityMultiplier = 3;
                        Assert.That(mover.JumpSpeed * mover.JumpSpeed / (2 * mover.JumpGravity), Is.EqualTo(height).Within(.0001));
                        Assert.That(GameSettings.Tightrope.RopeMovement.JumpHeight, Is.EqualTo(1.5f));
                        mover.FreeMovement = false;
                        mover.CourseJumpHeightOverride = GameSettings.Tightrope.RopeMovement.JumpHeight;
                        Assert.That(mover.JumpSpeed * mover.JumpSpeed / (2 * mover.JumpGravity), Is.EqualTo(1.5f).Within(.0001));
                    }
                }
            }
            finally
            {
                field.SetValue(character, saved);
                Object.DestroyImmediate(go);
                SceneManager.SetActiveScene(original);
                if (opened) EditorSceneManager.CloseScene(cannon, true);
            }
        }
    }
}
