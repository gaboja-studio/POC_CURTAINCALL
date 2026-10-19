using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        /// <summary>실제 원격 봇을 쉼터 대포에 먼저 앉힌 뒤, 운반자의 승강·도보 연결·2구간 출발을 검사한다.</summary>
        [UnityTest, Category("LocalBotProcess")]
        public IEnumerator ShelterLift_SeatedRemoteFlyerDoesNotBlockCarrier_ThroughSecondDeparture()
        {
            var helperType = GameType("CurtainCall.HumanCannon.HumanCannonPracticeBot");
            if (!File.Exists((string)helperType.GetProperty("ExecutablePath").GetValue(null)))
                Assert.Ignore("현재 씬을 포함한 로컬 봇 실행파일이 필요합니다.");
            DisableEnhancementThreats();
            var helper = Object.FindAnyObjectByType(helperType) as Component;
            var trampoline = StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current") as Component;
            try
            {
                Call(helper, "SetRole", Enum.Parse(GameType("CurtainCall.HumanCannon.CannonBotRole"), "Flyer"));
                Call(helper, "StartBot");
                yield return WaitUntil(() => (ulong)Get(helper, "BotId") != ulong.MaxValue, 45, "봇 실제 LAN 접속");
                ulong bot = (ulong)Get(helper, "BotId");
                yield return WaitUntil(() => (ulong)Call(system, "Occupant", 0) == bot, 35, "봇 출발 대포 탑승");
                var remote = ((IEnumerable)StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "All"))
                    .Cast<Component>().Single(p => (ulong)Get(p, "OwnerClientId") == bot);

                var handle = (Transform)Get(trampoline, "Handle");
                Call(mover, "SetStartPose", handle.position, Quaternion.identity, true);
                yield return null;
                Call(trampoline, "RequestGrab");
                yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor, 3, "사람이 트램폴린 운반");
                var lift = Call(route, "Transport", 1);
                Call(mover, "SetStartPose", (Vector3)Get(lift, "StartPosition") + Vector3.up * .02f, Quaternion.identity, true);
                yield return new WaitForSeconds(1.8f);
                Assert.AreEqual(1, Get(route, "AliveFlyers"));
                Assert.AreEqual(0, Get(route, "ArrivedFlyers"), "출발 대포는 쉼터 도착으로 오인하지 않는다.");
                Assert.AreEqual("Waiting", Get(Call(route, "TransportState", 1), "Phase").ToString());

                // 접근 위치만 준비한다. 점유 확정/원격 좌석 이동은 실제 호스트 상태와 메시지를 사용한다.
                SeatRemoteAtShelter(remote, bot);
                yield return WaitUntil(() => (int)Get(route, "ArrivedFlyers") == 1, 3, "쉼터 대포 대기도 합류로 인정");
                Assert.IsTrue((bool)Get(route, "ShelterReady"));
                yield return WaitUntil(() => Get(Call(route, "TransportState", 1), "Phase").ToString() == "Arrived",
                    8, "운반자 1.5초 탑승 유지 후 자동 상승");
                Assert.That(Vector3.Distance(player.transform.position, (Vector3)Call(lift, "Position", 1f)), Is.LessThan(.15f));
                Assert.AreEqual(Actor, Get(trampoline, "Carrier"));
                Assert.AreEqual(bot, Call(system, "Occupant", 3), "원격 동료는 위 대포에서 기다린다.");

                // 실제 지상 이동으로 리프트에서 쉼터 바닥, 다음 운송 발판까지 걸어간다.
                yield return WalkCarrierForLift(new Vector3(-19, 6, 78));
                var departure = Call(route, "Transport", 2);
                yield return WalkCarrierForLift((Vector3)Get(departure, "StartPosition"));
                yield return WaitUntil(() => Get(Call(route, "TransportState", 2), "Phase").ToString() == "Arrived",
                    9, "2구간 운반자 출발 발판도 자동 이동");
                Assert.AreEqual(Actor, Get(trampoline, "Carrier"));
                Assert.That(player.transform.position.z, Is.GreaterThan(95));
                yield return WaitUntil(() => (bool)Call(system, "IsFlying", bot), 6, "운반자 준비 후 2구간 대포 발사");
                AssertBody(0);
                int before = restartCount;
                Call(player, "RequestRestartAll");
                yield return WaitUntil(() => restartCount >= before + 2 && !(bool)Get(route, "ShelterReady"), 5,
                    "R 뒤 이전 쉼터 합류 상태가 남지 않는다.");
                Assert.AreEqual("Waiting", Get(Call(route, "TransportState", 1), "Phase").ToString());
            }
            finally
            {
                if (mover != null) Call(mover, "SetMoveInput", 0f, 0f);
                if (helper != null) Call(helper, "StopBot");
            }
        }

        void SeatRemoteAtShelter(Component remote, ulong actor)
        {
            var seats = (Array)Get(system, "seats");
            uint revision = (uint)Get(Get(seats.GetValue(0), "State"), "Revision");
            Call(system, "HandleRequest", actor, 0, Enum.Parse(GameType("CurtainCall.HumanCannon.CannonAction"), "Cancel"),
                revision, 0u, Vector3.zero, false);
            var station = Call(system, "Station", 3);
            var point = (Transform)Get(station, "BoardingPoint");
            Call(remote.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "SetStartPose", point.position, point.rotation, true);
            Assert.AreEqual(3, Call(system, "FindBoardableStation", remote));
            revision = (uint)Get(Get(seats.GetValue(3), "State"), "Revision");
            Call(system, "HandleRequest", actor, 3, Enum.Parse(GameType("CurtainCall.HumanCannon.CannonAction"), "Board"),
                revision, 0u, Vector3.zero, false);
            Assert.AreEqual(actor, Call(system, "Occupant", 3));
        }

        IEnumerator WalkCarrierForLift(Vector3 target)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            var forward = (Vector3)Get(mover, "CourseForward");
            var right = Vector3.Cross(Vector3.up, forward);
            while (Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).magnitude > .25f
                && Time.realtimeSinceStartup < deadline)
            {
                var direction = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).normalized;
                Call(mover, "SetMoveInput", Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
                yield return null;
            }
            Call(mover, "SetMoveInput", 0f, 0f);
            Assert.That(Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).magnitude,
                Is.LessThan(.35f), "운반 상태로 쉼터 바닥을 실제 걸어서 연결한다.");
            Assert.That(player.transform.position.y, Is.EqualTo(target.y).Within(.15f));
        }
    }
}
