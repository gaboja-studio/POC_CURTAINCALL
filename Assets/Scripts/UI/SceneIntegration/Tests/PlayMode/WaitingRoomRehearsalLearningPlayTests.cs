using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator W6_Learning_LowPoleLessonsReachTheirRealFinish()
        {
            Behaviour reader = null;
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                reader = (Behaviour)LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerInputReader"));
                var input = reader.GetType().GetProperty("Current");
                var frameType = T("CurtainCall.Player.PlayerInputFrame");
                reader.enabled = false;
                for (int stage = 1; stage <= 2; stage++)
                {
                    yield return JumpStage(stage);
                    var course = (Component)Priv(Rehearsal, "course");
                    var poles = course.GetComponent(T("CurtainCall.Tightrope.TightropeTrapeze"));
                    Call(Static("CurtainCall.Cameras.CameraViewDirector", "Current"), "SetLookRotation", Quaternion.LookRotation((Vector3)Get(course, "Forward")));
                    int from = -1;
                    float until = Time.realtimeSinceStartup + 65f;
                    while ((int)Static(RehearsalType, "CurrentStage") == stage && Time.realtimeSinceStartup < until)
                    {
                        var frame = Activator.CreateInstance(frameType);
                        int held = (int)Call(poles, "HeldPole", LocalPlayer);
                        float move = 0f, side = 0f;
                        bool jump = false, tap = false;
                        if (held >= 0)
                        {
                            from = held;
                            float angle = (float)Call(poles, "SwingAngle", held), speed = (float)Call(poles, "SwingSpeed", held);
                            move = angle > 65f ? -1f : angle < -65f ? 1f : speed < -.2f ? -1f : 1f;
                            jump = (bool)Call(poles, "CanLaunchTowardNext", held, false);
                        }
                        else if ((bool)Call(poles, "IsInFlight", LocalPlayer))
                        {
                            int next = from + 1;
                            var target = next < stage ? (Vector3)Call(poles, "PoleFeet", next, false)
                                : (Vector3)Call(course, "GetLanePosition", 0, (float)Get(course, "TrapezeGapTo") + 1.2f);
                            move = Mathf.Clamp((float)Call(course, "GetDistance", target) - (float)Call(course, "GetDistance", LocalPlayer.transform.position), -1f, 1f);
                            side = Mathf.Clamp((target.x - LocalPlayer.transform.position.x) * 3f, -1f, 1f);
                            tap = next < stage && (bool)Call(poles, "CanGrabPole", next, LocalPlayer);
                        }
                        else
                        {
                            move = 1f;
                            side = -Mathf.Clamp((float)Get(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerBalance")), "Value") / 15f, -1f, 1f);
                            if ((bool)Call(poles, "CanGrabPole", 0, LocalPlayer)) { move = 0f; tap = true; }
                        }
                        frameType.GetField("Move").SetValue(frame, move); frameType.GetField("Posture").SetValue(frame, side);
                        frameType.GetField("ActionPressed").SetValue(frame, jump); frameType.GetField("InteractTapped").SetValue(frame, tap);
                        input.SetValue(reader, frame);
                        yield return null;
                    }
                    input.SetValue(reader, Activator.CreateInstance(frameType));
                    Assert.AreEqual(stage + 1, Static(RehearsalType, "CurrentStage"), $"{stage}개 낮은 봉을 원본 잡기/비행/착지/이동으로 통과: feet={LocalPlayer.transform.position}, lastPole={from}");
                }
            }
            finally { if (reader != null) reader.enabled = true; FlowKeys(); }
        }

        [UnityTest]
        public IEnumerator W6_Learning_HangHelpAndNativeRescueGuides()
        {
            Component helper = null;
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                var course = (Component)Priv(Rehearsal, "course");
                var hangs = Static("CurtainCall.Tightrope.TightropeHangSystem", "Current");
                yield return CannonWait(() => (bool)Priv(Rehearsal, "courseStarted")
                    && Get(Priv(Rehearsal, "courseRun"), "State").ToString() == "Running"
                    && (int)Priv(hangs, "lastRound") == (int)Priv(hangs, "Round"), 5f, "Wait for the native course round before preparing a hang");
                PlaceOnRope(course, 0, 6f);
                yield return new WaitForSecondsRealtime(.3f);
                string fallTrace = "";
                var balance = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerBalance"));
                Action traceFall = () => fallTrace = $"currentSame={ReferenceEquals(hangs, Static("CurtainCall.Tightrope.TightropeHangSystem", "Current"))}, active={Priv(hangs, "Active")}, book={Get(Priv(hangs, "book"), "Count")}, rounds={Priv(hangs, "lastRound")}/{Priv(hangs, "Round")}, localHang={Priv(hangs, "localHang") != null}\n" + Environment.StackTrace;
                balance.GetType().GetEvent("Fell").AddEventHandler(balance, traceFall);
                Assert.IsTrue((bool)Call(hangs, "ServerTryHang", LocalPlayer, 6f, 0));
                yield return null; yield return null;
                balance.GetType().GetEvent("Fell").RemoveEventHandler(balance, traceFall);
                Assert.AreEqual("Normal", Get(LocalPlayer, "State").ToString(), fallTrace);
                yield return CannonWait(() => ((string)Static(GuideType, "CurrentTitle")).Contains("도움 요청"), 2f,
                    $"Help guide: hanging={Call(hangs, "IsHanging", LocalPlayer)}, state={Get(LocalPlayer, "State")}, key={Invoke(Rehearsal, "RescueQuestKey")}, shown={Priv(Rehearsal, "rescueGuide")}, phase={Field(State, "Phase")}, quest={Get(Priv(Rehearsal, "quest"), "Body")}");
                StringAssert.Contains("동료에게 도움", (string)Get(Priv(Rehearsal, "quest"), "Body"));
                Call(hangs, "ServerReleaseForInspection", LocalId);
                PlaceOnRope(course, 0, 5f);
                var net = Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager");
                var prefab = (GameObject)Field(Field(net, "NetworkConfig"), "PlayerPrefab");
                helper = Object.Instantiate(prefab).GetComponent(T("Unity.Netcode.NetworkObject"));
                Call(helper, "SpawnWithOwnership", 799UL, true);
                ((Behaviour)helper.GetComponent(T("Unity.Netcode.Components.NetworkTransform"))).enabled = false;
                var remote = helper.GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                remote.transform.position = (Vector3)Call(course, "GetLanePosition", 0, 6f);
                Assert.IsTrue((bool)Call(hangs, "ServerTryHang", remote, 6f, 0));
                yield return null; yield return null;
                StringAssert.Contains("동료 구출", (string)Static(GuideType, "CurrentTitle"));
                FlowKeys("F"); yield return null; yield return null;
                Assert.IsTrue((bool)Call(hangs, "IsRescuing", LocalPlayer), "실제 F 입력으로 원본 구조 시작");
                yield return new WaitForSecondsRealtime(2.5f);
                FlowKeys();
                Assert.IsFalse((bool)Call(hangs, "IsHanging", remote));
                Assert.AreEqual("Normal", Get(remote, "State").ToString());
                Assert.AreEqual("", Invoke(Rehearsal, "RescueQuestKey"), "구조 성공 뒤 구조 퀘스트가 남지 않음");
            }
            finally
            {
                FlowKeys();
                if (helper != null && (bool)Get(helper, "IsSpawned")) Call(helper, "Despawn", true);
            }
        }

        [UnityTest]
        public IEnumerator W6_Learning_WikiDeviceLessonsRejectArrivalWithoutSkill()
        {
            yield return Launch(1, "Performance", 0, "Tightrope", "Tightrope");
            var original = Find("CurtainCall.Tightrope.TightropeTrapeze");
            Assert.AreEqual(3, Get(original, "PoleCount"), "일반 공연은 원본 세 봉 유지");
            Assert.IsTrue(((Behaviour)Find("CurtainCall.Tightrope.RopeLaneJumpRules")).isActiveAndEnabled);
            Assert.IsTrue(((Behaviour)Find("CurtainCall.Tightrope.TightropeHandholdRangeView")).isActiveAndEnabled);
            yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
            Assert.AreEqual(38f, Get(Priv(Rehearsal, "course"), "FinishDistance"));
            Assert.AreEqual(2, Get(Priv(Rehearsal, "course"), "LaneCount"));
            // 1단계는 기획상 전원 도착으로 완료한다. 기물 학습을 요구하는 2-1 이후만 검사한다.
            for (int stage = 1; stage < 5; stage++)
            {
                yield return JumpStage(stage);
                var course = (Component)Priv(Rehearsal, "course");
                Assert.AreEqual(new[] { 38f, 32f, 38f, 32f, 34f }[stage], Get(course, "FinishDistance"), "기존 연습 길이");
                Assert.AreEqual(new[] { 2, 1, 1, 2, 4 }[stage], Get(course, "LaneCount"));
                if (stage == 3) AssertRehearsalRelay(course);
                if (stage == 4)
                {
                    var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
                    Assert.AreEqual(2, Get(trapeze, "PoleCount"));
                    Assert.IsNull(Call(trapeze, "PoleVisual", 2), "공연의 마지막 봉 순서를 미리 보여주지 않는다");
                }
                var finish = (Pose)Call(course, "GetFinishPose", 0);
                Call(LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover")), "Relocate", finish.position, finish.rotation);
                yield return new WaitForSecondsRealtime(.7f);
                Assert.AreEqual(stage, Static(RehearsalType, "CurrentStage"), "기능을 쓰지 않은 도착은 완료 아님");
                Assert.IsFalse((bool)Invoke(Rehearsal, "LearnedSkill", LocalPlayer));
                Assert.AreEqual(0, Get(Priv(Rehearsal, "arrived"), "Count"));
                yield return CaptureWholeCourse(course, stage);
            }
        }

        static void PositionLearningHelper(Component helper, Component course, int lane, float distance)
        {
            Vector3 position = (Vector3)Call(course, "GetLanePosition", lane, distance);
            var rotation = Quaternion.LookRotation((Vector3)Get(course, "Forward"));
            Call(helper.GetComponent(T("CurtainCall.Player.PlayerMover")), "SetStartPose", position, rotation, true);
            var replica = helper.GetComponent(T("Unity.Netcode.Components.NetworkTransform"));
            Invoke(replica, "SetStateInternal", position, rotation, helper.transform.localScale, true);
            SetPriv(replica, "InboundState", Priv(replica, "OutboundState"));
            Invoke(replica, "TransformStateUpdate");
            Call(helper.GetComponent(T("CurtainCall.Player.PlayerBalance")), "SetDisplayedState", true, 0f);
            Physics.SyncTransforms();
        }

        IEnumerator LearnLaneForFixture(Component course)
        {
            // 실제 옆줄 점프로 학습한 뒤 도착/투표 테스트의 위치만 준비한다.
            PlaceOnRope(course, 0, 15f);
            yield return new WaitForSecondsRealtime(.3f);
            FlowKeys("E", "Space"); yield return null; yield return null; FlowKeys();
            float until = Time.realtimeSinceStartup + 4f;
            while (!(bool)Invoke(Rehearsal, "LearnedSkill", LocalPlayer) && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue((bool)Invoke(Rehearsal, "LearnedSkill", LocalPlayer), "실제 옆줄 착지 학습");
        }

        [UnityTest]
        public IEnumerator W6_Learning_ViewRelativeMovementFromBothEnds()
        {
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                var course = (Component)Priv(Rehearsal, "course");
                var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var camera = Static("CurtainCall.Cameras.CameraViewDirector", "Current");
                foreach (int direction in new[] { 1, -1 })
                {
                    float from = direction > 0 ? -1f : 39f;
                    int lane = direction > 0 ? 0 : 1;
                    var position = (Vector3)Call(course, "GetLanePosition", lane, from);
                    var rotation = Quaternion.LookRotation(Vector3.forward * direction);
                    Call(mover, "Relocate", position, rotation);
                    Call(camera, "SetLookRotation", rotation);
                    yield return new WaitForSecondsRealtime(.2f);
                    FlowKeys("W"); yield return new WaitForSecondsRealtime(1.3f); FlowKeys();
                    float walked = (float)Call(course, "GetDistance", LocalPlayer.transform.position);
                    Assert.Greater((walked - from) * direction, 1.2f, "양 끝 발판에서 시선 방향 W로 줄 진입");
                    Assert.IsTrue((bool)Get(course.GetComponent(T("CurtainCall.Tightrope.CourseControlSwitcher")), "IsLocalOnRope"));
                    FlowKeys("S"); yield return new WaitForSecondsRealtime(.3f); FlowKeys();
                    float backward = (float)Call(course, "GetDistance", LocalPlayer.transform.position);
                    Assert.Less((backward - walked) * direction, -.1f, "S는 바라보는 방향의 뒤쪽");
                }
                Assert.AreEqual(0, Static(RehearsalType, "CurrentStage"));
                Assert.IsFalse((bool)Invoke(Rehearsal, "LearnedSkill", LocalPlayer), "양방향 이동만으로 줄 전환 수업을 통과하지 않음");
            }
            finally { FlowKeys(); }
        }

        [UnityTest]
        public IEnumerator W6_Learning_LowPolesUseNativeGrabAndFlight()
        {
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                for (int stage = 1; stage <= 2; stage++)
                {
                    yield return JumpStage(stage);
                    var course = (Component)Priv(Rehearsal, "course");
                    var pole = course.GetComponent(T("CurtainCall.Tightrope.TightropeTrapeze"));
                    Assert.AreEqual(stage, Get(pole, "PoleCount"));
                    Assert.IsFalse((bool)Priv(course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole")), "HasGap"));
                    PlaceOnRope(course, 0, 9f);
                    yield return new WaitForSecondsRealtime(.4f);
                    Assert.IsTrue((bool)Call(pole, "CanGrabPole", 0, LocalPlayer), "공연 낮은 봉은 단독 F로 진입 가능");
                    FlowKeys("F"); yield return null; yield return null; FlowKeys();
                    float until = Time.realtimeSinceStartup + 3f;
                    while ((int)Call(pole, "HeldPole", LocalPlayer) != 0 && Time.realtimeSinceStartup < until) yield return null;
                    Assert.AreEqual(0, Call(pole, "HeldPole", LocalPlayer));
                    Assert.IsFalse((bool)Invoke(Rehearsal, "LearnedSkill", LocalPlayer), "잡기만으로는 통과 아님");
                    FlowKeys("W"); yield return new WaitForSecondsRealtime(.25f);
                    FlowKeys("W", "Space"); yield return null; yield return null; FlowKeys();
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.IsTrue((bool)Call(pole, "IsInFlight", LocalPlayer), "원본 Space 비행");
                    Assert.AreEqual(stage == 1, Invoke(Rehearsal, "LearnedSkill", LocalPlayer), "연속 수업은 다음 봉 잡기도 필요");
                    Assert.AreEqual(stage, Static(RehearsalType, "CurrentStage"), "2단계는 실제 도착까지 진행");
                }
            }
            finally { FlowKeys(); }
        }
    }
}
