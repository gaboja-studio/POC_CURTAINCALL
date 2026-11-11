using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest, Category("CommonRulesFollowup"), Category("CommonInspectorSettings")]
        public IEnumerator CommonPoles_TwoVerticalRidersUpperDriveSharedDeadlineAndSwitchRelease()
        {
            DisableEnhancementThreats();
            var poles = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var fixture = new GameObject("Common poles runtime fixture");
            var basePoint = new Vector3(9000, 9000, 9000);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(fixture.transform); floor.transform.position = basePoint + Vector3.down * 2.5f; // 봉 놓기 착지가 공통 부상 구간(5m 이상 15m 미만)에 들도록 바닥을 2m 낮춘다.
            floor.transform.localScale = new Vector3(40, 1, 40);
            var poleObject = new GameObject("Shared switch pole"); poleObject.transform.SetParent(fixture.transform);
            poleObject.transform.position = basePoint + Vector3.up * 4f;
            var pole = poleObject.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonPole"));
            var gate = fixture.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonSawGate"));
            Put(pole, "linkedGate", gate);

            int index = (int)Call(poles, "Register", pole);
            var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
            var instance = Object.Instantiate(prefab);
            var remoteObject = instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            try
            {
                Call(remoteObject, "SpawnWithOwnership", 777UL, true);
                var remote = instance.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var remoteMover = instance.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                Call(mover, "SetStartPose", basePoint + Vector3.up * 3f, Quaternion.identity, true);
                Physics.SyncTransforms();
                yield return null;
                Assert.IsTrue((bool)Call(pilot, "TryRequestNearbyPole"));
                Assert.That(Call(poles, "HeldBy", Actor), Is.EqualTo(index));
                var state = Call(poles, "State", index);
                Assert.That(Get(state, "Capacity"), Is.EqualTo(2));
                Assert.That((double)Get(state, "HoldUntil") - (double)Get(poles, "Clock"), Is.EqualTo(30).Within(.1));
                Assert.That(Get(pole, "HoldSeconds"), Is.EqualTo(30f));
                yield return null;
                Assert.IsTrue((bool)Get(gate, "IsOpen"));
                Vector3 upper = (Vector3)Call(poles, "HangingPosition", index, Actor);
                Call(remoteMover, "SetStartPose", upper - Vector3.up * 1.45f, Quaternion.identity, true);
                Physics.SyncTransforms();
                CommonPoleRequest(poles, 777UL, "PlatformGrab", index);
                Assert.That(Call(poles, "HeldBy", 777UL), Is.EqualTo(index));
                Assert.That(Call(poles, "OccupantCount", index), Is.EqualTo(2));
                Assert.IsFalse((bool)Call(poles, "HasFreeSeat", index));
                Vector3 lower = (Vector3)Call(poles, "HangingPosition", index, 777UL);
                Assert.That(Vector3.ProjectOnPlane(upper - lower, Vector3.up).magnitude, Is.LessThan(.001));
                Assert.That(upper.y - lower.y, Is.EqualTo(1.45f).Within(.001));
                Assert.That((double)Call(state, "DeadlineAt", 1), Is.EqualTo((double)Call(state, "DeadlineAt", 0)).Within(.00001));
                CommonPoleRequest(poles, 777UL, "Swing", index, 1f, 1);
                yield return new WaitForSeconds(.15f);
                Assert.That((float)Call(poles, "SpeedAt", index), Is.EqualTo(0).Within(.01));
                CommonPoleRequest(poles, Actor, "Swing", index, 1f, 1);
                yield return new WaitForSeconds(.15f);
                Assert.That(Mathf.Abs((float)Call(poles, "SpeedAt", index)), Is.GreaterThan(1));
                CommonPoleRequest(poles, Actor, "Release", index);
                Assert.That(Call(poles, "HeldBy", Actor), Is.EqualTo(index), "아래 사람을 두고 상단만 놓지 않는다.");
                CommonPoleRequest(poles, 777UL, "Release", index);
                yield return null;
                Assert.IsTrue((bool)Get(gate, "IsOpen"), "아래 사람만 놓으면 스위치는 유지한다.");
                CommonPoleRequest(poles, Actor, "Release", index);
                yield return null;
                Assert.IsFalse((bool)Get(gate, "IsOpen"), "상단이 놓는 즉시 스위치를 해제한다.");
                Assert.IsTrue(Flying);
                yield return WaitUntil(() => !Flying, 6, "공통 봉에서 일반 바닥으로 착지");
                AssertBody(12); // 약 6m 내려온 실제 착지이므로 공통 낙하 규칙으로 두 다리를 잃는다.
                Assert.IsTrue((bool)Get(mover, "Simulated"));
                Assert.IsNotNull(Get(pole, "VisualBar"));
                Assert.That(Get(pole, "CommonVisual"), Is.Not.Null);
                // 다시 잡은 2인도 상단의 30초 창 하나를 공유하고 동시에 만료한다.
                Call(mover, "SetStartPose", basePoint + Vector3.up * 3f, Quaternion.identity, true);
                Physics.SyncTransforms(); yield return null;
                Assert.IsTrue((bool)Call(pilot, "TryRequestNearbyPole"));
                upper = (Vector3)Call(poles, "HangingPosition", index, Actor);
                Call(remoteMover, "SetStartPose", upper - Vector3.up * 1.45f, Quaternion.identity, true);
                Call(system, "AbandonPole", 777UL);
                Physics.SyncTransforms(); CommonPoleRequest(poles, 777UL, "PlatformGrab", index);
                Assert.That(Call(poles, "OccupantCount", index), Is.EqualTo(2));
                var deadlines = (double[])Get(state, "deadlines");
                deadlines[0] = deadlines[1] = (double)Get(poles, "Clock") - .01;
                Call(poles, "TickState", index);
                Assert.That(Call(poles, "OccupantCount", index), Is.Zero);
                Assert.That(Call(poles, "HeldBy", Actor), Is.EqualTo(-1));
                Assert.That(Call(poles, "HeldBy", 777UL), Is.EqualTo(-1));
            }
            finally
            {
                Call(poles, "Deactivate", index);
                Call(pilot, "ClearMotionForInspection");
                if (remoteObject != null) Call(remoteObject, "Despawn", true);
                Object.Destroy(fixture);
            }
        }

        static void CommonPoleRequest(Component poles, ulong actor, string action, int index, float input = 0, uint sequence = 0)
        {
            var kind = poles.GetType().GetNestedType("PoleAction", BindingFlags.NonPublic);
            var state = Call(poles, "State", index);
            Call(poles, "HandleRequest", actor, Enum.Parse(kind, action), index, (uint)Get(state, "Revision"), 0, 0u, 0u, input, sequence, -1d);
        }
    }
}
