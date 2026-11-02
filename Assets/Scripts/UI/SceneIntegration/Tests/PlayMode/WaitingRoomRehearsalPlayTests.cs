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
            // Task-097: 리허설 시작 지점 → 대기실 연습실에 1단계 코스, 연습실 철창만 열림, 내 캐릭터는 출발대. 도착대에 서면 다음 소구간.
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
                    Assert.AreEqual(((Component)gate).name != "PracticeGate", locked, ((Component)gate).name + " 잠김 여부");
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
                Assert.IsTrue((bool)Get(next, "HasSwingGap"), "2-1은 끊긴 줄(그네)");
                Assert.IsNotNull(Find("CurtainCall.Tightrope.TightropeSwingPole"), "인간 그네가 붙어야 한다");
                yield return new WaitForSecondsRealtime(.5f);
                Assert.Less((float)Call(next, "GetDistance", player.transform.position), 0f, "새 소구간 출발대로 옮겨진다");
                Assert.IsFalse((bool)Static(RehearsalType, "IsCompleted"));
            }
            finally { EndTutorial(); }
        }
    }
}
