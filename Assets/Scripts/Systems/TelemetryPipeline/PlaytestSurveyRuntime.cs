using System;
using System.IO;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Reward;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.UI.PanelsV1;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Each local player opens their own browser; no host-only path and no log dependency.</summary>
    public sealed class PlaytestSurveyRuntime : MonoBehaviour
    {
        static PlaytestSurveyRuntime instance;
        SurveyLaunchCache cache;
        PlaytestBuildTags configuration;
        CurtainCallMenuPanels menu;
        NetworkSessionManager session;
        string currentRun, currentIdentity, pendingCode, pendingIdentity;
        int currentSlot = -1;
        float pendingUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { instance = null; SceneManager.sceneLoaded -= SceneLoaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Watch() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (instance != null || !PlaytestBuildTags.Current().AllowsSurveys(Application.version)) return;
            if (TelemetryRuntime.SceneType(scene.name) == "UNKNOWN") return;
            var go = new GameObject(nameof(PlaytestSurveyRuntime)); DontDestroyOnLoad(go); go.AddComponent<PlaytestSurveyRuntime>();
        }
        void Awake()
        {
            configuration = PlaytestBuildTags.Current();
            if (!configuration.AllowsSurveys(Application.version)) { Destroy(gameObject); return; }
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            string root = TelemetryRuntime.StorageDirectory(Application.persistentDataPath, Application.dataPath);
            cache = new SurveyLaunchCache(Path.Combine(root, "SurveyCache"));
        }
        string EnvironmentName => Application.isEditor || Debug.isDebugBuild ? "TEST" : "LIVE";
        void Update()
        {
            var nextSession = NetworkSessionManager.Instance;
            if (session != nextSession)
            {
                if (session != null) session.Disconnected -= Disconnected;
                session = nextSession;
                if (session != null) session.Disconnected += Disconnected;
            }
            var next = CommonMenuUI.Current != null ? CommonMenuUI.Current.Menu : null;
            if (menu != next) { if (menu != null) menu.OnLeaveConfirmed.RemoveListener(LeaveConfirmed); menu = next; if (menu != null) menu.OnLeaveConfirmed.AddListener(LeaveConfirmed); }
            if (pendingCode != null)
            {
                if (SceneManager.GetSceneByName("Start").isLoaded && (session == null || session.ConnectionState == SessionConnectionState.Offline))
                { string code = pendingCode, identity = pendingIdentity; pendingCode = null; Open(identity, SurveyLaunchPolicy.Exit, code); }
                else if (Time.unscaledTime > pendingUntil) pendingCode = null;
            }
            var campaign = CampaignRun.Instance; var rewards = PerformanceRewards.Instance != null ? PerformanceRewards.Instance.State : null;
            var player = NetworkPlayer.Local;
            if (campaign == null || !campaign.State.IsRunning || rewards == null || player == null || !player.IsSpawned
                || rewards.Generation != campaign.State.Generation || string.IsNullOrEmpty(rewards.SessionId) || player.Slot is < 0 or > 3)
            {
                // NGO may despawn the local player before it announces the host disconnect.
                // Keep the last local run/slot until the room has actually closed.
                if (session == null || session.ConnectionState == SessionConnectionState.Offline)
                { currentRun = null; currentSlot = -1; }
                return;
            }
            currentRun = TelemetryRuntime.SharedRunId(rewards.SessionId, campaign.State.Generation); currentSlot = player.Slot;
            // Anonymous service IDs can change between player processes/builds.
            // persistentDataPath already separates OS users; keep the survey claim stable there.
            currentIdentity ??= SurveyLaunchCache.LocalIdentity;
            var step = campaign.State;
            var night = configuration.Collection.night;
            if (night.Matches(Application.version) && step.Day == night.nightDay && step.Phase == CampaignPhase.Night && SceneManager.GetSceneByName("WaitingRoomBase").isLoaded
                && !cache.WasOpened(currentIdentity, SurveyLaunchPolicy.Night, night))
                Open(currentIdentity, SurveyLaunchPolicy.Night, NewCode(SurveyLaunchPolicy.Night));
        }
        string NewCode(string kind) => SurveyLaunchPolicy.Code(configuration.Collection.For(kind), Application.version, kind, EnvironmentName, currentRun, currentSlot, Guid.NewGuid().ToString("N"), DateTime.UtcNow);
        void LeaveConfirmed()
        {
            // Use the pre-exit snapshot: the menu's earlier listener may already have stopped NGO.
            if (currentRun == null || currentSlot < 0 || pendingCode != null || !configuration.Collection.exit.Matches(Application.version)) return;
            pendingCode = NewCode(SurveyLaunchPolicy.Exit); pendingIdentity = currentIdentity; pendingUntil = Time.unscaledTime + 60;
        }
        void Disconnected(string reason)
        {
            LeaveConfirmed();
            currentRun = null; currentSlot = -1;
        }
        void Open(string identity, string kind, string code)
        {
            try
            {
                var survey = configuration.Collection.For(kind);
                if (survey.Matches(Application.version) && !cache.WasOpened(identity, kind, survey))
                    cache.TryOpen(identity, kind, survey, code, Application.OpenURL);
            }
            catch (Exception) { Debug.LogWarning("[Survey] 설문 브라우저를 열지 못했습니다. 다음 해당 시점에 다시 시도합니다."); }
        }
        void OnDestroy()
        {
            if (menu != null) menu.OnLeaveConfirmed.RemoveListener(LeaveConfirmed);
            if (session != null) session.Disconnected -= Disconnected;
            if (instance == this) instance = null;
        }
    }
}
