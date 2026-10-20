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

namespace CurtainCall.SpaceLink.Tests
{
    /// <summary>
    /// 2인 FAIL(2026-10-17) 재현: 인간 대포 씬이 열리자마자 호스트가 낙하선 아래의 보통 상태 플레이어를 죽인다.
    /// 원격 소유자의 출발점 순간이동은 늦게 도착하므로 그 사이 호스트에는 이전 공간의 낙하 위치가 남는다.
    /// 실제 LAN 호스트 1명이며, 원격의 "아직 배치 전"은 Entry 배치 완료 표시를 미리 채워 합성한다. 실제 2인 검증이 아니다.
    /// </summary>
    public sealed class PerformanceEntryJudgeTests
    {
        const string CannonPath = "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";
        const string CannonScene = "HumanCannon";
        const string PlayerType = "CurtainCall.Network.PlayerSync.NetworkPlayer";
        const string EntryType = "CurtainCall.SpaceLink.CampaignPerformanceEntry";
        const string CampaignType = "CurtainCall.Campaign.CampaignRun";
        const string RewardsType = "CurtainCall.Reward.PerformanceRewards";
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Vector3 Below = new(0f, -500f, 0f);

        Component session, network, player, mover, campaign, entry;
        readonly List<Action> restore = new();

        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Get(object target, string name) => target.GetType().GetProperty(name, Members) is PropertyInfo p
            ? p.GetValue(target) : target.GetType().GetField(name, Members).GetValue(target);
        static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Members);
            if (property != null) property.SetValue(target, value);
            else target.GetType().GetField(name, Members).SetValue(target, value);
        }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Members)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        static object StaticGet(string type, string name) => GameType(type).GetProperty(name).GetValue(null);
        static object StaticCall(string type, string name, params object[] args) => GameType(type)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
        static object EnumValue(string type, string name) => Enum.Parse(GameType(type), name);
        static IEnumerator WaitUntil(Func<bool> predicate, float seconds, string reason)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(predicate(), reason + " timed out");
        }
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        string State => Get(player, "State").ToString();
        bool LocalPlaced => (bool)Get(entry, "LocalPlaced");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(CannonPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            session = (Component)StaticCall("CurtainCall.Network.Session.NetworkSessionManager", "EnsureExists");
            network = (Component)Get(session, "NetworkManager");
            var hosting = (Task<bool>)Call(session, "HostLanAsync", Type.Missing);
            yield return WaitUntil(() => hosting.IsCompleted, 10f, "LAN 호스트 시작");
            Assert.IsTrue(hosting.GetAwaiter().GetResult());
            yield return WaitUntil(() =>
            {
                player = StaticGet(PlayerType, "Local") as Component;
                return player != null && (bool)Get(player, "IsSpawned") && (int)Get(player, "Slot") >= 0;
            }, 10f, "실제 호스트 NetworkPlayer spawn");
            foreach (string type in new[] { "CurtainCall.Player.PlayerInputReader", "CurtainCall.Player.PlayerController" })
                ((Behaviour)player.GetComponent(GameType(type))).enabled = false;
            mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            // 중력 대신 위치로 낙하 상태를 둔다. 실제 물리 낙하·키 입력이 아니다.
            Set(mover, "Simulated", false);
            object spaces = Get(StaticGet("CurtainCall.Settings.GameSettings", "Base"), "SpaceTransition");
            Override(spaces, "humanCannonScene", CannonScene);
            yield return Frames(2);
            Assert.AreEqual("Normal", State);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Task leave = null;
            try { if (session != null) leave = (Task)Call(session, "LeaveAsync"); } catch { }
            float deadline = Time.realtimeSinceStartup + 5f;
            while (leave != null && !leave.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            if (network != null && (bool)Get(network, "IsListening")) Call(network, "Shutdown", Type.Missing);
            yield return null;
            if (entry != null) Object.Destroy(entry.gameObject);
            yield return null;
            foreach (var action in Enumerable.Reverse(restore)) action();
            restore.Clear();
            foreach (string type in new[] { RewardsType, CampaignType })
                if (StaticGet(type, "Instance") is Component c && c != null) Object.Destroy(c.gameObject);
            foreach (var go in new[] { session, network }.Where(c => c != null).Select(c => c.gameObject).Distinct()) Object.Destroy(go);
            yield return null;
            session = network = player = mover = campaign = entry = null;
        }

        [UnityTest]
        public IEnumerator Managed_HostDoesNotKillPlayerBeforeOwnerPlacementArrives()
        {
            yield return PrepareManagedCannon();
            // 원격 소유자의 순간이동이 아직 오지 않은 상태: 이 화면의 배치를 이미 끝난 것으로 막고 이전 낙하 위치를 둔다.
            Set(entry, "placed", player);
            Teleport(Below);
            yield return Frames(5);
            Assert.AreEqual("Normal", State, "배치 전 낙하 위치로 바로 사망 판정하면 안 된다(2인 FAIL의 P2).");

            // 소유자 순간이동 도착 뒤에는 기존 낙하 사망 판정이 그대로 동작한다.
            SpawnPose(out var spawn, out var rotation);
            Call(mover, "SetStartPose", spawn, rotation, true);
            yield return Frames(3);
            Assert.AreEqual("Normal", State);
            Teleport(Below);
            yield return WaitUntil(() => State == "Fallen", 3f, "출발점 확인 후 기존 낙하 사망");
        }

        [UnityTest]
        public IEnumerator Managed_FallenAtEntryIsPlacedAfterLaterRevive()
        {
            // 1명 LAN에서는 공연 중 사망이 곧 전멸 정산이므로, 원격이 "공연 상태는 받았지만 부활은 아직" 받은 순서를
            // 도입 시간대의 배치 함수 직접 호출로 합성한다. 매 프레임 재시도(Update)는 같은 함수를 부른다.
            yield return PrepareManagedCannon(false);
            Teleport(Below);
            Call(player, "ServerSetState", EnumValue("CurtainCall.Network.PlayerSync.PlayerState", "Fallen"));
            yield return Frames(2);
            Call(entry, "Place", player);
            Assert.IsFalse(LocalPlaced, "사망 상태라 옮기지 않았으면 배치 완료로 표시하지 않는다.");
            Assert.IsTrue((bool)Call(player, "ServerRevive"));
            yield return Frames(2);
            Call(entry, "Place", player);
            SpawnPose(out var spawn, out _);
            Assert.IsTrue(LocalPlaced, "부활 후 배치 재시도");
            Assert.AreEqual("Normal", State);
            Assert.That(Vector3.Distance(player.transform.position, spawn), Is.LessThan(0.3f));
        }

        [UnityTest]
        public IEnumerator Managed_RevivedAtNightCannotDieInPreviousCannonScene()
        {
            yield return PrepareManagedCannon();
            yield return WaitUntil(() => LocalPlaced, 3f, "인간대포 출발점 배치");
            yield return Frames(3);
            Teleport(Below);
            Assert.IsTrue((bool)Call(player, "ServerKill"), "공연 중 낙하 사망은 허용한다.");
            Assert.AreEqual("Fallen", State);

            // 씬 전환보다 먼저 오는 밤 정비를 합성한다. 옛 씬과 낙하 위치는 그대로 남긴다.
            object night = Get(campaign, "State");
            Set(night, "Phase", EnumValue("CurtainCall.Campaign.CampaignPhase", "Night"));
            Set(night, "PerformanceIndex", 1);
            Call(campaign, "Apply", night);
            Assert.AreEqual("Normal", State, "밤 정비 시작 시 외팔로 부활한다.");
            Assert.IsFalse((bool)Call(player, "ServerKill"), "공연 종료 뒤 같은 프레임의 옛 함정 피해도 거부한다.");
            yield return Frames(5);
            Assert.AreEqual("Normal", State, "대기실 배치 전 옛 씬의 낙하 판정으로 다시 죽으면 안 된다.");
            var balance = player.GetComponent(GameType("CurtainCall.Player.PlayerBalance"));
            Assert.IsFalse((bool)Get(balance, "HasFallen"));
            Assert.IsTrue((bool)Get(mover, "ControlEnabled"));
            Assert.AreEqual(GameType("CurtainCall.Player.PlayerCondition").GetField("OneArmParts").GetValue(null), Get(player, "LostParts"));
        }

        [UnityTest]
        public IEnumerator Standalone_CannonStillKillsPlayerBelowFallHeight()
        {
            Assert.IsNull(StaticGet(PlayerType, "ServerCanChangeState"), "단독 실행은 연결 판정 잠금이 없다.");
            Teleport(Below);
            yield return WaitUntil(() => State == "Fallen", 3f, "단독 대포의 기존 낙하 사망");
        }

        IEnumerator PrepareManagedCannon(bool performance = true)
        {
            campaign = (Component)StaticCall(CampaignType, "EnsureExists");
            yield return null;
            // 최소 인원을 우회하는 테스트 전용 RunState. 접속 명부는 LAN 호스트 1명 그대로다.
            object host = Activator.CreateInstance(GameType("CurtainCall.Campaign.RunState"));
            Call(host, "StartSession");
            Set(campaign, "host", host);
            Set(campaign, "offlineRun", false);
            Call(campaign, "Apply", Get(host, "Snapshot"));
            if (performance)
            {
                Assert.IsTrue((bool)Call(host, "EndIntro"));
                object snapshot = Get(host, "Snapshot");
                Set(snapshot, "TrickId", "HumanCannon");
                Set(host, "s", snapshot);
                Call(campaign, "Apply", snapshot);
                Assert.AreEqual("Performance", Get(Get(campaign, "State"), "Phase").ToString());
            }
            Set(session, "<GameState>k__BackingField", EnumValue("CurtainCall.Network.Session.GameSessionState", "Playing"));
            yield return Frames(3); // 공연 시작 시간대 규칙(InjuryRevival)이 먼저 끝나게 한다.
            StaticCall(EntryType, "Prepare", SceneManager.GetSceneByName(CannonScene));
            foreach (var root in SceneManager.GetSceneByName(CannonScene).GetRootGameObjects())
                if (root.GetComponentInChildren(GameType(EntryType), true) is Component found) entry = found;
            Assert.IsNotNull(entry);
        }

        void Teleport(Vector3 position) => Call(mover, "SetStartPose", position, Quaternion.identity, true);

        void SpawnPose(out Vector3 position, out Quaternion rotation)
        {
            object[] args = { (int)Get(player, "Slot"), (float)Get(mover, "LaneSpacing"), null, null };
            GameType("CurtainCall.Network.PlayerSync.PlayerSpawnPoints").GetMethod("GetPose").Invoke(null, args);
            position = (Vector3)args[2];
            rotation = (Quaternion)args[3];
        }

        void Override(object target, string field, object value)
        {
            object before = Get(target, field);
            restore.Add(() => Set(target, field, before));
            Set(target, field, value);
        }
    }
}
