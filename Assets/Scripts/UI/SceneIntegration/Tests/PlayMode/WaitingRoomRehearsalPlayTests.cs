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
        public IEnumerator W6_Rehearsal_TrapezeUsesPerformanceDevicesAndRules()
        {
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            var helpers = new System.Collections.Generic.List<Component>();
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                Assert.IsFalse((bool)Static("CurtainCall.Tightrope.PiggybackSystem", "Suppressed"), "공연처럼 목마 연결 가능");
                yield return JumpStage(3);
                Assert.IsFalse((bool)Static("CurtainCall.Tightrope.PiggybackSystem", "Suppressed"), "2-3도 공연 목마 규칙 사용");
                yield return JumpStage(4);
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                AssertRehearsalTrapeze(course);
                Assert.IsFalse((bool)Static("CurtainCall.Tightrope.PiggybackSystem", "Suppressed"), "3단계도 공연 목마 규칙 사용");
                var stack = Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
                var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
                PlaceOnRope(course, (int)Get(trapeze, "LaneIndex"), (float)Get(trapeze, "EntryDistance"));
                yield return null;
                Assert.IsTrue((bool)Call(trapeze, "CanGrabPole", 0, LocalPlayer), "첫 봉도 공통 거리 안이면 혼자 잡을 수 있다");

                var network = Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager");
                var prefab = (GameObject)Field(Field(network, "NetworkConfig"), "PlayerPrefab");
                for (int i = 0; i < 2; i++)
                {
                    var helper = Object.Instantiate(prefab).GetComponent(T("Unity.Netcode.NetworkObject"));
                    Call(helper, "SpawnWithOwnership", 740UL + (ulong)i, true);
                    helpers.Add(helper);
                }
                yield return null;
                var bottom = helpers[0].GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var third = helpers[1].GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var rotation = Quaternion.LookRotation((Vector3)Get(course, "Forward"));
                var entry = (Vector3)Call(course, "GetLanePosition", (int)Get(trapeze, "LaneIndex"), (float)Get(trapeze, "EntryDistance"));
                foreach (var helper in new[] { bottom, third })
                {
                    var position = entry - (Vector3)Get(course, "Forward") * (helper == bottom ? .5f : 1f);
                    Call(helper.GetComponent(T("CurtainCall.Player.PlayerMover")), "SetStartPose", position, rotation, true);
                    var replica = helper.GetComponent(T("Unity.Netcode.Components.NetworkTransform"));
                    Invoke(replica, "SetStateInternal", position, rotation, helper.transform.localScale, true);
                    SetPriv(replica, "InboundState", Priv(replica, "OutboundState"));
                    Invoke(replica, "TransformStateUpdate");
                }
                Physics.SyncTransforms();
                yield return null;
                // 합성 소유자는 별도 프로세스가 없으므로 실제 원격 수신 경계의 균형 활성 상태를 공급한다.
                foreach (var helper in new[] { bottom, third })
                    Call(helper.GetComponent(T("CurtainCall.Player.PlayerBalance")), "SetDisplayedState", true, 0f);
                StringAssert.StartsWith("연결:", (string)Invoke(stack, "ServerTryLink", LocalId));
                yield return null; yield return null;
                Assert.AreEqual(2, Call(stack, "GetStackSize", LocalPlayer), "공연 원본 목마로 2인 연결");
                Assert.AreSame(bottom, Call(stack, "GetBelow", LocalPlayer));
                foreach (var helper in new[] { bottom, third })
                    Call(helper.GetComponent(T("CurtainCall.Player.PlayerBalance")), "SetDisplayedState", true, 0f);
                StringAssert.StartsWith("연결:", (string)Invoke(stack, "ServerTryLink", 741UL));
                Assert.AreEqual(3, Call(stack, "GetStackSize", LocalPlayer), "공연에 없는 2인 상한을 추가하지 않는다");
                Assert.AreEqual(3, Call(stack, "GetStackSize", third));
                // 다음 첫 봉 진입 검사를 위해 합성 세 번째 참가자만 원본 하차 경로로 내린다.
                Call(stack, "ServerReleaseForInspection", 741UL);
                yield return null; yield return null;
                Assert.AreEqual(2, Call(stack, "GetStackSize", LocalPlayer));
                Assert.IsTrue((bool)Invoke(stack, "CanEnterTrapeze", LocalPlayer));
                Assert.IsTrue((bool)Call(trapeze, "CanGrabPole", 0, LocalPlayer), "목마 위 사람은 공연과 같은 거리/자격으로 첫 봉을 잡는다");
                Assert.IsTrue((bool)Invoke(LocalPlayer.GetComponent(T("CurtainCall.HumanCannon.HumanCannonPilot")), "TryRequestNearbyPole"));
                yield return null;
                Assert.AreEqual(0, Call(trapeze, "HeldPole", LocalPlayer));
                Assert.IsTrue((bool)Call(trapeze, "CanAttachToPole", 0, bottom), "아래 동료도 기존 F 매달리기 범위에 있어야 한다");
                var firstPole = ((System.Array)Priv(trapeze, "poles")).GetValue(0);
                CommonPoleRequest(740UL,(int)Priv(firstPole,"Index"),"PlatformGrab");
                Assert.AreEqual(740UL, Call(trapeze, "PassengerAt", 0), "아래 동료도 원본 공중그네에 함께 매달림");
                Assert.IsTrue((bool)Priv(trapeze, "HoldTimeLimited"));
                Assert.That((double)Priv(firstPole,"Until")-(double)Priv(trapeze,"Clock"), Is.InRange(28d,30.01d));
                yield return new WaitForSecondsRealtime(.5f);
                Assert.AreEqual(LocalId, Call(trapeze, "HolderAt", 0), "30초가 지나기 전에는 계속 매달린다");
                Assert.IsFalse((bool)Call(stack, "IsInStack", LocalPlayer), "봉으로 전환하면 원본 목마 연결이 해제됨");

                // 3단계의 공중그네에서도 기존 무패널티 출발대 복귀가 동작해야 한다.
                Call(LocalPlayer, "RequestState", System.Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen"));
                yield return new WaitForSecondsRealtime(2.2f);
                Assert.AreEqual("Normal", Get(LocalPlayer, "State").ToString());
                Assert.AreEqual(4, Static(RehearsalType, "CurrentStage"));
                Assert.Less((float)Call(course, "GetDistance", LocalPlayer.transform.position), 0f);
                yield return JumpStage(4);
                var rebuiltStack = (Behaviour)Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
                Assert.IsNotNull(rebuiltStack, "같은 3단계를 재구성해도 목마 시스템이 남아야 한다");
                Assert.IsTrue(rebuiltStack.isActiveAndEnabled);
                Assert.IsNotNull(rebuiltStack.GetComponent(T("CurtainCall.Tightrope.PiggybackFollow")));
            }
            finally
            {
                foreach (var helper in helpers)
                    if (helper != null && (bool)Get(helper, "IsSpawned")) Call(helper, "Despawn", true);
                EndTutorial();
            }
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
                yield return LearnLaneForFixture(course);
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
                for (int i = 0; i < 3; i++)
                {
                    Assert.IsTrue((bool)Call(Rehearsal, "TryClearCurrentStage"));
                    yield return WaitStage(i + 1, 5f);
                    yield return null;
                }
                var oldCourse = (Component)Priv(Rehearsal, "course");
                // 같은 2-3 재시도: 새 도착대에 겹치는 이전 회차 좌표를 호스트에 남긴다.
                remote.transform.position = ((Pose)Call(oldCourse, "GetFinishPose", 0)).position;
                yield return JumpStage(3);
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
                yield return new WaitForSecondsRealtime(.5f);
                Assert.AreEqual(3, Static(RehearsalType, "CurrentStage"));
                Assert.IsFalse(arrivals.Contains(remoteId), "배치 확인 후에도 목마 학습 없이 도착만 하면 완료 아님");

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
                FreshTutorial(); // 일반 안내가 켜져 있어도 리허설 설명이 우선해야 한다.
                Assert.IsTrue((bool)T(RehearsalType).GetMethod("StartPractice").Invoke(null, new object[] { 3 }), "연습실 안에서 2-3 재연습 시작");
                yield return WaitStage(3, 10f);
                Assert.IsTrue((bool)Static(RehearsalType, "IsPracticing"));
                Assert.IsNotNull(Priv(Rehearsal, "course"), "고른 소구간 코스 생성");
                yield return null; yield return null;
                Assert.AreEqual("자유 연습", Static(GuideType, "CurrentTitle"), "재연습 첫 설명을 일반 안내가 지우면 안 된다");
                var selectedStage = ((System.Array)T("CurtainCall.WaitingRoom.RehearsalStage").GetField("All").GetValue(null)).GetValue(3);
                yield return WaitGuide((string)Get(selectedStage, "DisplayGuideTitle"), 12f);
                Assert.AreEqual(Get(selectedStage, "DisplayGuideTitle"), Static(GuideType, "CurrentTitle"), "재연습 설명을 일반 대기실 안내가 덮으면 안 된다");
                Assert.IsFalse((bool)Static(FirstDayFlowType, "FirstDayQuestVisible"), "재연습 중에는 일반 집결 퀘스트를 숨긴다");
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
                Assert.AreEqual(2, Get(course, "LaneCount"), "기획서 1단계의 두 줄");
                Assert.AreEqual(38f, Get(course, "FinishDistance"));
                Assert.IsTrue((bool)Call(course, "IsLaneUsable", 1, (float)Get(course, "FinishDistance") - 2f));
                Assert.IsFalse((bool)Get(course, "HasSwingGap"));
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
                Assert.AreEqual(1f, ((CanvasGroup)Priv(balanceView, "visibility")).alpha, "실제 균형바가 화면에 표시되어야 한다");
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/01-rope-and-balance.png");
                yield return null;
                float sideBeforeJump = (float)Call(course, "GetSide", player.transform.position);
                FlowKeys("E"); yield return null; yield return null;
                FlowKeys("E", "Space"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.15f); FlowKeys();
                until = Time.realtimeSinceStartup + 4f;
                while ((float)Call(course, "GetSide", player.transform.position) < sideBeforeJump + 2f && Time.realtimeSinceStartup < until) yield return null;
                Assert.Greater((float)Call(course, "GetSide", player.transform.position), sideBeforeJump + 2f, "강제 배치 없이 E+Space로 옆줄 이동");
                yield return new WaitForSecondsRealtime(.4f);
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/02-lane-jump.png");
                yield return null;

                // 추락 시험의 시작점만 배치한다. 상태를 Fallen/Normal로 직접 바꾸지 않는다.
                var unsupported = (Vector3)Call(course, "GetLanePosition", 0, 4f) - (Vector3)Get(course, "Right") * 2f;
                Call(mover, "Relocate", unsupported, Quaternion.LookRotation((Vector3)Get(course, "Forward")));
                yield return new WaitForSecondsRealtime(.2f);
                FlowKeys("W");
                until = Time.realtimeSinceStartup + 7f;
                while (Get(player, "State").ToString() == "Normal" && Time.realtimeSinceStartup < until) yield return null;
                FlowKeys();
                Assert.AreEqual("Fallen", Get(player, "State").ToString(), "지지면 밖에서 중력으로 실제 추락: " + player.transform.position);
                until = Time.realtimeSinceStartup + 7f;
                while (Get(player, "State").ToString() != "Normal" && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                float recoveredDistance = (float)Call(course, "GetDistance", player.transform.position);
                Assert.Less(recoveredDistance, 0f, "철회된 줄 위 개인 체크포인트가 아니라 소구간 출발대로 복귀");
                Assert.GreaterOrEqual(recoveredDistance, -(float)Get(Get(course, "Shape"), "StartPlatformLength"), "출발대 안으로 복귀");
                Assert.IsTrue((bool)Get(mover, "Simulated"));
                FlowKeys("W"); yield return new WaitForSecondsRealtime(2f); FlowKeys();
                Assert.Greater((float)Call(course, "GetDistance", player.transform.position), .3f, "복귀 후 실제 W 입력으로 다시 줄 진입");
                Assert.IsTrue((bool)Get(balance, "IsActive"));
                Assert.AreEqual(1f, ((CanvasGroup)Priv(balanceView, "visibility")).alpha, "추락 복귀 뒤 줄에 오르면 원본 균형 UI도 다시 표시");
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/03-retry-input.png");
                yield return null; yield return null;

                // 위치만 준비하고 원본 입력 경로에 F/Space를 보낸다. Grab/Release 상태를 직접 호출하지 않는다.
                yield return JumpStage(3);
                course = (Component)Priv(Rehearsal, "course");
                AssertRehearsalRelay(course);
                int index = 0;
                int lane = (int)Call(RelayGate, "PoleLane", index);
                PlaceOnRope(course, lane, 11f);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.IsTrue((bool)Call(RelayGate, "CanUsePole", index, player));
                FlowKeys("F"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.2f); FlowKeys();
                until = Time.realtimeSinceStartup + 2f;
                while (!(bool)Call(RelayGate, "IsHeld", player) && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(LocalId, Call(RelayGate, "HolderAt", index), "실제 F 입력으로 공연 출발 봉 잡기");
                int target = (int)Call(RelayGate, "OpenedLane", index);
                Assert.IsTrue((bool)Call(RelayGate, "IsLaneConnected", target));
                Assert.AreNotEqual(lane, target, "공연처럼 출발 봉 줄을 제외하고 무작위 연결");
                Assert.IsTrue((bool)Call(RelayGate, "IsUnlimitedHold", index));
                StringAssert.DoesNotContain("봉 유지", (string)Get(RelayGate, "LocalHint"));
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/04-native-relay-held.png");
                yield return null; yield return null;
                FlowKeys("Space"); yield return null; yield return null; yield return new WaitForSecondsRealtime(.2f); FlowKeys();
                Assert.IsFalse((bool)Call(RelayGate, "IsHeld", player));
                Assert.IsFalse((bool)Call(RelayGate, "IsLaneConnected", target));
                ScreenCapture.CaptureScreenshot(".harness/local/rehearsal/corrected/05-native-relay-release.png");
                yield return null; yield return null;
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
                yield return LearnLaneForFixture((Component)course);
                var finishPose = (Pose)Call(course, "GetFinishPose", 0);
                Call(mover, "Relocate", finishPose.position, finishPose.rotation);
                yield return WaitStage(1, 5f);
                var next = Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.IsNotNull(next);
                Assert.AreEqual(1, Get(next, "LaneCount"), "2-1은 원본 낮은 봉을 배우는 한 줄");
                yield return null;
                Assert.IsFalse((bool)Call(next, "IsSegmentBlocked", 0, 4f), "2-1의 첫 봉 앞 접근 줄은 이어져 있음");
                Assert.IsFalse((bool)Priv(Find("CurtainCall.Tightrope.TightropeSwingPole"), "HasGap"), "휴면 일반 그네를 활성화하지 않는다");
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
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/quest-flow");
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
            var card = (Component)Priv(panel, "card");
            Assert.IsNotNull(card, key + ": 현재 임무 카드");
            Assert.AreEqual("Objective", Field(card, "role").ToString());
            Assert.IsTrue(card.gameObject.activeInHierarchy && !(bool)Get(card, "Closing"), key + ": 실제 임무 카드 표시");
            Assert.AreEqual(title, Get(card, "Title"));
            Assert.IsNotEmpty((string)Get(card, "Content"), key + ": 임무 설명 유지");
            foreach (var label in card.GetComponentsInChildren<TMPro.TMP_Text>())
                Assert.IsNotNull(label.font, key + ": 전환 후 임무 폰트");
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
                yield return WaitQuestReady("gather");
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
                yield return CannonWait(() => (bool)Static(FirstDayFlowType, "FirstDayQuestVisible")
                    && (string)Static(FirstDayFlowType, "FirstDayQuestTitle") == "[tutorial] 다음 공연 준비하기", 15,
                    "날짜 연출 종료/조작 해제 뒤 2일차 아침 퀘스트");
                yield return QuestFlowCapture("07-next-morning");
                Assert.IsTrue((bool)Call(campaign, "NotifyAllAtStageEntrance"));
                yield return null; yield return null;
                Assert.IsFalse((bool)Static(FirstDayFlowType, "FirstDayQuestVisible"), "2일차 첫 공연을 시작하면 아침 안내 제거");
            }
            finally { EndTutorial(); }
        }
    }
}
