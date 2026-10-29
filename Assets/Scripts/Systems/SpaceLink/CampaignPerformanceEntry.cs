using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.SpaceLink
{
    /// <summary>공용 런으로 로드한 공연에만 임시 진입 정책을 붙인다. 원본 씬은 변경하지 않는다.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CampaignPerformanceEntry : MonoBehaviour
    {
        string trickId;
        CampaignRun campaign;
        TightropeRun run;
        NetworkPlayer placed;
        PlayerMover mover;
        PlayerController controller;
        PlayerControlScheme previousScheme;
        bool previousFreeMovement;
        bool controlHeld;
        readonly System.Collections.Generic.HashSet<NetworkPlayer> prepared = new();
        readonly System.Collections.Generic.HashSet<NetworkPlayer> seenAtStart = new();
        System.Func<NetworkPlayer, PlayerState, bool> guard, previousGuard;

        public bool LocalPlaced => placed != null;

        public static void Prepare(Scene scene)
        {
            var session = NetworkSessionManager.Instance;
            var campaign = CampaignRun.Instance;
            if (!scene.isLoaded || session == null || session.GameState != GameSessionState.Playing
                || campaign == null || campaign.IsOfflineRun || !campaign.State.IsRunning) return;
            var settings = GameSettings.Base.SpaceTransition;
            string trick = scene.name == settings.TightropeScene ? "Tightrope"
                : scene.name == settings.HumanCannonScene ? "HumanCannon" : null;
            if (trick == null) return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<CampaignPerformanceEntry>(true) != null) return;

            var go = new GameObject(nameof(CampaignPerformanceEntry));
            SceneManager.MoveGameObjectToScene(go, scene);
            var entry = go.AddComponent<CampaignPerformanceEntry>();
            entry.trickId = trick;
            entry.campaign = campaign;
            entry.run = FindInScene<TightropeRun>(scene);
            if (entry.run != null) entry.run.SetExternallyManaged(true);
            else if (session.IsHost) entry.HoldDeathsUntilPlaced(); // 줄타기는 TightropeRun이 시작 전 판정을 막는다.
            // 줄타기 UI는 보존하고 공용 재시작·접속 테스트 패널만 제한한다.
            // 인간대포 연습 봇은 에디터에서 TestHud.OnEnable이 sceneLoaded보다 먼저 붙이므로 함께 끈다.
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour is PlayerCommandDebugHud
                        || behaviour.GetType().Name == "NetworkSessionTestUI"
                        || behaviour.GetType().Name == "HumanCannonTestHud"
                        || behaviour.GetType().Name == "HumanCannonPracticeBot") behaviour.enabled = false;
            }
            if (entry.run != null) go.AddComponent<CampaignTightropeAdapter>().Initialize(entry.run, entry);
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        public bool IsCurrentPerformance => campaign != null && campaign.State.Phase == CampaignPhase.Performance
            && campaign.State.TrickId == trickId;

        void Update()
        {
            if (!IsCurrentPerformance) return; // 클라이언트의 공용 상태 수신이 씬 로드보다 늦을 수 있다.
            var session = NetworkSessionManager.Instance;
            if (session == null || session.GameState != GameSessionState.Playing) return;
            if (session.IsHost)
                foreach (var player in NetworkPlayer.All)
                {
                    if (prepared.Add(player) && player.State == PlayerState.Arrived) player.ServerSetState(PlayerState.Normal);
                    if (guard != null && IsAtStart(player)) seenAtStart.Add(player);
                }
            var local = NetworkPlayer.Local;
            if (local == null || !local.IsSpawned || local.Slot == NetworkPlayer.NoSlot || local.State == PlayerState.Arrived) return;
            if (placed != local) Place(local);
            if (controlHeld && (run == null || run.State == TightropeRunState.Running)) ReleaseControl();
        }

        /// <summary>
        /// 호스트: 원격 소유자의 출발점 순간이동은 씬 로드보다 늦게 온다. 그 전의 이전 공간 위치로 사망 판정하지 않도록
        /// 해당 공연 중 출발점에서 한 번 확인된 플레이어만 사망을 받는다. 종료 후 부활자가 옛 씬에서 다시 죽는 것도 막는다.
        /// </summary>
        void HoldDeathsUntilPlaced()
        {
            previousGuard = NetworkPlayer.ServerCanChangeState;
            guard = (player, next) => (next != PlayerState.Fallen || (IsCurrentPerformance && seenAtStart.Contains(player)))
                && (previousGuard == null || previousGuard(player, next));
            NetworkPlayer.ServerCanChangeState = guard;
        }

        static bool IsAtStart(NetworkPlayer player)
        {
            if (player.Slot == NetworkPlayer.NoSlot || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover playerMover)) return false;
            PlayerSpawnPoints.GetPose(player.Slot, playerMover.LaneSpacing, out var position, out _);
            return Vector3.Distance(player.transform.position, position) <= 1.5f;
        }

        void Place(NetworkPlayer player)
        {
            // 실제로 출발점에 옮긴 뒤에만 배치 완료로 본다. 사망 상태로 들어왔으면 부활 뒤 다시 시도한다.
            if (player.State != PlayerState.Normal || !player.TryGetComponent(out mover)) return;
            placed = player;
            controller = player.GetComponent<PlayerController>();
            previousScheme = controller != null && controller.HasAssignedScheme ? controller.Scheme : null;
            previousFreeMovement = mover.FreeMovement;
            PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out var position, out var rotation);
            if (run != null && player.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
            mover.SetStartPose(position, rotation);
            mover.PassThrough = false;
            if (run == null) return;
            mover.SetControlEnabled(false);
            controlHeld = true;
        }

        void ReleaseControl()
        {
            controlHeld = false;
            if (mover != null && placed != null && placed.State == PlayerState.Normal) mover.SetControlEnabled(true);
        }

        void OnDisable()
        {
            ReleaseControl();
            if (guard != null && NetworkPlayer.ServerCanChangeState == guard) NetworkPlayer.ServerCanChangeState = previousGuard;
            guard = null;
            if (run == null) return;
            run.StopExternalPerformance();
            if (controller != null) controller.SetScheme(previousScheme);
            if (mover != null) mover.FreeMovement = previousFreeMovement;
        }
    }
}
