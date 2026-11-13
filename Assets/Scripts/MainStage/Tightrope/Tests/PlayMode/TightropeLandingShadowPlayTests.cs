using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    /// <summary>외줄 공연에는 씬 전용 표시가 없으므로 공통 착지 그림자(Task-20261113-126)가 그린다.</summary>
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator RopeJump_ShowsCommonLandingShadowWithSafeOutlineOnTheRope()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(2, 4f);
            var markers = Object.FindAnyObjectByType(GameType("CurtainCall.Player.LandingShadowMarkers"), FindObjectsInactive.Include) as Component;
            Assert.IsNotNull(markers, "공통 착지 그림자가 자동 생성되어야 한다");
            Call(mover, "RequestJump");
            yield return Until(() => (bool)Get(mover, "IsAirborne"), 2, "외줄 점프 이륙");
            Transform marker = null;
            yield return Until(() =>
            {
                marker = markers.transform.Find("GroundMarker_" + Get(player, "OwnerClientId"));
                return marker != null && marker.gameObject.activeInHierarchy;
            }, 2, "외줄 점프 중 발밑 그림자");
            Assert.That(marker.position.y, Is.LessThan(player.transform.position.y));
            Assert.That(Vector2.Distance(new Vector2(marker.position.x, marker.position.z),
                new Vector2(player.transform.position.x, player.transform.position.z)), Is.LessThan(0.1f));
            var block = new MaterialPropertyBlock();
            marker.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            var rim = block.GetColor("_RimColor");
            var safe = (Color)Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "LandingMarker"), "SafeColor");
            Assert.That(rim.a, Is.GreaterThan(0), "내 캐릭터 그림자는 외곽선을 그린다");
            Assert.That(block.GetFloat("_ClipMode"), Is.EqualTo(1), "줄처럼 좁은 박스 착지면에서는 면 밖으로 나간 원을 자른다");
            var landing = Get(Static("CurtainCall.Settings.GameSettings", "Base"), "LandingMarker");
            float normalWidth = (float)Get(landing, "OutlineWidth") / marker.lossyScale.x;
            Assert.That(block.GetFloat("_OutlineWidth"), Is.LessThan(normalWidth), "좁은 줄 위에서는 외곽선이 자동으로 얇아진다");
            Assert.That(Vector3.Distance(new Vector3(rim.r, rim.g, rim.b), new Vector3(safe.r, safe.g, safe.b)), Is.LessThan(0.01f),
                "줄 위 낮은 점프는 안전 체크 구간이고 발밑이 줄이므로 안전 색");
        }
    }
}
