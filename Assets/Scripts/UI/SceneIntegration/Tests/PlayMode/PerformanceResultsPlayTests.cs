using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator StartLobbyCurtain_KeepsLobbyCameraAndUi_WithAdditiveUi()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Start");
            yield return new WaitForSecondsRealtime(.5f);
            var session = Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance");
            var host = (System.Threading.Tasks.Task<bool>)Call(session, "HostLanAsync", (ushort)17791);
            while (!host.IsCompleted) yield return null;
            Assert.IsTrue(host.Result);
            var flow = Find("CurtainCall.StartLobby.StartLobbyFlow");
            Assert.IsTrue((bool)Call(flow, "OpenLobbyAsHost"));
            float until = Time.realtimeSinceStartup + 20;
            while ((!UnityEngine.SceneManagement.SceneManager.GetSceneByName("Lobby").isLoaded
                || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"))
                && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetSceneByName("Lobby").isLoaded);
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetSceneByName("CommonInteractionUI").isLoaded);
            Assert.IsFalse((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"));
            Assert.IsTrue(Camera.allCameras.Any(c => c.gameObject.scene.name == "Lobby"), "Lobby must keep rendering with additive UI loaded.");
            var lobbyHud = Find("CurtainCall.UI.SceneIntegration.LobbyHudBinding");
            Assert.IsNotNull(lobbyHud, "현재 로비 HUD 바인딩이 남아 있어야 합니다.");
            Assert.IsTrue(lobbyHud.gameObject.activeInHierarchy);
            var lobbyInfo = Field(lobbyHud, "info");
            Assert.AreEqual(1f, ((CanvasGroup)Private(lobbyInfo, "visibility")).alpha, "커튼 전환 후 로비 HUD가 보여야 합니다.");
            Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"));
            var room = Find("CurtainCall.StartLobby.LobbyRoom");
            var board = Find("CurtainCall.StartLobby.LobbyReadyBoard");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var interaction = Find("CurtainCall.UI.SceneIntegration.CommonInteractionUI");
            CursorTestKey(null);
            Call(mover, "SetStartPose", board.transform.position + new Vector3(0, .1f, -1), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(board, Get(room, "FocusedInteraction"));
            Assert.IsTrue((bool)Get(Get(interaction, "Prompt"), "IsVisible"), "C6 prompt must accompany the lobby F target.");
            for (int i = 0; i < 3; i++)
            {
                CursorTestKey("F"); yield return null; yield return null;
                CursorTestKey(null); yield return null; yield return null;
                Assert.AreEqual(i % 2 == 0, Get(board, "LocalReady"), "F must toggle the actual ready state exactly once.");
            }
            ScreenCapture.CaptureScreenshot(".harness/local/start-lobby-curtain.png");
            yield return null;
            var lever = Find("CurtainCall.StartLobby.LobbyCurtainLever");
            Call(mover, "SetStartPose", lever.transform.position + new Vector3(0, .1f, -1), Quaternion.identity);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(lever, Get(room, "FocusedInteraction"));
            Assert.IsTrue((bool)Get(Get(interaction, "Prompt"), "IsVisible"));
            CursorTestKey("F"); yield return null; yield return null;
            CursorTestKey(null); yield return null; yield return null;
            Assert.AreEqual("Intro", Field(State, "Phase").ToString(), "Ready host must start the campaign with F at the lever.");
            Call(flow, "LeaveToStart");
            until = Time.realtimeSinceStartup + 20;
            while ((!UnityEngine.SceneManagement.SceneManager.GetSceneByName("Start").isLoaded
                || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"))
                && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetSceneByName("Start").isLoaded);
            Assert.IsFalse((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"));
            Assert.IsTrue(Camera.allCameras.Any(c => c.gameObject.scene.name == "Start"));
        }

        [UnityTest]
        public IEnumerator ExplorationWarnings_UseTopBannerAboveAllUi()
        {
            yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
            var ui = Find("CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI");
            var notifications = Private(ui, "notifications");
            var banner = (Component)Get(notifications, "Banner");
            var captions = Private(ui, "captions");
            var panel = (RectTransform)banner.transform;
            var position = panel.anchoredPosition;
            Call(captions, "HideImmediate");
            // 표시 경로만 합성한다. 각 초의 실제 발생 시점은 변경하지 않는다.
            foreach (int seconds in new[] { 60, 30, 20, 10 })
            {
                var announcement = Activator.CreateInstance(T("CurtainCall.WaitingRoom.ExplorationAnnouncement"),
                    seconds, seconds == 60, $"공연 {seconds}초 전입니다");
                Call(ui, "PresentAnnouncement", announcement);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.IsTrue((bool)Get(banner, "IsVisible"));
                Assert.IsFalse((bool)Get(captions, "IsVisible"), "탐색 경고가 하단 자막에 중복 표시되면 안 됩니다.");
                Assert.AreEqual(1f, panel.anchorMin.y);
                Assert.AreEqual(1f, panel.anchorMax.y);
                Assert.AreEqual(position, panel.anchoredPosition, "기존 상단 알림 위치를 유지해야 합니다.");
                var canvas = banner.GetComponent<Canvas>();
                Assert.IsTrue(canvas.overrideSorting);
                Assert.AreEqual(short.MaxValue, canvas.sortingOrder);
                foreach (var layer in SortingLayer.layers)
                    Assert.GreaterOrEqual(SortingLayer.GetLayerValueFromID(canvas.sortingLayerID),
                        SortingLayer.GetLayerValueFromID(layer.id));
            }
        }

        [UnityTest]
        public IEnumerator CurtainBarrier_HoldsThroughPresentationReset_AndRequiresEveryAcknowledgement()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            var transition = T("CurtainCall.UI.SceneIntegration.SceneCurtainTransition");
            yield return (IEnumerator)transition.GetMethod("Cover").Invoke(null, null);
            try
            {
                var coordinator = Find(transition.FullName);
                var ending = Find("CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation");
                var curtain = Get(ending, "Curtain");
                Assert.IsTrue((bool)Static(transition.FullName, "CanTransition"));
                Assert.IsTrue((bool)Get(curtain, "IsFullyCovered"));
                Call(ending, "ResetPresentation");
                yield return new WaitForSecondsRealtime(.6f);
                Assert.IsTrue((bool)Get(curtain, "IsFullyCovered"), "결과 표시 초기화가 전환 커튼을 열면 안 됩니다.");
                // 응답 대기 상태만 합성한다. 실제 두 PC의 메시지 왕복 검증은 별도로 필요하다.
                var waiting = (System.Collections.Generic.HashSet<ulong>)Private(coordinator, "waiting");
                waiting.Add(123);
                Assert.IsFalse((bool)Static(transition.FullName, "CanTransition"));
                waiting.Clear();
                Assert.IsTrue((bool)Static(transition.FullName, "CanTransition"));
                var loading = (System.Collections.Generic.HashSet<string>)Private(coordinator, "loadingScenes");
                loading.Add("DelayedScene");
                try
                {
                    transition.GetMethod("Complete").Invoke(null, null);
                    yield return new WaitForSecondsRealtime(.6f);
                    Assert.IsTrue((bool)Get(curtain, "IsFullyCovered"), "로컬 NGO 로드가 끝나기 전에는 열림 통지가 와도 닫혀 있어야 합니다.");
                }
                finally { loading.Clear(); }
            }
            finally { transition.GetMethod("Complete").Invoke(null, null); }
            float until = Time.realtimeSinceStartup + 10;
            while ((bool)Static(transition.FullName, "IsActive") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse((bool)Static(transition.FullName, "IsActive"));
        }

        [UnityTest]
        public IEnumerator CurtainBarrier_NewCoverCancelsPreviousReveal()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            var transition = T("CurtainCall.UI.SceneIntegration.SceneCurtainTransition");
            yield return (IEnumerator)transition.GetMethod("Cover").Invoke(null, null);
            uint previous = (uint)Private(Find(transition.FullName), "sequence");
            transition.GetMethod("Complete").Invoke(null, null);
            yield return (IEnumerator)transition.GetMethod("Cover").Invoke(null, null);
            try
            {
                Assert.IsFalse((bool)transition.GetMethod("CanTransitionFor").Invoke(null, new object[] { previous }));
                transition.GetMethod("CompleteFor").Invoke(null, new object[] { previous });
                yield return new WaitForSecondsRealtime(.6f);
                var ending = Find("CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation");
                Assert.IsTrue((bool)Static(transition.FullName, "CanTransition"));
                Assert.IsTrue((bool)Get(Get(ending, "Curtain"), "IsFullyCovered"), "이전 전환의 열림이 새 전환을 노출하면 안 됩니다.");
            }
            finally { transition.GetMethod("Complete").Invoke(null, null); }
            float until = Time.realtimeSinceStartup + 10;
            while ((bool)Static(transition.FullName, "IsActive") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse((bool)Static(transition.FullName, "IsActive"));
        }

        [UnityTest]
        public IEnumerator C12_BothActualPerformances_RecordAndDisplay()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            foreach (string trick in new[] { "Tightrope", "HumanCannon" })
            {
                yield return Launch(2, "Performance", 0, trick, trick);
                Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetSceneByName("performance_background").isLoaded,
                    "Stage background must be ready when the curtain opens.");
                var rewards = Find("CurtainCall.Reward.PerformanceRewards");
                float until = Time.realtimeSinceStartup + 10;
                while ((Get(rewards, "State") == null || !(bool)Get(Get(rewards, "State"), "IsOpen")) && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Get(Get(rewards, "State"), "IsOpen"));
                var binding = Find("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding");
                yield return new WaitForSecondsRealtime(.2f);
                var common = Find("CurtainCall.UI.SceneIntegration.CommonPerformanceUI");
                var feedback = Get(common, "Feedback");
                Assert.IsNotNull(feedback);
                var feedbackView = Get(feedback, "View");
                var participant = ((IEnumerable)Get(Get(rewards, "State"), "Segments")).Cast<object>().First();
                string participantId = (string)Get(participant, "ParticipantId");
                Assert.IsTrue((bool)Call(rewards, "AddAudienceReaction", participantId, 20));
                yield return new WaitForSecondsRealtime(.2f);
                Assert.IsTrue((bool)Get(feedbackView, "IsVisible"), (string)Get(feedbackView, "LastIssue"));
                var feedbackTexts = ((Component)feedbackView).GetComponentsInChildren<TMPro.TMP_Text>(true);
                Assert.IsTrue(feedbackTexts.Any(t => t.text == "환호"));
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath($".harness/local/c10-{trick}.png"));
                yield return new WaitForSecondsRealtime(.8f);
                Assert.IsTrue((bool)Call(rewards, "AddAudienceReaction", participantId, -5));
                yield return new WaitForSecondsRealtime(.2f);
                Assert.IsTrue(feedbackTexts.Any(t => t.text == "야유"));
                Assert.IsTrue(feedbackTexts.Any(t => t.text == "−5"));
                yield return new WaitForSecondsRealtime(.8f);
                Assert.IsFalse((bool)Get(feedbackView, "HasRuntimePresentation"));
                if (trick == "Tightrope")
                {
                    var piggyback = Find("CurtainCall.Tightrope.PiggybackSystem");
                    Assert.IsTrue((bool)Get(piggyback, "SuppressLegacyFeedback"));
                    Call(piggyback, "ShowHandholdLanding");
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.IsTrue(feedbackTexts.Any(t => t.text == "손 잡아줌"), (string)Get(feedbackView, "LastIssue"));
                    Assert.AreEqual("", feedbackTexts.Single(t => t.name == "70_AmountTMP").text);
                }
                if (trick == "Tightrope")
                    Find("CurtainCall.Tightrope.TightropeRun").GetType().GetMethod("ServerStartPerformance", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(Find("CurtainCall.Tightrope.TightropeRun"), null);
                else
                {
                    var route = Find("CurtainCall.HumanCannon.HumanCannonRouteSystem");
                    var wall = ((Array)Private(binding, "cannonFires")).Cast<Component>().First();
                    Call(wall, "Arm", (double)Get(route, "Clock"));
                }
                yield return new WaitForSecondsRealtime(.3f);
                Assert.IsTrue((bool)Call(rewards, "FinishPerformance", false, "C12Verification", null));
                var result = Get(rewards, "Latest");
                // 기본 자금 없이 호응 보상만. 환호 +a 후 야유 −5가 남긴 호응도를 실패여도 생존 ×2로 지급한다.
                long cheer = (long)Get(result, "CheerReward");
                Assert.AreEqual(0L, Get(result, "CompletionReward"));
                Assert.AreEqual(cheer, Get(result, "Income"));
                Assert.AreEqual(cheer, Get(rewards, "Balance"));
                Assert.AreEqual(0L, cheer % 2, "생존자 호응 × 2");
                Assert.Greater(Convert.ToDouble(Get(result, "ElapsedSeconds")), 0);
                Assert.Greater(Convert.ToDouble(Get(result, "CourseLength")), 0);
                var people = ((IEnumerable)Get(result, "Participants")).Cast<object>().ToArray();
                Assert.IsNotNull(Get(people[0], "MaximumDistance"));
                var dto = T("CurtainCall.Reward.RewardResultData");
                var wire = dto.GetMethod("From").Invoke(null, new[] { result });
                var copy = Call(JsonUtility.FromJson(JsonUtility.ToJson(wire), dto), "Read");
                Assert.AreEqual(Get(result, "Income"), Get(copy, "Income"));
                Assert.AreEqual(Convert.ToDouble(Get(result, "ElapsedSeconds")), Convert.ToDouble(Get(copy, "ElapsedSeconds")), .000001);
                Assert.AreEqual(Convert.ToDouble(Get(people[0], "MaximumDistance")), Convert.ToDouble(Get(((IEnumerable)Get(copy, "Participants")).Cast<object>().First(), "MaximumDistance")), .000001);
                var ending = Find("CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation");
                Assert.IsNotNull(Private(ending, "resultsView"));
                yield return new WaitForSecondsRealtime(1.7f);
                Assert.IsTrue((bool)Get(ending, "ResultVisible"));
                var view = Private(ending, "resultsView");
                var resultData = Call(ending, "BuildResults", result);
                Assert.AreEqual($"+{cheer}", Get(resultData, "BaseAmount"));
                Assert.AreEqual("+0", Get(resultData, "BonusAmount"));
                StringAssert.StartsWith("호응 보상", (string)Get(resultData, "BaseLabel"));
                Assert.IsFalse((bool)Get(feedbackView, "HasRuntimePresentation"));
                var voice = Find("CurtainCall.Voice.VoiceChatDevOverlay");
                if (voice != null) Assert.IsTrue((bool)Get(voice, "PresentationSuppressed"));
                Assert.IsTrue((bool)Get(view, "IsVisible"));
                var layers = (Array)Private(view, "memberLayers");
                Assert.AreEqual(1, layers.Cast<object>().Count(l => ((Component)Field(l, "root")).gameObject.activeSelf));
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath($".harness/local/c12-solo-{trick}.png"));
                bool covered = false;
                until = Time.realtimeSinceStartup + 20;
                while (Time.realtimeSinceStartup < until)
                {
                    covered |= (bool)Get(Get(ending, "Curtain"), "IsFullyCovered");
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "WaitingRoomBase"
                        && !(bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive")) break;
                    yield return null;
                }
                Assert.IsTrue(covered, "Curtain must render a fully closed frame before loading.");
                Assert.AreEqual("WaitingRoomBase", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                Assert.IsFalse(UnityEngine.SceneManagement.SceneManager.GetSceneByName("performance_background").isLoaded);
                Assert.IsFalse((bool)Get(Get(ending, "Curtain"), "IsBusy"));
            }
        }
    }
}
