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
#if UNITY_EDITOR
        string previousVersion;
        [UnitySetUp] public IEnumerator UseTheSurveyVersionForLegacyRegression()
        {
            previousVersion = UnityEditor.PlayerSettings.bundleVersion;
            UnityEditor.PlayerSettings.bundleVersion = "0.1.0";
            yield return null;
        }
#endif
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
        [UnityTest] public IEnumerator HostDisconnectKeepsEachParticipantRunAndQueuesExitWithoutMenuClick()
        {
            yield return SurveyOnlyBuildBootstrapsAndBindsMenuWithoutCreatingLogCollector();
            var survey = (MonoBehaviour)UnityEngine.Object.FindAnyObjectByType(surveyType);
            // Inspect the deferred launch without opening a real browser or changing the user's cache.
            survey.enabled = false;
            var sessionType = Type.GetType("CurtainCall.Network.Session.NetworkSessionManager, Assembly-CSharp", true);
            var session = sessionType.GetProperty("Instance").GetValue(null);
            Assert.That(session, Is.Not.Null);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var connectionType = sessionType.GetProperty("ConnectionState").PropertyType;
            string run = "7a9e2d31-8f5b-4cde-9201-1bfa8294a732";
            for (int slot = 1; slot <= 3; slot++)
            {
                sessionType.GetMethod("SetState", flags).Invoke(session, new[] { Enum.Parse(connectionType, "Client") });
                surveyType.GetField("currentRun", flags).SetValue(survey, run);
                surveyType.GetField("currentSlot", flags).SetValue(survey, slot);
                surveyType.GetField("currentIdentity", flags).SetValue(survey, "installation");
                surveyType.GetField("pendingCode", flags).SetValue(survey, null);
                // The participant has already despawned, but the session is still stopping.
                surveyType.GetMethod("Update", flags).Invoke(survey, null);
                Assert.That(surveyType.GetField("currentRun", flags).GetValue(survey), Is.EqualTo(run));
                sessionType.GetMethod("HandleStopped", flags).Invoke(session, new object[] { false });
                string code = (string)surveyType.GetField("pendingCode", flags).GetValue(survey);
                Assert.That(code, Does.StartWith("CCS1.0_1_0.TEST.EXIT." + run.Replace("-", "") + "." + slot + "."));
                Assert.That(surveyType.GetField("pendingIdentity", flags).GetValue(survey), Is.EqualTo("installation"));
                Assert.That(surveyType.GetField("currentRun", flags).GetValue(survey), Is.Null);
                // NGO stop and the menu can both report the same exit; keep one invitation.
                sessionType.GetMethod("HandleStopped", flags).Invoke(session, new object[] { false });
                surveyType.GetMethod("LeaveConfirmed", flags).Invoke(survey, null);
                Assert.That(surveyType.GetField("pendingCode", flags).GetValue(survey), Is.EqualTo(code));
            }
        }
        [UnityTest] public IEnumerator M2DoesNotCreateM1SurveyEvenWhenSurveyTagIsOn()
        {
#if UNITY_EDITOR
            UnityEditor.PlayerSettings.bundleVersion = "0.2.0";
            var tags = Game("PlaytestBuildTags");
            logsKey = (string)tags.GetMethod("PreferenceKey").Invoke(null, new object[] { "LOGS" });
            surveysKey = (string)tags.GetMethod("PreferenceKey").Invoke(null, new object[] { "SURVEY_010" });
            previousLogs = UnityEditor.EditorPrefs.GetBool(logsKey, false); previousSurveys = UnityEditor.EditorPrefs.GetBool(surveysKey, false);
            UnityEditor.EditorPrefs.SetBool(logsKey, false); UnityEditor.EditorPrefs.SetBool(surveysKey, true);
            surveyType = Game("PlaytestSurveyRuntime");
            yield return SceneManager.LoadSceneAsync("Start", LoadSceneMode.Single); yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType(surveyType), Is.Null);
#else
            Assert.Ignore("Editor-only tag setup."); yield break;
#endif
        }
        [UnityTest] public IEnumerator DisconnectWithoutPlayingNeverQueuesAnExitSurvey()
        {
            yield return SurveyOnlyBuildBootstrapsAndBindsMenuWithoutCreatingLogCollector();
            var survey = (MonoBehaviour)UnityEngine.Object.FindAnyObjectByType(surveyType);
            survey.enabled = false;
            var sessionType = Type.GetType("CurtainCall.Network.Session.NetworkSessionManager, Assembly-CSharp", true);
            var session = sessionType.GetProperty("Instance").GetValue(null);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var connectionType = sessionType.GetProperty("ConnectionState").PropertyType;
            sessionType.GetMethod("SetState", flags).Invoke(session, new[] { Enum.Parse(connectionType, "Client") });
            surveyType.GetMethod("Update", flags).Invoke(survey, null);
            sessionType.GetMethod("HandleStopped", flags).Invoke(session, new object[] { false });
            Assert.That(surveyType.GetField("pendingCode", flags).GetValue(survey), Is.Null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
#if UNITY_EDITOR
            if (previousVersion != null) UnityEditor.PlayerSettings.bundleVersion = previousVersion;
            if (logsKey != null) UnityEditor.EditorPrefs.SetBool(logsKey, previousLogs);
            if (surveysKey != null) UnityEditor.EditorPrefs.SetBool(surveysKey, previousSurveys);
            if (surveyType != null) { var item = UnityEngine.Object.FindAnyObjectByType(surveyType); if (item != null) UnityEngine.Object.Destroy(item); }
#endif
            yield return null;
        }
    }
}
