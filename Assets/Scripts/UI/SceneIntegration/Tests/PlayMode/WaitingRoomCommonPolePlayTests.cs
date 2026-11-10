using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest, Category("TutorialCommonRulesRegression")]
        public IEnumerator TutorialCommonPoles_VisualRootFollowsDisableAndIsDestroyedWithCourse()
        {
            yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
            yield return JumpStage(3);
            var gate = RelayGate;
            yield return CannonWait(() => (bool)Get(gate, "IsActive"), 5f, "튜토리얼 스위치 봉 준비");
            var pole = (Behaviour)Call(gate, "PhysicalPole", 0);
            yield return CannonWait(() => (Transform)Get(pole, "CommonVisual") != null, 5f, "공통 외형 생성");
            var visual = (Transform)Get(pole, "CommonVisual");
            Assert.IsTrue(visual.gameObject.activeInHierarchy);
            Assert.IsNull(visual.parent);
            Assert.AreEqual(pole.gameObject.scene, visual.gameObject.scene);
            var service = (Behaviour)CommonPoles;
            try
            {
                // 표시 메서드를 직접 호출하지 않고 Unity의 실제 OnDisable을 검사한다.
                pole.enabled = false;
                Assert.IsFalse(visual.gameObject.activeSelf, "봉 비활성화 즉시 분리된 외형도 숨김");
                yield return null;
                Assert.IsFalse(visual.gameObject.activeSelf, "공통 service의 LateUpdate가 다시 표시하지 않음");
                pole.enabled = true;
                yield return CannonWait(() => visual.gameObject.activeInHierarchy, 3f, "재활성화 후 공통 외형 복원");
                Assert.AreSame(visual, Get(pole, "CommonVisual"), "재활성화로 외형을 중복 생성하지 않음");

                // 씬 unload에서는 별도 표시 루트가 service보다 먼저 파괴될 수 있다.
                // 그 순서를 모사해 OnDisable이 새 표시 루트를 생성하지 않는지 검사한다.
                Invoke(pole, "DestroyCommonVisual");
                service.enabled = false;
                Assert.IsNull(Get(pole, "CommonVisual"), "service 종료는 사라진 외형을 다시 생성하지 않는다.");
                yield return null;
                Assert.IsTrue(visual == null);
                Assert.IsNull(Get(pole, "CommonVisual"));
                service.enabled = true;
                yield return CannonWait(() => (Transform)Get(pole, "CommonVisual") != null, 3f, "service 재활성화 후 외형 생성");
                visual = (Transform)Get(pole, "CommonVisual");

                // 실제 튜토리얼 단계 전환은 기존 코스를 비활성화한 뒤 Destroy한다.
                yield return JumpStage(4);
                yield return null;
                Assert.IsTrue(pole == null, "이전 단계 물리 봉 파괴");
                Assert.IsTrue(visual == null, "OnDestroy가 별도 scene-root 외형까지 정리");
            }
            finally
            {
                if (pole != null) pole.enabled = true;
                if (service != null) service.enabled = true;
            }
        }

        [UnityTest, Category("TutorialCommonRulesRegression")]
        public IEnumerator TutorialCommonPoles_TwoRiderSwitchUsesSharedReleaseRuleAndMatchingHints()
        {
            var settings = (Object)Static("CurtainCall.Settings.GameSettings", "Poles");
            string saved = JsonUtility.ToJson(settings);
            Component helper = null;
            Behaviour reader = null;
            object common = null;
            int index = -1;
            try
            {
                JsonUtility.FromJsonOverwrite("{\"capacity\":2,\"preventReleaseWithPassengers\":true}", settings);
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return JumpStage(3);
                var course = (Component)Priv(Rehearsal, "course");
                var gate = RelayGate;
                common = CommonPoles;
                yield return CannonWait(() => (bool)Get(gate, "IsActive"), 5f, "튜토리얼 지정 스위치 봉 준비");
                Assert.IsTrue((bool)Priv(gate, "UsesSpots"));
                Assert.AreEqual(1, Get(gate, "PoleCount"));
                index = CommonRelayIndex(gate, 0);
                var pole = (Component)Call(gate, "PhysicalPole", 0);
                var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                reader = (Behaviour)LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerInputReader"));
                reader.enabled = false;
                var rotation = Quaternion.LookRotation((Vector3)Get(course, "Forward"));
                Call(mover, "SetStartPose", (Vector3)Call(pole, "HangingPosition", 0f, 0), rotation, true);
                Physics.SyncTransforms();
                CommonPoleRequest(LocalId, index, "PlatformGrab");
                Assert.AreEqual(index, Call(common, "HeldBy", LocalId));
                Assert.IsTrue((bool)Call(gate, "IsLaneConnected", 1));

                // 호스트와 합성 원격 소유자: 실제 두 클라이언트 통신을 검증하는 검사는 아니다.
                var network = Get(Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance"), "NetworkManager");
                var prefab = (GameObject)Field(Field(network, "NetworkConfig"), "PlayerPrefab");
                helper = Object.Instantiate(prefab).GetComponent(T("Unity.Netcode.NetworkObject"));
                const ulong passenger = 786UL;
                Call(helper, "SpawnWithOwnership", passenger, true);
                ((Behaviour)helper.GetComponent(T("Unity.Netcode.Components.NetworkTransform"))).enabled = false;
                var passengerPlayer = helper.GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var passengerMover = helper.GetComponent(T("CurtainCall.Player.PlayerMover"));
                // 실제 접근은 줄 윗면에서 한다. 탑승 후 하단 발 좌표로 먼저 옮기면
                // 몸통 중심이 지지면 안에 들어가 공통 벽·층 차단 검사에 정상적으로 거절된다.
                Vector3 approach = (Vector3)Call(gate, "PoleFeet", 0);
                Vector3 hanging = (Vector3)Call(pole, "HangingPosition", 0f, 1);
                Call(passengerMover, "SetStartPose", approach, rotation, true);
                Physics.SyncTransforms();
                Assert.IsTrue((bool)Get(passengerPlayer, "IsSpawned"));
                Assert.AreEqual("Normal", Get(passengerPlayer, "State").ToString());
                Assert.That(Vector3.Distance(passengerPlayer.transform.position, approach), Is.LessThan(.001f));
                Assert.IsTrue((bool)Call(gate, "CanUsePole", 0, passengerPlayer),
                    "줄 위 접근 위치에서 공통 거리·벽·층 판정을 통과해야 한다. position=" + passengerPlayer.transform.position);
                Assert.That((float)Call(gate, "HoldRemaining", 0), Is.GreaterThan(0f));
                CommonPoleRequest(passenger, index, "PlatformGrab");
                Assert.AreEqual(2, Call(common, "OccupantCount", index), "튜토리얼도 공통 정원 2명");
                Assert.AreEqual(index, Call(common, "HeldBy", passenger));
                var attached = (Vector3)Call(common, "HangingPosition", index, passenger);
                Assert.That(Vector3.Distance(attached, hanging), Is.LessThan(.001f));
                Assert.That(attached.y, Is.LessThan(approach.y), "접근 때와 달리 탑승 결과의 하단 발은 줄 아래에 놓인다.");
                yield return null;
                Assert.That(Vector3.Distance(passengerPlayer.transform.position, attached), Is.LessThan(.001f),
                    "공통 표시 갱신이 원격 소유자 몸도 하단 매달림 위치로 이동시킨다.");
                var runtime = ((System.Array)Priv(common, "states")).GetValue(index);
                float originalAngle = (float)Priv(runtime, "Angle");
                try
                {
                    SetPriv(runtime, "Angle", 40f);
                    Call(pole, "PresentSwing", 40f);
                    Assert.That((float)Call(common, "AngleAt", index), Is.EqualTo(40f).Within(.001f));
                    AssertCommonPoleHandPose(LocalPlayer, pole, 0);
                    AssertCommonPoleHandPose(passengerPlayer, pole, 1);
                }
                finally
                {
                    SetPriv(runtime, "Angle", originalAngle);
                    Call(pole, "PresentSwing", originalAngle);
                }
                Assert.IsTrue((bool)Call(common, "IsReleaseBlockedByPassenger", LocalId));
                Assert.IsFalse((bool)Call(common, "IsReleaseBlockedByPassenger", passenger));
                StringAssert.Contains("아래 동료가 먼저 손을 놓아야 합니다", (string)Get(gate, "LocalHint"));
                StringAssert.Contains("아래 동료가 먼저 손을 놓아야 합니다",
                    (string)Invoke(Rehearsal, "QuestFor", Priv(Rehearsal, "Stage"), mover));

                JsonUtility.FromJsonOverwrite("{\"preventReleaseWithPassengers\":false}", settings);
                Assert.IsFalse((bool)Call(common, "IsReleaseBlockedByPassenger", LocalId), "조회도 공통 설정 변경을 따름");
                StringAssert.DoesNotContain("아래 동료가 먼저", (string)Get(gate, "LocalHint"));
                JsonUtility.FromJsonOverwrite("{\"preventReleaseWithPassengers\":true}", settings);
                CommonPoleRequest(LocalId, index, "Release");
                Assert.AreEqual(2, Call(common, "OccupantCount", index), "아래 동료가 있으면 상단 해제 거절");
                Assert.IsTrue((bool)Call(gate, "IsLaneConnected", 1));

                CommonPoleRequest(passenger, index, "Release");
                yield return null;
                Assert.AreEqual(1, Call(common, "OccupantCount", index));
                Assert.AreEqual(LocalId, Call(gate, "HolderAt", 0));
                Assert.IsTrue((bool)Call(gate, "IsLaneConnected", 1), "하단만 놓으면 길 유지");
                Assert.IsFalse(Blocked(course, 1, 16f), "연결된 길의 실제 지지면 유지");
                Assert.IsFalse((bool)Call(common, "IsReleaseBlockedByPassenger", LocalId));
                StringAssert.DoesNotContain("아래 동료가 먼저", (string)Get(gate, "LocalHint"));

                CommonPoleRequest(LocalId, index, "Release");
                Assert.IsFalse((bool)Call(gate, "IsLaneConnected", 1), "상단이 놓는 즉시 연결 해제");
                yield return null;
                Assert.AreEqual(0, Call(common, "OccupantCount", index));
                Assert.IsTrue(Blocked(course, 1, 16f), "표시뿐 아니라 실제 지지면도 닫힘");
            }
            finally
            {
                if (common != null && index >= 0) Call(common, "Deactivate", index);
                if (helper != null && (bool)Get(helper, "IsSpawned")) Call(helper, "Despawn", true);
                if (reader != null) reader.enabled = true;
                JsonUtility.FromJsonOverwrite(saved, settings);
                FlowKeys();
            }
        }

        static void AssertCommonPoleHandPose(Component actor, Component pole, int seat)
        {
            var presentation = actor.GetComponent(T("CurtainCall.Player.HumanoidPlayerPresentation"));
            Assert.IsNotNull(presentation);
            object[] output = { Vector3.zero, Vector3.zero };
            Assert.IsTrue((bool)Invoke(presentation, "TryHangGrip", output));
            var bar = (Transform)Get(pole, "VisualBar");
            float spacing = (float)Get(pole, "RiderSpacing");
            Vector3 expectedGrip = bar.position - Vector3.up * (seat * spacing);
            Assert.That(Vector3.Distance((Vector3)output[0], expectedGrip), Is.LessThan(.001f),
                seat == 0 ? "위 사람 손은 실제로 흔들린 봉에 맞춘다." : "아래 사람 손은 자신의 매달림 순서 높이에 맞춘다.");
            var grip = (Transform)Get(pole, "Grip");
            Assert.That(Vector3.Distance((Vector3)output[1], grip.rotation * Vector3.right), Is.LessThan(.001f),
                "손 축도 공통 봉의 실제 회전축을 사용한다.");
        }
    }
}
