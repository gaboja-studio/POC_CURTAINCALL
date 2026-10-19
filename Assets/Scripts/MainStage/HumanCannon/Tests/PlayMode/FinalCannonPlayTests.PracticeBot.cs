using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        /// <summary>로컬 실행파일을 사용한 실제 두 프로세스 LAN 협동. 실행파일 없는 CI에서는 명시적으로 생략한다.</summary>
        [UnityTest, Category("LocalBotProcess")]
        public IEnumerator PracticeBot_RealClient_CarriesSwapsLaunchesAndRestartsWithoutTakingLocalPlayer()
        {
            var helperType = GameType("CurtainCall.HumanCannon.HumanCannonPracticeBot");
            string executable = (string)helperType.GetProperty("ExecutablePath").GetValue(null);
            if (!File.Exists(executable)) Assert.Ignore("Build Local Practice Bot 실행파일이 필요한 로컬 2인 검사입니다.");
            var helper = Object.FindAnyObjectByType(helperType) as Component;
            Assert.IsNotNull(helper, "인간대포 테스트 HUD가 봇 패널을 연결해야 합니다.");
            var trampoline = StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current") as Component;
            var director = StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current") as Component;
            try
            {
                Call(helper, "StartBot");
                yield return WaitUntil(() => (ulong)Get(helper, "BotId") != ulong.MaxValue, 45, "실제 봇 프로세스의 LAN 접속과 역할 수신");
                ulong bot = (ulong)Get(helper, "BotId");
                Assert.AreNotEqual(Actor, bot);
                Assert.AreEqual(2, Get(session, "PlayerCount"));
                Assert.AreSame(player, StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"));
                Assert.AreSame(player, Get(director, "LocalPlayer"), "봇이 사람의 카메라를 가져가면 안 됩니다.");
                Assert.AreSame(pilot, Get(system, "pilot"));
                Call(helper, "StartBot"); // 중복 버튼은 세 번째 프로세스/플레이어를 만들지 않는다.
                yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == bot, 40,
                    "봇이 실제 이동·F 요청으로 트램폴린을 잡음");
                Assert.AreEqual(2, Get(session, "PlayerCount"));

                // 관찰자의 마우스 시선이 원격 운반 장치를 돌리면 안 된다. 실제 별도 프로세스 소유자를 검사한다.
                yield return new WaitForEndOfFrame();
                Quaternion remoteCarryRotation = trampoline.transform.rotation;
                float previousYaw = (float)Get(director, "yaw"), previousPitch = (float)Get(director, "pitch");
                foreach (float yaw in new[] { 90f, 180f, 270f })
                {
                    SetCarryView(director, yaw, 0);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Assert.That(Quaternion.Angle(trampoline.transform.rotation, remoteCarryRotation), Is.LessThan(.1f),
                        "내 카메라를 돌려도 원격 봇의 트램폴린 방향은 유지");
                }
                SetCarryView(director, previousYaw, previousPitch);

                yield return WaitUntil(() => Get(Call(route, "TransportState", 0), "Phase").ToString() == "Moving"
                    && (ulong)Get(Call(route, "TransportState", 0), "Rider") == bot, 10,
                    "실제 봇 운반자가 출발 리프트에 탑승하여 이동함");
                Component remoteCarrier = null;
                foreach (Component candidate in (IEnumerable)StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "All"))
                    if ((ulong)Get(candidate, "OwnerClientId") == bot) remoteCarrier = candidate;
                Assert.IsNotNull(remoteCarrier);
                Assert.IsFalse((bool)Get(remoteCarrier, "IsLocal"));
                var transit = ((Array)Get(route, "runtime")).GetValue(0);
                Quaternion replicatedRotation = Quaternion.AngleAxis(90f, Vector3.up) * (Quaternion)Get(transit, "Rotation");
                Quaternion previousRemoteRotation = remoteCarrier.transform.rotation;
                try
                {
                    // 실제 별도 프로세스의 원격 객체에 수신된 시선 회전을 주입한다. 원격 마우스 입력을 가장하지 않는다.
                    remoteCarrier.transform.rotation = replicatedRotation;
                    Call(route, "UpdatePassenger");
                    Call(trampoline, "LateUpdate");
                    Assert.That(Quaternion.Angle(remoteCarrier.transform.rotation, replicatedRotation), Is.LessThan(.05f),
                        "리프트 위치 보정이 원격 운반자의 수신 회전을 탑승 당시 방향으로 덮어쓰면 안 된다.");
                    Vector3 surfaceDirection = Vector3.ProjectOnPlane((Vector3)Get(trampoline, "Surface")
                        - remoteCarrier.transform.position, Vector3.up);
                    Assert.That(Vector3.Angle(surfaceDirection, replicatedRotation * Vector3.forward), Is.LessThan(.05f),
                        "리프트 이동 중 트램폴린도 원격 운반자의 수신 시선 앞에 있어야 한다.");
                    Assert.That(Vector3.Distance(((Transform)Get(trampoline, "Handle")).position,
                        remoteCarrier.transform.position), Is.LessThan(.003f), "회전 후에도 손잡이는 운반자 위치 유지");
                }
                finally
                {
                    remoteCarrier.transform.rotation = previousRemoteRotation;
                    Call(trampoline, "LateUpdate");
                }

                var flyer = Enum.Parse(GameType("CurtainCall.HumanCannon.CannonBotRole"), "Flyer");
                int before = restartCount;
                Call(helper, "SetRole", flyer);
                yield return WaitUntil(() => restartCount >= before + 2 && (ulong)Get(trampoline, "Carrier") == ulong.MaxValue,
                    5, "교대가 두 플레이어와 운반 소유권을 초기화함");
                yield return WaitUntil(() => (bool)Call(system, "IsSeated", bot), 35, "공중 역할 봇이 대포에 탑승함");
                Assert.AreSame(player, StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"));
                Assert.AreSame(player, Get(director, "LocalPlayer"));

                var handle = (Transform)Get(trampoline, "Handle");
                Call(mover, "SetStartPose", handle.position, Quaternion.identity, true);
                yield return null;
                Call(trampoline, "RequestGrab");
                yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor, 3, "이번에는 사람이 트램폴린을 운반함");
                yield return WaitUntil(() => Get(Call(route, "TransportState", 0), "Phase").ToString() == "Arrived", 12,
                    "운반자 혼자 출발 리프트 이용");
                yield return WaitUntil(() => (bool)Call(system, "IsFlying", bot), 6, "봇이 운반자 준비 후 실제 대포 발사");
                Assert.IsTrue((bool)Get(director, "IsFreeThirdPerson"), "사람은 운반 카메라를 유지함");
                yield return WaitUntil(() => MarkerVisible(bot), 3, "원격 봇의 공중 위치도 사람 화면에 원형 표시");
                Component remote = null;
                foreach (Component candidate in (IEnumerable)StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "All"))
                    if ((ulong)Get(candidate, "OwnerClientId") == bot) remote = candidate;
                Assert.IsNotNull(remote);
                AssertMarkerUnder(remote);
                yield return WaitUntil(() => BotSegment(bot) > 0, 10, "봇이 실제 트램폴린에 착지하여 반동함");

                before = restartCount;
                Call(player, "RequestRestartAll");
                yield return WaitUntil(() => restartCount >= before + 2 && !(bool)Call(system, "IsFlying", bot)
                    && (ulong)Get(trampoline, "Carrier") == ulong.MaxValue, 5, "공중 봇과 운반자 모두 R 재시작");
                Assert.AreEqual(2, Get(session, "PlayerCount"), "R 뒤에도 실제 두 접속 유지");
                yield return null; yield return null;
                AssertMarkerHidden(bot);
                Assert.AreSame(player, StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"));
                Call(helper, "StopBot");
                yield return WaitUntil(() => (int)Get(session, "PlayerCount") == 1, 10, "봇 종료 뒤 플레이어/점유 정리");
                Assert.IsFalse((bool)Get(helper, "Running"));
                yield return null; yield return null;
                Assert.IsNull(GroundMarker(bot), "접속 종료한 참가자의 표시도 제거한다.");
            }
            finally { if (helper != null) Call(helper, "StopBot"); }
        }

        uint BotSegment(ulong actor)
        {
            foreach (var flight in (IEnumerable)Call(Get(system, "flying"), "Capture"))
                if ((ulong)Get(flight, "Actor") == actor) return (uint)Get(flight, "Segment");
            return 0;
        }
    }
}
