using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Player.EditorTools.Tests
{
    /// <summary>Task-037 공용 조작 규칙: 기본 이동 규칙 선택·규칙 고르는 순서·입력 막기 요청자.</summary>
    public class ControlRulesTests
    {
        static T Make<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            obj.hideFlags = HideFlags.DontSave;
            return obj;
        }

        [Test]
        public void DefaultScheme_EmptySettings_UsesPlatformMovement()
        {
            var settings = Make<ControlSettings>();
            Assert.IsInstanceOf<PlatformControlScheme>(settings.ResolveDefault());
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void DefaultScheme_Changed_IsUsedRightAway()
        {
            var settings = Make<ControlSettings>();
            var other = Make<PlatformControlScheme>();
            settings.DefaultScheme = other;
            Assert.AreSame(other, settings.ResolveDefault());
            settings.DefaultScheme = null;
            Assert.AreNotSame(other, settings.ResolveDefault());
            Assert.IsInstanceOf<PlatformControlScheme>(settings.ResolveDefault());
            Object.DestroyImmediate(other);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void ChooseScheme_Order_AssignedStartSceneDefault()
        {
            var assigned = Make<PlatformControlScheme>();
            var start = Make<PlatformControlScheme>();
            var scene = Make<PlatformControlScheme>();
            var fallback = Make<PlatformControlScheme>();

            Assert.AreSame(fallback, PlayerController.ChooseScheme(null, null, null, fallback));
            Assert.AreSame(scene, PlayerController.ChooseScheme(null, null, scene, fallback));
            Assert.AreSame(start, PlayerController.ChooseScheme(null, start, scene, fallback));
            Assert.AreSame(assigned, PlayerController.ChooseScheme(assigned, start, scene, fallback));

            Object.DestroyImmediate(assigned);
            Object.DestroyImmediate(start);
            Object.DestroyImmediate(scene);
            Object.DestroyImmediate(fallback);
        }

        [Test]
        public void InputGate_StaysRequestedUntilEveryRequesterCloses()
        {
            var shop = new object();
            var chat = new object();
            Assume.That(InputGate.IsRequested, Is.False);

            InputGate.SetBlocking(shop, true);
            InputGate.SetBlocking(chat, true);
            InputGate.SetBlocking(shop, true); // 같은 요청자는 한 번만
            Assert.IsTrue(InputGate.IsRequested);
            Assert.IsTrue(InputGate.IsBlocked);

            InputGate.SetBlocking(shop, false);
            Assert.IsTrue(InputGate.IsRequested, "다른 UI가 열려 있으면 계속 막는다");

            InputGate.SetBlocking(chat, false);
            Assert.IsFalse(InputGate.IsRequested);
            Assert.IsTrue(InputGate.IsBlocked, "닫힌 프레임에는 키를 놓을 때까지 막는다");
        }

        [Test]
        public void InputGate_NullRequester_IsIgnored()
        {
            InputGate.SetBlocking(null, true);
            Assert.IsFalse(InputGate.IsRequested);
        }
    }
}
