using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        const string RehearsalType = "CurtainCall.WaitingRoom.WaitingRoomRehearsal";

        static IEnumerator WaitStage(int stage, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while ((int)Static(RehearsalType, "CurrentStage") != stage && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(stage, Static(RehearsalType, "CurrentStage"), $"소구간 {stage}이어야 한다");
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_BuildsCourseOnRingAndAdvancesWhenEveryoneArrives()
        {
            // Task-097: 리허설 시작 지점 → 대기실 연습실에 1단계 코스, 연습실 철창도 닫힘, 내 캐릭터는 출발대. 도착대에 서면 다음 소구간.
            FreshTutorial();
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                Assert.AreEqual("Rehearsal", Field(State, "Phase").ToString());
                Assert.AreEqual("None", Field(State, "Timer").ToString(), "리허설 중 타이머 없음");
                yield return WaitStage(0, 10f);

                var course = Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.IsNotNull(course, "1단계 코스가 있어야 한다");
                Assert.IsTrue(((Component)course).name.StartsWith("RehearsalCourse_"), ((Component)course).name);
                Assert.AreEqual(2, Get(course, "LaneCount"));
                var run = Find("CurtainCall.Tightrope.TightropeRun");
                Assert.IsTrue((bool)Get(run, "PracticeMode"));
                Assert.IsTrue((bool)Get(run, "ExternallyManaged"));

                foreach (var gate in Object.FindObjectsByType(T("CurtainCall.WaitingRoom.NightDoorLock"), FindObjectsSortMode.None))
                {
                    bool locked = (bool)Get(gate, "IsLocked");
                    Assert.IsTrue(locked, ((Component)gate).name + " 잠김 여부");
                }

                yield return new WaitForSecondsRealtime(1f);
                var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                float distance = (float)Call(course, "GetDistance", player.transform.position);
                Assert.Less(distance, 0f, "출발대(출발선 뒤)에 서 있어야 한다");
                Assert.Greater(distance, -5f);
                Assert.Less(Mathf.Abs(player.transform.position.y - ((Component)course).transform.position.y), 1f, "출발대 높이");

                // 도착대로 옮기면(전원 = 나 혼자) 호스트가 다음 소구간으로 넘긴다.
                var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
                float finish = (float)Get(course, "FinishDistance");
                var finishPose = (Pose)Call(course, "GetFinishPose", 0);
                Call(mover, "Relocate", finishPose.position, finishPose.rotation);
                yield return WaitStage(1, 5f);
                var next = Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.IsNotNull(next);
                Assert.AreEqual(1, Get(next, "LaneCount"), "2-1 그네 코스는 1줄");
                yield return null;
                Assert.IsTrue((bool)Call(next, "IsSegmentBlocked", 0, 13f), "2-1은 그네 자리(10~16m)가 끊긴 줄");
                Assert.IsNotNull(Find("CurtainCall.Tightrope.TightropeSwingPole"), "인간 그네가 붙어야 한다");
                yield return new WaitForSecondsRealtime(.5f);
                Assert.Less((float)Call(next, "GetDistance", player.transform.position), 0f, "새 소구간 출발대로 옮겨진다");
                Assert.IsFalse((bool)Static(RehearsalType, "IsCompleted"));
            }
            finally { EndTutorial(); }
        }
        [UnityTest]
        public IEnumerator W6_FirstDay_GuideSecondPerformanceAndNightQuests()
        {
            FreshTutorial();
            try
            {
                yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoomBase");
                Assert.AreEqual("WaitingForExit", Field(State, "Timer").ToString());
                var campaign = Find("CurtainCall.Campaign.CampaignRun");
                Assert.IsFalse((bool)Call(campaign, "NotifyAllAtStageEntrance"));
                float until = Time.realtimeSinceStartup + 25f;
                while (Field(State, "Timer").ToString() == "WaitingForExit" && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreNotEqual("WaitingForExit", Field(State, "Timer").ToString());
                until = Time.realtimeSinceStartup + 8f;
                while (!(bool)Static(TutorialType, "FirstDayQuestVisible") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Static(TutorialType, "FirstDayQuestVisible"),
                    "state=" + Field(State, "Phase") + ", player=" + Get(Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"), "State")
                    + ", todo=" + Static(TutorialType, "TodoVisible"));
                Assert.IsFalse((bool)Static("CurtainCall.WaitingRoom.WaitingRoomRehearsal", "IsPreTutorialRestricted"));
                var gather = Find("CurtainCall.WaitingRoom.StageEntranceGather");
                var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var box = gather.GetComponent<BoxCollider>();
                Call(mover, "Relocate", box.bounds.center - Vector3.up * .5f, Quaternion.identity);
                until = Time.realtimeSinceStartup + 15f;
                while (Field(State, "Phase").ToString() != "Performance" && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual("Performance", Field(State, "Phase").ToString());
                Assert.AreEqual(1, Field(State, "Day"));
                Assert.AreEqual(1, Field(State, "PerformanceIndex"));
                // 공연 결과의 기존 공개 신호로 첫날 밤까지 연결한다. 공연 조작 자체는 별도 회귀 테스트 대상이다.
                until = Time.realtimeSinceStartup + 20f;
                while ((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive") && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSecondsRealtime(1f);
                Assert.IsTrue((bool)Call(campaign, "EndPerformance"));
                until = Time.realtimeSinceStartup + 20f;
                while ((!UnityEngine.SceneManagement.SceneManager.GetSceneByName("WaitingRoomBase").isLoaded
                    || !(bool)Static(TutorialType, "FirstDayQuestVisible")) && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual("Night", Field(State, "Phase").ToString());
                Assert.AreEqual(1, Field(State, "Day"));
                Assert.IsTrue((bool)Static(TutorialType, "FirstDayQuestVisible"));
                Assert.IsFalse((bool)Static("CurtainCall.WaitingRoom.NightBed", "ScheduleReady"));
                ConfirmTonight();
                yield return null; yield return null;
                Assert.IsTrue((bool)Static("CurtainCall.WaitingRoom.NightBed", "ScheduleReady"));
                string expected = (string)T("CurtainCall.WaitingRoom.WaitingRoomText").GetMethod("Get").Invoke(null,
                    new object[] { "first-day.quest.night-bed.title", "" });
                Assert.AreEqual(expected, Static(TutorialType, "FirstDayQuestTitle"));
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/first-night-quest.png");
                yield return null; yield return null;
            }
            finally { EndTutorial(); }
        }
    }
}
