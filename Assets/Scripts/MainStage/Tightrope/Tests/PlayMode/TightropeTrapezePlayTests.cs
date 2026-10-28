using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator PoleInteraction_TrapezeUsesExistingOutlineAndFPrompt()
            => FirstTrapezePoleInjuredEntry(false, verifyInteraction: true);

        [UnityTest]
        public IEnumerator TrapezeCatchRequiresActualNextPoleContactAfterFastLaunch() => TrapezeRangeCatch(false, .5f);

        [UnityTest]
        public IEnumerator TrapezeCatchWindowIncludesRangeLimitForOccupiedNextPole() => TrapezeRangeCatch(true, 1f);

        IEnumerator TrapezeRangeCatch(bool occupied, float fraction)
        {
            EnableTrapezeForEntryTest();
            yield return BeginPerformance(); DisableSwingHazards();
            var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
            yield return Until(() => (bool)Get(trapeze, "IsActive"), 2f, "공중그네 준비");
            var poles = (Array)Get(trapeze, "poles");
            var first = poles.GetValue(0);
            ulong owner = (ulong)Get(player, "OwnerClientId");
            Component partner = null;
            try
            {
                if (occupied)
                {
                    partner = CreatePracticeBotTestPartner((int)Get(trapeze, "LaneIndex"), (float)Get(trapeze, "EntryDistance") - 3f);
                    var next = poles.GetValue(1);
                    Set(next, "Upper", (ulong)Get(partner, "OwnerClientId"));
                    Set(next, "Until", (double)Get(trapeze, "Clock") + 30d);
                }
                Set(first, "Upper", owner);
                Set(first, "Until", (double)Get(trapeze, "Clock") + 30d);
                Set(first, "Speed", 5000f); // 출발 시 과도한 반동을 실제 서버 발사 경로로 보낸다.
                Call(trapeze, "ReconcileLocal");
                Vector3 origin = player.transform.position;
                var command = trapeze.GetType().GetNestedType("Command", BindingFlags.NonPublic);
                Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Release"), 0, (uint)Get(first, "Revision"), 0f, 0u);
                Call(trapeze, "ReconcileLocal");
                float limit = (float)Call(trapeze, "FlightDistanceLimit", 0, origin);
                Vector3 forward = (Vector3)Get(course, "Forward");
                Vector3 launchVelocity = (Vector3)Get(trapeze, "flightVelocity");
                Assert.That(Vector3.ProjectOnPlane(launchVelocity, Vector3.up).magnitude,
                    Is.EqualTo((float)Get(trapeze, "flightSpeedLimit")).Within(.001f), "출발할 때 속도를 조정한다.");
                Call(trapeze, "StepFlight");
                Assert.That(Vector3.ProjectOnPlane(player.transform.position - origin, Vector3.up).magnitude,
                    Is.LessThanOrEqualTo(limit + .02f), "큰 반동도 다음 봉 비거리 상한을 넘지 않는다.");
                var body = (CharacterController)Get(mover, "Body");
                body.enabled = false;
                player.transform.position = origin + forward * (limit + .2f);
                body.enabled = true;
                Vector3 beyond = player.transform.position;
                Call(trapeze, "StepFlight");
                Assert.That(Vector3.Dot(player.transform.position - beyond, forward), Is.GreaterThan(0f),
                    "경계를 지난 비행도 뒤로 잘라 내거나 제자리에 막지 않는다.");
                body.enabled = false;
                float gravity = (float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "Character"), "Gravity");
                float horizontalSpeed = Vector3.Dot(launchVelocity, forward);
                float beforeTime = limit * .49f / horizontalSpeed;
                player.transform.position = origin + launchVelocity * beforeTime + Vector3.down * (.5f * gravity * beforeTime * beforeTime);
                body.enabled = true;
                Assert.IsFalse((bool)Call(trapeze, "TryRequestCatch", player), "절반 전 F는 잡히지 않는다.");
                body.enabled = false;
                float catchTime = limit * fraction / horizontalSpeed;
                player.transform.position = origin + launchVelocity * catchTime + Vector3.down * (.5f * gravity * catchTime * catchTime);
                Set(trapeze, "flightVelocity", launchVelocity + Vector3.down * (gravity * catchTime));
                body.enabled = true;
                Assert.IsFalse((bool)Call(trapeze, "CanGrabPole", 0, player), "출발 봉을 다시 잡아 도약 거리를 초기화하지 않는다.");
                Assert.IsFalse((bool)Call(trapeze, "CanGrabPole", 2, player), "중간 봉을 건너뛰지 않는다.");
                if (fraction < 1f)
                {
                    Assert.IsFalse((bool)Call(trapeze, "InCatchRange", player, 1, false), "절반 비거리는 실제 다음 봉 접촉이 아니다.");
                    Assert.IsFalse((bool)Call(trapeze, "TryRequestCatch", player), "절반 비거리 F를 예약하지 않는다.");
                    var requests = (IDictionary)Get(trapeze, "pendingRequests");
                    Assert.That(requests.Count, Is.Zero, "로컬 범위 밖 F는 서버 요청을 만들지 않는다.");
                    Vector3 beforeCatch = player.transform.position;
                    // 클라이언트의 접촉 F가 위치 동기화보다 먼저 도착한 서버 경로는 별도로 검사한다.
                    Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Grab"), 1, (uint)Get(poles.GetValue(1), "Revision"), 0f, (uint)Get(trapeze, "localFlightToken"));
                    object waiting = requests[owner];
                    Assert.IsNotNull(waiting);
                    Assert.IsFalse((bool)Get(waiting, "Armed"), "서버 위치가 범위 밖이면 비행 끝까지 예약하지 않는다.");
                    double until = (double)Get(waiting, "Until");
                    Assert.That(until, Is.EqualTo((double)Get(trapeze, "Clock") + .5d).Within(.001d));
                    Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Grab"), 1, (uint)Get(poles.GetValue(1), "Revision"), 0f, (uint)Get(trapeze, "localFlightToken"));
                    Assert.AreSame(waiting, requests[owner]);
                    Assert.That((double)Get(waiting, "Until"), Is.EqualTo(until), "중복 입력이 위치 대기 기한을 갱신하지 않는다.");
                    Call(trapeze, "ReconcileLocal");
                    Assert.That((int)Call(trapeze, "HeldPole", player), Is.EqualTo(-1), "먼 봉으로 즉시 이동하지 않는다.");
                    Assert.That(Vector3.Distance(player.transform.position, beforeCatch), Is.LessThan(.001f));
                    Call(trapeze, "StepFlight");
                    Assert.That(Vector3.Distance(player.transform.position, beforeCatch), Is.GreaterThan(.001f), "서버 위치 대기 중에도 비행을 계속한다.");
                    Assert.IsFalse((bool)Call(trapeze, "IsHeld", player));
                    // 다음 봉까지 테스트 위치를 전진시킨 뒤 실제 접촉에서만 대기 요청을 확정한다.
                    body.enabled = false;
                    player.transform.position = (Vector3)Call(trapeze, "PoleFeet", 1, false);
                    body.enabled = true;
                }
                Assert.IsTrue((bool)Call(trapeze, "InCatchRange", player, 1, occupied), "잡기는 다음 봉/동료의 실제 구형 범위 안에서만 허용한다.");
                Vector3 contact = player.transform.position;
                if (fraction < 1f) Call(trapeze, "ProcessPendingRequests");
                else Assert.IsTrue((bool)Call(trapeze, "TryRequestCatch", player));
                Assert.That(((IDictionary)Get(trapeze, "pendingRequests")).Count, Is.Zero);
                Assert.That((int)Call(trapeze, "HeldPole", player), Is.EqualTo(1));
                Assert.That((bool)Call(trapeze, "IsLower", player), Is.EqualTo(occupied));
                Assert.IsFalse((bool)Call(trapeze, "IsInFlight", player));
                Call(trapeze, "ReconcileLocal");
                Assert.That(Vector3.Distance(player.transform.position, (Vector3)Call(trapeze, "PoleFeet", 1, occupied)),
                    Is.LessThan(.01f));
                Assert.That(Vector3.Distance(player.transform.position, contact), Is.LessThan(1.1f), "실제 접촉 뒤 잡기 보정도 몇 m씩 순간 이동하지 않는다.");
            }
            finally { DestroyPracticeBotTestPartner(partner); }
        }

        [UnityTest, Category("TightropeJourneyRegression")]
        public IEnumerator FirstTrapezePoleCatchesJumpFromLeglessPartner() => FirstTrapezePoleInjuredEntry(false);

        [UnityTest, Category("TightropeJourneyRegression")]
        public IEnumerator FirstTrapezePoleCatchesOneArmedJumpFromLeglessPartner() => FirstTrapezePoleInjuredEntry(true);

        IEnumerator FirstTrapezePoleInjuredEntry(bool oneArmedUpper, bool verifyInteraction = false)
        {
            EnableTrapezeForEntryTest();
            yield return BeginPerformance(); DisableSwingHazards();
            var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
            yield return Until(() => (bool)Get(trapeze, "IsActive"), 2f, "공중그네 준비");
            int lane = (int)Get(trapeze, "LaneIndex");
            float entry = (float)Get(trapeze, "EntryDistance");
            if (verifyInteraction)
                yield return Until(() => UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.UI.SceneIntegration.CommonInteractionUI")) != null,
                    15f, "기존 상호작용 UI 비동기 로드");
            yield return StandOnRopeForSawTest(lane, entry - 1.7f);
            var partner = CreatePracticeBotTestPartner(lane, entry - 1f);
            var reader = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"));
            try
            {
                Call(partner, "ServerCut", 2);
                if (oneArmedUpper) Call(player, "ServerSetOneArm");
                yield return null;
                yield return null;
                var partnerMover = partner.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                Assert.That((float)Get(partnerMover, "BodyHeight"), Is.LessThan(1.1f));
                reader.enabled = true;
                yield return Until(() => !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 2f, "점프 입력 잠금 해제");
                var stack = Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
                Call(stack, "ServerTryLink", (ulong)Get(player, "OwnerClientId"));
                Assert.AreSame(partner, Call(stack, "GetBelow", player));
                var follow = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.PiggybackFollow"));
                Set(reader, "<Current>k__BackingField", CatchFrame(move: 1f, jump: true));
                Call(follow, "LateUpdate");
                reader.enabled = false;
                Assert.IsNull(Call(stack, "GetBelow", player));
                var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
                var scheme = Get(player.GetComponent(GameType("CurtainCall.Player.PlayerController")), "Scheme");
                float deadline = Time.time + 2f;
                bool caught = false;
                float nextTrace = 0f;
                string trajectory = "";
                while (Time.time < deadline && Get(player, "State").ToString() == "Normal")
                {
                    if (Time.time >= nextTrace)
                    {
                        nextTrace = Time.time + .1f;
                        trajectory += $" y={player.transform.position.y-course.transform.position.y:F2},d={Call(course, "GetDistance", player.transform.position)},air={Get(player, "IsAirborne")},eligible={Call(trapeze, "Eligible", player)};";
                    }
                    if ((bool)Call(trapeze, "CanGrabPole", 0, player))
                    {
                        Assert.That(player.transform.position.y - course.transform.position.y, Is.LessThan(2f), "기존 2m 하한보다 낮은 실제 분리 점프를 검사한다.");
                        Assert.IsTrue((bool)Call(stack, "CanEnterTrapeze", player), "실제 목마 분리 점프로 첫 봉 진입 자격을 얻는다.");
                        if (verifyInteraction)
                        {
                            // UI의 입력 잠금 대기 동안만 궤적을 고정한다. 자격/공중 상태는 실제 분리 점프 그대로 쓴다.
                            bool simulated = (bool)Get(mover, "Simulated");
                            mover.GetType().GetProperty("Simulated").SetValue(mover, false);
                            try
                            {
                                var visual = (Transform)Call(trapeze, "PoleVisual", 0);
                                var feet = (Vector3)Call(trapeze, "PoleFeet", 0, false);
                                yield return AssertExistingPoleInteraction(visual, feet + Vector3.up * 1.6f, true);
                            }
                            finally { mover.GetType().GetProperty("Simulated").SetValue(mover, simulated); }
                        }
                        Call(scheme, "Tick", context, CatchFrame(tap: true));
                        caught = true;
                        break;
                    }
                    yield return null;
                }
                Assert.IsTrue(caught, "부상으로 낮아진 목마에서도 실제 도약 중 F 잡기 범위에 도달해야 한다." + trajectory);
                Assert.That((int)Call(trapeze, "HeldPole", player), Is.EqualTo(0));
                yield return null;
                Assert.IsTrue((bool)Call(trapeze, "IsHeld", player));
                if (verifyInteraction)
                {
                    var visual = (Transform)Call(trapeze, "PoleVisual", 0);
                    var feet = (Vector3)Call(trapeze, "PoleFeet", 0, false);
                    yield return AssertExistingPoleInteraction(visual, feet + Vector3.up * 1.6f, false);
                }
            }
            finally { reader.enabled = false; DestroyPracticeBotTestPartner(partner); }
        }

        [UnityTest, Category("TightropeJourneyRegression")]
        public IEnumerator FirstTrapezePoleStillRejectsUnassistedGroundJump()
        {
            EnableTrapezeForEntryTest();
            yield return BeginPerformance(); DisableSwingHazards();
            var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
            yield return Until(() => (bool)Get(trapeze, "IsActive"), 2f, "공중그네 준비");
            yield return StandOnRopeForSawTest((int)Get(trapeze, "LaneIndex"), (float)Get(trapeze, "EntryDistance"));
            Call(mover, "RequestJump");
            yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "혼자 제자리 점프");
            do
            {
                Assert.IsFalse((bool)Call(trapeze, "CanGrabPole", 0, player));
                Assert.IsFalse((bool)Call(trapeze, "TryRequestCatch", player));
                yield return null;
            } while ((bool)Get(mover, "IsAirborne"));
            Assert.That((ulong)Call(trapeze, "HolderAt", 0), Is.EqualTo(ulong.MaxValue));
        }

        void EnableTrapezeForEntryTest()
        {
            JsonUtility.FromJsonOverwrite("{\"trapezeEnabled\":true}", legacyCourseSettings);
            JsonUtility.FromJsonOverwrite("{\"trapezeEnabled\":true}", Get(course, "Shape"));
            Call(course, "Rebuild");
        }

        [UnityTest]
        [Category("TightropeJourneyRegression")]
        public IEnumerator MountAndForwardJumpInSameFrameStartsFromPartnersHead()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            yield return StandOnRopeForSawTest(1, 8f);
            var partner = CreatePracticeBotTestPartner(1, 8.7f);
            var reader = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"));
            try
            {
                reader.enabled = true;
                yield return Until(() => !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 2f, "키 해제 대기");
                var stack = Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
                var follow = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.PiggybackFollow"));
                Call(stack, "ServerTryLink", (ulong)Get(player, "OwnerClientId"));
                Assert.AreSame(partner, Call(stack, "GetBelow", player));
                Assert.IsNull(Get(follow, "localMover"), "이번 프레임에 처음 승인된 탑승을 재현한다.");
                Set(reader, "<Current>k__BackingField", CatchFrame(move: 1f, jump: true));
                Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), "목마 입력 검사는 입력 잠금 해제 후 실행한다.");
                Call(follow, "LateUpdate");
                reader.enabled = false;
                Assert.IsNull(Call(stack, "GetBelow", player), "같은 프레임의 W+Space도 목마 분리 점프를 해야 한다.");
                Assert.IsTrue((bool)Get(mover, "Simulated"));
                float height = (float)Get(partner.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "BodyHeight");
                Assert.That(player.transform.position.y, Is.EqualTo(partner.transform.position.y + height).Within(.04f));
                var switcher = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.CourseControlSwitcher"));
                Call(switcher, "UpdateSurfaceMode");
                Assert.IsFalse((bool)Get(mover, "FreeMovement"), "운동 Update 직전에도 목마 점프를 플랫폼 착지로 오인하지 않는다.");
                Assert.That(Get(player.GetComponent(GameType("CurtainCall.Player.PlayerController")), "Scheme").GetType().Name,
                    Is.EqualTo("TightropeControlScheme"));
                yield return null;
                Assert.IsTrue((bool)Get(mover, "IsAirborne"));
            }
            finally { reader.enabled = false; DestroyPracticeBotTestPartner(partner); }
        }

        [UnityTest]
        [Category("TightropeJourneyRegression")]
        public IEnumerator TrapezeReleaseIsFlightBeforeMotionUpdateAndRestartClearsIt()
        {
            JsonUtility.FromJsonOverwrite("{\"trapezeEnabled\":true}", legacyCourseSettings);
            JsonUtility.FromJsonOverwrite("{\"trapezeEnabled\":true}", Get(course, "Shape"));
            Call(course, "Rebuild");
            yield return BeginPerformance(); DisableSwingHazards();
            var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
            yield return Until(() => (bool)Get(trapeze, "IsActive"), 2f, "공중그네 라운드 준비");
            var poles = (Array)Get(trapeze, "poles");
            var pole = poles.GetValue(0);
            ulong owner = (ulong)Get(player, "OwnerClientId");
            Set(pole, "Upper", owner);
            Set(pole, "Until", (double)Get(trapeze, "Clock") + 30d);
            Call(trapeze, "ReconcileLocal");
            Assert.IsTrue((bool)Call(trapeze, "IsHeld", player));
            Assert.IsFalse((bool)Call(trapeze, "IsInFlight", player));

            var command = trapeze.GetType().GetNestedType("Command", BindingFlags.NonPublic);
            Call(trapeze, "HandleRequest", owner, Enum.Parse(command, "Release"), 0, (uint)Get(pole, "Revision"), 0f, 0u);
            Assert.IsFalse((bool)Get(trapeze, "localFlying"), "운동 Update 이전의 요청 처리 시점을 검사한다.");
            Assert.IsTrue((bool)Call(trapeze, "IsInFlight", player), "서버가 발사를 승인한 즉시 비행으로 판정해야 한다.");
            Assert.IsFalse((bool)Call(trapeze, "IsHeld", player));
            Call(trapeze, "ReconcileLocal");
            Assert.IsTrue((bool)Get(trapeze, "localFlying"));

            Vector3 beforeReply = player.transform.position;
            Set(trapeze, "flightVelocity", Vector3.down * 4f);
            double waitingUntil = (double)Get(trapeze, "Clock") + .5d;
            Set(trapeze, "pendingUntil", waitingUntil);
            Call(trapeze, "StepFlight");
            Assert.That(player.transform.position.y, Is.LessThan(beforeReply.y), "F 답변 대기 중에도 공중에 멈추지 않는다.");
            Assert.IsFalse((bool)Call(trapeze, "TryRequestCatch", player));
            Assert.That((double)Get(trapeze, "pendingUntil"), Is.EqualTo(waitingUntil), "중복 F로 대기 시간이 늘어나지 않는다.");
            Set(trapeze, "pendingUntil", (double)Get(trapeze, "Clock") - .01d);
            Call(trapeze, "StepFlight");
            Assert.That(player.transform.position.y, Is.LessThan(beforeReply.y), "답변이 없어도 같은 비행을 계속한다.");

            uint ticket = (uint)Get(trapeze, "localFlightToken");
            Set(trapeze, "finishedFlightToken", ticket);
            Assert.IsFalse((bool)Call(trapeze, "IsInFlight", player), "착지 완료 후 남은 서버 스냅샷으로 비행을 되살리지 않는다.");
            Set(trapeze, "finishedFlightToken", 0u);
            Set(trapeze, "localAborted", true);
            Assert.IsFalse((bool)Call(trapeze, "IsInFlight", player), "취소된 비행은 서버 정리 전에도 입력을 잡지 않는다.");
            Set(trapeze, "localAborted", false);

            int generation = (int)Get(run, "Round");
            var hud = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return Until(() => (int)Get(run, "Round") > generation, 5f, "비행 중 전체 재시작");
            yield return null;
            Assert.IsFalse((bool)Call(trapeze, "IsInFlight", player));
            Assert.IsTrue((bool)Get(mover, "Simulated"));
            Assert.IsTrue(((CharacterController)Get(mover, "Body")).enabled);
        }
    }
}
