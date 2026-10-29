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
        long LocalAudienceAmount()
        {
            foreach (var segment in (IEnumerable)Get(Get(rewards, "State"), "Segments"))
                if ((ulong)Get(segment, "ClientId") == (ulong)Get(player, "OwnerClientId"))
                    return (long)Get(segment, "Amount");
            throw new AssertionException("Local reward segment missing");
        }
        IEnumerator PrepareHangRescue()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
        }

        [UnityTest]
        public IEnumerator HangRescue_GenerousReachMatchesHostAndAcceptsMovingInteractThenCancel()
        {
            yield return PrepareHangRescue();
            var helper = SpawnHandholdHelper(1, 6.2f);
            var target = helper.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var hangs = (Component)Static("CurtainCall.Tightrope.TightropeHangSystem", "Current");
            var balance = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            var settings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Hang");
            var origin = player.transform.position;
            try
            {
                yield return null;
                Assert.IsTrue((bool)Call(hangs, "ServerTryHang", target, 6.2f, 1));
                Assert.That((float)Get(settings, "RescueDistance"), Is.EqualTo(2.5f));
                Assert.IsNotNull(Call(hangs, "NearestRescueTarget", player), "이전 1.5m 밖인 2.2m에서도 구조 가능하다.");
                var grip = (Vector3)Call(course, "GetLanePosition", 1, 6.2f);
                player.transform.position = grip - (Vector3)Get(course, "Forward") * 2.6f;
                Assert.IsNull(Call(hangs, "NearestRescueTarget", player));
                Call(hangs, "ServerRescue", Get(player, "OwnerClientId"), Get(target, "OwnerClientId"));
                Assert.IsFalse((bool)Call(hangs, "IsRescuing", player), "호스트도 범위 밖 요청은 거절한다.");
                player.transform.position = origin + Vector3.up * .65f;
                Assert.IsNotNull(Call(hangs, "NearestRescueTarget", player), "발 높이의 작은 오차를 허용한다.");
                player.transform.position = grip - Vector3.up * 1.9f;
                Assert.IsNull(Call(hangs, "NearestRescueTarget", player), "매달린 높이에서는 구조하지 않는다.");
                player.transform.position = origin;
                Set(mover, "<IsAirborne>k__BackingField", true);
                Assert.IsNull(Call(hangs, "NearestRescueTarget", player));
                Call(hangs, "ServerRescue", Get(player, "OwnerClientId"), Get(target, "OwnerClientId"));
                Assert.IsFalse((bool)Call(hangs, "IsRescuing", player));
                Set(mover, "<IsAirborne>k__BackingField", false);
                Call(balance, "SetDisplayedState", true, 80f);
                Set(balance, "<RedTime>k__BackingField", .5f);
                balance.enabled = true;
                Call(mover, "SetMoveInput", 1f);
                Assert.IsTrue((bool)Call(hangs, "TryHandleInput", context, CatchFrame(tap: true, move: 1f)));
                Assert.IsTrue((bool)Call(hangs, "IsRescuing", player));
                Assert.IsTrue((bool)Get(balance, "IsRescueSupported"), "구조를 승인하는 프레임부터 보호한다.");
                yield return new WaitForSeconds(.25f);
                Assert.That((float)Get(balance, "Value"), Is.EqualTo(80f));
                Assert.That((float)Get(balance, "RedTime"), Is.EqualTo(.5f));
                Assert.That(Vector3.Distance(player.transform.position, origin), Is.LessThan(.05f), "구조 시작 프레임의 이동 입력도 지운다.");
                var cancel = CatchFrame(); Set(cancel, "InteractLongPressed", true);
                Assert.IsTrue((bool)Call(hangs, "TryHandleInput", context, cancel));
                Assert.IsFalse((bool)Call(hangs, "IsRescuing", player));
                Assert.IsFalse((bool)Get(balance, "IsRescueSupported"));
                Call(balance, "Step", .1f);
                Assert.That((float)Get(balance, "RedTime"), Is.GreaterThan(.5f), "취소하면 일반 균형·위험 계산을 복원한다.");
            }
            finally
            {
                balance.enabled = false;
                if (helper != null) Call(helper, "Despawn", true);
            }
        }

        [UnityTest]
        public IEnumerator HangRescue_ProtectsThroughFullPullAndReleasesOnCompletion()
        {
            yield return PrepareHangRescue();
            var helper = SpawnHandholdHelper(1, 6.2f);
            var target = helper.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var hangs = (Component)Static("CurtainCall.Tightrope.TightropeHangSystem", "Current");
            var balance = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            try
            {
                yield return null;
                Assert.IsTrue((bool)Call(hangs, "ServerTryHang", target, 6.2f, 1));
                Call(balance, "SetDisplayedState", true, 90f);
                Set(balance, "<RedTime>k__BackingField", .5f);
                balance.enabled = true;
                long beforeRescue = LocalAudienceAmount();
                Call(hangs, "ServerRescue", Get(player, "OwnerClientId"), Get(target, "OwnerClientId"));
                Assert.IsTrue((bool)Get(balance, "IsRescueSupported"));
                Assert.AreEqual(beforeRescue, LocalAudienceAmount(), "구조 시작에는 지급하지 않는다.");
                yield return new WaitForSeconds(.3f);
                Assert.That((float)Get(balance, "Value"), Is.EqualTo(90f));
                Assert.That((float)Get(balance, "RedTime"), Is.EqualTo(.5f));
                Assert.IsFalse((bool)Get(balance, "HasFallen"));
                yield return Until(() => !(bool)Call(hangs, "IsRescuing", player), 4f, "동료 끌어올리기 완료");
                Assert.IsFalse((bool)Call(hangs, "IsHanging", target));
                Assert.AreEqual(beforeRescue + 10, LocalAudienceAmount(), "실제 구조 완료에 협동 환호를 한 번 지급한다.");
                Assert.IsFalse((bool)Get(balance, "IsRescueSupported"));
                Assert.IsFalse((bool)Get(balance, "HasFallen"));
                Call(balance, "Step", .1f);
                Assert.That((float)Get(balance, "RedTime"), Is.GreaterThan(.5f));
            }
            finally
            {
                balance.enabled = false;
                if (helper != null) Call(helper, "Despawn", true);
            }
        }

        [UnityTest]
        public IEnumerator HangRescue_ReleasesSupportOnRangeLossTargetRemovalAndDisable()
        {
            yield return PrepareHangRescue();
            var helper = SpawnHandholdHelper(1, 6.2f);
            var target = helper.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            var hangs = (Behaviour)Static("CurtainCall.Tightrope.TightropeHangSystem", "Current");
            var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            var origin = player.transform.position;
            try
            {
                yield return null;
                Assert.IsTrue((bool)Call(hangs, "ServerTryHang", target, 6.2f, 1));
                void StartPull()
                {
                    Call(hangs, "ServerRescue", Get(player, "OwnerClientId"), Get(target, "OwnerClientId"));
                    Assert.IsTrue((bool)Get(balance, "IsRescueSupported"));
                }
                StartPull();
                player.transform.position = origin - (Vector3)Get(course, "Forward") * 5f;
                Call(hangs, "ServerTick", .01f);
                Assert.IsFalse((bool)Get(balance, "IsRescueSupported"));
                player.transform.position = origin;
                StartPull();
                hangs.enabled = false;
                Assert.IsFalse((bool)Get(balance, "IsRescueSupported"));
                hangs.enabled = true; yield return null;
                Assert.IsTrue((bool)Call(hangs, "ServerTryHang", target, 6.2f, 1));
                StartPull();
                Call(helper, "Despawn", true); helper = null;
                Call(hangs, "ServerTick", .01f);
                Assert.IsFalse((bool)Get(balance, "IsRescueSupported"));
            }
            finally { if (helper != null) Call(helper, "Despawn", true); }
        }
    }
}
