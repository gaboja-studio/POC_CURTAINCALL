using System.Collections;
using System.Collections.Generic;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>모든 화면의 커튼 닫힘을 확인한 뒤 씬 교체를 허용하는 로컬 렌더링 장벽.</summary>
    public sealed class SceneCurtainTransition : MonoBehaviour
    {
        const string CoverMessage = "CurtainCall.Transition.Cover";
        const string CoveredMessage = "CurtainCall.Transition.Covered";
        const string RevealMessage = "CurtainCall.Transition.Reveal";
        static SceneCurtainTransition instance;
        readonly HashSet<ulong> waiting = new();
        readonly HashSet<string> loadingScenes = new();
        TransitionCurtainView view;
        NetworkManager watchedNetwork;
        NetworkManager network;
        NetworkSceneManager networkScenes;
        CustomMessagingManager messaging;
        Coroutine reveal;
        bool requested, locallyCovered;
        uint sequence;
        public static bool IsActive => instance != null && instance.requested;
        public static bool CanTransition => instance != null && instance.locallyCovered
            && instance.view != null && instance.view.IsFullyCovered && instance.waiting.Count == 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var root = new GameObject(nameof(SceneCurtainTransition));
            DontDestroyOnLoad(root);
            root.AddComponent<SceneCurtainTransition>();
        }

        public static bool CanTransitionFor(uint version) => CanTransition && instance.sequence == version;

        public static IEnumerator Cover() => CoverFor(null);

        public static IEnumerator CoverFor(System.Action<uint> started)
        {
            Bootstrap();
            var owner = instance;
            owner.AttachNetwork();
            uint version = owner.sequence + 1;
            owner.Begin(version);
            started?.Invoke(version);
            if (owner.network != null && owner.network.IsServer)
            {
                foreach (ulong id in owner.network.ConnectedClientsIds)
                    if (id != owner.network.LocalClientId) owner.waiting.Add(id);
                owner.Broadcast(CoverMessage);
            }
            yield return owner.CloseLocally(version);
            float deadline = Time.unscaledTime + 15;
            while (owner.requested && owner.sequence == version && owner.waiting.Count > 0 && Time.unscaledTime < deadline)
            {
                if (owner.network == null || !owner.network.IsListening) break;
                owner.waiting.RemoveWhere(id => !owner.network.ConnectedClients.ContainsKey(id));
                yield return null;
            }
            if (owner.requested && owner.sequence == version && !CanTransition)
                Debug.LogError("[Curtain] 커튼 닫힘 확인 실패. 씬 전환을 취소합니다.");
        }

        void Begin(uint next)
        {
            if (reveal != null) { StopCoroutine(reveal); reveal = null; }
            sequence = next;
            requested = true;
            locallyCovered = false;
            waiting.Clear();
            InputGate.SetBlocking(this, true);
        }

        IEnumerator CloseLocally(uint version)
        {
            // 진행 중인 UI 로드는 끝내되, 전환 UI를 동시에 중복 로드하지 않는다.
            using (CommonUiSceneLoader.DeferSceneChanges())
            {
                while (CommonUiSceneLoader.IsLoading) yield return null;
                if (PerformanceEndingPresentation.Current == null)
                {
                    AsyncOperation operation = null;
                    try { operation = SceneManager.LoadSceneAsync(PerformanceEndingPresentation.ScenePath, LoadSceneMode.Additive); }
                    catch (System.Exception exception) { Debug.LogError($"[Curtain] 전환 UI 로드 실패: {exception.Message}"); }
                    if (operation == null) yield break;
                    yield return operation;
                }
            }
            if (!requested || sequence != version) yield break;
            var owner = PerformanceEndingPresentation.Current;
            view = owner != null ? owner.Curtain : null;
            if (view == null) yield break;
            view.SetLoadingText("다음 공간 준비 중");
            if (!view.Close()) yield break;
            float deadline = Time.unscaledTime + 4;
            while (requested && sequence == version && view != null && !view.IsFullyCovered && Time.unscaledTime < deadline)
                yield return null;
            if (requested && sequence == version) locallyCovered = view != null && view.IsFullyCovered;
        }

        // 취소된 이전 호출자의 finally가 새 전환을 열지 못하게 한다.
        public static void CompleteFor(uint version)
        {
            if (instance != null && instance.sequence == version) Complete();
        }

        public static void Complete()
        {
            if (instance == null) return;
            if (instance.network != null && instance.network.IsListening && instance.network.IsServer)
                instance.Broadcast(RevealMessage);
            instance.StartReveal();
        }

        void StartReveal()
        {
            if (requested && reveal == null) reveal = StartCoroutine(Reveal(sequence));
        }

        IEnumerator Reveal(uint version)
        {
            // sceneLoaded 다음 Update에서 시작하는 로컬 UI·배경 로드와 LateUpdate 배치를 기다린다.
            int settled = 0;
            while (requested && sequence == version && settled < 2)
            {
                yield return null;
                var background = Object.FindFirstObjectByType<CurtainCall.PerformanceBackground.PerformanceBackgroundLoader>();
                settled = loadingScenes.Count > 0 || CommonUiSceneLoader.IsLoading
                    || (background != null && background.IsTransitioning) ? 0 : settled + 1;
            }
            if (!requested || sequence != version) yield break;
            if (view != null) view.Open();
            while (view != null && view.IsBusy) yield return null;
            Release();
            reveal = null;
        }

        void Awake()
        {
            // 기존 전환 UI 프리팹의 컴포넌트는 유지하되, 조정자는 씬과 독립적으로 하나만 둔다.
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            NetworkManager.OnInstantiated += WatchNetwork;
            WatchNetwork(NetworkManager.Singleton);
        }

        void WatchNetwork(NetworkManager next)
        {
            if (watchedNetwork == next) return;
            if (watchedNetwork != null)
            {
                watchedNetwork.OnClientStarted -= AttachNetwork;
                watchedNetwork.OnServerStarted -= AttachNetwork;
            }
            watchedNetwork = next;
            if (next == null) return;
            next.OnClientStarted += AttachNetwork;
            next.OnServerStarted += AttachNetwork;
            AttachNetwork();
        }

        void Update()
        {
            WatchNetwork(NetworkManager.Singleton);
            AttachNetwork();
        }

        void AttachNetwork()
        {
            var next = NetworkManager.Singleton;
            var nextMessaging = next != null && next.IsListening ? next.CustomMessagingManager : null;
            if (messaging == nextMessaging) return;
            bool lostConnection = messaging != null;
            DetachNetwork();
            if (lostConnection && requested)
            {
                StopAllCoroutines();
                reveal = null;
                Release();
            }
            network = next;
            messaging = nextMessaging;
            if (messaging == null) return;
            networkScenes = next.SceneManager;
            if (networkScenes != null) networkScenes.OnSceneEvent += OnSceneEvent;
            messaging.RegisterNamedMessageHandler(CoverMessage, OnCover);
            messaging.RegisterNamedMessageHandler(CoveredMessage, OnCovered);
            messaging.RegisterNamedMessageHandler(RevealMessage, OnReveal);
        }

        void OnSceneEvent(SceneEvent value)
        {
            if (network == null) return;
            if ((value.SceneEventType == SceneEventType.Load || value.SceneEventType == SceneEventType.Unload)
                && value.ClientId == network.LocalClientId)
                loadingScenes.Add(value.SceneName);
            else if ((value.SceneEventType == SceneEventType.LoadComplete || value.SceneEventType == SceneEventType.UnloadComplete)
                && value.ClientId == network.LocalClientId)
                loadingScenes.Remove(value.SceneName);
        }

        void OnCover(ulong sender, FastBufferReader reader)
        {
            if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out uint version);
            Begin(version);
            StartCoroutine(CoverAndReply(version));
        }

        IEnumerator CoverAndReply(uint version)
        {
            yield return CloseLocally(version);
            if (requested && sequence == version && locallyCovered && network != null && network.IsListening)
                Send(CoveredMessage, NetworkManager.ServerClientId);
        }

        void OnCovered(ulong sender, FastBufferReader reader)
        {
            if (network == null || !network.IsServer) return;
            reader.ReadValueSafe(out uint version);
            if (requested && version == sequence) waiting.Remove(sender);
        }

        void OnReveal(ulong sender, FastBufferReader reader)
        {
            if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out uint version);
            if (requested && version == sequence) StartReveal();
        }

        void Broadcast(string name)
        {
            foreach (ulong id in network.ConnectedClientsIds)
                if (id != network.LocalClientId) Send(name, id);
        }

        void Send(string name, ulong id)
        {
            using var writer = new FastBufferWriter(sizeof(uint), Allocator.Temp);
            writer.WriteValueSafe(sequence);
            messaging.SendNamedMessage(name, id, writer, NetworkDelivery.ReliableSequenced);
        }

        void DetachNetwork()
        {
            if (networkScenes != null) networkScenes.OnSceneEvent -= OnSceneEvent;
            networkScenes = null;
            loadingScenes.Clear();
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(CoverMessage);
                messaging.UnregisterNamedMessageHandler(CoveredMessage);
                messaging.UnregisterNamedMessageHandler(RevealMessage);
            }
            messaging = null;
            network = null;
        }

        void Release()
        {
            requested = locallyCovered = false;
            waiting.Clear();
            if (view != null) view.OpenImmediate();
            InputGate.SetBlocking(this, false);
        }

        void OnDestroy()
        {
            if (instance != this) return;
            NetworkManager.OnInstantiated -= WatchNetwork;
            WatchNetwork(null);
            DetachNetwork();
            Release();
            instance = null;
        }
    }
}
