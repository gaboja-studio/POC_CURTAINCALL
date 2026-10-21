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
        [UnityTest, Category("TightropeJourneyRegression")]
        public IEnumerator FirstTrapezePoleCatchesJumpFromLeglessPartner() => FirstTrapezePoleInjuredEntry(false);

        [UnityTest, Category("TightropeJourneyRegression")]
        public IEnumerator FirstTrapezePoleCatchesOneArmedJumpFromLeglessPartner() => FirstTrapezePoleInjuredEntry(true);

        IEnumerator FirstTrapezePoleInjuredEntry(bool oneArmedUpper)
        {
            EnableTrapezeForEntryTest();
            yield return BeginPerformance(); DisableSwingHazards();
            var trapeze = Static("CurtainCall.Tightrope.TightropeTrapeze", "Current");
            yield return Until(() => (bool)Get(trapeze, "IsActive"), 2f, "공중그네 준비");
            int lane = (int)Get(trapeze, "LaneIndex");
            float entry = (float)Get(trapeze, "EntryDistance");
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
            Assert.That(player.transform.position, Is.EqualTo(beforeReply), "잡기 답변 대기 중 먼저 추락하지 않는다.");
            Assert.IsFalse((bool)Call(trapeze, "TryRequestCatch", player));
            Assert.That((double)Get(trapeze, "pendingUntil"), Is.EqualTo(waitingUntil), "중복 F로 대기 시간이 늘어나지 않는다.");
            Set(trapeze, "pendingUntil", (double)Get(trapeze, "Clock") - .01d);
            Call(trapeze, "StepFlight");
            Assert.That(player.transform.position.y, Is.LessThan(beforeReply.y), "답변이 없으면 제한 시간 뒤 원래 비행을 재개한다.");

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
