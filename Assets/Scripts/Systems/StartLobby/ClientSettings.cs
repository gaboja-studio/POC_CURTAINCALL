using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.StartLobby
{
    /// <summary>이 PC의 음량·그래픽 설정. 팀의 게임 규칙과 분리해 PlayerPrefs에 저장한다.</summary>
    public static class ClientSettings
    {
        const string VolumeKey = "CurtainCall.Settings.Volume";
        const string BgmVolumeKey = "CurtainCall.Settings.BgmVolume";
        const string SfxVolumeKey = "CurtainCall.Settings.SfxVolume";

        /// <summary>로컬 음량 변경. 디스크 저장은 기존처럼 Save로 한다.</summary>
        public static event System.Action AudioChanged;
        const string FullscreenKey = "CurtainCall.Settings.Fullscreen";
        const string ScreenModeKey = "CurtainCall.Settings.ScreenMode";
        const string WidthKey = "CurtainCall.Settings.ResolutionWidth";
        const string HeightKey = "CurtainCall.Settings.ResolutionHeight";
        const string FrameRateKey = "CurtainCall.Settings.FrameRateLimit";
        static readonly IReadOnlyList<int> frameRateOptions = System.Array.AsReadOnly(new[] { 30, 60, 90, 120, 144, 165, 240, -1 });

        public static IReadOnlyList<int> AvailableFrameRateLimits => frameRateOptions;

        /// <summary>주사율 중복과 잘못된 크기를 제외한 지원 해상도(가로·세로 오름차순).</summary>
        public static Resolution[] AvailableResolutions
        {
            get
            {
                var result = new List<Resolution>();
                foreach (var resolution in Screen.resolutions)
                {
                    if (resolution.width <= 0 || resolution.height <= 0) continue;
                    if (result.Exists(item => item.width == resolution.width && item.height == resolution.height)) continue;
                    result.Add(resolution);
                }
                if (result.Count == 0) result.Add(DesktopResolution());
                result.Sort((a, b) => a.width != b.width ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
                return result.ToArray();
            }
        }

        /// <summary>전체 음량(0~1).</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                AudioListener.volume = Volume;
                AudioChanged?.Invoke();
            }
        }

        public static float BgmVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
            set { PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value)); AudioChanged?.Invoke(); }
        }

        public static float SfxVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            set { PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value)); AudioChanged?.Invoke(); }
        }

        /// <summary>기존 bool API. true는 전체 화면 창, false는 창 모드로 설정한다.</summary>
        public static bool Fullscreen
        {
            get => ScreenMode != FullScreenMode.Windowed;
            set => ScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public static FullScreenMode ScreenMode
        {
            get
            {
                // 기존 true의 의미는 독점 전체 화면이 아니라 전체 화면 창이었다.
                if (!PlayerPrefs.HasKey(ScreenModeKey))
                    return PlayerPrefs.GetInt(FullscreenKey, 1) == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                return NormalizeMode((FullScreenMode)PlayerPrefs.GetInt(ScreenModeKey));
            }
            set => SetGraphics(value, ResolutionWidth, ResolutionHeight, FrameRateLimit);
        }

        public static int ResolutionWidth => SavedResolution().width;
        public static int ResolutionHeight => SavedResolution().height;

        /// <summary>기본 60. -1은 제한 없음이며 VSync는 사용하지 않는다.</summary>
        public static int FrameRateLimit
        {
            get => NormalizeFrameRate(PlayerPrefs.GetInt(FrameRateKey, 60));
            set
            {
                PlayerPrefs.SetInt(FrameRateKey, NormalizeFrameRate(value));
                ApplyFrameRate();
            }
        }

        /// <summary>유효한 값으로 보정해 로컬 설정과 실행 환경에 반영한다. 디스크 저장은 Save로 한다.</summary>
        public static void SetGraphics(FullScreenMode mode, int width, int height, int fps)
        {
            mode = NormalizeMode(mode);
            var resolution = ResolveResolution(width, height);
            PlayerPrefs.SetInt(ScreenModeKey, (int)mode);
            PlayerPrefs.SetInt(FullscreenKey, mode == FullScreenMode.Windowed ? 0 : 1);
            PlayerPrefs.SetInt(WidthKey, resolution.width);
            PlayerPrefs.SetInt(HeightKey, resolution.height);
            PlayerPrefs.SetInt(FrameRateKey, NormalizeFrameRate(fps));
            ApplyScreen();
            ApplyFrameRate();
        }

        public static void ApplySaved()
        {
            AudioListener.volume = Volume;
            AudioChanged?.Invoke();
            // 키 이관과 현재 모니터에서 지원하지 않는 저장 해상도 복구도 같은 경로로 처리한다.
            SetGraphics(ScreenMode, ResolutionWidth, ResolutionHeight, FrameRateLimit);
        }

        public static void Save() => PlayerPrefs.Save();

        internal static void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRateLimit;
        }

        static FullScreenMode NormalizeMode(FullScreenMode mode) =>
            mode == FullScreenMode.ExclusiveFullScreen || mode == FullScreenMode.Windowed
                ? mode : FullScreenMode.FullScreenWindow;

        static int NormalizeFrameRate(int fps)
        {
            foreach (int option in frameRateOptions)
                if (fps == option) return fps;
            return 60;
        }

        static Resolution SavedResolution() => ResolveResolution(PlayerPrefs.GetInt(WidthKey, 0), PlayerPrefs.GetInt(HeightKey, 0));

        static Resolution ResolveResolution(int width, int height)
        {
            var available = AvailableResolutions;
            foreach (var resolution in available)
                if (resolution.width == width && resolution.height == height) return resolution;
            var desktop = DesktopResolution();
            foreach (var resolution in available)
                if (resolution.width == desktop.width && resolution.height == desktop.height) return resolution;
            return available[available.Length - 1];
        }

        static Resolution DesktopResolution()
        {
            // currentResolution은 독점 전체 화면에서 선택된 크기일 수 있으므로 디스플레이의 시스템 크기를 우선한다.
            var desktop = Screen.currentResolution;
            if (Display.main != null && Display.main.systemWidth > 0 && Display.main.systemHeight > 0)
            {
                desktop.width = Display.main.systemWidth;
                desktop.height = Display.main.systemHeight;
            }
            if (desktop.width > 0 && desktop.height > 0) return desktop;
            return new Resolution { width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height) };
        }

        static void ApplyScreen()
        {
            if (Application.isEditor) return; // 에디터 Game 창은 바꾸지 않는다.
            // 전체 화면 창은 선택 해상도를 보존하되 실제 크기는 데스크톱을 따른다.
            var resolution = ScreenMode == FullScreenMode.FullScreenWindow ? DesktopResolution() : SavedResolution();
            Screen.SetResolution(resolution.width, resolution.height, ScreenMode);
        }
    }
}
