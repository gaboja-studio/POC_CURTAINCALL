using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.StartLobby;
using CurtainCall.Campaign;
using CurtainCall.Player;
using CurtainCall.Voice;
using CurtainCall.UI.SettingsV2;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using V2View = CurtainCall.UI.SettingsV2.SettingsPanelView;
using V2Value = CurtainCall.UI.SettingsV2.SettingsValue;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>v2 프리팹의 편집 세션을 기존 로컬 설정·입력·음성·런 서비스에 연결한다.</summary>
    public sealed class CommonSettingsUI : MonoBehaviour
    {
        public const string PrefabResource = "Prefabs/UIs/SettingsUiV2/C14_Settings_v2";
        // 기존 씬의 C14 참조와 Configure 계약은 보존하되 표시하지 않는다.
        [SerializeField] SettingsPanelView main;
        [SerializeField] SettingsPanelView teammates;
        [SerializeField] CanvasGroup mainGroup;
        [SerializeField] UnityEngine.UI.Button teammateButton;
        [SerializeField] TMP_Text controlsHint;
        [SerializeField] TMP_Text teammateHint;
        V2View view;
        Action closed;
        bool gameplayContext, closing, loadingDevices;
        CampaignSnapshot abandonContext;
        Resolution[] resolutions;
        InputActionAsset inputDraft;
        readonly Dictionary<string, InputBindingPreferences.BindingEntry> bindings = new();
        readonly Dictionary<string, (float volume, bool muted)> peers = new();
        SettingsRebindRequest capture;
        int captureFrame, session;
        VoiceChat deviceVoice;
        string selectedInput = "unavailable", selectedOutput = "unavailable";
        bool inputTouched, outputTouched;
        static readonly FullScreenMode[] ScreenModes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        static readonly string[] ScreenLabels = { "전체 화면", "전체 화면 창", "창 모드" };
        public bool IsExtensionOpen => view != null && view.IsConfirmationOpen;
        public bool IsCapturingBinding => view != null && view.IsRebinding;
        public V2View Main => view;
        public SettingsPanelView Teammates => teammates;
        public bool IsOpen => view != null && view.IsVisible;

        public void Configure(SettingsPanelView original, SettingsPanelView peerView, CanvasGroup group,
            UnityEngine.UI.Button peerButton, TMP_Text controls, TMP_Text hint)
        { main = original; teammates = peerView; mainGroup = group; teammateButton = peerButton; controlsHint = controls; teammateHint = hint; }
        void Awake()
        {
            main.Hide(); teammates.Hide();
            var prefab = Resources.Load<GameObject>(PrefabResource);
            if (prefab == null) throw new InvalidOperationException("설정 v2 제품 프리팹이 없습니다: " + PrefabResource);
            var root = new GameObject("SettingsV2Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 315; canvas.referencePixelsPerUnit = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f; scaler.referencePixelsPerUnit = 100;
            var instance = Instantiate(prefab, root.transform, false); instance.name = "C14_SettingsV2";
            view = instance.GetComponent<V2View>(); view.IsExternallyRoutedCancel = true;
            view.ApplyRequested += Apply;
            view.ActionRequested += HandleAction;
            view.ActionConfirming += _ => { if (CampaignRun.Instance != null) abandonContext = CampaignRun.Instance.State; };
            view.RebindRequested += request => { capture = request; captureFrame = Time.frameCount; };
            view.RebindCancelled += _ => capture = null;
            view.DraftChanged += DraftChanged;
            view.Closed += OnViewClosed;
        }
        public void Open(bool gameplay, Action onClosed)
        {
            Close(); closed = onClosed;
            var run = CampaignRun.Instance;
            gameplayContext = gameplay && run != null && run.State.Phase != CampaignPhase.None;
            inputDraft = InputBindingPreferences.CreateDraft(); bindings.Clear(); peers.Clear();
            inputTouched = outputTouched = false; selectedInput = selectedOutput = "unavailable";
            deviceVoice = VoiceChat.Instance;
            resolutions = ClientSettings.AvailableResolutions;
            var catalog = BuildCatalog();
            if (!view.Open(catalog, out string error))
            { Debug.LogError("설정 v2를 열지 못했습니다: " + error); OnViewClosed(); return; }
            if (deviceVoice != null) deviceVoice.DevicesChanged += RefreshDevices;
            RefreshDevices(); RefreshGame(); DraftChanged(view.GetDraftSnapshot());
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(view.CancelButton.gameObject);
        }
        SettingsCatalog BuildCatalog()
        {
            var catalog = new SettingsCatalog();
            string[] ids = { "game", "graphics", "controls", "audio", "other" }, labels = { "게임", "그래픽", "조작", "사운드", "기타" };
            for (int i = 0; i < 5; i++) catalog.pages.Add(new SettingsPage { id = ids[i], label = labels[i], enabled = i != 0 || gameplayContext });
            catalog.reservedBindingIds.AddRange(new[] { "<Keyboard>/escape", "<Keyboard>/m" });
            var game = catalog.pages[0];
            game.notice = "게임 진행 중에만 사용할 수 있습니다.\n런 포기는 대기실의 쉬는 시간·밤에 호스트만 실행할 수 있습니다.";
            var abandon = ActionRow("RunAbandon", "현재 런을 실패 처리하고 새 런 시작", "런 포기");
            abandon.requiresRunPermission = true;
            abandon.confirmationTitle = "런 포기";
            abandon.confirmationBody = "정말 현재 런을 포기하고 새로 시작하겠습니까?\n모든 단원이 다음 런 1일차부터 다시 시작합니다.";
            game.rows.Add(abandon);
            // 대기실 안내(Task-086): 이 PC에 저장. 끄기·처음부터 다시·본 안내 다시 읽기.
            game.rows.Add(Toggle("WaitingRoomGuide", "대기실 안내 표시", CurtainCall.WaitingRoom.WaitingRoomTutorial.Enabled));
            var guideReset = ActionRow("GuideReset", "본 대기실 안내를 모두 지우고 처음부터 다시 띄웁니다.", "안내 처음부터");
            guideReset.requiresConfirmation = false;
            game.rows.Add(guideReset);
            game.rows.Add(Paragraph("GuideSeen", "본 안내 다시 보기\n\n" + CurtainCall.WaitingRoom.WaitingRoomTutorial.SeenSummary()));
            var graphics = catalog.pages[1];
            graphics.rows.Add(Choice("ScreenMode", "화면 모드", ((int)ClientSettings.ScreenMode).ToString(), ScreenModes.Select((m, i) => new SettingsChoice(((int)m).ToString(), ScreenLabels[i]))));
            string resolution = ClientSettings.ResolutionWidth + "x" + ClientSettings.ResolutionHeight;
            graphics.rows.Add(Choice("Resolution", "해상도", resolution, resolutions.Select(r => new SettingsChoice(r.width + "x" + r.height, $"{r.width} × {r.height}"))));
            graphics.rows.Last().disabledValueLabel = "데스크톱 해상도 사용";
            graphics.rows.Add(Choice("FrameRate", "FPS 제한", ClientSettings.FrameRateLimit.ToString(), ClientSettings.AvailableFrameRateLimits.Select(f => new SettingsChoice(f.ToString(), f < 0 ? "제한 없음" : f == 60 ? "60 FPS (기본)" : $"{f} FPS"))));
            graphics.rows.Add(Toggle("HideNearbyCharacter", "카메라가 가까울 때 내 캐릭터 숨기기", ClientSettings.HideNearbyCharacter));
            // 혈흔 연출(Task-090): 게임 설정에서 꺼져 있으면 항목을 보이지 않고 모두 꺼진 상태다.
            if (CurtainCall.Damage.InjuryBloodEffects.IsEnabledByGame)
                graphics.rows.Add(Toggle("ShowBloodEffect", "혈흔 연출 표시", ClientSettings.ShowBloodEffect));
            graphics.rows.Add(Paragraph("GraphicsHint", "전체 화면 창은 데스크톱 해상도를 사용합니다.\n변경한 설정은 적용을 누르면 이 PC에 저장됩니다."));
            var controls = catalog.pages[2];
            controls.notice = "변경 버튼을 누르고 새 키를 입력하세요. ESC로 입력 대기를 취소합니다.\n사용 중인 키는 확인 후 옮깁니다. 모든 키를 설정하고 적용을 눌러야 저장됩니다.\nESC 메뉴·M 마이크·마우스 시점·게임패드는 고정 입력입니다.";
            // Move의 에셋 part 순서는 negative/positive지만 표시만 전방/후방으로 정렬한다.
            if (inputDraft != null) foreach (var entry in InputBindingPreferences.GetBindings(inputDraft)
                .OrderBy(b => b.ActionName == "Move" ? (b.PartName == "positive" ? 0 : 1) : 2))
            {
                string id = entry.BindingId.ToString(); bindings[id] = entry;
                controls.rows.Add(new SettingsRow { id = id, label = ActionLabel(entry.ActionName, entry.PartName), kind = SettingsRowKind.Rebind,
                    value = new V2Value { rowId = id, bindingId = entry.Path, bindingDisplay = entry.DisplayName } });
            }
            if (inputDraft == null) controls.rows.Add(Paragraph("NoBindings", "입력 설정을 불러오지 못했습니다."));
            var voice = deviceVoice; var sound = catalog.pages[3];
            sound.rows.Add(Slider("volume", "게임 전체 음량", ClientSettings.Volume));
            sound.rows.Add(Slider("bgmVolume", "배경음 음량", ClientSettings.BgmVolume));
            sound.rows.Add(Slider("sfxVolume", "효과음 음량", ClientSettings.SfxVolume));
            sound.rows.Add(Slider("teamVolume", "전체 음성 수신 음량", voice != null ? voice.TeamVolume : 1));
            sound.rows.Add(Slider("micVolume", "내 마이크 입력 음량", voice != null ? voice.MicVolume : 1));
            sound.rows.Add(Toggle("micEnabled", "마이크 켜기", voice == null || !voice.IsMicMuted));
            sound.rows.Add(Toggle("teamMuted", "전체 음성 수신 음소거", voice != null && voice.IsTeamMuted));
            sound.rows.Add(Choice("InputDevice", "음성 입력 기기", "unavailable", new[] { new SettingsChoice("unavailable", "기기 목록을 불러와 주세요"), new SettingsChoice(VoiceChat.SystemDefaultDeviceId, "시스템 기본 기기") }));
            sound.rows.Add(Choice("OutputDevice", "음성 출력 기기", "unavailable", new[] { new SettingsChoice("unavailable", "기기 목록을 불러와 주세요"), new SettingsChoice(VoiceChat.SystemDefaultDeviceId, "시스템 기본 기기") }));
            var load = ActionRow("LoadDevices", "기기 목록 새로고침 (음성 서비스 연결)", "기기 목록 불러오기"); load.requiresConfirmation = false; sound.rows.Add(load);
            sound.rows.Add(Paragraph("DeviceHint", "출력 기기 선택은 음성 채팅에 적용됩니다. 게임 소리 출력은 운영체제에서 변경하세요.\n마이크 접근 권한이 필요하며, 기기를 연결·해제하면 목록이 갱신됩니다."));
            var others = voice != null ? voice.Participants.Where(p => !p.IsSelf).ToArray() : Array.Empty<Unity.Services.Vivox.VivoxParticipant>();
            if (others.Length == 0) sound.rows.Add(Paragraph("NoPeers", "현재 음성 채널에 다른 단원이 없습니다."));
            foreach (var peer in others)
            {
                string id = peer.PlayerId, name = string.IsNullOrWhiteSpace(peer.DisplayName) ? "단원" : peer.DisplayName;
                peers[id] = (voice.ParticipantVolume(id), voice.ParticipantMuted(id));
                sound.rows.Add(Slider("peerVolume_" + id, name + " · 음량", peers[id].volume));
                sound.rows.Add(Toggle("peerMuted_" + id, name + " · 음소거", peers[id].muted));
            }
            var credits = catalog.pages[4];
            credits.rows.Add(Paragraph("Developers", "개발자\nMoHoDu · 총괄 시스템 / 기획 조율\nmingyu (gyumin9048-ctrl) · 대기실 / 에셋 관리\nRagSpirin · 공연장 / 탈출로\nlakuase2 · 기존 공연장 개발 기여"));
            credits.rows.Add(Paragraph("AssetCredits", "에셋 및 소프트웨어\nPretendard — Kil Hyung-jin · SIL Open Font License 1.1 (아래 원문)\nQuaternius Standard — Quaternius · CC0 1.0\nhttps://creativecommons.org/publicdomain/zero/1.0/\nDOTween / DOTween Pro — Copyright (c) 2014-2026 Daniele Giardini - Demigiant\nhttps://dotween.demigiant.com/license.php"));
            credits.rows.Add(Paragraph("CreditsNotice", "추가 외부 에셋·BM 폰트 및 패키지의 개별 배포 조건은 확인 중입니다.\n현재 목록은 전체 라이선스 검토 완료나 모든 권리 확보를 뜻하지 않습니다."));
            var license = Resources.Load<TextAsset>("Fonts/Pretendard-License");
            credits.rows.Add(Paragraph("PretendardLicense", license != null ? license.text : "Pretendard 라이선스 원문을 불러오지 못했습니다."));
            foreach (var page in catalog.pages)
            {
                bool resettable = page.id == "graphics" || page.id == "audio" || page.id == "controls" && inputDraft != null;
                var reset = ActionRow("Reset_" + page.id, resettable ? page.label + " 설정을 기본값으로 되돌립니다.\n적용을 눌러야 저장됩니다." : "초기화할 설정이 없습니다.", "초기화");
                reset.requiresConfirmation = false; reset.actionStyle = "tab"; reset.enabled = resettable;
                page.rows.Add(reset);
            }
            return catalog;
        }
        static SettingsRow Paragraph(string id, string text) => new() { id = id, label = text, kind = SettingsRowKind.Paragraph };
        static SettingsRow Slider(string id, string label, float value) => new() { id = id, label = label, kind = SettingsRowKind.Slider, value = new V2Value { rowId = id, number = value } };
        static SettingsRow Toggle(string id, string label, bool value) => new() { id = id, label = label, kind = SettingsRowKind.Toggle, value = new V2Value { rowId = id, toggle = value } };
        static SettingsRow Choice(string id, string label, string selected, IEnumerable<SettingsChoice> choices) => new() { id = id, label = label, kind = SettingsRowKind.Choice, choices = choices.ToList(), value = new V2Value { rowId = id, choiceId = selected } };
        static SettingsRow ActionRow(string id, string label, string button) => new() { id = id, label = label, kind = SettingsRowKind.Action, actionId = id, confirmationTitle = button, confirmationBody = label, confirmationButton = button };
        static string ActionLabel(string name, string part)
        {
            if (name == "Move") return part == "negative" ? "뒤로 이동" : "앞으로 이동";
            if (name == "Posture") return part == "negative" ? "왼쪽 이동 / 균형" : "오른쪽 이동 / 균형";
            return name switch { "Action" => "점프 / 행동", "DirectionLeft" => "왼쪽 보조 행동", "DirectionRight" => "오른쪽 보조 행동", "Interact" => "상호작용", "Sprint" => "달리기", "Crouch" => "앉기", _ => name };
        }
        void DraftChanged(List<V2Value> values)
        {
            var mode = values.Find(v => v.rowId == "ScreenMode");
            if (mode != null) view.SetRowAvailability("Resolution", mode.choiceId != ((int)FullScreenMode.FullScreenWindow).ToString(),
                mode.choiceId == ((int)FullScreenMode.FullScreenWindow).ToString() ? "전체 화면 창은 데스크톱 해상도를 사용합니다." : "");
            var input = values.Find(v => v.rowId == "InputDevice"); var output = values.Find(v => v.rowId == "OutputDevice");
            if (input != null && input.choiceId != selectedInput) { selectedInput = input.choiceId; inputTouched = true; }
            if (output != null && output.choiceId != selectedOutput) { selectedOutput = output.choiceId; outputTouched = true; }
        }
        void RefreshDevices()
        {
            if (!IsOpen || deviceVoice == null) return;
            var inputs = deviceVoice.AvailableInputDevices.Select(d => new SettingsChoice(d.DeviceID, d.DeviceName)).ToList();
            var outputs = deviceVoice.AvailableOutputDevices.Select(d => new SettingsChoice(d.DeviceID, d.DeviceName)).ToList();
            if (!inputTouched) selectedInput = (deviceVoice.ActiveInputDevice ?? deviceVoice.EffectiveInputDevice)?.DeviceID ?? "unavailable";
            if (!outputTouched) selectedOutput = (deviceVoice.ActiveOutputDevice ?? deviceVoice.EffectiveOutputDevice)?.DeviceID ?? "unavailable";
            UpdateDevice("InputDevice", inputs, selectedInput);
            UpdateDevice("OutputDevice", outputs, selectedOutput);
        }
        void UpdateDevice(string rowId, List<SettingsChoice> choices, string selected)
        {
            if (!choices.Exists(c => c.id == VoiceChat.SystemDefaultDeviceId))
                choices.Add(new SettingsChoice(VoiceChat.SystemDefaultDeviceId, "시스템 기본 기기"));
            if (!choices.Exists(c => c.id == selected)) choices.Insert(0, new SettingsChoice(selected,
                deviceVoice.AreAudioDevicesReady ? "사용 가능한 기기 없음 / 연결 해제" : "기기 목록을 불러와 주세요"));
            view.SetChoices(rowId, choices, selected, "");
        }
        bool SyncBindings(List<V2Value> values, out string error)
        {
            error = ""; if (inputDraft == null) return true;
            inputDraft.RemoveAllBindingOverrides();
            foreach (var value in values)
            {
                if (!bindings.TryGetValue(value.rowId, out var binding)) continue;
                if (!InputBindingPreferences.TryValidateKeyPath(value.bindingId, out string path, out error)) return false;
                var action = inputDraft.FindAction(binding.ActionId);
                action.ApplyBindingOverride(binding.BindingIndex, path);
            }
            return InputBindingPreferences.TryValidateDraft(inputDraft, out error);
        }
        void Update()
        {
            if (!IsOpen || capture == null || Time.frameCount <= captureFrame) return;
            if (Keyboard.current != null)
                foreach (var key in Keyboard.current.allKeys)
                {
                    if (!key.wasPressedThisFrame) continue;
                    if (key.keyCode == Key.Escape) return; // CommonMenuUI가 캡처 취소를 한 번만 전달한다.
                    Capture("<Keyboard>/" + key.name); return;
                }
            // 마우스 버튼(왼쪽 제외)도 받는다. UV 등 같은 마우스 조작용.
            var mouse = Mouse.current;
            if (mouse == null) return;
            foreach (var button in new[] { mouse.rightButton, mouse.middleButton, mouse.forwardButton, mouse.backButton })
                if (button != null && button.wasPressedThisFrame) { Capture("<Mouse>/" + button.name); return; }
        }
        void Capture(string candidate)
        {
            var request = capture; capture = null;
            if (!InputBindingPreferences.TryValidateKeyPath(candidate, out string path, out string error) || !bindings.ContainsKey(request.rowId))
            { view.RejectRebind(request.token, string.IsNullOrEmpty(error) ? "키를 변경할 수 없습니다." : error); return; }
            view.CompleteRebind(request.token, path, InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice), out _);
        }
        async void HandleAction(SettingsActionRequest request)
        {
            if (request.actionId == "RunAbandon")
            {
                var run = CampaignRun.Instance;
                bool success = gameplayContext && run != null && run.AbandonRun(abandonContext);
                view.CompleteAction(request.token, success, "런 상태가 변경되어 포기할 수 없습니다.");
                if (success) CommonMenuUI.Current?.CloseAll();
                return;
            }
            if (request.actionId == "GuideReset")
            {
                CurtainCall.WaitingRoom.WaitingRoomTutorial.ResetProgress();
                if (view.CompleteAction(request.token, true, null, false)) view.ShowMessage("대기실 안내를 처음부터 다시 띄웁니다.");
                return;
            }
            if (request.actionId.StartsWith("Reset_", StringComparison.Ordinal))
            {
                if (view.CompleteAction(request.token, true, null, false)) ResetPage(request.actionId.Substring(6));
                return;
            }
            if (request.actionId != "LoadDevices") { view.CompleteAction(request.token, false, "지원하지 않는 동작입니다."); return; }
            view.CompleteAction(request.token, true, null, false);
            if (loadingDevices || deviceVoice == null) { view.ShowMessage("음성 서비스를 준비 중입니다."); return; }
            int current = session; loadingDevices = true; view.ShowMessage("음성 기기 목록을 불러오는 중입니다…");
            try
            {
                bool ready = await deviceVoice.InitializeAudioDevicesAsync();
                if (current != session || !IsOpen) return;
                RefreshDevices(); view.ShowMessage(ready ? "기기 목록을 갱신했습니다." : "기기 목록을 불러오지 못했습니다.");
            }
            catch (Exception) { if (current == session && IsOpen) view.ShowMessage("음성 기기를 불러오지 못했습니다. 연결·마이크 권한을 확인하세요."); }
            finally { if (current == session) loadingDevices = false; }
        }
        void ResetPage(string page)
        {
            var values = new List<V2Value>();
            switch (page)
            {
                case "graphics":
                    var resolution = ClientSettings.DefaultResolution;
                    values.Add(new V2Value { rowId = "ScreenMode", choiceId = ((int)ClientSettings.DefaultScreenMode).ToString() });
                    values.Add(new V2Value { rowId = "Resolution", choiceId = resolution.width + "x" + resolution.height });
                    values.Add(new V2Value { rowId = "FrameRate", choiceId = ClientSettings.DefaultFrameRateLimit.ToString() });
                    values.Add(new V2Value { rowId = "HideNearbyCharacter", toggle = true });
                    if (CurtainCall.Damage.InjuryBloodEffects.IsEnabledByGame) values.Add(new V2Value { rowId = "ShowBloodEffect", toggle = true });
                    break;
                case "controls":
                    if (inputDraft == null) return;
                    inputDraft.RemoveAllBindingOverrides();
                    values.AddRange(InputBindingPreferences.GetBindings(inputDraft).Select(b => new V2Value { rowId = b.BindingId.ToString(), bindingId = b.Path, bindingDisplay = b.DisplayName }));
                    break;
                case "audio":
                    values.Add(new V2Value { rowId = "volume", number = ClientSettings.DefaultVolume });
                    values.Add(new V2Value { rowId = "bgmVolume", number = ClientSettings.DefaultBgmVolume });
                    values.Add(new V2Value { rowId = "sfxVolume", number = ClientSettings.DefaultSfxVolume });
                    values.Add(new V2Value { rowId = "teamVolume", number = 1 });
                    values.Add(new V2Value { rowId = "micVolume", number = 1 });
                    values.Add(new V2Value { rowId = "micEnabled", toggle = true });
                    values.Add(new V2Value { rowId = "teamMuted", toggle = false });
                    values.Add(new V2Value { rowId = "InputDevice", choiceId = VoiceChat.SystemDefaultDeviceId });
                    values.Add(new V2Value { rowId = "OutputDevice", choiceId = VoiceChat.SystemDefaultDeviceId });
                    foreach (var id in peers.Keys)
                    {
                        values.Add(new V2Value { rowId = "peerVolume_" + id, number = 1 });
                        values.Add(new V2Value { rowId = "peerMuted_" + id, toggle = false });
                    }
                    break;
                default: return;
            }
            if (view.SetDraftValues(values)) view.ShowMessage("이 탭을 초기화했습니다. 적용을 눌러 저장하세요.");
        }
        async void Apply(SettingsApplyRequest request)
        {
            int current = session;
            bool Current() => this != null && view != null && current == session && view.IsRequestCurrent(request.token);
            try
            {
                if (loadingDevices) { view.CompleteApply(request.token, false, "기기 조회가 끝난 뒤 적용해 주세요."); return; }
                if (!SyncBindings(request.values, out string error)) { view.CompleteApply(request.token, false, error); return; }
                var values = request.values.ToDictionary(v => v.rowId);
                if (deviceVoice != null)
                {
                    if (inputTouched && selectedInput != "unavailable" && !await deviceVoice.SelectInputDeviceAsync(selectedInput))
                    { if (Current()) view.CompleteApply(request.token, false, "선택한 입력 기기가 사라졌습니다. 목록을 새로고침하세요."); return; }
                    if (!Current()) return;
                    if (outputTouched && selectedOutput != "unavailable" && !await deviceVoice.SelectOutputDeviceAsync(selectedOutput))
                    { if (Current()) view.CompleteApply(request.token, false, "출력 기기가 사라졌습니다. 입력 기기는 이미 적용됐을 수 있습니다."); return; }
                }
                if (!Current()) return;
                if (inputDraft != null && !InputBindingPreferences.TryApply(inputDraft, out error)) { view.CompleteApply(request.token, false, error); return; }
                var resolution = resolutions.First(r => r.width + "x" + r.height == values["Resolution"].choiceId);
                var mode = (FullScreenMode)int.Parse(values["ScreenMode"].choiceId);
                ClientSettings.SetGraphics(mode, resolution.width, resolution.height, int.Parse(values["FrameRate"].choiceId));
                ClientSettings.HideNearbyCharacter = values["HideNearbyCharacter"].toggle;
                if (values.TryGetValue("ShowBloodEffect", out var blood)) ClientSettings.ShowBloodEffect = blood.toggle;
                if (values.TryGetValue("WaitingRoomGuide", out var guide)) CurtainCall.WaitingRoom.WaitingRoomTutorial.Enabled = guide.toggle;
                ClientSettings.Volume = values["volume"].number; ClientSettings.BgmVolume = values["bgmVolume"].number; ClientSettings.SfxVolume = values["sfxVolume"].number; ClientSettings.Save();
                var voice = VoiceChat.Instance;
                voice?.ApplyAudioPreferences(values["micVolume"].number, !values["micEnabled"].toggle, values["teamVolume"].number, values["teamMuted"].toggle);
                foreach (var id in peers.Keys) voice?.SetParticipantPreference(id, values["peerVolume_" + id].number, values["peerMuted_" + id].toggle);
                view.CompleteApply(request.token, true);
            }
            catch (Exception) { if (Current()) view.CompleteApply(request.token, false, "설정을 모두 적용하지 못했습니다. 연결·장치를 확인하세요. 일부 값은 적용됐을 수 있습니다."); }
        }
        void LateUpdate() { if (IsOpen) RefreshGame(); }
        void RefreshGame()
        {
            var run = CampaignRun.Instance;
            view.SetRunPermission(gameplayContext && run != null && run.CanAbandonRun,
                run != null && !run.HasAuthority ? "런 포기는 호스트만 실행할 수 있습니다." : "대기실의 쉬는 시간·밤에만 런을 포기할 수 있습니다.");
        }
        public void OpenGraphics() { if (IsOpen) view.ShowPage(1); }
        public void OpenTeammates() { if (IsOpen) view.ShowPage(3); }
        public void ApplySettings() => view?.RequestApply();
        public void CancelBindingCapture() { if (IsCapturingBinding) view.Cancel(); }
        public void CancelTop() => view?.Cancel();
        public void RequestAbandon()
        {
            if (!IsOpen || view.CurrentPage != 0) return;
            RefreshGame();
            var row = view.transform.Find("00_Visuals/40_Content/30_Scroll/00_Viewport/00_Content/RunAbandon_Row/10_Action");
            var button = row != null ? row.GetComponent<UnityEngine.UI.Button>() : null;
            if (button != null && button.interactable) button.onClick.Invoke();
        }
        public void ConfirmAbandon()
        {
            if (!IsExtensionOpen) return;
            view.transform.Find("00_Visuals/90_ActionConfirmation/00_Dialog/40_Content/30_Confirm").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }
        void OnViewClosed()
        {
            var callback = closing ? null : closed; closed = null;
            Cleanup(); callback?.Invoke();
        }
        void Cleanup()
        {
            session++; loadingDevices = false; capture = null;
            if (deviceVoice != null) deviceVoice.DevicesChanged -= RefreshDevices;
            deviceVoice = null;
            if (inputDraft != null) Destroy(inputDraft); inputDraft = null;
        }
        public void Close()
        {
            closing = true; closed = null; view?.Hide(); closing = false; Cleanup();
            main.Hide(); teammates.Hide();
        }
        void OnDestroy() => Cleanup();
    }
}
