using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.StartLobby
{
    /// <summary>시작 씬·로비 씬 공용 설정 화면(핵심만: 전체 음량, 화면 모드, 조작 안내).</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        public Slider volume;
        public TMP_Text volumeValue;
        public Button screenMode;
        public TMP_Text screenModeLabel;
        public Button close;

        /// <summary>닫기를 눌렀다.</summary>
        public event System.Action Closed;

        void Awake()
        {
            if (volume != null) volume.onValueChanged.AddListener(SetVolume);
            if (screenMode != null) screenMode.onClick.AddListener(ToggleScreen);
            if (close != null) close.onClick.AddListener(Close);
        }

        void OnEnable() => Refresh();

        void OnDisable() => ClientSettings.Save();

        void OnDestroy()
        {
            if (volume != null) volume.onValueChanged.RemoveListener(SetVolume);
            if (screenMode != null) screenMode.onClick.RemoveListener(ToggleScreen);
            if (close != null) close.onClick.RemoveListener(Close);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void SetVolume(float value)
        {
            ClientSettings.Volume = value;
            Refresh();
        }

        void ToggleScreen()
        {
            ClientSettings.Fullscreen = !ClientSettings.Fullscreen;
            Refresh();
        }

        void Refresh()
        {
            if (volume != null) volume.SetValueWithoutNotify(ClientSettings.Volume);
            if (volumeValue != null) volumeValue.text = $"{Mathf.RoundToInt(ClientSettings.Volume * 100)}%";
            if (screenModeLabel != null) screenModeLabel.text = ClientSettings.Fullscreen ? "전체 화면" : "창 모드";
        }
    }
}
