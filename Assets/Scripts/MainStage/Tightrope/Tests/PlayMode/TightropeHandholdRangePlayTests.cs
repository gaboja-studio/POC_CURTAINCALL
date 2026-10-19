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
