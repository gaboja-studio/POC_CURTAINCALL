using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>공간 씬의 수명과 공통 UI의 로컬 additive 로드를 연결한다.</summary>
    public sealed class CommonUiSceneLoader : MonoBehaviour
    {
        static CommonUiSceneLoader instance;
        static int transitionHolds;
        public static bool IsLoading => instance != null && instance.loading != null;

        /// <summary>진행 중 로드는 끝내되 새 UI 씬 작업은 공간 전환이 끝날 때까지 보류한다.</summary>
        public static System.IDisposable DeferSceneChanges() => new TransitionHold();
        sealed class TransitionHold : System.IDisposable
        {
            bool disposed;
            public TransitionHold() { transitionHolds++; }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                transitionHolds = Mathf.Max(0, transitionHolds - 1);
            }
        }
        readonly List<PerformanceSceneBinding> spaces = new();
        readonly List<WaitingRoomUiBinding> waitingRooms = new();
        readonly List<InteractionSceneBinding> interactionSpaces = new();
        public static void RegisterInteraction(InteractionSceneBinding space)
        {
            if (instance == null)
            {
                var root = new GameObject("CommonUiSceneLoader"); DontDestroyOnLoad(root);
                instance = root.AddComponent<CommonUiSceneLoader>();
            }
            if (!instance.interactionSpaces.Contains(space)) instance.interactionSpaces.Add(space);
        }
        public static void UnregisterInteraction(InteractionSceneBinding space)
        { if (instance != null) instance.interactionSpaces.Remove(space); }
        Coroutine loading;
        NetworkSceneManager networkScenes;
        NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler previousLoad;
        NetworkSceneManager.VerifySceneBeforeUnloadingDelegateHandler previousUnload;
        public static PerformanceSceneBinding CurrentSpace => instance != null ? instance.SelectSpace() : null;
        public static WaitingRoomUiBinding CurrentWaitingRoom => instance != null ? instance.SelectWaitingRoom() : null;

        public static void Register(WaitingRoomUiBinding space)
        {
            if (instance == null)
            {
                var root = new GameObject("CommonUiSceneLoader");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<CommonUiSceneLoader>();
            }
            if (!instance.waitingRooms.Contains(space)) instance.waitingRooms.Add(space);
        }

        public static void Unregister(WaitingRoomUiBinding space)
        {
            if (instance != null) instance.waitingRooms.Remove(space);
        }

        WaitingRoomUiBinding SelectWaitingRoom()
        {
            // A hub may keep the waiting-room scene loaded during a performance.
            if (SelectSpace() != null) return null;
            for (int i = waitingRooms.Count - 1; i >= 0; i--)
                if (waitingRooms[i] != null && waitingRooms[i].isActiveAndEnabled) return waitingRooms[i];
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; transitionHolds = 0; }

        public static void Register(PerformanceSceneBinding space)
        {
            if (instance == null)
            {
                var root = new GameObject("CommonUiSceneLoader");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<CommonUiSceneLoader>();
            }
            if (!instance.spaces.Contains(space)) instance.spaces.Add(space);
        }

        public static void Unregister(PerformanceSceneBinding space)
        {
            if (instance != null) instance.spaces.Remove(space);
        }

        PerformanceSceneBinding SelectSpace()
        {
            var active = SceneManager.GetActiveScene();
            for (int i = spaces.Count - 1; i >= 0; i--)
                if (spaces[i] != null && spaces[i].isActiveAndEnabled && spaces[i].gameObject.scene == active) return spaces[i];
            for (int i = spaces.Count - 1; i >= 0; i--)
                if (spaces[i] != null && spaces[i].isActiveAndEnabled) return spaces[i];
            return null;
        }

        void Update()
        {
            AttachNetworkFilter();
            if (transitionHolds > 0) return;
            spaces.RemoveAll(s => s == null);
            waitingRooms.RemoveAll(s => s == null);
            interactionSpaces.RemoveAll(s => s == null);
            var scene = SceneManager.GetSceneByPath(CommonPerformanceUI.ScenePath);
            var waiting = SceneManager.GetSceneByPath(CommonWaitingRoomUI.ScenePath);
            bool waitingChanged = CurrentWaitingRoom != null ? !waiting.isLoaded : waiting.isLoaded && SceneManager.sceneCount > 1;
            var interaction = SceneManager.GetSceneByPath(CommonInteractionUI.ScenePath);
            bool interactionChanged = interactionSpaces.Count > 0 ? !interaction.isLoaded : interaction.isLoaded && SceneManager.sceneCount > 1;
            if (loading == null && (interactionChanged || waitingChanged || (CurrentSpace != null ? !scene.isLoaded || PerformanceEndingPresentation.Current == null : scene.isLoaded && SceneManager.sceneCount > 1)))
                loading = StartCoroutine(Reconcile());
        }

        IEnumerator Reconcile()
        {
            // Single 전환/OnDisable과 같은 프레임에서 다음 공간이 등록될 수 있다.
            yield return null;
            if (CurrentSpace != null && PerformanceEndingPresentation.Current == null
                && !SceneManager.GetSceneByPath(PerformanceEndingPresentation.ScenePath).isLoaded)
            {
                if (Application.CanStreamedLevelBeLoaded(PerformanceEndingPresentation.ScenePath))
                    yield return SceneManager.LoadSceneAsync(PerformanceEndingPresentation.ScenePath, LoadSceneMode.Additive);
                else { Debug.LogError("[UI] PerformanceTransitionUI must be in the build scene list."); enabled = false; }
            }
            var scene = SceneManager.GetSceneByPath(CommonPerformanceUI.ScenePath);
            if (CurrentSpace != null && !scene.isLoaded)
            {
                if (!Application.CanStreamedLevelBeLoaded(CommonPerformanceUI.ScenePath))
                {
                    Debug.LogError("[UI] CommonPerformanceUI must be enabled in the build scene list.");
                    enabled = false;
                }
                else yield return SceneManager.LoadSceneAsync(CommonPerformanceUI.ScenePath, LoadSceneMode.Additive);
            }
            else if (CurrentSpace == null && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                if (CommonPerformanceUI.Current != null) CommonPerformanceUI.Current.Hide();
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            var waiting = SceneManager.GetSceneByPath(CommonWaitingRoomUI.ScenePath);
            if (CurrentWaitingRoom != null && !waiting.isLoaded)
            {
                if (Application.CanStreamedLevelBeLoaded(CommonWaitingRoomUI.ScenePath))
                    yield return SceneManager.LoadSceneAsync(CommonWaitingRoomUI.ScenePath, LoadSceneMode.Additive);
                else { Debug.LogError("[UI] CommonWaitingRoomUI must be enabled in the build scene list."); enabled = false; }
            }
            else if (CurrentWaitingRoom == null && waiting.isLoaded && SceneManager.sceneCount > 1)
                yield return SceneManager.UnloadSceneAsync(waiting);
            var interaction = SceneManager.GetSceneByPath(CommonInteractionUI.ScenePath);
            if (interactionSpaces.Count > 0 && !interaction.isLoaded)
            {
                if (Application.CanStreamedLevelBeLoaded(CommonInteractionUI.ScenePath))
                    yield return SceneManager.LoadSceneAsync(CommonInteractionUI.ScenePath, LoadSceneMode.Additive);
                else { Debug.LogError("[UI] CommonInteractionUI must be enabled in the build scene list."); enabled = false; }
            }
            else if (interactionSpaces.Count == 0 && interaction.isLoaded && SceneManager.sceneCount > 1)
                yield return SceneManager.UnloadSceneAsync(interaction);
            loading = null;
            // The next Update can reconcile registrations which changed during an async load.
        }

        // HUD에는 NetworkObject가 없다. NGO의 늦은 참가 동기화에 공통 UI를 다시 싣지 않는다.
        // 기존 검증 delegate는 보존하고 일반 공간 씬에 그대로 위임한다.
        void AttachNetworkFilter()
        {
            var next = NetworkManager.Singleton != null ? NetworkManager.Singleton.SceneManager : null;
            if (ReferenceEquals(next, networkScenes)) return;
            DetachNetworkFilter();
            networkScenes = next;
            if (next == null) return;
            previousLoad = next.VerifySceneBeforeLoading;
            previousUnload = next.VerifySceneBeforeUnloading;
            next.VerifySceneBeforeLoading = AllowNetworkLoad;
            next.VerifySceneBeforeUnloading = AllowNetworkUnload;
        }

        bool AllowNetworkLoad(int index, string name, LoadSceneMode mode) =>
            name != "CommonPerformanceUI" && name != "PerformanceTransitionUI" && name != "CommonWaitingRoomUI" && name != "CommonInteractionUI" && (previousLoad == null || previousLoad(index, name, mode));
        bool AllowNetworkUnload(Scene scene) =>
            scene.path != CommonPerformanceUI.ScenePath && scene.path != PerformanceEndingPresentation.ScenePath && scene.path != CommonWaitingRoomUI.ScenePath && scene.path != CommonInteractionUI.ScenePath && (previousUnload == null || previousUnload(scene));

        void DetachNetworkFilter()
        {
            if (networkScenes == null) return;
            if (networkScenes.VerifySceneBeforeLoading == AllowNetworkLoad) networkScenes.VerifySceneBeforeLoading = previousLoad;
            if (networkScenes.VerifySceneBeforeUnloading == AllowNetworkUnload) networkScenes.VerifySceneBeforeUnloading = previousUnload;
            networkScenes = null; previousLoad = null; previousUnload = null;
        }

        void OnDestroy()
        {
            DetachNetworkFilter();
            if (instance == this) instance = null;
        }
    }
}
