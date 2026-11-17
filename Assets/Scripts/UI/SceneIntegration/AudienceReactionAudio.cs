using CurtainCall.Settings;
using CurtainCall.PerformanceBackground;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Preloaded human takes with baked short reverb; bounded local spatial voices, no extra packets.</summary>
    public sealed class AudienceReactionAudio : MonoBehaviour
    {
        [SerializeField] AudioClip[] cheers;
        [SerializeField] AudioClip[] boos;
        [SerializeField] AudioSource cheerSource;
        [SerializeField] AudioSource booSource;
        [SerializeField] AudioSource ambienceSource;
        bool venueActive;
        float ambienceLevel;
        int lastCheer = -1, lastBoo = -1;
        float nextCheer, nextBoo;
        AudioSource fading;
        float fadeUntil, fadingVolume;
        int lastSector = -1;
        // Positions in the existing arena background's local coordinates, at the upper audience ring.
        static readonly Vector3[] Seats = { new(-246, 38, 326), new(246, 38, 326), new(-246, 38, -166), new(246, 38, -166) };
        public int PlayCount { get; private set; }
        public int AmbienceStartCount { get; private set; }
        public bool AmbiencePlaying => ambienceSource != null && ambienceSource.isPlaying;
        public void ConfigureAmbience(AudioSource source, AudioClip clip)
        { ambienceSource = source; source.clip = clip; }
        public void SetVenueActive(bool active) => venueActive = active;
        public void Configure(AudioClip[] positive, AudioClip[] negative, AudioSource up, AudioSource down)
        { cheers = positive; boos = negative; cheerSource = up; booSource = down; }
        void Awake()
        {
            Prepare(cheerSource, cheers, LocalAudioCategory.AudienceCheer); Prepare(booSource, boos, LocalAudioCategory.AudienceBoo);
            if (ambienceSource != null)
            {
                ambienceSource.playOnAwake = false; ambienceSource.loop = true; ambienceSource.spatialBlend = 0;
                ambienceSource.dopplerLevel = 0; ambienceSource.reverbZoneMix = 0; ambienceSource.priority = 160;
                LocalAudioController.Register(ambienceSource, LocalAudioCategory.AudienceMurmur);
                LocalAudioController.SetSourceVolume(ambienceSource, 0);
                if (ambienceSource.clip != null && ambienceSource.clip.loadState == AudioDataLoadState.Unloaded) ambienceSource.clip.LoadAudioData();
            }
        }
        static void Prepare(AudioSource source, AudioClip[] clips, LocalAudioCategory category)
        {
            if (source == null) return;
            source.playOnAwake = false; source.loop = false;
            source.spatialBlend = GameSettings.Base.AudienceFeedback.SpatialBlend;
            source.dopplerLevel = 0; source.spread = 120; source.minDistance = 10; source.maxDistance = 1000;
            // The audience is a large area source. Keep the feedback readable across the whole course.
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0, 1, 1, .75f));
            source.rolloffMode = AudioRolloffMode.Custom; source.reverbZoneMix = 0;
            source.pitch = 1; source.priority = 96;
            LocalAudioController.Register(source, category);
            if (clips == null) return;
            foreach (var clip in clips) if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }
        public bool Play(bool cheer)
        {
            var source = cheer ? cheerSource : booSource;
            var clips = cheer ? cheers : boos;
            float now = Time.unscaledTime;
            if (source == null || source.isPlaying || clips == null || clips.Length == 0 || now < (cheer ? nextCheer : nextBoo)) return false;
            int previous = cheer ? lastCheer : lastBoo;
            int index = previous < 0 ? Random.Range(0, clips.Length) : clips.Length == 1 ? 0 : (previous + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            var clip = clips[index];
            // Never decode/load on a reaction. An unready optional voice is skipped.
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded) return false;
            if (fading == source) { source.Stop(); fading = null; }
            var opposite = cheer ? booSource : cheerSource;
            if (opposite != null && opposite.isPlaying)
            {
                fading = opposite; fadingVolume = GameSettings.Base.AudienceFeedback.AudioVolume;
                fadeUntil = now + .12f;
            }
            var root = PerformanceBackgroundLoader.BackgroundRoot;
            lastSector = (lastSector + 1 + Random.Range(0, Seats.Length - 1)) % Seats.Length;
            source.transform.position = root != null ? root.TransformPoint(Seats[lastSector]) : Seats[lastSector];
            source.spatialBlend = GameSettings.Base.AudienceFeedback.SpatialBlend;
            LocalAudioController.SetSourceVolume(source, GameSettings.Base.AudienceFeedback.AudioVolume * Random.Range(.94f, 1f));
            source.clip = clip; source.Play(); PlayCount++;
            if (cheer) { lastCheer = index; nextCheer = now + GameSettings.Base.AudienceFeedback.AudioInterval; }
            else { lastBoo = index; nextBoo = now + GameSettings.Base.AudienceFeedback.AudioInterval; }
            return true;
        }
        public void Stop()
        {
            if (cheerSource != null) cheerSource.Stop();
            if (booSource != null) booSource.Stop();
            nextCheer = nextBoo = 0;
            fading = null;
        }
        void Update()
        {
            UpdateAmbience();
            if (fading != null)
            {
                float left = fadeUntil - Time.unscaledTime;
                LocalAudioController.SetSourceVolume(fading, fadingVolume * Mathf.Clamp01(left / .12f));
                if (left <= 0) { fading.Stop(); fading = null; }
            }
        }
        void UpdateAmbience()
        {
            if (ambienceSource == null || ambienceSource.clip == null) return;
            var settings = GameSettings.Base.AudienceFeedback;
            if (settings.AmbienceVolume <= 0)
            {
                ambienceLevel = 0; ambienceSource.Stop(); LocalAudioController.SetSourceVolume(ambienceSource, 0); return;
            }
            if (venueActive && !ambienceSource.isPlaying && ambienceSource.clip.loadState == AudioDataLoadState.Loaded)
            {
                ambienceSource.time = Random.Range(0, ambienceSource.clip.length);
                ambienceSource.Play(); AmbienceStartCount++;
            }
            bool reaction = (cheerSource != null && cheerSource.isPlaying) || (booSource != null && booSource.isPlaying);
            float target = venueActive ? settings.AmbienceVolume * (reaction ? settings.AmbienceDuck : 1) : 0;
            float seconds = venueActive && reaction ? .18f : settings.AmbienceFadeSeconds;
            ambienceLevel = Mathf.MoveTowards(ambienceLevel, target, Time.unscaledDeltaTime * settings.AmbienceVolume / seconds);
            LocalAudioController.SetSourceVolume(ambienceSource, ambienceLevel);
            if (!venueActive && ambienceLevel <= 0) ambienceSource.Stop();
        }
        void OnDisable()
        {
            Stop(); venueActive = false; ambienceLevel = 0;
            if (ambienceSource != null) ambienceSource.Stop();
        }
    }
}
