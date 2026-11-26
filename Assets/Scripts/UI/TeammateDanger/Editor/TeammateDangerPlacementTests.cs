using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.UI.TeammateDanger.EditorTools
{
    /// <summary>화면 밖 위험한 동료 원형 아이콘의 가장자리 자리·화살촉 방향(2026-11-26 M3).</summary>
    public sealed class TeammateDangerPlacementTests
    {
        static readonly Vector2 Screen = new(1920, 1080);

        [Test]
        public void 오른쪽_화면_밖이면_오른쪽_여백_안쪽에_놓고_오른쪽을_가리킨다()
        {
            var place = TeammateDangerIndicators.EdgePlacement(new Vector3(4000, 540, 10), Screen, 48, 175);
            Assert.AreEqual(1920 - 48, place.position.x, .01f);
            Assert.AreEqual(540, place.position.y, .01f);
            Assert.AreEqual(-90f, place.degrees, .01f, "위(0도)에서 시계 방향 90도 = 오른쪽");
        }

        [Test]
        public void 아래쪽은_아래_HUD를_피하는_아래_여백을_쓴다()
        {
            var place = TeammateDangerIndicators.EdgePlacement(new Vector3(960, -3000, 10), Screen, 48, 175);
            Assert.AreEqual(175, place.position.y, .01f);
            Assert.AreEqual(960, place.position.x, .01f);
            Assert.AreEqual(180f, Mathf.Abs(Mathf.DeltaAngle(0f, place.degrees)), .01f, "아래를 가리킨다");
        }

        [Test]
        public void 카메라_뒤에_있으면_투영이_뒤집히므로_반대쪽_가장자리에_놓는다()
        {
            // 카메라 뒤(z<0)에서 화면 위쪽으로 투영된 점 = 실제로는 아래쪽(뒤쪽) 방향.
            var place = TeammateDangerIndicators.EdgePlacement(new Vector3(960, 900, -5), Screen, 48, 175);
            Assert.AreEqual(175, place.position.y, .01f);
        }

        [Test]
        public void 대각선이면_먼저_닿는_가장자리에서_멈춘다()
        {
            var place = TeammateDangerIndicators.EdgePlacement(new Vector3(960 + 5000, 540 + 5000, 10), Screen, 48, 175);
            Assert.AreEqual(1080 - 48, place.position.y, .01f, "위쪽 여백이 먼저 닿는다");
            Assert.Less(place.position.x, 1920 - 48 + .01f);
        }
    }
}
