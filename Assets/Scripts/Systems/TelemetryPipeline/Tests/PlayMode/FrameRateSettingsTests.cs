using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.Telemetry.Pipeline.PlayTests
{
    public sealed class FrameRateSettingsTests
    {
        [UnityTest]
        public IEnumerator LimitChangesAndDisablingSurviveSceneChanges()
        {
#if UNITY_EDITOR
            var settings = Resources.Load<ScriptableObject>("Settings/GameSettings/BaseGameSettings");
            Assert.That(settings, Is.Not.Null);
            bool wasDirty = UnityEditor.EditorUtility.IsDirty(settings);
            var serialized = new UnityEditor.SerializedObject(settings);
            var enabled = serialized.FindProperty("frameRate.limitEnabled");
            var maximum = serialized.FindProperty("frameRate.maxFps");
            bool previousEnabled = enabled.boolValue;
            int previousMax = maximum.intValue;
            var type = Type.GetType("CurtainCall.Settings.FrameRateController, Assembly-CSharp", true);
            var instance = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            Scene temporary = default;
            try
            {
                yield return null;
                var controller = instance.GetValue(null);
                Assert.That(controller, Is.Not.Null, "Runtime bootstrap must create the controller.");
                enabled.boolValue = true;
                maximum.intValue = 120;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                yield return null;
                yield return null;
                Assert.That(Application.targetFrameRate, Is.EqualTo(120));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);

                enabled.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                yield return null;
                yield return null;
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);

                enabled.boolValue = true;
                maximum.intValue = 60;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                temporary = SceneManager.CreateScene("FrameRateSettingsTest");
                yield return SceneManager.UnloadSceneAsync(temporary);
                yield return null;
                Assert.That(instance.GetValue(null), Is.SameAs(controller));
                Assert.That(Application.targetFrameRate, Is.EqualTo(60));

                maximum.intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                yield return null;
                yield return null;
                Assert.That(Application.targetFrameRate, Is.EqualTo(1), "Invalid FPS must be clamped.");
            }
            finally
            {
                enabled.boolValue = previousEnabled;
                maximum.intValue = previousMax;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!wasDirty) UnityEditor.EditorUtility.ClearDirty(settings);
                if (temporary.IsValid() && temporary.isLoaded) SceneManager.UnloadSceneAsync(temporary);
            }
#else
            Assert.Ignore("Editor setting-change regression test.");
            yield break;
#endif
        }
    }
}
