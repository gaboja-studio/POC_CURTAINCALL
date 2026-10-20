using System.Collections;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Local rendering barrier. Gameplay and NGO retain ownership of scene loading.</summary>
    public sealed class SceneCurtainTransition : MonoBehaviour
    {
        static SceneCurtainTransition instance;
        TransitionCurtainView view;
        bool requested;
        float began;
        NetworkSceneManager networkScenes;
        public static bool IsActive => instance != null && instance.requested;
        public static IEnumerator Cover()
        {
            if (PerformanceEndingPresentation.Current == null)
            {
                while (CommonUiSceneLoader.IsLoading) yield return null;
                if (PerformanceEndingPresentation.Current == null)
                    yield return SceneManager.LoadSceneAsync(PerformanceEndingPresentation.ScenePath, LoadSceneMode.Additive);
            }
            var owner = PerformanceEndingPresentation.Current;
            if (owner == null || owner.Curtain == null) yield break;
            instance = owner.GetComponent<SceneCurtainTransition>() ?? owner.gameObject.AddComponent<SceneCurtainTransition>();
            instance.view = owner.Curtain;
            instance.requested = true;
            instance.began = Time.unscaledTime;
            InputGate.SetBlocking(instance, true);
            instance.view.SetLoadingText("다음 공간 준비 중");
            if (!instance.view.Close()) { instance.Release(); yield break; }
            float deadline = Time.unscaledTime + 4;
            while (instance != null && !instance.view.IsFullyCovered && Time.unscaledTime < deadline) yield return null;
            // A lost viewport must never deadlock gameplay.
        }
        public static void Complete()
        {
            if (instance != null) instance.StartCoroutine(instance.Reveal());
        }
        IEnumerator Reveal()
        {
            yield return null;
            while (CommonUiSceneLoader.IsLoading) yield return null;
            // Gameplay scene completion can precede the locally loaded stage backdrop.
            var background = Object.FindFirstObjectByType<CurtainCall.PerformanceBackground.PerformanceBackgroundLoader>();
            while (background != null && background.IsTransitioning && Time.unscaledTime - began < 45) yield return null;
            if (view != null) view.Open();
            while (view != null && view.IsBusy && Time.unscaledTime - began < 45) yield return null;
            Release();
        }
        void Update()
        {
            var network = NetworkManager.Singleton;
            var next = network != null && network.IsListening && !network.IsServer ? network.SceneManager : null;
            if (next != networkScenes)
            {
                if (networkScenes != null) networkScenes.OnSceneEvent -= OnSceneEvent;
                networkScenes = next;
                if (networkScenes != null) networkScenes.OnSceneEvent += OnSceneEvent;
            }
            if (requested && Time.unscaledTime - began > 45) Release();
        }
        void Awake()
        {
            instance = this;
            view = GetComponent<PerformanceEndingPresentation>().Curtain;
        }
        void OnSceneEvent(SceneEvent value)
        {
            if (value.SceneEventType == SceneEventType.Load) StartCoroutine(Cover());
            else if (value.SceneEventType == SceneEventType.LoadEventCompleted) Complete();
        }
        void Release()
        {
            requested = false;
            if (view != null) view.OpenImmediate();
            InputGate.SetBlocking(this, false);
        }
        void OnDestroy()
        {
            if (networkScenes != null) networkScenes.OnSceneEvent -= OnSceneEvent;
            Release(); if (instance == this) instance = null;
        }
    }
}
