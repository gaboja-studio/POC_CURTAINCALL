using CurtainCall.EscapeRoute;
using CurtainCall.Settings;
using NUnit.Framework;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 2026-11-26 Task-205(M3 피드백 리허설·가이드): 새 안내 문구가 조작·역할을 바르게 설명하는지 확인한다.
    /// 화면 배치·노출 시점은 사람 확인 대상이다.
    /// </summary>
    public sealed class M3GuideTextTests
    {
        [Test]
        public void PoleHolderGuide_ExplainsPumpJumpAndRelease()
        {
            var (title, body) = PoleControlGuide.Text(PoleControlGuide.Role.Holder);
            Assert.That(title, Is.Not.Empty);
            StringAssert.Contains("W/S", body);
            StringAssert.Contains("Space", body);
            StringAssert.Contains("F 길게", body);
        }

        [Test]
        public void PoleHangerGuide_FollowsTopRiderOnlySwingSetting()
        {
            var (_, body) = PoleControlGuide.Text(PoleControlGuide.Role.Hanger);
            if (GameSettings.Poles.TopRiderOnlySwing) StringAssert.Contains("위 동료", body);
            else StringAssert.Contains("함께 반동", body);
            StringAssert.Contains("F 길게", body);
        }

        // 2026-11-26 사용자: 기물 첫 획득은 가운데 배너(요약)가 먼저, 배너가 사라지면 왼쪽에 기존 가이드. 기존 안내는 왼쪽에 남는다.
        [TestCase("G05")] [TestCase("G06a")] [TestCase("G06b")] [TestCase("G06c")] [TestCase("S03")] [TestCase("S04")]
        public void ItemUsageTutorials_StayInLeftGuide(string id)
        {
            var cue = WaitingRoomHudProfile.Current.Cue(id);
            Assert.IsNotNull(cue, id);
            Assert.AreEqual(RoomNotice.Guide, cue.role, id);
        }

        [Test]
        public void NoRole_HasNoGuide()
        {
            var (title, body) = PoleControlGuide.Text(PoleControlGuide.Role.None);
            Assert.IsNull(title); Assert.IsNull(body);
        }

        [Test]
        public void SawGuide_SaysItFiresWithTheUseKey()
        {
            var (title, body) = EscapeExitSaw.FireGuideText("휴대 절단기", "좌클릭");
            StringAssert.Contains("휴대 절단기", title);
            StringAssert.Contains("좌클릭", body);
            StringAssert.Contains("발사", body);
            Assert.That(body, Does.Not.Contain("{use}"));
        }

        [TestCase("magician-hairpin")] [TestCase("stake-puller")] [TestCase("uv-lamp")] [TestCase("power-battery")]
        [TestCase("keycard-copy")] [TestCase("video-tape")] [TestCase("video-tape-1")] [TestCase("video-tape-2")]
        [TestCase("rescue-gloves")] [TestCase("sticky-gloves")] [TestCase("balloon-pants")] [TestCase("spring-shoes")]
        public void PickupBanner_HasShortSummary(string id)
        {
            Assert.IsTrue(ItemPickupGuide.IsGuided(id), id);
            var (title, body, controls) = ItemPickupGuide.Banner(id, "기물", "Q");
            StringAssert.Contains("기물", title);
            Assert.That(body, Is.Not.Empty, id);
            Assert.That(body.Split('\n').Length + (controls.Length > 0 ? 1 : 0), Is.LessThanOrEqualTo(ItemPickupGuide.MaxLines), "배너는 최대 4줄");
            Assert.That(body + controls, Does.Not.Contain("{key}").And.Not.Contain("<"), "배너 본문은 서식 태그를 글자로 보여 주므로 태그를 쓰지 않는다");
        }

        [TestCase("route-key")] [TestCase("structure-a:management-key")]
        public void RemovedKeys_HaveNoPickupBanner(string id) => Assert.IsFalse(ItemPickupGuide.IsGuided(id));

        [Test]
        public void PerformancePickupBanner_ShowsSlotKey()
        {
            var (_, body, controls) = ItemPickupGuide.Banner("spring-shoes", "스프링 신발", "E");
            Assert.AreEqual("공중에서 한 번 더 점프합니다.", body, "위 줄은 기물 설명");
            Assert.AreEqual("E: 사용   연습실·공연장에서 사용", controls, "아래 줄 조작법은 항목을 나눠 적는다");
        }
    }
}
