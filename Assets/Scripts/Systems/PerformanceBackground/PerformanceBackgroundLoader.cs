using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.PerformanceBackground
{
    public sealed class PerformanceBackgroundLoader : MonoBehaviour
    {
        static PerformanceBackgroundLoader instance;
        readonly HashSet<NetworkManager> managers = new();
        readonly Dictionary<NetworkSceneManager, (NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler previous, NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler installed)> filters = new();
        readonly PerformanceBackgroundEnvironment environment = new();
        Coroutine reconcile;
        Scene ownedScene;
        Transform backgroundRoot;
        bool quitting;

        public bool IsTransitioning => reconcile != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateOnLoad()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(PerformanceBackgroundLoader));
            DontDestroyOnLoad(go);
            go.AddComponent<PerformanceBackgroundLoader>();
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            NetworkManager.OnInstantiated += WatchNetwork;
            foreach (var manager in FindObjectsByType<NetworkManager>(FindObjectsSortMode.None)) WatchNetwork(manager);
            QueueReconcile();
        }

        void WatchNetwork(NetworkManager manager)
        {
            if (manager == null || !managers.Add(manager)) return;
            manager.OnServerStarted += InstallNetworkFilters;
            manager.OnClientStarted += InstallNetworkFilters;
            InstallNetworkFilters();
        }

        void InstallNetworkFilters()
        {
            foreach (var manager in managers)
            {
                if (manager == null || manager.SceneManager == null) continue;
                var sceneManager = manager.SceneManager;
                if (filters.ContainsKey(sceneManager)) continue;
                var previous = sceneManager.VerifySceneBeforeLoading;
                NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler filter = (index, name, mode) =>
                    name != PerformanceBackgroundLayout.SceneName && (previous == null || previous(index, name, mode));
                filters.Add(sceneManager, (previous, filter));
                sceneManager.VerifySceneBeforeLoading = filter;
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => QueueReconcile();
        void OnSceneUnloaded(Scene scene) => QueueReconcile();
        void OnActiveSceneChanged(Scene previous, Scene current) => QueueReconcile();
        void OnApplicationQuit() => quitting = true;
        void LateUpdate() => environment.Update(ownedScene.IsValid() && ownedScene.isLoaded);

        void QueueReconcile()
        {
            if (!quitting && reconcile == null) reconcile = StartCoroutine(Reconcile());
        }

        bool TryFindPerformance(out Vector3 position)
        {
            var active = SceneManager.GetActiveScene();
            if (active.isLoaded && PerformanceBackgroundLayout.TryGetPosition(active.name, out position)) return true;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && PerformanceBackgroundLayout.TryGetPosition(scene.name, out position)) return true;
            }
            position = default;
            return false;
        }

        IEnumerator Reconcile()
        {
            // 씬 로드 콜백 안에서 active scene을 정하는 기존 전환 처리를 먼저 끝낸다.
            yield return null;
            while (!quitting)
            {
                bool needed = TryFindPerformance(out var position);
                var background = SceneManager.GetSceneByPath(PerformanceBackgroundLayout.ScenePath);
                if (needed && !background.isLoaded)
                {
                    AsyncOperation operation = null;
                    try { operation = SceneManager.LoadSceneAsync(PerformanceBackgroundLayout.ScenePath, LoadSceneMode.Additive); }
                    catch (Exception exception) { Debug.LogError($"[PerformanceBackground] 배경 로드 실패: {exception.Message}"); }
                    if (operation == null) break;
                    yield return operation;
                    ownedScene = SceneManager.GetSceneByPath(PerformanceBackgroundLayout.ScenePath);
                    if (!ownedScene.isLoaded) { yield return null; continue; }
                    ApplyLayout(ownedScene, position);
                    continue;
                }
                if (!needed && ownedScene.IsValid() && ownedScene.isLoaded)
                {
                    var operation = SceneManager.UnloadSceneAsync(ownedScene);
                    if (operation != null) yield return operation;
                    ownedScene = default;
                    backgroundRoot = null;
                    continue;
                }
                if (needed && background.isLoaded && background == ownedScene && backgroundRoot != null)
                    backgroundRoot.position = position;
                break;
            }
            reconcile = null;
        }

        void ApplyLayout(Scene scene, Vector3 position)
        {
            var roots = scene.GetRootGameObjects();
            var container = new GameObject("PerformanceBackgroundPlacement");
            SceneManager.MoveGameObjectToScene(container, scene);
            backgroundRoot = container.transform;
            foreach (var root in roots) root.transform.SetParent(backgroundRoot, true);
            backgroundRoot.localScale = Vector3.one * PerformanceBackgroundLayout.Scale;
            backgroundRoot.position = position;
            foreach (var light in container.GetComponentsInChildren<Light>(true))
            {
                light.range *= PerformanceBackgroundLayout.Scale;
                if (light.type != LightType.Directional)
                    light.intensity *= PerformanceBackgroundLayout.Scale * PerformanceBackgroundLayout.Scale;
            }
        }

        void OnDestroy()
        {
            if (instance != this) return;
            environment.Restore();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            NetworkManager.OnInstantiated -= WatchNetwork;
            foreach (var manager in managers)
            {
                if (manager == null) continue;
                manager.OnServerStarted -= InstallNetworkFilters;
                manager.OnClientStarted -= InstallNetworkFilters;
            }
            foreach (var entry in filters)
                if (entry.Key.VerifySceneBeforeLoading == entry.Value.installed)
                    entry.Key.VerifySceneBeforeLoading = entry.Value.previous;
            if (!quitting && ownedScene.IsValid() && ownedScene.isLoaded)
                SceneManager.UnloadSceneAsync(ownedScene);
            instance = null;
        }
    }
}
