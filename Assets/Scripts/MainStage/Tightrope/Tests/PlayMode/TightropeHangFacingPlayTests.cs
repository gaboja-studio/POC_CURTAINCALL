using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        /// <summary>
        /// 2026-11-01 버그: 외줄 매달림 중 흔들림마다 캐릭터가 오른쪽 ↔ 왼쪽을 번갈아 봤다(맨 왼쪽 줄에서 발견).
        /// 모든 줄에서 매달린 동안 한쪽(줄에 수직)을 계속 보고, 가운데가 아닌 줄은 코스 가운데 쪽을 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator HangFacing_EveryLaneStaysOnOneSideFacingCourseCenter()
        {
            yield return PrepareHangRescue();
            var hangs = (Component)Static("CurtainCall.Tightrope.TightropeHangSystem", "Current");
            var presentation = player.GetComponent(GameType("CurtainCall.Player.HumanoidPlayerPresentation"));
            Assert.IsNotNull(presentation, "휴머노이드 외형");
            int lanes = (int)Get(course, "LaneCount");
            ulong id = (ulong)Get(player, "OwnerClientId");
            for (int lane = 0; lane < lanes; lane++)
            {
                // 앞 줄 매달림을 풀고 떨어지기 전에(같은 프레임) 다음 줄에 매단다.
                if (lane > 0) Call(hangs, "ServerReleaseForInspection", id);
                Assert.IsTrue((bool)Call(hangs, "ServerTryHang", player, 6.2f, lane), $"{lane}번 줄 매달림");
                Assert.AreEqual(lane, (int)Get(FindHang(hangs, id), "Lane"), $"{lane}번 줄을 골랐다");
                yield return Until(() => (bool)Get(presentation, "IsHanging"), 2, $"{lane}번 줄 매달림 표현");
                Vector3 courseForward = (Vector3)Get(course, "Forward");
                Vector3 first = Vector3.zero;
                for (int frame = 0; frame < 45; frame++)
                {
                    yield return new WaitForEndOfFrame(); // 표현기의 LateUpdate 보정 뒤 모델 방향
                    if (!(bool)Get(presentation, "IsHanging")) break;
                    var model = (Component)Get(presentation, "Model");
                    Vector3 facing = Vector3.ProjectOnPlane(model.transform.forward, Vector3.up).normalized;
                    Assert.That(Mathf.Abs(Vector3.Dot(facing, courseForward)), Is.LessThan(.4f), $"{lane}번 줄: 줄에 수직으로 매달린다");
                    if (frame == 0) first = facing;
                    Assert.That(Vector3.Dot(facing, first), Is.GreaterThan(.8f), $"{lane}번 줄 {frame}프레임: 바라보는 쪽이 뒤집히지 않는다");
                }
                Assert.AreNotEqual(Vector3.zero, first, $"{lane}번 줄: 매달린 프레임을 확인했다");
                float side = (float)Call(course, "GetSide", player.transform.position);
                if (Mathf.Abs(side) > .05f)
                {
                    Vector3 toCenter = Vector3.Cross(Vector3.up, courseForward) * -Mathf.Sign(side);
                    Assert.That(Vector3.Dot(first, toCenter), Is.GreaterThan(.5f), $"{lane}번 줄: 코스 가운데 쪽을 본다");
                }
            }
            Call(hangs, "ServerReleaseForInspection", id);
        }

        static object FindHang(Component hangs, ulong id)
        {
            foreach (var hang in (IEnumerable)Get(hangs, "Hangs"))
                if ((ulong)Get(hang, "Id") == id) return hang;
            throw new AssertionException("매달림 장부에 없음");
        }
    }
}
