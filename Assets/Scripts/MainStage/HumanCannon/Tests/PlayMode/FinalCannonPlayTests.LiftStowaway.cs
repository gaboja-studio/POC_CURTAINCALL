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
        /// <summary>
        /// 출발 직전 발판 위 트램폴린으로 뛰어 높이 뜬 사람이 이동 중 계속 반동하며 대포 없이 같이 가지 못한다(2026-11-04 사용자 보고).
        /// </summary>
        [UnityTest]
        public IEnumerator MovingLift_TrampolineBouncerCannotRideAlong()
        {
            DisableEnhancementThreats();
            var trampoline = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current");
            var lift = Call(route, "Transport", 0);
            var start = (Vector3)Get(lift, "StartPosition");
            trampoline.transform.SetPositionAndRotation(start, Quaternion.identity);
            Call(trampoline, "ResetPreviousPose");
            Call(mover, "SetStartPose", start + Vector3.up * .01f, Quaternion.identity, true);
            yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "트램폴린 위 대기");
            yield return null;
            int before = (int)Get(trampoline, "LocalBounceCount");
            Call(mover, "RequestJump");
            yield return WaitUntil(() => (int)Get(trampoline, "LocalBounceCount") > before && Flying, 4, "대기 중 반동은 허용");
            yield return WaitUntil(() => player.transform.position.y - start.y > 2.5f, 3, "탑승 높이보다 높이 뜬다");
            Assert.AreEqual("Waiting", Get(Call(route, "TransportState", 0), "Phase").ToString(),
                "높이 뜬 반동자도 탑승자로 세어 운반자만 탄 것으로 출발하지 않는다.");

            // 출발 순간 이미 떠 있던 경우를 재현한다. 긴 이동 시간으로 발판이 트램폴린 아래에 머문다.
            var state = Call(route, "TransportState", 0);
            Assert.IsTrue((bool)Call(state, "TryStart", 999UL, (uint)Get(state, "Revision"), (double)Get(route, "Clock"), 600f));
            int launched = (int)Get(trampoline, "LocalBounceCount");
            yield return WaitUntil(() => !Flying && !(bool)Call(lift, "ContainsAirColumn", player.transform.position), 8,
                "이동 중 트램폴린 재반동 없이 착지해 발판 밖으로 밀려난다");
            Assert.AreEqual(launched, Get(trampoline, "LocalBounceCount"), "이동 중 발판 위 트램폴린은 반동하지 않는다.");
            Assert.IsFalse((bool)Call(system, "IsFlying", Actor));
        }

        /// <summary>실제 원격 봇(2인): 호스트가 트램폴린을 들고 출발한 뒤 뒤따라 트램폴린으로 뛰어든다.</summary>
        [UnityTest, Category("LocalBotProcess")]
        public IEnumerator TwoPlayers_JumpOntoDepartedLiftTrampolineDoesNotRideAlong() => RemoteStowaway(true);

        /// <summary>실제 원격 봇(2인): 출발 전부터 운반자가 든 트램폴린 위에서 계속 반동한다.</summary>
        [UnityTest, Category("LocalBotProcess")]
        public IEnumerator TwoPlayers_BouncingBeforeDepartureDoesNotRideAlong() => RemoteStowaway(false);

        IEnumerator RemoteStowaway(bool late)
        {
            var helperType = GameType("CurtainCall.HumanCannon.HumanCannonPracticeBot");
            if (!File.Exists((string)helperType.GetProperty("ExecutablePath").GetValue(null)))
                Assert.Ignore("현재 씬을 포함한 로컬 봇 실행파일이 필요합니다.");
            DisableEnhancementThreats();
            var helper = Object.FindAnyObjectByType(helperType) as Component;
            var trampoline = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonTrampoline", "Current");
            var lift = Call(route, "Transport", 0);
            try
            {
                Call(helper, "SetRole", Enum.Parse(GameType("CurtainCall.HumanCannon.CannonBotRole"), late ? "LiftJumper" : "LiftBouncer"));
                Call(helper, "StartBot");
                yield return WaitUntil(() => (ulong)Get(helper, "BotId") != ulong.MaxValue, 45, "봇 실제 LAN 접속");
                ulong bot = (ulong)Get(helper, "BotId");
                var remote = ((IEnumerable)StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "All"))
                    .Cast<Component>().Single(p => (ulong)Get(p, "OwnerClientId") == bot);

                Call(mover, "SetStartPose", ((Transform)Get(trampoline, "Handle")).position, Quaternion.identity, true);
                yield return null;
                Call(trampoline, "RequestGrab");
                yield return WaitUntil(() => (ulong)Get(trampoline, "Carrier") == Actor, 3, "호스트가 트램폴린 운반");
                string Phase() => Get(Call(route, "TransportState", 0), "Phase").ToString();
                bool OnLift() => (bool)Call(lift, "ContainsAirColumn", remote.transform.position);
                Vector3 liftStart = (Vector3)Get(lift, "StartPosition");

                Vector3 travel = Vector3.ProjectOnPlane((Vector3)Call(lift, "Position", 1f) - liftStart, Vector3.up).normalized;
                bool NearTrampoline() => Vector3.ProjectOnPlane((Vector3)Get(trampoline, "Surface") - remote.transform.position, Vector3.up).magnitude < 3.5f
                    && remote.transform.position.y > ((Vector3)Get(trampoline, "Surface")).y - .5f;
                // 악용하는 운반자는 갑판 쪽을 바라봐 트램폴린을 갑판 가장자리 위에 둔다. 승강로 위로 내밀면 동료가 떨어져 라운드가 재시작된다.
                var facingDeck = Quaternion.LookRotation(-travel);
                // 운반 방향은 3인칭 시선(없으면 손잡이→본체 방향)을 따른다. 시선과 본체를 함께 갑판 쪽으로 돌린다.
                void TurnTrampolineToDeck()
                {
                    var view = StaticGet("CurtainCall.Cameras.CameraViewDirector", "Current");
                    if (view != null) Call(view, "SetLookRotation", facingDeck);
                    var handle = (Transform)Get(trampoline, "Handle");
                    Vector3 body = Vector3.ProjectOnPlane(trampoline.transform.position - handle.position, Vector3.up);
                    trampoline.transform.RotateAround(handle.position, Vector3.up, Vector3.SignedAngle(body, -travel, Vector3.up));
                    Call(trampoline, "ResetPreviousPose");
                }
                int restarts = restartCount;
                if (late)
                {
                    // 봇이 발판 뒤 갑판 대기 위치에 선 뒤에 운반자가 올라탄다(출발 즉시 뛰어드는 상황).
                    yield return WaitUntil(() => !OnLift() && Vector3.ProjectOnPlane(remote.transform.position - liftStart, Vector3.up).magnitude < 6f
                        && Mathf.Abs(remote.transform.position.y - liftStart.y) < .3f, 30, "봇이 승강기 뒤에서 대기");
                    Call(mover, "SetStartPose", liftStart + Vector3.up * .02f, facingDeck, true);
                    yield return null;
                    TurnTrampolineToDeck();
                    yield return WaitUntil(() => Phase() == "Moving", 8, "운반자 1인 + 트램폴린 자동 출발");
                    Assert.IsFalse(OnLift(), "출발 시점에 봇은 발판 밖에 있다.");
                }
                else
                {
                    // 동료가 트램폴린에서 반동하는 동안 운반자가 실제로 걸어서 승강기에 올라탄다.
                    Call(mover, "SetStartPose", liftStart - travel * 5f + Vector3.up * .02f, Quaternion.identity, true);
                    yield return WaitUntil(() => (bool)Call(system, "IsFlying", bot), 25, "봇이 운반 중인 트램폴린에서 반동");
                    yield return WalkCarrierForLift(liftStart);
                    Call(mover, "SetStartPose", player.transform.position, facingDeck, true);
                    yield return null;
                    TurnTrampolineToDeck();
                    yield return WaitUntil(() => (bool)Call(system, "IsFlying", bot) && NearTrampoline(), 10, "봇이 승강기 위 운반자의 트램폴린에서 반동");
                    float until = Time.realtimeSinceStartup + 6;
                    while (Time.realtimeSinceStartup < until && Phase() == "Waiting") yield return null;
                    Assert.AreEqual(restarts, restartCount, "검사 중 라운드가 재시작되면 결과가 의미 없다.");
                    Assert.That((bool)Call(lift, "ContainsPassenger", player.transform.position), "운반자가 승강기 위에 있다.");
                    Assert.AreEqual("Waiting", Phase(), "반동 중인 동료가 있으면 운반자만 탄 것으로 출발하지 않는다.");
                    // 반동하던 동료가 떠나면(연습 봇 역할 교대는 라운드를 재시작하므로 접속 종료로 대신한다) 운반자만 정상 출발한다.
                    Call(helper, "StopBot");
                    yield return WaitUntil(() => Phase() == "Moving", 15, "동료가 떠난 뒤 운반자 출발");
                    yield return WaitUntil(() => Phase() == "Arrived", 8, "운반자와 트램폴린 도착");
                    Assert.AreEqual(Actor, Get(trampoline, "Carrier"));
                    yield break;
                }
                int bounces = 0, ejected = 0;
                bool wasFlying = (bool)Call(system, "IsFlying", bot), wasOnLift = OnLift();
                float closest = float.MaxValue;
                while (Phase() == "Moving")
                {
                    bool flying = (bool)Call(system, "IsFlying", bot), onLift = OnLift();
                    if (flying && !wasFlying && (onLift || NearTrampoline())) bounces++;
                    if (wasOnLift && !onLift) ejected++;
                    wasFlying = flying; wasOnLift = onLift;
                    closest = Mathf.Min(closest, Vector3.ProjectOnPlane(
                        ((Vector3)Get(trampoline, "Surface")) - remote.transform.position, Vector3.up).magnitude);
                    yield return null;
                }
                Debug.Log($"[LiftStowawayTest] late={late} closest={closest:F2}m bouncesOnLift={bounces} leftLift={ejected} bot={remote.transform.position}");
                Assert.AreEqual("Arrived", Phase());
                if (late) Assert.That(closest, Is.LessThan(3f), "봇이 실제로 이동 중 트램폴린 가까이 뛰어들어야 검사가 의미 있다.");
                yield return new WaitForSeconds(.5f);
                Assert.AreEqual(restarts, restartCount, "검사 중 라운드가 재시작되면 결과가 의미 없다.");
                Assert.IsFalse(OnLift() || NearTrampoline(), $"봇이 대포 없이 승강기와 함께 도착했다. 이동 중 발판 위 반동 {bounces}회, 봇 위치 {remote.transform.position}");
                Assert.AreEqual(0, bounces, "이동 중 발판 위 트램폴린 반동은 서버가 받아들이지 않는다.");
                Assert.AreEqual(Actor, Get(trampoline, "Carrier"));
            }
            finally
            {
                if (mover != null) Call(mover, "SetMoveInput", 0f, 0f);
                if (helper != null) Call(helper, "StopBot");
            }
        }
    }
}
