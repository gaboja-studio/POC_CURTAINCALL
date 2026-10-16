using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Player.EditorTools.Tests
{
    /// <summary>Task-037 공용 조작 규칙: 기본 이동 규칙 선택·규칙 고르는 순서·입력 막기 요청자. Task-045 1인칭 시선 기준 기본 이동.</summary>
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
        public void DefaultScheme_ProjectAssetAndFallback_AreBothPlatformMovement()
        {
            var asset = Resources.Load<ControlSettings>(ControlSettings.ResourcePath);
            Assert.IsNotNull(asset, "프로젝트 조작 설정 에셋");
            Assert.IsInstanceOf<PlatformControlScheme>(asset.ResolveDefault(), "프로젝트 Default");
            var empty = Make<ControlSettings>();
            Assert.IsInstanceOf<PlatformControlScheme>(empty.ResolveDefault(), "비었을 때 fallback");
            Object.DestroyImmediate(empty);
        }

        // Task-045: 1인칭 시선 기준 입력 → 코스 기준 입력. 코스는 +Z, 결과는 (옆, 앞).
        static Vector3 Yaw(float degrees) => Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;

        static void AssertMove(Vector3 expectedWorld, Vector2 courseInput, Vector3 courseForward, string what)
        {
            Vector3 world = Vector3.Cross(Vector3.up, courseForward) * courseInput.x + courseForward * courseInput.y;
            Assert.LessOrEqual(Vector3.Distance(expectedWorld, world), 0.0001f, $"{what}: 기대 {expectedWorld}, 실제 {world}");
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void FirstPersonInput_FollowsHorizontalView(float yaw)
        {
            Vector3 view = Yaw(yaw);
            Vector3 right = Vector3.Cross(Vector3.up, view);
            Vector3 course = Vector3.forward;
            AssertMove(view, PlatformControlScheme.ToCourseInput(view, course, 0f, 1f), course, "W 시선 전방");
            AssertMove(-view, PlatformControlScheme.ToCourseInput(view, course, 0f, -1f), course, "S 시선 반대");
            AssertMove(-right, PlatformControlScheme.ToCourseInput(view, course, -1f, 0f), course, "A 시선 왼쪽");
            AssertMove(right, PlatformControlScheme.ToCourseInput(view, course, 1f, 0f), course, "D 시선 오른쪽");
        }

        [Test]
        public void FirstPersonInput_IgnoresPitchAndOtherCourseDirections()
        {
            Vector3 pitched = Quaternion.Euler(-40f, 90f, 0f) * Vector3.forward;
            Vector3 course = Yaw(135f);
            AssertMove(Vector3.right, PlatformControlScheme.ToCourseInput(pitched, course, 0f, 1f), course, "pitch는 수평 방향만");
        }

        [Test]
        public void FirstPersonInput_DiagonalIsNotFaster()
        {
            Vector2 input = PlatformControlScheme.ToCourseInput(Yaw(30f), Vector3.forward, 1f, 1f);
            Assert.AreEqual(1f, input.magnitude, 0.0001f, "대각선 길이 1");
            Assert.LessOrEqual(Mathf.Abs(input.x), 1f);
            Assert.LessOrEqual(Mathf.Abs(input.y), 1f);
        }

        [Test]
        public void NoView_KeepsCourseInput()
        {
            Assert.AreEqual(new Vector2(1f, 1f), PlatformControlScheme.ToCourseInput(Vector3.zero, Vector3.forward, 1f, 1f), "3인칭·관전·다른 플레이어");
            Assert.AreEqual(new Vector2(-1f, 0.5f), PlatformControlScheme.ToCourseInput(Vector3.right, Vector3.zero, -1f, 0.5f), "코스 방향 없음");
        }

        [Test]
        public void MoveBasis_FirstPersonHeldThroughViewChange_KeepsViewUntilRelease()
        {
            var basis = new PlatformControlScheme.MoveBasis();
            Assert.AreEqual(Vector3.right, basis.Resolve(Vector3.right, true), "1인칭에서 누름");
            Assert.AreEqual(Vector3.forward, basis.Resolve(Vector3.forward, true), "같은 시점 안에서는 마우스 회전을 따름");
            Assert.AreEqual(Vector3.forward, basis.Resolve(Vector3.zero, true), "누른 채 3인칭: 직전 시선 유지");
            Assert.AreEqual(Vector3.zero, basis.Resolve(Vector3.zero, false), "모두 뗌");
            Assert.AreEqual(Vector3.zero, basis.Resolve(Vector3.zero, true), "다시 누르면 3인칭 코스 기준");
        }

        [Test]
        public void MoveBasis_ThirdPersonHeldIntoFirstPerson_KeepsCourseUntilRelease()
        {
            var basis = new PlatformControlScheme.MoveBasis();
            Assert.AreEqual(Vector3.zero, basis.Resolve(Vector3.zero, true), "3인칭에서 누름");
            Assert.AreEqual(Vector3.zero, basis.Resolve(Vector3.left, true), "누른 채 1인칭: 코스 기준 유지");
            Assert.AreEqual(Vector3.left, basis.Resolve(Vector3.left, false), "모두 뗌");
            Assert.AreEqual(Vector3.left, basis.Resolve(Vector3.left, true), "다시 누르면 1인칭 시선 기준");
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
