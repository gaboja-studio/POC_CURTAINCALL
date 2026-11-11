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
        public IEnumerator TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow()
        {
            DisableEnhancementThreats();
            const int index = 0; // 첫 구간 톱벽 그네(2026-11-02 NormalPole 삭제로 1 → 0)
            double grabRequestedAt = 0;
            yield return GrabEnhancementPole(index, time => grabRequestedAt = time);
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", index);
            var state = Call(poles, "State", index);
            var gate = Get(pole, "LinkedGate");
            Assert.IsNotNull(gate);
            foreach (Transform blade in ((Component)gate).transform.Find("SawVisual").GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("GateSawDisc")))
                Assert.That(Vector3.Dot(blade.forward, Vector3.right), Is.GreaterThan(.999f),
                    "실제 호스트의 관문 업데이트 후에도 개별 톱날90도 방향을 유지한다.");
            Assert.IsFalse((bool)Get(state, "IsUnlimitedHold"));
            double rearmAt = (double)Get(state, "RearmAt");
            // 잡기 확인 뒤 프레임 대기 시간이 남은 40초에 포함되지 않도록 요청 시각으로 비교한다.
            Assert.That(rearmAt, Is.InRange(grabRequestedAt + 40d, (double)Get(poles, "Clock") + 40d));
            var gateTransform = ((Component)gate).transform;
            var walls = new[] { "LeftBlades", "RightBlades" }.Select(n => gateTransform.Find("SawVisual/" + n).GetComponent<Collider>()).ToArray();
            var fence = gateTransform.Find("Barrier/MetalMesh");
            Assert.IsTrue((bool)Get(gate, "IsOpen"), "봉을 잡는 순간 톱벽이 갈라지기 시작한다.");
            Assert.IsFalse((bool)Get(gate, "IsSpinning"), "잡으면 톱이 먼저 멈춘다.");
            Assert.That((float)Get(gate, "OpenAmount"), Is.LessThan(.5f), "멈춘 뒤 갈라지기 시작한다.");
            yield return WaitUntil(() => (float)Get(gate, "OpenAmount") >= 1f, 2.5f, "0.5초 멈춤 뒤 1초 동안 좌우로 갈라짐");
            Assert.IsTrue((bool)Get(gate, "IsSpinning"), "다 열리면 양쪽 톱이 다시 돈다.");
            Physics.SyncTransforms();
            float gap = walls[1].bounds.min.x - walls[0].bounds.max.x;
            Assert.That(gap, Is.EqualTo((float)Get(gate, "OpenGap")).Within(.02f), "트램펄린 폭 + 좌우 0.5m 틈");
            Assert.IsTrue(walls.All(w => w.enabled), "갈라진 양쪽 벽은 계속 막는다.");
            // 바닥(x ±22) 밖으로 밀려난 양쪽 바깥 톱 3장씩이 떨어져 낙하 부상 높이에서 사라진다.
            yield return WaitUntil(() => (int)Get(gate, "GoneBladeCount") == 6, 3f, "바닥 밖 톱 낙하 후 소멸");
            Assert.AreEqual(0, (int)Get(gate, "FallingBladeCount"));
            // 바닥 밖으로 삐져나온 양쪽 끝 철망·위 파이프 조각 1개씩(4개)도 함께 떨어져 사라진다.
            Assert.That((int)Get(gate, "FallingFenceCount") + (int)Get(gate, "GoneFenceCount"), Is.EqualTo(4));
            yield return WaitUntil(() => (int)Get(gate, "GoneFenceCount") == 4, 2f, "바닥 밖 철망 낙하 후 소멸");
            Assert.IsTrue(fence.GetComponentsInChildren<Renderer>().All(r => r.enabled));
            Assert.IsNull(gateTransform.Find("AppliedGateFrame/TopBeam"), "열린 틈을 가로지르는 통파이프가 남지 않는다.");
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
            yield return WaitUntil(() => Get(gate, "Phase").ToString() == "Active", 45f, "40초 뒤 봉 주기 종료");
            Assert.That((double)Get(poles, "Clock"), Is.GreaterThanOrEqualTo(rearmAt));
            Assert.IsTrue((bool)Get(gate, "IsOpen"), "한 번 열린 톱벽은 닫히지 않는다.");
            Assert.That(walls[1].bounds.min.x - walls[0].bounds.max.x, Is.EqualTo(gap).Within(.001f));
        }

        [UnityTest]
        public IEnumerator AirbornePlayerCrossesTheGateBeforeTheTrampolineCarrier()
        {
            DisableEnhancementThreats();
            yield return GrabEnhancementPole(0);
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", 0);
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
        public IEnumerator UpperMetalMeshBlocksFlightWithoutCuttingAboveTheSawRow()
        {
            DisableEnhancementThreats();
            yield return Board(0);
            yield return ReleaseThenArm();
            Input(tapped: true);
            Assert.IsTrue(Flying);
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var gate = (Component)Get(Call(poles, "Pole", 0), "LinkedGate");
            var row = gate.transform.Find("SawVisual").GetComponentsInChildren<Renderer>();
            float sawTop = row.Max(r => r.bounds.max.y);
            Call(pilot, "Launch", 0, Get(pilot, "launchRevision"),
                gate.transform.position + new Vector3(0, 3.4f, -1), Quaternion.identity, Vector3.forward * 4f);
            bool blocked = false;
            while (player.transform.position.y > sawTop + .3f)
            {
                Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")),
                    $"Above-row flight: feet={player.transform.position}, initial saw top={sawTop}, "
                    + string.Join("; ", Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonSawGate"), FindObjectsSortMode.None)
                        .Cast<Component>().Select(g => $"{g.transform.position}:touch={Call(g, "Touches", player)}")));
                float side = Vector3.Dot(player.transform.position - gate.transform.position, gate.transform.forward);
                Assert.That(side, Is.LessThan(0), "철망을 뚫고 통과하지 않는다.");
                if (side > -.8f) blocked = true;
                yield return null;
            }
            Assert.IsTrue(blocked, "톱날 위 철망에 실제 비행 충돌이 발생해야 한다.");
            yield return WaitUntil(() => Convert.ToInt32(Get(player, "LostParts")) != 0, 2, "낙하 후 실제 톱날 접촉");
            AssertBody(4);
        }

        [UnityTest]
        public IEnumerator PoleSaw_ActualSwingAvoidsContact_ThenCrossingCutsWithoutForcingRelease()
        {
            DisableEnhancementThreats();
            const int index = 1;
            yield return GrabEnhancementPole(index);
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            var pole = (Component)Call(poles, "Pole", index);
            // Task-044에서 저장 코스의 봉 톱은 제거됐다. 제품 배치는 복구하지 않고 테스트 수명 동안만
            // 독립 톱을 등록해 기존 반동 회피/호스트 접촉 절단/점유 유지 회귀를 계속 검사한다.
            var course = HazardCourse();
            var originalHazards = (Array)Get(course, "hazards");
            var sawObject = new GameObject("PoleSaw_ContactRegression_TestOnly");
            try
            {
                var saw = (Behaviour)sawObject.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonHazard"));
                saw.enabled = false;
                HazardSet(saw, "kind", Enum.Parse(GameType("CurtainCall.HumanCannon.CannonHazardKind"), "GroundSaw"));
                HazardSet(saw, "radius", .55f);
                HazardSet(saw, "bladeHalfThickness", .12f);
                var anchor = sawObject.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonPoleSaw"));
                Call(anchor, "Configure", pole, saw, 0f);
                // 왕복 궤도는 EditMode에서 확인한다. 이 검사는 중립 몸통 평면의 접촉만 분리한다.
                HazardSet(saw, "travel", Vector3.zero);
                var testHazards = Array.CreateInstance(originalHazards.GetType().GetElementType(), originalHazards.Length + 1);
                Array.Copy(originalHazards, testHazards, originalHazards.Length);
                testHazards.SetValue(saw, originalHazards.Length);
                HazardSet(course, "hazards", testHazards);

                Vector3 rest = (Vector3)Call(pole, "HangingPosition", 0f);
                Vector3 forward = (Vector3)Get(pole, "Forward");
                float deadline = Time.realtimeSinceStartup + 3;
                while (Vector3.Dot(player.transform.position - rest, forward) < 1f && Time.realtimeSinceStartup < deadline)
                {
                    PoleEnhancementInput(1);
                    yield return null;
                }
                Assert.That(Vector3.Dot(player.transform.position - rest, forward), Is.GreaterThanOrEqualTo(1f),
                    "입력으로 실제 몸을 톱날의 앞뒤 접촉 범위 밖으로 움직인다.");
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
                var linked = Get(pole, "LinkedGate");
                if (linked != null) Assert.AreEqual("Retracted", Get(linked, "Phase").ToString(),
                    "테스트 공중 톱의 부상이 지상 관문을 임의로 재가동하지 않는다.");
            }
            finally
            {
                HazardSet(course, "hazards", originalHazards);
                sawObject.SetActive(false);
                Object.Destroy(sawObject);
            }
        }


        [UnityTest]
        public IEnumerator PoleGrab_ExpandedReachAcceptsNearMiss_ButRejectsBeyondTolerance()
        {
            DisableEnhancementThreats();
            // Enabling the real input reader requires one neutral frame before UI is
            // allowed. Keep it enabled here; the controller remains disabled for injection.
            reader.enabled = true;
            yield return WaitUntil(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 3, "Reader neutral release");
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            ShowHiddenPole(poles, 1);
            var pole = (Component)Call(poles, "Pole", 1);
            var grip = (Transform)Get(pole, "Grip");
            yield return WaitUntil(() => Object.FindAnyObjectByType(GameType("CurtainCall.UI.SceneIntegration.CommonInteractionUI")) != null,
                15f, "기존 상호작용 UI 비동기 로드");
            yield return Board(0);
            yield return ReleaseThenArm();
            Input(tapped: true);
            yield return WaitUntil(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 3,
                "Flight camera transition completes before checking interactive UI");
            float reach = (float)Call(pole, "GrabRadius", player, 0f);
            Vector3 centerOffset = player.transform.TransformVector(((CharacterController)Get(mover, "Body")).center);
            Vector3 approach = (Vector3)Get(pole, "Forward");
            Vector3 target = (Vector3)Call(pole, "BarPosition", 0f);
            Call(pilot, "Launch", 0, Get(pilot, "launchRevision"), target + approach * (reach + 1f) - centerOffset, grip.rotation, Vector3.zero);
            AssertPoleInteractionUI(pole, false);
            Input(tapped: true);
            yield return null;
            Assert.AreEqual(-1, Get(pilot, "HeldPole"), "현재 설정의25% 접근 여유 밖에서는 잡지 않는다.");
            Call(pilot, "Launch", 0, Get(pilot, "launchRevision"), target + approach * (reach - .25f) - centerOffset, grip.rotation, Vector3.zero);
            // A real F gesture is selected once per frame. Let the enabled reader
            // observe the new approach after this test-only relocation.
            yield return null;
            Assert.IsTrue((bool)Call(poles, "CanGrab", 1, player), "The configured near-side approach is actually grabbable before checking presentation.");
            AssertPoleInteractionUI(pole, true);
            Input(tapped: true);
            yield return WaitUntil(() => (int)Get(pilot, "HeldPole") == 1, 2, "expanded host grab reach");
            Assert.AreEqual(Actor, Get(Call(poles, "State", 1), "Occupant"));
            yield return null;
            yield return WaitUntil(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 3,
                "Held camera transition completes before checking Space guidance");
            AssertPoleInteractionUI(pole, false);
        }

        void AssertPoleInteractionUI(Component pole, bool expected)
        {
            var ui = (Component)Object.FindAnyObjectByType(GameType("CurtainCall.UI.SceneIntegration.CommonInteractionUI"));
            Assert.IsNotNull(ui, "기존 공통 상호작용 UI 씬을 사용한다.");
            var camera = Camera.main;
            var cameraPosition = camera.transform.position;
            var cameraRotation = camera.transform.rotation;
            bool wasEnabled = reader.enabled;
            try
            {
                var point = (Vector3)Call(pole, "BarPosition", 0f);
                // Observe from the same unobstructed side as the player. A camera placed
                // behind the bar can be inside the current course's physical fence.
                camera.transform.position = player.transform.TransformPoint(((CharacterController)Get(mover, "Body")).center);
                camera.transform.LookAt(point);
                reader.enabled = true;
                Physics.SyncTransforms();
                Call(ui, "LateUpdate");
                var highlighted = (System.Collections.IEnumerable)Get(ui, "Highlighted");
                var visual = (Transform)Get(pole, "CommonVisual") ?? pole.transform;
                bool found = highlighted.Cast<Transform>().Contains(visual);
                Assert.AreEqual(expected, found, "잡기 범위·점유에 맞춰 기존 외곽선이 표시/해제되어야 한다. "
                    + $"material={Get(ui, "material") != null}, blocked={StaticGet("CurtainCall.Player.InputGate", "IsBlocked")}, "
                    + string.Join("; ", ((IEnumerable)Get(ui, "candidates")).Cast<object>().Select(c =>
                        $"{Get(c, "Root")}: available={Get(c, "Available")}, visible={Call(ui, "IsVisible", c, camera, reader.transform)}, point={Get(c, "Point")}")));
                var prompt = Get(ui, "Prompt");
                bool held = (bool)Get(pilot, "IsHoldingPole");
                // C6 enters from alpha zero; the synchronous contract is presentation
                // acceptance. Its fade visibility is covered by InteractionPromptPlayTests.
                Assert.AreEqual(expected || held, Get(prompt, "HasRuntimePresentation"), "잡기 전 F, 매달린 뒤 Space 안내를 사용한다.");
                if (expected)
                {
                    Assert.AreEqual("F", Get(Get(prompt, "keyLabel"), "text"));
                    StringAssert.Contains("잡기", (string)Get(Get(prompt, "actionLabel"), "text"));
                    Assert.IsTrue(visual.GetComponentsInChildren<MeshRenderer>().Any(r => r.name == "__InteractionOutline" && r.enabled));
                }
                if (held) {
                    StringAssert.AreEqualIgnoringCase("Space", (string)Get(Get(prompt, "keyLabel"), "text"));
                    Assert.AreEqual("그네에서 도약", Get(Get(prompt, "actionLabel"), "text"));
                }
            }
            finally { reader.enabled = wasEnabled; camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation); }
        }


        /// <summary>
        /// 일반 그네는 2026-11-04 사용자 결정으로 씬에서 숨겼다. 그네 공통 동작 검사는 숨긴 그네를 이 검사 안에서만 다시 켠다.
        /// </summary>
        static void ShowHiddenPole(object poles, int index)
        {
            var pole = (Component)((Array)Get(poles, "poles")).GetValue(index);
            if (pole != null && !pole.gameObject.activeSelf) pole.gameObject.SetActive(true);
        }

        IEnumerator GrabEnhancementPole(int index, Action<double> onGrabRequest = null)
        {
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            ShowHiddenPole(poles, index);
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
                HazardSet(fire, "available", false);
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
