using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    /// <summary>저장된 씬의 실제 LAN 호스트 1명으로 공용 경계를 검사한다. 입력·위치는 합성하며 다중 클라이언트 검사는 아니다.</summary>
    public sealed partial class TightropeCampaignPlayTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string ScenePath = "Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity";
        Component session, network, player, mover, bridge, run, course, campaign, rewards;
        FieldInfo devModeCache;
        object previousDevMode;
        object runSettings;
        float previousTimeLimit;
        object legacyCourseSettings;
        string previousCourseJson;
        object sawSettingsSnapshotTarget;
        string previousSawsJson;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 실제 LAN 호스트 1명 검사. PC의 EditorPrefs는 변경하지 않는다.
            devModeCache = GameType("CurtainCall.Dev.DevMode").GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic);
            previousDevMode = devModeCache.GetValue(null);
            devModeCache.SetValue(null, false); // 실제 외줄 씬의 1인 시작 지원을 검증한다.
            runSettings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Run");
            previousTimeLimit = (float)Get(runSettings, "TimeLimit");
            sawSettingsSnapshotTarget = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Saws");
            previousSawsJson = JsonUtility.ToJson(sawSettingsSnapshotTarget);
            // 기존 회전봉·화재 시나리오의 배치를 명시한다. 새 협동 관문의 플레이 검증을 뜻하지 않는다.
            legacyCourseSettings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Course");
            previousCourseJson = JsonUtility.ToJson(legacyCourseSettings);
            JsonUtility.FromJsonOverwrite("{\"relayGapEnabled\":false,\"trapezeEnabled\":false}", legacyCourseSettings);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            bridge = Static("CurtainCall.Tightrope.TightropeCampaignBridge", "Current") as Component;
            Assert.IsNotNull(bridge, "저장된 외줄 씬에 공용 공연 연결이 있어야 한다.");
            session = Static("CurtainCall.Network.Session.NetworkSessionManager", "Instance") as Component;
            network = (Component)Get(session, "NetworkManager");
            campaign = (Component)Get(bridge, "Campaign");
            rewards = (Component)Get(bridge, "Rewards");
            run = Static("CurtainCall.Tightrope.TightropeRun", "Current") as Component;
            course = Static("CurtainCall.Tightrope.TightropeCourse", "Current") as Component;
            var host = (Task<bool>)Call(session, "HostLanAsync", Type.Missing);
            yield return Until(() => host.IsCompleted, 10, "LAN 호스트 시작");
            Assert.IsTrue(host.GetAwaiter().GetResult());
            yield return Until(() =>
            {
                player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local") as Component;
                return player != null && (bool)Get(player, "IsSpawned");
            }, 10, "로컬 플레이어 생성");
            mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            ((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerController"))).enabled = false;
            ((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"))).enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (session != null)
            {
                var leave = (Task)Call(session, "LeaveAsync");
                yield return Until(() => leave.IsCompleted, 10, "LAN 방 정리");
                if (leave.IsFaulted) throw leave.Exception;
            }
            if (network != null && (bool)Get(network, "IsListening")) Call(network, "Shutdown", Type.Missing);
            yield return null;
            foreach (var go in new[] { rewards, campaign, session, network }.Where(c => c != null).Select(c => c.gameObject).Distinct())
                Object.Destroy(go);
            yield return null;
            session = network = player = mover = bridge = run = course = campaign = rewards = null;
            devModeCache?.SetValue(null, previousDevMode);
            if (runSettings != null) Set(runSettings, "timeLimit", previousTimeLimit);
            if (legacyCourseSettings != null && previousCourseJson != null)
                JsonUtility.FromJsonOverwrite(previousCourseJson, legacyCourseSettings);
            legacyCourseSettings = null; previousCourseJson = null;
            if (sawSettingsSnapshotTarget != null && previousSawsJson != null)
                JsonUtility.FromJsonOverwrite(previousSawsJson, sawSettingsSnapshotTarget);
            sawSettingsSnapshotTarget = null; previousSawsJson = null;
        }

        [UnityTest]
        public IEnumerator HealthyRopeWalkKeepsSpeedAtLowAndVeryHighFrameRates()
        {
            yield return BeginPerformance();
            var saws = (Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var fire = (Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            saws.enabled = false;
            fire.enabled = false;
            float beforeCapture = Time.captureDeltaTime;
            var body = (CharacterController)Get(mover, "Body");
            float minimumRatio = float.PositiveInfinity;
            try
            {
                foreach (int fps in new[] { 30, 120, 240, 2000 })
                {
                    Time.captureDeltaTime = 1f / fps;
                    yield return StandOnRopeForSawTest(1, 4f);
                    float expectedSpeed = (float)Get(mover, "CourseSpeedOverride");
                    Assert.That(expectedSpeed, Is.GreaterThan(0f));
                    float start = (float)Call(course, "GetDistance", player.transform.position);
                    float duration = 0f;
                    int airborneFrames = 0, freeMovementFrames = 0;
                    for (int frame = 0; frame < 120; frame++)
                    {
                        Call(mover, "SetMoveInput", 1f);
                        yield return null;
                        duration += Time.deltaTime;
                        if ((bool)Get(mover, "IsAirborne")) airborneFrames++;
                        if ((bool)Get(mover, "FreeMovement")) freeMovementFrames++;
                    }
                    float measured = ((float)Call(course, "GetDistance", player.transform.position) - start) / duration;
                    minimumRatio = Mathf.Min(minimumRatio, measured / expectedSpeed);
                    Debug.Log($"[TightropeSpeedProbe] fps={fps}, expected={expectedSpeed:F4}, measured={measured:F4}, minimumMove={body.minMoveDistance:F4}, airborneFrames={airborneFrames}, freeMovementFrames={freeMovementFrames}, multiplier={Get(mover, "SpeedMultiplier")}, lostParts={Get(player, "LostParts")}");
                    Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
                    Assert.AreEqual(0, freeMovementFrames);
                }
                Assert.That(minimumRatio, Is.GreaterThan(0.95f), "건강한 단독 캐릭터의 줄 이동이 프레임 간격 때문에 누락되면 안 된다.");
            }
            finally { Time.captureDeltaTime = beforeCapture; }
        }

        [UnityTest]
        public IEnumerator RopeJumpReachesConfiguredHeightAndLandsBackOnTheRope()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            float beforeCapture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                yield return StandOnRopeForSawTest(2, 4f);
                float startY = player.transform.position.y;
                float expectedHeight = (float)Get(mover, "CourseJumpHeightOverride");
                Assert.That(expectedHeight, Is.GreaterThan(0f));
                Call(mover, "RequestJump");
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2, "높아진 점프 이륙");
                float peakY = player.transform.position.y;
                for (int frame = 0; frame < 180 && (bool)Get(mover, "IsAirborne"); frame++)
                {
                    peakY = Mathf.Max(peakY, player.transform.position.y);
                    yield return null;
                }
                float measuredHeight = peakY - startY;
                Debug.Log($"[TightropeJumpProbe] configured={expectedHeight:F3}, measured={measuredHeight:F3}, fps=60");
                Assert.That(measuredHeight, Is.EqualTo(expectedHeight).Within(0.12f), "프레임 단위 중력 적분 오차를 제외하고 설정 높이만큼 뛰어야 한다.");
                Assert.IsFalse((bool)Get(mover, "IsAirborne"));
                Assert.IsFalse((bool)Get(mover, "FreeMovement"));
                Assert.That(player.transform.position.y, Is.EqualTo(startY).Within(0.05f));
                Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            }
            finally { Time.captureDeltaTime = beforeCapture; }
        }

        [UnityTest]
        public IEnumerator AirborneInputCannotChangeLaunchDirectionOrNeighborLaneLanding()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            float previousCapture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                yield return StandOnRopeForSawTest(1, 4f);
                float start = (float)Call(course, "GetDistance", player.transform.position);
                Call(mover, "SetMoveInput", 1f);
                Call(mover, "RequestLaneJump", 1);
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2, "옆줄 점프 이륙");
                Vector3 launchVelocity = (Vector3)Get(mover, "airVelocity");
                for (int i = 0; i < 10; i++) { Call(mover, "SetMoveInput", 1f); yield return null; }
                float forward = (float)Call(course, "GetDistance", player.transform.position);
                Assert.That(forward - start, Is.GreaterThan(0.12f));
                for (int i = 0; i < 10; i++) { Call(mover, "SetMoveInput", -1f); yield return null; }
                float reverse = (float)Call(course, "GetDistance", player.transform.position);
                Assert.That(reverse - forward, Is.GreaterThan(0.12f), "공중에서 S를 눌러도 이륙한 방향으로 계속 이동해야 한다.");
                Assert.That((Vector3)Get(mover, "airVelocity"), Is.EqualTo(launchVelocity));
                for (int frame = 0; frame < 150 && (bool)Get(mover, "IsAirborne"); frame++)
                {
                    Assert.That((Vector3)Get(mover, "airVelocity"), Is.EqualTo(launchVelocity), "입력을 놓아도 공중 속도는 유지한다.");
                    Call(mover, "SetMoveInput", 0f);
                    yield return null;
                }
                Assert.IsFalse((bool)Get(mover, "IsAirborne"));
                float landing = (float)Call(course, "GetDistance", player.transform.position);
                Assert.That(landing, Is.GreaterThan(reverse + 0.12f));
                Vector3 destination = (Vector3)Call(course, "GetLanePosition", 2, landing);
                Assert.That((float)Call(course, "GetSide", player.transform.position),
                    Is.EqualTo((float)Call(course, "GetSide", destination)).Within(0.03f));
                Assert.AreEqual("Normal", Get(player, "State").ToString());
            }
            finally { Time.captureDeltaTime = previousCapture; }
        }

        [UnityTest]
        public IEnumerator AirInputCannotMoveReservedLandingBackIntoAnotherPlayer()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var other = new GameObject("Standing lane landing blocker");
            float previousCapture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                var otherMover = other.AddComponent(GameType("CurtainCall.Player.PlayerMover"));
                Call(otherMover, "SetStartPose", (Vector3)Call(course, "GetLanePosition", 2, 4f), Quaternion.identity, true);
                yield return StandOnRopeForSawTest(1, 4f);
                Call(mover, "SetMoveInput", 0f);
                Call(mover, "RequestLaneJump", 1);
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2, "동료 뒤로 보정하는 점프");
                Assert.IsTrue((bool)Get(mover, "laneAlongLimited"));
                for (int frame = 0; frame < 150 && (bool)Get(mover, "IsAirborne"); frame++)
                { Call(mover, "SetMoveInput", 1f); yield return null; }
                Assert.IsFalse((bool)Get(mover, "IsAirborne"));
                Vector3 separation = player.transform.position - other.transform.position;
                separation.y = 0f;
                Assert.That(separation.magnitude, Is.GreaterThanOrEqualTo((float)Get(mover, "BodyRadius")
                    + (float)Get(otherMover, "BodyRadius") - 0.01f), "공중 W 입력이 동료 회피 보정의 착지점을 몸 안으로 옮기면 안 된다.");
                Assert.AreEqual("Normal", Get(player, "State").ToString());
            }
            finally { Time.captureDeltaTime = previousCapture; Object.Destroy(other); }
        }

        [UnityTest]
        public IEnumerator OrbitCameraKeepsItsFramingWhileSoloWalksTowardMovingSaws()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var director = (Component)Static("CurtainCall.Cameras.CameraViewDirector", "Current");
            var cameraRules = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Camera");
            float beforeCapture = Time.captureDeltaTime;
            float totalDistanceDrift = 0f, totalLookAheadDrift = 0f;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    yield return StandOnRopeForSawTest(1, 3.5f);
                    yield return new WaitForSeconds(0.5f);
                    float start = (float)Call(course, "GetDistance", player.transform.position);
                    float maxDistanceDrift = 0f, maxLookAheadDrift = 0f;
                    float elapsed = 0f;
                    for (int frame = 0; frame < 100; frame++)
                    {
                        Call(mover, "SetMoveInput", 1f);
                        yield return null;
                        elapsed += Time.deltaTime;
                        object view = Get(director, "CurrentView");
                        maxDistanceDrift = Mathf.Max(maxDistanceDrift,
                            Mathf.Abs((float)Get(view, "Distance") - (float)Get(cameraRules, "Distance")));
                        maxLookAheadDrift = Mathf.Max(maxLookAheadDrift,
                            Mathf.Abs((float)Get(view, "LookAhead") - (float)Get(cameraRules, "LookAhead")));
                        Assert.IsTrue((bool)Get(director, "UsesMouseLook"));
                    }
                    float traveled = (float)Call(course, "GetDistance", player.transform.position) - start;
                    Debug.Log($"[TightropeCameraWalkProbe] attempt={attempt}, traveled={traveled:F3}, elapsed={elapsed:F3}, maxDistanceDrift={maxDistanceDrift:F3}, maxLookAheadDrift={maxLookAheadDrift:F3}");
                    Assert.That(traveled, Is.EqualTo((float)Get(mover, "CourseSpeedOverride") * elapsed).Within(.1f));
                    totalDistanceDrift = Mathf.Max(totalDistanceDrift, maxDistanceDrift);
                    totalLookAheadDrift = Mathf.Max(totalLookAheadDrift, maxLookAheadDrift);
                }
                Assert.That(totalDistanceDrift, Is.LessThan(.01f), "옆에서 왕복하는 톱 때문에 단독 카메라가 줌을 반복하면 안 된다.");
                Assert.That(totalLookAheadDrift, Is.LessThan(.01f), "톱을 따라 바라보는 지점이 바뀌면 전진감이 흔들린다.");
            }
            finally { Time.captureDeltaTime = beforeCapture; }
        }

        [UnityTest]
        public IEnumerator CameraAutomaticallyUsesOrbitThirdPersonOnRopeAndHonorsInputBlock()
        {
            var framing = (Component)Static("CurtainCall.Cameras.TightropeCameraFraming", "Current");
            var director = (Component)Static("CurtainCall.Cameras.CameraViewDirector", "Current");
            Assert.IsNotNull(framing);
            yield return Until(() => (bool)Get(framing, "IsLocalViewActive")
                && !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 3, "출발 플랫폼 기본 1인칭");
            Assert.IsTrue((bool)Get(director, "IsFirstPerson"));
            Assert.AreEqual("FirstPerson", Get(framing, "CurrentMode").ToString());
            Assert.IsTrue((bool)Get(mover, "FreeMovement"));
            Set(director, "yaw", 75f); // 마우스 장치 조작 없이 시선 상태를 합성한다.
            Vector3 forward = (Vector3)Get(director, "ViewForward");
            object blocker = new object();
            var block = GameType("CurtainCall.Player.InputGate").GetMethod("SetBlocking");
            try
            {
                block.Invoke(null, new[] { blocker, (object)true });
                yield return null;
                Assert.IsTrue((bool)Get(director, "inputBlocked"));
                Assert.IsTrue((bool)Get(director, "IsFirstPerson"));
                Assert.That(Vector3.Dot(forward, (Vector3)Get(director, "ViewForward")), Is.GreaterThan(0.999f));
            }
            finally { block.Invoke(null, new[] { blocker, (object)false }); }
            yield return Until(() => !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 3, "메뉴 닫은 뒤 입력 재개");

            yield return StandOnRopeForSawTest(1, 4f);
            yield return Until(() => (bool)Get(framing, "IsLocalViewActive")
                && Get(Get(director, "CurrentView"), "Kind").ToString() == "FreeThirdPerson", 3, "줄 탑승 자동 공전 3인칭");
            Assert.AreEqual("FreeThirdPerson", Get(framing, "CurrentMode").ToString());
            Assert.IsTrue((bool)Get(director, "UsesMouseLook"));
            Assert.IsTrue((bool)Get(director, "IsFreeThirdPerson"));
            yield return new WaitForSeconds(0.25f); // 시점 전환 보간이 끝난 실제 카메라 방향을 비교한다.
            Vector3 ropeForward = (Vector3)Get(director, "ViewForward");
            Set(director, "yaw", 235f);
            yield return null;
            Assert.IsTrue((bool)Get(director, "UsesMouseLook"));
            Assert.That(Vector3.Dot(ropeForward, (Vector3)Get(director, "ViewForward")), Is.LessThan(-.9f),
                "줄 위에서도 마우스 시선 상태를 따라 카메라가 돌아야 한다.");
            float startDistance = (float)Call(course, "GetDistance", player.transform.position);
            float startSide = (float)Call(course, "GetSide", player.transform.position);
            for (int frame = 0; frame < 20; frame++)
            {
                Call(mover, "SetMoveInput", 1f);
                yield return null;
            }
            Call(mover, "SetMoveInput", 0f);
            Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.GreaterThan(startDistance),
                "카메라가 뒤를 보아도 W는 코스 앞쪽으로 이동한다.");
            Assert.That((float)Call(course, "GetSide", player.transform.position), Is.EqualTo(startSide).Within(.01f));
            try
            {
                block.Invoke(null, new[] { blocker, (object)true });
                yield return null;
                Assert.IsTrue((bool)Get(director, "inputBlocked"), "줄 위 메뉴도 마우스 공전 입력을 차단한다.");
            }
            finally { block.Invoke(null, new[] { blocker, (object)false }); }
            yield return null;

            Call(mover, "RequestJump");
            yield return Until(() => (bool)Get(mover, "IsAirborne"), 2, "줄 위 점프 시작");
            int airborneFrames = 0;
            float landingDeadline = Time.realtimeSinceStartup + 3f;
            while ((bool)Get(mover, "IsAirborne") && Time.realtimeSinceStartup < landingDeadline)
            {
                airborneFrames++;
                Assert.AreEqual("FreeThirdPerson", Get(framing, "CurrentMode").ToString());
                Assert.AreEqual("FreeThirdPerson", Get(Get(director, "CurrentView"), "Kind").ToString());
                Assert.IsTrue((bool)Get(director, "UsesMouseLook"), "줄 사이 공중에서도 마우스 공전 3인칭을 유지한다.");
                yield return null;
            }
            Assert.That(airborneFrames, Is.GreaterThan(0));
            Assert.IsFalse((bool)Get(mover, "IsAirborne"), "줄 위로 다시 착지해야 한다.");
            Assert.IsFalse((bool)Get(mover, "FreeMovement"));

            var start = (Pose)Call(course, "GetStartPose", (int)Get(player, "Slot"));
            Call(mover, "SetStartPose", start.position, start.rotation, true);
            yield return Until(() => (bool)Get(mover, "FreeMovement") && !(bool)Get(mover, "IsAirborne")
                && (bool)Get(director, "IsFirstPerson"), 3, "플랫폼 복귀 자동 1인칭");
            Assert.AreEqual("FirstPerson", Get(framing, "CurrentMode").ToString());
            Assert.IsTrue((bool)Get(director, "IsFirstPerson"));
            Assert.IsTrue((bool)Get(director, "UsesMouseLook"));
        }

        [UnityTest]
        public IEnumerator SoloButtonStartsOfflineWithoutDevModeAndRestoresStartRuleOnExit()
        {
            Assert.IsFalse((bool)Static("CurtainCall.Dev.DevMode", "Enabled"));
            var baseSettings = Get(Static("CurtainCall.Settings.GameSettings", "Base"), "Session");
            int originalMinimum = (int)Get(baseSettings, "MinPlayers");
            var hud = (Behaviour)Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            var leave = (Task)Call(session, "LeaveAsync");
            yield return Until(() => leave.IsCompleted, 10, "혼자 시작 전 오프라인");
            yield return null;
            yield return ClickPracticeButton(hud, "혼자 시작");
            yield return Until(() => (bool)Get(bridge, "PerformanceOpen"), 10, "혼자 시작 공연 연결");
            player = Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local") as Component;
            mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            ((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerController"))).enabled = false;
            ((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"))).enabled = false;
            Assert.AreEqual(1, (int)Get(session, "PlayerCount"));
            Assert.AreEqual(1, (int)Get(session, "MinPlayers"));
            Assert.IsFalse((bool)Static("CurtainCall.Dev.DevMode", "Enabled"));
            Assert.AreEqual(originalMinimum, (int)Get(baseSettings, "MinPlayers"));
            var saws = Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            yield return Until(() => (bool)Call(saws, "IsHorizontalMoving", 0), 3, "혼자 시작 양옆 톱날 작동");
            hud.enabled = false;
            Assert.AreEqual(originalMinimum, (int)Get(session, "MinPlayers"), "외줄 전용 시작 인원 요청은 씬 수명에 한정된다.");
            hud.enabled = true;
            yield return null;
            Assert.AreEqual(1, (int)Get(session, "MinPlayers"));
        }

        [UnityTest]
        public IEnumerator ChosenStartingLaneSurvivesOneButtonCourseStart()
        {
            var chosen = (Vector3)Call(course, "GetLanePosition", (int)Get(course, "LaneCount") - 1, -2f);
            Call(mover, "SetStartPose", chosen, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            yield return null;
            var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "StartCourseByHost"));
            yield return Until(() => (bool)Get(bridge, "PerformanceOpen"), 5, "한 번의 시작 요청으로 공연 진입");
            yield return null;
            Assert.That(Mathf.Abs((float)Call(course, "GetSide", player.transform.position) - (float)Call(course, "GetSide", chosen)), Is.LessThan(0.1f));
            Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.EqualTo(-2f).Within(0.1f));
            Assert.IsFalse((bool)Get(mover, "IsAirborne"));
            Assert.IsFalse((bool)Get(player.GetComponent(GameType("CurtainCall.Player.PlayerBalance")), "IsActive"));
        }

        [UnityTest]
        public IEnumerator SessionStartWaitsForHudPerformanceButtonThenBothSidesMove()
        {
            var hud = (Component)Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            var director = (Component)Static("CurtainCall.Cameras.CameraViewDirector", "Current");
            Assert.IsFalse((bool)Call(hud, "StartPerformanceByHost"), "방 시작 전에는 공연만 따로 시작할 수 없다.");
            Assert.IsTrue((bool)Call(session, "StartGame")); // 왼쪽 게임 시작 버튼과 같은 진입점
            yield return Until(() => Phase == "Intro", 5, "공연 시작 버튼 대기");
            yield return StandOnRopeForSawTest(1, 2f);
            Assert.IsFalse((bool)Get(mover, "FreeMovement"), "도입 단계에도 줄 탑승 연습은 가능하다.");
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            int leftIndex = ActiveHorizontalSaw(saws, true), rightIndex = ActiveHorizontalSaw(saws, false);
            Vector3 leftBefore = (Vector3)Call(saws, "GetHorizontalPosition", leftIndex);
            Vector3 rightBefore = (Vector3)Call(saws, "GetHorizontalPosition", rightIndex);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual("Waiting", Get(run, "State").ToString());
            Assert.AreEqual(leftBefore, (Vector3)Call(saws, "GetHorizontalPosition", leftIndex));
            Assert.AreEqual(rightBefore, (Vector3)Call(saws, "GetHorizontalPosition", rightIndex));
            Assert.IsFalse((bool)Call(saws, "IsHorizontalMoving", leftIndex));
            Assert.IsFalse((bool)Call(saws, "IsHorizontalMoving", rightIndex));
            Assert.IsTrue((bool)Get(director, "inputBlocked"), "줄에 올라가도 공연 시작 버튼을 누를 커서를 유지한다.");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsTrue(Cursor.visible);
            yield return ClickPracticeButton(hud, "외줄타기 시작 (톱날 작동)");
            Assert.IsFalse((bool)Call(hud, "StartPerformanceByHost"), "중복 클릭은 공연을 다시 시작하지 않는다.");
            yield return Until(() => (bool)Get(bridge, "PerformanceOpen") && Get(run, "State").ToString() == "Running", 5, "HUD 공연 시작");
            yield return Until(() => !(bool)Get(director, "inputBlocked"), 3, "공연 시작 후 마우스 시점 조작 재개");
            yield return new WaitForSeconds(0.25f);
            Vector3 right = (Vector3)Get(course, "Right");
            Assert.IsTrue((bool)Call(saws, "IsHorizontalMoving", leftIndex));
            Assert.IsTrue((bool)Call(saws, "IsHorizontalMoving", rightIndex));
            Assert.That(Vector3.Dot((Vector3)Call(saws, "GetHorizontalPosition", leftIndex) - leftBefore, right), Is.GreaterThan(0.2f), "왼쪽 톱날은 코스 안쪽인 오른쪽으로 움직인다.");
            Assert.That(Vector3.Dot((Vector3)Call(saws, "GetHorizontalPosition", rightIndex) - rightBefore, right), Is.LessThan(-0.2f), "오른쪽 톱날은 코스 안쪽인 왼쪽으로 움직인다.");
            Assert.That((float)Get(run, "TimeRemaining"), Is.LessThan((float)Get(run, "TimeLimit")));
        }

        [UnityTest]
        public IEnumerator OfficialStartWaitsForCampaignAndPreservesExistingInjury()
        {
            Call(player, "ServerSetOneArm");
            int parts = Convert.ToInt32(Get(player, "LostParts"));
            Assert.AreEqual(3, CountBits(parts));
            yield return BeginPerformance();
            Assert.AreEqual(parts, Convert.ToInt32(Get(player, "LostParts")), "공연 시작이 온전한 몸으로 복구하면 안 된다.");
            Assert.IsTrue((bool)Get(run, "UsesCampaign"));
            Assert.IsFalse((bool)Call(run, "RestartByHost"), "공연 중 R 재시작은 부활·중복 정산을 일으키면 안 된다.");
            Assert.IsNotNull(Object.FindAnyObjectByType(GameType("CurtainCall.Cameras.CameraViewDirector")));
            Assert.IsNotNull(Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud")));
        }

        [UnityTest]
        public IEnumerator ExternalEntryOwnsPlacementStartAndSettlementWithBridgePresent()
        {
            // 진입/정산만 검사한다. 순간이동으로 필수 4인 관문 통과를 대신하지 않는다.
            Set(sawSettingsSnapshotTarget, "finaleFourStackRequired", false);
            var space = Get(Static("CurtainCall.Settings.GameSettings", "Base"), "SpaceTransition");
            string previousScene = (string)Get(space, "TightropeScene");
            Component entry = null;
            try
            {
                Set(space, "tightropeScene", run.gameObject.scene.name);
                Call(run, "SetExternallyManaged", true);
                Assert.IsTrue((bool)Call(session, "StartGame"));
                yield return Until(() => Phase == "Intro", 5, "관리 공연 도입");
                GameType("CurtainCall.SpaceLink.CampaignPerformanceEntry").GetMethod("Prepare")
                    .Invoke(null, new object[] { run.gameObject.scene });
                entry = (Component)Object.FindAnyObjectByType(GameType("CurtainCall.SpaceLink.CampaignPerformanceEntry"));
                Assert.IsNotNull(entry);
                // 진입 배치를 잠시 지연한다. Adapter는 실제 Update를 계속 실행하며 배치 확인을 기다린다.
                ((Behaviour)entry).enabled = false;
                var adapter = entry.GetComponent(GameType("CurtainCall.SpaceLink.CampaignTightropeAdapter"));
                Assert.IsNotNull(adapter);
                Assert.IsTrue((bool)Call(campaign, "EndIntro"));
                for (int i = 0; i < 5; i++) yield return null;
                Assert.IsFalse((bool)Get(adapter, "Started"));
                Assert.IsFalse((bool)Get(bridge, "PerformanceOpen"), "Bridge가 외부 배치 확인보다 먼저 명부를 열면 안 된다.");
                Assert.AreEqual(0, Get(run, "Round"));
                Assert.AreEqual("Waiting", Get(run, "State").ToString());
                ((Behaviour)entry).enabled = true;
                yield return Until(() => (bool)Get(adapter, "Started") && Get(run, "State").ToString() == "Running", 5,
                    "배치 후 Adapter가 공연 한 번 시작");
                Assert.AreEqual(1, Get(run, "Round"));
                Assert.IsTrue((bool)Get(bridge, "PerformanceOpen"));
                Assert.IsFalse((bool)Call(run, "StartExternalPerformance"), "중복 시작은 거절한다.");
                Assert.IsFalse((bool)Call(run, "RestartByHost"));
                Assert.IsFalse((bool)Call(run, "ResetWaitingPracticeByHost"));
                var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
                Assert.IsFalse((bool)Call(hud, "RestartCourseByHost"), "실제 캠페인 씬의 R은 테스트용 전체 초기화를 실행하지 않는다.");
                Assert.IsFalse((bool)Call(hud, "StartCourseByHost"));
                Assert.IsFalse((bool)Call(hud, "StartPerformanceByHost"));
                Assert.AreEqual(Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "Session"), "MinPlayers"),
                    Get(session, "MinPlayers"), "실제 캠페인에서는 테스트 씬의 1인 시작 예외를 해제한다.");
                var finish = (Pose)Call(course, "GetFinishPose", (int)Get(player, "Slot"));
                Call(mover, "SetStartPose", finish.position, finish.rotation, true);
                yield return Until(() => Get(bridge, "Result") != null && Phase == "RestTime", 5, "Adapter 공용 정산");
                Assert.AreEqual("Succeeded", Get(run, "State").ToString());
                Assert.AreEqual("Playing", Get(session, "GameState").ToString());
                Assert.AreEqual(1, ((ICollection)Get(rewards, "History")).Count);
                Assert.AreEqual(1, Get(run, "Round"));
                var results = Get(run, "Results");
                Call(adapter, "HandleResult", Get(results, "Latest"));
                Call(adapter, "LateUpdate");
                yield return null;
                Assert.AreEqual(1, ((ICollection)Get(rewards, "History")).Count);
                Assert.AreEqual(1, Get(run, "Round"));
            }
            finally
            {
                Set(space, "tightropeScene", previousScene);
                if (entry != null) Object.Destroy(entry.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator FirstArrivalSettlesOnceAndKeepsSessionPlaying()
        {
            // 별도 관문 검사와 분리하여 공용 정산 경계만 구성한다. TearDown에서 원래 규칙을 복구한다.
            Set(sawSettingsSnapshotTarget, "finaleFourStackRequired", false);
            yield return BeginPerformance();
            Assert.AreNotEqual(0, Convert.ToInt32(Call(player, "ServerCut", 1)));
            int parts = Convert.ToInt32(Get(player, "LostParts"));
            yield return null;
            var finish = (Pose)Call(course, "GetFinishPose", (int)Get(player, "Slot"));
            Call(mover, "SetStartPose", finish.position, finish.rotation, true);
            yield return Until(() => Get(bridge, "Result") != null && Phase == "RestTime", 5, "도착 공용 정산");
            var result = Get(bridge, "Result");
            Assert.IsTrue((bool)Get(result, "Succeeded"));
            Assert.AreEqual("Succeeded", Get(run, "State").ToString());
            Assert.AreEqual("RestTime", Phase);
            Assert.AreEqual("Playing", Get(session, "GameState").ToString(), "공연 성공은 방 종료가 아니다.");
            Assert.AreEqual(parts, Convert.ToInt32(Get(player, "LostParts")));
            long balance = Convert.ToInt64(Get(rewards, "Balance"));
            Assert.IsFalse((bool)Call(rewards, "FinishPerformance", true, "Completed", null));
            Assert.IsFalse((bool)Call(run, "RestartByHost"));
            yield return null;
            Assert.AreSame(result, Get(bridge, "Result"));
            Assert.AreEqual(balance, Convert.ToInt64(Get(rewards, "Balance")));
            Assert.AreEqual(1, ((ICollection)Get(rewards, "History")).Count);
        }

        [UnityTest]
        public IEnumerator AllDeadRecordsDeathThenRevivesOneArmedWithoutRestart()
        {
            yield return BeginPerformance();
            yield return null;
            Assert.IsTrue((bool)Call(player, "ServerKill"));
            yield return Until(() => Get(bridge, "Result") != null && Phase == "RestTime", 5, "전멸 정산과 다음 시간대");
            var result = Get(bridge, "Result");
            Assert.IsFalse((bool)Get(result, "Succeeded"));
            Assert.AreEqual(1, ((ICollection)Get(result, "Deaths")).Count, "부활 전 사망이 정산에 남아야 한다.");
            Assert.AreEqual(0L, Convert.ToInt64(Get(result, "Income")));
            Assert.AreEqual("Failed", Get(run, "State").ToString());
            var diagnostic = Get(Get(run, "Results"), "Latest");
            var participant = ((IEnumerable)Get(diagnostic, "Participants")).Cast<object>().Single();
            Assert.AreEqual("Fallen", Get(participant, "Outcome").ToString(), "외줄 진단에도 부활 전 사망이 남아야 한다.");
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.AreEqual(3, CountBits(Convert.ToInt32(Get(player, "LostParts"))), "공용 부활은 외팔 상태여야 한다.");
            int round = (int)Get(run, "Round");
            yield return new WaitForSeconds(3.3f);
            Assert.AreEqual("Failed", Get(run, "State").ToString());
            Assert.AreEqual(round, Get(run, "Round"), "옛 3초 자동 재시작이 실행되면 안 된다.");
            Assert.AreEqual(1, ((ICollection)Get(rewards, "History")).Count);
        }

        [UnityTest]
        public IEnumerator TimeLimitEndsThroughSharedSettlementWithoutRestoringSurvivors()
        {
            yield return BeginPerformance();
            Call(player, "ServerSetOneArm");
            // 메모리의 제한시간만 줄여 실제 Update의 시간 초과 경로를 기다린다. 에셋은 저장하지 않는다.
            Set(runSettings, "timeLimit", 1f);
            yield return Until(() => Get(bridge, "Result") != null && Phase == "RestTime", 5, "제한시간 정산과 공용 단계 전환");
            var result = Get(bridge, "Result");
            Assert.IsFalse((bool)Get(result, "Succeeded"));
            Assert.AreEqual("TimeExpired", Get(result, "EndReason"));
            Assert.AreEqual("RestTime", Phase);
            Assert.AreEqual(0, ((ICollection)Get(result, "Deaths")).Count);
            Assert.AreEqual(3, CountBits(Convert.ToInt32(Get(player, "LostParts"))));
        }

        [UnityTest]
        public IEnumerator FollowingHumanCannonPhaseDoesNotStartOrSettleTightrope()
        {
            yield return BeginPerformance();
            Set(runSettings, "timeLimit", 1f);
            yield return Until(() => Phase == "RestTime", 5, "공연 종료");
            var result = Get(bridge, "Result");
            Assert.IsTrue((bool)Call(campaign, "NotifyAllAtStageEntrance"));
            yield return null;
            Assert.AreEqual("HumanCannon", Get(Get(campaign, "State"), "TrickId"));
            Assert.IsFalse((bool)Get(bridge, "IsTightropePerformance"));
            Assert.IsFalse((bool)Get(bridge, "PerformanceOpen"));
            var finish = (Pose)Call(course, "GetFinishPose", (int)Get(player, "Slot"));
            Call(mover, "SetStartPose", finish.position, finish.rotation, true);
            yield return null;
            Assert.AreSame(result, Get(bridge, "Result"));
            Assert.AreNotEqual("Running", Get(run, "State").ToString());
            Assert.AreEqual(1, ((ICollection)Get(rewards, "History")).Count);
            Assert.IsTrue((bool)Call(player, "ServerKill"), "외줄이 다음 공연의 사망 판정을 막으면 안 된다.");
        }

        [UnityTest]
        public IEnumerator CourseDisableReturnsSharedControlsAndInjuryPolicy()
        {
            yield return BeginPerformance();
            var effects = player.GetComponent(GameType("CurtainCall.Damage.BodyDamageEffects"));
            yield return Until(() => Get(effects, "Policy") != null, 3, "공연 조작 컴포넌트의 부상 정책 적용");
            Assert.AreEqual("TightropeBodyPenalty", Get(effects, "Policy").GetType().Name);
            var rope = (Vector3)Call(course, "GetLanePosition", 0, 2f);
            Call(mover, "SetStartPose", rope, Quaternion.identity, true);
            var controller = player.GetComponent(GameType("CurtainCall.Player.PlayerController"));
            yield return Until(() => (bool)Get(controller, "HasAssignedScheme"), 3, "줄 착지와 외줄 조작 전환");
            Assert.IsTrue((bool)Get(controller, "HasAssignedScheme"));
            course.gameObject.SetActive(false);
            yield return null;
            Assert.IsFalse((bool)Get(controller, "HasAssignedScheme"));
            var policy = Get(effects, "Policy");
            Assert.IsTrue(policy == null || policy.GetType().Name != "TightropeBodyPenalty");
            Assert.IsFalse((bool)Get(player.GetComponent(GameType("CurtainCall.Player.PlayerBalance")), "IsActive"));
        }

        [UnityTest]
        public IEnumerator HorizontalSawContactsFollowSharedCutOrderRegardlessOfSide()
        {
            yield return BeginPerformance();
            var sawObject = new GameObject("Shared injury contact test");
            var saw = (Behaviour)sawObject.AddComponent(GameType("CurtainCall.Tightrope.Saws.TightropeSaws"));
            saw.enabled = false;
            Set(saw, "course", course);
            Set(saw, "run", run);
            var body = Activator.CreateInstance(GameType("CurtainCall.Tightrope.Saws.BodyCapsule"),
                player.transform.position, (float)Get(mover, "BodyHeight"), (float)Get(mover, "BodyRadius"));
            int expected = 0;
            var order = new[] { "LeftLeg", "RightLeg", "LeftArm", "RightArm" };
            for (int i = 0; i < order.Length; i++)
            {
                var kind = Enum.Parse(GameType("CurtainCall.Tightrope.Saws.SawKind"), "Horizontal");
                var rightContact = player.transform.position + (Vector3)Get(course, "Right") * 0.1f + Vector3.up * 1.2f;
                Call(saw, "ServerTouch", player, body, kind, 100 + i, i, rightContact);
                expected |= Convert.ToInt32(Enum.Parse(GameType("CurtainCall.Player.BodyPart"), order[i]));
                Assert.AreEqual(expected, Convert.ToInt32(Get(player, "LostParts")), order[i]);
            }
            Assert.AreEqual("Fallen", Get(player, "State").ToString());
            Object.Destroy(sawObject);
            yield return Until(() => Get(bridge, "Result") != null, 5, "네 부위 손실의 공용 사망 정산");
            Assert.AreEqual(1, ((ICollection)Get(Get(bridge, "Result"), "Deaths")).Count);
        }

        string Phase => Get(Get(campaign, "State"), "Phase").ToString();

        [UnityTest]
        public IEnumerator LobbyGroundRemainsSafeWithoutBalanceInput()
        {
            var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            Assert.IsFalse((bool)Get(balance, "IsActive"), "접속 직후 출발 바닥에서는 균형이 꺼져야 한다.");
            yield return new WaitForSeconds(15f);
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.IsFalse((bool)Get(balance, "HasFallen"));
            Assert.IsFalse((bool)Get(balance, "IsActive"));
        }

        [UnityTest]
        public IEnumerator WalkingOntoRopeAutomaticallyChangesControlsDuringIntroAndPerformance()
        {
            Assert.IsTrue((bool)Call(session, "StartGame"));
            yield return Until(() => Phase == "Intro", 5, "도입 시작");
            yield return WalkOnAndOffRope();
            Assert.IsTrue((bool)Call(campaign, "EndIntro"));
            yield return Until(() => (bool)Get(bridge, "PerformanceOpen"), 5, "공연 시작");
            yield return WalkOnAndOffRope();
        }

        IEnumerator WalkOnAndOffRope()
        {
            var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            var controller = player.GetComponent(GameType("CurtainCall.Player.PlayerController"));
            var switcher = course.GetComponent(GameType("CurtainCall.Tightrope.CourseControlSwitcher"));
            Assert.IsFalse((bool)Get(balance, "IsActive"));
            float deadline = Time.realtimeSinceStartup + 8f;
            while ((float)Call(course, "GetDistance", player.transform.position) < 0.35f && Time.realtimeSinceStartup < deadline)
            {
                Call(mover, "SetMoveInput", 0f, 1f);
                yield return null;
            }
            Assert.IsTrue((bool)Get(switcher, "IsLocalOnRope"), "바닥에서 걸어 진입하면 별도 키 없이 줄타기가 켜져야 한다.");
            Assert.AreEqual("TightropeControlScheme", Get(controller, "Scheme").GetType().Name);
            Assert.IsTrue((bool)Get(balance, "IsActive"));
            Assert.IsFalse((bool)Get(mover, "FreeMovement"));
            deadline = Time.realtimeSinceStartup + 8f;
            while ((float)Call(course, "GetDistance", player.transform.position) > -0.5f && Time.realtimeSinceStartup < deadline)
            {
                Call(mover, "SetMoveInput", 0f, -1f);
                yield return null;
            }
            yield return null;
            Assert.IsFalse((bool)Get(switcher, "IsLocalOnRope"));
            Assert.IsFalse((bool)Get(balance, "IsActive"), "출발 바닥으로 돌아오면 균형이 꺼져야 한다.");
            Assert.IsTrue((bool)Get(mover, "FreeMovement"));
            Assert.AreEqual("Normal", Get(player, "State").ToString());
        }

        [UnityTest]
        public IEnumerator RKeyRestartsWholeCourseAndClearsInjuriesWithoutDuplicateSettlement()
        {
            yield return BeginPerformance();
            Call(player, "ServerCut", 2);
            Call(course, "SetLaneBlocked", 1, true);
            Call(course, "BlockSegment", 3, 3f, 9f, true);
            var position = (Vector3)Call(course, "GetLanePosition", 2, 4f);
            Call(mover, "SetStartPose", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            yield return new WaitForSeconds(0.3f);
            int oldRound = (int)Get(run, "Round");
            var oldId = (string)Get(bridge, "PerformanceId");
            int historyCount = ((ICollection)Get(rewards, "History")).Count;
            var oldKeyboard = Keyboard.current;
            var originalInputSettings = InputSystem.settings;
            var originalBackground = originalInputSettings.backgroundBehavior;
            var originalEditorInput = originalInputSettings.editorInputBehaviorInPlayMode;
            bool cloneSettings = UnityEditor.EditorUtility.IsPersistent(originalInputSettings);
            // InputSystem은 교체된 임시 기본 설정을 파괴한다. 에셋인 경우만 복제하고 임시 설정은 값을 복구한다.
            var testInputSettings = cloneSettings ? Object.Instantiate(originalInputSettings) : originalInputSettings;
            if (cloneSettings) { testInputSettings.hideFlags = HideFlags.DontSave; InputSystem.settings = testInputSettings; }
            // 화면 포커스를 조작하지 않는 검사에서도 합성 키가 정상 InputSystem 경로를 통과하게 한다.
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return Until(() => !(bool)Static("CurtainCall.Player.InputGate", "IsBlocked"), 3, "리셋 입력 허용");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return null;
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return Until(() => (int)Get(run, "Round") > oldRound && (bool)Get(bridge, "PerformanceOpen"), 5, "R 전체 리셋");
                Assert.AreNotEqual(oldId, Get(bridge, "PerformanceId"));
                Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
                Assert.AreEqual("Normal", Get(player, "State").ToString());
                Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.LessThan(0f));
                Assert.IsFalse((bool)Get(player.GetComponent(GameType("CurtainCall.Player.PlayerBalance")), "IsActive"));
                Assert.IsFalse((bool)Call(course, "IsLaneBlocked", 1));
                Assert.IsFalse((bool)Call(course, "IsSegmentBlocked", 3, 5f));
                Assert.IsFalse((bool)Get(run, "IsPerformanceStarted"));
                Assert.That((float)Get(run, "TimeRemaining"), Is.GreaterThan((float)Get(run, "TimeLimit") - 1f));
                Assert.AreEqual(historyCount, ((ICollection)Get(rewards, "History")).Count, "리셋으로 가짜 실패 정산이 생기면 안 된다.");
                Assert.IsNull(Get(bridge, "Result"));
                Assert.AreEqual("Playing", Get(session, "GameState").ToString());
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
                if (cloneSettings)
                {
                    InputSystem.settings = originalInputSettings;
                    Object.Destroy(testInputSettings);
                }
                else
                {
                    testInputSettings.backgroundBehavior = originalBackground;
                    testInputSettings.editorInputBehaviorInPlayMode = originalEditorInput;
                }
            }
        }

        [UnityTest]
        public IEnumerator LobbyResetRestoresStartAndHealthWithoutStartingGame()
        {
            Call(player, "ServerCut", 2);
            Call(course, "SetLaneBlocked", 1, true);
            Call(course, "BlockSegment", 3, 3f, 9f, true);
            var position = (Vector3)Call(course, "GetLanePosition", 2, 4f);
            Call(mover, "SetStartPose", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return null;
            Assert.AreEqual("Waiting", Get(session, "GameState").ToString());
            Assert.AreEqual("None", Phase);
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.LessThan(0f));
            Assert.IsFalse((bool)Get(player.GetComponent(GameType("CurtainCall.Player.PlayerBalance")), "IsActive"));
            Assert.IsFalse((bool)Call(course, "IsLaneBlocked", 1));
            Assert.IsFalse((bool)Call(course, "IsSegmentBlocked", 3, 5f));
            Assert.IsFalse((bool)Get(run, "IsPerformanceStarted"));
            Assert.AreEqual(0, ((ICollection)Get(rewards, "History")).Count);
        }

        [UnityTest]
        public IEnumerator FallRequestFromBeforeResetCannotKillFreshAttempt()
        {
            yield return BeginPerformance();
            int oldEpoch = (int)Get(Get(player, "restartCount"), "Value");
            int oldRound = (int)Get(run, "Round");
            var fallen = Enum.Parse(GameType("CurtainCall.Network.PlayerSync.PlayerState"), "Fallen");
            var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return Until(() => (int)Get(run, "Round") > oldRound && (bool)Get(bridge, "PerformanceOpen"), 5, "새 회차");
            Assert.That((int)Get(Get(player, "restartCount"), "Value"), Is.GreaterThan(oldEpoch));
            Call(player, "RequestStateRpc", fallen, oldEpoch);
            yield return null;
            Assert.AreEqual("Normal", Get(player, "State").ToString(), "리셋 전에 보낸 사망 요청은 버려야 한다.");
            Assert.IsTrue((bool)Get(bridge, "PerformanceOpen"));
            Call(player, "RequestState", fallen);
            yield return Until(() => Get(bridge, "Result") != null, 5, "현재 회차의 정상 사망 요청");
            Assert.IsFalse((bool)Get(Get(bridge, "Result"), "Succeeded"));
        }

        [UnityTest]
        public IEnumerator WholeCourseResetAfterDeathStartsWithFreshLivingRoster()
        {
            yield return BeginPerformance();
            Call(player, "ServerKill");
            yield return Until(() => Get(bridge, "Result") != null && Phase == "RestTime", 5, "전멸 정산");
            int oldRound = (int)Get(run, "Round");
            var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            Assert.IsFalse((bool)Call(hud, "RestartCourseByHost"), "같은 프레임 중복 리셋은 한 번만 처리한다.");
            yield return Until(() => (int)Get(run, "Round") > oldRound && (bool)Get(bridge, "PerformanceOpen"), 5, "사망 후 리셋");
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            Assert.IsNull(Get(bridge, "Result"));
            var segments = ((IEnumerable)Get(Get(rewards, "State"), "Segments")).Cast<object>().ToArray();
            Assert.AreEqual(1, segments.Length);
            Assert.IsFalse((bool)Get(segments[0], "Dead"));
            Assert.AreEqual(1, ((ICollection)Get(rewards, "History")).Count, "이전 결과를 중복 정산하지 않는다.");
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue((bool)Get(bridge, "PerformanceOpen"), "이전 사망 기록 때문에 새 공연이 바로 끝나면 안 된다.");
        }

        [UnityTest]
        public IEnumerator EarlyLaneJumpPressSurvivesLandingAndUsesPressedDirectionOnce()
        {
            yield return BeginPerformance();
            var position = (Vector3)Call(course, "GetLanePosition", 1, 4f);
            Call(mover, "SetStartPose", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            yield return null;
            yield return Until(() => ((CharacterController)Get(mover, "Body")).isGrounded, 3, "줄 위 접지");
            var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            Call(balance, "SetBalanceActive", false); // 입력 여유만 검사하며 균형 난수는 배제한다.
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            var frameType = GameType("CurtainCall.Player.PlayerInputFrame");
            object Frame(bool press, int direction)
            {
                var frame = Activator.CreateInstance(frameType);
                Set(frame, "ActionPressed", press);
                Set(frame, "AuxDirection", direction);
                return frame;
            }
            try
            {
                Call(scheme, "Tick", context, Frame(true, 0));
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2, "첫 제자리 점프");
                yield return Until(() => (float)Get(mover, "verticalSpeed") < 0f
                    && player.transform.position.y - position.y < 0.28f, 3, "착지 직전");
                Assert.IsTrue((bool)Get(mover, "IsAirborne"), "착지 전에 다음 점프를 눌러야 한다.");
                Call(scheme, "Tick", context, Frame(true, 1));
                float deadline = Time.realtimeSinceStartup + 2f;
                while (Get(mover, "CurrentJump").ToString() != "Lane" && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    Call(scheme, "Tick", context, Frame(false, -1));
                }
                Assert.AreEqual("Lane", Get(mover, "CurrentJump").ToString());
                Assert.AreEqual(1, (int)Get(mover, "LaneJumpDirection"), "착지 전에 누른 오른쪽 방향을 유지해야 한다.");
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3, "옆줄 점프 착지");
                var expected = (Vector3)Call(course, "GetLanePosition", 2, 4f);
                Assert.That(Vector3.Distance(player.transform.position, expected), Is.LessThan(0.2f));
                for (int i = 0; i < 4; i++)
                {
                    Call(scheme, "Tick", context, Frame(false, 1));
                    yield return null;
                    Assert.IsFalse((bool)Get(mover, "IsAirborne"), "한 번 누른 입력이 반복 점프하면 안 된다.");
                }
            }
            finally
            {
                Call(scheme, "Exit", context);
                Object.Destroy(scheme);
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneBuildsOutlinesAndExistingCooperativeSystems()
        {
            yield return BeginPerformance();
            yield return null;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            Assert.IsNotNull(saws, "톱날 프리팹 연결");
            Assert.IsNotNull(Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current"), "기존 코스의 추격 불·구간 화재 연결");
            Assert.IsNotNull(Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.Fire.FireView")));
            Assert.IsNotNull(Static("CurtainCall.Tightrope.PiggybackSystem", "Current"));
            Assert.IsNotNull(Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.PiggybackFollow")));
            Assert.IsNotNull(Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.RopeLaneJumpRules")));
            var outlineType = GameType("CurtainCall.Tightrope.TightropeOutline");
            Assert.That(saws.GetComponentsInChildren(outlineType, true).Length, Is.GreaterThan(0));
            Assert.That(course.GetComponentsInChildren(outlineType, true).Length, Is.EqualTo(2), "출발·도착 플랫폼");
            var shader = Resources.Load<Shader>("TightropeOutline");
            Assert.IsNotNull(shader);
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader));
        }

        [UnityTest]
        public IEnumerator WarningBandAndTunedWalkAreAppliedByAutomaticBoarding()
        {
            yield return BeginPerformance();
            var position = (Vector3)Call(course, "GetLanePosition", 1, 2f);
            Call(mover, "SetStartPose", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            yield return Until(() => ((CharacterController)Get(mover, "Body")).isGrounded
                && !(bool)Get(mover, "FreeMovement"), 3, "자동 줄 탑승");
            var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            Assert.IsTrue((bool)Get(balance, "IsActive"));
            ((Behaviour)balance).enabled = false; // 정확한 경고 값을 고정해 실제 UI 표시를 확인한다.
            Call(balance, "SetDisplayedState", true, 40f);
            yield return null;
            Assert.AreEqual("Yellow", Get(balance, "Zone").ToString());
            var gauge = Object.FindAnyObjectByType(GameType("CurtainCall.UI.PerformanceHudV1.TightropeBalanceView"));
            Assert.IsNotNull(gauge);
            Assert.IsTrue((bool)Get(gauge, "HasValidData"));
            var zones = (RectTransform[])Get(gauge, "zoneRects");
            Assert.IsTrue(zones[1].gameObject.activeInHierarchy);
            Assert.IsTrue(zones[3].gameObject.activeInHierarchy);
            Assert.Greater(zones[1].rect.width, 0, "새 P1의 왼쪽 경고 구간");
            Assert.Greater(zones[3].rect.width, 0, "새 P1의 오른쪽 경고 구간");
            float speed = (float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "RopeMovement"), "MoveSpeed");
            Assert.That((float)Get(mover, "CourseSpeedOverride"), Is.EqualTo(speed));
            float from = (float)Call(course, "GetDistance", player.transform.position);
            float elapsed = 0f;
            while (elapsed < 0.6f)
            {
                Call(mover, "SetMoveInput", 1f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            float travelled = (float)Call(course, "GetDistance", player.transform.position) - from;
            Assert.That(travelled / elapsed, Is.EqualTo(speed).Within(0.15f), "실제 줄 위 이동 속도");
        }

        IEnumerator BeginPerformance()
        {
            Assert.IsTrue((bool)Call(session, "StartGame"));
            yield return Until(() => Phase == "Intro", 5, "공용 도입 단계");
            Assert.AreEqual("Waiting", Get(run, "State").ToString());
            Assert.IsFalse((bool)Get(bridge, "PerformanceOpen"));
            Assert.IsTrue((bool)Call(campaign, "EndIntro"));
            yield return Until(() => (bool)Get(bridge, "PerformanceOpen") && Get(run, "State").ToString() == "Running", 5, "공용 외줄 공연 시작");
        }

        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator MovingHorizontalSawCanBeJumpedWithoutDamage()
        {
            yield return BeginPerformance();
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var sawSettings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Saws");
            int sawIndex = ActiveHorizontalSaw(saws, true);
            float side = (float)Call(course, "GetSide", (Vector3)Call(course, "GetLanePosition", 2, 0f));
            float distance = (float)Call(course, "GetDistance", (Vector3)Call(saws, "HorizontalPositionAtSide", sawIndex, side));
            yield return StandOnRopeForSawTest(2, distance);
            Vector3 right = (Vector3)Get(course, "Right");
            float SideGap() => Vector3.Dot((Vector3)Call(saws, "GetHorizontalPosition", sawIndex) - player.transform.position, right);
            // 실제 톱날이 점프 정점에 몸 중심을 지나가도록 한다. 복귀 중인 톱날은 선택하지 않는다.
            float takeoffGap = (float)Call(sawSettings, "GetHorizontalSpeed", sawIndex) * (float)Get(mover, "AirTime") * 0.5f;
            float previousGap = SideGap();
            bool readyToJump = false;
            yield return Until(() =>
            {
                if (readyToJump) return true; // Until은 성공 직후 같은 프레임에 조건을 한 번 더 확인한다.
                float gap = SideGap();
                bool approaching = gap > previousGap;
                previousGap = gap;
                return readyToJump = approaching && gap >= -takeoffGap && gap < -0.8f;
            }, 15, "첫 톱날 접근");
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            Call(mover, "RequestJump");
            yield return Until(() => (bool)Get(mover, "IsAirborne"), 2, "톱날 회피 점프");
            float peak = player.transform.position.y;
            float deadline = Time.realtimeSinceStartup + 3f;
            while ((bool)Get(mover, "IsAirborne") && Time.realtimeSinceStartup < deadline)
            {
                peak = Mathf.Max(peak, player.transform.position.y);
                Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")), "공중에서 발밑으로 지나간 톱날에 다치면 안 된다.");
                yield return null;
            }
            Assert.IsFalse((bool)Get(mover, "IsAirborne"));
            Assert.That(SideGap(), Is.GreaterThan(0.8f), "착지 전에 톱날이 몸 너비를 완전히 지나가야 한다.");
            Assert.That(peak - course.transform.position.y, Is.GreaterThan((float)Get(sawSettings, "horizontalAttackTop")));
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            Assert.AreEqual("Normal", Get(player, "State").ToString());
        }

        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator MovingHorizontalSawStillHitsPlayerWhoDoesNotJump()
        {
            yield return BeginPerformance();
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            int sawIndex = ActiveHorizontalSaw(saws, true);
            float side = (float)Call(course, "GetSide", (Vector3)Call(course, "GetLanePosition", 2, 0f));
            float distance = (float)Call(course, "GetDistance", (Vector3)Call(saws, "HorizontalPositionAtSide", sawIndex, side));
            yield return StandOnRopeForSawTest(2, distance);
            yield return Until(() => Convert.ToInt32(Get(player, "LostParts")) != 0, 15, "피하지 않은 톱날 접촉");
            Assert.AreEqual(1, CountBits(Convert.ToInt32(Get(player, "LostParts"))), "판정을 끈 상태로 회피가 통과해서는 안 된다.");
        }

        [UnityTest]
        public IEnumerator IncomingVerticalSawCanBeDodgedByLaneJump()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            const float distance = 20f; // 기존 가로 톱날 사이의 줄을 사용한다.
            yield return StandOnRopeForSawTest(1, distance);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var sawSettings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Saws");
            float speed = (float)Get(sawSettings, "verticalSpeed");
            float elapsed = ((float)Get(sawSettings, "verticalSpawnDistance") - distance - 1.2f) / speed;
            // 출현 대기 시간만 생략한다. 이후 이동·접촉·착지는 저장된 씬의 Update에서 처리한다.
            Call(saws, "SetVertical", true, 1, 1, elapsed, -1f);
            Call(mover, "RequestLaneJump", 1);
            yield return Until(() => (bool)Get(mover, "IsAirborne"), 2, "옆줄 회피 시작");
            yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3, "옆줄 착지");
            yield return Until(() => (float)Get(saws, "VerticalDistance") < distance - 0.8f, 4, "이전 줄로 톱날 통과");
            Assert.IsTrue((bool)Get(saws, "IsVerticalActive"), "화재 보호로 제거된 톱을 회피 성공으로 세지 않는다.");
            var expected = (Vector3)Call(course, "GetLanePosition", 2, distance);
            Assert.That(Vector3.Distance(player.transform.position, expected), Is.LessThan(0.2f));
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            Assert.AreEqual("Normal", Get(player, "State").ToString());
        }

        [UnityTest]
        public IEnumerator RestoredCourseFireWarnsBlocksThenResetsWithWholeCourse()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var fire = (Component)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            Assert.IsNotNull(fire);
            yield return Until(() => Get(fire, "Settings") != null, 3, "기존 화재 설정 연결");
            var snapshot = Get(fire, "Settings");
            Set(snapshot, "triggerMode", Enum.Parse(GameType("CurtainCall.Tightrope.Fire.FireTriggerMode"), "Time"));
            var segments = (Array)Get(snapshot, "segments");
            Assert.AreEqual((int)Get(course, "LaneCount") - 1, segments.Length, "한 줄을 제외한 모든 줄에 화재 단계를 둔다.");
            // 이번 회차 메모리 복사본의 시간만 앞당긴다. 저장된 코스 배치·에셋은 변경하지 않는다.
            var first = segments.GetValue(0);
            Set(first, "lane", 1); // 색상·판정 회귀는 현재 검사자의 옆 줄로 고정한다.
            Set(first, "igniteAt", (float)Get(run, "PerformanceElapsed") + 1.5f);
            segments.SetValue(first, 0);
            Set(snapshot, "warningSeconds", 1f);
            Set(snapshot, "chaseDelay", 0f);
            yield return Until(() => (int)Get(fire, "PendingSegment") == 0, 3, "시간 모드 화재 예고");
            yield return null;
            Assert.That((float)Call(fire, "WarningRemaining", 0), Is.GreaterThan(0f));
            Assert.IsTrue((bool)Get(fire, "IsChasing"));
            var view = fire.transform.Find("SegmentFire1Part1");
            Assert.IsNotNull(view);
            Assert.IsTrue(view.gameObject.activeInHierarchy, "발화 전 예고 표시가 있어야 한다.");
            var warning = view.GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(warning.g, Is.GreaterThan(0.7f), "발화 전에는 노란 경고를 표시한다.");
            float chaseBefore = (float)Get(fire, "ChaseDistance");
            yield return Until(() => (bool)Call(fire, "IsSegmentActive", 0), 3, "구간 실제 발화");
            yield return null;
            float burnedProbe = (float)Get(first, "from") + .1f;
            Assert.IsTrue((bool)Call(course, "IsSegmentBlocked", 0, burnedProbe), "불붙은 줄 구간은 착지할 수 없어야 한다.");
            Assert.That((float)Get(fire, "ChaseDistance"), Is.GreaterThan(chaseBefore));
            Assert.That(view.GetComponent<Renderer>().sharedMaterial.color.g, Is.LessThan(0.3f), "발화 후에는 불 표시로 바뀐다.");
            int oldRound = (int)Get(run, "Round");
            var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return Until(() => (int)Get(fire, "Round") > oldRound && (bool)Get(bridge, "PerformanceOpen"), 5, "화재 포함 전체 리셋");
            yield return null;
            Assert.IsFalse((bool)Get(fire, "IsChasing"));
            Assert.IsFalse((bool)Call(fire, "IsSegmentActive", 0));
            Assert.IsFalse((bool)Call(course, "IsSegmentBlocked", 0, burnedProbe));
            Assert.That((float)Call(fire, "WarningRemaining", 0), Is.LessThan(0f));
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.IsFalse(fire.transform.Find("ChaseFire").gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator CourseConvergesOneLaneAtATimeAndSoloRouteRemainsOpen()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 2f);
            var fire = (Component)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            var snapshot = Get(fire, "Settings");
            Assert.AreEqual("LeadDistance", Get(snapshot, "triggerMode").ToString());
            Set(snapshot, "warningSeconds", 0.2f); // 대기 시간만 줄이고 저장된 거리·순서·구간은 사용한다.
            var segments = (Array)Get(snapshot, "segments");
            Assert.AreEqual((int)Get(course, "LaneCount") - 1, segments.Length);
                Assert.AreEqual("RandomConnected", Get(snapshot, "laneOrder").ToString());
            int safeLane = (int)Call(snapshot, "FindSurvivingLane", (int)Get(course, "LaneCount")) - 1;
            Assert.That(safeLane, Is.InRange(0, (int)Get(course, "LaneCount") - 1));
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments.GetValue(i);
                float trigger = (float)Get(segment, "triggerDistance");
                // 예고 선두 거리와 실제 소각 구간은 다르다. 소각 구간 안에서 연결 수를 검사한다.
                float probe = Mathf.Max(trigger, (float)Get(segment, "from") + .1f);
                if ((bool)Call(course, "IsSwingGap", probe)) probe = (float)Get(course, "SwingGapTo") + 1f;
                var position = (Vector3)Call(course, "GetLanePosition", safeLane, probe);
                Call(mover, "SetStartPose", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
                int expected = i;
                yield return Until(() => (int)Get(fire, "PendingSegment") == expected, 3, "다음 줄 예고");
                Assert.IsFalse((bool)Call(fire, "IsSegmentActive", i), "예고가 끝나기 전에는 줄이 불타면 안 된다.");
                for (int later = i + 1; later < segments.Length; later++)
                    Assert.IsFalse((bool)Call(fire, "IsSegmentActive", later));
                yield return Until(() => (bool)Call(fire, "IsSegmentActive", expected), 3, "예고 후 한 줄 발화");
                int usable = Enumerable.Range(0, (int)Get(course, "LaneCount"))
                    .Count(lane => (bool)Call(course, "IsLaneUsable", lane, probe));
                Assert.AreEqual((int)Get(course, "LaneCount") - i - 1, usable);
                Assert.IsTrue((bool)Call(course, "IsLaneUsable", safeLane, probe), "실제 끊어진 구간 바깥에서 화재 생존 줄을 검사한다.");
            }
            var saws = Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            float aheadOfSurvivor = (float)Call(course, "GetDistance", player.transform.position) + 5f;
            Assert.IsTrue((bool)Call(saws, "WouldBlockRequiredRoute", safeLane, aheadOfSurvivor),
                "현재 최후반 한 줄에 회피 불가능한 일반 톱을 추가하는 경로는 보호한다.");
            int oldRound = (int)Get(run, "Round");
            var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return Until(() => (int)Get(fire, "Round") > oldRound && (bool)Get(bridge, "PerformanceOpen"), 5, "수렴 코스 초기화");
            Assert.AreEqual(-1, (int)Get(fire, "PendingSegment"));
            for (int lane = 0; lane < (int)Get(course, "LaneCount"); lane++)
                Assert.IsTrue((bool)Call(course, "IsLaneUsable", lane, 70f));
            Assert.AreEqual("Normal", Get(player, "State").ToString());
        }

        [UnityTest]
        public IEnumerator FireWarningClearsAnExistingSawFromTheRequiredEscapeRoute()
        {
            yield return BeginPerformance();
            var fire = (Component)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current");
            var settings = Get(fire, "Settings");
            var segments = (Array)Get(settings, "segments");
            var warning = segments.GetValue(0);
            int lane = (int)Get(warning, "lane") - 1;
            yield return StandOnRopeForSawTest(lane, 4f);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var sawSettings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Saws");
            float elapsed = ((float)Get(sawSettings, "verticalSpawnDistance") - 8f) / (float)Get(sawSettings, "verticalSpeed");
            Call(saws, "SetVertical", true, lane, 1, elapsed, -1f);
            yield return null;
            Assert.IsTrue((bool)Get(saws, "IsVerticalActive"), "경고 전에는 옆줄로 회피할 수 있는 정면 톱을 유지한다.");
            Set(warning, "triggerDistance", 4f);
            Set(warning, "from", 0f); // 이번 검사의 탈출 경로(4m)를 포함하는 화재를 구성한다.
            Set(settings, "warningSeconds", 4f);
            segments.SetValue(warning, 0);
            yield return Until(() => (int)Get(fire, "PendingSegment") == 0, 3, "첫 줄 소각 경고");
            yield return Until(() => !(bool)Get(saws, "IsVerticalActive"), 3, "탈출 경로의 기존 정면 톱 제거");
            Assert.IsFalse((bool)Call(fire, "IsSegmentActive", 0), "불붙기 전에 이동 경로를 비워야 한다.");
        }

        [UnityTest]
        public IEnumerator IncomingVerticalSawStillHitsPlayerWhoDoesNotDodge()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            const float distance = 20f;
            yield return StandOnRopeForSawTest(1, distance);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var sawSettings = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Saws");
            float speed = (float)Get(sawSettings, "verticalSpeed");
            float elapsed = ((float)Get(sawSettings, "verticalSpawnDistance") - distance - 1.2f) / speed;
            Call(saws, "SetVertical", true, 1, 1, elapsed, -1f);
            yield return Until(() => Convert.ToInt32(Get(player, "LostParts")) != 0, 3, "피하지 않은 정면 톱날 접촉");
            yield return Until(() => Get(bridge, "Result") != null, 3, "정면 톱 즉사의 공용 정산");
            Assert.AreEqual(1, ((ICollection)Get(Get(bridge, "Result"), "Deaths")).Count,
                "현재 정면 톱 규칙은 한 부위 절단이 아니라 즉사다.");
        }

        int ActiveHorizontalSaw(Component saws, bool? startsLeft = null) =>
            Enumerable.Range(0, (int)Get(saws, "HorizontalCount")).First(i => (bool)Call(saws, "IsHorizontalEnabled", i)
                && (!startsLeft.HasValue || (bool)Call(Get(saws, "Settings"), "StartsLeft", i) == startsLeft.Value));

        IEnumerator StandOnRopeForSawTest(int lane, float distance)
        {
            var position = (Vector3)Call(course, "GetLanePosition", lane, distance);
            Call(mover, "SetStartPose", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")), true);
            yield return Until(() => ((CharacterController)Get(mover, "Body")).isGrounded
                && !(bool)Get(mover, "FreeMovement"), 3, "톱날 검사 위치 줄 탑승");
            // 회피 궤적만 검사한다. 균형 난수로 인한 추락은 별도 균형 검사에서 다룬다.
            ((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"))).enabled = false;
        }

        static int CountBits(int bits) { int count = 0; for (; bits != 0; bits &= bits - 1) count++; return count; }
        static IEnumerator Until(Func<bool> predicate, float seconds, string reason)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(predicate(), reason + " 시간 초과");
        }
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Static(string type, string name) => GameType(type).GetProperty(name).GetValue(null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Members) is PropertyInfo property
            ? property.GetValue(target) : target.GetType().GetField(name, Members).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Members)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    }
}
