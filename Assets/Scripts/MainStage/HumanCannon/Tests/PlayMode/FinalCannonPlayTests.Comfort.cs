using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator GroundMarker_ShowsOnJumpAndFlight_HidesOnGroundDeathRestartAndVoid()
        {
            DisableEnhancementThreats();
            var markers = GroundMarkers();
            Assert.IsNotNull(markers);
            var material = (Material)Get(markers, "markerMaterial");
            Assert.IsNotNull(material);
            Assert.AreEqual("CurtainCall/HumanCannon/GroundMarker", material.shader.name);
            Assert.IsTrue(material.shader.isSupported);
            Call(mover, "SetStartPose", new Vector3(0, 0.01f, 57), Quaternion.identity, true);
            yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "점프 전 실제 바닥 접촉");
            AssertMarkerHidden(Actor);
            Call(mover, "RequestJump");
            yield return WaitUntil(() => (bool)Get(player, "IsAirborne"), 2, "일반 점프 입력이 실제 이륙을 시작함");
            yield return WaitUntil(() => MarkerVisible(Actor), 2, "일반 지상 점프에도 발밑 원형 표시");
            AssertMarkerUnder(player);
            Assert.That(MarkerOutlineAlpha(Actor), Is.GreaterThan(0), "내 캐릭터 그림자는 선명 높이 아래에서 외곽선을 그린다");
            // 바닥에서 막 뛴 낮은 높이는 안전 체크 구간이다. 발밑이 실제 바닥이므로 안전 색.
            var safeColor = (Color)Get(Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "LandingMarker"), "SafeColor");
            var rim = MarkerOutlineColor(Actor);
            Assert.That(Vector3.Distance(new Vector3(rim.r, rim.g, rim.b), new Vector3(safeColor.r, safeColor.g, safeColor.b)), Is.LessThan(0.01f), "안전 체크 높이 안의 안전 착지는 안전 색");
            yield return WaitUntil(() => !(bool)Get(player, "IsAirborne"), 3, "일반 점프 착지");
            yield return null;
            AssertMarkerHidden(Actor);

            yield return Board(6);
            AssertMarkerHidden(Actor);
            yield return ReleaseThenArm(); Input(tapped: true);
            // The current final cannon points over a void. Isolate the marker's supported
            // surface check using a real landing cushion, then test the void separately below.
            var cushion = GameObject.Find("LandingCushion").GetComponent<BoxCollider>().bounds;
            Call(pilot, "Launch", 6, Get(pilot, "launchRevision"),
                new Vector3(cushion.center.x, cushion.max.y + 5, cushion.center.z), Quaternion.identity, Vector3.zero);
            yield return WaitUntil(() => MarkerVisible(Actor), 2, "대포 비행에도 발밑 원형 표시");
            AssertMarkerUnder(player);
            Assert.IsTrue(Flying);
            Call(player, "ServerKill");
            yield return null; yield return null;
            AssertMarkerHidden(Actor);
            Call(player, "RequestRestartAll");
            yield return WaitUntil(() => Get(player, "State").ToString() == "Normal", 3, "재시작 후 생존");
            yield return null; yield return null;
            AssertMarkerHidden(Actor);

            yield return Board(6);
            yield return ReleaseThenArm(); Input(tapped: true);
            Call(pilot, "Launch", 6, Get(pilot, "launchRevision"), new Vector3(100, 15, 160), Quaternion.identity, Vector3.up);
            yield return null; yield return null;
            Assert.IsTrue(Flying);
            AssertMarkerHidden(Actor);
        }

        [UnityTest]
        public IEnumerator FireWalls_WaitThirtySecondsAfterDeparture_KeepSpeed_AndRestart()
        {
            var fires = Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonFireWall"), FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(2, fires.Length);
            foreach (var item in fires)
            {
                // 2구간 불은 숨김·사용 안 함(2026-11-03 담당자 결정). 1구간 불만 쫓아온다.
                Assert.AreEqual(item.name == "FirstSectionFire", (bool)Get(item, "Available"), item.name);
                // 1구간 불 예고 25초(2026-11-03 담당자 결정). 숨긴 2구간 불은 기존 30초 유지.
                Assert.AreEqual(item.name == "FirstSectionFire" ? 25f : 30f, Get(item, "warningSeconds"), item.name);
                Assert.AreEqual(0.7f, Get(item, "speed"));
            }
            yield return Board(0);
            yield return ReleaseThenArm(); Input(tapped: true);
            Assert.IsTrue(Flying);
            yield return null;
            var fire = System.Array.Find(fires, f => f.name == "FirstSectionFire");
            var second = System.Array.Find(fires, f => f.name == "SecondSectionFire");
            Assert.IsNotNull(fire); Assert.IsNotNull(second);
            Assert.IsTrue((bool)Get(fire, "Armed"));
            Assert.IsFalse((bool)Get(second, "Armed"));
            double armed = (double)Get(fire, "ArmedAt");
            Vector3 start = ((Transform)Get(fire, "start")).position;
            // 서버 시계가 실제로 25초 흐르는 동안 예고만 표시하고 전진/접촉 피해는 시작하지 않는다.
            while ((double)Get(route, "Clock") < armed + 25)
            {
                double clock = (double)Get(route, "Clock");
                Assert.IsFalse((bool)Call(fire, "Advancing", clock));
                Assert.IsFalse((bool)Call(fire, "Threatens", start, 0.3f, clock));
                Assert.That(Vector3.Distance((Vector3)Call(fire, "Front", clock), start), Is.LessThan(0.001f));
                Assert.IsTrue(((Transform)Get(fire, "visual")).gameObject.activeSelf);
                Assert.IsTrue(((Transform)Get(fire, "effect")).gameObject.activeSelf, "예고부터 불꽃 파티클 표시");
                yield return null;
            }
            yield return WaitUntil(() => (double)Get(route, "Clock") >= armed + 25.3, 2, "25초 뒤 불꽃 전진");
            double now = (double)Get(route, "Clock");
            Assert.IsTrue((bool)Call(fire, "Advancing", now));
            Assert.That(Vector3.Distance((Vector3)Call(fire, "Front", now), start),
                Is.EqualTo((now - armed - 25) * 0.7).Within(0.002), "전진 속도는 기존 초당 0.7m");
            Assert.IsTrue((bool)Call(fire, "Threatens", start, 0.3f, now));
            Call(player, "RequestRestartAll");
            yield return null; yield return null;
            AssertFiresIdle();
            Assert.AreEqual("대기", Call(route, "FireStatus", 0));
            foreach (var item in fires) Assert.IsFalse(((Transform)Get(item, "visual")).gameObject.activeSelf);
            Assert.IsFalse(((Transform)Get(fire, "effect")).gameObject.activeSelf, "재시작하면 불꽃 파티클도 숨김");
            yield return WaitUntil(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 3, "재시작 후 입력 해제");
            yield return Board(3);
            yield return ReleaseThenArm(); Input(tapped: true);
            Assert.IsFalse((bool)Get(second, "Armed"), "숨긴 2구간 불은 쉼터 대포 발사에도 예고하지 않는다");
            Assert.IsFalse((bool)Get(fire, "Armed"));
            Assert.IsFalse(((Component)second).gameObject.activeInHierarchy);
            Assert.AreEqual("사용 안 함", Call(route, "FireStatus", 1));
        }

        Component GroundMarkers() => Object.FindAnyObjectByType(GameType("CurtainCall.HumanCannon.HumanCannonGroundMarkers")) as Component;
        Transform GroundMarker(ulong actor) => GroundMarkers().transform.Find("GroundMarker_" + actor);
        bool MarkerVisible(ulong actor)
        {
            var marker = GroundMarker(actor);
            return marker != null && marker.gameObject.activeInHierarchy;
        }
        void AssertMarkerHidden(ulong actor) => Assert.IsFalse(MarkerVisible(actor));
        float MarkerOutlineAlpha(ulong actor) => MarkerOutlineColor(actor).a;
        Color MarkerOutlineColor(ulong actor)
        {
            var block = new MaterialPropertyBlock();
            GroundMarker(actor).GetComponent<MeshRenderer>().GetPropertyBlock(block);
            return block.GetColor("_RimColor");
        }
        void AssertMarkerUnder(Component target)
        {
            // 테스트 코루틴은 원격 위치 Update와 표시 LateUpdate 사이에서 재개될 수 있다.
            Call(GroundMarkers(), "LateUpdate");
            ulong actor = (ulong)Get(target, "OwnerClientId");
            Assert.IsTrue(MarkerVisible(actor));
            var marker = GroundMarker(actor);
            Assert.That(marker.position.x, Is.EqualTo(target.transform.position.x).Within(0.1f));
            Assert.That(marker.position.z, Is.EqualTo(target.transform.position.z).Within(0.1f));
            Assert.That(marker.position.y, Is.LessThan(target.transform.position.y));
            Assert.IsNotNull(marker.GetComponent<MeshRenderer>());
            Assert.IsNull(marker.GetComponent<Collider>(), "표시는 캐릭터/장애물 충돌에 영향을 주지 않는다.");
        }
    }
}
