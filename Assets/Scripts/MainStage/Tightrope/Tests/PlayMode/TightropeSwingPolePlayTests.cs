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
        public IEnumerator PoleInteraction_SwingUsesExistingOutlineAndFPrompt()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            yield return Until(() => UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.UI.SceneIntegration.CommonInteractionUI")) != null,
                15f, "기존 상호작용 UI 비동기 로드");
            yield return StandOnRopeForSawTest(1, (float)Get(course, "SwingGapFrom") - .8f);
            var pole = Call(Static("CurtainCall.Tightrope.TightropeSwingPole", "Current"), "ForLane", 1);
            Assert.IsTrue((bool)Call(pole, "CanGrabPole", player));
            var visual = (Transform)Get(pole, "PoleVisual");
            yield return AssertExistingPoleInteraction(visual, visual.position, true);
            Call(mover, "SetStartPose", player.transform.position - (Vector3)Get(course, "Forward") * 6f, course.transform.rotation);
            yield return AssertExistingPoleInteraction(visual, visual.position, false);
        }

        IEnumerator AssertExistingPoleInteraction(Transform visual, Vector3 point, bool expected, string action = "봉 잡기")
        {
            var ui = (Component)UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.UI.SceneIntegration.CommonInteractionUI"));
            Assert.IsNotNull(ui, "저장된 공연 씬의 공통 상호작용 UI를 재사용한다.");
            Assert.IsNotNull(visual);
            var camera = Camera.main;
            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            var inputReader = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"));
            bool wasEnabled = inputReader.enabled;
            try
            {
                camera.transform.position = point - (Vector3)Get(course, "Forward") * 2f + Vector3.up * .25f;
                camera.transform.LookAt(point);
                inputReader.enabled = true;
                yield return Until(() => !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 2f, "상호작용 UI 입력 재개");
                camera.transform.position = point - (Vector3)Get(course, "Forward") * 2f + Vector3.up * .25f;
                camera.transform.LookAt(point);
                Physics.SyncTransforms();
                Call(ui, "LateUpdate");
                bool highlighted = false;
                foreach (Transform item in (IEnumerable)Get(ui, "Highlighted")) if (item == visual) highlighted = true;
                Assert.AreEqual(expected, highlighted, "실제 봉 메시가 기존 상호작용 외곽선의 대상이어야 한다.");
                var prompt = Get(ui, "Prompt");
                if (expected)
                {
                    float deadline = Time.realtimeSinceStartup + 2f;
                    while (!(bool)Get(prompt, "IsVisible") && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        camera.transform.position = point - (Vector3)Get(course, "Forward") * 2f + Vector3.up * .25f;
                        camera.transform.LookAt(point);
                        Call(ui, "LateUpdate");
                    }
                    Assert.AreEqual(true, Get(prompt, "IsVisible"), "기존 F 안내의 표시 연출 완료");
                    Assert.AreEqual("F", Get(Get(prompt, "keyLabel"), "text"));
                    Assert.AreEqual(action, Get(Get(prompt, "actionLabel"), "text"));
                    bool shell = false;
                    foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.name == "__InteractionOutline" && renderer.enabled) shell = true;
                    Assert.IsTrue(shell, "기존 아트 메시를 기존 InteractionOutline으로 강조한다.");
                }
                else
                    Assert.IsFalse((bool)Get(prompt, "IsVisible") && (string)Get(Get(prompt, "actionLabel"), "text") == action,
                        "범위를 벗어나면 해당 봉의 F 안내를 숨긴다.");
            }
            finally { inputReader.enabled = wasEnabled; camera.transform.SetPositionAndRotation(previousPosition, previousRotation); }
        }

        [UnityTest]
        public IEnumerator SwingGapHasNoHiddenSupportAndSurvivesWholeCourseRestart()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            Assert.IsTrue((bool)Get(course, "HasSwingGap"));
            float from = (float)Get(course, "SwingGapFrom"), to = (float)Get(course, "SwingGapTo");
            yield return StandOnRopeForSawTest(1, from - 1f);
            var middle = (Vector3)Call(course, "GetLanePosition", 1, (from + to) * .5f);
            Assert.IsFalse(Physics.Raycast(middle + Vector3.up * 3f, Vector3.down, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                "줄 메시만 숨기지 않고 실제 지지면도 제거한다.");
            Assert.IsFalse((bool)Call(course, "IsLaneUsable", 1, (from + to) * .5f));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Get(bridge, "Result") == null && Time.realtimeSinceStartup < deadline)
            { Call(mover, "SetMoveInput", 1f); yield return null; }
            Assert.IsNotNull(Get(bridge, "Result"), $"봉 없이 걸어가면 줄 아래로 추락해 공연 결과가 나야 한다. pos={player.transform.position}");
            Assert.IsFalse((bool)Get(Get(bridge, "Result"), "Succeeded"), "혼자 추락하면 공용 공연 실패로 끝난다. 종료 후 캐릭터 Normal 복구와 구분한다.");
            int generation = (int)Get(run, "Round");
            var hud = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return Until(() => (int)Get(run, "Round") > generation, 5f, "구간 초기화");
            Assert.IsFalse((bool)Call(course, "IsLaneUsable", 1, (from + to) * .5f), "화재를 초기화해도 구조적인 끊어진 줄은 복구되지 않는다.");
        }

        [UnityTest]
        public IEnumerator SwingPoleUsesCannonMomentumAndAirControlThenTeammateCatchesTheLanding()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            // Verify prepared art on runtime instances; do not apply it to the saved scene.
            var profile = Resources.Load<ScriptableObject>("Prefabs/Objects/ETCs/TightropeArtKit/TightropeArtProfile");
            Assert.IsNotNull(profile);
            Set(course, "artProfile", profile);
            Call(course, "BuildMeshes");
            yield return null;
            var pole = (Component)Call(Static("CurtainCall.Tightrope.TightropeSwingPole", "Current"), "ForLane", 1);
            Call(pole, "BuildViews");
            float from = (float)Get(course, "SwingGapFrom"), to = (float)Get(course, "SwingGapTo");
            yield return StandOnRopeForSawTest(1, from - .8f);
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            Component helper = null;
            float captureBefore = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                Call(scheme, "Enter", context);
                helper = CreatePracticeBotTestPartner(1, (float)Get(pole, "LandingDistance"));
                yield return null;
                var catches = (Component)Static("CurtainCall.Tightrope.TightropeCatchSystem", "Current");
                var request = catches.GetType().GetNestedType("Request", BindingFlags.NonPublic);
                Call(catches, "ServerHandle", (ulong)Get(helper, "OwnerClientId"), Enum.Parse(request, "Prepare"), 0UL);
                Assert.IsTrue((bool)Call(catches, "IsReady", helper));
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                yield return Until(() => (bool)Call(pole, "IsHeld", player) && !(bool)Get(mover, "Simulated"), 2f, "F로 실제 봉 탑승");
                Assert.IsFalse(((CharacterController)Get(mover, "Body")).enabled);
                var physical = (Component)Get(pole, "physicalPole");
                Assert.AreEqual("HumanCannonPole", physical.GetType().Name, "회전봉의 움직임·속도는 인간대포 컴포넌트를 사용한다.");
                Assert.That((float)Get(pole, "AirControlSpeed"), Is.EqualTo(3f));
                float deadline = Time.realtimeSinceStartup + 4f;
                bool releaseWindow = false;
                while (Time.realtimeSinceStartup < deadline)
                {
                    float predicted = (float)Call(course, "GetDistance", (Vector3)Call(pole, "PredictLanding", true));
                    if ((bool)Get(pole, "CanLetGo") && predicted >= to + .55f && predicted <= to + 1.7f)
                    { releaseWindow = true; break; }
                    Call(scheme, "Tick", context, CatchFrame(move: 1f)); yield return null;
                    Call(pole, "UpdateViews");
                    var bar = (Transform)Get(pole, "crossbar");
                    var art = bar.Find("Prepared Art");
                    Assert.IsNotNull(art, "The runtime preparation must display the prepared moving pole.");
                    var grip = (Vector3)Call(pole, "WorldFeet", Get(pole, "OwnAngle"), 1) + Vector3.up * 1.3f;
                    Assert.That(Vector3.Distance(bar.position, grip), Is.LessThan(.001f),
                        "The visible bar must follow the physical grip throughout the swing.");
                    Assert.That(Vector3.Distance(art.position, bar.position), Is.LessThan(.001f));
                    Assert.That(art.GetComponentsInChildren<Collider>().Length, Is.Zero,
                        "Prepared art must not introduce extra collision or block the launch.");
                }
                Assert.IsTrue(releaseWindow, $"인간대포의 W 반동으로 건널 창이 있어야 한다. angle={Get(pole, "SwingAngle")}, predicted={Call(pole, "PredictLanding", true)}");
                if (System.IO.File.Exists(".harness/local/tightrope-swing-capture"))
                { ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/tightrope-swing-held.png")); yield return null; }
                Call(scheme, "Tick", context, CatchFrame(move: 1f, jump: true));
                Assert.IsFalse((bool)Call(pole, "IsHeld", player));
                Assert.IsTrue((bool)Call(pole, "IsCrossing", player));
                Assert.IsNotNull(Get(pole, "flightMover"));
                deadline = Time.realtimeSinceStartup + 4f;
                while (Get(pole, "flightMover") != null && Time.realtimeSinceStartup < deadline)
                { Call(scheme, "Tick", context, CatchFrame(move: 1f)); yield return null; }
                Assert.IsNull(Get(pole, "flightMover"), "탄도 비행을 끝내고 원래 이동으로 복구한다.");
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.GreaterThan(to));
                Assert.IsTrue((bool)Get(mover, "Simulated"));
                Assert.IsTrue(((CharacterController)Get(mover, "Body")).enabled);
                Assert.That((float)Call(catches, "SuccessRemaining", player), Is.GreaterThan(0f), "봉 도약도 실제 받기 성공으로 확정한다.");
                var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
                float shock = (float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "RopeMovement"), "LandingShock");
                Assert.That(Mathf.Abs((float)Get(balance, "LastShock")), Is.EqualTo(shock * .1f).Within(.01f));
                if (System.IO.File.Exists(".harness/local/tightrope-swing-capture"))
                { ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/tightrope-swing-catch.png")); yield return null; }
            }
            finally
            {
                Time.captureDeltaTime = captureBefore;
                Call(scheme, "Exit", context); UnityEngine.Object.Destroy(scheme);
                DestroyPracticeBotTestPartner(helper);
            }
        }

        [UnityTest]
        public IEnumerator SwingPoleRejectsAnotherOwnerAndRestartRestoresTheHeldPlayersControls()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            var pole = (Component)Call(Static("CurtainCall.Tightrope.TightropeSwingPole", "Current"), "ForLane", 1);
            yield return StandOnRopeForSawTest(1, (float)Get(course, "SwingGapFrom") - .8f);
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            try
            {
                Assert.IsFalse((bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), "봉 입력 전 공용 입력 차단");
                Assert.IsTrue((bool)Call(pole, "CanGrabPole", player), "봉 입력 전 잡기 자격");
                Call(scheme, "Tick", context, CatchFrame(tap: true));
                Assert.IsTrue((bool)Call(pole, "IsHeld", player), "호스트가 F 입력으로 봉 점유를 확정한다.");
                yield return Until(() => (bool)Call(pole, "IsHeld", player) && !(bool)Get(mover, "Simulated"), 2f, "봉 소유권 확정");
                var operation = pole.GetType().GetNestedType("Operation", BindingFlags.NonPublic);
                Call(pole, "ServerHandle", 777UL, Enum.Parse(operation, "LetGo"), 0f);
                Assert.IsTrue((bool)Call(pole, "IsHeld", player), "다른 소유자는 봉을 놓게 할 수 없다.");
                int generation = (int)Get(run, "Round");
                var hud = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
                Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
                yield return Until(() => (int)Get(run, "Round") > generation, 5f, "봉을 잡은 채 재시작");
                yield return null;
                Assert.IsFalse((bool)Call(pole, "IsHeld", player));
                Assert.IsTrue((bool)Get(mover, "Simulated"));
                Assert.IsTrue(((CharacterController)Get(mover, "Body")).enabled);
                Assert.IsNull(Get(pole, "flightMover"));
            }
            finally { Call(scheme, "Exit", context); UnityEngine.Object.Destroy(scheme); }
        }

        [UnityTest]
        public IEnumerator NativeRopeJumpIgnoresCannonAirInput()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            yield return StandOnRopeForSawTest(2, 4f);
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            float before = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                Call(scheme, "Enter", context);
                Call(scheme, "Tick", context, CatchFrame(jump: true)); Call(mover, "Update");
                Assert.IsTrue((bool)Get(mover, "IsAirborne"));
                float side = (float)Call(course, "GetSide", player.transform.position);
                var input = CatchFrame(move: 1f); Set(input, "Posture", 1f);
                Call(scheme, "Tick", context, input); Call(mover, "Update");
                float changed = (float)Call(course, "GetSide", player.transform.position);
                Assert.That(changed, Is.EqualTo(side).Within(.002f), "일반 줄 위 점프에는 A/D 공중 조작이 적용되지 않는다.");
                Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.EqualTo(4f).Within(.002f), "이륙 후 W 입력도 궤적을 바꾸지 않는다.");
                Call(scheme, "Tick", context, CatchFrame()); Call(mover, "Update");
                Assert.That((float)Call(course, "GetSide", player.transform.position), Is.EqualTo(changed).Within(.002f), "키를 놓으면 전 프레임 보정이 남지 않는다.");
                Assert.That((float)Get(mover, "JumpGravity"), Is.EqualTo((float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "Character"), "Gravity")));
            }
            finally { Time.captureDeltaTime = before; Call(scheme, "Exit", context); UnityEngine.Object.Destroy(scheme); }
        }

        void DisableSwingHazards()
        {
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
        }
    }
}
