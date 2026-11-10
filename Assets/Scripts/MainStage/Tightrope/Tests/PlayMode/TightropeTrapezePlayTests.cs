using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        // 봉 점유·비행 검사는 TightropeCommonPolesPlayTests와 공통 HumanCannon 검사로 이동했다.
        void EnableTrapezeForEntryTest()
        {
            JsonUtility.FromJsonOverwrite("{\"trapezeEnabled\":true}", legacyCourseSettings);
            JsonUtility.FromJsonOverwrite("{\"trapezeEnabled\":true}", Get(course, "Shape"));
            Call(course, "Rebuild");
        }
        [UnityTest, Category("TightropeJourneyRegression")]
        public IEnumerator MountAndForwardJumpInSameFrameStartsFromPartnersHead()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            yield return StandOnRopeForSawTest(1, 8f);
            var partner = CreatePracticeBotTestPartner(1, 8.7f);
            var reader = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"));
            try
            {
                reader.enabled = true;
                yield return Until(() => !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 2f, "키 해제 대기");
                var stack = Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
                var follow = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.PiggybackFollow"));
                Call(stack, "ServerTryLink", (ulong)Get(player, "OwnerClientId"));
                Assert.AreSame(partner, Call(stack, "GetBelow", player));
                Assert.IsNull(Get(follow, "localMover"), "이번 프레임에 처음 승인된 탑승을 재현한다.");
                Set(reader, "<Current>k__BackingField", CatchFrame(move: 1f, jump: true));
                Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), "목마 입력 검사는 입력 잠금 해제 후 실행한다.");
                Call(follow, "LateUpdate");
                reader.enabled = false;
                Assert.IsNull(Call(stack, "GetBelow", player), "같은 프레임의 W+Space도 목마 분리 점프를 해야 한다.");
                Assert.IsTrue((bool)Get(mover, "Simulated"));
                float height = (float)Get(partner.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "BodyHeight");
                Assert.That(player.transform.position.y, Is.EqualTo(partner.transform.position.y + height).Within(.04f));
                var switcher = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.CourseControlSwitcher"));
                Call(switcher, "UpdateSurfaceMode");
                Assert.IsFalse((bool)Get(mover, "FreeMovement"), "운동 Update 직전에도 목마 점프를 플랫폼 착지로 오인하지 않는다.");
                Assert.That(Get(player.GetComponent(GameType("CurtainCall.Player.PlayerController")), "Scheme").GetType().Name,
                    Is.EqualTo("TightropeControlScheme"));
                yield return null;
                Assert.IsTrue((bool)Get(mover, "IsAirborne"));
            }
            finally { reader.enabled = false; DestroyPracticeBotTestPartner(partner); }
        }
    }
}
