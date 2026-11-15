using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.PerformanceBackground;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Presentation
{
    [DefaultExecutionOrder(1100)]
    sealed class PerformanceFinaleLighting : MonoBehaviour
    {
        const float CueStart = .86f;
        const float CueFull = .96f;
        const float FadeSeconds = 1.5f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        readonly Dictionary<Light, float> venueLights = new();
        readonly Dictionary<PerformanceBackgroundMotion.LightPulse, float> pulses = new();
        readonly Dictionary<NetworkPlayer, Light> followSpots = new();
        MaterialPropertyBlock beamProperties;
        readonly List<NetworkPlayer> inactivePlayers = new();
        TightropeCourse ropeCourse;
        HumanCannonDestination cannonFinish;
        MeshFilter beamTemplate;
        Scene performance;
        Scene venue;
        float cue;

        void Awake() => beamProperties = new MaterialPropertyBlock();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= AddToVenue;
            SceneManager.sceneLoaded += AddToVenue;
        }

        static void AddToVenue(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != PerformanceBackgroundLayout.ScenePath) return;
            var go = new GameObject("Performance Finale Lighting");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PerformanceFinaleLighting>();
        }

        void BindPerformance(Scene active)
        {
            RestoreLights();
            performance = active;
            ropeCourse = null;
            cannonFinish = null;
            if (active.name != "Tightrope" && active.name != "HumanCannon") return;
            foreach (var root in active.GetRootGameObjects())
            {
                if (active.name == "Tightrope") ropeCourse = root.GetComponentInChildren<TightropeCourse>();
                else cannonFinish = root.GetComponentInChildren<HumanCannonDestination>();
                if (ropeCourse != null || cannonFinish != null) break;
            }
        }

        void LateUpdate()
        {
            var active = SceneManager.GetActiveScene();
            if (active != performance) BindPerformance(active);
            if (ropeCourse == null && cannonFinish == null) return;

            float progress = 0f;
            foreach (var player in NetworkPlayer.All)
                if (IsPerformer(player)) progress = Mathf.Max(progress, Progress(player.transform.position));

            var loadedVenue = SceneManager.GetSceneByPath(PerformanceBackgroundLayout.ScenePath);
            if (!loadedVenue.isLoaded)
            {
                if (cue > 0f) RestoreLights();
                return;
            }
            if (cue <= 0f && progress <= CueStart) return;
            if (venue != loadedVenue) CacheLights(loadedVenue);

            float target = Mathf.InverseLerp(CueStart, CueFull, progress);
            cue = Mathf.MoveTowards(cue, target, Time.unscaledDeltaTime / FadeSeconds);
            if (cue <= 0f)
            {
                RestoreLights();
                return;
            }

            float blend = Mathf.SmoothStep(0f, 1f, cue);
            foreach (var pair in venueLights)
                if (pair.Key != null) pair.Key.intensity = pair.Value * Mathf.Lerp(1f,
                    pair.Key.name.StartsWith("Route Key ") ? .55f : .23f, blend);

            foreach (var pair in pulses)
                if (pair.Key.target != null) pair.Key.restIntensity = pair.Value * Mathf.Lerp(1f, .23f, blend);

            UpdateFollowSpots(blend);
        }

        bool IsPerformer(NetworkPlayer player) =>
            player != null && player.IsSpawned && player.State == PlayerState.Normal;

        float Progress(Vector3 position)
        {
            if (ropeCourse != null)
            {
                if (ropeCourse.FinishDistance <= 0f || position.y < ropeCourse.RopeHeightAt(position) - 2f) return 0f;
                return ropeCourse.GetDistance(position) / ropeCourse.FinishDistance;
            }
            var cannon = HumanCannonSystem.Current;
            if (cannonFinish == null || cannonFinish.Zone == null || cannon == null || cannon.StationCount == 0
                || cannon.Station(0) == null) return 0f;
            Vector3 start = cannon.Station(0).Muzzle.position;
            Vector3 finish = cannonFinish.Zone.transform.position;
            if (position.y < finish.y - 8f) return 0f;
            Vector3 axis = Vector3.ProjectOnPlane(finish - start, Vector3.up);
            return axis.sqrMagnitude > .001f ? Vector3.Dot(position - start, axis) / axis.sqrMagnitude : 0f;
        }

        void CacheLights(Scene loadedVenue)
        {
            RestoreLights();
            venue = loadedVenue;
            foreach (var root in venue.GetRootGameObjects())
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                    if (filter.name == "VisibleShowBeam")
                    {
                        beamTemplate = filter;
                        break;
                    }
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    venueLights.Add(light, light.intensity);
                foreach (var motion in root.GetComponentsInChildren<PerformanceBackgroundMotion>(true))
                    foreach (var pulse in motion.practicalLights)
                    {
                        if (pulse?.target == null || pulses.ContainsKey(pulse)) continue;
                        pulses.Add(pulse, pulse.restIntensity);
                        venueLights.Remove(pulse.target);
                    }
            }
        }

        void UpdateFollowSpots(float blend)
        {
            inactivePlayers.Clear();
            foreach (var pair in followSpots)
                if (!IsPerformer(pair.Key)) inactivePlayers.Add(pair.Key);
            foreach (var player in inactivePlayers)
            {
                Destroy(followSpots[player].gameObject);
                followSpots.Remove(player);
            }

            foreach (var player in NetworkPlayer.All)
            {
                if (!IsPerformer(player)) continue;
                if (!followSpots.TryGetValue(player, out var spot))
                {
                    var source = new GameObject("Finale Follow Spot");
                    source.transform.SetParent(transform, false);
                    source.transform.localScale = Vector3.one / PerformanceBackgroundLayout.Scale;
                    spot = source.AddComponent<Light>();
                    spot.type = LightType.Spot;
                    spot.range = 11f;
                    spot.spotAngle = 30f;
                    spot.innerSpotAngle = 17f;
                    spot.color = new Color(1f, .90f, .75f);
                    spot.shadows = LightShadows.None;
                    AddVisibleBeam(source.transform);
                    followSpots.Add(player, spot);
                }
                Vector3 focus = player.transform.position + Vector3.up * .8f;
                Vector3 offset = Vector3.up * 3.5f - Vector3.forward * 2.5f;
                spot.transform.SetPositionAndRotation(focus + offset, Quaternion.LookRotation(-offset));
                spot.intensity = 55f * blend;
                if (spot.transform.childCount > 0)
                {
                    var beam = spot.transform.GetChild(0).GetComponent<MeshRenderer>();
                    beamProperties.SetColor(BaseColor, new Color(1f, .9f, .76f, .075f * blend));
                    beam.SetPropertyBlock(beamProperties);
                }
            }
        }

        void AddVisibleBeam(Transform source)
        {
            if (beamTemplate == null || !beamTemplate.TryGetComponent(out MeshRenderer templateRenderer)) return;
            var beam = new GameObject("Follow Spot Beam");
            beam.transform.SetParent(source, false);
            beam.transform.localScale = new Vector3(.031f, .031f, .034f);
            beam.AddComponent<MeshFilter>().sharedMesh = beamTemplate.sharedMesh;
            var renderer = beam.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = templateRenderer.sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void OnDisable() => RestoreLights();

        void RestoreLights()
        {
            foreach (var pair in venueLights)
                if (pair.Key != null) pair.Key.intensity = pair.Value;
            foreach (var pair in pulses)
                if (pair.Key.target != null) pair.Key.restIntensity = pair.Value;
            foreach (var spot in followSpots.Values)
                if (spot != null)
                {
                    spot.enabled = false;
                    Destroy(spot.gameObject);
                }
            venueLights.Clear();
            pulses.Clear();
            followSpots.Clear();
            beamTemplate = null;
            venue = default;
            cue = 0f;
        }
    }
}
