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
            var rect = (RectTransform)button.transform;
            var center = (Vector2)rect.TransformPoint(rect.rect.center);
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
            finally { InputSystem.RemoveDevice(pointer); }
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
                Call(scheme, "Enter", context);
                var input = CatchFrame(jump: true); Set(input, "AuxDirection", 1);
                Call(scheme, "Tick", context, input); Call(mover, "Update");
                Assert.AreEqual("Lane", Get(mover, "CurrentJump").ToString());
                for (int i = 0; i < 120 && (bool)Get(mover, "IsAirborne"); i++)
                {
                    input = CatchFrame(move: i % 2 == 0 ? 1f : -1f);
                    Set(input, "Posture", i % 2 == 0 ? -1f : 1f);
                    Call(scheme, "Tick", context, input); Call(mover, "Update");
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
