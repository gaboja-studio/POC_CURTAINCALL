using System.Collections.Generic;
using CurtainCall.StartLobby;
using Unity.Services.Vivox.AudioTaps;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Settings
{
    public enum LocalAudioCategory { Sfx, Bgm, Voice }

    /// <summary>
    /// 네이티브 Vivox는 Unity 리스너를 거치지 않는다. Unity 음원은 기본 SFX로 분류하며,
    /// BGM은 재생 전에 Register해야 한다. 씬·private 에셋 변경은 필요하지 않다.
    /// </summary>
    public sealed class LocalAudioController : MonoBehaviour
    {
        sealed class Route
        {
            public AudioSource Source;
            public LocalAudioCategory Category;
            public float BaseVolume;
            public float AppliedVolume;
            public float Multiplier = 1f;
        }

        static LocalAudioController instance;
        readonly Dictionary<AudioSource, Route> routes = new();
        readonly List<AudioSource> removed = new();
        float nextDiscovery;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() => EnsureExists();

        static LocalAudioController EnsureExists()
        {
            if (instance != null) return instance;
            var host = new GameObject(nameof(LocalAudioController)) { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(host);
            return host.AddComponent<LocalAudioController>();
        }

        /// <summary>Play/PlayOneShot 전에 호출하면 즉시 적용한다(자동 탐색은 최대 .25초 지연).
        /// Voice는 분류별 음량을 거치지 않으며 Vivox AudioTap은 항상 제외한다.</summary>
        public static void Register(AudioSource source, LocalAudioCategory category)
        {
            if (source == null || source.GetComponent<VivoxAudioTap>() != null) return;
            var controller = EnsureExists();
            if (!controller.routes.TryGetValue(source, out var route))
            {
                route = new Route { Source = source, BaseVolume = source.volume, AppliedVolume = source.volume };
                controller.routes.Add(source, route);
            }
            route.Category = category;
            Apply(route);
        }

        /// <summary>분류가 음소거된 동안에도 원래 음원의 페이드·음량을 변경한다.
        /// source.volume 직접 수정도 감지하지만 0에서는 음소거 전 값을 복구할 수 없다.</summary>
        public static void SetSourceVolume(AudioSource source, float volume)
        {
            if (source == null) return;
            var controller = EnsureExists();
            if (!controller.routes.TryGetValue(source, out var route))
            {
                Register(source, LocalAudioCategory.Sfx);
                if (!controller.routes.TryGetValue(source, out route)) return;
            }
            route.BaseVolume = Mathf.Clamp01(volume);
            Apply(route);
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            AudioListener.volume = ClientSettings.Volume;
            ClientSettings.AudioChanged += Refresh;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Discover();

        void Discover()
        {
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!routes.ContainsKey(source)) Register(source, LocalAudioCategory.Sfx);
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= nextDiscovery)
            {
                nextDiscovery = Time.unscaledTime + .25f;
                Discover();
            }
            Refresh();
        }

        void Refresh()
        {
            removed.Clear();
            foreach (var pair in routes)
            {
                var route = pair.Value;
                if (route.Source == null) { removed.Add(pair.Key); continue; }
                if (route.Source.GetComponent<VivoxAudioTap>() != null)
                {
                    route.Source.volume = route.BaseVolume;
                    removed.Add(pair.Key);
                    continue;
                }
                // 이미 적용한 값에 다시 곱하지 않고 기존 페이드의 상대 변화를 보존한다.
                if (!Mathf.Approximately(route.Source.volume, route.AppliedVolume))
                    route.BaseVolume = route.Multiplier > 0f
                        ? Mathf.Clamp01(route.Source.volume / route.Multiplier) : route.Source.volume;
                Apply(route);
            }
            foreach (var source in removed) routes.Remove(source);
        }

        static void Apply(Route route)
        {
            route.Multiplier = route.Category == LocalAudioCategory.Bgm ? ClientSettings.BgmVolume
                : route.Category == LocalAudioCategory.Sfx ? ClientSettings.SfxVolume : 1f;
            route.AppliedVolume = route.BaseVolume * route.Multiplier;
            route.Source.volume = route.AppliedVolume;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            ClientSettings.AudioChanged -= Refresh;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (var route in routes.Values)
                if (route.Source != null) route.Source.volume = route.BaseVolume;
            instance = null;
        }
    }
}
