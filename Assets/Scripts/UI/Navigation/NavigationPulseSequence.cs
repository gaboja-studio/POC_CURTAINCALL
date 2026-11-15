using System.Collections.Generic;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.UI.Wayfinding
{
    /// <summary>
    /// Repeating waves travel from the player toward the destination. A new wave starts every pulse interval,
    /// so arrows near the player keep giving direction feedback even on long routes.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(RehearsalNavigationView))]
    public sealed class NavigationPulseSequence : MonoBehaviour
    {
        [SerializeField] RehearsalNavigationView view;
        [SerializeField] NavigationPulseGlyph destination;
        [SerializeField] NavigationPulseGlyph arrivalEcho;
        [Tooltip("파동이 경로를 따라 목적지 쪽으로 진행하는 속도(m/s, 1배속 기준).")]
        [SerializeField, Min(.5f)] float waveSpeed = 6f;
        [SerializeField, Min(.02f)] float fadeInSeconds = .12f;
        [SerializeField, Min(0)] float holdSeconds = .16f;
        [SerializeField, Min(.02f)] float fadeOutSeconds = .48f;
        [SerializeField, Min(.1f)] float arrivalSeconds = .8f;
        [SerializeField] bool previewInEditor = true;
        [SerializeField] bool automaticPlayback = true;
        [SerializeField] Color signalColor = new(.12f, 1, .86f, 1);
        [SerializeField, Range(0, 1)] float emphasis = 1;
        readonly List<NavigationPulseGlyph> glyphs = new();
        NavigationPulseGlyph departureEcho;
        bool hideDestinationBeacon;
        float intervalOverride;
        double epoch;
        /// <summary>Seconds between waves at 1x speed. Defaults to GameSettings > Base > 네비게이션 안내 > 화살표 파동 간격; a per-guide override wins.</summary>
        public float PulseIntervalSeconds => intervalOverride > 0 ? intervalOverride : GameSettings.Base.Navigation.PulseIntervalSeconds;
        public float WaveSpeed => Mathf.Max(.5f, waveSpeed);
        /// <summary>Length of one arrow flash (fade in, hold, fade out).</summary>
        public float FlashSeconds => Mathf.Max(.02f, fadeInSeconds) + Mathf.Max(0, holdSeconds) + Mathf.Max(.02f, fadeOutSeconds);
        public bool PreviewInEditor => previewInEditor && automaticPlayback && isActiveAndEnabled;
        public bool AutomaticPlayback { get => automaticPlayback; set => automaticPlayback = value; }
        public float LastEvaluatedSeconds { get; private set; }

        static double Clock
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return UnityEditor.EditorApplication.timeSinceStartup;
#endif
                return Time.unscaledTimeAsDouble;
            }
        }

        /// <summary>0 or less returns to the common setting. Values are clamped to 0.2–10 seconds.</summary>
        public void SetPulseIntervalOverride(float seconds) =>
            intervalOverride = float.IsFinite(seconds) && seconds > 0 ? Mathf.Clamp(seconds, .2f, 10) : 0;

        /// <summary>Phase (0..interval) at which a wave passes the given distance from the destination.</summary>
        public float LightTime(float goalDistance) => Mathf.Repeat(-goalDistance / WaveSpeed, PulseIntervalSeconds);
        public float DepartureLightSeconds => LightTime(view != null ? view.RouteLength : 0);

        public void Configure(RehearsalNavigationView pathView, NavigationPulseGlyph goal, NavigationPulseGlyph echo)
        {
            if (view != null) view.PresentationChanged -= Rebind;
            view = pathView; destination = goal; arrivalEcho = echo;
            if (isActiveAndEnabled) view.PresentationChanged += Rebind;
            Rebind();
        }

        void OnEnable()
        {
            if (view == null) view = GetComponent<RehearsalNavigationView>();
            if (view != null) { view.PresentationChanged -= Rebind; view.PresentationChanged += Rebind; }
            epoch = Clock; Rebind();
        }

        void Rebind()
        {
            glyphs.Clear();
            if (view == null) return;
            // Indexed like the view's marker pool: slot i is i spacings away from the destination.
            foreach (var marker in view.MarkerInstances)
                glyphs.Add(marker != null && marker.TryGetComponent<NavigationPulseGlyph>(out var glyph) ? glyph : null);
            SetAppearance(signalColor, emphasis);
            if (automaticPlayback) EvaluateAt((float)(Clock - epoch));
        }

        void Update()
        {
            if (automaticPlayback && (Application.isPlaying || previewInEditor)) EvaluateAt((float)(Clock - epoch));
        }

        public void EvaluateAt(float elapsedSeconds)
        {
            LastEvaluatedSeconds = elapsedSeconds;
            float t = Mathf.Max(0, elapsedSeconds), interval = PulseIntervalSeconds, speed = WaveSpeed;
            for (int i = 0; i < glyphs.Count; i++)
            {
                var glyph = glyphs[i];
                if (glyph == null || !glyph.gameObject.activeSelf) continue;
                float a = Flash(t + view.GetMarkerGoalDistance(i) / speed, interval);
                glyph.Apply(a * view.GetMarkerPresence(i), Mathf.Lerp(.88f, 1, a));
            }
            float burstSeconds = Mathf.Min(arrivalSeconds, interval * .95f);
            float arrivalAge = Mathf.Repeat(t, interval);
            float burst = Burst(arrivalAge, burstSeconds);
            float goal = view != null ? view.DestinationPresence : 1;
            float breathe = .5f + .5f * Mathf.Cos(arrivalAge * Mathf.PI * 2 / interval);
            if (destination != null) destination.Apply(hideDestinationBeacon ? 0 : (.62f + .38f * Mathf.Max(burst, breathe)) * goal, 1 + .04f * burst);
            if (arrivalEcho != null) arrivalEcho.Apply(burst * .7f * goal, Mathf.Lerp(1.05f, 1.7f, 1 - burst));
            if (departureEcho != null)
            {
                float length = view != null ? view.RouteLength : 0;
                float departure = Burst(Mathf.Repeat(t + length / speed, interval), burstSeconds);
                departureEcho.Apply(departure * .7f * (view != null ? view.StartPresence : 1), Mathf.Lerp(1.05f, 1.7f, 1 - departure));
            }
        }

        static float Burst(float age, float seconds) => age >= 0 && age <= seconds ? 1 - age / seconds : 0;

        /// <summary>Envelope of the waves reaching an arrow; overlapping waves are combined when the interval is shorter than a flash.</summary>
        float Flash(float time, float interval)
        {
            float a = 0, length = FlashSeconds;
            for (float age = Mathf.Repeat(time, interval); age <= length; age += interval) a = Mathf.Max(a, Envelope(age));
            return a;
        }

        public bool DestinationBeaconHidden => hideDestinationBeacon;
        public NavigationPulseGlyph ArrivalEcho => arrivalEcho;
        public NavigationPulseGlyph DepartureEcho => departureEcho;
        public void SetEndpointPresentation(bool objectDestination,bool objectStart,Vector3 start)
        {
            hideDestinationBeacon=objectDestination;
            if(objectStart&&departureEcho==null&&arrivalEcho!=null)
            {
                departureEcho=Instantiate(arrivalEcho,transform);departureEcho.name="Departure echo";
            }
            if(departureEcho!=null)
            {
                departureEcho.gameObject.SetActive(objectStart);
                if(view!=null)view.TrackStartEffect(objectStart?departureEcho.transform:null,start);
                departureEcho.SetAppearance(signalColor,emphasis);
            }
        }

        public void SetAppearance(Color color, float strength)
        {
            signalColor = color; emphasis = Mathf.Clamp01(strength);
            foreach (var glyph in glyphs) if (glyph != null) glyph.SetAppearance(color, emphasis);
            if (destination != null) destination.SetAppearance(color, emphasis);
            if (arrivalEcho != null) arrivalEcho.SetAppearance(color, emphasis);
            if (departureEcho != null) departureEcho.SetAppearance(color, emphasis);
        }

        void OnValidate() => SetAppearance(signalColor, emphasis);

        float Envelope(float age)
        {
            if (age < 0) return 0;
            float fadeIn = Mathf.Max(.02f, fadeInSeconds), fadeOut = Mathf.Max(.02f, fadeOutSeconds);
            if (age < fadeIn) return Mathf.SmoothStep(0, 1, age / fadeIn);
            age -= fadeIn + Mathf.Max(0, holdSeconds);
            return age <= 0 ? 1 : 1 - Mathf.SmoothStep(0, 1, age / fadeOut);
        }

        void OnDisable()
        {
            if (view != null) view.PresentationChanged -= Rebind;
            foreach (var glyph in glyphs) if (glyph != null) glyph.Apply(0);
            if (destination != null) destination.Apply(0);
            if (arrivalEcho != null) arrivalEcho.Apply(0);
            if (departureEcho != null) departureEcho.Apply(0);
        }
    }
}
