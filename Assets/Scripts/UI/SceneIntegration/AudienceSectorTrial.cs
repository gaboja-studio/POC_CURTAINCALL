using System.Collections.Generic;
using CurtainCall.PerformanceBackground;
using CurtainCall.Reward;
using CurtainCall.Settings;
using Unity.Profiling;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Optional single-sector experiment. One baked mesh, GPU pose offsets, no per-person objects.</summary>
    public sealed class AudienceSectorTrial : MonoBehaviour
    {
        [SerializeField] GameObject sectorPrefab;
        static readonly ProfilerMarker ObserveMarker = new("AudienceSectorTrial.Observe");
        static readonly int Reaction = Shader.PropertyToID("_Reaction");
        static readonly int Started = Shader.PropertyToID("_Started");
        readonly Dictionary<string, RewardSegment> previous = new(4);
        MaterialPropertyBlock block;
        Transform parent;
        GameObject sector;
        MeshRenderer mesh;
        Light seatingLight;
        float reactionStarted = -100;
        string performance;
        long revision;
        RewardRules rules;
        float nextReaction;
        float pendingAt;
        int pending;
        public int ReactionCount { get; private set; }
        public int LastDirection { get; private set; }
        public double CreationMilliseconds { get; private set; }
        public float LastReactionLatencySeconds { get; private set; }
        public MeshRenderer SectorRenderer => mesh;
        public void Configure(GameObject prefab) => sectorPrefab = prefab;
        void Awake() => block = new MaterialPropertyBlock();
        void LateUpdate()
        {
            bool enabled = GameSettings.Base.AudienceFeedback.CrowdTrial;
            var root = PerformanceBackgroundLoader.BackgroundRoot;
            if (parent != root)
            {
                Clear();
                if (sector != null) Destroy(sector);
                sector = null; mesh = null; seatingLight = null; parent = root;
            }
            // Creation is during the scene's presentation setup, never within Observe/reaction handling.
            if (enabled && sector == null && parent != null && sectorPrefab != null)
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                sector = Instantiate(sectorPrefab, parent, false);
                mesh = sector.GetComponent<MeshRenderer>();
                seatingLight = sector.GetComponentInChildren<Light>(true);
                CreationMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            }
            if (sector != null && sector.activeSelf != enabled) sector.SetActive(enabled);
            if (!enabled) { pending = 0; return; }
            if (pending != 0 && mesh != null && Time.unscaledTime >= pendingAt && Time.unscaledTime >= nextReaction)
            {
                LastDirection = pending > 0 ? 1 : -1;
                LastReactionLatencySeconds = Time.unscaledTime - pendingAt + .12f;
                block.SetFloat(Reaction, LastDirection);
                block.SetFloat(Started, Time.time);
                reactionStarted = Time.time;
                mesh.SetPropertyBlock(block);
                pending = 0; ReactionCount++;
                nextReaction = Time.unscaledTime + 1.9f;
            }
            UpdateSeatingLight();
        }
        void UpdateSeatingLight()
        {
            if (seatingLight == null) return;
            float age = Time.time - reactionStarted;
            float envelope = Mathf.Clamp01(age / .28f) * Mathf.Clamp01((1.8f-age) / .55f);
            seatingLight.enabled = age >= 0 && envelope > 0 && LastDirection != 0;
            if (!seatingLight.enabled) return;
            bool cheer = LastDirection > 0;
            // Same muted lamp colour. A broad passing wash cheers; a fixed narrow pool presses down.
            float x = cheer ? Mathf.Lerp(58,132,Mathf.Clamp01(age/1.4f)) : 95;
            var aim = new Vector3(x,35,432);
            seatingLight.transform.localRotation = Quaternion.LookRotation(aim-seatingLight.transform.localPosition);
            seatingLight.spotAngle = cheer ? 85 : 40;
            seatingLight.innerSpotAngle = cheer ? 45 : 24;
            seatingLight.intensity = (cheer ? 9000 : 6000) * envelope;
        }
        public void Observe(RewardSnapshot snapshot)
        {
            using var marker = ObserveMarker.Auto();
            if (snapshot?.Performance == null || !snapshot.IsOpen) { Clear(); return; }
            if (performance == snapshot.Performance.Id && snapshot.Revision <= revision) return;
            bool continuous = performance == snapshot.Performance.Id && rules.Equals(snapshot.Rules);
            if (!continuous)
            {
                pending = 0; nextReaction = 0; reactionStarted = -100;
                if (seatingLight != null) seatingLight.enabled = false;
                if (mesh != null) { block.Clear(); mesh.SetPropertyBlock(block); }
            }
            int selected = 0;
            for (int i = 0; i < snapshot.Segments.Count; i++)
            {
                var person = snapshot.Segments[i];
                if (continuous && previous.TryGetValue(person.ParticipantId, out var before)
                    && before.Connected && person.Connected && !before.Dead && !person.Dead && before.Slot == person.Slot
                    && person.ReactionSequence > before.ReactionSequence
                    && (person.ReactionDelta > 0 || person.ReactionAmount < 0))
                {
                    // At most one pulse per snapshot, larger delta wins, boo breaks equal ties.
                    int value = person.ReactionAmount == 0 ? 1 : person.ReactionAmount;
                    if (System.Math.Abs(value) > System.Math.Abs(selected) || (System.Math.Abs(value) == System.Math.Abs(selected) && value < selected)) selected = value;
                }
            }
            previous.Clear();
            for (int i = 0; i < snapshot.Segments.Count; i++)
            { var person = snapshot.Segments[i]; previous[person.ParticipantId] = person; }
            performance = snapshot.Performance.Id; revision = snapshot.Revision; rules = snapshot.Rules;
            if (selected != 0 && GameSettings.Base.AudienceFeedback.CrowdTrial && mesh != null && mesh.gameObject.activeInHierarchy)
            {
                // Batch near-simultaneous players, then finish one readable gesture before the next.
                if (pending == 0) pendingAt = Time.unscaledTime + .12f;
                if (System.Math.Abs(selected) > System.Math.Abs(pending)
                    || (System.Math.Abs(selected) == System.Math.Abs(pending) && selected < pending)) pending = selected;
            }
        }
        public void Clear()
        {
            previous.Clear(); performance = null; revision = 0; nextReaction = 0; pending = 0; LastDirection = 0; reactionStarted = -100;
            if (seatingLight != null) seatingLight.enabled = false;
            if (mesh != null) { block.Clear(); mesh.SetPropertyBlock(block); }
        }
        void OnDisable()
        {
            Clear(); if (sector != null) Destroy(sector); sector = null; mesh = null; seatingLight = null; parent = null;
        }
    }
}
