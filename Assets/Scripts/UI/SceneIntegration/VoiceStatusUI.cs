using CurtainCall.Settings;
using CurtainCall.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Production microphone status; SpeechDetected comes from the real voice channel.</summary>
    public sealed class VoiceStatusUI : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [SerializeField] MicrophoneStatusIcon icon;
        public void Configure(Button toggle, TMP_Text text) { button = toggle; label = text; }
        public void ConfigureIcon(MicrophoneStatusIcon value) => icon = value;
        void Awake() => button.onClick.AddListener(() => VoiceChat.Instance?.ToggleMicMuted());
        void Update()
        {
            var voice = VoiceChat.Instance;
            bool visible = CommonMenuUI.HasSupportedSpace && !CommonMenuUI.IsStart && !SceneCurtainTransition.IsActive;
            button.gameObject.SetActive(visible);
            if (!visible || voice == null) return;
            bool muted = voice.IsMicMuted || voice.MicVolume <= 0;
            label.text = GameSettings.Base.VoiceChat.MuteKey.ToString();
            label.color = new Color32(240, 238, 220, 255);
            if (icon != null) icon.SetMuted(muted);
            button.interactable = !CurtainCall.Player.InputGate.IsRequested;
        }
    }
}
