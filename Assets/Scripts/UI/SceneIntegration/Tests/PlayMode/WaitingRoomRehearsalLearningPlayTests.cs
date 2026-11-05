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
        public IEnumerator W6_Learning_FourLaneLessonsRejectArrivalWithoutSkill()
        {
            yield return Launch(1, "Performance", 0, "Tightrope", "Tightrope");
            var original = Find("CurtainCall.Tightrope.TightropeTrapeze");
            Assert.AreEqual(3, Get(original, "PoleCount"), "일반 공연은 원본 세 봉 유지");
            Assert.IsTrue(((Behaviour)Find("CurtainCall.Tightrope.RopeLaneJumpRules")).isActiveAndEnabled);
            Assert.IsTrue(((Behaviour)Find("CurtainCall.Tightrope.TightropeHandholdRangeView")).isActiveAndEnabled);
            yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
            Assert.AreEqual(30f, Get(Priv(Rehearsal, "course"), "FinishDistance"));
            Assert.AreEqual(4, Get(Priv(Rehearsal, "course"), "LaneCount"));
            for (int stage = 1; stage < 5; stage++)
            {
                yield return JumpStage(stage);
                var course = (Component)Priv(Rehearsal, "course");
                Assert.AreEqual(new[] { 30f, 30f, 32f, 30f, 34f }[stage], Get(course, "FinishDistance"), "기존 연습 길이");
                Assert.AreEqual(4, Get(course, "LaneCount"));
                if (stage == 2) AssertRehearsalRelay(course);
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

        [UnityTest]
        public IEnumerator W6_Learning_PiggybackCarryAndRealJumpRequired()
        {
            Component helper = null;
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return JumpStage(3);
                var course = (Component)Priv(Rehearsal, "course");
                var network = Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager");
                var prefab = (GameObject)Field(Field(network, "NetworkConfig"), "PlayerPrefab");
                helper = Object.Instantiate(prefab).GetComponent(T("Unity.Netcode.NetworkObject"));
                Call(helper, "SpawnWithOwnership", 751UL, true);
                yield return null;
                var stack = Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
                float carryStart = (float)Get(course, "FinishDistance") - 6.5f;
                PlaceOnRope(course, 0, carryStart - .5f);
                PositionLearningHelper(helper, course, 0, carryStart);
                yield return null;
                // 합성 원격의 수신 상태와 원본 연결 판정을 준비한다. 실제 F 요청은 독립 4클라이언트에서 검증한다.
                Call(helper.GetComponent(T("CurtainCall.Player.PlayerBalance")), "SetDisplayedState", true, 0f);
                StringAssert.StartsWith("연결:", (string)Invoke(stack, "ServerTryLink", LocalId));
                yield return null; yield return null;
                Assert.IsTrue((bool)Call(stack, "IsInStack", LocalPlayer), "원본 목마 연결");
                Assert.IsNotNull(Call(stack, "GetBelow", LocalPlayer));
                for (int step = 1; step <= 22; step++)
                {
                    // 별도 프로세스 없는 합성 원격의 위치 수신을 준비한다. 실제4인 입력 완주와 구분한다.
                    PositionLearningHelper(helper, course, 0, carryStart + step * .2f);
                    yield return new WaitForSecondsRealtime(.04f);
                }
                Assert.IsFalse((bool)Invoke(Rehearsal, "LearnedSkill", LocalPlayer), "연결/이동만으로는 도약 학습 완료 아님");
                FlowKeys("W", "Space"); yield return null; yield return null; FlowKeys();
                float until = Time.realtimeSinceStartup + 5f;
                while ((int)Static(RehearsalType, "CurrentStage") == 3 && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(4, Static(RehearsalType, "CurrentStage"), "목마 이동 뒤 원본 분리 점프와 실제 착지로 완료");
                Assert.AreEqual((byte)0, Invoke(Rehearsal, "Progress", LocalId), "새 회차에는 이전 학습 증거가 남지 않음");
            }
            finally
            {
                FlowKeys();
                if (helper != null && (bool)Get(helper, "IsSpawned")) Call(helper, "Despawn", true);
            }
        }
    }
}
