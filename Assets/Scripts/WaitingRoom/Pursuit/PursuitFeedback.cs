using CurtainCall.Cameras;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.UI.SceneIntegration;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>One shared danger edge for the camera's local or spectated player.</summary>
    [DefaultExecutionOrder(1300), DisallowMultipleComponent]
    public sealed class PursuitFeedback : MonoBehaviour
    {
        static PursuitFeedback displayOwner, fallback;
        PursuitDirector director;
        PlayerInputReader offlineReader;
        PursuitEdgeGraphic edge;
        GameObject canvasObject;
        Canvas canvas;
        GameObject lastObserver;
        Object lastSource;
        double lastRound;
        float lastWaitingElapsed = -1;
        float intensity, beat;
        public int HighlightCount => 0; // Retained for diagnostics; silhouette outlines have been removed.
        public float Intensity => intensity;
        public bool IsChased { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { displayOwner = null; fallback = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateFallback()
        {
            if (fallback) return;
            var host = new GameObject("Pursuit feedback (shared)");
            DontDestroyOnLoad(host);
            fallback = host.AddComponent<PursuitFeedback>();
        }

        void OnEnable()
        {
            director = GetComponent<PursuitDirector>();
            ClaimDisplay();
        }

        bool ClaimDisplay()
        {
            if (displayOwner && displayOwner != this && displayOwner.isActiveAndEnabled) return false;
            displayOwner = this;
            if (canvasObject) return true;
            canvasObject = new GameObject("PursuitThreatEdges", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -5;
            canvas.enabled = false;
            var go = new GameObject("ThreatEdges", typeof(RectTransform), typeof(CanvasRenderer), typeof(PursuitEdgeGraphic));
            go.transform.SetParent(canvasObject.transform, false);
            edge = go.GetComponent<PursuitEdgeGraphic>(); edge.raycastTarget = false;
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return true;
        }
        internal static GameObject LocalObserver(ref PlayerInputReader offlineReader)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
                var p = NetworkPlayer.Local;
                return p != null && p.IsSpawned && p.State == PlayerState.Normal ? p.gameObject : null;
            }
            if (offlineReader == null) offlineReader = CurtainCall.Player.OfflineLocalPlayer.Reader;
            return offlineReader != null && offlineReader.isActiveAndEnabled ? offlineReader.gameObject : null;
        }
        public static bool Threatens(PursuitActor actor, GameObject observer) => actor != null && observer != null
            && actor.IsActivated && actor.State == PursuitMotion.Run && actor.ChasedPlayer == observer;

        // Eye feedback still uses LocalObserver. Only the camera edge follows spectator focus.
        internal static GameObject ScreenObserver(ref PlayerInputReader offlineReader)
        {
            if (CameraViewDirector.Current && CameraViewDirector.Current.IsShowingDeathFall) return null;
            var observer = CameraSpectator.IsSpectating
                ? CameraSpectator.Target ? CameraSpectator.Target.gameObject : null
                : LocalObserver(ref offlineReader);
            if (!observer || !observer.activeInHierarchy || PursuitDirector.IsCaptured(observer)) return null;
            if (observer.TryGetComponent(out NetworkPlayer player)
                && (!player.isActiveAndEnabled || player.State != PlayerState.Normal
                    || NetworkManager.Singleton && NetworkManager.Singleton.IsListening && !player.IsSpawned)) return null;
            if (observer.TryGetComponent(out PlayerBalance balance) && balance.HasFallen) return null;
            return observer;
        }

        void LateUpdate()
        {
            if (!ClaimDisplay()) return;
            var observer = ScreenObserver(ref offlineReader);
            PursuitThreatSample sample = default;
            if (observer && !SceneCurtainTransition.IsActive)
            {
                if (EscapeCampaignContent.IsTraversalActive)
                {
                    var chase = EscapeUnderstageChase.Current;
                    var timer = EscapeRouteTimer.Current;
                    var settings = GameSettings.WorldUi;
                    bool available = settings.escapeThreatEnabled && chase && chase.CanShowThreatFeedback;
                    var player = observer.GetComponent<NetworkPlayer>();
                    sample = PursuitThreatSample.Escape(observer, chase, timer ? timer.StartedAt : -1,
                        available, available && chase.TargetsPlayer(player),
                        chase ? Vector3.Distance(observer.transform.position, chase.transform.position) : float.PositiveInfinity,
                        settings.escapeThreatDistance);
                }
                else sample = WaitingSample(observer);
            }
            Present(sample, Time.unscaledDeltaTime);
        }

        PursuitThreatSample WaitingSample(GameObject observer)
        {
            var current = PursuitDirector.Current ? PursuitDirector.Current : director;
            bool available = current && current.isActiveAndEnabled && current.Elapsed >= 0
                && current.gameObject.scene == SceneManager.GetActiveScene();
            float nearest = float.PositiveInfinity;
            bool chased = false;
            if (available) foreach (var actor in current.Actors) {
                if (actor != null && Threatens(actor, observer)) {
                    chased = true; nearest = Mathf.Min(nearest, Vector3.Distance(observer.transform.position, actor.transform.position));
                }
            }
            if (available && lastWaitingElapsed > current.Elapsed) ClearDisplay();
            lastWaitingElapsed = available ? current.Elapsed : -1;
            return new PursuitThreatSample(observer, current, 0, available, chased,
                chased ? Mathf.Clamp01(1 - nearest / 20f) : 0, available ? Mathf.Clamp01(current.Elapsed / 180f) : 0);
        }

        void Present(PursuitThreatSample sample, float deltaTime)
        {
            var rules = GameSettings.WaitingRoom.Pursuit;
            if (!sample.Available || !sample.IsEscape && !rules.ScreenEffect) { ClearDisplay(); return; }
            // Never carry another subject's pulse, or a previous round's danger, into this view.
            if (lastObserver != sample.Observer || lastSource != sample.Source || lastRound != sample.Round)
                ClearDisplay();
            lastObserver = sample.Observer; lastSource = sample.Source; lastRound = sample.Round;
            IsChased = sample.Threatened;
            float target = IsChased ? Mathf.Lerp(.5f, 1, sample.Danger) * rules.EdgeIntensity : 0;
            // Smooth onset/release and continuous phase: never flash the entire image.
            intensity = Mathf.MoveTowards(intensity, target, deltaTime * (target > intensity ? 1.6f : .8f));
            beat += deltaTime * Mathf.Lerp(1, 1.9f, Mathf.Max(sample.Danger, sample.Escalation));
            float pulse = Mathf.Pow((Mathf.Cos(beat * Mathf.PI * 2) + 1) * .5f, 3);
            edge.SetThreat(intensity * Mathf.Lerp(1 - rules.PulseAmount, 1, pulse), rules.EdgeWidth);
            canvas.enabled = intensity > .001f;
        }

        void ClearDisplay()
        {
            intensity = beat = 0; IsChased = false;
            lastObserver = null; lastSource = null; lastRound = -1;
            if (edge) edge.SetThreat(0, GameSettings.WaitingRoom.Pursuit.EdgeWidth);
            if (canvas) canvas.enabled = false;
        }

        void OnDisable()
        {
            ClearDisplay();
            if (displayOwner == this) displayOwner = null;
            if (canvasObject)
            {
                canvasObject.SetActive(false);
                if (Application.isPlaying) Destroy(canvasObject);
                else DestroyImmediate(canvasObject);
            }
            canvasObject = null; canvas = null; edge = null;
            lastWaitingElapsed = -1;
        }
    }
}
