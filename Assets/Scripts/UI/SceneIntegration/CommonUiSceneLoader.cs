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
        readonly List<PerformanceSceneBinding> spaces = new();
        Coroutine loading;
        NetworkSceneManager networkScenes;
        NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler previousLoad;
        NetworkSceneManager.VerifySceneBeforeUnloadingDelegateHandler previousUnload;
        public static PerformanceSceneBinding CurrentSpace => instance != null ? instance.SelectSpace() : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

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
            spaces.RemoveAll(s => s == null);
            var scene = SceneManager.GetSceneByPath(CommonPerformanceUI.ScenePath);
            if (loading == null && (CurrentSpace != null ? !scene.isLoaded : scene.isLoaded && SceneManager.sceneCount > 1))
                loading = StartCoroutine(Reconcile());
        }

        IEnumerator Reconcile()
        {
            // Single 전환/OnDisable과 같은 프레임에서 다음 공간이 등록될 수 있다.
            yield return null;
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
            loading = null;
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
            name != "CommonPerformanceUI" && (previousLoad == null || previousLoad(index, name, mode));
        bool AllowNetworkUnload(Scene scene) =>
            scene.path != CommonPerformanceUI.ScenePath && (previousUnload == null || previousUnload(scene));

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
