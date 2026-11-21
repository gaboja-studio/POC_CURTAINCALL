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
        bool controlHeld, initialized;
        readonly System.Collections.Generic.HashSet<NetworkPlayer> prepared = new();
        readonly System.Collections.Generic.HashSet<NetworkPlayer> seenAtStart = new();
        readonly System.Collections.Generic.Dictionary<NetworkPlayer, double> entryGraceEnds = new();
        System.Func<NetworkPlayer, PlayerState, bool> guard, previousGuard;

        public bool LocalPlaced => placed != null && placed == NetworkPlayer.Local && placed.IsSpawned;

        internal bool LocalParticipantReady
        {
            get
            {
                var local = NetworkPlayer.Local;
                return local != null && local.IsSpawned && local.Slot != NetworkPlayer.NoSlot
                    && !local.IsApplyingInitialCondition
                    && (LocalPlaced || local.State is PlayerState.Fallen or PlayerState.Retired);
            }
        }

        // 로비를 건너뛴 치트 세션의 참가자에게는 SpaceLinkDirector가 없을 수 있다.
        // 씬 전환을 결정하는 호스트 관리자와 별개로, 각 화면이 자기 공연 진입을 준비한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterScenePreparation()
        {
            SceneManager.sceneLoaded -= PrepareLoadedScene;
            SceneManager.sceneLoaded += PrepareLoadedScene;
        }

        static void PrepareLoadedScene(Scene scene, LoadSceneMode mode) => Prepare(scene);

        public static void Prepare(Scene scene)
        {
            var session = NetworkSessionManager.Instance;
            var campaign = CampaignRun.Instance;
            if (!scene.isLoaded || session == null || session.GameState != GameSessionState.Playing
                || campaign != null && campaign.IsOfflineRun) return;
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
            entry.InitializeIfReady();
        }

        bool InitializeIfReady()
        {
            if (initialized) return true;
            var session = NetworkSessionManager.Instance;
            campaign = CampaignRun.Instance;
            // 새 세션 직행에서는 씬 로드 뒤에 첫 캠페인 스냅샷을 받는다. 초기화를 버리지 않고 기다린다.
            if (session == null || session.GameState != GameSessionState.Playing
                || campaign == null || campaign.IsOfflineRun || !campaign.State.IsRunning) return false;
            initialized = true;
            if (run != null) run.SetExternallyManaged(true);
            else if (session.IsHost) HoldDeathsUntilPlaced(); // 줄타기는 TightropeRun이 시작 전 판정을 막는다.
            // 줄타기 UI는 보존하고 공용 재시작·접속 테스트 패널만 제한한다.
            // 인간대포 연습 봇은 에디터에서 TestHud.OnEnable이 sceneLoaded보다 먼저 붙이므로 함께 끈다.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour is PlayerCommandDebugHud
                        || behaviour.GetType().Name == "NetworkSessionTestUI"
                        || behaviour.GetType().Name == "HumanCannonTestHud"
                        || behaviour.GetType().Name == "HumanCannonPracticeBot") behaviour.enabled = false;
            }
            if (run != null) gameObject.AddComponent<CampaignTightropeAdapter>().Initialize(run, this);
            return true;
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
            if (!InitializeIfReady() || !IsCurrentPerformance) return; // 클라이언트의 공용 상태 수신이 씬 로드보다 늦을 수 있다.
            var session = NetworkSessionManager.Instance;
            if (session == null || session.GameState != GameSessionState.Playing) return;
            if (run != null && run.State == TightropeRunState.Waiting)
            {
                var course = run.GetComponent<TightropeCourse>();
                course.ApplyStage(course.TestStage > 0 ? course.TestStage : campaign.State.Difficulty);
            }
            if (session.IsHost)
                foreach (var player in NetworkPlayer.All)
                {
                    if (prepared.Add(player) && player.State == PlayerState.Arrived) player.ServerSetState(PlayerState.Normal);
                    if (guard == null || seenAtStart.Contains(player)) continue;
                    if (IsAtStart(player, player.transform.position)) seenAtStart.Add(player);
                    else if (player.IsSpawned && player.Slot != NetworkPlayer.NoSlot)
                        entryGraceEnds.TryAdd(player, Time.unscaledTimeAsDouble + GameSettings.Base.SpaceTransition.PerformancePlacementGraceSeconds);
                }
            var local = NetworkPlayer.Local;
            if (local == null || !local.IsSpawned || local.Slot == NetworkPlayer.NoSlot || local.State == PlayerState.Arrived) return;
            if (placed != local) Place(local);
            if (controlHeld && (run == null || run.State == TightropeRunState.Running)) ReleaseControl();
        }

        /// <summary>
        /// 호스트: 원격 소유자의 출발점 순간이동은 씬 로드보다 늦게 온다. 그 전의 이전 공간 위치로 사망 판정하지 않도록
        /// 출발점 도착을 확인하거나 최대 대기 시간이 지나면 사망 판정을 재개한다. 종료 후 옛 씬의 사망은 계속 막는다.
        /// </summary>
        void HoldDeathsUntilPlaced()
        {
            previousGuard = NetworkPlayer.ServerCanChangeState;
            guard = (player, next) => (previousGuard == null || previousGuard(player, next))
                && (!this || !isActiveAndEnabled || gameObject.scene != SceneManager.GetActiveScene()
                    || next != PlayerState.Fallen || IsCurrentPerformance && (seenAtStart.Contains(player)
                        || entryGraceEnds.TryGetValue(player, out double until) && Time.unscaledTimeAsDouble >= until));
            NetworkPlayer.ServerCanChangeState = guard;
        }

        internal static void ConfirmPlayerPlacement(NetworkPlayer player, Vector3 feet)
        {
            var entry = FindFirstObjectByType<CampaignPerformanceEntry>();
            if (entry != null && entry.guard != null && entry.IsCurrentPerformance && IsAtStart(player, feet))
                entry.seenAtStart.Add(player);
        }

        static bool IsAtStart(NetworkPlayer player, Vector3 feet)
        {
            if (player.Slot == NetworkPlayer.NoSlot || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover playerMover)) return false;
            PlayerSpawnPoints.GetPose(player.Slot, playerMover.LaneSpacing, out var position, out _);
            return Vector3.Distance(feet, position) <= 1.5f;
        }

        void Place(NetworkPlayer player)
        {
            // 실제로 출발점에 옮긴 뒤에만 배치 완료로 본다. 사망 상태로 들어왔으면 부활 뒤 다시 시도한다.
            if (player.State != PlayerState.Normal || !player.TryGetComponent(out mover)) return;
            placed = player;
            controller = player.GetComponent<PlayerController>();
            previousScheme = controller != null && controller.HasAssignedScheme ? controller.Scheme : null;
            previousFreeMovement = mover.FreeMovement;
            if (run != null)
            {
                var pose = run.GetComponent<TightropeCourse>().GetStartPose(player.Slot);
                if (player.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
                mover.SetStartPose(pose.position, pose.rotation);
            }
            else
            {
                PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out var position, out var rotation);
                mover.SetStartPose(position, rotation);
            }
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
            if (!initialized) return;
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
