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
        public IEnumerator ManualCatchInputAndLegacyRequestsStayDisabledAcrossRestart()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var catches = (Component)Static("CurtainCall.Tightrope.TightropeCatchSystem", "Current");
            Assert.IsNotNull(catches);
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            try
            {
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero, "빈 자리 F는 폐기된 수동 준비를 켜지 않는다.");
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero, "F를 다시 누르면 취소한다.");

                var request = catches.GetType().GetNestedType("Request", BindingFlags.NonPublic);
                Call(catches, "ServerHandle", (ulong)Get(player, "OwnerClientId"), Enum.Parse(request, "Prepare"), 0UL);
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero, "옛 클라이언트의 준비 요청도 거절한다.");
                Call(scheme, "Tick", context, CatchFrame(move: 1f));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero, "W 입력이 준비를 취소한다.");
                Call(scheme, "Tick", context, CatchFrame());

                Call(scheme, "Tick", context, CatchFrame(jump: true));
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero, "Space 입력이 준비를 취소한다.");
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "취소 후 실제 점프");
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "준비 취소 검사 착지");

                int oldRound = (int)Get(run, "Round");
                var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
                Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
                yield return Until(() => (int)Get(run, "Round") > oldRound, 5f, "받기 준비 포함 전체 리셋");
                Assert.That((float)Get(catches, "LocalReadyRemaining"), Is.Zero);
                Assert.IsNull(Get(catches, "LocalFeedback"));
            }
            finally
            {
                Call(scheme, "Exit", context);
                Object.Destroy(scheme);
            }
        }

        [UnityTest]
        public IEnumerator RescueGlovesTwoSpawnedPlayersReduceLandingShockWithoutAutomaticMount()
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
                // Outside passive handhold's 1m, inside the glove's 2m: exercise the item effect itself.
                var destination = (Vector3)Call(course, "GetLanePosition", 2, 5.5f);
                Call(helperMover, "SetStartPose", destination, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
                // Feed a stationary owner snapshot through NGO's receive path. A transform-only
                // teleport would be overwritten next frame by the original spawn snapshot.
                var replica = instance.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform"));
                Call(replica, "SetStateInternal", destination, helper.transform.rotation, helper.transform.localScale, true);
                Set(replica, "InboundState", Get(replica, "OutboundState"));
                Call(replica, "TransformStateUpdate");
                ((IDictionary)Get(run, "placedPlayers"))[helper] = (int)Get(helper, "RestartVersion");
                Call(instance.GetComponent(GameType("CurtainCall.Player.PlayerBalance")), "SetDisplayedState", true, 0f);
                Physics.SyncTransforms();
                yield return null;
                var request = catches.GetType().GetNestedType("Request", BindingFlags.NonPublic);
                Assert.That(Vector3.Distance(helper.transform.position, destination), Is.LessThan(.01f), "합성 소유자 위치가 NGO 수신 경로에서도 유지되어야 한다.");
                GiveCatchGloves(helper);
                Assert.IsTrue((bool)Call(catches, "IsGloveCatcher", helper), "구조 장갑을 가진 줄 위 동료만 자동으로 받아준다.");
                float reduction = (float)Get(catches, "CatchReduction");
                Assert.That(reduction, Is.EqualTo(.3f).Within(.0001f));
                object[] activeRange = { helper, 0, 0f, 0f };
                Assert.IsFalse((bool)Call(catches, "TryGetCatchRange", activeRange), "자동 장비가 수동 준비 띠를 만들지 않는다.");
                yield return null;
                yield return null;
                var poseView = catches.GetComponent(GameType("CurtainCall.Tightrope.TightropeCatchPoseView"));
                Assert.That((int)Get(poseView, "PosedPlayerCount"), Is.Zero, "성공 전에는 폐기된 수동 준비 자세를 표시하지 않는다.");
                if (System.IO.File.Exists(".harness/local/tightrope-feedback-capture"))
                {
                    ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/tightrope-ready.png"));
                    yield return null;
                }

                long beforeCatch = LocalAudienceAmount();
                Call(mover, "SetMoveInput", 0f);
                Call(mover, "RequestLaneJump", 1);
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "받기 준비 동료 쪽으로 실제 옆줄 점프");
                Assert.That((float)Get(balance, "laneLandingReduction"), Is.EqualTo(reduction).Within(.0001f));
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "받아주기 실제 착지");
                yield return Until(() => (float)Call(catches, "SuccessRemaining", player) > 0, 1f, "호스트가 장갑 받기를 확정");
                Assert.AreEqual(beforeCatch + 10, LocalAudienceAmount(), "동료 받기는 실제 착지 성공 때 협동 환호를 지급한다.");
                Assert.That(Mathf.Abs((float)Get(balance, "LastShock")),
                    Is.EqualTo((float)Get(settings, "LaneLandingShock") * (1f - reduction)).Within(.001f));
                Assert.That((float)Get(balance, "LaneBoostMultiplier"),
                    Is.EqualTo(1f + ((float)Get(settings, "LaneSwayBoost") - 1f) * (1f - reduction)).Within(.001f));
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

                Call(mover, "SetStartPose", helper.transform.position - (Vector3)Get(course, "Forward") * .5f,
                    Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
                yield return Until(() => !(bool)Get(player, "IsAirborne") && ((CharacterController)Get(mover, "Body")).isGrounded,
                    2f, "목마 요청 전 접지 동기화");
                // Cursor/interaction targeting is covered by its own tests. Exercise the explicit validated mount request here.
                // The synthetic owner has no process to send balanceActive; apply its received display state at this boundary.
                Call(instance.GetComponent(GameType("CurtainCall.Player.PlayerBalance")), "SetDisplayedState", true, 0f);
                Assert.IsTrue((bool)Call(course, "UsesRopeRules", player));
                Assert.IsTrue((bool)Call(course, "UsesRopeRules", helper));
                Assert.IsNotNull(Call(stack, "FindMountTarget", player));
                StringAssert.StartsWith("연결:", (string)Call(stack, "ServerTryLink", (ulong)Get(player, "OwnerClientId")));
                Assert.IsTrue(InStack(player));
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

        Component catchEquipmentFixture;
        void GiveCatchGloves(Component owner)
        {
            var carry = Static("CurtainCall.WaitingRoom.PropCarry", "Instance") as Component;
            if (carry == null) carry = catchEquipmentFixture = new GameObject("Catch equipment fixture").AddComponent(GameType("CurtainCall.WaitingRoom.PropCarry"));
            var item = Activator.CreateInstance(GameType("CurtainCall.WaitingRoom.PropItem"), 99001, "rescue-gloves", "rescue-gloves", 5);
            Assert.AreEqual("None", Call(carry, "Receive", (ulong)Get(owner, "OwnerClientId"), item).ToString());
        }
    }
}
