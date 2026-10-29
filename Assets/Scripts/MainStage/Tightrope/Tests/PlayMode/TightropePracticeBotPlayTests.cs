using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator PracticeBotLauncherProtectsOccupiedAndStartedRoomsWithoutChangingHostInput()
        {
            var panelObject = new GameObject("Practice bot launch boundary");
            var panel = panelObject.AddComponent(GameType("CurtainCall.Tightrope.TightropePracticeBot"));
            var controller = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerController"));
            var reader = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"));
            bool controllerEnabled = controller.enabled, readerEnabled = reader.enabled;
            try
            {
                var args = new object[] { null };
                Assert.IsTrue((bool)panel.GetType().GetMethod("CanStartBot").Invoke(panel, args));
                // Exercise the two-person guard without spawning a child process or changing shared session settings.
                Call(session, "SetPlayerCount", 2);
                Assert.IsFalse((bool)panel.GetType().GetMethod("CanStartBot").Invoke(panel, args));
                StringAssert.Contains("혼자", (string)args[0]);
                Call(session, "SetPlayerCount", 1);
                yield return BeginPerformance();
                Assert.IsFalse((bool)panel.GetType().GetMethod("CanStartBot").Invoke(panel, args));
                Call(panel, "StopBot");
                Assert.That((ulong)Get(panel, "BotId"), Is.EqualTo(ulong.MaxValue));
                Assert.IsFalse((bool)Get(panel, "Running"));
                Assert.That(controller.enabled, Is.EqualTo(controllerEnabled));
                Assert.That(reader.enabled, Is.EqualTo(readerEnabled));
                Assert.AreSame(player, Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"));
                Assert.That(Get(session, "ConnectionState").ToString(), Is.EqualTo("Host"));
            }
            finally { Call(session, "SetPlayerCount", 1); Object.Destroy(panelObject); }
        }

        [UnityTest]
        public IEnumerator PracticeBotBrainFollowsWithNormalRopeSpeedAndDoesNotSteerInAir()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var partner = CreatePracticeBotTestPartner(1, 8f);
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            var brain = Activator.CreateInstance(GameType("CurtainCall.Tightrope.TightropePracticeBotBrain"), new object[] { player, partner });
            float capture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                float start = (float)Call(course, "GetDistance", player.transform.position);
                for (int frame = 0; frame < 30; frame++)
                {
                    var input = Call(brain, "BuildInput");
                    Assert.That((float)Get(input, "Move"), Is.EqualTo(1f));
                    Assert.IsFalse((bool)Get(input, "ActionPressed"));
                    Call(scheme, "Tick", context, input);
                    yield return null;
                }
                float traveled = (float)Call(course, "GetDistance", player.transform.position) - start;
                Assert.That(traveled, Is.EqualTo(1.5f).Within(.08f), "봇도 줄 위 정상 3m/s 입력을 사용한다.");
                Call(mover, "RequestJump");
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "봇 입력 검사 실제 점프");
                var airborne = Call(brain, "BuildInput");
                Assert.That((float)Get(airborne, "Move"), Is.Zero);
                Assert.That((int)Get(airborne, "AuxDirection"), Is.Zero);
                Assert.IsFalse((bool)Get(airborne, "ActionPressed"));
                Assert.IsFalse((bool)Get(airborne, "InteractTapped"));
                Assert.AreSame(player, Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"));
            }
            finally
            {
                Time.captureDeltaTime = capture;
                Call(scheme, "Exit", context);
                Object.Destroy(scheme);
                DestroyPracticeBotTestPartner(partner);
            }
        }

        [UnityTest]
        public IEnumerator PracticeBotEscapesTheNumberedWarningLaneUsingNormalLaneJump()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            var fire = (Component)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            yield return Until(() => Get(fire, "Settings") != null, 3f, "봇 화재 회차 설정");
            var segments = (Array)Get(Get(fire, "Settings"), "segments");
            int lane = (int)Get(segments.GetValue(0), "lane") - 1;
            yield return StandOnRopeForSawTest(lane, 4f);
            var partner = CreatePracticeBotTestPartner(lane == 0 ? 1 : lane - 1, 8f);
            var brain = Activator.CreateInstance(GameType("CurtainCall.Tightrope.TightropePracticeBotBrain"), new object[] { player, partner });
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            try
            {
                // 원격 프리팹 생성 중 Reader.OnEnable이 공용 입력 해제 대기를 건다.
                // 실제 worker처럼 정상 입력이 재개된 뒤 한 번의 점프 입력을 보낸다.
                yield return Until(() => !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 2f,
                    "동료 생성 뒤 입력 해제 대기 완료");
                Set(fire, "pendingSegment", 0);
                Set(fire, "pendingIgniteAt", (float)Get(fire, "Elapsed") + 20f);
                var input = Call(brain, "BuildInput");
                int direction = (int)Get(input, "AuxDirection");
                Assert.That(direction, Is.EqualTo(lane == 0 ? 1 : -1), "화재 설정의 1부터 시작하는 줄 번호를 실제 0부터 시작하는 줄 번호에 맞춰야 한다.");
                Assert.IsTrue((bool)Get(input, "ActionPressed"));
                Call(scheme, "Tick", context, input);
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "봇 입력이 실제 옆줄 점프를 시작");
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "불 예고 옆줄로 착지");
                var expected = (Vector3)Call(course, "GetLanePosition", lane + direction, 4f);
                Assert.That(Vector3.Distance(player.transform.position, expected), Is.LessThan(.2f));
            }
            finally
            {
                Call(scheme, "Exit", context);
                Object.Destroy(scheme);
                DestroyPracticeBotTestPartner(partner);
            }
        }

        [UnityTest]
        public IEnumerator PracticeBotLowerWalksFromPlatformWhileCarryingPartner()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var position = (Vector3)Call(course, "GetLanePosition", 1, -1f);
            Call(mover, "SetStartPose", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            yield return Until(() => (bool)Get(mover, "FreeMovement") && !(bool)Get(mover, "IsAirborne"), 3f, "봇 아래 플랫폼 출발 위치");
            var partner = CreatePracticeBotTestPartner(1, -2f);
            var stack = (Component)Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
            var brain = Activator.CreateInstance(GameType("CurtainCall.Tightrope.TightropePracticeBotBrain"), new object[] { player, partner });
            Call(brain, "SetRole", Enum.Parse(GameType("CurtainCall.Tightrope.TightropeBotRole"), "Lower"));
            try
            {
                Call(stack, "ServerTryLink", 779UL);
                yield return Until(() => Call(stack, "GetAbove", player) != null, 2f, "출발 플랫폼 위 목마 연결");
                Assert.AreSame(partner, Call(stack, "GetAbove", player));
                Assert.IsTrue((bool)Get(mover, "FreeMovement"));
                var input = Call(brain, "BuildInput");
                Assert.That((float)Get(input, "Move"), Is.EqualTo(1f), "목마 아래 봇은 자기 머리 위 동료를 따라가며 멈추지 않고 줄 입구로 걸어야 한다.");
                Assert.IsFalse((bool)Get(input, "InteractLongPressed"));
            }
            finally { DestroyPracticeBotTestPartner(partner); }
        }

        [UnityTest]
        public IEnumerator PracticeBotChangesUpperRoleByNormalReleaseWithoutRestartingCourse()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var partner = CreatePracticeBotTestPartner(1, 5f);
            var stack = (Component)Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            var brain = Activator.CreateInstance(GameType("CurtainCall.Tightrope.TightropePracticeBotBrain"), new object[] { player, partner });
            var roles = GameType("CurtainCall.Tightrope.TightropeBotRole");
            int round = (int)Get(run, "Round");
            try
            {
                Call(brain, "SetRole", Enum.Parse(roles, "Upper"));
                yield return new WaitForSeconds(.25f);
                var mount = Call(brain, "BuildInput");
                Assert.IsTrue((bool)Get(mount, "InteractTapped"));
                Call(scheme, "Tick", context, mount);
                yield return Until(() => Call(stack, "GetBelow", player) != null, 2f, "봇 위 역할 실제 목마 연결");
                Assert.AreSame(partner, Call(stack, "GetBelow", player));
                Call(brain, "SetRole", Enum.Parse(roles, "Lower"));
                var release = Call(brain, "BuildInput");
                Assert.IsTrue((bool)Get(release, "InteractLongPressed"));
                Call(scheme, "Tick", context, release);
                yield return Until(() => Call(stack, "GetBelow", player) == null, 2f, "역할 변경 기존 하차 경로");
                Assert.That((int)Get(run, "Round"), Is.EqualTo(round), "역할 교대가 코스 전체를 리셋하면 안 된다.");
                Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.GreaterThan(3f));
                // 다음 봇 판단 전에 상대가 바로 위로 올라오는 실제 LAN 메시지 순서를 재현한다.
                Call(stack, "ServerTryLink", 779UL);
                Assert.AreSame(partner, Call(stack, "GetAbove", player));
                var reversed = Call(brain, "BuildInput");
                Assert.That((float)Get(reversed, "Move"), Is.EqualTo(1f),
                    "이전 아래 연결의 해제 대기가 새 위 연결을 막으면 봇이 영원히 정지한다.");
                Assert.IsFalse((bool)Get(reversed, "InteractLongPressed"));
                Call(brain, "Reset");
                Assert.That(Get(brain, "Role").ToString(), Is.EqualTo("Lower"), "R 리셋 뒤에도 선택한 역할을 유지한다.");
            }
            finally
            {
                Call(scheme, "Exit", context);
                Object.Destroy(scheme);
                DestroyPracticeBotTestPartner(partner);
            }
        }

        Component CreatePracticeBotTestPartner(int lane, float distance, ulong owner = 779UL)
        {
            var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
            var instance = Object.Instantiate(prefab);
            var networkObject = instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            Call(networkObject, "SpawnWithOwnership", owner, true);
            var other = instance.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var otherMover = instance.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            var position = (Vector3)Call(course, "GetLanePosition", lane, distance);
            Call(otherMover, "SetStartPose", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            var replica = instance.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform"));
            Call(replica, "SetStateInternal", position, other.transform.rotation, other.transform.localScale, true);
            Set(replica, "InboundState", Get(replica, "OutboundState"));
            Call(replica, "TransformStateUpdate");
            Physics.SyncTransforms();
            // 이 도우미는 이미 배치된 원격 참가자 대역이다. 실제 복귀 통신은 다중 프로세스 회귀 검사에서 검증한다.
            ((System.Collections.IDictionary)Get(run, "placedPlayers"))[other] = (int)Get(other, "RestartVersion");
            return other;
        }

        static void DestroyPracticeBotTestPartner(Component other)
        {
            if (other == null) return;
            var networkObject = other.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            if ((bool)Get(networkObject, "IsSpawned")) Call(networkObject, "Despawn", true);
            else Object.Destroy(other.gameObject);
        }
    }
}
