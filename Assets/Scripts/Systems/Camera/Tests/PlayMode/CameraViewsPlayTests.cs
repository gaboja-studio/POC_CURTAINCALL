using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Cameras.Tests
{
    /// <summary>
    /// 실제 대기실 + LAN 호스트로 재진입 카메라 보간을 확인한다. 이동은 PlayerMover.SetStartPose 순간이동,
    /// 시선은 director 값 주입이며 실제 키보드·마우스 입력 검증이 아니다.
    /// 게임 코드는 Assembly-CSharp라 asmdef가 참조할 수 없어 이름으로 부른다.
    /// </summary>
    public sealed class CameraViewsPlayTests
    {
        const string SessionType = "CurtainCall.Network.Session.NetworkSessionManager";
        const string PlayerType = "CurtainCall.Network.PlayerSync.NetworkPlayer";
        const ushort Port = 7791;
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        Component director;
        Component local;
        CursorLockMode cursorLock;
        bool cursorVisible;

        Component Mover => local.GetComponent(GameType("CurtainCall.Player.PlayerMover"));

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!IsNull(director)) Call(director, "SetInputBlocked", false);
            if (Get(GameType(SessionType), "Instance") is Component session && session != null)
            {
                var leave = (Task)Call(session, "LeaveAsync");
                yield return WaitFor(() => leave.IsCompleted, 10f, "방 나가기");
                var manager = Get(session, "NetworkManager") as Component;
                if (manager != null)
                {
                    yield return WaitFor(() => !(bool)Get(manager, "IsListening"), 10f, "네트워크 종료");
                    Object.Destroy(manager.gameObject);
                }
                Object.Destroy(session.gameObject);
                yield return null;
            }
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
            director = local = null;
        }

        [UnityTest]
        public IEnumerator WaitingRoom_ReentryBlendsFromCorridorWithoutChangingViewKind()
        {
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            Call(director, "SetInputBlocked", true);
            Mover.GetType().GetProperty("Simulated").SetValue(Mover, false);
            yield return Teleport(new Vector3(12, .08f, 70));
            SetView(0, 0);
            yield return Seconds(.6f);
            var close = (ScriptableObject)Get(director, "CurrentView");
            Assert.AreEqual("WaitingRoomCloseThirdPerson", close.name);
            float duration = (float)Get(close, "TransitionDuration");
            yield return Teleport(new Vector3(9.6f, .08f, 70));
            yield return Seconds(.4f);
            Assert.AreSame(close, Get(director, "CurrentView"), "문턱을 지나는 동안은 좁은 시점을 유지한다");
            Call(Mover, "SetStartPose", new Vector3(8.5f, .08f, 70), Quaternion.identity);
            yield return FixedSteps(1);
            yield return Frames(1);
            var lobby = (ScriptableObject)Get(director, "CurrentView");
            Assert.AreEqual("WaitingRoomLobbyCamera", lobby.name);
            Assert.AreEqual(Get(close, "Kind"), Get(lobby, "Kind"), "시점 종류는 유지한다");
            Assert.AreEqual(duration, (float)Get(lobby, "TransitionDuration"), .001f);
            Assert.Greater(duration, 0);
            var target = local.transform.position + new Vector3(0, (float)Get(lobby, "Height"), -(float)Get(lobby, "Distance"));
            Assert.Greater(Vector3.Distance(Camera.main.transform.position, target), .1f, "재진입 첫 화면은 목표 위치로 즉시 자르지 않는다");
            yield return Seconds(duration + .3f);
            AssertVector(target, Camera.main.transform.position, "복귀 보간 완료", .2f);
            yield return Teleport(new Vector3(9.6f, .08f, 70));
            Assert.AreSame(lobby, Get(director, "CurrentView"), "경계 여유 구간에만 돌아가도 좁은 시점에 재진입하지 않는다");
        }

        IEnumerator Host(string scene)
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scene,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
#endif
            var session = Call(GameType(SessionType), "EnsureExists");
            Assert.IsNotNull(session, "NetworkSessionManager");
            var host = (Task<bool>)Call(session, "HostLanAsync", Port);
            yield return WaitFor(() => host.IsCompleted, 10f, "LAN 호스트");
            Assert.IsTrue(host.Result, "LAN 호스트 시작");
            yield return WaitFor(() => !IsNull(Get(GameType(PlayerType), "Local")), 10f, "로컬 플레이어 생성");
            local = (Component)Get(GameType(PlayerType), "Local");
            director = (Component)Object.FindAnyObjectByType(GameType("CurtainCall.Cameras.CameraViewDirector"));
            Assert.IsNotNull(director, "카메라 리그 CameraViewDirector");
            yield return Frames(3);
        }

        IEnumerator Teleport(Vector3 position)
        {
            Call(Mover, "SetStartPose", position, Quaternion.identity);
            yield return FixedSteps(3);
        }

        /// <summary>시선을 직접 정하고 마우스 적분을 막는다(진입 시 시선 초기화 뒤에 부른다).</summary>
        void SetView(float yaw, float pitch)
        {
            const BindingFlags field = BindingFlags.NonPublic | BindingFlags.Instance;
            Call(director, "SetInputBlocked", true);
            director.GetType().GetField("yaw", field).SetValue(director, yaw);
            director.GetType().GetField("pitch", field).SetValue(director, pitch);
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static IEnumerator FixedSteps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
            yield return Frames(2);
        }

        static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"{what} 대기 시간 초과");
                yield return null;
            }
        }

        static void AssertVector(Vector3 expected, Vector3 actual, string what, float tolerance = 0.001f) =>
            Assert.LessOrEqual(Vector3.Distance(expected, actual), tolerance, $"{what}: 기대 {expected}, 실제 {actual}");

        static bool IsNull(object value) => value == null || (value is Object unityObject && unityObject == null);

        static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        static object Get(object target, string name)
        {
            var type = target as Type ?? target.GetType();
            var instance = target is Type ? null : target;
            var property = type.GetProperty(name, Public);
            if (property != null) return property.GetValue(instance);
            var field = type.GetField(name, Public);
            Assert.IsNotNull(field, $"{type.Name}.{name}");
            return field.GetValue(instance);
        }

        static object Call(object target, string name, params object[] args)
        {
            var type = target as Type ?? target.GetType();
            var method = type.GetMethods(Public).First(m => m.Name == name && m.GetParameters().Length == args.Length);
            return method.Invoke(target is Type ? null : target, args);
        }
    }
}
