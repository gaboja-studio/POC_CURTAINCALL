using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.UI.Wayfinding
{
    [ExecuteAlways, RequireComponent(typeof(RehearsalNavigationView))]
    public sealed class NavigationPulseSequence : MonoBehaviour
    {
        [SerializeField] RehearsalNavigationView view;
        [SerializeField] NavigationPulseGlyph destination;
        [SerializeField] NavigationPulseGlyph arrivalEcho;
        [SerializeField, Min(.5f)] float repeatSeconds = 4.2f;
        [SerializeField, Min(.1f)] float travelSeconds = 2.35f;
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
        double epoch;
        public float RepeatSeconds => Mathf.Max(repeatSeconds, travelSeconds + fadeInSeconds + holdSeconds + fadeOutSeconds + .15f, travelSeconds + arrivalSeconds + .15f);
        public float TravelSeconds => Mathf.Max(.1f, travelSeconds);
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
            bool wasEmpty = glyphs.Count == 0;
            glyphs.Clear();
            if (view == null) return;
            // The route pool is stored from destination to player. Traverse it in reverse for an outward wave.
            for (int i = view.VisibleMarkerCount - 1; i >= 0; i--)
            {
                var marker = view.MarkerInstances[i];
                if (marker != null && marker.TryGetComponent<NavigationPulseGlyph>(out var glyph)) glyphs.Add(glyph);
            }
            if (wasEmpty && glyphs.Count > 0) epoch = Clock;
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
            float t = Mathf.Repeat(Mathf.Max(0, elapsedSeconds), RepeatSeconds);
            for (int i = 0; i < glyphs.Count; i++)
            {
                float delay = glyphs.Count <= 1 ? 0 : i / (float)(glyphs.Count - 1) * TravelSeconds;
                float a = Envelope(t - delay);
                glyphs[i].Apply(a, Mathf.Lerp(.88f, 1, a));
            }
            float arrivalAge = t - TravelSeconds;
            float burst = arrivalAge >= 0 && arrivalAge <= arrivalSeconds ? 1 - arrivalAge / arrivalSeconds : 0;
            if (destination != null) destination.Apply(hideDestinationBeacon ? 0 : .62f + .38f * Mathf.Max(burst, .5f + .5f * Mathf.Sin(t * Mathf.PI * 2 / RepeatSeconds)), 1 + .04f * burst);
            if (arrivalEcho != null) arrivalEcho.Apply(burst * .7f, Mathf.Lerp(1.05f, 1.7f, 1 - burst));
            float departure = t < arrivalSeconds ? 1-t/arrivalSeconds : 0;
            if(departureEcho!=null)departureEcho.Apply(departure*.7f,Mathf.Lerp(1.05f,1.7f,1-departure));
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
                if(view!=null)view.PlaceGroundEffect(departureEcho.transform,start);
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
