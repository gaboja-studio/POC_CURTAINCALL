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
        // 로딩 커튼(전환 UI 200)과 암전(1000) 위에 마이크 상태를 띄운다(2026-11-19 사용자: 로딩 중에도 마이크 끄기/켜기).
        const int LoadingSortingOrder = 1010;
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [SerializeField] MicrophoneStatusIcon icon;
        Canvas canvas;
        int baseSortingOrder;
        public void Configure(Button toggle, TMP_Text text) { button = toggle; label = text; }
        public void ConfigureIcon(MicrophoneStatusIcon value) => icon = value;
        void Awake()
        {
            button.onClick.AddListener(() => VoiceChat.Instance?.ToggleMicMuted());
            canvas = GetComponent<Canvas>();
            if (canvas != null) baseSortingOrder = canvas.sortingOrder;
        }
        void Update()
        {
            var voice = VoiceChat.Instance;
            bool loading = SceneCurtainTransition.IsActive;
            bool visible = CommonMenuUI.HasSupportedSpace && !CommonMenuUI.IsStart;
            button.gameObject.SetActive(visible);
            int order = loading ? LoadingSortingOrder : baseSortingOrder;
            if (canvas != null && canvas.sortingOrder != order) canvas.sortingOrder = order;
            if (!visible || voice == null) return;
            bool muted = voice.IsMicMuted || voice.MicVolume <= 0;
            label.text = GameSettings.Base.VoiceChat.MuteKey.ToString();
            label.color = new Color32(240, 238, 220, 255);
            if (icon != null) icon.SetMuted(muted);
            // 카메라 안내·로딩처럼 게임 입력만 막힌 때도 누를 수 있다. 공통 메뉴가 열렸을 때만 막는다.
            button.interactable = !VoiceChat.IsMuteKeyBlocked;
        }
    }
}
