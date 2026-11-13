using System.Collections;
using CurtainCall.Campaign;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Shared failure result, held until the host explicitly returns to the lobby.</summary>
    [DefaultExecutionOrder(950)]
    public sealed class FailureEndingPresentation : MonoBehaviour
    {
        public static FailureEndingPresentation Current { get; private set; }
        [SerializeField] CanvasGroup fade;
        [SerializeField] FailureEndingStage stage;
        [SerializeField, Min(.1f)] float fadeOutSeconds = .8f, fadeInSeconds = 1.25f;
        [SerializeField, Min(0)] float blackHoldSeconds = .25f, sceneHoldSeconds = 1.5f;
        EscapeEndingResults results;
        CampaignRun run;
        int generation = -1;
        bool started, returning, release, finished;
        Coroutine sequence;
        public bool IsPresenting => started && !finished;
        public bool Ready => IsPresenting && results != null && results.View != null && results.View.Visible && !returning;
        public float FadeAlpha => fade != null ? fade.alpha : 0;

        public static bool IsFailureEnding(CampaignSnapshot state) => state.Phase == CampaignPhase.Ending
            && state.LastEnd is RunEndReason.Deadline or RunEndReason.EscapeAllRetired;

        public static FailureEndingPresentation EnsureExists()
        {
            if (Current != null) return Current;
            var prefab = Resources.Load<GameObject>("Prefabs/Endings/FailureEndingA");
            if (prefab == null) { Debug.LogError("FailureEndingA prefab is missing."); return null; }
            var instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);
            return instance.GetComponent<FailureEndingPresentation>();
        }

        void Awake()
        {
            Current = this;
            results = GetComponent<EscapeEndingResults>();
            fade.alpha = 0; fade.blocksRaycasts = fade.interactable = false;
            SceneManager.sceneLoaded += Loaded;
        }

        void LateUpdate()
        {
            run = CampaignRun.Instance;
            if (run == null || !IsFailureEnding(run.State))
            {
                if (started) ResetPresentation();
                return;
            }
            if (generation != run.State.Generation) ResetPresentation();
            if (!started) Begin();
            if (!IsPresenting) return;
            // An additive return may reveal a lobby which was already loaded.
            if (stage.Playing && SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.Current == SpaceId.Hub
                && (release && !SpaceLinkDirector.Instance.IsTransitioning || CurtainCall.Network.Session.NetworkSessionManager.Instance?.IsHost == false))
            { Finish(); return; }
            CurtainCall.UI.SceneIntegration.CommonPerformanceUI.Current?.HideHints();
            if (Ready) results.Show(run.HasAuthority);
        }

        void Begin()
        {
            if (started || run == null) return;
            started = true; finished = returning = release = false;
            generation = run.State.Generation;
            results.Freeze();
            InputGate.SetBlocking(this, true);
            sequence = StartCoroutine(Present());
        }

        IEnumerator Present()
        {
            yield return FadeTo(1, fadeOutSeconds);
            while (IsPresenting && !results.HasSnapshot) { results.Freeze(); yield return null; }
            if (!IsPresenting) yield break;
            stage.Begin(results.People);
            yield return new WaitForSecondsRealtime(blackHoldSeconds);
            yield return FadeTo(0, fadeInSeconds);
            yield return new WaitForSecondsRealtime(sceneHoldSeconds);
            if (IsPresenting) results.Show(run != null && run.HasAuthority);
            sequence = null;
        }

        IEnumerator FadeTo(float alpha, float seconds)
        {
            float before = fade.alpha;
            fade.blocksRaycasts = true;
            for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            { fade.alpha = Mathf.Lerp(before, alpha, Mathf.SmoothStep(0, 1, elapsed / seconds)); yield return null; }
            fade.alpha = alpha;
            fade.blocksRaycasts = alpha > 0;
        }

        public bool ReturnToLobby()
        {
            if (!Ready || run == null || !run.HasAuthority) return false;
            returning = true;
            sequence = StartCoroutine(Return());
            return true;
        }
        IEnumerator Return()
        {
            yield return FadeTo(1, fadeOutSeconds);
            release = true;
            sequence = null;
        }
        public IEnumerator HoldBeforeTransition()
        {
            run = CampaignRun.Instance;
            Begin();
            while (isActiveAndEnabled && !release && run != null && IsFailureEnding(run.State)) yield return null;
        }
        void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (IsPresenting && scene.name == CurtainCall.Settings.GameSettings.Base.SpaceTransition.HubScene) Finish();
        }
        void Finish()
        {
            finished = true;
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
            stage.Stop();
            if (results.View != null) results.View.SetVisible(false);
            fade.alpha = 0; fade.blocksRaycasts = false;
            InputGate.SetBlocking(this, false);
        }
        void ResetPresentation()
        {
            Finish();
            results.Reset();
            started = returning = release = finished = false;
            generation = -1;
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= Loaded;
            if (stage != null) stage.Stop();
            InputGate.SetBlocking(this, false);
            if (Current == this) Current = null;
        }
    }
}
