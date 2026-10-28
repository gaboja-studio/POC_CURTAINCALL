using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.StartLobby;
using CurtainCall.Campaign;
using CurtainCall.Player;
using CurtainCall.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>설정 편집값을 탭 사이에 보존하고 적용할 때만 로컬 서비스에 전달한다.</summary>
    public sealed class CommonSettingsUI : MonoBehaviour
    {
        // 기존 씬의 C14 인스턴스와 참조를 보존한다.
        [SerializeField] SettingsPanelView main;
        [SerializeField] SettingsPanelView teammates;
        [SerializeField] CanvasGroup mainGroup;
        [SerializeField] UnityEngine.UI.Button teammateButton;
        [SerializeField] TMP_Text controlsHint;
        [SerializeField] TMP_Text teammateHint;
        SettingsTabsView view;
        SettingsExtensionPanel extension;
        UnityEngine.UI.Button abandonButton;
        Action closed;
        bool gameplayContext, confirmingAbandon;
        CampaignSnapshot abandonContext;
        Resolution[] resolutions;
        int resolutionIndex, fpsIndex, modeIndex;
        float volume, bgmVolume, sfxVolume, micVolume, teamVolume;
        bool micMuted, teamMuted;
        readonly Dictionary<string, (float volume, bool muted)> peers = new();
        InputActionAsset inputDraft;
        InputAction captureAction;
        int captureIndex, captureFrame;
        UnityEngine.UI.Button captureButton;
        readonly Dictionary<string, UnityEngine.UI.Button> bindingButtons = new();
        Action<string, bool> modeText, resolutionText, fpsText, inputDeviceText, outputDeviceText;
        string[] inputIds = Array.Empty<string>(), outputIds = Array.Empty<string>();
        string[] inputNames = Array.Empty<string>(), outputNames = Array.Empty<string>();
        string inputDeviceId, outputDeviceId;
        bool devicesTouched, busy, applying;
        int session;
        VoiceChat deviceVoice;
        static readonly FullScreenMode[] ScreenModes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        static readonly string[] ScreenLabels = { "전체 화면", "전체 화면 창", "창 모드" };
        public bool IsExtensionOpen => extension != null && extension.IsOpen;
        public bool IsCapturingBinding => captureAction != null;
        public SettingsTabsView Main => view;
        public SettingsPanelView Teammates => teammates;
        public bool IsOpen => view != null && view.IsVisible;

        public void Configure(SettingsPanelView original, SettingsPanelView peerView, CanvasGroup group,
            UnityEngine.UI.Button peerButton, TMP_Text controls, TMP_Text hint)
        { main = original; teammates = peerView; mainGroup = group; teammateButton = peerButton; controlsHint = controls; teammateHint = hint; }
        void Awake()
        {
            main.Hide(); teammates.Hide();
            var root = new GameObject("SettingsTabsCanvas", typeof(RectTransform)); root.transform.SetParent(transform, false);
            view = root.AddComponent<SettingsTabsView>(); view.Initialize(controlsHint.font);
            view.PageChanged += _ => CancelBindingCapture();
            extension = new SettingsExtensionPanel(transform, controlsHint.font);
        }
        public void Open(bool gameplay, Action onClosed)
        {
            Close(); closed = onClosed; devicesTouched = false; inputDeviceId = outputDeviceId = null; busy = false;
            var run = CampaignRun.Instance;
            gameplayContext = gameplay && run != null && run.State.Phase != CampaignPhase.None;
            volume = ClientSettings.Volume; bgmVolume = ClientSettings.BgmVolume; sfxVolume = ClientSettings.SfxVolume;
            var voice = VoiceChat.Instance;
            micVolume = voice != null ? voice.MicVolume : 1; teamVolume = voice != null ? voice.TeamVolume : 1;
            micMuted = voice != null && voice.IsMicMuted; teamMuted = voice != null && voice.IsTeamMuted;
            peers.Clear(); bindingButtons.Clear();
            inputDraft = InputBindingPreferences.CreateDraft();
            resolutions = ClientSettings.AvailableResolutions;
            resolutionIndex = Array.FindIndex(resolutions, r => r.width == ClientSettings.ResolutionWidth && r.height == ClientSettings.ResolutionHeight);
            if (resolutionIndex < 0) resolutionIndex = 0;
            modeIndex = Array.IndexOf(ScreenModes, ClientSettings.ScreenMode); if (modeIndex < 0) modeIndex = 1;
            fpsIndex = 0;
            for (int i = 0; i < ClientSettings.AvailableFrameRateLimits.Count; i++)
                if (ClientSettings.AvailableFrameRateLimits[i] == ClientSettings.FrameRateLimit) fpsIndex = i;
            view.Open(gameplayContext, ApplySettings, CancelMain); view.SetBusy(false);
            BuildGame(); BuildGraphics(); BuildControls(); BuildSound(); BuildCredits();
            RefreshGraphics(); RefreshGame();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(view.CancelButton.gameObject);
        }
        void BuildGame()
        {
            view.Paragraph(0, "GameHint", "게임 진행 중에만 사용할 수 있습니다.\n런 포기는 대기실의 쉬는 시간·밤에 호스트만 실행할 수 있습니다.");
            abandonButton = view.ActionRow(0, "RunAbandon", "현재 런을 실패 처리하고 새 런 시작", "런 포기", RequestAbandon);
            abandonButton.name = "AbandonRun";
        }
        void BuildGraphics()
        {
            modeText = view.Choice(1, "ScreenMode", "화면 모드", "", d => { modeIndex = Wrap(modeIndex + d, ScreenModes.Length); RefreshGraphics(); });
            resolutionText = view.Choice(1, "Resolution", "해상도", "", d => { resolutionIndex = Wrap(resolutionIndex + d, resolutions.Length); RefreshGraphics(); });
            fpsText = view.Choice(1, "FrameRate", "FPS 제한", "", d => { fpsIndex = Wrap(fpsIndex + d, ClientSettings.AvailableFrameRateLimits.Count); RefreshGraphics(); });
            view.Paragraph(1, "GraphicsHint", "전체 화면 창은 데스크톱 해상도를 사용합니다.\n변경한 설정은 적용을 누르면 이 PC에 저장됩니다.");
        }
        void RefreshGraphics()
        {
            modeText(ScreenLabels[modeIndex], true);
            var resolution = resolutions[resolutionIndex]; bool desktop = ScreenModes[modeIndex] == FullScreenMode.FullScreenWindow;
            resolutionText(desktop ? "데스크톱 해상도 사용" : $"{resolution.width} × {resolution.height}", !desktop);
            int fps = ClientSettings.AvailableFrameRateLimits[fpsIndex];
            fpsText(fps < 0 ? "제한 없음" : fps == 60 ? "60 FPS (기본)" : $"{fps} FPS", true);
        }
        static int Wrap(int index, int count) => (index % count + count) % count;
        public void OpenGraphics() { if (IsOpen && !IsExtensionOpen) view.ShowPage(1); }

        void BuildControls()
        {
            view.Paragraph(2, "ControlsHint", "변경 버튼을 누르고 새 키를 입력하세요. ESC로 입력 대기를 취소합니다.\n중복 키는 사용할 수 없습니다. ESC 메뉴·M 마이크·마우스 시점·게임패드는 고정 입력입니다.");
            if (inputDraft == null) { view.Paragraph(2, "NoBindings", "입력 설정을 불러오지 못했습니다."); return; }
            var map = inputDraft.FindActionMap("Common");
            foreach (var action in map.actions)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite || !binding.effectivePath.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)) continue;
                    var target = action; int index = i;
                    string id = action.id + ":" + i;
                    var button = view.ActionRow(2, id, ActionLabel(action.name, binding.name), action.GetBindingDisplayString(i), () => BeginBindingCapture(target, index));
                    bindingButtons[id] = button;
                }
            view.ActionRow(2, "ResetBindings", "조작 키 기본값 복원 (적용 전까지 미저장)", "기본값 복원", () =>
            { CancelBindingCapture(); inputDraft.RemoveAllBindingOverrides(); RefreshBindingLabels(); view.SetMessage("기본 키로 되돌렸습니다. 적용을 눌러 저장하세요."); });
        }
        static string ActionLabel(string name, string part)
        {
            if (name == "Move") return part == "negative" ? "뒤로 이동" : "앞으로 이동";
            if (name == "Posture") return part == "negative" ? "왼쪽 이동 / 균형" : "오른쪽 이동 / 균형";
            string label = name switch { "Action" => "점프 / 행동", "DirectionLeft" => "왼쪽 보조 행동", "DirectionRight" => "오른쪽 보조 행동", "Interact" => "상호작용", "Sprint" => "달리기", "Crouch" => "앉기", _ => name };
            string direction = part switch { "negative" => "감소", "positive" => "증가", _ => part };
            return string.IsNullOrEmpty(direction) ? label : label + " · " + direction;
        }
        void BeginBindingCapture(InputAction action, int index)
        {
            CancelBindingCapture(); captureAction = action; captureIndex = index; captureFrame = Time.frameCount;
            captureButton = bindingButtons[action.id + ":" + index]; captureButton.GetComponentInChildren<TMP_Text>().text = "새 키 입력…";
            view.SetMessage("새 키를 누르세요. ESC는 취소, M은 마이크 단축키로 예약되어 있습니다.");
        }
        public void CancelBindingCapture()
        {
            if (captureAction == null) return;
            captureAction = null; captureButton = null; RefreshBindingLabels(); view.SetMessage("");
        }
        void RefreshBindingLabels()
        {
            if (inputDraft == null) return;
            foreach (var action in inputDraft.FindActionMap("Common").actions)
                for (int i = 0; i < action.bindings.Count; i++)
                    if (bindingButtons.TryGetValue(action.id + ":" + i, out var button)) button.GetComponentInChildren<TMP_Text>().text = action.GetBindingDisplayString(i);
        }
        void Update()
        {
            if (!IsOpen || captureAction == null || Keyboard.current == null || Time.frameCount <= captureFrame) return;
            foreach (var key in Keyboard.current.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                if (key.keyCode == Key.Escape) { CancelBindingCapture(); return; }
                if (key.keyCode == Key.M) { view.SetMessage("M은 마이크 단축키입니다. 다른 키를 선택하세요."); return; }
                if (!InputBindingPreferences.TryRebind(inputDraft, captureAction.bindings[captureIndex].id, "<Keyboard>/" + key.name, out string error))
                { view.SetMessage(error); return; }
                CancelBindingCapture(); view.SetMessage("키를 변경했습니다. 적용을 눌러 저장하세요."); return;
            }
        }
        void BuildSound()
        {
            view.Slider(3, "volume", "게임 전체 음량", volume, v => volume = v);
            view.Slider(3, "bgmVolume", "배경음 음량", bgmVolume, v => bgmVolume = v);
            view.Slider(3, "sfxVolume", "효과음 음량", sfxVolume, v => sfxVolume = v);
            view.Slider(3, "teamVolume", "전체 음성 수신 음량", teamVolume, v => teamVolume = v);
            view.Slider(3, "micVolume", "내 마이크 입력 음량", micVolume, v => micVolume = v);
            view.Toggle(3, "micEnabled", "마이크 켜기", !micMuted, v => micMuted = !v);
            view.Toggle(3, "teamMuted", "전체 음성 수신 음소거", teamMuted, v => teamMuted = v);
            BuildDevices();
            var voice = VoiceChat.Instance;
            if (voice == null) view.Paragraph(3, "VoiceUnavailable", "음성 서비스를 준비 중입니다. 장치·팀원 정보는 연결 후 다시 열면 표시됩니다.");
            var others = voice != null ? voice.Participants.Where(p => !p.IsSelf).ToArray() : Array.Empty<Unity.Services.Vivox.VivoxParticipant>();
            if (others.Length == 0) view.Paragraph(3, "NoPeers", "현재 음성 채널에 다른 단원이 없습니다.");
            foreach (var peer in others)
            {
                string id = peer.PlayerId;
                peers[id] = (voice.ParticipantVolume(id), voice.ParticipantMuted(id));
                string name = string.IsNullOrWhiteSpace(peer.DisplayName) ? "단원" : peer.DisplayName;
                view.Slider(3, "peerVolume_" + id, name + " · 음량", peers[id].volume, v => { var p = peers[id]; peers[id] = (v, p.muted); });
                view.Toggle(3, "peerMuted_" + id, name + " · 음소거", peers[id].muted, v => { var p = peers[id]; peers[id] = (p.volume, v); });
            }
        }
        void BuildDevices()
        {
            deviceVoice = VoiceChat.Instance;
            inputDeviceText = view.Choice(3, "InputDevice", "음성 입력 기기", "준비 필요", d =>
            {
                if (inputIds.Length == 0) return;
                inputDeviceId = inputIds[Wrap(Array.IndexOf(inputIds, inputDeviceId) + d, inputIds.Length)];
                devicesTouched = true; RefreshDevices();
            }, false);
            outputDeviceText = view.Choice(3, "OutputDevice", "음성 출력 기기", "준비 필요", d =>
            {
                if (outputIds.Length == 0) return;
                outputDeviceId = outputIds[Wrap(Array.IndexOf(outputIds, outputDeviceId) + d, outputIds.Length)];
                devicesTouched = true; RefreshDevices();
            }, false);
            view.ActionRow(3, "LoadDevices", "기기 목록 새로고침 (음성 서비스 연결)", "기기 목록 불러오기", LoadDevices);
            view.Paragraph(3, "DeviceHint", "출력 기기 선택은 음성 채팅에 적용됩니다. 게임 소리 출력은 운영체제에서 변경하세요.\n마이크 접근 권한이 필요하며, 기기를 연결·해제하면 목록이 갱신됩니다.");
            if (deviceVoice != null) deviceVoice.DevicesChanged += RefreshDevices;
            RefreshDevices();
        }
        void RefreshDevices()
        {
            if (!IsOpen || deviceVoice == null || inputDeviceText == null) return;
            inputIds = deviceVoice.AvailableInputDevices.Select(d => d.DeviceID).ToArray();
            outputIds = deviceVoice.AvailableOutputDevices.Select(d => d.DeviceID).ToArray();
            inputNames = deviceVoice.AvailableInputDevices.Select(d => d.DeviceName).ToArray();
            outputNames = deviceVoice.AvailableOutputDevices.Select(d => d.DeviceName).ToArray();
            if (!devicesTouched)
            {
                inputDeviceId = deviceVoice.EffectiveInputDevice?.DeviceID;
                outputDeviceId = deviceVoice.EffectiveOutputDevice?.DeviceID;
            }
            int input = Array.IndexOf(inputIds, inputDeviceId), output = Array.IndexOf(outputIds, outputDeviceId);
            inputDeviceText(input >= 0 ? inputNames[input] : deviceVoice.AreAudioDevicesReady ? "사용 가능한 기기 없음 / 연결 해제" : "기기 목록을 불러와 주세요", inputIds.Length > 0);
            outputDeviceText(output >= 0 ? outputNames[output] : deviceVoice.AreAudioDevicesReady ? "사용 가능한 기기 없음 / 연결 해제" : "기기 목록을 불러와 주세요", outputIds.Length > 0);
        }
        async void LoadDevices()
        {
            if (busy || deviceVoice == null) { view.SetMessage("음성 서비스를 준비 중입니다."); return; }
            int current = session; busy = true; view.SetMessage("음성 기기 목록을 불러오는 중입니다…");
            try
            {
                bool ready = await deviceVoice.InitializeAudioDevicesAsync();
                if (current != session || !IsOpen) return;
                RefreshDevices(); view.SetMessage(ready ? "기기 목록을 갱신했습니다." : "기기 목록을 불러오지 못했습니다.");
            }
            catch (Exception) { if (current == session && IsOpen) view.SetMessage("음성 기기를 불러오지 못했습니다. 연결·마이크 권한을 확인하세요."); }
            finally { if (current == session) busy = false; }
        }
        void BuildCredits()
        {
            view.Paragraph(4, "Developers", "개발자\nMoHoDu · 총괄 시스템 / 기획 조율\nmingyu (gyumin9048-ctrl) · 대기실 / 에셋 관리\nRagSpirin · 공연장 / 탈출로\nlakuase2 · 기존 공연장 개발 기여", 210);
            view.Paragraph(4, "AssetCredits", "에셋 및 소프트웨어\nPretendard — Kil Hyung-jin · SIL Open Font License 1.1 (아래 원문)\nQuaternius Standard — Quaternius · CC0 1.0\nhttps://creativecommons.org/publicdomain/zero/1.0/\nDOTween / DOTween Pro — Copyright (c) 2014-2026 Daniele Giardini - Demigiant\nhttps://dotween.demigiant.com/license.php", 240);
            view.Paragraph(4, "CreditsNotice", "추가 외부 에셋·BM 폰트 및 패키지의 개별 배포 조건은 확인 중입니다.\n현재 목록은 전체 라이선스 검토 완료나 모든 권리 확보를 뜻하지 않습니다.", 90);
            var license = Resources.Load<TextAsset>("Fonts/Pretendard-License");
            view.Paragraph(4, "PretendardLicense", license != null ? license.text : "Pretendard 라이선스 원문을 불러오지 못했습니다.");
        }
        public void RequestAbandon()
        {
            var run = CampaignRun.Instance;
            if (!IsOpen || view.CurrentPage != 0 || !gameplayContext || IsExtensionOpen || run == null || !run.CanAbandonRun) return;
            abandonContext = run.State; confirmingAbandon = true; view.SetCovered(true);
            extension.Open("런 포기", "정말 현재 런을 포기하고 새로 시작하겠습니까?\n모든 단원이 다음 런 1일차부터 다시 시작합니다.", "예", ConfirmAbandon, RestoreMain);
        }
        public void ConfirmAbandon()
        {
            if (!IsExtensionOpen || !confirmingAbandon) return;
            extension.SetAcceptEnabled(false);
            var run = CampaignRun.Instance;
            if (run == null || !run.AbandonRun(abandonContext))
            { extension.SetMessage("런 상태가 변경되어 포기할 수 없습니다. 아니요를 눌러 돌아가 주세요."); return; }
            confirmingAbandon = false; CommonMenuUI.Current?.CloseAll();
        }
        void RestoreMain()
        {
            confirmingAbandon = false; if (!IsOpen) return;
            view.SetCovered(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(abandonButton.gameObject);
        }
        void LateUpdate() { if (IsOpen) RefreshGame(); }
        void RefreshGame()
        {
            if (abandonButton == null) return;
            var run = CampaignRun.Instance;
            abandonButton.interactable = gameplayContext && run != null && run.CanAbandonRun;
            abandonButton.GetComponentInChildren<TMP_Text>().text = run != null && !run.HasAuthority ? "런 포기 (호스트 전용)" : "런 포기";
        }
        public async void ApplySettings()
        {
            if (!IsOpen || IsExtensionOpen || IsCapturingBinding || busy) return;
            if (inputDraft != null && !InputBindingPreferences.TryValidateDraft(inputDraft, out string validationError))
            { view.SetMessage(validationError); return; }
            int current = session; busy = applying = true; view.SetBusy(true);
            try
            {
                if (devicesTouched && deviceVoice != null)
                {
                    if (inputDeviceId != null && !await deviceVoice.SelectInputDeviceAsync(inputDeviceId))
                    { view.SetMessage("선택한 입력 기기가 사라졌습니다. 목록을 새로고침하세요."); return; }
                    if (current != session || !IsOpen) return;
                    if (outputDeviceId != null && !await deviceVoice.SelectOutputDeviceAsync(outputDeviceId))
                    { view.SetMessage("선택한 출력 기기가 사라졌습니다. 입력 기기는 이미 적용됐을 수 있습니다."); return; }
                    if (current != session || !IsOpen) return;
                }
                if (inputDraft != null && !InputBindingPreferences.TryApply(inputDraft, out string error))
                { view.SetMessage(error); return; }
                var resolution = resolutions[resolutionIndex];
                ClientSettings.SetGraphics(ScreenModes[modeIndex], resolution.width, resolution.height, ClientSettings.AvailableFrameRateLimits[fpsIndex]);
                ClientSettings.Volume = volume; ClientSettings.BgmVolume = bgmVolume; ClientSettings.SfxVolume = sfxVolume; ClientSettings.Save();
                var voice = VoiceChat.Instance;
                voice?.ApplyAudioPreferences(micVolume, micMuted, teamVolume, teamMuted);
                foreach (var peer in peers) voice?.SetParticipantPreference(peer.Key, peer.Value.volume, peer.Value.muted);
                busy = applying = false; CancelMain();
            }
            catch (Exception) { if (current == session && IsOpen) view.SetMessage("설정을 모두 적용하지 못했습니다. 연결·장치를 확인하세요. 일부 값은 적용됐을 수 있습니다."); }
            finally { if (current == session) { busy = applying = false; if (IsOpen) view.SetBusy(false); } }
        }
        public void OpenTeammates() { if (IsOpen) view.ShowPage(3); }
        public void CancelTop()
        { if (applying) return; if (IsExtensionOpen) extension.Cancel(); else if (IsCapturingBinding) CancelBindingCapture(); else if (IsOpen) CancelMain(); }
        void CancelMain()
        { if (applying) return; var callback = closed; closed = null; Close(); callback?.Invoke(); }
        public void Close()
        {
            session++; busy = applying = false;
            if (deviceVoice != null) deviceVoice.DevicesChanged -= RefreshDevices;
            deviceVoice = null;
            CancelBindingCapture(); extension?.Hide(); confirmingAbandon = false; view?.Hide();
            main.Hide(); teammates.Hide();
            if (inputDraft != null) Destroy(inputDraft); inputDraft = null; closed = null;
        }
        void OnDestroy()
        { session++; if (deviceVoice != null) deviceVoice.DevicesChanged -= RefreshDevices; if (inputDraft != null) Destroy(inputDraft); }
    }
}
