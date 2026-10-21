using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.Telemetry.Pipeline.PlayTests
{
    public sealed class SurveyBootstrapTests
    {
        string logsKey, surveysKey;
        bool previousLogs, previousSurveys;
        Type surveyType, logType;
        static Type Game(string name) => Type.GetType("CurtainCall.Telemetry.Pipeline." + name + ", Assembly-CSharp", true);
        [UnityTest] public IEnumerator SurveyOnlyBuildBootstrapsAndBindsMenuWithoutCreatingLogCollector()
        {
#if UNITY_EDITOR
            var tags = Game("PlaytestBuildTags");
            logsKey = (string)tags.GetMethod("PreferenceKey").Invoke(null, new object[] { "LOGS" });
            surveysKey = (string)tags.GetMethod("PreferenceKey").Invoke(null, new object[] { "SURVEY_010" });
            previousLogs = UnityEditor.EditorPrefs.GetBool(logsKey, false); previousSurveys = UnityEditor.EditorPrefs.GetBool(surveysKey, false);
            UnityEditor.EditorPrefs.SetBool(logsKey, false); UnityEditor.EditorPrefs.SetBool(surveysKey, true);
            surveyType = Game("PlaytestSurveyRuntime"); logType = Game("TelemetryRuntime");
            var prior = UnityEngine.Object.FindAnyObjectByType(logType); if (prior != null) UnityEngine.Object.Destroy(prior);
            yield return null;
            yield return SceneManager.LoadSceneAsync("Start", LoadSceneMode.Single);
            float until = Time.realtimeSinceStartup + 8;
            Component survey = null;
            while (Time.realtimeSinceStartup < until)
            {
                survey = UnityEngine.Object.FindAnyObjectByType(surveyType) as Component;
                if (survey != null && surveyType.GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(survey) != null) break;
                yield return null;
            }
            Assert.That(survey, Is.Not.Null);
            Assert.That(surveyType.GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(survey), Is.Not.Null);
            Assert.That(UnityEngine.Object.FindAnyObjectByType(logType), Is.Null);
            Assert.That(surveyType.GetField("pendingCode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(survey), Is.Null);
#else
            Assert.Ignore("Editor-only tag setup; PC browser opening is checked by a person.");
            yield break;
#endif
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
#if UNITY_EDITOR
            if (logsKey != null) UnityEditor.EditorPrefs.SetBool(logsKey, previousLogs);
            if (surveysKey != null) UnityEditor.EditorPrefs.SetBool(surveysKey, previousSurveys);
            if (surveyType != null) { var item = UnityEngine.Object.FindAnyObjectByType(surveyType); if (item != null) UnityEngine.Object.Destroy(item); }
#endif
            yield return null;
        }
    }
}
