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
    /// <summary>저장된 코스·실제 LAN 호스트 1명의 입력/비행/착지 회귀. 다중 클라이언트 동기화 검사는 아니다.</summary>
    public sealed partial class FinalCannonPlayTests
    {
        const string ScenePath = "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";
        const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Component session, network, player, system, pilot, mover, route, campaign, rewards;
        Behaviour controller, reader;
        bool controllerEnabled, readerEnabled;
        object campaignPhase, latestResult;
        readonly List<(EventInfo Event, object Target, Delegate Handler)> subscriptions = new();
        int restartCount, reviveCount, campaignStepCount, resultCount;
        ulong Actor => (ulong)Get(player, "OwnerClientId");
        bool Flying => (bool)Get(pilot, "IsFlying");
        int SafeLandings => (int)Get(system, "LocalSafeLandingCount");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            restartCount = reviveCount = campaignStepCount = resultCount = 0;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            session = Singleton("CurtainCall.Network.Session.NetworkSessionManager");
            Assert.IsNotNull(session, "저장된 테스트 씬이 세션 프리팹을 생성해야 합니다.");
            network = (Component)Get(session, "NetworkManager");
            var host = (Task<bool>)Call(session, "HostLanAsync", Type.Missing);
            yield return WaitUntil(() => host.IsCompleted, 10, "LAN host startup");
            Assert.IsTrue(host.GetAwaiter().GetResult(), "기본 LAN 포트로 실제 호스트 시작");
            yield return WaitUntil(() =>
            {
                player = StaticGet("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local") as Component;
                return player != null && (bool)Get(player, "IsSpawned")
                    && (pilot = player.GetComponent(GameType("CurtainCall.HumanCannon.HumanCannonPilot"))) != null;
            }, 10, "local NetworkPlayer and cannon pilot");
            system = Object.FindAnyObjectByType(GameType("CurtainCall.HumanCannon.HumanCannonSystem")) as Component;
            route = Object.FindAnyObjectByType(GameType("CurtainCall.HumanCannon.HumanCannonRouteSystem")) as Component;
            mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            controller = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerController"));
            reader = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"));
            Assert.IsNotNull(system); Assert.IsNotNull(route); Assert.IsNotNull(mover);
            controllerEnabled = controller.enabled; readerEnabled = reader.enabled;
            controller.enabled = reader.enabled = false;
            yield return WaitUntil(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 5, "input release gate");
            Assert.AreEqual(10, Get(system, "StationCount"));
            Assert.AreEqual("Waiting", Get(session, "GameState").ToString(), "최소 시작 인원이나 DevMode를 바꾸지 않는다.");
            Assert.AreEqual(0, Convert.ToInt32(Get(player, "LostParts")));
            ObservePlayerAndResults();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in subscriptions) item.Event.RemoveEventHandler(item.Target, item.Handler);
            subscriptions.Clear();
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
            // 테스트가 연 씬과 함께 생성된 영속 관리자만 정리한다. 설정·EditorPrefs·에셋은 저장하지 않는다.
            var managers = new[] { Singleton("CurtainCall.Reward.PerformanceRewards"),
                Singleton("CurtainCall.Campaign.CampaignRun"), session, network };
            foreach (var go in managers.Where(c => c != null).Select(c => c.gameObject).Distinct()) Object.Destroy(go);
            yield return null;
            session = network = player = system = pilot = mover = route = campaign = rewards = null;
            controller = reader = null;
            if (cleanupError != null) throw cleanupError;
            if (leave != null && leave.IsFaulted) throw leave.Exception;
        }

        [UnityTest]
        public IEnumerator EveryCannon_BoardsAndCancelsIndependentlyWhileShelterLiftWaits()
        {
            Call(player, "ServerSetOneArm");
            yield return null;
            for (int index = 0; index < 10; index++)
            {
                Assert.AreEqual("Waiting", Get(Call(route, "TransportState", 1), "Phase").ToString());
                var station = (Component)Call(system, "Station", index);
                var art = station.transform.Find("CC_PropArt");
                Assert.IsNotNull(art, station.name + " missing applied cannon art");
                var visible = art.GetComponentsInChildren<MeshFilter>();
                Assert.That(visible.Length, Is.GreaterThan(0), station.name + " must have a visible cannon model");
                foreach (var filter in visible)
                {
                    Assert.IsNotNull(filter.sharedMesh, station.name + " missing cannon mesh at runtime");
                    Assert.IsTrue(filter.GetComponent<Renderer>().enabled, station.name + " cannon renderer disabled");
                }
                yield return Board(index);
                yield return ReleaseThenArm();
                Input(held: true, progress: 1);
                Assert.IsFalse((bool)Call(system, "IsSeated", Actor), "긴 F는 현재 좌석만 취소한다: " + index);
                Assert.IsFalse(Flying);
                yield return null;
            }
            Assert.AreEqual(0, SafeLandings);
            AssertFiresIdle();
            AssertNoRecoveryOrResult();
        }

        [UnityTest]
        public IEnumerator FourFinalFlights_RequireNewFAndPreserveHealthyLeglessAndOneArmBodies()
        {
            // 이 검사는 대포별 탄도·부상 보존을 비교한다. 톱 피해는 별도 실제 접촉 검사에서 다룬다.
            var saws = Object.FindAnyObjectByType(GameType("CurtainCall.HumanCannon.HumanCannonFinalSaws"));
            saws.GetType().GetField("warningSeconds", InstanceMembers).SetValue(saws, 120f);
            int[] expectedParts = { 0, 4, 12, 13 }; // 온전함 → 왼다리 없음 → 두 다리 없음 → 오른팔만 남음.
            for (int i = 0; i < 4; i++)
            {
                if (i > 0) Assert.AreNotEqual(0, Convert.ToInt32(Call(player, "ServerCut", 1)), "호스트 절단이 적용되어야 한다.");
                Assert.AreEqual(expectedParts[i], Convert.ToInt32(Get(player, "LostParts")));
                yield return Board(6 + i);
                // 탑승을 일으킨 F와 그 후 아직 놓지 않은 F 모두 발사/취소로 재사용하지 않는다.
                Input(held: true, progress: 1); yield return null;
                Input(tapped: true); yield return null;
                Assert.AreEqual(Actor, Call(system, "Occupant", 6 + i));
                Assert.IsFalse(Flying);
                yield return ReleaseThenArm();
                int before = SafeLandings;
                Input(tapped: true);
                Assert.IsTrue(Flying, "놓은 뒤 새 F로 발사한다.");
                Assert.AreEqual(ulong.MaxValue, Call(system, "Occupant", 6 + i));
                Assert.That(Vector3.Distance((Vector3)Get(pilot, "velocity"), new Vector3(0, 12, 8)), Is.LessThan(0.001f));
                Assert.AreEqual(4.5f, Get(pilot, "airControlSpeed"));
                Vector3 start = player.transform.position;
                float highest = start.y;
                float deadline = Time.realtimeSinceStartup + 8;
                while (Flying && Time.realtimeSinceStartup < deadline)
                {
                    AssertBody(expectedParts[i]);
                    highest = Mathf.Max(highest, player.transform.position.y);
                    yield return null;
                }
                Assert.IsFalse(Flying, "실제 프레임의 비행이 제한 시간 안에 착지해야 한다.");
                Assert.That(highest, Is.GreaterThan(start.y + 0.5f));
                Assert.That(player.transform.position.z, Is.GreaterThan(start.z + 3f));
                Assert.AreEqual(before + 1, SafeLandings);
                var pad = GameObject.Find("FinalCannonCourse/FlightLandingPad").GetComponent(GameType("CurtainCall.HumanCannon.HumanCannonLandingZone"));
                Assert.IsTrue((bool)Call(pad, "SupportsLanding", player.transform.position));
                AssertBody(expectedParts[i]);
                AssertFiresIdle();
                AssertNoRecoveryOrResult();
            }
        }

        [UnityTest]
        public IEnumerator OriginalCushion_EndsActualFallWithoutHealingRevivingRestartingOrResult()
        {
            Call(player, "ServerSetOneArm");
            AssertBody(13);
            yield return Board(6);
            yield return ReleaseThenArm();
            Input(tapped: true);
            Assert.IsTrue(Flying);
            int before = SafeLandings;
            var cushion = GameObject.Find("LandingCushion").GetComponent<BoxCollider>();
            Vector3 drop = new Vector3(cushion.bounds.center.x, cushion.bounds.max.y + 3, cushion.bounds.center.z);
            // 호스트가 승인한 실제 비행 식별자를 유지하고 낙하 시작점만 준비한다. 착지 판정은 호출하지 않는다.
            Call(pilot, "Launch", 6, Get(pilot, "launchRevision"), drop, Quaternion.identity, Vector3.down * 2);
            float deadline = Time.realtimeSinceStartup + 5;
            while (Flying && Time.realtimeSinceStartup < deadline)
            {
                AssertBody(13);
                yield return null;
            }
            Assert.IsFalse(Flying);
            Assert.That(player.transform.position.y, Is.LessThan(drop.y - 2));
            Assert.AreEqual(before + 1, SafeLandings);
            var zone = cushion.GetComponent(GameType("CurtainCall.HumanCannon.HumanCannonLandingZone"));
            Assert.IsTrue((bool)Call(zone, "SupportsLanding", player.transform.position));
            yield return null;
            AssertBody(13);
            AssertFiresIdle();
            AssertNoRecoveryOrResult();
        }

        IEnumerator Board(int index)
        {
            var station = (Component)Call(system, "Station", index);
            var boarding = (Transform)Get(station, "BoardingPoint");
            Call(mover, "SetStartPose", boarding.position, Quaternion.identity, true);
            yield return WaitUntil(() => ((CharacterController)Get(mover, "Body")).isGrounded
                && !(bool)Get(player, "IsAirborne"), 3, "탑승 위치로 옮긴 뒤 실제 바닥 접촉과 공유 공중 상태 해제");
            Assert.AreEqual(index, Call(system, "FindBoardableStation", player));
            Input(tapped: true);
            yield return WaitUntil(() => (ulong)Call(system, "Occupant", index) == Actor, 2, "부상별 탑승 시간 뒤 호스트 확정");
            Assert.AreEqual(Actor, Call(system, "Occupant", index));
            Assert.IsFalse(Flying, "첫 F는 탑승만 한다.");
            yield return null;
        }

        IEnumerator ReleaseThenArm()
        {
            Input(); yield return null;
            Input(); yield return null;
        }

        void Input(bool tapped = false, bool held = false, float progress = 0)
        {
            object frame = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerInputFrame"));
            frame.GetType().GetField("InteractTapped").SetValue(frame, tapped);
            frame.GetType().GetField("InteractLongPressed").SetValue(frame, held);
            frame.GetType().GetField("InteractHoldProgress").SetValue(frame, progress);
            Call(pilot, "ReadInput", frame);
        }

        void AssertBody(int parts)
        {
            Assert.AreEqual("Normal", Get(player, "State").ToString());
            Assert.AreEqual(parts, Convert.ToInt32(Get(player, "LostParts")), "비행·안전 착지는 부상을 복구하지 않는다.");
        }

        void AssertFiresIdle()
        {
            foreach (Component fire in Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonFireWall"), FindObjectsSortMode.None))
                Assert.IsFalse((bool)Get(fire, "Armed"));
        }

        void ObservePlayerAndResults()
        {
            var playerType = GameType("CurtainCall.Network.PlayerSync.NetworkPlayer");
            Subscribe(playerType, null, "Restarted", nameof(Restarted));
            Subscribe(playerType, null, "Revived", nameof(Revived));
            campaign = Singleton("CurtainCall.Campaign.CampaignRun");
            rewards = Singleton("CurtainCall.Reward.PerformanceRewards");
            if (campaign != null)
            {
                campaignPhase = Get(Get(campaign, "State"), "Phase");
                Subscribe(campaign.GetType(), campaign, "StepChanged", nameof(CampaignStepped));
            }
            if (rewards != null)
            {
                latestResult = Get(rewards, "Latest");
                Subscribe(rewards.GetType(), rewards, "ResultConfirmed", nameof(ResultConfirmed));
            }
        }

        void AssertNoRecoveryOrResult()
        {
            Assert.AreEqual(0, restartCount); Assert.AreEqual(0, reviveCount);
            Assert.AreEqual(0, campaignStepCount); Assert.AreEqual(0, resultCount);
            Assert.AreSame(campaign, Singleton("CurtainCall.Campaign.CampaignRun"));
            Assert.AreSame(rewards, Singleton("CurtainCall.Reward.PerformanceRewards"));
            if (campaign != null) Assert.AreEqual(campaignPhase, Get(Get(campaign, "State"), "Phase"));
            if (rewards != null) Assert.AreSame(latestResult, Get(rewards, "Latest"));
            Assert.AreEqual("Waiting", Get(session, "GameState").ToString());
        }

        void Restarted<T>(T value) => restartCount++;
        void Revived<T>(T value) => reviveCount++;
        void CampaignStepped<T>(T value) => campaignStepCount++;
        void ResultConfirmed<T>(T value) => resultCount++;

        void Subscribe(Type owner, object target, string name, string callback)
        {
            var info = owner.GetEvent(name);
            var method = GetType().GetMethod(callback, InstanceMembers).MakeGenericMethod(info.EventHandlerType.GenericTypeArguments[0]);
            var handler = Delegate.CreateDelegate(info.EventHandlerType, this, method);
            info.AddEventHandler(target, handler);
            subscriptions.Add((info, target, handler));
        }

        static IEnumerator WaitUntil(Func<bool> predicate, float seconds, string reason)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(predicate(), reason + " timed out");
        }

        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static Component Singleton(string type) => StaticGet(type, "Instance") as Component;
        static object StaticGet(string type, string name) => GameType(type).GetProperty(name).GetValue(null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, InstanceMembers) is PropertyInfo property
            ? property.GetValue(target) : target.GetType().GetField(name, InstanceMembers).GetValue(target);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(InstanceMembers)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    }
}
