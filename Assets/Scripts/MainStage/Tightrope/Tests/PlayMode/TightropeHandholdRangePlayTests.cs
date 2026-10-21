using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator GreenDangerRecoveryRunsGraduallyAndPausesWhileAirborne()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var balance = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            bool wasEnabled = balance.enabled;
            float previousCapture = Time.captureDeltaTime;
            try
            {
                // 시드 상태와 중앙 유지 입력만 제어하고 회복은 실제 Update에서 진행한다.
                Time.captureDeltaTime = 1f / 60f;
                Call(balance, "SetDisplayedState", true, 70f);
                Call(balance, "Step", 1f);
                Assert.That((float)Get(balance, "RedTimeRatio"), Is.EqualTo(.5f).Within(.001f));
                balance.enabled = true;
                for (int i = 0; i < 60; i++)
                {
                    Call(balance, "SetDisplayedState", true, 0f);
                    yield return null;
                }
                float remaining = (float)Get(balance, "RedTimeRatio");
                Assert.That(remaining, Is.EqualTo(.25f).Within(.01f));
                Call(balance, "SetAirborne", true);
                for (int i = 0; i < 60; i++) yield return null;
                Assert.That((float)Get(balance, "RedTimeRatio"), Is.EqualTo(remaining).Within(.001f));
                Call(balance, "SetAirborne", false);
                for (int i = 0; i < 66; i++)
                {
                    Call(balance, "SetDisplayedState", true, 0f);
                    yield return null;
                }
                Assert.That((float)Get(balance, "RedTime"), Is.Zero);
                Assert.IsFalse((bool)Get(balance, "HasFallen"));
            }
            finally
            {
                balance.enabled = wasEnabled;
                Time.captureDeltaTime = previousCapture;
            }
        }

        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator RealLaneLandingsAddFivePercentagePointsOnlyOncePerLanding()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var balance = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            bool wasEnabled = balance.enabled;
            try
            {
                // 실제 이동/착지 이벤트를 쓰되, 위험 구간의 시간 누적은 분리해서 착지 자체만 검증한다.
                balance.enabled = false;
                Call(mover, "RequestJump");
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "제자리 점프 이륙");
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "제자리 점프 착지");
                Assert.That((float)Get(balance, "RedTimeRatio"), Is.Zero, "제자리 점프는 줄 변경 위험을 더하지 않는다.");
                for (int i = 0; i < 4; i++)
                {
                    Call(balance, "SetDisplayedState", true, 0f);
                    Call(mover, "SetMoveInput", 0f);
                    Call(mover, "RequestLaneJump", i % 2 == 0 ? 1 : -1);
                    yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "옆줄 점프 이륙");
                    Assert.That((float)Get(balance, "RedTimeRatio"), Is.EqualTo(.05f * i).Within(.0001f), "이륙 시점에는 추락 위험을 더하지 않는다.");
                    yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "옆줄 점프 착지");
                    Assert.That((float)Get(balance, "LastShock"), Is.Not.Zero);
                    Assert.That((float)Get(balance, "RedTimeRatio"), Is.EqualTo(.05f * (i + 1)).Within(.0001f));
                    Call(balance, "SetAirborne", false);
                    Assert.That((float)Get(balance, "RedTimeRatio"), Is.EqualTo(.05f * (i + 1)).Within(.0001f), "착지 알림이 반복되어도 중복 적용하지 않는다.");
                    Assert.IsFalse((bool)Get(balance, "HasFallen"));
                    Assert.AreEqual("Normal", Get(player, "State").ToString());
                }
            }
            finally { balance.enabled = wasEnabled; }
        }

        [UnityTest]
        public IEnumerator HandholdRangeMatchesPassiveReachFollowsRemoteAndHidesBlockedOrRemovedHelpers()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var rules = (Component)Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.RopeLaneJumpRules"));
            var view = rules.GetComponent(GameType("CurtainCall.Tightrope.TightropeHandholdRangeView"));
            Assert.IsNotNull(view, "저장된 씬에서 범위 표시를 설치한다.");
            Call(view, "LateUpdate");
            Assert.AreEqual(0, Get(view, "VisibleRangeCount"), "혼자일 때 자기 범위는 그리지 않는다.");
            var helper = SpawnHandholdHelper(2, 5f);
            try
            {
                yield return null;
                Call(view, "LateUpdate");
                Assert.AreEqual(1, Get(view, "VisibleRangeCount"));
                Assert.IsTrue((bool)Call(rules, "IsHandholdLanding", 2, 4.001f));
                Assert.IsTrue((bool)Call(rules, "IsHandholdLanding", 2, 5.999f));
                Assert.IsFalse((bool)Call(rules, "IsHandholdLanding", 2, 3.999f));
                Assert.IsFalse((bool)Call(rules, "IsHandholdLanding", 2, 6.001f));
                Assert.IsFalse((bool)Call(rules, "IsHandholdLanding", 1, 5f));
                CheckHandholdMesh(view, 4f, 6f);

                PositionHandholdHelper(helper, 2, 7f);
                yield return null;
                Call(view, "LateUpdate");
                CheckHandholdMesh(view, 6f, 8f);
                Assert.IsFalse((bool)Call(rules, "IsHandholdLanding", 2, 4f));
                Assert.IsTrue((bool)Call(rules, "IsHandholdLanding", 2, 6.5f));
                Call(course, "SetLaneBlocked", 2, true);
                Call(view, "LateUpdate");
                Assert.AreEqual(0, Get(view, "VisibleRangeCount"), "불타거나 막힌 줄에 착지 보조를 표시하지 않는다.");
                Call(course, "SetLaneBlocked", 2, false);
                Call(view, "LateUpdate");
                Assert.AreEqual(1, Get(view, "VisibleRangeCount"));

                Call(helper, "Despawn", true);
                helper = null;
                yield return null;
                Call(view, "LateUpdate");
                Assert.AreEqual(0, Get(view, "VisibleRangeCount"));
                Assert.IsFalse((bool)Call(rules, "IsHandholdLanding", 2, 7f));
            }
            finally
            {
                if (helper != null) Call(helper, "Despawn", true);
                Call(course, "SetLaneBlocked", 2, false);
            }
        }

        [UnityTest]
        public IEnumerator HandholdRangeLandingKeepsExistingHalfShockAndHalfExtraSway()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var helper = SpawnHandholdHelper(2, 4.8f);
            try
            {
                yield return null;
                var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
                var settings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "RopeMovement");
                var handhold = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Handhold");
                float reduction = (float)Get(handhold, "Reduction");
                Assert.That(reduction, Is.EqualTo(.5f));
                Call(mover, "SetMoveInput", 0f);
                Call(mover, "RequestLaneJump", 1);
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "표시 범위 안으로 옆줄 점프");
                Assert.That((float)Get(balance, "laneLandingReduction"), Is.EqualTo(reduction).Within(.001f));
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "동료 앞뒤 범위에 착지");
                Assert.That(Mathf.Abs((float)Get(balance, "LastShock")),
                    Is.EqualTo((float)Get(settings, "LaneLandingShock") * (1f - reduction)).Within(.001f));
                Assert.That((float)Get(balance, "LaneBoostMultiplier"),
                    Is.EqualTo(1f + ((float)Get(settings, "LaneSwayBoost") - 1f) * (1f - reduction)).Within(.001f));
            }
            finally { if (helper != null) Call(helper, "Despawn", true); }
        }

        Component SpawnHandholdHelper(int lane, float distance)
        {
            var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
            var instance = Object.Instantiate(prefab);
            var netObject = instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            Call(netObject, "SpawnWithOwnership", 778UL, true);
            PositionHandholdHelper(netObject, lane, distance);
            return netObject;
        }

        void PositionHandholdHelper(Component helper, int lane, float distance)
        {
            var destination = (Vector3)Call(course, "GetLanePosition", lane, distance);
            Call(helper.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "SetStartPose", destination,
                Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            var replica = helper.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform"));
            Call(replica, "SetStateInternal", destination, helper.transform.rotation, helper.transform.localScale, true);
            Set(replica, "InboundState", Get(replica, "OutboundState"));
            Call(replica, "TransformStateUpdate");
            Physics.SyncTransforms();
        }

        void CheckHandholdMesh(Component view, float from, float to)
        {
            var meshes = view.GetComponentsInChildren<MeshFilter>().Where(m => m.gameObject.activeInHierarchy
                && m.gameObject.name == "HandholdRange").ToArray();
            Assert.AreEqual(1, meshes.Length);
            var mesh = meshes[0];
            var distances = mesh.sharedMesh.vertices.Select(v => (float)Call(course, "GetDistance", mesh.transform.TransformPoint(v))).ToArray();
            Assert.That(distances.Min(), Is.EqualTo(from).Within(.01f));
            Assert.That(distances.Max(), Is.EqualTo(to).Within(.01f));
            Assert.AreEqual(0, mesh.GetComponentsInChildren<Collider>(true).Length, "범위 표시는 이동·충돌을 바꾸지 않는다.");
            Assert.Greater(mesh.sharedMesh.triangles.Length, 0);
        }
    }
}
