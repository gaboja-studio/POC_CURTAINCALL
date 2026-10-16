using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    /// <summary>저장된 옆 코스의 격리와 실제 1인 호스트 구간 전환. 협동 플레이 검증을 대신하지 않는다.</summary>
    public sealed partial class SoloPracticePlayTests
    {
        const string ScenePath = "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Prefix = "CurtainCall.HumanCannon.";
        Component practice, session, network, player;
        GameObject template;
        GameObject[] originals;
        Vector3[] originalPositions;
        Behaviour controller, reader;
        bool controllerEnabled, readerEnabled;
        GameObject Runtime => (GameObject)Get(practice, "PracticeInstance");
        ulong Actor => (ulong)Get(player, "OwnerClientId");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            practice = Object.FindAnyObjectByType(GameType(Prefix + "HumanCannonSoloPractice")) as Component;
            Assert.IsNotNull(practice, "저장된 씬에 구간별 연습 컨트롤러가 있어야 한다.");
            originals = (GameObject[])Get(practice, "originalRoots");
            template = (GameObject)Get(practice, "practiceTemplate");
            originalPositions = originals.Select(root => root.transform.position).ToArray();
            Assert.IsFalse(template.activeSelf, "Play 진입 직후 템플릿 시스템을 원본과 동시에 켜지 않는다.");
            Assert.IsFalse((bool)Get(practice, "IsPracticing"));
            session = Singleton("CurtainCall.Network.Session.NetworkSessionManager");
            Assert.IsNotNull(session);
            network = (Component)Get(session, "NetworkManager");
            Assert.IsFalse((bool)Get(network, "IsListening"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Task leave = null;
            Exception cleanupError = null;
            try { if (session != null) leave = (Task)Call(session, "LeaveAsync"); }
            catch (Exception exception) { cleanupError = exception; }
            float deadline = Time.realtimeSinceStartup + 5;
            while (leave != null && !leave.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            try { if (network != null && (bool)Get(network, "IsListening")) Call(network, "Shutdown", Type.Missing); }
            catch (Exception exception) { cleanupError ??= exception; }
            yield return null;
            if (controller != null) controller.enabled = controllerEnabled;
            if (reader != null) reader.enabled = readerEnabled;
            var managers = new[] { Singleton("CurtainCall.Reward.PerformanceRewards"),
                Singleton("CurtainCall.Campaign.CampaignRun"), session, network };
            foreach (var go in managers.Where(c => c != null).Select(c => c.gameObject).Distinct()) Object.Destroy(go);
            yield return null;
            practice = session = network = player = null;
            controller = reader = null;
            if (cleanupError != null) throw cleanupError;
            if (leave != null && leave.IsFaulted) throw leave.Exception;
        }

        [UnityTest]
        public IEnumerator SavedSideCourse_ReferencesItsOwnDevicesAndPreservesOriginalTenCannons()
        {
            var originalObjects = new HashSet<Object>();
            foreach (var root in originals)
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    originalObjects.Add(transform.gameObject);
                    foreach (var component in transform.GetComponents<Component>()) originalObjects.Add(component);
                }
            foreach (var component in template.GetComponentsInChildren<Component>(true))
            {
                Assert.IsNotNull(component, "복사본의 Missing Script");
                using var data = new UnityEditor.SerializedObject(component);
                var property = data.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == UnityEditor.SerializedPropertyType.ObjectReference)
                        Assert.IsFalse(originalObjects.Contains(property.objectReferenceValue),
                            component.name + "." + property.propertyPath + "가 원본 코스를 참조한다.");
            }
            var originalSystem = Current("HumanCannonSystem");
            var copiedSystem = In(template, "HumanCannonSystem");
            Assert.AreEqual(10, Get(originalSystem, "StationCount"));
            Assert.AreEqual(10, Get(copiedSystem, "StationCount"));
            for (int i = 0; i < 10; i++)
            {
                var source = (Component)Call(originalSystem, "Station", i);
                var copy = (Component)Call(copiedSystem, "Station", i);
                Assert.IsTrue(originalObjects.Contains(source));
                Assert.IsTrue(copy.transform.IsChildOf(template.transform));
                Assert.That(Vector3.Distance(source.transform.position + Vector3.right * 50, copy.transform.position), Is.LessThan(0.001f));
                if (i >= 6) Assert.AreEqual("FinalCannon" + (i - 5), source.name);
            }
            AssertOriginalPoseAndActivity(true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllTenEntries_HostSoloAndSwitchDevices_CushionDropKeepsInjury()
        {
            for (int index = 0; index < 10; index++)
            {
                Set(practice, "bodyPreset", index == 8 ? 3 : 0);
                yield return Enter(index);
                AssertRuntimeIsolation(index);
                AssertBody(index == 8 ? 13 : 0);
                if (index == 8)
                {
                    var cannon = Current("HumanCannonSystem");
                    yield return Until(() => (int)Get(cannon, "LocalSafeLandingCount") == 1, 5, "연습 쿠션 실제 낙하 착지");
                    Assert.IsFalse((bool)Get(player.GetComponent(GameType(Prefix + "HumanCannonPilot")), "IsFlying"));
                    var zone = Runtime.transform.Find("LandingCushion").GetComponent(GameType(Prefix + "HumanCannonLandingZone"));
                    Assert.IsTrue((bool)Call(zone, "SupportsLanding", player.transform.position));
                    AssertBody(13);
                }
                else AssertEntryPosition(index);
            }
            Assert.AreEqual("Waiting", Get(session, "GameState").ToString());
        }

        [UnityTest]
        public IEnumerator Retry_ReappliesSelectedOneArmPreset_ThenRestoresOriginalCourseAndPose()
        {
            Set(practice, "bodyPreset", 3);
            yield return Enter(9);
            AssertBody(13);
            Vector3 originalPose = (Vector3)Get(practice, "originalPlayerPosition");
            var previous = Runtime;
            // 연습 도중 몸 상태가 달라져도 재시작은 사용자가 선택한 프리셋으로 돌아간다.
            Call(player, "ServerRestoreAll");
            AssertBody(0);
            yield return Enter(9);
            Assert.IsTrue(previous == null, "재시작은 이전 복사본을 제거해야 한다.");
            AssertBody(13);
            AssertEntryPosition(9);
            Call(practice, "ReturnToCourse");
            yield return Until(() => !(bool)Get(practice, "IsBusy"), 5, "원본 코스 복귀");
            Assert.IsFalse((bool)Get(practice, "IsPracticing"));
            Assert.AreEqual(-1, Get(practice, "SelectedSection"));
            AssertOriginalPoseAndActivity(true);
            Assert.That(Vector3.Distance(originalPose, player.transform.position), Is.LessThan(0.15f));
            foreach (string name in new[] { "HumanCannonSystem", "HumanCannonPoleSystem", "HumanCannonRouteSystem", "HumanCannonTrampoline" })
                Assert.IsTrue(originals.Any(root => Current(name).transform.IsChildOf(root.transform)));
            Assert.IsFalse(template.activeSelf);
            AssertBody(13);
            yield return Until(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 5, "복귀 후 새 입력 가능");
            var mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            object[] spawn = { Convert.ToInt32(Get(player, "Slot")), Get(mover, "LaneSpacing"), null, null };
            GameType("CurtainCall.Network.PlayerSync.PlayerSpawnPoints").GetMethod("GetPose").Invoke(null, spawn);
            Assert.AreEqual((Vector3)spawn[2], (Vector3)Get(mover, "startPosition"), "복귀 후 재시작 위치가 옆 코스에 남으면 안 된다.");
            Call(mover, "ResetToStart");
            Assert.AreEqual((Vector3)spawn[2], player.transform.position);
        }

        [UnityTest]
        public IEnumerator CarrierSections_GrabTrampolineAndUsePartnerHoldThenWarningAndActiveSaw()
        {
            foreach (int index in new[] { 1, 6 })
            {
                yield return Enter(index);
                var trampoline = Current("HumanCannonTrampoline");
                var gate = (Component)Get(practice, "selectedGate");
                var pole = (Component)Get(practice, "selectedPole");
                Assert.IsTrue((bool)Call(trampoline, "CanGrab", player), "선택 위치에서 손잡이를 잡을 수 있어야 한다.");
                Call(trampoline, "RequestGrab");
                Assert.AreEqual(Actor, Get(trampoline, "Carrier"));
                Assert.AreEqual("Active", Get(gate, "Phase").ToString());
                Call(practice, "TogglePartnerHold");
                yield return Until(() => Get(gate, "Phase").ToString() == "Retracted", 2, "봉 도우미 유지");
                Assert.IsFalse((bool)Get(gate, "CarrierPassed"));
                Vector3 crossed = gate.transform.position + gate.transform.forward * 2;
                crossed.y = FloorTop(index);
                Call(player.GetComponent(GameType("CurtainCall.Player.PlayerMover")), "SetStartPose", crossed, Quaternion.identity, true);
                yield return Until(() => (bool)Get(gate, "CarrierPassed"), 2, "실제 운반자 통과 인식");
                Call(practice, "TogglePartnerHold");
                yield return Until(() => Get(gate, "Phase").ToString() == "Warning", 2, "도우미 해제 후 재동작 예고");
                float warning = (float)Get(pole, "WarningSeconds");
                yield return Until(() => Get(gate, "Phase").ToString() == "Active", warning + 2, "예고 종료 후 톱 재동작");
                AssertBody(0);
                Assert.AreEqual(Actor, Get(trampoline, "Carrier"), "section=" + index + " position=" + player.transform.position);
            }
        }

        [UnityTest]
        public IEnumerator ThreeTransportEntries_GrabSupportedTrampolineAndAutomaticallyReachDestination()
        {
            for (int index = 3; index <= 5; index++)
            {
                yield return Enter(index);
                AssertEntryPosition(index);
                var transport = Transport(index);
                var route = Current("HumanCannonRouteSystem");
                var trampoline = Current("HumanCannonTrampoline");
                Assert.IsTrue((bool)Call(trampoline, "CanGrab", player), "진입 직후 손잡이 접근: " + index);
                Call(trampoline, "RequestGrab");
                Assert.AreEqual(Actor, Get(trampoline, "Carrier"));
                Assert.IsTrue((bool)Call(transport, "ContainsLoad", trampoline), "잡은 트램폴린 전체가 발판 안에 있어야 한다: " + index);
                yield return Until(() => (bool)Call(route, "CanStart", player, index - 3), 2, "발판 탑승 조건: " + index);
                yield return Until(() => Get(Call(route, "TransportState", index - 3), "Phase").ToString() == "Moving", 4, "자동 출발: " + index);
                Assert.IsTrue((bool)Call(route, "IsRiding", Actor));
                Assert.IsTrue(IsFreeThirdPerson, "승강기 운반 중 자유 3인칭: " + index);
                float duration = (float)Get(transport, "TravelSeconds");
                yield return Until(() => Get(Call(route, "TransportState", index - 3), "Phase").ToString() == "Arrived", duration + 3, "발판 도착: " + index);
                yield return null; // 도착 프레임 LateUpdate의 마지막 승객 위치 적용을 기다린다.
                Vector3 expected = (Vector3)Call(transport, "Position", 1f)
                    - (Quaternion)Get(transport, "Rotation") * Vector3.forward * 1.2f;
                Assert.That(Vector3.Distance(expected, player.transform.position), Is.LessThan(0.2f), "자동 운송 도착 위치: " + index);
                Assert.IsFalse((bool)Call(route, "IsRiding", Actor));
                Assert.AreEqual(Actor, Get(trampoline, "Carrier"));
                AssertBody(0);
                Assert.IsTrue(IsFreeThirdPerson, "도착 후에도 운반 시점 유지: " + index);
                Call(trampoline, "RequestDrop");
                yield return Until(() => IsFirstPerson, 3, "승강기 도착 후 놓으면 1인칭: " + index);
            }
        }

        IEnumerator Enter(int index)
        {
            Call(practice, "SelectSection", index);
            yield return Until(() => !(bool)Get(practice, "IsBusy"), 15, "구간 " + index + " 준비");
            Assert.IsTrue((bool)Get(practice, "IsPracticing"), (string)Get(practice, "message"));
            Assert.AreEqual(index, Get(practice, "SelectedSection"));
            player = StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local") as Component;
            Assert.IsNotNull(player);
            if (controller == null)
            {
                controller = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerController"));
                reader = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"));
                controllerEnabled = controller.enabled; readerEnabled = reader.enabled;
                controller.enabled = reader.enabled = false;
            }
            Assert.IsTrue((bool)Get(session, "IsHost"));
            Assert.AreEqual("Waiting", Get(session, "GameState").ToString());
            Assert.AreEqual(1, ((IEnumerable)Get(network, "ConnectedClientsIds")).Cast<object>().Count());
            yield return Until(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 5, "입력 재활성화");
        }

        void AssertRuntimeIsolation(int index)
        {
            AssertOriginalPoseAndActivity(false);
            Assert.IsFalse(template.activeSelf);
            foreach (string name in new[] { "HumanCannonSystem", "HumanCannonRouteSystem", "HumanCannonTrampoline" })
                Assert.IsTrue(Current(name).transform.IsChildOf(Runtime.transform), name + "이 활성 복사본에 속해야 한다.");
            if (index is 1 or 6) Assert.IsNull(Current("HumanCannonPoleSystem"));
            else Assert.IsTrue(Current("HumanCannonPoleSystem").transform.IsChildOf(Runtime.transform));
            Assert.AreEqual(10, Get(Current("HumanCannonSystem"), "StationCount"));
        }

        void AssertEntryPosition(int index)
        {
            Vector3 expected;
            if (index is 0 or 9)
                expected = ((Transform)Get(Call(Current("HumanCannonSystem"), "Station", index == 0 ? 0 : 6), "BoardingPoint")).position;
            else if (index is 3 or 4 or 5)
            {
                var transport = Transport(index);
                expected = (Vector3)Get(transport, "StartPosition") - (Quaternion)Get(transport, "Rotation") * Vector3.forward * 1.2f;
            }
            else if (index is 1 or 6)
            {
                var gate = (Component)Get(practice, "selectedGate");
                expected = gate.transform.position - gate.transform.forward * 5;
                expected.y = FloorTop(index);
            }
            else
            {
                expected = (Vector3)Call(Get(practice, "selectedPole"), "HangingPosition", 0f);
                expected.y = FloorTop(index);
            }
            Assert.That(Vector3.Distance(expected, player.transform.position), Is.LessThan(0.2f),
                "구간 진입 위치: " + index + " expected=" + expected + " actual=" + player.transform.position + " preparedFloor=" + Get(practice, "floorY"));
        }

        float FloorTop(int index) => Runtime.transform.Find(index is 6 or 7
            ? "SplitRouteCourse/SecondCourseFloor" : "SplitRouteCourse/FirstCourseFloor")
            .GetComponentsInChildren<BoxCollider>().Where(c => c.enabled && !c.isTrigger).Max(c => c.bounds.max.y);

        Component Transport(int index) => Runtime.transform.Find("SplitRouteCourse/"
            + new[] { "DepartureTransport", "ShelterLift", "ShelterDeparture" }[index - 3]).GetComponent(GameType(Prefix + "HumanCannonTransport"));

        void AssertOriginalPoseAndActivity(bool active)
        {
            for (int i = 0; i < originals.Length; i++)
            {
                Assert.AreEqual(active, originals[i].activeSelf, originals[i].name);
                Assert.AreEqual(originalPositions[i], originals[i].transform.position, originals[i].name + " 원본 위치 보존");
            }
        }

        void AssertBody(int parts)
        {
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.AreEqual(parts, Convert.ToInt32(Get(player, "LostParts")));
        }

        static IEnumerator Until(Func<bool> predicate, float seconds, string reason)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(predicate(), reason + " timed out");
        }

        static Component In(GameObject root, string type) => root.GetComponentInChildren(GameType(Prefix + type), true);
        static Component Current(string type) => StaticGet(Prefix + type, "Current") as Component;
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static Component Singleton(string type) => StaticGet(type, "Instance") as Component;
        static object StaticGet(string type, string name) => GameType(type).GetProperty(name).GetValue(null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Members) is PropertyInfo property
            ? property.GetValue(target) : target.GetType().GetField(name, Members).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Members)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    }
}
