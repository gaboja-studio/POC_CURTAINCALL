using System;
using System.Collections;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.Session;
using CurtainCall.Settings;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.SpaceLink
{
    /// <summary>
    /// 장소 전환. 판정 화면(호스트 또는 오프라인 런)이 <see cref="CampaignRun.StepChanged"/>를 받아 장소를 정하고 공간 씬을 바꾼다.
    /// 접속 중이면 NGO 씬 관리(<see cref="NetworkSceneManager"/>)로 모두 함께 바꾸고, 오프라인이면 Unity <see cref="SceneManager"/>로 바꾼다.
    /// 방식은 공통 세팅 "장소 전환"(통째 교체/추가 로드)을 전환할 때마다 읽는다.
    /// 런·자금·부상 관리자는 이미 씬이 바뀌어도 남으므로(DontDestroyOnLoad) 여기서는 씬만 바꾼다.
    /// </summary>
    public sealed class SpaceLinkDirector : MonoBehaviour
    {
        const int MaxLogLines = 8;

        public static SpaceLinkDirector Instance { get; private set; }

        /// <summary>없으면 만들어 돌려준다(씬이 바뀌어도 유지).</summary>
        public static SpaceLinkDirector EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(SpaceLinkDirector));
            DontDestroyOnLoad(go);
            return go.AddComponent<SpaceLinkDirector>();
        }

        readonly struct TransitionRequest
        {
            public readonly SpaceId Space;
            public readonly string SceneName;
            public readonly bool Reload;
            readonly int generation;
            readonly int day;
            readonly int performanceIndex;
            readonly string trickId;

            public TransitionRequest(SpaceId space, string sceneName, in CampaignSnapshot state, bool reload = false)
            {
                Space = space;
                SceneName = sceneName;
                Reload = reload;
                generation = state.Generation;
                day = state.Day;
                performanceIndex = state.PerformanceIndex;
                trickId = state.TrickId;
            }

            public bool Matches(in CampaignSnapshot state) => Space != SpaceId.Performance
                || (state.Phase == CampaignPhase.Performance && state.Generation == generation && state.Day == day
                    && state.PerformanceIndex == performanceIndex && state.TrickId == trickId);
        }

        readonly List<string> log = new();
        CampaignRun campaign;
        TransitionRequest? pending;
        Coroutine running;
        RestTimerState lastTimer;
        bool stepFailed;

        /// <summary>이 화면이 장소를 정하는지(호스트 또는 오프라인 런).</summary>
        public bool HasAuthority => campaign != null && campaign.HasAuthority;

        /// <summary>장소를 바꾸는 중인지.</summary>
        public bool IsTransitioning => running != null;

        /// <summary>지금 떠 있는 장소(모든 화면이 같게 본다). 공간 씬이 없으면 시작 씬.</summary>
        public SpaceId Current
        {
            get
            {
                var settings = GameSettings.Base.SpaceTransition;
                if (SpaceRoute.TryFind(SceneManager.GetActiveScene().name, settings, out var active) && active != SpaceId.Hub)
                    return active;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && SpaceRoute.TryFind(scene.name, settings, out var space) && space != SpaceId.Hub)
                        return space;
                }
                return SpaceId.Hub;
            }
        }

        /// <summary>최근 전환 기록(디버그 화면용).</summary>
        public IReadOnlyList<string> Log => log;

        /// <summary>장소가 바뀌었다(판정 화면에서만, 전환이 끝난 뒤).</summary>
        public event Action<SpaceId> SpaceChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            campaign = CampaignRun.EnsureExists();
            campaign.StepChanged += HandleStep;
            campaign.Changed += HandleChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            lastTimer = campaign.State.Timer;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (campaign != null)
            {
                campaign.StepChanged -= HandleStep;
                campaign.Changed -= HandleChanged;
            }
            Instance = null;
        }

        // ── 전환 요청 ────────────────────────────────────────

        /// <summary>쉬는 시간에 대기실에서 백스테이지로 나간다(호스트·오프라인 전용, 통째 교체면 모두 함께 이동).</summary>
        public bool LeaveWaitingRoom()
        {
            if (!HasAuthority || campaign.State.Phase != CampaignPhase.RestTime || Current != SpaceId.WaitingRoom) return false;
            campaign.NotifyLeftWaitingRoom(); // A안은 이미 흐르는 중이라 무시돼도 이동은 한다.
            return GameSettings.Base.SpaceTransition.KeepWaitingRoomOnExit || Request(SpaceId.Exploration);
        }

        /// <summary>장소로 바꾼다(호스트·오프라인 전용). 바꾸는 중이면 끝난 뒤 마지막 요청만 처리한다.</summary>
        public bool Request(SpaceId target)
        {
            if (!HasAuthority)
            {
                Debug.LogWarning("[SpaceLink] 장소 전환은 호스트(또는 오프라인 런)에서만 할 수 있습니다.");
                return false;
            }
            Enqueue(target, campaign.State);
            return true;
        }

        /// <summary>새 Dev 시작 지점을 같은 씬에서도 초기화한다. 이 요청만 Single로 로드하며 저장 설정은 바꾸지 않는다.</summary>
        public bool DebugReloadCurrentStep()
        {
            if (!CurtainCall.Dev.DevMode.Enabled || !HasAuthority || !campaign.State.IsRunning) return false;
            var state = campaign.State;
            var target = SpaceRoute.ForPhase(state.Phase);
            string sceneName = SpaceRoute.SceneName(target, GameSettings.Base.SpaceTransition, campaign.IsOfflineRun ? null : state.TrickId);
            pending = new TransitionRequest(target, sceneName, state, true);
            if (running == null) running = StartCoroutine(Run());
            return true;
        }

        void Enqueue(SpaceId target, in CampaignSnapshot state)
        {
            // 오프라인 합성런은 기존 더미를 유지한다. 실제 묘기/단독 연습의 참가자는 LAN 경로로 준비한다.
            string trickId = campaign.IsOfflineRun ? null : state.TrickId;
            string sceneName = SpaceRoute.SceneName(target, GameSettings.Base.SpaceTransition, trickId);
            pending = new TransitionRequest(target, sceneName, state);
            if (running == null) running = StartCoroutine(Run());
        }

        void HandleStep(CampaignSnapshot s)
        {
            if (HasAuthority) Enqueue(SpaceRoute.ForPhase(s.Phase), s);
        }

        void HandleChanged()
        {
            var s = campaign.State;
            // 실제 물리 출구는 타이머만 시작한다. 별도 탐색 씬 전환은 더미 경로에서만 사용한다.
            if (!GameSettings.Base.SpaceTransition.KeepWaitingRoomOnExit
                && HasAuthority && s.Phase == CampaignPhase.RestTime && lastTimer == RestTimerState.WaitingForExit
                && s.Timer == RestTimerState.Running && Current == SpaceId.WaitingRoom && pending == null)
                Request(SpaceId.Exploration);
            lastTimer = s.Timer;
        }

        // ── 전환 실행 ────────────────────────────────────────

        IEnumerator Run()
        {
            // 이동이 필요 없는 요청도 StartCoroutine의 반환값 대입 뒤에 끝나도록 한다.
            yield return null;
            while (pending.HasValue)
            {
                var request = pending.Value;
                pending = null;
                if (!IsCurrent(request)) continue;
                if (request.Reload || request.Space != Current || !IsLoaded(request.SceneName))
                {
                    uint curtainVersion = 0;
                    yield return CurtainCall.UI.SceneIntegration.SceneCurtainTransition.CoverFor(version => curtainVersion = version);
                    try
                    {
                        if (IsCurrent(request) && CurtainCall.UI.SceneIntegration.SceneCurtainTransition.CanTransitionFor(curtainVersion))
                            yield return Transition(request);
                    }
                    finally { CurtainCall.UI.SceneIntegration.SceneCurtainTransition.CompleteFor(curtainVersion); }
                }
            }
            running = null;
        }

        bool IsCurrent(in TransitionRequest request) => HasAuthority && request.Matches(campaign.State);

        IEnumerator Transition(TransitionRequest request)
        {
            var settings = GameSettings.Base.SpaceTransition;
            var mode = request.Reload ? SpaceTransitionMode.Single : settings.Mode;
            var from = Current;
            var target = request.Space;
            string sceneName = request.SceneName;
            stepFailed = false;

            if (mode == SpaceTransitionMode.Single)
            {
                yield return Load(sceneName, LoadSceneMode.Single);
            }
            else
            {
                // 이전 공간을 먼저 닫고 붙인다. 두 공간 씬이 잠깐이라도 함께 떠 있으면 EventSystem 등 씬마다 하나인 것이 겹친다.
                var hub = SceneManager.GetSceneByName(settings.HubScene);
                foreach (var scene in LoadedSpaceScenes(settings))
                {
                    if (scene.name == sceneName) continue;
                    if (hub.isLoaded && SceneManager.GetActiveScene() == scene) SceneManager.SetActiveScene(hub);
                    yield return Unload(scene);
                    if (stepFailed || !IsCurrent(request)) break;
                }
                if (!stepFailed && IsCurrent(request) && !IsLoaded(sceneName))
                    yield return Load(sceneName, LoadSceneMode.Additive);
            }

            if (stepFailed)
            {
                AddLog($"실패: {SpaceRoute.Describe(from)} → {SpaceRoute.Describe(target)} ({sceneName})");
                yield break;
            }
            if (!IsCurrent(request))
            {
                AddLog($"폐기: 지난 공연 요청 ({sceneName})");
                yield break;
            }
            AddLog($"{SpaceRoute.Describe(mode)}: {SpaceRoute.Describe(from)} → {SpaceRoute.Describe(target)} ({sceneName})");
            SpaceChanged?.Invoke(target);
        }

        IEnumerator Load(string sceneName, LoadSceneMode mode)
        {
            var network = OnlineSceneManager();
            if (network == null)
            {
                AsyncOperation op = null;
                try { op = SceneManager.LoadSceneAsync(sceneName, mode); }
                catch (Exception exception) { Fail($"{sceneName} 씬 로드 실패: {exception.Message}"); }
                if (op == null)
                {
                    if (!stepFailed) Fail($"{sceneName} 씬을 열 수 없습니다(빌드 목록 확인).");
                    yield break;
                }
                while (!op.isDone) yield return null;
                if (!IsLoaded(sceneName)) Fail($"{sceneName} 씬 로드가 완료되지 않았습니다.");
                yield break;
            }

            bool done = false;
            void Completed(string scene, LoadSceneMode loadMode, List<ulong> completed, List<ulong> timedOut)
            {
                if (scene != sceneName || loadMode != mode) return;
                done = true;
                if (timedOut.Count > 0) Fail($"{sceneName} 로드 시간 초과: {string.Join(", ", timedOut)}");
            }

            network.OnLoadEventCompleted += Completed;
            var status = network.LoadScene(sceneName, mode);
            if (status != SceneEventProgressStatus.Started)
            {
                network.OnLoadEventCompleted -= Completed;
                Fail($"NGO {sceneName} 로드 실패: {status}");
                yield break;
            }
            while (!done && OnlineSceneManager() == network) yield return null;
            network.OnLoadEventCompleted -= Completed;
            if (!done) Fail($"{sceneName} 로드 중 접속이 끊겼습니다.");
            else if (!stepFailed && !IsLoaded(sceneName)) Fail($"{sceneName} 씬 로드가 완료되지 않았습니다.");
        }

        IEnumerator Unload(Scene scene)
        {
            string sceneName = scene.name;
            var network = OnlineSceneManager();
            if (network == null)
            {
                AsyncOperation op = null;
                try { op = SceneManager.UnloadSceneAsync(scene); }
                catch (Exception exception) { Fail($"{sceneName} 씬 닫기 실패: {exception.Message}"); }
                if (op == null)
                {
                    if (!stepFailed) Fail($"{sceneName} 씬을 닫을 수 없습니다.");
                    yield break;
                }
                while (!op.isDone) yield return null;
                if (scene.isLoaded) Fail($"{sceneName} 씬 닫기가 완료되지 않았습니다.");
                yield break;
            }

            bool done = false;
            void Completed(string name, LoadSceneMode loadMode, List<ulong> completed, List<ulong> timedOut)
            {
                if (name != sceneName) return;
                done = true;
                if (timedOut.Count > 0) Fail($"{sceneName} 닫기 시간 초과: {string.Join(", ", timedOut)}");
            }

            network.OnUnloadEventCompleted += Completed;
            var status = network.UnloadScene(scene);
            if (status != SceneEventProgressStatus.Started)
            {
                network.OnUnloadEventCompleted -= Completed;
                Fail($"NGO {sceneName} 닫기 실패: {status}");
                yield break;
            }
            while (!done && OnlineSceneManager() == network) yield return null;
            network.OnUnloadEventCompleted -= Completed;
            if (!done) Fail($"{sceneName} 닫기 중 접속이 끊겼습니다.");
            else if (!stepFailed && scene.isLoaded) Fail($"{sceneName} 씬 닫기가 완료되지 않았습니다.");
        }

        /// <summary>접속 중인 호스트면 NGO 씬 관리, 아니면 null(오프라인 로드).</summary>
        static NetworkSceneManager OnlineSceneManager()
        {
            var session = NetworkSessionManager.Instance;
            var network = session != null ? session.NetworkManager : null;
            if (network == null || !network.IsListening || !network.IsServer || !network.NetworkConfig.EnableSceneManagement) return null;
            return network.SceneManager;
        }

        // ── 화면마다 (판정 여부와 상관없이) ─────────────────

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CampaignPerformanceEntry.Prepare(scene);
            // 추가 로드: 공간 씬의 조명·새로 만든 오브젝트가 그 공간에 속하도록 활성 씬으로 둔다(클라이언트도 각자).
            if (mode != LoadSceneMode.Additive) return;
            if (SpaceRoute.TryFind(scene.name, GameSettings.Base.SpaceTransition, out var space) && space != SpaceId.Hub)
                SceneManager.SetActiveScene(scene);
        }

        static List<Scene> LoadedSpaceScenes(BaseGameSettings.SpaceTransitionSettings settings)
        {
            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && SpaceRoute.TryFind(scene.name, settings, out var space) && space != SpaceId.Hub)
                    scenes.Add(scene);
            }
            return scenes;
        }

        static bool IsLoaded(string sceneName) => SceneManager.GetSceneByName(sceneName).isLoaded;

        void Fail(string message)
        {
            stepFailed = true;
            Debug.LogError($"[SpaceLink] {message}");
        }

        void AddLog(string line)
        {
            Debug.Log($"[SpaceLink] {line}");
            log.Add($"[{Time.time:0.0}s] {line}");
            if (log.Count > MaxLogLines) log.RemoveAt(0);
        }
    }
}
