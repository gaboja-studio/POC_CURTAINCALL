using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        /// <summary>실제 1인 호스트의 매달림 상태를 별도 수신 목록에 복원한다. 두 클라이언트 연결 검사는 아니다.</summary>
        [UnityTest]
        public IEnumerator ActivePoleFlightSnapshot_PreservesIdentity_AndReleaseLandingRestartStillWork()
        {
            DisableEnhancementThreats();
            yield return GrabEnhancementPole(1);
            int station = (int)Get(pilot, "flightStationIndex");
            uint launch = (uint)Get(pilot, "launchRevision");
            uint heldSegment = (uint)Get(pilot, "flightSegment");
            Assert.That(heldSegment, Is.GreaterThan(0));
            Assert.AreEqual("None", Get(player, "CurrentJump").ToString(), "일반 점프 상태로는 매달림을 찾을 수 없는 기존 조건");
            var hostFlights = Get(system, "flying");
            var snapshot = Call(hostFlights, "Capture");
            var observer = new GameObject("Flight Registry Observer");
            observer.SetActive(false);
            try
            {
                var receiver = observer.AddComponent(system.GetType());
                var flights = Get(receiver, "flying");
                Assert.IsTrue((bool)Call(flights, "Restore", snapshot, Get(system, "StationCount")));
                Assert.IsTrue((bool)Call(receiver, "IsFlying", Actor), "새 화면에서도 매달린 동료를 프레이밍 대상으로 찾는다.");
                Assert.IsTrue((bool)Call(receiver, "MatchesFlight", Actor, station, launch, heldSegment));
                PoleEnhancementInput(0, release: true);
                Assert.AreEqual(-1, Get(pilot, "HeldPole"));
                Assert.IsTrue(Flying);
                Assert.AreEqual(heldSegment + 1, Get(pilot, "flightSegment"));
                // 실제 호스트의 봉 놓기 식별자로 기존 수신 경로를 실행한다. 네트워크 전달을 가장하지 않는다.
                Call(receiver, "ApplyPoleRelease", Actor, station, launch, heldSegment,
                    player.transform.position, player.transform.rotation, Vector3.zero, 4.5f);
                Assert.IsTrue((bool)Call(receiver, "MatchesFlight", Actor, station, launch, heldSegment + 1));
                Call(receiver, "ApplyLanding", Actor, station, launch, heldSegment, true);
                Assert.IsTrue((bool)Call(receiver, "IsFlying", Actor), "봉 놓기 전의 착지 통지는 무시한다.");
                Call(receiver, "ApplyLanding", Actor, station, launch, heldSegment + 1, true);
                Assert.IsFalse((bool)Call(receiver, "IsFlying", Actor));
                Assert.IsTrue((bool)Call(flights, "Restore", snapshot, Get(system, "StationCount")));
                var reset = System.Enum.Parse(GameType("CurtainCall.HumanCannon.CannonAction"), "Reset");
                Call(receiver, "Apply", 0, reset, ulong.MaxValue, Vector3.zero, Quaternion.identity, Vector3.zero);
                Assert.IsFalse((bool)Call(receiver, "IsFlying", Actor));

                Call(player, "RequestRestartAll");
                yield return WaitUntil(() => !Flying && !(bool)Call(system, "IsFlying", Actor), 3, "실제 호스트 재시작의 비행 정리");
                Assert.AreEqual(-1, Get(pilot, "HeldPole"));
            }
            finally { Object.Destroy(observer); }
        }
    }
}
