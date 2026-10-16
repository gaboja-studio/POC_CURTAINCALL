using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Cameras.Tests
{
    /// <summary>
    /// 실제 씬 + LAN 호스트로 공용 카메라를 확인한다. 이동은 PlayerMover.SetStartPose 순간이동, 요청·상태는 공개 API 호출이며
    /// 실제 키보드·마우스 입력 검증이 아니다. 게임 코드는 Assembly-CSharp라 asmdef가 참조할 수 없어 이름으로 부른다.
    /// </summary>
    public sealed class CameraViewsPlayTests
    {
        const string CameraViewsScene = "Assets/Scenes/Tests/Systems/CameraViews/CameraViews.unity";
        const string ProtoScene = "Assets/Scenes/Prototypes/proto_0.0.2.unity";
        const string SessionType = "CurtainCall.Network.Session.NetworkSessionManager";
        const string PlayerType = "CurtainCall.Network.PlayerSync.NetworkPlayer";
        const string StateType = "CurtainCall.Network.PlayerSync.PlayerState";
        const ushort Port = 7791;
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        static readonly Vector3 InsideZone = new(0f, 0.05f, 10f);
        static readonly Vector3 OutsideZone = new(0f, 0.05f, 0f);
        static readonly Vector3 DefaultFollow = new(0f, 3.75f, -4.5f);

        Component director;
        Component local;
        CursorLockMode cursorLock;
        bool cursorVisible;
        ScriptableObject inputSettings;     // 테스트 키보드 동안 값을 바꿔 둔 Input System 설정(에셋이 아닌 임시 설정만)
        object backgroundBehavior;          // 위 설정의 원래 값
        object editorInputBehavior;
        object keyboard;                    // 테스트가 추가한 Input System 키보드
        GameObject rig;
        float measuredSpeed;

        bool FirstPerson => (bool)Get(director, "IsFirstPerson");
        bool HideModel => (bool)Get(director, "HideLocalModel");
        Vector3 ViewForward => (Vector3)Get(director, "ViewForward");
        bool Spectating => (bool)Get(GameType("CurtainCall.Cameras.CameraSpectator"), "IsSpectating");
        CinemachineFollow Follow => director.GetComponent<CinemachineFollow>();
        CinemachineRotationComposer Composer => director.GetComponent<CinemachineRotationComposer>();
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
            EndKeyboard();
            if (rig != null) Object.Destroy(rig);
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
        public IEnumerator Host_UsesDefaultThirdPersonFraming()
        {
            yield return Host(CameraViewsScene);
            Assert.AreSame(local, Get(director, "LocalPlayer"), "LocalPlayerCamera → director 로컬 플레이어");
            Assert.IsFalse(FirstPerson);
            AssertVector(DefaultFollow, Follow.FollowOffset, "기본 3인칭 따라가기");
            AssertVector(new Vector3(0f, 1.2f, 1.75f), Composer.TargetOffset, "기본 3인칭 바라보기");
            Assert.AreEqual(60f, Camera.main.fieldOfView, 0.01f, "기본 FOV");
            yield return Seconds(1.5f);
            AssertVector(local.transform.position + DefaultFollow, Camera.main.transform.position, "3인칭 카메라 위치", 0.3f);
            Assert.AreEqual(0f, ViewForward.y, 0.0001f, "수평 시선");
            Assert.AreEqual(1f, ViewForward.magnitude, 0.001f, "정규화 시선");
        }

        [UnityTest]
        public IEnumerator CameraScene_DisablesBalanceAfterSpawnAndRestart()
        {
            yield return Host(CameraViewsScene);
            var balance = local.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            Assert.IsNotNull(balance);
            for (int pass = 0; pass < 2; pass++)
            {
                float end = Time.realtimeSinceStartup + 15f;
                do
                {
                    Assert.IsFalse((bool)Get(balance, "IsActive"), "카메라 테스트 씬은 균형 비활성");
                    Assert.IsFalse((bool)Get(balance, "HasFallen"), "균형으로 추락하지 않음");
                    Assert.AreEqual("Normal", Get(local, "State").ToString(), "플레이어 정상 상태 유지");
                    yield return null;
                } while (Time.realtimeSinceStartup < end);
                if (pass == 0)
                {
                    Call(GameType(PlayerType), "ServerRestartAll");
                    yield return Frames(3);
                }
            }
        }

        [UnityTest]
        public IEnumerator Zone_FirstPersonHidesLocalModelOnlyWhileRendering()
        {
            yield return Host(CameraViewsScene);
            var slot = local.GetComponent(GameType("CurtainCall.Player.PlayerModelSlot"));
            var model = Get(slot, "CurrentModel") as GameObject;
            Assert.IsNotNull(model, "로컬 현재 모델");
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            renderers[0].forceRenderingOff = true; // 원래 숨겨 둔 값은 복구 후에도 유지돼야 한다
            var hiddenBefore = renderers.Select(r => r.forceRenderingOff).ToArray();
            var enabledBefore = renderers.Select(r => r.enabled).ToArray();
            try
            {
                yield return Teleport(InsideZone);
                Assert.IsTrue(FirstPerson, "구역 안 1인칭");
                Assert.IsTrue(HideModel);
                AssertVector(EyePosition(), Camera.main.transform.position, "1인칭 눈 위치", 0.05f);
                Assert.AreEqual(0f, Mathf.DeltaAngle(0f, Camera.main.transform.eulerAngles.z), 0.01f, "Dutch 없음");
                Assert.AreEqual(0f, ViewForward.y, 0.0001f);

                bool hiddenWhileRendering = false;
                yield return CaptureMainRender(() => hiddenWhileRendering = renderers.All(r => r.forceRenderingOff));
                Assert.IsTrue(hiddenWhileRendering, "게임 카메라 렌더링 중 숨김");
                CollectionAssert.AreEqual(hiddenBefore, renderers.Select(r => r.forceRenderingOff).ToArray(), "렌더링 밖 원래 값");
                CollectionAssert.AreEqual(enabledBefore, renderers.Select(r => r.enabled).ToArray(), "Renderer.enabled 유지");

                yield return Teleport(OutsideZone);
                Assert.IsFalse(FirstPerson, "구역 밖 3인칭");
                Assert.IsFalse(HideModel);
                AssertVector(DefaultFollow, Follow.FollowOffset, "3인칭 복귀");
                Assert.AreEqual(cursorLock, Cursor.lockState, "커서 복구");
                Assert.AreEqual(cursorVisible, Cursor.visible, "커서 표시 복구");
            }
            finally
            {
                renderers[0].forceRenderingOff = false;
            }
        }

        [UnityTest]
        public IEnumerator Requests_PriorityRecentAndCleanupWithZone()
        {
            yield return Host(CameraViewsScene);
            yield return Teleport(InsideZone);
            Assert.IsTrue(FirstPerson, "구역(priority 10) 1인칭");
            var third = Get(Get(director, "Settings"), "DefaultThirdPerson");
            var source = new GameObject("Camera Request Probe");

            Call(director, "RequestView", source, third, 20, Vector3.zero);
            yield return Frames(2);
            Assert.IsFalse(FirstPerson, "높은 priority 우선");
            Call(director, "ReleaseView", source);
            yield return Frames(2);
            Assert.IsTrue(FirstPerson, "해제 후 남은 구역 요청");

            Call(director, "RequestView", source, third, 10, Vector3.zero);
            yield return FixedSteps(3);
            Assert.IsFalse(FirstPerson, "동률은 최근 등록, 구역의 매 tick 갱신은 순서 유지");
            Object.Destroy(source);
            yield return Frames(2);
            Assert.IsTrue(FirstPerson, "파괴된 출처 정리");

            var zone = (Behaviour)Object.FindAnyObjectByType(GameType("CurtainCall.Cameras.CameraViewZone"));
            zone.enabled = false;
            yield return Frames(2);
            Assert.IsFalse(FirstPerson, "구역 disable 해제");
            zone.enabled = true;
            yield return FixedSteps(2);
            Assert.IsTrue(FirstPerson, "구역 enable 재요청");

            var volume = zone.GetComponent<BoxCollider>();
            volume.enabled = false;
            yield return FixedSteps(2);
            Assert.IsFalse(FirstPerson, "collider disable 정리");
            volume.enabled = true;
            yield return FixedSteps(2);
            Assert.IsTrue(FirstPerson, "collider enable 재요청");
        }

        [UnityTest]
        public IEnumerator Transition_BlendsOnWinnerChangeAndAppliesLiveEdits()
        {
            yield return Host(CameraViewsScene);
            yield return Seconds(1f);
            var preset = ScriptableObject.CreateInstance(GameType("CurtainCall.Cameras.CameraViewPreset"));
            Set(preset, "Kind", Enum.Parse(GameType("CurtainCall.Cameras.CameraViewKind"), "FirstPerson"));
            Set(preset, "TransitionDuration", 0.5f);
            var source = new GameObject("Transition Probe");
            try
            {
                Call(director, "RequestView", source, preset, 50, Vector3.zero);
                yield return null;
                Assert.IsTrue(FirstPerson);
                Assert.Greater(Vector3.Distance(Camera.main.transform.position, EyePosition()), 0.5f, "전환 시작은 이전 화면에서");
                yield return Seconds(0.7f);
                AssertVector(EyePosition(), Camera.main.transform.position, "전환 완료 1인칭 위치", 0.05f);

                Set(preset, "FieldOfView", 75f);
                Set(preset, "EyeHeight", 1f);
                yield return Frames(2);
                Assert.AreEqual(75f, Camera.main.fieldOfView, 0.01f, "같은 승자 값 수정은 전환 재시작 없음");
                AssertVector(local.transform.position + Vector3.up, Camera.main.transform.position, "눈높이 즉시 반영", 0.05f);

                Call(director, "ReleaseView", source);
                yield return Frames(2);
                Assert.IsFalse(FirstPerson);
                Assert.AreEqual(60f, Camera.main.fieldOfView, 0.01f, "기본 시점 즉시 전환");
            }
            finally
            {
                Object.Destroy(source);
                Object.Destroy(preset);
            }
        }

        [UnityTest]
        public IEnumerator Fallen_SpectatesInThirdPersonAndRestartRestoresZoneView()
        {
            yield return Host(CameraViewsScene);
            yield return Teleport(InsideZone);
            Assert.IsTrue(FirstPerson);

            Call(local, "ServerSetState", Enum.Parse(GameType(StateType), "Fallen"));
            yield return Frames(3);
            Assert.IsTrue(Spectating, "사망 후 관전");
            Assert.IsFalse(FirstPerson, "관전 중 1인칭 제외");
            Assert.IsFalse(HideModel, "관전 중 모델 숨김 중지");
            Assert.AreEqual(Vector3.zero, ViewForward, "관전 중 조작 시선 없음");
            Assert.AreEqual(cursorLock, Cursor.lockState, "관전 중 커서 복구");

            Call(GameType(PlayerType), "ServerRestartAll");
            yield return WaitFor(() => FirstPerson, 3f, "재시작 후 남아 있던 구역 요청 복구");
            Assert.IsFalse(Spectating);
        }

        [UnityTest]
        public IEnumerator Retired_SpectatesLikeFallenAndIsNotWatched()
        {
            yield return Host(CameraViewsScene);
            yield return Teleport(InsideZone);
            Assert.IsTrue(FirstPerson);

            Call(local, "ServerSetState", Enum.Parse(GameType(StateType), "Retired"));
            yield return Frames(3);
            Assert.IsTrue(Spectating, "리타이어 후 관전");
            Assert.IsFalse(FirstPerson, "관전 중 1인칭 제외");
            Assert.IsFalse(HideModel);
            Assert.IsNull(Get(GameType("CurtainCall.Cameras.CameraSpectator"), "Target"), "관전 대상은 Normal만(혼자면 없음)");

            Call(GameType(PlayerType), "ServerRestartAll");
            yield return WaitFor(() => FirstPerson, 3f, "재시작 후 구역 1인칭 복구");
            Assert.IsFalse(Spectating);
        }

        [UnityTest]
        public IEnumerator LocalDespawn_ClearsViewAndRestoresCursor()
        {
            yield return Host(CameraViewsScene);
            yield return Teleport(InsideZone);
            Assert.IsTrue(FirstPerson);
            var leave = (Task)Call(Get(GameType(SessionType), "Instance"), "LeaveAsync");
            yield return WaitFor(() => leave.IsCompleted && IsNull(Get(director, "LocalPlayer")), 10f, "로컬 소멸");
            yield return Frames(2);
            Assert.IsFalse(FirstPerson);
            Assert.AreEqual(Vector3.zero, ViewForward);
            Assert.IsNull(director.GetComponent<CinemachineCamera>().Target.TrackingTarget);
            Assert.AreEqual(cursorLock, Cursor.lockState, "커서 복구");
        }

        [UnityTest]
        public IEnumerator Proto_TightropeProviderKeepsFramingAndOcclusionRules()
        {
            yield return Host(ProtoScene);
            var course = Get(GameType("CurtainCall.Tightrope.TightropeCourse"), "Current") as Component;
            Assert.IsNotNull(course, "proto 외줄 코스");
            var rules = Get(Get(GameType("CurtainCall.Settings.GameSettings"), "Tightrope"), "Camera");
            var forward = (Vector3)Get(course, "Forward");
            var follow = Vector3.up * (float)Get(rules, "Height") - forward * (float)Get(rules, "Distance");
            Assert.IsFalse(FirstPerson);
            AssertVector(follow, Follow.FollowOffset, "외줄 따라가기");
            AssertVector(new Vector3(0f, (float)Get(rules, "LookHeight"), (float)Get(rules, "LookAhead")),
                Composer.TargetOffset, "외줄 바라보기(캐릭터 로컬)");
            Assert.AreEqual(60f, Camera.main.fieldOfView, 0.01f);

            var framing = director.GetComponent(GameType("CurtainCall.Cameras.TightropeCameraFraming"));
            var fade = director.GetComponent(GameType("CurtainCall.Cameras.CameraOcclusionFade"));
            Assert.IsNotNull(framing, "외줄 구도 공급자");
            var exclusions = (IDictionary)Private(fade, "exclusions");
            Assert.IsTrue(exclusions.Contains(framing), "외줄 가림 예외 등록");
            Assert.AreEqual(1, (int)Get(Private(fade, "ruleRequests"), "Count"), "외줄 가림 수치 요청");
            var exclude = (Func<Transform, bool>)exclusions[framing];
            Assert.IsTrue(exclude(course.transform), "코스 루트 제외");
            var outside = new GameObject("Occluder Probe");
            Assert.IsFalse(exclude(outside.transform), "코스 밖 물체는 가림 대상");
            Object.Destroy(outside);

            Call(local, "ServerSetState", Enum.Parse(GameType(StateType), "Fallen"));
            yield return Frames(3);
            Assert.IsTrue(Spectating, "사망 후 관전");
            Assert.IsFalse(FirstPerson);
            AssertVector(follow, Follow.FollowOffset, "관전 중 외줄 구도 유지");
        }

        // Task-045: 테스트 키보드(Input System 상태 이벤트) → PlayerInputReader → 기본 이동 규칙 → PlayerMover 실제 변위.
        // 사람이 누른 키보드·마우스 확인이 아니다. 시선(yaw/pitch)은 마우스 대신 director 값을 직접 넣는다.

        [UnityTest]
        public IEnumerator FirstPerson_DefaultSchemeKeysMoveAlongHorizontalView()
        {
            yield return Host(CameraViewsScene);
            var controller = local.GetComponent(GameType("CurtainCall.Player.PlayerController"));
            Assert.IsFalse((bool)Get(controller, "HasAssignedScheme"), "끼운 규칙 없음");
            Assert.AreSame(Get(GameType("CurtainCall.Player.ControlSettings"), "Default"), Get(controller, "Scheme"), "프로젝트 Default 규칙");
            yield return Teleport(InsideZone);
            Assert.IsTrue(FirstPerson);
            yield return BeginKeyboard();

            SetView(90f, 30f);
            yield return Frames(2);
            AssertVector(Vector3.right, ViewForward, "yaw 90 수평 시선");
            yield return Hold(Vector3.right, "W 시선 전방", "W");
            float straight = measuredSpeed;
            yield return Hold(Vector3.left, "S 시선 반대", "S");
            yield return Hold((Vector3.right + Vector3.back).normalized, "W+D 시선 기준 대각선", "W", "D");
            Assert.AreEqual(straight, measuredSpeed, straight * 0.1f, "대각선·pitch에도 속도 같음");

            SetView(180f, -20f);
            yield return Frames(2);
            yield return Hold(Vector3.back, "yaw 180 W", "W");
            yield return Hold(Vector3.right, "yaw 180 A", "A");
        }

        [UnityTest]
        public IEnumerator ViewChangeWhileHeld_KeepsDirectionUntilRelease()
        {
            yield return Host(CameraViewsScene);
            yield return Teleport(InsideZone);
            Assert.IsTrue(FirstPerson);
            yield return BeginKeyboard();
            SetView(90f, 0f);
            yield return Frames(2);

            Keys("W");
            yield return Frames(3);
            yield return Teleport(OutsideZone); // W를 누른 채 구역 밖 → 3인칭
            Assert.IsFalse(FirstPerson, "구역 밖 3인칭");
            Vector3 start = local.transform.position;
            yield return Seconds(0.4f);
            Vector3 delta = local.transform.position - start;
            delta.y = 0f;
            Keys();
            yield return Frames(3);
            Assert.Greater(delta.magnitude, 0.3f, $"누른 채 이동 {delta}");
            Assert.Greater(Vector3.Dot(delta.normalized, Vector3.right), 0.995f, $"누른 채 시점 전환: 직전 1인칭 시선 유지 {delta}");

            yield return Hold(Vector3.forward, "다시 누르면 3인칭 코스 기준", "W");
        }

        [UnityTest]
        public IEnumerator FreeThirdPerson_OrbitsWithViewAndKeysFollowView()
        {
            yield return Host(CameraViewsScene);
            var preset = ScriptableObject.CreateInstance(GameType("CurtainCall.Cameras.CameraViewPreset"));
            Set(preset, "Kind", Enum.Parse(GameType("CurtainCall.Cameras.CameraViewKind"), "FreeThirdPerson"));
            Set(preset, "PitchLimit", 35f);
            var source = new GameObject("Free Third Person Probe");
            try
            {
                Call(director, "RequestView", source, preset, 20, Vector3.zero);
                yield return Frames(2);
                Assert.IsTrue((bool)Get(director, "IsFreeThirdPerson"), "3인칭 비고정");
                Assert.IsTrue((bool)Get(director, "UsesMouseLook"), "마우스 시선");
                Assert.IsFalse(FirstPerson);
                Assert.IsFalse(HideModel, "3인칭은 모델 표시");
                yield return BeginKeyboard();

                SetView(90f, 20f);
                yield return Frames(3);
                AssertVector(Vector3.right, ViewForward, "비고정 수평 시선");
                var args = new object[] { preset, local.transform.position, 90f, 20f, null };
                var pose = (Vector3)GameType("CurtainCall.Cameras.CameraViewDirector").GetMethod("FreeThirdPersonPose").Invoke(null, args);
                AssertVector(pose, Camera.main.transform.position, "시선 뒤 3인칭 위치", 0.05f);
                Assert.Less(Vector3.Dot(Camera.main.transform.position - local.transform.position, Vector3.right), -1f, "카메라는 시선 뒤");
                Vector3 cameraForward = Camera.main.transform.forward;
                cameraForward.y = 0f;
                Assert.Greater(Vector3.Dot(cameraForward.normalized, Vector3.right), 0.99f, "카메라는 시선 방향을 봄");
                Assert.Less(Camera.main.transform.forward.y, 0f, "pitch 20: 위에서 내려다봄");

                yield return Hold(Vector3.right, "비고정 W 시선 전방", "W");
                yield return Hold(Vector3.back, "비고정 D 시선 오른쪽", "D");

                Call(director, "ReleaseView", source);
                yield return Frames(2);
                Assert.IsFalse((bool)Get(director, "UsesMouseLook"), "해제 후 기본 3인칭 고정");
                Assert.AreEqual(cursorLock, Cursor.lockState, "커서 복구");
            }
            finally
            {
                if (!IsNull(director)) Call(director, "ReleaseView", source);
                Object.Destroy(source);
                Object.Destroy(preset);
            }
        }

        [UnityTest]
        public IEnumerator ThirdPerson_KeysKeepCourseAxes()
        {
            yield return Host(CameraViewsScene);
            yield return Teleport(OutsideZone);
            Assert.IsFalse(FirstPerson, "구역 밖 3인칭");
            yield return BeginKeyboard();
            yield return Hold(Vector3.forward, "W 코스 앞", "W");
            yield return Hold(Vector3.back, "S 코스 뒤", "S");
            yield return Hold(Vector3.right, "D 코스 오른쪽", "D");
        }

        [UnityTest]
        public IEnumerator FirstPerson_OtherPlayerKeepsCourseAxes()
        {
            yield return Host(CameraViewsScene);
            yield return Teleport(InsideZone);
            Assert.IsTrue(FirstPerson);
            SetView(90f, 0f);

            // 로컬이 아닌 몸통: 공용 카메라 1인칭 시선을 쓰지 않고 자기 코스(+Z) 기준이어야 한다
            rig = new GameObject("Other Player Rig");
            rig.transform.position = new Vector3(-8f, 0.5f, 0f);
            var mover = rig.AddComponent(GameType("CurtainCall.Player.PlayerMover"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { rig });
            var scheme = Get(GameType("CurtainCall.Player.ControlSettings"), "Default");
            var frame = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
            frame.GetType().GetField("Move").SetValue(frame, 1f);
            Call(scheme, "Enter", context);
            yield return WaitFor(() => ((CharacterController)Get(mover, "Body")).isGrounded && !(bool)Get(mover, "IsAirborne"), 3f, "rig 착지");

            Vector3 start = rig.transform.position;
            float end = Time.time + 0.4f;
            while (Time.time < end)
            {
                Call(scheme, "Tick", context, frame);
                yield return null;
            }
            Call(scheme, "Exit", context);
            Vector3 delta = rig.transform.position - start;
            delta.y = 0f;
            Assert.Greater(delta.magnitude, 0.3f, "rig 이동");
            Assert.Greater(Vector3.Dot(delta.normalized, Vector3.forward), 0.995f, $"다른 플레이어는 코스 기준: {delta}");
            Assert.IsTrue(FirstPerson, "로컬 1인칭 유지");
        }

        IEnumerator Host(string scene)
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scene,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("에디터에서만 씬 경로로 연다");
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

        Vector3 EyePosition() => local.transform.position + Vector3.up * ((float)Get(Mover, "BodyHeight") * 0.9f);

        /// <summary>1인칭 시선을 직접 정하고 마우스 적분을 막는다(진입 시 시선 초기화 뒤에 부른다).</summary>
        void SetView(float yaw, float pitch)
        {
            const BindingFlags field = BindingFlags.NonPublic | BindingFlags.Instance;
            Call(director, "SetInputBlocked", true);
            director.GetType().GetField("yaw", field).SetValue(director, yaw);
            director.GetType().GetField("pitch", field).SetValue(director, pitch);
        }

        /// <summary>
        /// 테스트 키보드를 붙인다. 에디터 포커스와 무관하게 게임으로 들어가도록 Input System 임시 설정의 포커스 값 2개를 잠시 바꾼다.
        /// 설정 객체는 교체하지 않는다(교체하면 Input System이 임시 설정을 파괴한다). 설정이 에셋이면 바꾸지 않고 건너뛴다.
        /// 재시작·생성 직후 입력 막기가 풀리고 몸이 땅에 닿을 때까지 기다린다.
        /// </summary>
        IEnumerator BeginKeyboard()
        {
            var system = InputType("InputSystem");
            var settings = (ScriptableObject)Get(system, "settings");
#if UNITY_EDITOR
            Assume.That(UnityEditor.AssetDatabase.GetAssetPath(settings), Is.Empty, "Input System 설정 에셋은 테스트가 바꾸지 않는다");
#endif
            inputSettings = settings;
            backgroundBehavior = Get(settings, "backgroundBehavior");
            editorInputBehavior = Get(settings, "editorInputBehaviorInPlayMode");
            SetEnum(settings, "backgroundBehavior", "IgnoreFocus");
            SetEnum(settings, "editorInputBehaviorInPlayMode", "AllDeviceInputAlwaysGoesToGameView");
            keyboard = system.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Keyboard", "CameraViews Test Keyboard", null });
            Keys();
            yield return WaitFor(() => !(bool)Get(GameType("CurtainCall.Player.InputGate"), "IsBlocked"), 3f, "입력 막기 해제");
            yield return WaitFor(() => ((CharacterController)Get(Mover, "Body")).isGrounded && !(bool)Get(Mover, "IsAirborne"), 3f, "착지");
        }

        void EndKeyboard()
        {
            var system = InputType("InputSystem");
            if (keyboard != null)
            {
                system.GetMethod("RemoveDevice", new[] { InputType("InputDevice") }).Invoke(null, new[] { keyboard });
                keyboard = null;
            }
            if (inputSettings != null)
            {
                inputSettings.GetType().GetProperty("backgroundBehavior", Public).SetValue(inputSettings, backgroundBehavior);
                inputSettings.GetType().GetProperty("editorInputBehaviorInPlayMode", Public).SetValue(inputSettings, editorInputBehavior);
            }
            inputSettings = null;
        }

        /// <summary>테스트 키보드 상태를 이 키들만 눌린 상태로 바꾼다. 비우면 모두 뗀다.</summary>
        void Keys(params string[] names)
        {
            var system = InputType("InputSystem");
            var keyType = InputType("Key");
            var stateType = InputType("LowLevel.KeyboardState");
            var keys = Array.CreateInstance(keyType, names.Length);
            for (int i = 0; i < names.Length; i++) keys.SetValue(Enum.Parse(keyType, names[i]), i);
            var state = Activator.CreateInstance(stateType, new object[] { keys });
            system.GetMethods(Public)
                .First(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 3)
                .MakeGenericMethod(stateType)
                .Invoke(null, new[] { keyboard, state, -1d });
        }

        /// <summary>키를 0.4초 눌러 로컬 플레이어의 수평 변위 방향을 확인하고, 수평 속도를 <see cref="measuredSpeed"/>에 남긴다.</summary>
        IEnumerator Hold(Vector3 expected, string what, params string[] names)
        {
            Keys(names);
            yield return Frames(3);
            Vector3 start = local.transform.position;
            float started = Time.time;
            yield return Seconds(0.4f);
            Vector3 delta = local.transform.position - start;
            float elapsed = Time.time - started;
            Keys();
            yield return Frames(3);
            delta.y = 0f;
            Assert.Greater(delta.magnitude, 0.3f, $"{what}: 이동하지 않음 {delta}");
            Assert.Greater(Vector3.Dot(delta.normalized, expected), 0.995f, $"{what}: 기대 {expected}, 실제 {delta.normalized}");
            measuredSpeed = delta.magnitude / elapsed;
        }

        static IEnumerator CaptureMainRender(Action onRender)
        {
            bool seen = false;
            void Begin(ScriptableRenderContext context, Camera camera)
            {
                if (seen || camera != Camera.main) return;
                seen = true;
                onRender();
            }
            RenderPipelineManager.beginCameraRendering += Begin;
            try
            {
                for (int i = 0; i < 30 && !seen; i++) yield return null;
                if (!seen) Camera.main.Render();
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Begin;
            }
            Assert.IsTrue(seen, "게임 카메라 렌더링");
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

        static Type InputType(string name) => Type.GetType("UnityEngine.InputSystem." + name + ", Unity.InputSystem", true);

        static void SetEnum(object target, string property, string value)
        {
            var info = target.GetType().GetProperty(property, Public);
            info.SetValue(target, Enum.Parse(info.PropertyType, value));
        }

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

        static void Set(object target, string name, object value) => target.GetType().GetField(name, Public).SetValue(target, value);

        static object Private(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        static object Call(object target, string name, params object[] args)
        {
            var type = target as Type ?? target.GetType();
            var method = type.GetMethods(Public).First(m => m.Name == name && m.GetParameters().Length == args.Length);
            return method.Invoke(target is Type ? null : target, args);
        }
    }
}
