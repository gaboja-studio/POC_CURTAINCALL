using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.StartLobby;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.WaitingRoom;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Sound
{
    /// <summary>
    /// 사운드 확인 전용 씬(Scenes/Tests/Systems/Sound/UiSoundTest)의 조작 패널.
    /// - 적용 상황 재현: 게임 코드와 같은 함수(방송 → ExplorationTimerView, 사망 카드 → HudUIManager.ShowDeathWarning,
    ///   탈출로 → EscapeLocalDeathSound, 타이머 → RestTimerView)를 불러 카드·배너와 소리를 함께 확인한다.
    /// - 사운드(시트 ID) 바로 듣기와 최근 재생 기록(호출 코드 위치·결과).
    /// 네트워크·런 진행과 연결하지 않는다. 플레이 중 사운드 설정 창에서 바꾼 값은 다음 재생부터 반영된다.
    /// </summary>
    public sealed class SoundCuePreview : MonoBehaviour
    {
        [SerializeField] RestTimerView timer;
        [Tooltip("비활성 오브젝트의 실제 방송 진입점. 런 진행 없이 소집 경보만 일으킨다.")]
        [SerializeField] ExplorationTimerAnnouncer announcer;
        [SerializeField] Font previewFont;
        [SerializeField, Min(1f)] float timerStartSeconds = 35f;
        [SerializeField, Min(1f)] float timerDurationSeconds = 120f;
        [SerializeField, Min(.5f)] float cardSeconds = 4f;

        bool timerRunning, timerPaused;
        float remaining, speed = 1f;
        Vector2 leftScroll, rightScroll;
        GUIStyle label, small, title, button;

        public void Configure(RestTimerView view, ExplorationTimerAnnouncer recall, Font font)
        {
            timer = view;
            announcer = recall;
            previewFont = font;
        }

        void OnEnable()
        {
            if (announcer != null) announcer.Announced += PresentBanner;
        }

        void OnDisable()
        {
            if (announcer != null) announcer.Announced -= PresentBanner;
        }

        void Start()
        {
            if (timer != null) timer.Hide();
        }

        // ── 상황 재현(게임 코드와 같은 함수) ─────────────

        /// <summary>실제 방송 진입점에서 소집 경보를 일으킨다 → ExplorationTimerView가 소리, 여기서는 배너만 띄운다.</summary>
        void RaiseRecall()
        {
            if (announcer == null) return;
            typeof(ExplorationTimerAnnouncer).GetMethod("Raise", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(announcer, new object[] { 60, true });
        }

        // 대기실 HUD(CommonWaitingRoomUI.PresentAnnouncement)와 같은 배너.
        void PresentBanner(ExplorationAnnouncement message)
        {
            var config = WaitingRoomHudProfile.Current;
            if (config == null) return;
            WaitingRoomHudProfile.Emit(message.IsRecallAlarm ? config.recall : config.broadcast, transform,
                message.Title ?? (message.IsRecallAlarm ? "소집 경보!" : "방송"), message.Message, cardSeconds);
        }

        void PerformanceDeath() => HudUIManager.Current.ShowDeathWarning("사망한 단원의 호응도가 절반으로 줄어듭니다", transform, "사망했습니다", cardSeconds,
            SoundSituations.PerformanceLocalDeath);

        void WaitingRoomDeath(bool local) => HudUIManager.Current.ShowDeathWarning("", transform, local ? "나 사망" : "단원 2 사망", cardSeconds,
            local ? SoundSituations.WaitingRoomLocalDeath : null);

        void StartTimer(float seconds)
        {
            timerRunning = true;
            timerPaused = false;
            remaining = seconds;
            if (timer != null) timer.Hide(); // 첫 프레임은 소리 없이 시작해 실제 진입과 같게 한다.
        }

        void StopTimer()
        {
            timerRunning = false;
            if (timer != null) timer.Hide();
        }

        void Update()
        {
            HandleShortcuts();
            if (timer == null || !timerRunning) return;
            if (!timerPaused) remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime * speed);
            var state = new CampaignSnapshot
            {
                Generation = 1, Day = 1, Phase = CampaignPhase.RestTime, Rest = RestKind.Between,
                Timer = timerPaused ? RestTimerState.Stopped : RestTimerState.Running, TimeRemaining = remaining,
            };
            timer.Show(state, timerDurationSeconds);
        }

        void HandleShortcuts()
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.digit1Key.wasPressedThisFrame) RaiseRecall();
            if (keys.digit2Key.wasPressedThisFrame) PerformanceDeath();
            if (keys.digit3Key.wasPressedThisFrame) WaitingRoomDeath(true);
            if (keys.digit4Key.wasPressedThisFrame) EscapeLocalDeathSound.Notify(PlayerState.Normal, PlayerState.Fallen, true);
            if (keys.digit5Key.wasPressedThisFrame) EscapeLocalDeathSound.Notify(PlayerState.EscapeCaptured, PlayerState.Retired, true);
            if (keys.tKey.wasPressedThisFrame) StartTimer(timerStartSeconds);
            if (keys.spaceKey.wasPressedThisFrame && timerRunning) timerPaused = !timerPaused;
        }

        // ── 화면 ─────────────────────────────────────────

        void OnGUI()
        {
            var catalog = SoundCatalog.Current;
            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            label ??= new GUIStyle(GUI.skin.label) { font = previewFont, fontSize = 18, richText = true };
            small ??= new GUIStyle(label) { fontSize = 15, wordWrap = true };
            title ??= new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold };
            button ??= new GUIStyle(GUI.skin.button) { font = previewFont, fontSize = 17, richText = true };
            try
            {
                // 위쪽 가운데는 개발용 시작 지점·치트 안내가, 오른쪽은 타이머가 쓴다.
                GUILayout.BeginArea(new Rect(24, 96, 860, 960), GUI.skin.box);
                DrawSituations(catalog);
                GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(900, 96, 460, 960), GUI.skin.box);
                DrawCues(catalog);
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        void DrawSituations(SoundCatalog catalog)
        {
            GUILayout.Label("UI 사운드 확인 · 적용 상황 재현", title);
            GUILayout.Label("게임 코드와 같은 함수를 불러 카드·배너와 소리를 함께 냅니다. 숫자키 1~5 = 위에서부터, T = 타이머 35초, Space = 타이머 멈춤.", small);
            GUILayout.Label($"사용자 설정 음량: 전체 {ClientSettings.Volume:0.00} · 효과음 {ClientSettings.SfxVolume:0.00}" + (catalog != null ? $" · 사운드 설정 UI {catalog.uiVolume:0.00}" : ""), small);
            GUILayout.Space(6);
            leftScroll = GUILayout.BeginScrollView(leftScroll, GUILayout.Height(580));
            Row(catalog, "1", SoundSituations.RecallBroadcast, "ExplorationTimerAnnouncer → ExplorationTimerView", RaiseRecall);
            Row(catalog, "2", SoundSituations.PerformanceLocalDeath, "HudUIManager.ShowDeathWarning (공연)", PerformanceDeath);
            Row(catalog, "3", SoundSituations.WaitingRoomLocalDeath, "HudUIManager.ShowDeathWarning (대기실 '나')", () => WaitingRoomDeath(true));
            Row(catalog, " ", null, "대기실 동료 사망 카드 — 소리 없어야 정상(UI-102 미결정)", () => WaitingRoomDeath(false), "동료 사망 (소리 없음)");
            Row(catalog, "4", SoundSituations.EscapeLocalDeath, "EscapeLocalDeathSound: Normal → Fallen", () => EscapeLocalDeathSound.Notify(PlayerState.Normal, PlayerState.Fallen, true));
            Row(catalog, "5", SoundSituations.EscapeLocalDeath, "EscapeLocalDeathSound: 포획 → Retired", () => EscapeLocalDeathSound.Notify(PlayerState.EscapeCaptured, PlayerState.Retired, true), "탈출로 내 포획 확정");
            Row(catalog, "T", SoundSituations.RestTimerWarning, "RestTimerView.Show (35초부터 타이머 재현)", () => StartTimer(timerStartSeconds));
            Row(catalog, " ", SoundSituations.RestTimerFinalTick, "RestTimerView.Show (12초부터 타이머 재현)", () => StartTimer(12f));
            if (catalog != null)
                foreach (var situation in catalog.situations)
                {
                    if (situation == null || IsCovered(situation.key)) continue;
                    Row(catalog, " ", situation.key, "재현 함수 없음 → 상황 키로 바로 재생", () => SoundManager.Play(situation.key));
                }
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.Label("휴식 타이머 재현", title);
            GUILayout.Label(timer == null ? "<color=#ff8080>타이머 UI가 연결되지 않았습니다.</color>"
                : timerRunning ? $"남은 시간 {remaining:0.0}초 · 속도 x{speed:0} {(timerPaused ? "· <color=#ffb060>멈춤(소리 없음)</color>" : "")}"
                : "정지 상태. 오른쪽 타이머가 30초에서 붉게(경고음), 10초부터 깜박이며 초마다 틱.", label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"T  {timerStartSeconds:0}초부터", button, GUILayout.Width(170), GUILayout.Height(40))) StartTimer(timerStartSeconds);
            if (GUILayout.Button("12초부터", button, GUILayout.Width(120), GUILayout.Height(40))) StartTimer(12f);
            if (GUILayout.Button(timerPaused ? "재개" : "멈춤", button, GUILayout.Width(100), GUILayout.Height(40)) && timerRunning) timerPaused = !timerPaused;
            if (GUILayout.Button("x1", button, GUILayout.Width(60), GUILayout.Height(40))) speed = 1f;
            if (GUILayout.Button("x3", button, GUILayout.Width(60), GUILayout.Height(40))) speed = 3f;
            if (GUILayout.Button("정지", button, GUILayout.Width(100), GUILayout.Height(40))) StopTimer();
            GUILayout.EndHorizontal();
        }

        static bool IsCovered(string key) => key == SoundSituations.RecallBroadcast || key == SoundSituations.PerformanceLocalDeath
            || key == SoundSituations.WaitingRoomLocalDeath || key == SoundSituations.EscapeLocalDeath
            || key == SoundSituations.RestTimerWarning || key == SoundSituations.RestTimerFinalTick;

        void Row(SoundCatalog catalog, string shortcut, string key, string path, System.Action action, string labelOverride = null)
        {
            var situation = catalog != null && key != null ? catalog.FindSituation(key) : null;
            var cue = catalog != null && key != null ? catalog.Resolve(key, out _) : null;
            string name = labelOverride ?? situation?.label ?? key ?? "";
            string sound = key == null ? "<color=#a0a0a0>소리 없음</color>"
                : situation == null ? "<color=#ff8080>상황 없음(설정 창에서 복원)</color>"
                : !situation.enabled ? "<color=#ffb060>상황 꺼짐</color>"
                : cue == null ? "<color=#ff8080>사운드 없음</color>"
                : $"{cue.id} {cue.situation}" + (!cue.enabled ? " <color=#ffb060>(꺼짐)</color>" : !cue.HasClip ? " <color=#ff8080>(클립 없음)</color>" : "");
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.BeginVertical(GUILayout.Width(640));
            GUILayout.Label($"[{shortcut}] <b>{name}</b>  →  {sound}", label);
            GUILayout.Label("     " + path, small);
            GUILayout.EndVertical();
            if (GUILayout.Button("▶ 재현", button, GUILayout.Width(120), GUILayout.Height(44))) action();
            GUILayout.EndHorizontal();
        }

        void DrawCues(SoundCatalog catalog)
        {
            GUILayout.Label("사운드 (시트 ID)", title);
            if (catalog == null)
            {
                GUILayout.Label("<color=#ff8080>Resources/Settings/Sound/SoundCatalog 가 없습니다.</color>", label);
                return;
            }
            rightScroll = GUILayout.BeginScrollView(rightScroll, GUILayout.Height(400));
            foreach (var cue in catalog.cues)
            {
                if (cue == null) continue;
                GUILayout.BeginHorizontal(GUI.skin.box);
                string status = !cue.enabled ? " <color=#ffb060>(꺼짐)</color>" : !cue.HasClip ? " <color=#ff8080>(클립 없음)</color>" : "";
                string playing = SoundManager.IsPlaying(cue.id) ? " <color=#7fd4ff>●</color>" : "";
                GUILayout.Label($"<b>{cue.id}</b> {cue.situation}{status}{playing}" + (cue.loop ? " <color=#7fd4ff>루프</color>" : ""), label, GUILayout.Width(300));
                if (GUILayout.Button("▶", button, GUILayout.Width(50), GUILayout.Height(34)))
                    SoundManager.Play(cue.id, new SoundPlayOptions { IgnoreCooldown = true });
                if (GUILayout.Button("■", button, GUILayout.Width(50), GUILayout.Height(34))) SoundManager.Stop(cue.id);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.Label("최근 재생 기록", title);
            var history = SoundManager.History;
            for (int i = history.Count - 1, shown = 0; i >= 0 && shown < 9; i--, shown++)
            {
                var record = history[i];
                string color = record.Result == SoundPlayResult.Played ? "#7fe08f" : record.Result == SoundPlayResult.Cooldown ? "#f0c060" : "#ff8070";
                string name = catalog.FindSituation(record.Request)?.label ?? record.Request;
                GUILayout.Label($"<color={color}>{record.Result}</color> {name} → {record.CueId}\n     <color=#a0a0a0>{record.Caller}</color>", small);
            }
        }
    }
}
