using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// Task-092 공연용 장비 효과(실제 캠페인 호스트로 공연 씬 진입, 대기실 소지 장부 유지).
    /// 스프링 신발: 다칠 바닥 착지 → 부상 대신 반동·1 소모, 쿨타임 없이 다시 반동하고 다 쓰면 기존 부상(2026-11-02 내구도 2).
    /// 광대 벌룬 바지: 하강 변위 30% 감소, 하강이 끝나면 1 소모.
    /// 끈끈이 장갑: 외줄 추락 매달림 7초 → 10.5초, 추가 시간에 들어선 매달림이 끝나면 1 소모.
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        object CarryItem(object carry, ulong me, string id) =>
            ((IEnumerable)Call(carry, "Held", me)).Cast<object>().FirstOrDefault(i => (string)Get(i, "ItemId") == id);

        int UsesOf(object carry, ulong me, string id) { var item = CarryItem(carry, me, id); return item == null ? 0 : (int)Get(item, "Uses"); }

        void Give(object carry, ulong me, int uid, string id, int uses) =>
            Assert.AreEqual("None", Call(carry, "Receive", me, Activator.CreateInstance(T("CurtainCall.WaitingRoom.PropItem"), uid, id, id, uses)).ToString());

        [UnityTest, Category("CommonRulesFollowup")]
        public IEnumerator Performance_SpringShoesReboundInsteadOfFallInjuryWithoutCooldown()
        {
            yield return Launch(2, "Performance", 1, "HumanCannon", "HumanCannon");
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            ulong me = (ulong)Get(player, "OwnerClientId");
            Give(carry, me, 9201, "spring-shoes", 2);
            var system = Find("CurtainCall.HumanCannon.HumanCannonSystem");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            // 대포 탑승 자리는 안전 착지면일 수 있다. 추락 부상이 나는 바닥(안전 착지면 아님·코스 아래 아님)을 찾는다.
            var safeSurface = system.GetType().GetMethod("IsSafeLandingSurface", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var protectedZone = T("CurtainCall.HumanCannon.HumanCannonLandingZone").GetMethod("ContainsLanding",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { T("CurtainCall.Player.PlayerMover"), typeof(Vector3) }, null);
            float injuryHeight = (float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "BodyDamage"), "FallInjuryHeight");
            var belowCourse = T("CurtainCall.HumanCannon.HumanCannonRouteSystem").GetMethod("IsBelowCourse");
            Vector3? found = null;
            for (int s = 0; s < (int)Get(system, "StationCount") && found == null; s++)
            {
                var boarding = ((Transform)Get((Component)Call(system, "Station", s), "BoardingPoint")).position;
                for (float radius = 1.5f; radius <= 6f && found == null; radius += 1.5f)
                    for (int a = 0; a < 12 && found == null; a++)
                    {
                        var probe = boarding + Quaternion.Euler(0, a * 30f, 0) * Vector3.forward * radius + Vector3.up * 2f;
                        if (!Physics.Raycast(probe, Vector3.down, out var hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                        if (hit.collider.GetComponentInParent(T("CurtainCall.Player.PlayerMover")) != null) continue;
                        if ((bool)belowCourse.Invoke(null, new object[] { hit.point }) || !(bool)safeSurface.Invoke(null, new object[] { mover, hit.point })) continue;
                        if ((bool)protectedZone.Invoke(null, new object[] { mover, hit.point })) continue;
                        found = hit.point;
                    }
            }
            Assert.IsNotNull(found, "실제 지지 가능한 일반 바닥을 찾아야 한다");
            Call(mover, "SetStartPose", found.Value + Vector3.up * .05f, Quaternion.identity, true);
            yield return new WaitForSecondsRealtime(.3f);
            Vector3 floor = player.transform.position;

            var flying = system.GetType().GetField("flying", AnyInstance).GetValue(system);
            var land = system.GetType().GetMethod("HandleLanding", AnyInstance);
            int rebounds = 0;
            var evt = system.GetType().GetEvent("ServerSpringRebounded");
            Action<ulong> onRebound = _ => rebounds++;
            evt.AddEventHandler(null, onRebound);
            try
            {
                Call(flying, "Begin", me, 1, 900u);
                Call(player, "BeginFallAt", floor + Vector3.up * (injuryHeight + .3f));
                land.Invoke(system, new object[] { player, 1, 900u, 0u, floor });
                yield return null;
                Assert.AreEqual(1, rebounds, "다칠 바닥이면 스프링 신발이 반동한다");
                Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")), "반동하면 추락 부상이 없다");
                Assert.AreEqual(1, UsesOf(carry, me, "spring-shoes"), "실제 반동 때 1 소모");
                Assert.IsTrue((bool)Call(flying, "Matches", me, 1, 900u, 1u), "반동하면 같은 비행이 다음 구간으로 이어진다");

                // 2026-11-02 사용자: 공연 기물 쿨타임 없음. 바로 다음 착지도 반동하고, 마지막 회차 뒤 신발이 사라진다.
                Call(player, "BeginFallAt", floor + Vector3.up * (injuryHeight + .3f));
                land.Invoke(system, new object[] { player, 1, 900u, 1u, floor });
                yield return null;
                Assert.AreEqual(2, rebounds, "쿨타임 없이 바로 다음 착지도 반동한다");
                Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
                Assert.AreEqual(0, UsesOf(carry, me, "spring-shoes"), "새 제품 내구도 2를 다 쓰면 사라진다");

                Call(player, "BeginFallAt", floor + Vector3.up * (injuryHeight + .3f));
                land.Invoke(system, new object[] { player, 1, 900u, 2u, floor });
                yield return null;
                Assert.AreEqual(2, rebounds, "다 쓴 뒤에는 반동하지 않는다");
                Assert.AreNotEqual(0, Convert.ToInt32(Get(player, "LostParts")), "신발이 없으면 기존 추락 부상");
            }
            finally { evt.RemoveEventHandler(null, onRebound); }
        }

        [UnityTest]
        public IEnumerator Performance_BalloonPantsSlowDescentAndUseOncePerFlight()
        {
            yield return Launch(2, "Performance", 1, "HumanCannon", "HumanCannon");
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            ulong me = (ulong)Get(player, "OwnerClientId");
            var system = Find("CurtainCall.HumanCannon.HumanCannonSystem");
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var pilot = player.GetComponent(T("CurtainCall.HumanCannon.HumanCannonPilot"));
            var station = (Component)Call(system, "Station", 1);
            Call(mover, "SetStartPose", ((Transform)Get(station, "BoardingPoint")).position, Quaternion.identity, true);
            yield return new WaitForSecondsRealtime(.3f);
            var launch = pilot.GetType().GetMethod("Launch", AnyInstance);

            // 바지 없이 / 바지 입고 같은 높이에서 같은 시간 떨어진 거리를 비교한다.
            float[] drops = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) Give(carry, me, 9301, "balloon-pants", 4);
                Vector3 start = player.transform.position + Vector3.up * 20f;
                launch.Invoke(pilot, new object[] { -1, 950u + (uint)pass, start, Quaternion.identity, Vector3.zero });
                yield return null;
                float y0 = player.transform.position.y, until = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < until) yield return null;
                drops[pass] = y0 - player.transform.position.y;
                Assert.AreEqual(pass == 1, (bool)Get(pilot, "BalloonPantsActive"), pass == 1 ? "하강 중 바지가 켜진다" : "바지가 없으면 꺼져 있다");
                pilot.GetType().GetMethod("ReleaseMovement", AnyInstance).Invoke(pilot, null);
                yield return null;
            }
            Assert.Greater(drops[0], .5f);
            float ratio = drops[1] / drops[0];
            Assert.That(ratio, Is.InRange(.55f, .85f), $"하강 변위 30% 감소(초안). 바지 없음 {drops[0]:0.00}m, 바지 {drops[1]:0.00}m");
            float wait = Time.realtimeSinceStartup + 2f;
            while (UsesOf(carry, me, "balloon-pants") != 3 && Time.realtimeSinceStartup < wait) yield return null;
            Assert.AreEqual(3, UsesOf(carry, me, "balloon-pants"), "하강이 끝나면 비행당 1 소모");
            Assert.AreEqual(0f, (float)Call(carry, "CooldownLeft", me, "balloon-pants"), "공연 기물 쿨타임 없음(2026-11-02)");
        }

        [UnityTest]
        public IEnumerator Performance_StickyGlovesExtendTightropeHangAndUseAfterExtraTime()
        {
            yield return Launch(3, "Performance", 0, "Tightrope", "Tightrope");
            var carry = Find("CurtainCall.WaitingRoom.PropCarry");
            var player = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
            ulong me = (ulong)Get(player, "OwnerClientId");
            Give(carry, me, 9401, "sticky-gloves", 3);
            var hangs = Static("CurtainCall.Tightrope.TightropeHangSystem", "Current");
            var course = Static("CurtainCall.Tightrope.TightropeCourse", "Current");
            // 실제 줄 위(쓸 수 있는 줄의 앞쪽)로 옮긴 뒤 매달린다.
            var mover = player.GetComponent(T("CurtainCall.Player.PlayerMover"));
            int lanes = (int)Get(course, "LaneCount"); int lane = -1; float along = 6f;
            for (int i = 0; i < lanes && lane < 0; i++) if ((bool)Call(course, "IsLaneUsable", i, along)) lane = i;
            Assert.GreaterOrEqual(lane, 0, "쓸 수 있는 줄");
            Call(mover, "SetStartPose", (Vector3)Call(course, "GetLanePosition", lane, along) + Vector3.up * .02f, Quaternion.identity);
            yield return new WaitForSecondsRealtime(.3f);
            object[] point = { player.transform.position, 0, 0f };
            Assert.IsTrue((bool)course.GetType().GetMethod("TryGetRopePoint").Invoke(course, point), "줄 위");
            Assert.IsTrue((bool)Call(hangs, "ServerTryHang", player, (float)point[2], (int)point[1]), "추락 게이지 넘침 → 매달림");
            var hang = ((IEnumerable)Get(hangs, "Hangs")).Cast<object>().Single(h => (ulong)h.GetType().GetField("Id").GetValue(h) == me);
            double Field2(string name) => (double)hang.GetType().GetField(name).GetValue(hang);
            Assert.AreEqual(10.5d, Field2("Total"), 1e-3, "7초 × 1.5");
            Assert.AreEqual(3.5d, Field2("Extra"), 1e-3);
            Assert.AreEqual(3, UsesOf(carry, me, "sticky-gloves"), "기본 시간 안에서는 소모하지 않는다");

            string cue = null;
            var noticeEvent = carry.GetType().GetEvent("LocalItemNotice");
            Action<string, string, bool> onNotice = (_, c, __) => cue = c;
            noticeEvent.AddEventHandler(carry, onNotice);
            try
            {
                hang.GetType().GetField("Remaining").SetValue(hang, 3.4d); // 기본 7초를 다 쓴 상태
                yield return null; yield return null;
                Assert.IsTrue((bool)hang.GetType().GetField("ExtraEntered").GetValue(hang), "추가 시간에 들어섰다");
                Assert.AreEqual("sticky-gloves", cue, "\"아직 붙어 있다!\" 발동 문구");
                Assert.AreEqual(3, UsesOf(carry, me, "sticky-gloves"), "매달림이 끝나기 전에는 슬롯에 그대로");
                hang.GetType().GetField("Remaining").SetValue(hang, .01d);
                float until = Time.realtimeSinceStartup + 2f;
                while (UsesOf(carry, me, "sticky-gloves") != 2 && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(2, UsesOf(carry, me, "sticky-gloves"), "추가 시간에 들어선 매달림이 끝나면 1 소모");
            }
            finally { noticeEvent.RemoveEventHandler(carry, onNotice); }
        }
    }
}
