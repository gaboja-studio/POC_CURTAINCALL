using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        const string FirstDayFlowType = "CurtainCall.WaitingRoom.WaitingRoomFirstDayFlow";
        const string RehearsalType = "CurtainCall.WaitingRoom.WaitingRoomRehearsal";

        static IEnumerator WaitStage(int stage, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while ((int)Static(RehearsalType, "CurrentStage") != stage && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(stage, Static(RehearsalType, "CurrentStage"), $"소구간 {stage}이어야 한다");
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_VoteAndPersonalCheckpoint()
        {
            // 패널 확인용 캡처(사용자 2026-11-04 요청). 판정에는 영향이 없다.
            static IEnumerator CapturePanels(string name)
            {
                System.IO.Directory.CreateDirectory(".harness/local/rehearsal/panels");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/panels/" + name + ".png"));
                yield return null;
            }
            // 합성 원격 소유자 3명으로 호스트 판정을 검사한다. 실제 다인 접속/조작 검사는 아니다.
            var helpers = new System.Collections.Generic.List<Component>();
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                var questPanel = Priv(Rehearsal, "quest");
                float panelDeadline = Time.realtimeSinceStartup + 3f;
                while (!(bool)Get(questPanel, "Visible") && Time.realtimeSinceStartup < panelDeadline) yield return null;
                StringAssert.StartsWith("[tutorial] ", (string)Get(questPanel, "Title"), "조건형 퀘스트에만 tutorial 표기");
                string explanationTitle = (string)Static(GuideType, "CurrentTitle");
                Assert.IsNotEmpty(explanationTitle, "시간형 설명 패널이 출력되어야 한다");
                Assert.IsFalse(explanationTitle.StartsWith("[tutorial] "), "시간형 설명에는 tutorial 표기를 붙이지 않는다");
                yield return CapturePanels("rehearsal-1-intro");
                // 리허설 소개 10초 뒤 단계 소개 설명(기획서 4.3)이 왼쪽에 뜨는 장면.
                float stageIntroUntil = Time.realtimeSinceStartup + 14f;
                while (!((string)Static(GuideType, "CurrentTitle")).Contains("외줄타기") && Time.realtimeSinceStartup < stageIntroUntil) yield return null;
                yield return new WaitForSecondsRealtime(.5f);
                yield return CapturePanels("rehearsal-1b-stage-intro");
                var session = Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance");
                var network = Get(session, "NetworkManager");
                // NetworkConfig·PlayerPrefab은 Netcode의 공개 필드다(프로퍼티 아님).
                var prefab = (GameObject)Field(Field(network, "NetworkConfig"), "PlayerPrefab");
                for (int i = 0; i < 3; i++)
                {
                    var helper = Object.Instantiate(prefab).GetComponent(T("Unity.Netcode.NetworkObject"));
                    Call(helper, "SpawnWithOwnership", 700UL + (ulong)i, true);
                    helpers.Add(helper);
                }
                yield return null;
                var rehearsal = Rehearsal;
                var setVote = rehearsal.GetType().GetMethod("SetVote", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var localPlayer = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                ulong localId = (ulong)Get(localPlayer, "OwnerClientId");
                setVote.Invoke(rehearsal, new object[] { localId, true });
                Assert.AreEqual(0, ((System.Collections.Generic.HashSet<ulong>)Priv(rehearsal, "votes")).Count, "도착 전 투표 거절");
                var course = (Component)Priv(rehearsal, "course");
                var mover = localPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                float checkpointAt = (float)Call(Get(course, "Layout"), "GetLaneEnd", 0) - 3f;
                var checkpointPosition = (Vector3)Call(course, "GetLanePosition", 0, checkpointAt);
                var rotation = Quaternion.LookRotation((Vector3)Get(course, "Forward"));
                Call(mover, "Relocate", checkpointPosition, rotation);
                yield return new WaitForSecondsRealtime(.3f);
                Call(localPlayer, "RequestState", System.Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen"));
                yield return new WaitForSecondsRealtime(.5f);
                yield return CapturePanels("rehearsal-2-fallen");
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.AreEqual("Normal", Get(localPlayer, "State").ToString());
                // 사용자 2026-11-03: 복귀 지점은 줄 위가 아니라 소구간 출발대(출발대 위 = 코스 거리 0 이전).
                Assert.Less((float)Call(course, "GetDistance", localPlayer.transform.position), 0f, "추락 뒤 소구간 출발대 복귀");
                var finish = (Pose)Call(course, "GetFinishPose", 0);
                Call(mover, "Relocate", finish.position, finish.rotation);
                yield return null; yield return null;
                Assert.AreEqual(0, Static(RehearsalType, "CurrentStage"), "한 명 도착 즉시 전환 금지");
                yield return new WaitForSecondsRealtime(.4f);
                yield return CapturePanels("rehearsal-3-arrived");
                Assert.AreEqual(4, Priv(rehearsal, "participantCount"));
                // 2026-11-04 사용자: 도착대 밖으로 되돌리는 대신 도착한 사람 화면에만 투명 벽을 세운다.
                var walls = (GameObject)Priv(rehearsal, "finishWalls");
                Assert.IsNotNull(walls, "도착하면 도착대 투명 벽을 만든다");
                Assert.IsTrue(walls.activeSelf, "도착한 동안 투명 벽이 켜진다");
                Assert.AreEqual(4, walls.GetComponentsInChildren<BoxCollider>().Length, "앞·뒤·양옆 벽");
                Assert.IsTrue(walls.GetComponentsInChildren<Renderer>().Length == 0, "벽은 보이지 않는다");
                setVote.Invoke(rehearsal, new object[] { localId, true });
                setVote.Invoke(rehearsal, new object[] { 700UL, true });
                yield return null;
                Assert.AreEqual(0, Static(RehearsalType, "CurrentStage"), "4명 중2표는 과반수 아님");
                setVote.Invoke(rehearsal, new object[] { 700UL, false });
                Assert.AreEqual(1, ((System.Collections.Generic.HashSet<ulong>)Priv(rehearsal, "votes")).Count, "동의 취소");
                setVote.Invoke(rehearsal, new object[] { 700UL, true });
                setVote.Invoke(rehearsal, new object[] { 701UL, true });
                yield return WaitStage(1, 5f);
                Assert.AreEqual(0, ((System.Collections.Generic.HashSet<ulong>)Priv(rehearsal, "votes")).Count);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return CapturePanels("rehearsal-4-stage2-1");
            }
            finally
            {
                foreach (var helper in helpers)
                    if (helper != null && (bool)Get(helper, "IsSpawned")) Call(helper, "Despawn", true);
                EndTutorial();
            }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_TriggerStageWaitsForRemotePlacement()
        {
            // 합성 원격 1명: NetworkTransform보다 배치 요청이 먼저 오는 순서를 재현한다.
            Component helper = null;
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                var network = Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager");
                var prefab = (GameObject)Field(Field(network, "NetworkConfig"), "PlayerPrefab");
                helper = Object.Instantiate(prefab).GetComponent(T("Unity.Netcode.NetworkObject"));
                const ulong remoteId = 799UL;
                Call(helper, "SpawnWithOwnership", remoteId, true);
                var remote = helper.GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                // 실제 연결이 없는 합성 원격은 동기화 컴포넌트의 기본 좌표 보간을 끄고 수신 좌표를 직접 제어한다.
                ((Behaviour)helper.GetComponent(T("Unity.Netcode.Components.NetworkTransform"))).enabled = false;
                for (int i = 0; i < 2; i++)
                {
                    Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                    yield return WaitStage(i + 1, 5f);
                    yield return null;
                }
                var oldCourse = (Component)Priv(Rehearsal, "course");
                // 2-2 도착선: 2-3 도착대에 겹치는 이전 좌표를 호스트에 남긴다.
                remote.transform.position = (Vector3)Call(oldCourse, "GetLanePosition", 0, 38f);
                Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                yield return WaitStage(3, 5f);
                yield return null; yield return null;
                var rehearsal = Rehearsal;
                var course = (Component)Priv(rehearsal, "course");
                var arrivals = (System.Collections.Generic.HashSet<ulong>)Priv(rehearsal, "arrived");
                var placements = (System.Collections.Generic.HashSet<ulong>)Priv(rehearsal, "placed");
                Assert.IsTrue((bool)Invoke(rehearsal, "IsFinished", remote), $"이전 위치가 새 도착 범위에 겹치는 재현 조건: state={Get(remote, "State")}, position={remote.transform.position}, course={course.transform.position}, distance={Call(course, "GetDistance", remote.transform.position)}");
                Assert.IsFalse(arrivals.Contains(remoteId), "새 출발 배치 전 원격을 도착자로 등록하면 안 됨");
                int round = (int)Priv(rehearsal, "round");
                var start = (Pose)Call(course, "GetStartPose", 1);
                Invoke(rehearsal, "AcceptPlacement", remoteId, round, start.position);
                Assert.IsFalse(placements.Contains(remoteId), "요청만 먼저 와도 호스트 좌표가 옛 위치면 거절");
                remote.transform.position = start.position;
                Invoke(rehearsal, "AcceptPlacement", remoteId, round - 1, start.position);
                Assert.IsFalse(placements.Contains(remoteId), "이전 회차 확인 거절");
                Invoke(rehearsal, "AcceptPlacement", remoteId, round, new Vector3(float.NaN, 0f, 0f));
                Assert.IsFalse(placements.Contains(remoteId), "유효하지 않은 배치 좌표 거절");
                Invoke(rehearsal, "AcceptPlacement", remoteId, round, start.position);
                Assert.IsTrue(placements.Contains(remoteId), "현재 회차 출발점이 양쪽에서 확인되면 승인");
                yield return null; yield return null;
                Assert.IsFalse(arrivals.Contains(remoteId), "출발대는 도착대가 아님");
                var finish = (Pose)Call(course, "GetFinishPose", 1);
                remote.transform.position = finish.position;
                yield return null; yield return null;
                Assert.IsTrue(arrivals.Contains(remoteId), "배치 확인 후 실제 도착은 정상 등록");
                Assert.AreEqual(3, Static(RehearsalType, "CurrentStage"), "한 명 도착으로 즉시 다음 구간 전환 금지");

            }
            finally
            {
                if (helper != null && (bool)Get(helper, "IsSpawned")) Call(helper, "Despawn", true);
                T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
                EndTutorial();
            }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_FirstPerformanceAfterRehearsalStillKillsOnFall()
        {
            // 사용자 2026-11-03: 리허설을 거친 첫 외줄 공연에서 낙사 패널티가 사라졌다는 보고. 리허설 → 집결 → 공연 뒤 줄 위 추락이 사망으로 받아들여져야 한다.
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                for (int i = 0; i < 5 && !(bool)Static(RehearsalType, "IsCompleted"); i++)
                {
                    float until = Time.realtimeSinceStartup + 5f;
                    while (!(bool)Get(Rehearsal, "CanClearCurrentStage") && Time.realtimeSinceStartup < until) yield return null;
                    Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                    yield return null; yield return null;
                }
                Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
                var gather = Find("CurtainCall.WaitingRoom.StageEntranceGather");
                var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                Call(mover, "Relocate", gather.GetComponent<BoxCollider>().bounds.center - Vector3.up * .5f, Quaternion.identity);
                float deadline = Time.realtimeSinceStartup + 15f;
                while (Field(State, "Phase").ToString() != "Performance" && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual("Performance", Field(State, "Phase").ToString());
                deadline = Time.realtimeSinceStartup + 75f;
                while (((bool)Get(Static("CurtainCall.SpaceLink.SpaceLinkDirector", "Instance"), "IsTransitioning")
                    || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive")) && Time.realtimeSinceStartup < deadline) yield return null;
                // 출발점 배치·공연 시작 확인까지 잠시 기다린 뒤 줄 위로 옮겨 추락을 요청한다.
                yield return new WaitForSecondsRealtime(3f);
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.IsNotNull(course, "공연 코스");
                var run = course.GetComponent(T("CurtainCall.Tightrope.TightropeRun"));
                Assert.IsFalse((bool)Get(run, "PracticeMode"), "공연 코스는 연습 모드가 아니어야 한다");
                deadline = Time.realtimeSinceStartup + 20f;
                while (Get(run, "State").ToString() != "Running" && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual("Running", Get(run, "State").ToString(), "공연 진행 중");
                PlaceOnRope(course, 0, 6f);
                yield return new WaitForSecondsRealtime(.5f);
                Call(LocalPlayer, "RequestState", System.Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen"));
                deadline = Time.realtimeSinceStartup + 4f;
                while (Get(LocalPlayer, "State").ToString() == "Normal" && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual("Fallen", Get(LocalPlayer, "State").ToString(), "공연 중 추락은 사망으로 받아야 한다");
            }
            finally
            {
                T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
                EndTutorial();
            }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_PracticeAfterMasteryKeepsRecordAndEndsFreely()
        {
            // 이수 뒤 쉬는 시간: 호스트가 연습실에서 소구간을 골라 재연습하고 언제든 끝낸다. 이수 기록은 그대로다(CC-219).
            var saved = T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            try
            {
                yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoomBase");
                saved.SetValue(null, 5);
                Assert.IsTrue((bool)Static(RehearsalType, "HasMastered"));
                Assert.IsFalse((bool)Static(RehearsalType, "IsActive"), "쉬는 시간에는 리허설이 저절로 켜지지 않는다");
                var ring = GameObject.Find("S09_PracticeRoom").transform.Find("LegacyRing/PracticeRing/DeckCollider").GetComponent<Collider>();
                var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                Call(mover, "Relocate", new Vector3(ring.bounds.center.x, ring.bounds.max.y, ring.bounds.center.z), Quaternion.identity);
                yield return null; yield return null;
                Assert.IsTrue((bool)T(RehearsalType).GetMethod("StartPractice").Invoke(null, new object[] { 3 }), "연습실 안에서 2-3 재연습 시작");
                yield return WaitStage(3, 10f);
                Assert.IsTrue((bool)Static(RehearsalType, "IsPracticing"));
                Assert.IsNotNull(Priv(Rehearsal, "course"), "고른 소구간 코스 생성");
                Assert.AreEqual("RestTime", Field(State, "Phase").ToString(), "재연습은 캠페인 단계를 바꾸지 않는다");
                Assert.IsTrue((bool)T(RehearsalType).GetMethod("EndPractice").Invoke(null, null));
                float until = Time.realtimeSinceStartup + 5f;
                while ((bool)Static(RehearsalType, "IsActive") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsFalse((bool)Static(RehearsalType, "IsActive"), "자유 종료");
                Assert.AreEqual(5, saved.GetValue(null), "재연습이 이수 기록을 지우지 않는다");
                yield return new WaitForSecondsRealtime(1f); // 바닥 배치 뒤 착지까지
                Assert.Less(Mathf.Abs(LocalPlayer.transform.position.y - ring.bounds.max.y), 1.5f, "종료 뒤 연습실 바닥으로 복귀");
            }
            finally { saved.SetValue(null, 0); EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_ImmediateClearAdvancesOnlyCurrentStage()
        {
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/skip-button.png");
                yield return null;
                for (int stage = 0; stage < 5; stage++)
                {
                    float until = Time.realtimeSinceStartup + 5f;
                    while (!(bool)Get(Rehearsal, "CanClearCurrentStage") && Time.realtimeSinceStartup < until) yield return null;
                    Assert.IsTrue((bool)Get(Rehearsal, "CanClearCurrentStage"));
                    if (stage == 0)
                    {
                        var course = (Component)Priv(Rehearsal, "course");
                        var finish = (Pose)Call(course, "GetFinishPose", 0);
                        Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", finish.position, finish.rotation);
                    }
                    Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                    Assert.IsFalse((bool)Call(Rehearsal, "TryClearCurrentStage"), "같은 프레임 중복 클릭은 다음 구간을 건너뛰면 안 된다");
                    if (stage < 4) yield return WaitStage(stage + 1, 5f);
                    yield return null;
                }
                Assert.IsTrue((bool)Static(RehearsalType, "IsCompleted"));
                Assert.IsFalse((bool)Call(Rehearsal, "TryClearCurrentStage"));
                Assert.AreEqual("Rehearsal", Field(State, "Phase").ToString(), "완료 후 집결 흐름 유지");
            }
            finally { EndTutorial(); }
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_PerformanceInputHudAndFallRecovery()
        {
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/corrected");
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.IsNotNull(Get(course, "ArtProfile"), "공연 준비 프리팹의 아트 연결");
                var middle = ((Component)course).transform.Find("Generated/IntermediatePlatform_Lane0");
                if (middle == null)
                    foreach (var child in ((Component)course).GetComponentsInChildren<Transform>(true))
                        if (child.name == "IntermediatePlatform_Lane0") { middle = child; break; }
                Assert.IsNotNull(middle, "첫 줄 끝에 기존 공연 중간 발판");
                Assert.IsNotNull(middle.Find("Prepared Art"), "공연 바닥 아트 재사용");
                Assert.IsNotNull(middle.GetComponent<Collider>(), "기존 공연 발판의 지지 충돌체");
                Assert.AreEqual(38f, Get(course, "FinishDistance"));
                Assert.IsFalse((bool)Call(course, "IsLaneUsable", 0, 28f), "왼줄은 중간 발판 뒤로 이어지지 않는다");
                Assert.IsTrue((bool)Call(course, "IsLaneUsable", 1, 28f), "옆줄은 도착대로 연결");
                var player = LocalPlayer;
                var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var balance = player.GetComponent(T("CurtainCall.Player.PlayerBalance"));
                float until = Time.realtimeSinceStartup + 9f;
                while ((float)Call(course, "GetDistance", player.transform.position) < 1f && Time.realtimeSinceStartup < until)
                {
                    float value = (float)Get(balance, "Value");
                    if (value > 5f) FlowKeys("W", "A"); else if (value < -5f) FlowKeys("W", "D"); else FlowKeys("W");
                    yield return null;
                }
                FlowKeys(); yield return null;
                string info = "pos=" + player.transform.position + " distance=" + Call(course, "GetDistance", player.transform.position)
                    + " state=" + Get(player, "State") + " simulated=" + Get(mover, "Simulated")
                    + " control=" + Get(mover, "ControlEnabled") + " inputBlocked=" + Static("CurtainCall.Player.InputGate", "IsBlocked");
                System.IO.File.WriteAllText(".harness/local/rehearsal/corrected/input-diagnostic.txt", info);
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/00-entry-diagnostic.png");
                yield return null; yield return null;
                Assert.IsFalse((bool)Get(mover, "FreeMovement"), "출발대에서 W로 줄에 올라타면 공연 외줄 조작으로 전환: " + info);
                Assert.IsTrue((bool)Get(balance, "IsActive"), "실제 균형 계산");
                var binding = Find("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding");
                var balanceView = (Component)Get(binding, "LocalBalance");
                Assert.IsNotNull(balanceView);
                Assert.IsTrue(balanceView.gameObject.activeInHierarchy, "공연과 같은 균형바");
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/01-rope-and-balance.png");
                yield return null;
                FlowKeys("E"); yield return null; yield return null;
                FlowKeys("E", "Space"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.15f); FlowKeys();
                until = Time.realtimeSinceStartup + 4f;
                while ((float)Call(course, "GetSide", player.transform.position) < .7f && Time.realtimeSinceStartup < until) yield return null;
                Assert.Greater((float)Call(course, "GetSide", player.transform.position), .7f, "강제 배치 없이 E+Space로 옆줄 이동");
                yield return new WaitForSecondsRealtime(.4f);
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/02-lane-jump.png");
                yield return null;

                // 추락 시험의 시작점만 배치한다. 상태를 Fallen/Normal로 직접 바꾸지 않는다.
                PlaceOnRope(course, 0, 21.6f);
                yield return new WaitForSecondsRealtime(.2f);
                FlowKeys("W");
                until = Time.realtimeSinceStartup + 7f;
                while (Get(player, "State").ToString() == "Normal" && Time.realtimeSinceStartup < until) yield return null;
                FlowKeys();
                Assert.AreEqual("Fallen", Get(player, "State").ToString(), "W로 끊김을 벗어나 실제 추락: " + player.transform.position);
                until = Time.realtimeSinceStartup + 7f;
                while (Get(player, "State").ToString() != "Normal" && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.That((float)Call(course, "GetDistance", player.transform.position),
                    Is.EqualTo((float)Call(Get(course, "Layout"), "GetLaneEnd", 0) - 3f).Within(.6f), "마지막 줄 체크포인트로 복귀");
                Assert.IsTrue((bool)Get(mover, "Simulated"));
                FlowKeys("W"); yield return new WaitForSecondsRealtime(2f); FlowKeys();
                Assert.Greater((float)Call(course, "GetDistance", player.transform.position), .3f, "복귀 후 실제 W 입력으로 다시 줄 진입");
                Assert.IsTrue((bool)Get(balance, "IsActive"));
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/03-retry-input.png");
                yield return null; yield return null;

                // 다음 장치의 시작 위치만 준비한다. F/W/S/Space 처리나 잡기/놓기 상태는 직접 호출하지 않는다.
                yield return JumpStage(1);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                var pole = course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole"));
                PlaceOnRope(course, 0, (float)Priv(pole, "EntryDistance"));
                yield return new WaitForSecondsRealtime(.4f);
                FlowKeys("F"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.1f); FlowKeys();
                until = Time.realtimeSinceStartup + 2f;
                while (!(bool)Call(pole, "IsHeld", player) && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue((bool)Call(pole, "IsHeld", player), "공연과 같은 F로 인간 그네 잡기");
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/04-swing-grab.png");
                yield return null;
                until = Time.realtimeSinceStartup + 12f;
                while (!(bool)Priv(pole, "OwnCanLetGo") && Time.realtimeSinceStartup < until)
                {
                    FlowKeys((float)Priv(pole, "angularSpeed") >= 0f ? "W" : "S");
                    yield return null;
                }
                Assert.IsTrue((bool)Priv(pole, "OwnCanLetGo"), "실제 W/S 반동으로 놓기 가능: angle=" + Priv(pole, "angle") + " drive=" + Priv(pole, "drive"));
                FlowKeys("Space", "W"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.1f); FlowKeys("W");
                yield return new WaitForSecondsRealtime(.2f);
                until = Time.realtimeSinceStartup + 7f;
                while (((bool)Call(pole, "IsInFlight", player) || (bool)Call(pole, "IsHeld", player))
                    && Time.realtimeSinceStartup < until) yield return null;
                FlowKeys();
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.GreaterOrEqual((float)Call(course, "GetDistance", player.transform.position), 16f, "W/S 반동과 Space 놓기로 끊김 건너기");
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/05-swing-land.png");
                yield return null; yield return null;

                yield return JumpStage(2);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                pole = course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole"));
                var poles = (IList)Priv(pole, "poles");
                var first = poles[0]; var second = poles[1];
                PlaceOnRope(course, 0, (float)Priv(first, "EntryDistance"));
                yield return new WaitForSecondsRealtime(.4f);
                FlowKeys("F"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.1f); FlowKeys();
                until = Time.realtimeSinceStartup + 12f;
                while (!(bool)Priv(first, "OwnCanLetGo") && Time.realtimeSinceStartup < until)
                { FlowKeys((float)Priv(first, "angularSpeed") >= 0f ? "W" : "S"); yield return null; }
                Assert.IsTrue((bool)Priv(first, "OwnCanLetGo"));
                FlowKeys("Space", "W"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.1f); FlowKeys("W");
                until = Time.realtimeSinceStartup + 4f;
                while (!(bool)Call(second, "IsHeld", player) && Time.realtimeSinceStartup < until)
                {
                    FlowKeys("W", "F"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.08f);
                    FlowKeys("W"); yield return null;
                }
                Assert.IsTrue((bool)Call(second, "IsHeld", player), "실제 비행 중 F 입력으로 두 번째 그네 잡기");
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/06-chain-f-grab.png");
                yield return null; yield return null;
                until = Time.realtimeSinceStartup + 12f;
                while (!(bool)Priv(second, "OwnCanLetGo") && Time.realtimeSinceStartup < until)
                { FlowKeys((float)Priv(second, "angularSpeed") >= 0f ? "W" : "S"); yield return null; }
                Assert.IsTrue((bool)Priv(second, "OwnCanLetGo"));
                FlowKeys("Space", "W"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.1f); FlowKeys("W");
                yield return new WaitForSecondsRealtime(.2f);
                until = Time.realtimeSinceStartup + 7f;
                while (((bool)Call(second, "IsInFlight", player) || (bool)Call(second, "IsHeld", player))
                    && Time.realtimeSinceStartup < until) yield return null;
                FlowKeys();
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.GreaterOrEqual((float)Call(course, "GetDistance", player.transform.position), 20f, "두 번째 그네도 실제 W/S와 Space로 건넌다");
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/06b-chain-land.png");
                yield return null; yield return null;

                // 2-3과 3단계도 동일한 공연 F/Space 입력으로 연결·해제를 확인한다.
                foreach (int stage in new[] { 3, 4 })
                {
                    yield return JumpStage(stage);
                    course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                    Assert.AreEqual(stage == 3 ? 32f : 34f, Get(course, "FinishDistance"));
                    int index = stage == 3 ? 0 : 3, lane = stage == 3 ? 0 : 3, target = stage == 3 ? 1 : 2;
                    float at = stage == 3 ? 11.4f : 23.1f, gap = stage == 3 ? 15f : 20f;
                    PlaceOnRope(course, lane, at);
                    until = Time.realtimeSinceStartup + 3f;
                    while (!(bool)Call(RelayGate, "CanUsePole", index, player) && Time.realtimeSinceStartup < until) yield return null;
                    var controller = player.GetComponent(T("CurtainCall.Player.PlayerController"));
                    string triggerInfo = "stage=" + stage + " pos=" + player.transform.position + " active=" + Get(RelayGate, "IsActive")
                        + " state=" + Get(player, "State") + " simulated=" + Get(mover, "Simulated")
                        + " scheme=" + Get(controller, "Scheme") + " free=" + Get(mover, "FreeMovement");
                    System.IO.File.WriteAllText(".harness/local/rehearsal/corrected/trigger-input-diagnostic.txt", triggerInfo);
                    Assert.IsTrue((bool)Call(RelayGate, "CanUsePole", index, player), "트리거 사용 가능 안내: " + triggerInfo);
                    yield return null;
                    FlowKeys("F"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.2f); FlowKeys();
                    until = Time.realtimeSinceStartup + 1.5f;
                    while ((ulong)Call(RelayGate, "HolderAt", index) != LocalId && Time.realtimeSinceStartup < until) yield return null;
                    Assert.AreEqual(LocalId, Call(RelayGate, "HolderAt", index), "기존 F로 트리거 잡기: " + triggerInfo);
                    Assert.IsFalse(Blocked(course, target, gap));
                    ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/" + (stage == 3 ? "07-stage2-3-held" : "09-stage3-held") + ".png");
                    yield return null; yield return null;
                    FlowKeys("Space"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.2f); FlowKeys();
                    Assert.IsTrue(Blocked(course, target, gap));
                    ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/" + (stage == 3 ? "08-stage2-3-released" : "10-stage3-released") + ".png");
                    yield return null; yield return null;
                }
            }
            finally { FlowKeys(); EndTutorial(); }
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
        static IEnumerator QuestFlowCapture(string name)
        {
            const string dir = ".harness/local/rehearsal/quest-flow";
            System.IO.Directory.CreateDirectory(dir);
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(dir + "/" + name + ".png"));
            yield return null; yield return null;
        }

        string QuestTransitionState()
        {
            var link = Static("CurtainCall.SpaceLink.SpaceLinkDirector", "Instance");
            var player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            return "phase=" + Field(State, "Phase") + ", day=" + Field(State, "Day")
                + ", space=" + Get(link, "Current") + ", moving=" + Get(link, "IsTransitioning")
                + ", curtain=" + Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive")
                + ", player=" + (player == null ? "none" : Get(player, "State"))
                + ", quest=" + Static(FirstDayFlowType, "FirstDayQuestTitle");
        }

        IEnumerator WaitQuestReady(string key)
        {
            string title = (string)T("CurtainCall.WaitingRoom.WaitingRoomText").GetMethod("Get").Invoke(null,
                new object[] { "first-day.quest." + key + ".title", "" });
            string last = "";
            int stable = 0;
            float deadline = Time.realtimeSinceStartup + 75f;
            while (Time.realtimeSinceStartup < deadline && stable < 3)
            {
                string state = QuestTransitionState();
                if (last != state)
                {
                    System.IO.File.AppendAllText(".harness/local/rehearsal/quest-flow/transition-trace.txt", key + " | " + state + "\n");
                    last = state;
                }
                var link = Static("CurtainCall.SpaceLink.SpaceLinkDirector", "Instance");
                bool ready = !(bool)Get(link, "IsTransitioning")
                    && !(bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive")
                    && (string)Static(FirstDayFlowType, "FirstDayQuestTitle") == title;
                stable = ready ? stable + 1 : 0;
                yield return null;
            }
            Assert.GreaterOrEqual(stable, 3, key + ": " + QuestTransitionState());
            var owner = T(FirstDayFlowType).GetField("instance", Any | System.Reflection.BindingFlags.Static).GetValue(null);
            var panel = Priv(owner, "firstDayQuest");
            var root = (GameObject)Priv(panel, "root");
            foreach (var label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                Assert.IsNotNull(label.font, key + ": 전환 뒤 패널 폰트가 삭제되면 안 된다");
            var view = (Component)Priv(panel, "view");
            Assert.IsTrue((bool)Get(view, "IsVisible"), key + ": 표시 플래그뿐 아니라 실제 툴팁이 보여야 한다");
        }

        [UnityTest]
        public IEnumerator W6_FirstDay_GuideSecondPerformanceAndNightQuests()
        {
            FreshTutorial();
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/quest-flow");
            System.IO.File.WriteAllText(".harness/local/rehearsal/quest-flow/transition-trace.txt", "");
            try
            {
                // 첫 공연 패널은 별도 재현에서 확보했다. 이 검사는 미완료인 공연 사이부터 아침까지만 좁혀 실행한다.
                yield return Launch(1, "Between", 1, "Tightrope", "WaitingRoomBase");
                var campaign = Static("CurtainCall.Campaign.CampaignRun", "Instance");
                yield return WaitQuestReady("tutorial");
                yield return QuestFlowCapture("01-tutorial");
                Assert.IsFalse((bool)Call(campaign, "NotifyAllAtStageEntrance"));
                // 둘째 공연(CC-215): 안내 완료 뒤 호스트가 스케줄표에서 고르고 확정해야 집결로 시작한다.
                yield return WaitQuestReady("second-pick");
                Assert.IsFalse((bool)Call(campaign, "NotifyAllAtStageEntrance"), "둘째 공연 확정 전 집결 차단");
                var board = Static("CurtainCall.WaitingRoom.ScheduleBoard", "Instance");
                Assert.AreEqual("None", Call(board, "Set", 1, 1, "Tightrope", 1).ToString());
                Assert.AreEqual("None", Call(board, "Confirm").ToString());
                yield return WaitQuestReady("gather");
                yield return QuestFlowCapture("02-second-performance-ready");
                var gather = Find("CurtainCall.WaitingRoom.StageEntranceGather");
                var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                Call(mover, "Relocate", gather.GetComponent<BoxCollider>().bounds.center - Vector3.up * .5f, Quaternion.identity);
                float until = Time.realtimeSinceStartup + 15f;
                while (Field(State, "Phase").ToString() != "Performance" && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual("Performance", Field(State, "Phase").ToString());
                // 기존 검사는 Phase 변경 직후 IsActive=false면 씬 전환 전인데도 준비됐다고 판단했다.
                System.IO.File.AppendAllText(".harness/local/rehearsal/quest-flow/transition-trace.txt", "legacy-end-wait-entry | " + QuestTransitionState() + "\n");
                until = Time.realtimeSinceStartup + 75f;
                while (((bool)Get(Static("CurtainCall.SpaceLink.SpaceLinkDirector", "Instance"), "IsTransitioning")
                    || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive"))
                    && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsFalse((bool)Get(Static("CurtainCall.SpaceLink.SpaceLinkDirector", "Instance"), "IsTransitioning"));
                yield return null; yield return null;
                Assert.IsFalse((bool)Static(FirstDayFlowType, "FirstDayQuestVisible"), "공연에는 대기실 퀘스트가 없어야 한다");
                Assert.AreEqual(1, Field(State, "PerformanceIndex"));
                yield return QuestFlowCapture("03-second-performance");
                // 실제 공연 조작 완주 대신 기존 공개 종료 신호로 연결만 확인한다.
                Assert.IsTrue((bool)Call(campaign, "EndPerformance"));
                yield return WaitQuestReady("night-schedule");
                Assert.AreEqual("Night", Field(State, "Phase").ToString());
                Assert.IsFalse((bool)Static("CurtainCall.WaitingRoom.NightBed", "ScheduleReady"));
                yield return QuestFlowCapture("04-night-schedule");
                ConfirmTonight();
                yield return WaitQuestReady("night-bed");
                yield return QuestFlowCapture("05-night-bed");
                var bed = Find("CurtainCall.WaitingRoom.NightBed");
                var near = bed.transform.position - bed.transform.forward * 1.2f;
                Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", near, bed.transform.rotation);
                Call(bed, "Interact");
                yield return QuestFlowCapture("06-bed-rest");
                until = Time.realtimeSinceStartup + 15f;
                while (((int)Field(State, "Day") != 2 || Field(State, "Phase").ToString() != "RestTime"
                    || bed.transform.Find("BedRestCamera") != null) && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(2, Field(State, "Day"));
                Assert.AreEqual("RestTime", Field(State, "Phase").ToString());
                Assert.IsNull(bed.transform.Find("BedRestCamera"));
                yield return QuestFlowCapture("07-next-morning");
            }
            finally { EndTutorial(); }
        }
    }
}
