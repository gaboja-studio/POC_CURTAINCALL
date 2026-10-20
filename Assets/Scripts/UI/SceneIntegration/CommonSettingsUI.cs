using System;
using System.Linq;
using CurtainCall.StartLobby;
using CurtainCall.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>C14 owns drafts. Only an accepted Apply writes local game/voice settings.</summary>
    public sealed class CommonSettingsUI : MonoBehaviour
    {
        [SerializeField] SettingsPanelView main;
        [SerializeField] SettingsPanelView teammates;
        [SerializeField] CanvasGroup mainGroup;
        [SerializeField] Button teammateButton;
        [SerializeField] TMP_Text controlsHint;
        [SerializeField] TMP_Text teammateHint;
        Action closed;
        bool closing;
        string[] peerIds = Array.Empty<string>();
        public SettingsPanelView Main => main;
        public SettingsPanelView Teammates => teammates;
        public bool IsOpen => main.IsVisible || teammates.IsVisible;

        public void Configure(SettingsPanelView view, SettingsPanelView peerView, CanvasGroup group,
            Button peerButton, TMP_Text controls, TMP_Text hint)
        { main = view; teammates = peerView; mainGroup = group; teammateButton = peerButton; controlsHint = controls; teammateHint = hint; }

        void Awake()
        {
            main.ApplyRequested += Apply;
            teammates.ApplyRequested += ApplyTeammates;
            main.Closed.AddListener(MainClosed);
            teammates.Closed.AddListener(TeammatesClosed);
            teammateButton.onClick.AddListener(OpenTeammates);
        }
        public void Open(bool gameplay, Action onClosed)
        {
            Close(); closed = onClosed;
            var voice = VoiceChat.Instance;
            mainGroup.alpha = 1; mainGroup.interactable = mainGroup.blocksRaycasts = true;
            main.Open(new[] {
                new SettingsPageDefinition("game", "게임", Slider("volume", "게임 음량", ClientSettings.Volume),
                    Toggle("fullscreen", "전체 화면", ClientSettings.Fullscreen)),
                new SettingsPageDefinition("mic", "내 마이크", Slider("micVolume", "마이크 크기", voice != null ? voice.MicVolume : 1),
                    Toggle("micMuted", "마이크 음소거", voice != null && voice.IsMicMuted)),
                new SettingsPageDefinition("team", "팀원 음성", Slider("teamVolume", "팀원 전체 음량", voice != null ? voice.TeamVolume : 1),
                    Toggle("teamMuted", "팀원 전체 음소거", voice != null && voice.IsTeamMuted))
            }, gameplay);
            RefreshExtras();
        }
        static SettingsRowDefinition Slider(string id, string title, float value) => new(id, title, SettingsValueKind.Slider, value);
        static SettingsRowDefinition Toggle(string id, string title, bool value) => new(id, title, SettingsValueKind.Toggle, value ? 1 : 0);
        static float Value(SettingsSnapshot snapshot, string id) => snapshot.TryGet(id, out var value) ? value.Value : 0;
        void Apply(SettingsSnapshot snapshot)
        {
            if (!main.IsVisible || main.SessionVersion != snapshot.SessionVersion) return;
            ClientSettings.Volume = Value(snapshot, "volume");
            ClientSettings.Fullscreen = Value(snapshot, "fullscreen") >= .5f;
            ClientSettings.Save();
            VoiceChat.Instance?.ApplyAudioPreferences(Value(snapshot, "micVolume"), Value(snapshot, "micMuted") >= .5f,
                Value(snapshot, "teamVolume"), Value(snapshot, "teamMuted") >= .5f);
            main.AcceptApplied(snapshot.SessionVersion);
        }

        public void OpenTeammates()
        {
            if (!main.IsVisible || teammates.IsVisible || main.CurrentPage != 2) return;
            var voice = VoiceChat.Instance;
            if (voice == null) return;
            var peers = voice.Participants.Where(p => !p.IsSelf).Take(3).ToArray();
            if (peers.Length == 0) return;
            peerIds = peers.Select(p => p.PlayerId).ToArray();
            var pages = peers.Select((p, i) => new SettingsPageDefinition(p.PlayerId, PeerName(p.DisplayName),
                Slider($"peerVolume{i}", "내게 들리는 음량", voice.ParticipantVolume(p.PlayerId)),
                Toggle($"peerMuted{i}", "이 단원 음소거", voice.ParticipantMuted(p.PlayerId)))).ToArray();
            mainGroup.alpha = 0; mainGroup.interactable = mainGroup.blocksRaycasts = false;
            teammates.Open(pages, true);
        }
        static string PeerName(string name) => name != null && name.Length == 2 && name[0] == 'P' ? $"단원 {name[1]}" : "단원";
        void ApplyTeammates(SettingsSnapshot snapshot)
        {
            if (!teammates.IsVisible || teammates.SessionVersion != snapshot.SessionVersion) return;
            var voice = VoiceChat.Instance;
            if (voice == null) { teammates.RejectApply(snapshot.SessionVersion, "음성 연결을 확인해 주세요"); return; }
            for (int i = 0; i < peerIds.Length; i++)
                voice.SetParticipantPreference(peerIds[i], Value(snapshot, $"peerVolume{i}"), Value(snapshot, $"peerMuted{i}") >= .5f);
            teammates.AcceptApplied(snapshot.SessionVersion);
        }
        void LateUpdate() => RefreshExtras();
        void RefreshExtras()
        {
            controlsHint.gameObject.SetActive(main.IsVisible && main.CurrentPage == 0);
            bool team = main.IsVisible && main.CurrentPage == 2;
            teammateButton.gameObject.SetActive(team);
            teammateHint.gameObject.SetActive(team);
            bool any = VoiceChat.Instance != null && VoiceChat.Instance.Participants.Any(p => !p.IsSelf);
            teammateButton.interactable = any;
            teammateHint.text = any ? "현재 음성 채널의 단원만 표시합니다" : "현재 음성 채널에 다른 단원이 없습니다";
            // The supplied view rebuilds navigation when values change. Include our extra button afterwards.
            if (team && any && !teammates.IsVisible)
            {
                var toggles = main.GetComponentsInChildren<Toggle>().Where(t => t.IsInteractable()).ToArray();
                var cancel = main.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "50_Cancel");
                if (toggles.Length > 0 && cancel != null)
                {
                    var last = toggles[toggles.Length - 1];
                    var nav = last.navigation; nav.selectOnDown = teammateButton; last.navigation = nav;
                    teammateButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnDown = cancel };
                    nav = cancel.navigation; nav.selectOnUp = teammateButton; cancel.navigation = nav;
                }
            }
        }
        public void CancelTop()
        { if (teammates.IsVisible) teammates.Cancel(); else if (main.IsVisible) main.Cancel(); }
        public void Close()
        {
            closing = true;
            teammates.Hide(); main.Hide();
            closing = false; closed = null;
        }
        void MainClosed()
        {
            if (closing) return;
            var callback = closed; closed = null; callback?.Invoke();
        }
        void TeammatesClosed()
        {
            if (closing || !main.IsVisible) return;
            mainGroup.alpha = 1; mainGroup.interactable = mainGroup.blocksRaycasts = true;
            main.ShowPage(2);
        }
        void OnDestroy()
        {
            main.ApplyRequested -= Apply; teammates.ApplyRequested -= ApplyTeammates;
            main.Closed.RemoveListener(MainClosed); teammates.Closed.RemoveListener(TeammatesClosed);
        }
    }
}
