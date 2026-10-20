using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityEngine.TestTools.UnityTest]
        public IEnumerator WaitingStartCursorIsScopedAndMouseButtonStartsPerformance()
        {
            var hud = (Behaviour)Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            var director = (Component)Static("CurtainCall.Cameras.CameraViewDirector", "Current");
            yield return Until(() => (bool)Get(director, "IsFirstPerson")
                && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 3, "방 대기 중 이동 연습 가능");
            Assert.IsTrue((bool)Get(director, "inputBlocked"), "공연 시작 전 1인칭 카메라는 마우스를 빼앗지 않는다.");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsTrue(Cursor.visible);

            hud.enabled = false;
            yield return null;
            Assert.IsFalse((bool)Static("CurtainCall.Tightrope.UI.TightropeCampaignHud", "RequiresStartCursor"));
            Assert.IsFalse((bool)Get(director, "inputBlocked"), "HUD를 끄면 카메라 입력 제한도 해제한다.");
            hud.enabled = true;
            Call(run, "SetExternallyManaged", true);
            Assert.IsFalse((bool)Static("CurtainCall.Tightrope.UI.TightropeCampaignHud", "RequiresStartCursor"),
                "공연장에서 관리하는 시작 흐름은 독립 테스트 HUD가 가로막지 않는다.");
            Call(run, "SetExternallyManaged", false);
            yield return null;
            yield return null;
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            yield return ClickPracticeButton(hud, "외줄타기 시작 (1명부터 가능)");
            yield return Until(() => (bool)Get(bridge, "PerformanceOpen"), 5, "대기 화면 마우스 클릭으로 공연 시작");
            yield return Until(() => !(bool)Get(director, "inputBlocked"), 3, "시작 후 마우스 시점 조작 재개");
            Assert.IsFalse((bool)Static("CurtainCall.Tightrope.UI.TightropeCampaignHud", "RequiresStartCursor"));
        }

        IEnumerator ClickPracticeButton(Component owner, string label)
        {
            Component button = null;
            yield return Until(() =>
            {
                button = owner.GetComponentsInChildren(GameType("UnityEngine.UI.Button"), false)
                    .Cast<Component>().FirstOrDefault(b => b.GetComponentInChildren(GameType("TMPro.TMP_Text")) is Component text
                        && (string)Get(text, "text") == label);
                return button != null && (bool)Get(button, "interactable");
            }, 5f, label + " 표시·클릭 가능");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var center = (Vector2)rect.TransformPoint(rect.rect.center);
            var oldMouse = Mouse.current;
            var originalSettings = InputSystem.settings;
            var originalBackground = originalSettings.backgroundBehavior;
            var originalEditorInput = originalSettings.editorInputBehaviorInPlayMode;
            bool cloneSettings = UnityEditor.EditorUtility.IsPersistent(originalSettings);
            var pointerSettings = cloneSettings ? Object.Instantiate(originalSettings) : originalSettings;
            if (cloneSettings) { pointerSettings.hideFlags = HideFlags.DontSave; InputSystem.settings = pointerSettings; }
            // MPPM 참가자 창/검사 창이 포커스를 가져가도 합성 포인터를 게임 UI에 전달한다.
            pointerSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            pointerSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pointer = InputSystem.AddDevice<Mouse>("Tightrope practice UI test pointer");
            try
            {
                InputSystem.QueueStateEvent(pointer, new MouseState { position = center });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(pointer, new MouseState { position = center }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(pointer, new MouseState { position = center });
                yield return null; yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(pointer);
                if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent();
                if (cloneSettings)
                {
                    InputSystem.settings = originalSettings;
                    Object.Destroy(pointerSettings);
                }
                else
                {
                    pointerSettings.backgroundBehavior = originalBackground;
                    pointerSettings.editorInputBehaviorInPlayMode = originalEditorInput;
                }
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator LaneJumpIgnoresCannonAirInputAndAutomaticallyLandsOnChosenRope()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            yield return StandOnRopeForSawTest(2, 4f);
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = System.Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            float previousStep = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                yield return null; // Apply the fixed frame duration before launching.
                Call(scheme, "Enter", context);
                var input = CatchFrame(jump: true); Set(input, "AuxDirection", 1);
                Call(scheme, "Tick", context, input); yield return null;
                Assert.AreEqual("Lane", Get(mover, "CurrentJump").ToString());
                for (int i = 0; i < 120 && (bool)Get(mover, "IsAirborne"); i++)
                {
                    input = CatchFrame(move: i % 2 == 0 ? 1f : -1f);
                    Set(input, "Posture", i % 2 == 0 ? -1f : 1f);
                    // Advance the actual player loop so physics and landing callbacks run too.
                    Call(scheme, "Tick", context, input); yield return null;
                    var air = Static("CurtainCall.Tightrope.TightropeCannonAirControl", "Current");
                    Assert.That((Vector3)Call(air, "Consume"), Is.EqualTo(Vector3.zero), "옆줄 점프는 WASD 공중 보정을 사용하지 않는다.");
                }
                Assert.IsFalse((bool)Get(mover, "IsAirborne"));
                var target = (Vector3)Call(course, "GetLanePosition", 3, 4f);
                Assert.That(Vector3.ProjectOnPlane(player.transform.position - target, Vector3.up).magnitude, Is.LessThan(.04f));
                Assert.AreEqual("Normal", Get(player, "State").ToString());
            }
            finally
            {
                Time.captureDeltaTime = previousStep; Call(scheme, "Exit", context); Object.Destroy(scheme);
            }
        }
    }
}
