using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        const string TutorialType = "CurtainCall.WaitingRoom.WaitingRoomTutorial", GuideType = "CurtainCall.WaitingRoom.WaitingRoomTutorialPanel";

        /// <summary>
        /// 안내를 메모리 전용(PC 저장값을 읽지도 쓰지도 않음)으로 바꾸고 비운 뒤 켠다.
        /// 테스트가 중간에 끊겨도 다음 플레이에서 메모리 전용이 풀리므로 PC 설정이 바뀌지 않는다.
        /// </summary>
        static void FreshTutorial()
        {
            T(TutorialType).GetProperty("MemoryOnly").SetValue(null, true);
            T(TutorialType).GetMethod("ResetProgress").Invoke(null, null);
            T(TutorialType).GetProperty("Enabled").SetValue(null, true);
        }

        static void EndTutorial() => T(TutorialType).GetProperty("MemoryOnly").SetValue(null, false);

        /// <summary>문구 파일(사람이 고침)에서 읽은 제목. 문구가 바뀌어도 테스트를 고치지 않게 한다.</summary>
        static string TutorialTitle(string id)
        {
            var book = T(TutorialType).GetProperty("Book", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            var entry = Call(book, "Get", id);
            return (string)entry.GetType().GetField("title").GetValue(entry);
        }

        static IEnumerator WaitGuide(string title, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while ((string)Static(GuideType, "CurrentTitle") != title && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(title, Static(GuideType, "CurrentTitle"), title + " 안내가 떠야 한다");
        }

        static IEnumerator CompleteCloseUp(string id, Component camera, int expectedCount, float seconds)
        {
            yield return WaitGuide(TutorialTitle(id), seconds);
            float until = Time.realtimeSinceStartup + seconds;
            while (!(bool)Priv(camera, "routeReady") && (bool)Get(camera, "IsActive") && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Priv(camera, "routeReady"), id + " 이동 경로: " + Get(camera, "RouteFailure"));
            float moveIn = (float)Priv(camera, "moveIn"), moveOut = (float)Priv(camera, "moveOut");
            float expectedBack = Mathf.Max(1.8f, (float)Priv(camera, "backLength") / 14f) / 3f; // 이어 간 뒤에는 온 길이 아니라 플레이어까지 곧장 돌아간다.
            Assert.That(moveOut, Is.EqualTo(expectedBack).Within(.03f), id + " 복귀는 이동의 3배속");
            while ((int)Get(camera, "CompletedRouteCount") < expectedCount && Time.realtimeSinceStartup < until) yield return null;
            Assert.GreaterOrEqual((int)Get(camera, "CompletedRouteCount"), expectedCount, id + " 왕복 완료: " + Get(camera, "RouteFailure"));
        }

        [UnityTest]
        public IEnumerator W5_Tutorial_GuideRestShowsInOrderAndRemembers()
        {
            // Task-086: 1일차 가이드 쉬는 시간 연계 G01 → G11, 본 안내로 기록되고 새 런에서도 다시 뜨지 않는다.
            // 2026-10-31 사용자: G03 상호작용은 연계가 아니라 상호작용 가능한 대상을 처음 발견할 때 뜬다.
            FreshTutorial();
            try
            {
                string g01 = TutorialTitle("G01"), g03 = TutorialTitle("G03");
                yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoomBase");
                Assert.AreEqual("Guide", Field(State, "Rest").ToString());
                yield return WaitGuide(g01, 10f);
                Assert.AreNotEqual(g01, Static("CurtainCall.WaitingRoom.WaitingRoomGuidePanel", "CurrentTitle"), "가이드는 나레이션 패널을 쓰지 않는다");
                StringAssert.Contains("G01", (string)Static(TutorialType, "SeenIds"));
                StringAssert.Contains(g01, (string)T(TutorialType).GetMethod("SeenSummary").Invoke(null, null), "다시 보기 목록");

                // 2026-10-31: 연계 튜토리얼(G01→G11) 동안 조작이 막히고, G11은 무대 입구 통로를 클로즈업한 뒤 원래 시점으로 돌아온다.
                Assert.AreEqual(true, Static(TutorialType, "IsSequenceActive"), "연계 중");
                Assert.AreEqual(true, Static("CurtainCall.Player.InputGate", "IsRequested"), "연계 중에는 조작을 막는다");
                yield return WaitGuide(TutorialTitle("G11"), 8f);
                Assert.IsNotNull(GameObject.Find("TutorialCloseUpCamera"), "G11은 클로즈업한다");
                var tutorialCamera = Find("CurtainCall.WaitingRoom.WaitingRoomTutorialCamera");
                float routeDeadline = Time.realtimeSinceStartup + 20f;
                while (!(bool)Priv(tutorialCamera, "routeReady") && (bool)Get(tutorialCamera, "IsActive") && Time.realtimeSinceStartup < routeDeadline) yield return null;
                Assert.IsTrue((bool)Priv(tutorialCamera, "routeReady"), "이동 경로 준비: " + Get(tutorialCamera, "RouteFailure"));
                yield return new WaitForSecondsRealtime((float)Priv(tutorialCamera, "moveIn") + .1f);
                System.IO.Directory.CreateDirectory(".harness/local/rehearsal/followup");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/followup/tutorial-camera.png"));
                yield return null;

                float until = Time.realtimeSinceStartup + 40f;
                while ((bool)Static(TutorialType, "IsSequenceActive") && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(false, Static(TutorialType, "IsSequenceActive"), "연계가 끝나면 조작을 돌려준다");
                Assert.IsNull(GameObject.Find("TutorialCloseUpCamera"), "원래 시점으로 돌아온다");
                Assert.IsTrue((bool)Get(tutorialCamera, "RouteCompleted"), "실제 통행 경로를 왕복해야 한다: " + Get(tutorialCamera, "RouteFailure"));
                Assert.GreaterOrEqual(((Vector3[])Get(tutorialCamera, "RoutePoints")).Length, 2); // 막힘이 없으면 곧게 이어 두 점이 될 수 있다.

                Assert.AreEqual(false, Static("CurtainCall.Player.InputGate", "IsRequested"));

                // 상호작용 가능한 대상(상점)을 발견하면 상호작용 안내가 뜬다.
                var mover = ((Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local")).GetComponent(T("CurtainCall.Player.PlayerMover"));
                Vector3 phone = (Vector3)Get(Find("CurtainCall.WaitingRoom.ShopPhone"), "PresentationPoint");
                Call(mover, "SetStartPose", new Vector3(phone.x + 1f, .1f, phone.z), Quaternion.identity);
                yield return WaitGuide(g03, 12f);
                StringAssert.Contains("[F]", (string)Static(GuideType, "CurrentBody"), "상호작용 키 이름이 들어간다");

                // 사용자 2026-10-31: 앱을 다시 켤 때만 처음부터. 새 런(세대가 바뀜)을 시작해도 본 안내는 다시 뜨지 않는다.
                int generation = (int)Field(State, "Generation");
                yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoomBase");
                Assert.AreNotEqual(generation, Field(State, "Generation"), "다시 시작하면 새 런");
                float watchUntil = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < watchUntil)
                {
                    Assert.AreNotEqual(g01, Static(GuideType, "CurrentTitle"), "새 런에서 본 안내가 다시 떴다");
                    yield return null;
                }
                StringAssert.Contains("G01", (string)Static(TutorialType, "SeenIds"), "새 런에도 본 안내 기록이 남는다");
            }
            finally { EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W5_Tutorial_NightShowsAndDisabledShowsNothing()
        {
            FreshTutorial();
            try
            {
                string n01 = (string)T("CurtainCall.WaitingRoom.WaitingRoomText").GetMethod("Get").Invoke(null, new object[] { "first-day.quest.night-schedule.title", "" });
                T(TutorialType).GetProperty("Enabled").SetValue(null, false);
                yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
                yield return new WaitForSecondsRealtime(4f);
                Assert.AreEqual(true, Static(FirstDayFlowType, "FirstDayQuestVisible"), "임시 퀘스트는 튜토리얼 끄기와 독립적이다");
                T(TutorialType).GetProperty("Enabled").SetValue(null, true);
                // 2026-11-04 사용자: N01(오늘 밤 할 일)은 [tutorial] 없는 왼쪽 설명 패널로 밤 연계 첫 안내로 뜬다.
                // 오른쪽 [tutorial]은 첫날 진행 퀘스트 하나만 남는다.
                yield return WaitGuide(TutorialTitle("N01"), 10f);
                StringAssert.DoesNotContain("[tutorial]", (string)Static(GuideType, "CurrentTitle"), "설명 패널에는 [tutorial]이 없다");
                yield return new WaitForSecondsRealtime(.6f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/followup/tutorial-n01.png"));
                yield return null;
                float until = Time.realtimeSinceStartup + 6f;
                while (!(bool)Static(FirstDayFlowType, "FirstDayQuestVisible") && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(n01, Static(FirstDayFlowType, "FirstDayQuestTitle"), "오늘 밤 할 일 패널");
                // 밤 연계 N02→N04→N03(스케줄표 클로즈업). 잠자기(N05)는 일정 확정 뒤에만 숙소를 비춘다.
                var tutorialCamera = Find("CurtainCall.WaitingRoom.WaitingRoomTutorialCamera");
                yield return CompleteCloseUp("N02", tutorialCamera, 1, 25f);
                yield return CompleteCloseUp("N04", tutorialCamera, 2, 25f);
                yield return CompleteCloseUp("N03", tutorialCamera, 3, 25f);
                until = Time.realtimeSinceStartup + 15f;
                while ((bool)Static(TutorialType, "IsSequenceActive") && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(false, Static(TutorialType, "IsSequenceActive"));
                StringAssert.DoesNotContain("N05", (string)Static(TutorialType, "SeenIds"), "확정 전에는 잠자기 안내 없음");
                Assert.AreEqual(true, Static(FirstDayFlowType, "FirstDayQuestVisible"), "연계가 끝나도 오늘 할 일은 남는다");
                // 스케줄표를 보는 동안에는 표를 가리지 않게 숨긴다.
                var board = Find("CurtainCall.WaitingRoom.ScheduleBoardView");
                // 스케줄표는 멀어지면 닫히므로 앞에 선다.
                var mover = ((Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local")).GetComponent(T("CurtainCall.Player.PlayerMover"));
                Vector3 front = (Vector3)Get(board, "PresentationPoint");
                Call(mover, "SetStartPose", new Vector3(front.x, .1f, front.z), Quaternion.identity);
                yield return new WaitForSecondsRealtime(.2f);
                Call(board, "Interact"); yield return null; yield return null;
                Assert.AreEqual(true, Get(board, "IsOpen"), "스케줄표가 열린다");
                Assert.AreEqual(false, Static(FirstDayFlowType, "FirstDayQuestVisible"), "스케줄표를 보는 동안 숨김");
                board.GetType().GetMethod("SetOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(board, new object[] { false });
                yield return null; yield return null;
                Assert.AreEqual(true, Static(FirstDayFlowType, "FirstDayQuestVisible"), "스케줄표를 닫으면 다시 보인다");
                ConfirmTonight();
                // 앞 안내(예: 스케줄표 앞에서 뜬 상호작용 안내)가 표시 시간 동안 남아 있을 수 있다.
                yield return WaitGuide(TutorialTitle("N05"), 12f);
                Assert.IsNotNull(GameObject.Find("TutorialCloseUpCamera"), "잠자기는 숙소를 클로즈업한다");
                T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                var tutorial = T(TutorialType).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as Component;
                Assert.IsFalse(tutorial.gameObject.activeSelf, "리허설 동안 원본 안내와 자식 패널 모두 잠시 양보한다");
                var flow = Find(FirstDayFlowType) as Behaviour;
                flow.enabled = false;
                Assert.IsTrue(tutorial.gameObject.activeSelf, "연결 컴포넌트를 끄면 원본 실행 상태를 복원한다");

            }
            finally { EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W5_Tutorial_NotRepeatedNextDay()
        {
            // 사용자 2026-10-31: 튜토리얼은 다음 날에 다시 나오지 않는다(게임을 다시 시작해야 처음부터).
            // 1일차 밤 안내를 끝까지 본 뒤 일정 확정 → 잠자기로 2일차가 되어도 본 안내 기록이 남고 다시 뜨지 않는다.
            FreshTutorial();
            try
            {
                yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
                int generation = (int)Field(State, "Generation");
                yield return WaitGuide(TutorialTitle("N03"), 30f);
                float until = Time.realtimeSinceStartup + 10f;
                while ((bool)Static(TutorialType, "IsSequenceActive") && Time.realtimeSinceStartup < until) yield return null;
                ConfirmTonight();
                yield return WaitGuide(TutorialTitle("N05"), 6f);
                until = Time.realtimeSinceStartup + 10f;
                while ((bool)Static(TutorialType, "IsSequenceActive") && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(false, Static(TutorialType, "IsSequenceActive"), "잠자기 안내가 끝나야 눕는다");
                string seenNight = (string)Static(TutorialType, "SeenIds");
                foreach (var id in new[] { "N01", "N02", "N03", "N04", "N05" }) StringAssert.Contains(id, seenNight);

                var bed = Find("CurtainCall.WaitingRoom.NightBed");
                Call(bed, "Interact");
                until = Time.realtimeSinceStartup + 20f;
                while ((Field(State, "Phase").ToString() == "Dawn" || Field(State, "Phase").ToString() == "Night") && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(2, Field(State, "Day"), "2일차로 넘어간다");
                Assert.AreEqual(generation, Field(State, "Generation"), "같은 런");

                // 2일차 아침 동안 밤·가이드 연계 안내가 다시 뜨지 않는다.
                var chain = new System.Collections.Generic.HashSet<string>();
                // G03 상호작용은 연계가 아니라 대상을 처음 발견할 때 뜨므로 다음 날 처음 뜰 수 있다(2026-10-31).
                foreach (var id in new[] { "G01", "G11", "N01", "N02", "N03", "N04", "N05" }) chain.Add(TutorialTitle(id));
                until = Time.realtimeSinceStartup + 12f;
                while (Time.realtimeSinceStartup < until)
                {
                    Assert.IsFalse(chain.Contains((string)Static(GuideType, "CurrentTitle")), "다음 날 연계 안내가 다시 떴다: " + Static(GuideType, "CurrentTitle"));
                    Assert.AreEqual(false, Static(TutorialType, "IsSequenceActive"), "다음 날 연계 튜토리얼이 조작을 잠갔다");
                    yield return null;
                }
                string seenMorning = (string)Static(TutorialType, "SeenIds");
                foreach (var id in new[] { "N01", "N02", "N03", "N04", "N05" }) StringAssert.Contains(id, seenMorning, "다음 날에도 본 안내 기록이 남는다");
            }
            finally { EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W5_ItemBrokenNoticeShowsWhenToolUsedUp()
        {
            // 사용자 2026-10-31: 내구도가 다 닳아 기물이 파괴되면 오른쪽 패널로 알린다(매번).
            FreshTutorial();
            try
            {
                T(TutorialType).GetProperty("Enabled").SetValue(null, false);
                yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
                var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                const string Notice = "CurtainCall.WaitingRoom.WaitingRoomItemBrokenNotice";
                // 도구를 다 쓴 결과를 실제 결과 전달 경로(Answer)로 보낸다.
                ulong local = (ulong)Get(Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"), "OwnerClientId");
                for (int i = 0; i < 2; i++)
                {
                    state.GetType().GetField("answerUsedUp", flags).SetValue(state, true);
                    state.GetType().GetField("answerUsedUpItem", flags).SetValue(state, "천막 말뚝 뽑개");
                    state.GetType().GetMethod("Answer", flags).Invoke(state, new object[] { local, (true, "상자를 뜯어 열었습니다. 천막 말뚝 뽑개를 다 썼습니다.") });
                    yield return null;
                    Assert.AreEqual(true, Static(Notice, "IsVisible"), "파괴 알림이 뜬다(" + (i + 1) + "번째)");
                    Assert.AreEqual("천막 말뚝 뽑개", Static(Notice, "CurrentTitle"));
                    yield return new WaitForSecondsRealtime(4.5f);
                    Assert.AreEqual(false, Static(Notice, "IsVisible"), "잠시 뒤 사라진다");
                }
                // 다 쓰지 않은 결과에는 뜨지 않는다.
                state.GetType().GetMethod("Answer", flags).Invoke(state, new object[] { local, (true, "자물쇠가 헛돌았습니다.") });
                yield return null;
                Assert.AreEqual(false, Static(Notice, "IsVisible"));
            }
            finally { EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W5_DebugEscapeKeysOpenEscapeRoute()
        {
            // 사용자 2026-10-31: 로비 디버그 돈 기물 옆 디버그 탈출로 열쇠 1·2. F를 누르면 그 열쇠를 얻은 것으로 처리한다.
            FreshTutorial();
            try
            {
                T(TutorialType).GetProperty("Enabled").SetValue(null, false);
                yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
                var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
                var keys = Object.FindObjectsByType(T("CurtainCall.WaitingRoom.DebugEscapeKeyProp"), FindObjectsSortMode.None);
                Assert.AreEqual(2, keys.Length, "디버그 탈출로 열쇠 2개");
                foreach (var key in keys)
                {
                    StringAssert.Contains("디버그", (string)Get(key, "ActionLabel"));
                    Assert.AreEqual(true, Get(key, "IsAvailable"));
                    Call(key, "Interact");
                }
                yield return null; yield return null;
                foreach (var id in new[] { "structure-a:escape-key-1", "structure-a:escape-key-2" })
                    Assert.AreEqual(true, Call(state, "IsOpen", id), id + " 열림");
                foreach (var key in keys) Assert.AreEqual(false, Get(key, "IsAvailable"), "이미 얻으면 다시 쓸 수 없다");
            }
            finally { EndTutorial(); }
        }
    }
}
