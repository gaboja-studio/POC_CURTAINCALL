using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    /// <summary>
    /// 2026-11-11 출발대 4m→5m 상향 실측(Task-115). 실제 호스트·물리·착지 판정으로 낙하 결과를 기록한다.
    /// 규칙: 5m 이상 15m 미만 부상(2부위), 15m 이상 사망, -15m 이하 즉사.
    /// </summary>
    public sealed partial class FinalCannonPlayTests
    {
        static void Probe(string message) => Debug.Log("[RaisedDeck] " + message);
        string Body() => $"state={Get(player, "State")} lost={Convert.ToInt32(Get(player, "LostParts"))}";

        [UnityTest, Category("RaisedDeck")]
        public IEnumerator RaisedDeck_SpawnAndBoardingStandOnFiveMetreDeck()
        {
            DisableEnhancementThreats();
            var body = (CharacterController)Get(mover, "Body");
            yield return WaitUntil(() => body.isGrounded, 3, "시작 위치 착지");
            Probe($"spawn pos={player.transform.position} grounded={body.isGrounded} {Body()}");
            Assert.That(player.transform.position.y, Is.EqualTo(5.05f).Within(.3f));
            for (int i = 0; i < 3; i++)
            {
                var station = (Component)Call(system, "Station", i);
                Probe($"station{i} {station.name} boarding={((Transform)Get(station, "BoardingPoint")).position} muzzle={((Transform)Get(station, "Muzzle")).position} v={Get(station, "LaunchVelocity")}");
            }
            yield return Board(0);
            Probe($"boarded cannon0 occupant={Call(system, "Occupant", 0)} {Body()}");
            AssertBody(0);
        }

        [UnityTest, Category("RaisedDeck")]
        public IEnumerator RaisedDeck_DropFromDeckHeightOntoGround([Values(0f, .8f)] float jump)
        {
            DisableEnhancementThreats();
            // 출발대 발 높이(5m)+점프 높이에서 지상(0m) 위로 떨어진다. 틈(z 8~15) 너머 첫 지상 z=17.
            Vector3 from = new Vector3(0f, 5f + jump, 17f);
            Call(mover, "SetStartPose", from, Quaternion.identity, true);
            var body = (CharacterController)Get(mover, "Body");
            yield return null;
            yield return WaitUntil(() => body.isGrounded && !(bool)Get(player, "IsAirborne"), 4, "지상 착지");
            yield return new WaitForSeconds(.3f);
            Probe($"deck drop jump={jump} from={from.y:F2} land={player.transform.position} {Body()}");
            // 지상 발 높이는 약 0.08m라 점프 없는 5m 낙하는 4.92m(무피해), 점프 포함은 부상(두 다리).
            AssertBody(jump > 0f ? 12 : 0);
        }

        [UnityTest, Category("RaisedDeck")]
        public IEnumerator RaisedDeck_DepartureCannonMissesTrampoline([Values(0, 1, 2)] int index)
        {
            DisableEnhancementThreats();
            yield return FireDepartureAndTrack(index, "miss");
            // 최고점 약 14.3m → 지상 착지는 사망(15m) 미만의 부상.
            AssertBody(12);
        }

        [UnityTest, Category("RaisedDeck")]
        public IEnumerator RaisedDeck_DepartureCannonCaughtAtFirstCatch()
        {
            DisableEnhancementThreats();
            var trampoline = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current");
            var target = GameObject.Find("FirstCatch").transform.position;
            trampoline.transform.SetPositionAndRotation(target, Quaternion.identity);
            Call(trampoline, "ResetPreviousPose");
            Physics.SyncTransforms();
            yield return null;
            int before = (int)Get(trampoline, "LocalBounceCount");
            yield return FireDepartureAndTrack(0, "catch");
            Probe($"catch bounces={(int)Get(trampoline, "LocalBounceCount") - before} trampolineSurface={Get(trampoline, "Surface")}");
            Assert.That((int)Get(trampoline, "LocalBounceCount"), Is.GreaterThan(before), "FirstCatch 트램펄린이 출발 대포 비행을 받는다.");
            AssertBody(0);
        }

        [UnityTest, Category("RaisedDeck")]
        public IEnumerator RaisedDeck_DepartureLiftCarriesRiderDownWithoutFallDamage()
        {
            DisableEnhancementThreats();
            var lift = Call(route, "Transport", 0);
            var start = (Vector3)Get(lift, "StartPosition");
            Call(mover, "SetStartPose", start + Vector3.up * .01f, Quaternion.identity, true);
            var body = (CharacterController)Get(mover, "Body");
            yield return WaitUntil(() => body.isGrounded, 3, "리프트 발판 대기");
            var state = Call(route, "TransportState", 0);
            Assert.IsTrue((bool)Call(state, "TryStart", Actor, (uint)Get(state, "Revision"), (double)Get(route, "Clock"), 4f));
            float lowest = player.transform.position.y;
            yield return WaitUntil(() => { lowest = Mathf.Min(lowest, player.transform.position.y); return Get(state, "Phase").ToString() == "Arrived"; }, 10, "리프트 도착");
            yield return new WaitForSeconds(.5f);
            Probe($"lift start={start} end={player.transform.position} lowest={lowest:F2} {Body()}");
            AssertBody(0);
        }

        IEnumerator FireDepartureAndTrack(int index, string label)
        {
            yield return Board(index);
            yield return ReleaseThenArm();
            Input(tapped: true);
            Assert.IsTrue(Flying, "발사");
            Vector3 start = player.transform.position;
            float highest = start.y; Vector3 firstContact = Vector3.zero; bool contact = false;
            float deadline = Time.realtimeSinceStartup + 12;
            var body = (CharacterController)Get(mover, "Body");
            while (Time.realtimeSinceStartup < deadline)
            {
                highest = Mathf.Max(highest, player.transform.position.y);
                if (!Flying && !contact) { contact = true; firstContact = player.transform.position; }
                if (contact && body.isGrounded && !(bool)Get(player, "IsAirborne") && !Flying) break;
                if (Get(player, "State").ToString() != "Normal") break;
                yield return null;
            }
            yield return new WaitForSeconds(.3f);
            Probe($"{label} cannon{index} start={start} highest={highest:F2} firstContact={firstContact} final={player.transform.position} drop={(highest - player.transform.position.y):F2} {Body()}");
        }
    }
}
