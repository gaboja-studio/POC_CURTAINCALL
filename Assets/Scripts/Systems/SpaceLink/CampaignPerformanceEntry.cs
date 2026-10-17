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
            // 줄타기 UI는 보존하고 공용 재시작·접속 테스트 패널만 제한한다.
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour is PlayerCommandDebugHud
                        || behaviour.GetType().Name == "NetworkSessionTestUI"
                        || behaviour.GetType().Name == "HumanCannonTestHud") behaviour.enabled = false;
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
                    if (prepared.Add(player) && player.State == PlayerState.Arrived) player.ServerSetState(PlayerState.Normal);
            var local = NetworkPlayer.Local;
            if (local == null || !local.IsSpawned || local.Slot == NetworkPlayer.NoSlot || local.State == PlayerState.Arrived) return;
            if (placed != local) Place(local);
            if (controlHeld && (run == null || run.State == TightropeRunState.Running)) ReleaseControl();
        }

        void Place(NetworkPlayer player)
        {
            if (!player.TryGetComponent(out mover)) return;
            placed = player;
            controller = player.GetComponent<PlayerController>();
            previousScheme = controller != null && controller.HasAssignedScheme ? controller.Scheme : null;
            previousFreeMovement = mover.FreeMovement;
            if (player.State != PlayerState.Normal) return;
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
            if (run == null) return;
            run.StopExternalPerformance();
            if (controller != null) controller.SetScheme(previousScheme);
            if (mover != null) mover.FreeMovement = previousFreeMovement;
        }
    }
}
