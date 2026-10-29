using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator ActiveCatchFTogglesAndMoveJumpExpiryAndResetCancelReadiness()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var catches = (Component)Static("CurtainCall.Tightrope.TightropeCatchSystem", "Current");
            Assert.IsNotNull(catches);
            var settings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Handhold");
            float originalSeconds = (float)Get(settings, "ReadySeconds");
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            try
            {
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.GreaterThan(0f), "F는 빈 자리에서 받기 준비를 시작한다.");
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero, "F를 다시 누르면 취소한다.");

                yield return new WaitForSeconds(.25f); // 재입력 제한 뒤 실제 F 경로를 다시 사용한다.
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.GreaterThan(0f));
                Call(scheme, "Tick", context, CatchFrame(move: 1f));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero, "W 입력이 준비를 취소한다.");
                Call(scheme, "Tick", context, CatchFrame());

                yield return new WaitForSeconds(.25f);
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.GreaterThan(0f));
                Call(scheme, "Tick", context, CatchFrame(jump: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero, "Space 입력이 준비를 취소한다.");
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "취소 후 실제 점프");
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "준비 취소 검사 착지");

                Set(settings, "readySeconds", .3f);
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.GreaterThan(0f));
                yield return Until(() => (float)Get(catches, "LocalReadyRemaining") == 0f, 2f, "준비 시간 만료");
                Set(settings, "readySeconds", originalSeconds);
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.GreaterThan(0f));
                int oldRound = (int)Get(run, "Round");
                var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
                Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
                yield return Until(() => (int)Get(run, "Round") > oldRound, 5f, "받기 준비 포함 전체 리셋");
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero);
                Assert.IsNull(Get(catches, "LocalFeedback"));
            }
            finally
            {
                Set(settings, "readySeconds", originalSeconds);
                Call(scheme, "Exit", context);
                Object.Destroy(scheme);
            }
        }

        [UnityTest]
        public IEnumerator ActiveCatchTwoSpawnedPlayersReduceLandingShockWithoutAutomaticMount()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var catches = (Component)Static("CurtainCall.Tightrope.TightropeCatchSystem", "Current");
            var stack = (Component)Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
            bool InStack(Component who) => Call(stack, "GetBelow", who) != null || Call(stack, "GetAbove", who) != null;
            var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            var settings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "RopeMovement");
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            Component helperObject = null;
            try
            {
                // 한 LAN 호스트 안의 합성 원격 소유 캐릭터. AI/두 번째 클라이언트/실제 원격 입력은 사용하지 않는다.
                var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
                var instance = Object.Instantiate(prefab);
                helperObject = instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                Call(helperObject, "SpawnWithOwnership", 777UL, true);
                var helper = instance.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var helperMover = instance.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                Assert.IsTrue((bool)Get(helper, "IsSpawned"));
                Assert.IsFalse((bool)Get(helper, "IsLocal"));
                Assert.AreSame(player, Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"));
                var destination = (Vector3)Call(course, "GetLanePosition", 2, 4f);
                Call(helperMover, "SetStartPose", destination, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
                // Feed a stationary owner snapshot through NGO's receive path. A transform-only
                // teleport would be overwritten next frame by the original spawn snapshot.
                var replica = instance.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform"));
                Call(replica, "SetStateInternal", destination, helper.transform.rotation, helper.transform.localScale, true);
                Set(replica, "InboundState", Get(replica, "OutboundState"));
                Call(replica, "TransformStateUpdate");
                Physics.SyncTransforms();
                yield return null;
                var request = catches.GetType().GetNestedType("Request", BindingFlags.NonPublic);
                Assert.That(Vector3.Distance(helper.transform.position, destination), Is.LessThan(.01f), "합성 소유자 위치가 NGO 수신 경로에서도 유지되어야 한다.");
                Call(catches, "ServerHandle", 777UL, Enum.Parse(request, "Prepare"), 0UL);
                Assert.IsTrue((bool)Call(catches, "IsReady", helper), "실제 줄 위 동료가 받을 준비를 한다.");
                Assert.That((float)Get(catches, "CatchReduction"), Is.EqualTo(.9f).Within(.0001f));
                object[] activeRange = { helper, 0, 0f, 0f };
                Assert.IsTrue((bool)Call(catches, "TryGetCatchRange", activeRange));
                Assert.That((float)activeRange[3] - (float)activeRange[2], Is.EqualTo(2.5f).Within(.01f), "준비 띠는 실제 능동 받기 범위를 표시한다.");
                yield return null;
                yield return null;
                var poseView = catches.GetComponent(GameType("CurtainCall.Tightrope.TightropeCatchPoseView"));
                Assert.That((int)Get(poseView, "PosedPlayerCount"), Is.GreaterThan(0), "원격 동료의 남은 팔로 받기 자세를 표시한다.");
                if (System.IO.File.Exists(".harness/local/tightrope-feedback-capture"))
                {
                    ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/tightrope-ready.png"));
                    yield return null;
                }

                long beforeCatch = LocalAudienceAmount();
                Call(mover, "SetMoveInput", 0f);
                Call(mover, "RequestLaneJump", 1);
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "받기 준비 동료 쪽으로 실제 옆줄 점프");
                Assert.That((float)Get(balance, "laneLandingReduction"), Is.EqualTo(.9f).Within(.0001f));
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "받아주기 실제 착지");
                yield return Until(() => !(bool)Call(catches, "IsReady", helper), 1f, "호스트가 한 번의 받기를 확정");
                Assert.AreEqual(beforeCatch + 10, LocalAudienceAmount(), "동료 받기는 실제 착지 성공 때 협동 환호를 지급한다.");
                Assert.That(Mathf.Abs((float)Get(balance, "LastShock")),
                    Is.EqualTo((float)Get(settings, "LaneLandingShock") * .1f).Within(.001f));
                Assert.That((float)Get(balance, "LaneBoostMultiplier"),
                    Is.EqualTo(1f + ((float)Get(settings, "LaneSwayBoost") - 1f) * .1f).Within(.001f));
                Assert.IsFalse(InStack(player), "받아주기는 자동 목마 합체와 다르다.");
                Assert.IsFalse(InStack(helper));
                StringAssert.Contains("받아줬어요", (string)Get(catches, "LocalFeedback"));
                Assert.That((float)Call(catches, "SuccessRemaining", player), Is.GreaterThan(0f));
                Assert.That((float)Call(catches, "SuccessRemaining", helper), Is.GreaterThan(0f), "성공 표시는 관찰하는 모두에게 두 참가자를 표시한다.");
                if (System.IO.File.Exists(".harness/local/tightrope-feedback-capture"))
                {
                    ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/tightrope-catch-success.png"));
                    yield return null;
                }
                Assert.AreEqual("Normal", Get(player, "State").ToString());

                Call(scheme, "Tick", context, CatchFrame(tap: true));
                yield return Until(() => InStack(player), 2f, "가까운 동료에게 F를 누르면 기존 목마 연결");
                Assert.AreSame(helper, Call(stack, "GetBelow", player));
            }
            finally
            {
                Call(scheme, "Exit", context);
                Object.Destroy(scheme);
                if (helperObject != null)
                {
                    if ((bool)Get(helperObject, "IsSpawned")) Call(helperObject, "Despawn", true);
                    else Object.Destroy(helperObject.gameObject);
                }
            }
        }

        static object CatchFrame(bool tap = false, float move = 0f, bool jump = false)
        {
            var frame = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
            Set(frame, "InteractTapped", tap);
            Set(frame, "Move", move);
            Set(frame, "ActionPressed", jump);
            return frame;
        }
    }
}
