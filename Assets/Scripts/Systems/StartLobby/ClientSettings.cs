using UnityEngine;

namespace CurtainCall.StartLobby
{
    /// <summary>
    /// 이 PC에만 저장하는 핵심 설정(전체 음량·화면 모드). 게임 규칙 값(공통 세팅)과는 다르며 PlayerPrefs에 둔다.
    /// </summary>
    public static class ClientSettings
    {
        const string VolumeKey = "CurtainCall.Settings.Volume";
        const string FullscreenKey = "CurtainCall.Settings.Fullscreen";

        /// <summary>전체 음량(0~1).</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                AudioListener.volume = Volume;
            }
        }

        /// <summary>전체 화면(창 테두리 없는 전체 화면)인지.</summary>
        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                ApplyScreen();
            }
        }

        public static void ApplySaved()
        {
            AudioListener.volume = Volume;
            ApplyScreen();
        }

        public static void Save() => PlayerPrefs.Save();

        static void ApplyScreen()
        {
            if (Application.isEditor) return; // 에디터 Game 창은 바꾸지 않는다.
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }
    }
}
