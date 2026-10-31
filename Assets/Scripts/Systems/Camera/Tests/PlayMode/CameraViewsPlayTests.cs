using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Cinemachine;
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

        [UnityTest]
        public IEnumerator NearbyModel_RespectsSettingAndRestoresWithDistance()
        {
            const string key = "CurtainCall.Settings.HideNearbyCharacter";
            bool existed = PlayerPrefs.HasKey(key);
            int saved = PlayerPrefs.GetInt(key, 1);
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            var visibility = director.GetComponent(GameType("CurtainCall.Cameras.CameraModelVisibility"));
            var slot = local.GetComponent(GameType("CurtainCall.Player.PlayerModelSlot"));
            var model = (GameObject)Get(slot, "CurrentModel");
            Assert.IsNotNull(model);
            var renderer = model.GetComponentsInChildren<Renderer>(true).First(r => r.enabled && !r.forceRenderingOff);
            var hide = visibility.GetType().GetMethod("Hide", BindingFlags.Instance | BindingFlags.NonPublic);
            var restore = visibility.GetType().GetMethod("Restore", BindingFlags.Instance | BindingFlags.NonPublic);
            var camera = Camera.main;
            Vector3 before = camera.transform.position;
            try
            {
                var preset = Get(director, "CurrentView");
                Vector3 center = local.transform.position + Vector3.up * (float)Get(preset, "LookHeight");
                camera.transform.position = center + Vector3.back * .5f;
                PlayerPrefs.SetInt(key, 1);
                hide.Invoke(visibility, new object[] { camera });
                Assert.IsTrue(renderer.forceRenderingOff, "가까우면 내 모델을 숨김");
                restore.Invoke(visibility, null);
                camera.transform.position = center + Vector3.back * 1.3f;
                hide.Invoke(visibility, new object[] { camera });
                Assert.IsTrue(renderer.forceRenderingOff, "경계에서는 숨김을 유지해 깜빡임 방지");
                restore.Invoke(visibility, null);
                camera.transform.position = center + Vector3.back * 1.6f;
                hide.Invoke(visibility, new object[] { camera });
                Assert.IsFalse(renderer.forceRenderingOff, "멀어지면 다시 표시");
                camera.transform.position = center + Vector3.back * .5f;
                PlayerPrefs.SetInt(key, 0);
                hide.Invoke(visibility, new object[] { camera });
                Assert.IsFalse(renderer.forceRenderingOff, "설정을 끄면 가까워도 표시");
            }
            finally
            {
                restore.Invoke(visibility, null);
                camera.transform.position = before;
                if (existed) PlayerPrefs.SetInt(key, saved); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator WaitingRoom_WallShortensCameraArmAndRemovalRestoresItGradually()
        {
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            Call(director, "SetInputBlocked", true);
            Mover.GetType().GetProperty("Simulated").SetValue(Mover, false);
            yield return Teleport(new Vector3(1000f, 30f, 1000f));
            yield return Seconds(.8f);
            var preset = Get(director, "CurrentView");
            Vector3 pivot = local.transform.position + Vector3.up * (float)Get(preset, "LookHeight");
            Vector3 original = Camera.main.transform.position;
            Vector3 direction = (original - pivot).normalized;
            float originalDistance = Vector3.Distance(pivot, original);
            Assert.Greater(originalDistance, 2f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.SetPositionAndRotation(pivot + direction * 1.5f, Quaternion.LookRotation(direction));
                wall.transform.localScale = new Vector3(5f, 5f, .2f);
                Physics.SyncTransforms();
                yield return Frames(2);
                float blocked = Vector3.Distance(pivot, Camera.main.transform.position);
                Assert.Less(blocked, 1.4f, "벽 앞에서 카메라를 당긴다");
                wall.SetActive(false);
                Physics.SyncTransforms();
                yield return Frames(1);
                float returning = Vector3.Distance(pivot, Camera.main.transform.position);
                Assert.Less(returning, originalDistance - .1f, "벽이 사라져도 한 프레임에 원래 거리로 튀지 않는다");
                yield return Seconds(1.5f);
                Assert.That(Vector3.Distance(pivot, Camera.main.transform.position), Is.EqualTo(originalDistance).Within(.2f));
            }
            finally { Object.Destroy(wall); }
        }

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
        public IEnumerator WaitingRoom_ReentryBlendsFromRoomWithoutChangingViewKind()
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
            yield return Teleport(new Vector3(11f, .08f, 70));
            yield return Seconds(.4f);
            Assert.AreSame(close, Get(director, "CurrentView"), "문턱을 지나는 동안은 좁은 시점을 유지한다");
            Call(Mover, "SetStartPose", new Vector3(9.6f, .08f, 70), Quaternion.identity);
            yield return FixedSteps(1);
            yield return Frames(1);
            var lobby = (ScriptableObject)Get(director, "CurrentView");
            Assert.AreEqual("WaitingRoomLobbyCamera", lobby.name);
            Assert.AreEqual(Get(close, "Kind"), Get(lobby, "Kind"), "시점 종류는 유지한다");
            Assert.AreEqual(duration, (float)Get(lobby, "TransitionDuration"), .001f);
            Assert.Greater(duration, 0);
            var target = local.transform.position + new Vector3(0, (float)Get(lobby, "Height"), -(float)Get(lobby, "Distance"));
            Assert.Greater(Vector3.Distance(Camera.main.transform.position, target), .1f, "재진입 첫 화면은 목표 위치로 즉시 자르지 않는다");
            // 시점 전환이 끝난 뒤에도 충돌 해소에 따른 거리 복구가 진행될 수 있다.
            yield return Seconds(duration + 1.2f);
            // 목표 뒤에 벽이 있으면 스프링 암이 원래 위치까지 복귀하지 않는다.
            // 고정된 플레이어에 대해 보간이 끝난 뒤 출력이 안정됐는지 검사한다.
            var settled = Camera.main.transform.position;
            yield return Seconds(.3f);
            AssertVector(settled, Camera.main.transform.position, "복귀 보간 완료 후 안정", .1f);
            yield return Teleport(new Vector3(11f, .08f, 70));
            Assert.AreSame(lobby, Get(director, "CurrentView"), "경계 여유 구간에만 돌아가도 좁은 시점에 재진입하지 않는다");
        }

        [UnityTest]
        public IEnumerator WaitingRoom_StagePassageRetainsViewUntilPastEntrance()
        {
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            Mover.GetType().GetProperty("Simulated").SetValue(Mover, false);
            var zone = GameObject.Find("Zone_StageApproach");
            Assert.IsNotNull(zone, "현재 통로에 시점 구역이 있어야 한다");
            var component = zone.GetComponent(GameType("CurtainCall.Cameras.CameraViewZone"));
            yield return Teleport(new Vector3(-6f, .08f, 57f));
            Assert.AreSame(director, GetPrivate(component, "registered"), "통로 진입 시 가까운 시점을 등록한다");
            yield return Teleport(new Vector3(-6f, .08f, 63.8f));
            Assert.AreSame(director, GetPrivate(component, "registered"), "문턱에서는 통로 시점을 유지한다");
            yield return Teleport(new Vector3(-6f, .08f, 64.9f));
            Assert.IsNull(GetPrivate(component, "registered"), "문턱을 완전히 벗어나면 통로 시점을 해제한다");
            yield return Teleport(new Vector3(-6f, .08f, 63.8f));
            Assert.IsNull(GetPrivate(component, "registered"), "여유 구간만 밟아서는 다시 진입하지 않는다");
        }

        [UnityTest]
        public IEnumerator DeathView_FinalizeDropsOnlyThenHoldsDespiteTargetAndPresetChanges()
        {
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            Call(director, "SetInputBlocked", true);
            Mover.GetType().GetProperty("Simulated").SetValue(Mover, false);
            yield return Teleport(local.transform.position + Vector3.up * 12f);
            yield return Seconds(.6f);
            var camera = director.GetComponent<CinemachineCamera>();
            var before = camera.State;
            SetDead();
            camera.InternalUpdateCameraState(Vector3.up, -1f);
            AssertVector(before.GetFinalPosition(), camera.State.GetFinalPosition(), "사망 직전 출력에서 시작", .05f);
            Assert.That(SpectatorTarget, Is.Null, "낙하 관찰 중 관전 대상 없음");

            var request = ScriptableObject.CreateInstance(GameType("CurtainCall.Cameras.CameraViewPreset"));
            try
            {
                request.GetType().GetField("Height").SetValue(request, 40f);
                request.GetType().GetField("FieldOfView").SetValue(request, 100f);
                Call(director, "RequestView", director, request, 1000, Vector3.right);
                Call(Mover, "SetStartPose", local.transform.position + new Vector3(20f, -30f, 15f), Quaternion.Euler(0f, 120f, 0f));
                SetPrivate(director, "deathStarted", Time.unscaledTime - .3f);
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                AssertVector(before.GetFinalPosition() + Vector3.down * .75f, camera.State.GetFinalPosition(), "절반 시간에는 아래로만 이동", .02f);
                var halfway = camera.State;
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                AssertVector(halfway.GetFinalPosition(), camera.State.GetFinalPosition(), "같은 프레임 pipeline 재호출은 누적 이동하지 않음");
                Assert.Less(Quaternion.Angle(before.GetFinalOrientation(), camera.State.GetFinalOrientation()), .01f);
                Assert.AreEqual(before.Lens.FieldOfView, camera.State.Lens.FieldOfView, .001f, "시점 요청의 줌 변경도 차단");

                SetPrivate(director, "deathStarted", Time.unscaledTime - 2f);
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                var stopped = before.GetFinalPosition() + Vector3.down * 1.5f;
                AssertVector(stopped, camera.State.GetFinalPosition(), "최대 1.5m에서 정지");
                yield return Frames(4);
                AssertVector(stopped, camera.State.GetFinalPosition(), "생존자 없으면 관전 대기 시간 뒤에도 고정");
                AssertVector(stopped, Camera.main.transform.position, "Brain에도 최종 고정 포즈 적용", .02f);
                Assert.Less(Quaternion.Angle(before.GetFinalOrientation(), camera.State.GetFinalOrientation()), .01f);

                Call(local, "ServerRestoreAll");
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                Assert.IsFalse((bool)GetPrivate(director, "deathView"), "부활 시 고정 해제");
                Assert.IsFalse((bool)Get(GameType("CurtainCall.Cameras.CameraSpectator"), "IsSpectating"));
                yield return Seconds(.8f);
                Assert.Greater(Vector3.Distance(stopped, camera.State.GetFinalPosition()), 1f, "부활 뒤 다시 추적");
            }
            finally
            {
                Call(director, "ReleaseView", director);
                Object.Destroy(request);
            }
        }

        [UnityTest]
        public IEnumerator DeathView_GroundDeathStaysAboveFloorAndLooksAtCorpseLocation()
        {
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                floor.transform.position = new Vector3(1000f, 29.5f, 1000f);
                floor.transform.localScale = new Vector3(20f, 1f, 20f);
                Mover.GetType().GetProperty("Simulated").SetValue(Mover, false);
                yield return Teleport(new Vector3(1000f, 30.05f, 1000f));
                Physics.SyncTransforms();
                var camera = director.GetComponent<CinemachineCamera>();
                var low = camera.State;
                low.RawPosition = new Vector3(1000f, 30.1f, 1000f);
                low.PositionCorrection = Vector3.zero;
                SetPrivate(director, "lastOutput", low);
                SetDead();
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                var held = camera.State;
                Assert.That(held.GetFinalPosition().y, Is.GreaterThanOrEqualTo(31.2f));
                Vector3 target = new Vector3(1000f, 30.35f, 1000f);
                Assert.That(Vector3.Angle(held.GetFinalOrientation() * Vector3.forward, target - held.GetFinalPosition()), Is.LessThan(.1f));
                Assert.That((float)GetPrivate(director, "deathDropDistance"), Is.Zero);
                Call(Mover, "SetStartPose", local.transform.position + Vector3.down * 20f, Quaternion.identity);
                SetPrivate(director, "deathStarted", Time.unscaledTime - 2f);
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                AssertVector(held.GetFinalPosition(), camera.State.GetFinalPosition(), "땅 아래 캐릭터 루트나 하강 연출을 따라가지 않음");
                yield return Frames(3);
                AssertVector(held.GetFinalPosition(), Camera.main.transform.position, "실제 화면도 바닥 위에 고정", .02f);
                Vector3 viewport = Camera.main.WorldToViewportPoint(target);
                Assert.That(viewport.z, Is.GreaterThan(0f));
                Assert.That(viewport.x, Is.EqualTo(.5f).Within(.02f));
                Assert.That(viewport.y, Is.EqualTo(.5f).Within(.02f), "사망 위치가 화면 중앙에 보여야 한다.");
            }
            finally { Object.Destroy(floor); }
        }

        [UnityTest]
        public IEnumerator DeathView_AirborneDropStopsBeforeCameraTouchesFloor()
        {
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                floor.transform.position = new Vector3(1000f, 29.5f, 1000f);
                floor.transform.localScale = new Vector3(20f, 1f, 20f);
                Mover.GetType().GetProperty("Simulated").SetValue(Mover, false);
                yield return Teleport(new Vector3(1000f, 40f, 1000f));
                Physics.SyncTransforms();
                var camera = director.GetComponent<CinemachineCamera>();
                var low = camera.State;
                low.RawPosition = new Vector3(1000f, 31f, 1000f);
                low.PositionCorrection = Vector3.zero;
                SetPrivate(director, "lastOutput", low);
                SetDead();
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                SetPrivate(director, "deathStarted", Time.unscaledTime - 1f);
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                Assert.That(camera.State.GetFinalPosition().y, Is.GreaterThanOrEqualTo(30.29f), "공중 사망도 카메라가 지면을 뚫고 하강하면 안 된다.");
                Assert.That(camera.State.GetFinalPosition().y, Is.LessThan(31f), "가능한 만큼의 짧은 하강은 유지한다.");
            }
            finally { Object.Destroy(floor); }
        }

        [UnityTest]
        public IEnumerator DeathView_SurvivorSelectionWaitsThenReturnsToExistingSpectating()
        {
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            // 다른 PC 연결 검증이 아니라 대상 선택 회귀다. 미생성 플레이어를 생존자 목록에만 넣는다.
            var survivorObject = new GameObject("Camera test survivor");
            var survivor = survivorObject.AddComponent(GameType(PlayerType));
            var players = (IList)Get(GameType(PlayerType), "All");
            players.Add(survivor);
            try
            {
                survivor.transform.position = local.transform.position + Vector3.right * 10f;
                var camera = director.GetComponent<CinemachineCamera>();
                SetDead();
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                Assert.That(SpectatorTarget, Is.Null);
                Assert.AreSame(local.transform, camera.Target.TrackingTarget);
                SetPrivate(director, "deathStarted", Time.unscaledTime - 1.4f);
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                Assert.That(SpectatorTarget, Is.Null, "1.5초 전에는 생존자가 있어도 전환하지 않음");
                SetPrivate(director, "deathStarted", Time.unscaledTime - 1.6f);
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                Assert.AreSame(survivor, SpectatorTarget);
                Assert.AreSame(survivor.transform, camera.Target.TrackingTarget);
                Assert.IsTrue(camera.FollowTargetChanged, "PrePipeline 관전 전환도 Body 전에 대상 캐시에 반영");
                Assert.IsTrue(camera.LookAtTargetChanged, "Aim 캐시도 같은 pipeline에서 갱신");
                Assert.IsFalse((bool)GetPrivate(director, "deathView"));
                camera.InternalUpdateCameraState(Vector3.up, -1f);
                Assert.IsFalse((bool)GetPrivate(director, "deathView"), "같은 사망을 pipeline마다 재시작하지 않음");
            }
            finally
            {
                players.Remove(survivor);
                Object.Destroy(survivorObject);
            }
        }

        [UnityTest]
        public IEnumerator DeathView_DisableAndActiveSceneChangeDiscardPreviousAnchor()
        {
            yield return Host("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity");
            var camera = director.GetComponent<CinemachineCamera>();
            SetDead();
            camera.InternalUpdateCameraState(Vector3.up, -1f);
            Assert.IsTrue((bool)GetPrivate(director, "deathView"));
            var behaviour = (Behaviour)director;
            behaviour.enabled = false;
            Assert.IsFalse((bool)GetPrivate(director, "deathView"));
            Assert.IsFalse((bool)GetPrivate(director, "hasDeathOutput"));
            behaviour.enabled = true;
            camera.InternalUpdateCameraState(Vector3.up, -1f);
            Assert.IsTrue((bool)GetPrivate(director, "deathView"));
            var spectator = (Behaviour)director.GetComponent(GameType("CurtainCall.Cameras.CameraSpectator"));
            spectator.enabled = false;
            Assert.IsFalse((bool)GetPrivate(director, "deathView"), "관전 비활성화도 화면 고정 정리");
            spectator.enabled = true;
            Call(local, "ServerRestoreAll");
            camera.InternalUpdateCameraState(Vector3.up, -1f);
            SetDead();
            camera.InternalUpdateCameraState(Vector3.up, -1f);

            var previous = SceneManager.GetActiveScene();
            var next = SceneManager.CreateScene("Camera death cleanup test");
            try
            {
                Assert.IsTrue(SceneManager.SetActiveScene(next));
                Assert.IsFalse((bool)GetPrivate(director, "deathView"));
                Assert.IsFalse((bool)GetPrivate(director, "hasOutput"), "다음 씬에서 이전 출력 기준점을 재사용하지 않음");
                Assert.That(SpectatorTarget, Is.Null);
                Assert.IsFalse((bool)Get(GameType("CurtainCall.Cameras.CameraSpectator"), "IsSpectating"));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                SceneManager.UnloadSceneAsync(next);
            }
        }

        object SpectatorTarget => Get(GameType("CurtainCall.Cameras.CameraSpectator"), "Target");

        void SetDead() => Call(local, "ServerSetState", Enum.Parse(GameType("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen"));

        static object GetPrivate(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        static void SetPrivate(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

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
