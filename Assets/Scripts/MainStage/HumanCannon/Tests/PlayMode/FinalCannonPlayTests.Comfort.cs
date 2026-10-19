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
            yield return WaitUntil(() => !(bool)Get(player, "IsAirborne"), 3, "일반 점프 착지");
            yield return null;
            AssertMarkerHidden(Actor);

            yield return Board(6);
            AssertMarkerHidden(Actor);
            yield return ReleaseThenArm(); Input(tapped: true);
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
            var fires = Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonFireWall"), FindObjectsSortMode.None);
            Assert.AreEqual(2, fires.Length);
            foreach (var item in fires)
            {
                Assert.IsTrue((bool)Get(item, "Available"));
                Assert.AreEqual(30f, Get(item, "warningSeconds"));
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
            // 서버 시계가 실제로 30초 흐르는 동안 예고만 표시하고 전진/접촉 피해는 시작하지 않는다.
            while ((double)Get(route, "Clock") < armed + 30)
            {
                double clock = (double)Get(route, "Clock");
                Assert.IsFalse((bool)Call(fire, "Advancing", clock));
                Assert.IsFalse((bool)Call(fire, "Threatens", start, 0.3f, clock));
                Assert.That(Vector3.Distance((Vector3)Call(fire, "Front", clock), start), Is.LessThan(0.001f));
                Assert.IsTrue(((Transform)Get(fire, "visual")).gameObject.activeSelf);
                yield return null;
            }
            yield return WaitUntil(() => (double)Get(route, "Clock") >= armed + 30.3, 2, "30초 뒤 불꽃 전진");
            double now = (double)Get(route, "Clock");
            Assert.IsTrue((bool)Call(fire, "Advancing", now));
            Assert.That(Vector3.Distance((Vector3)Call(fire, "Front", now), start),
                Is.EqualTo((now - armed - 30) * 0.7).Within(0.002), "전진 속도는 기존 초당 0.7m");
            Assert.IsTrue((bool)Call(fire, "Threatens", start, 0.3f, now));
            Call(player, "RequestRestartAll");
            yield return null; yield return null;
            AssertFiresIdle();
            Assert.AreEqual("대기", Call(route, "FireStatus", 0));
            foreach (var item in fires) Assert.IsFalse(((Transform)Get(item, "visual")).gameObject.activeSelf);
            yield return WaitUntil(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 3, "재시작 후 입력 해제");
            yield return Board(3);
            yield return ReleaseThenArm(); Input(tapped: true);
            Assert.IsTrue((bool)Get(second, "Armed"), "두 번째 구간도 독립적인 출발 시점에 예고 시작");
            Assert.IsFalse((bool)Get(fire, "Armed"));
            Assert.That((double)Call(second, "RemainingWarning", (double)Get(route, "Clock")), Is.GreaterThan(29.5));
        }

        Component GroundMarkers() => Object.FindAnyObjectByType(GameType("CurtainCall.HumanCannon.HumanCannonGroundMarkers")) as Component;
        Transform GroundMarker(ulong actor) => GroundMarkers().transform.Find("GroundMarker_" + actor);
        bool MarkerVisible(ulong actor)
        {
            var marker = GroundMarker(actor);
            return marker != null && marker.gameObject.activeInHierarchy;
        }
        void AssertMarkerHidden(ulong actor) => Assert.IsFalse(MarkerVisible(actor));
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
