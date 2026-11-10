using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// 2026-11-04 사용자: 발판 확대·링 장식 숨김·리허설 넘기기(과반 → 종료 안내 → 페이드 → 첫 공연)·
    /// 연습실 외줄 상호작용 재연습(전원 동의). 합성 원격 1명으로 2인 투표 인원을 만든다. 실제 다인 입력 검증은 아니다.
    /// 화면은 .harness/local/rehearsal/vote/ 에 저장한다.
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        static IEnumerator CaptureVote(string name)
        {
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/vote");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/vote/" + name + ".png"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RoleHudInRehearsalShowsGuideAndObjectiveBeforeVote()
        {
            yield return Launch(1,"Rehearsal",0,"Tightrope","WaitingRoomBase");
            yield return WaitStage(0,10f);
            RectTransform FindRole(string role)
            {
                var type=T("CurtainCall.UI.SceneIntegration.HudRoleCard");
                foreach(Component c in Object.FindObjectsByType(type,FindObjectsSortMode.None))
                    if(type.GetField("role").GetValue(c).ToString()==role) return (RectTransform)c.transform;
                return null;
            }
            float until=Time.realtimeSinceStartup+6f;
            while((!FindRole("Guide") || !FindRole("Objective")) && Time.realtimeSinceStartup<until) yield return null;
            var guide=FindRole("Guide"); var quest=FindRole("Objective"); var vote=FindRole("Vote");
            Assert.IsTrue(guide && quest,"rehearsal starts with a lesson and a team objective");
            Assert.IsFalse(vote,"before arrival no vote should be offered");
            Assert.Less(guide.anchorMin.x,quest.anchorMin.x);
            Assert.AreEqual(1f,quest.anchorMin.x);
            yield return new WaitForSecondsRealtime(.3f);
            yield return CaptureVote("role-hud-live");
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_SkipNeedsArrivalAndPracticeVoteNeedsEveryone()
        {
            Component helper = null;
            T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                var rehearsal = Rehearsal;
                var course = (Component)Priv(rehearsal, "course");
                var shape = Get(course, "Shape");
                Assert.AreEqual(4f, (float)Get(shape, "StartPlatformLength"), .01f, "출발대 길이");
                Assert.AreEqual(3f, (float)Get(shape, "FinishPlatformLength"), .01f, "원본 도착대 설정 유지");
                Assert.AreEqual(2f, Get(course, "FinishPlatformScale"), "리허설 도착대 가로/길이만 2배");
                Physics.SyncTransforms();
                Assert.AreEqual(6f, course.transform.Find("Generated/FinishPlatform").GetComponent<Collider>().bounds.size.z, .01f);
                Assert.AreEqual(Get(Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Course"), "SideMargin"), Get(shape, "SideMargin"), "실제 공연 발판 폭을 복사한다");
                var room = GameObject.Find("S09_PracticeRoom").transform;
                Assert.IsFalse(room.Find("LegacyRing/PracticeLanes").gameObject.activeSelf, "리허설 중 링의 옛 연습 소품을 숨긴다");
                Assert.IsFalse(room.Find("LegacyRing/CollisionRepairs").gameObject.activeSelf, "사다리 뒤 막이도 숨긴다");

                // 투표 인원 2명(호스트 + 합성 원격).
                var network = Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager");
                var prefab = (GameObject)Field(Field(network, "NetworkConfig"), "PlayerPrefab");
                helper = Object.Instantiate(prefab).GetComponent(T("Unity.Netcode.NetworkObject"));
                const ulong remoteId = 731UL;
                Call(helper, "SpawnWithOwnership", remoteId, true);
                ((Behaviour)helper.GetComponent(T("Unity.Netcode.Components.NetworkTransform"))).enabled = false;
                helper.transform.position = (Vector3)Call(course, "GetLanePosition", 0, -2f);
                yield return new WaitForSecondsRealtime(1.5f);
                Invoke(rehearsal, "RequestSkip", true);
                Assert.AreEqual(0, Get(Priv(rehearsal, "votes"), "Count"), "도착 전 넘기기 거절");
                yield return CaptureVote("1-start-skip-panel");

                // 도착 → 투명 벽.
                var localPlayer = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                var mover = localPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                yield return LearnLaneForFixture(course);
                var finish = (Pose)Call(course, "GetFinishPose", 1);
                Call(mover, "Relocate", finish.position, finish.rotation);
                yield return new WaitForSecondsRealtime(.6f);
                var walls = (GameObject)Priv(rehearsal, "finishWalls");
                Assert.IsTrue(walls != null && walls.activeSelf, "도착한 사람 화면에 투명 벽");
                yield return CaptureVote("2-arrived-finish-platform");

                // 넘기기: 1/2는 과반 아님, 2/2면 통과.
                Invoke(rehearsal, "RequestSkip", true);
                yield return null;
                Assert.IsFalse((bool)Static(RehearsalType, "IsEnding"), "2명 중 1표는 과반이 아니다");
                yield return CaptureVote("3-skip-one-vote");
                Invoke(rehearsal, "HostSetSkipVote", remoteId, true);
                yield return null;
                yield return WaitStage(1, 5f);
                Assert.IsFalse((bool)Static(RehearsalType, "IsEnding"), "현재 구간만 넘기며 공연으로 직행하지 않음");
                yield return JumpStage(4);
                Assert.IsTrue((bool)Call(rehearsal, "TryClearCurrentStage"));
                yield return new WaitForSecondsRealtime(.5f);
                if (helper != null && (bool)Get(helper, "IsSpawned")) Call(helper, "Despawn", true);
                helper = null;
                // 공연 시작 연출(커튼)이 끝난 뒤 다음 시작 지점으로 옮긴다.
                float until = Time.realtimeSinceStartup + 30f;
                while ((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive") && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSecondsRealtime(3f);

                // 이수 뒤 대기실 쉬는 시간: 연습실 외줄 상호작용 → 전원 동의 패널.
                yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoomBase");
                T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 5);
                network = Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager");
                prefab = (GameObject)Field(Field(network, "NetworkConfig"), "PlayerPrefab");
                helper = Object.Instantiate(prefab).GetComponent(T("Unity.Netcode.NetworkObject"));
                Call(helper, "SpawnWithOwnership", remoteId, true);
                ((Behaviour)helper.GetComponent(T("Unity.Netcode.Components.NetworkTransform"))).enabled = false;
                room = GameObject.Find("S09_PracticeRoom").transform;
                var rope = room.Find("LegacyRing/PracticeLanes/Rope");
                Assert.IsNotNull(rope.GetComponent(T("CurtainCall.WaitingRoom.RehearsalPracticeStation")), "연습실 외줄 상호작용");
                localPlayer = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                mover = localPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var under = rope.GetComponentInChildren<Renderer>().bounds.center;
                Call(mover, "Relocate", new Vector3(under.x + 3f, .7f, under.z - 1.5f), Quaternion.LookRotation(Vector3.left));
                helper.transform.position = new Vector3(under.x - 3f, .7f, under.z - 1.5f);
                yield return new WaitForSecondsRealtime(1f);
                yield return CaptureVote("7-practice-station");
                var station = rope.GetComponent(T("CurtainCall.WaitingRoom.RehearsalPracticeStation"));
                Assert.IsTrue((float)Call(station, "Reach", localPlayer.transform.position, localPlayer) >= 0f, "줄 아래에서 닿는다");
                Call(station, "Interact");
                yield return null; yield return null;
                Assert.IsTrue((bool)Static(RehearsalType, "IsPracticeVoteOpen"), "재연습 투표가 열린다");
                Assert.AreEqual("리허설 다시 하기", Static(RehearsalType, "VotePanelTitle"));
                yield return new WaitForSecondsRealtime(.5f);
                yield return CaptureVote("8-practice-vote-panel");
                // 미동의 → 취소.
                Invoke(rehearsal, "HostAnswerPractice", remoteId, false);
                yield return null; yield return null;
                Assert.IsFalse((bool)Static(RehearsalType, "IsPracticeVoteOpen"), "한 명이라도 미동의면 취소");
                Assert.IsFalse((bool)Static(RehearsalType, "IsPracticing"));
                yield return new WaitForSecondsRealtime(.3f);
                yield return CaptureVote("9-practice-vote-cancelled");
                // 다시 열고 전원 동의 → 재연습(공연·1일차로 이어지지 않음).
                yield return new WaitForSecondsRealtime(4.5f);
                Call(station, "Interact");
                yield return null;
                Invoke(rehearsal, "HostAnswerPractice", remoteId, true);
                until = Time.realtimeSinceStartup + 10f;
                while (!((bool)Static(RehearsalType, "IsPracticing") && (int)Static(RehearsalType, "CurrentStage") == 0) && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Static(RehearsalType, "IsPracticing"), "전원 동의면 재연습");
                Assert.AreEqual("RestTime", Field(State, "Phase").ToString(), "재연습은 캠페인 단계를 바꾸지 않는다");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return CaptureVote("10-practice-started");
            }
            finally
            {
                if (helper != null && (bool)Get(helper, "IsSpawned")) Call(helper, "Despawn", true);
                T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
                if ((bool)Static(RehearsalType, "IsPracticing")) T(RehearsalType).GetMethod("EndPractice").Invoke(null, null);
            }
        }
    }
}
