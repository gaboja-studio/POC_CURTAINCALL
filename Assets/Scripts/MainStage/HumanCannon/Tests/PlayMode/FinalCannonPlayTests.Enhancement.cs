using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        // 기존 LAN 테스트와 같이 입력 프레임을 주입한다. 실제 키보드·마우스 검사는 아니다.
        // 잡기 직전 위치만 준비하고 점유, 반동, 시간 경과, 톱 접촉은 실제 호스트 업데이트로 확인한다.
        [UnityTest]
        public IEnumerator TimedLinkedPole_ReleaseKeepsTheWallOpenUntilFortySeconds()
        {
            DisableEnhancementThreats();
            const int index = 1; // 기존 첫 구간 관문 해제봉
            double grabRequestedAt = 0;
            yield return GrabEnhancementPole(index, time => grabRequestedAt = time);
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", index);
            var state = Call(poles, "State", index);
            var gate = Get(pole, "LinkedGate");
            Assert.IsNotNull(gate);
            foreach (Transform blade in ((Component)gate).transform.Find("SawVisual"))
                Assert.That(Vector3.Dot(blade.forward, Vector3.right), Is.GreaterThan(.999f),
                    "실제 호스트의 관문 업데이트 후에도 개별 톱날90도 방향을 유지한다.");
            Assert.IsFalse((bool)Get(state, "IsUnlimitedHold"));
            double rearmAt = (double)Get(state, "RearmAt");
            // 잡기 확인 뒤 프레임 대기 시간이 남은 40초에 포함되지 않도록 요청 시각으로 비교한다.
            Assert.That(rearmAt, Is.InRange(grabRequestedAt + 40d, (double)Get(poles, "Clock") + 40d));
            var barrier = ((Component)gate).transform.Find("Barrier").GetComponent<Collider>();
            var fence = barrier.transform.Find("MetalMesh");
            Assert.That(fence.childCount, Is.EqualTo(11));
            Assert.IsFalse(barrier.enabled);
            Assert.IsTrue(fence.GetComponentsInChildren<Renderer>().All(r => !r.enabled));
            double started = (double)Get(poles, "Clock");
            while ((double)Get(poles, "Clock") - started < 8.5)
            {
                Assert.AreEqual(index, Get(pilot, "HeldPole"), "기존 8초가 지나도 자동으로 손을 놓지 않는다.");
                Assert.AreEqual("Retracted", Get(gate, "Phase").ToString(), "협동 관문 정지는 점유 중 유지한다.");
                AssertBody(0);
                yield return null;
            }
            PoleEnhancementInput(0, release: true);
            Assert.AreEqual(-1, Get(pilot, "HeldPole"));
            Assert.IsTrue(Flying, "Space로 놓으면 기존 도약 비행을 시작한다.");
            Assert.IsFalse((bool)Get(state, "IsOccupied"));
            Assert.AreEqual("Retracted", Get(state, "Phase").ToString());
            Assert.That((double)Get(state, "RearmAt"), Is.EqualTo(rearmAt), "놓아도 잡은 순간의 40초 시각을 보존한다.");
            AssertBody(0);
            while ((double)Get(poles, "Clock") < rearmAt - 2d)
            {
                Assert.IsFalse(barrier.enabled);
                Assert.IsTrue(fence.GetComponentsInChildren<Renderer>().All(r => !r.enabled));
                yield return null;
            }
            yield return null;
            Assert.AreEqual("Warning", Get(gate, "Phase").ToString());
            Assert.IsFalse(barrier.enabled);
            yield return WaitUntil(() => Get(gate, "Phase").ToString() == "Active", 3f, "40초 뒤 재가동");
            Assert.That((double)Get(poles, "Clock"), Is.GreaterThanOrEqualTo(rearmAt));
            Assert.IsTrue(barrier.enabled);
            Assert.IsTrue(fence.GetComponentsInChildren<Renderer>().All(r => r.enabled));
            Assert.That(barrier.GetComponentsInChildren<Collider>().Count(c => c.enabled), Is.EqualTo(1), "기존 물리 충돌벽만 사용한다.");
        }

        [UnityTest]
        public IEnumerator AirbornePlayerCrossesTheGateBeforeTheTrampolineCarrier()
        {
            DisableEnhancementThreats();
            yield return GrabEnhancementPole(1);
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", 1);
            var gate = (Component)Get(pole, "LinkedGate");
            Assert.IsFalse((bool)Get(gate, "CarrierPassed"));
            PoleEnhancementInput(0, release: true);
            Vector3 origin = gate.transform.position + Vector3.up * 8f - gate.transform.forward;
            Call(pilot, "Launch", 0, Get(pilot, "launchRevision"), origin, gate.transform.rotation, gate.transform.forward * 6f);
            yield return WaitUntil(() => Vector3.Dot(player.transform.position - gate.transform.position, gate.transform.forward) > .5f,
                2f, "운반자 통과 전에도 물리 벽 위로 실제 비행 통과");
            Assert.IsFalse((bool)Get(gate, "CarrierPassed"));
            AssertBody(0);
        }

        [UnityTest]
        public IEnumerator PoleSaw_ActualSwingAvoidsContact_ThenCrossingCutsWithoutForcingRelease()
        {
            DisableEnhancementThreats();
            const int index = 1;
            yield return GrabEnhancementPole(index);
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", index);
            var anchor = Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonPoleSaw"), FindObjectsSortMode.None)
                .Cast<Component>().Single(item => (Component)Get(item, "Pole") == pole);
            var saw = (Behaviour)Get(anchor, "Hazard");
            // 접촉 여부를 분리해서 재현한다. 왕복 궤도는 EditMode 검사에서 확인하며 여기서는 중앙에 고정한다.
            HazardSet(saw, "travel", Vector3.zero);
            Vector3 rest = (Vector3)Call(pole, "HangingPosition", 0f);
            Vector3 forward = (Vector3)Get(pole, "Forward");
            float deadline = Time.realtimeSinceStartup + 3;
            while (Vector3.Dot(player.transform.position - rest, forward) < 1f && Time.realtimeSinceStartup < deadline)
            {
                PoleEnhancementInput(1);
                yield return null;
            }
            Assert.That(Vector3.Dot(player.transform.position - rest, forward), Is.GreaterThanOrEqualTo(1f),
                "입력으로 실제 몸을 수평 톱날의 앞뒤 접촉 범위 밖으로 움직인다.");
            AssertBody(0);
            saw.enabled = true;
            double safeUntil = (double)Get(poles, "Clock") + 0.4;
            while ((double)Get(poles, "Clock") < safeUntil)
            {
                PoleEnhancementInput(1);
                AssertBody(0);
                yield return null;
            }
            deadline = Time.realtimeSinceStartup + 3;
            while (Convert.ToInt32(Get(player, "LostParts")) == 0 && Time.realtimeSinceStartup < deadline)
            {
                PoleEnhancementInput(-1);
                yield return null;
            }
            AssertBody(4); // 공용 순서의 첫 부위인 왼다리. 접촉 피해 함수를 직접 호출하지 않는다.
            Assert.AreEqual(index, Get(pilot, "HeldPole"), "부상만 입고 살아 있으면 계속 매달린다.");
            Assert.IsFalse((bool)Get(Call(poles, "State", index), "IsUnlimitedHold"));
            Assert.AreEqual("Retracted", Get(Get(pole, "LinkedGate"), "Phase").ToString(),
                "새 공중 톱의 부상이 지상 관문을 임의로 재가동하지 않는다.");
        }


        [UnityTest]
        public IEnumerator PoleGrab_ExpandedReachAcceptsNearMiss_ButRejectsBeyondThreeMeters()
        {
            DisableEnhancementThreats();
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", 1);
            var grip = (Transform)Get(pole, "Grip");
            yield return Board(0);
            yield return ReleaseThenArm();
            Input(tapped: true);
            var hang = (Vector3)Call(pole, "HangingPosition", 0f);
            Call(pilot, "Launch", 0, Get(pilot, "launchRevision"), hang + Vector3.right * 3.25f, grip.rotation, Vector3.zero);
            Input(tapped: true);
            yield return null;
            Assert.AreEqual(-1, Get(pilot, "HeldPole"), "3m 밖의 다른 봉을 원격으로 잡지 않는다.");
            Call(pilot, "Launch", 0, Get(pilot, "launchRevision"), hang + Vector3.right * 2.75f, grip.rotation, Vector3.zero);
            Input(tapped: true);
            yield return WaitUntil(() => (int)Get(pilot, "HeldPole") == 1, 2, "expanded host grab reach");
            Assert.AreEqual(Actor, Get(Call(poles, "State", 1), "Occupant"));
        }


        IEnumerator GrabEnhancementPole(int index, Action<double> onGrabRequest = null)
        {
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", index);
            Assert.IsNotNull(pole);
            yield return Board(0);
            yield return ReleaseThenArm();
            Input(tapped: true);
            Assert.IsTrue(Flying);
            // 호스트가 승인한 발사 식별자를 유지한 채 접근 위치만 준비한다.
            var grip = (Transform)Get(pole, "Grip");
            Call(pilot, "Launch", 0, Get(pilot, "launchRevision"), Call(pole, "HangingPosition", 0f), grip.rotation, Vector3.zero);
            onGrabRequest?.Invoke((double)Get(poles, "Clock"));
            Input(tapped: true);
            yield return WaitUntil(() => (int)Get(pilot, "HeldPole") == index, 2, "봉 잡기 호스트 확정");
            yield return null;
            Assert.AreEqual(Actor, Get(Call(poles, "State", index), "Occupant"));
        }

        void DisableEnhancementThreats()
        {
            foreach (Behaviour hazard in Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonHazard"), FindObjectsSortMode.None))
                hazard.enabled = false;
            foreach (Behaviour wind in Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonWindZone"), FindObjectsSortMode.None))
                wind.enabled = false;
            foreach (Component fire in Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonFireWall"), FindObjectsSortMode.None))
                HazardSet(fire, "killOnContact", false);
        }

        void PoleEnhancementInput(float move, bool release = false)
        {
            var frame = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
            frame.GetType().GetField("Move").SetValue(frame, move);
            frame.GetType().GetField("ActionPressed").SetValue(frame, release);
            Call(pilot, "ReadInput", frame);
        }
    }
}
