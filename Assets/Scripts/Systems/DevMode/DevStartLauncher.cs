using System.Collections;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.Reward;
using CurtainCall.WaitingRoom;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CurtainCall.Dev
{
    /// <summary>Dev 모드의 호스트가 일차/콘텐츠를 골라 새 테스트 런을 시작한다. 씬 배치 불필요.</summary>
    public sealed class DevStartLauncher : MonoBehaviour
    {
        public static DevStartLauncher Instance { get; private set; }
        public static DevStartLauncher EnsureExists()
        {
            if (Instance != null) return Instance;
            if (!DevMode.Enabled) return null;
            var go = new GameObject(nameof(DevStartLauncher));
            DontDestroyOnLoad(go);
            return go.AddComponent<DevStartLauncher>();
        }

        public bool IsBusy { get; private set; }
        public bool IsOpen { get; private set; }
        public bool LastLaunchSucceeded { get; private set; }
        public string Status { get; private set; } = "일차와 시작 시점을 선택하세요.";
        public static string ShortcutLabel => Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer
            ? "Cmd+Shift+U" : "Ctrl+Shift+U";
        UIInputBlocker input;
        int day = 1, slot, content, difficulty = 1;
        public bool PreserveItemsAndCredits { get; set; }
        public bool PreserveInjuries { get; set; }
        public bool PreserveWaitingRoomUnlocks { get; set; }
        public bool PreservePerformanceClears { get; set; }
        RunStartPhase phase;
        GUIStyle label, button, heading;
        Vector2 scroll;
        static readonly string[] Contents = { "외줄타기", "인간대포" };
        static readonly string[] Tricks = { "Tightrope", "HumanCannon" };
        static readonly string[] Difficulties = { "1단계", "2단계", "3단계" };
        // RunStartPhase 순서와 같다. 탈출로는 대기실 합류 없이 탈출로 출발점에서 바로 시작한다(Task-089).
        static readonly string[] Phases = { "공연", "아침", "공연 사이", "밤 정비", "탈출로", "리허설" };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            input = gameObject.AddComponent<UIInputBlocker>();
            input.BlockWhileActive = false;
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (!DevMode.Enabled || keyboard == null) return;
            bool modifier = keyboard.ctrlKey.isPressed || keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed;
            if (keyboard.f8Key.wasPressedThisFrame || (modifier && keyboard.shiftKey.isPressed && keyboard.uKey.wasPressedThisFrame)) Toggle();
        }
        public void Toggle()
        {
            if (!DevMode.Enabled || IsBusy) return;
            IsOpen = !IsOpen;
            input.SetOpen(IsOpen);
        }

        public bool TryLaunch(RunStartPoint point)
        {
            if (IsBusy) return false;
            LastLaunchSucceeded = false;
            if (!DevMode.Enabled) { Status = "Dev 모드를 켜 주세요."; return false; }
            if (!point.IsValid(out var reason)) { Status = reason; return false; }
            var session = NetworkSessionManager.Instance;
            if (session != null && session.ConnectionState != SessionConnectionState.Offline && !session.IsHost)
            { Status = "접속 중에는 호스트만 시작 지점을 변경할 수 있습니다."; return false; }
            if (SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.IsTransitioning)
            { Status = "현재 씬 전환이 끝난 뒤 다시 시도하세요."; return false; }
            string scene = SpaceRoute.SceneName(SpaceRoute.ForPhase(point.TargetPhase), GameSettings.Base.SpaceTransition, point.TrickId);
            if (!Application.CanStreamedLevelBeLoaded(scene))
            { Status = $"Build Profiles의 씬 목록에 {scene} 씬을 추가해 주세요."; return false; }
            IsBusy = true;
            IsOpen = true;
            input.SetOpen(true);
            StartCoroutine(Launch(point, scene));
            return true;
        }

        IEnumerator Launch(RunStartPoint point, string scene)
        {
            var uiTransition = CommonUiSceneLoader.DeferSceneChanges();
            var items = PreserveItemsAndCredits ? PropCarry.Instance?.CaptureDevSnapshot() : null;
            var credits = PreserveItemsAndCredits ? PerformanceRewards.Instance?.Balance ?? 0 : 0;
            var exploration = PreserveWaitingRoomUnlocks ? ExplorationState.Instance?.CaptureDevSnapshot() : null;
            bool cannonUnlocked = PreserveWaitingRoomUnlocks && CampaignRun.Instance != null && CampaignRun.Instance.State.CannonUnlocked;
            var priorCampaign = CampaignRun.Instance;
            var clearHistory = PreservePerformanceClears && priorCampaign != null
                ? (priorCampaign.ClearedTier(CampaignSnapshot.TightropeId), priorCampaign.ClearedTier(CampaignSnapshot.HumanCannonId))
                : default;
            var injuries = new Dictionary<ulong, BodyPart>();
            if (PreserveInjuries)
            {
                foreach (var player in NetworkPlayer.All) if (player != null && player.IsSpawned) injuries[player.OwnerClientId] = player.LostParts;
                if (injuries.Count == 0)
                {
                    var reader = Object.FindFirstObjectByType<PlayerInputReader>();
                    var condition = reader != null ? reader.GetComponent<PlayerCondition>() : null;
                    if (condition != null) injuries[0] = condition.LostParts;
                }
            }
            try
            {
                Status = "테스트 세션 준비 중…";
                var session = NetworkSessionManager.EnsureExists();
                var campaign = CampaignRun.EnsureExists();
                var director = SpaceLinkDirector.EnsureExists();
                if (session == null) { Status = "네트워크 관리자 프리팹을 찾지 못했습니다."; yield break; }
                // 새 관리자들의 Start 이벤트 구독을 먼저 완료한다.
                yield return null;
                float until = Time.realtimeSinceStartup + 30;
                while (CommonUiSceneLoader.IsLoading && Time.realtimeSinceStartup < until) yield return null;
                if (CommonUiSceneLoader.IsLoading) { Status = "기존 UI 로드를 마친 뒤 다시 시도하세요."; yield break; }
                if (session.ConnectionState == SessionConnectionState.Offline)
                {
                    campaign.StopOfflineRun();
                    var hosting = session.HostLanAsync();
                    while (!hosting.IsCompleted) yield return null;
                    if (hosting.IsFaulted || hosting.IsCanceled || !hosting.Result)
                    { Status = "LAN 호스트를 시작하지 못했습니다. 포트/접속 상태를 확인하세요."; yield break; }
                }
                until = Time.realtimeSinceStartup + 30;
                while (session.IsHost && (NetworkPlayer.Local == null || !NetworkPlayer.Local.IsSpawned || NetworkPlayer.Local.Slot == NetworkPlayer.NoSlot))
                {
                    if (Time.realtimeSinceStartup > until) { Status = "플레이어 준비 시간이 초과되었습니다."; yield break; }
                    yield return null;
                }
                if (!session.IsHost) { Status = "호스트 연결이 필요합니다."; yield break; }
                PerformanceEndingPresentation.Current?.ResetPresentation();
                if (session.GameState == GameSessionState.Ended) session.ReturnToLobby();
                if (session.GameState == GameSessionState.Waiting && !session.StartGame())
                { Status = "게임을 시작하지 못했습니다."; yield break; }
                NetworkPlayer.ServerRestoreEveryone();
                if (PreserveInjuries)
                    foreach (var player in NetworkPlayer.All)
                        if (player != null && injuries.TryGetValue(player.OwnerClientId, out var parts)) player.ServerRestoreDevInjury(parts);
                // 같은 프레임에 최종 요청을 넣어 도입/로비를 경유하지 않는다.
                if (point.Phase == RunStartPhase.Rehearsal) WaitingRoomRehearsal.ResetForDevLaunch();
                if (!campaign.DebugStartAtPreservingUnlock(point, cannonUnlocked, clearHistory))
                { Status = "개발 시작 상태를 적용하지 못했습니다."; yield break; }
                int generation = campaign.State.Generation;
                // 캠페인 단계 이벤트의 자동 부활이 부상을 바꾸지 않도록 마지막에 다시 적용한다.
                if (PreserveInjuries)
                    foreach (var player in NetworkPlayer.All)
                        if (player != null && injuries.TryGetValue(player.OwnerClientId, out var parts)) player.ServerRestoreDevInjury(parts);
                if (items != null) PropCarry.Instance?.RestoreDevSnapshot(items, generation);
                if (exploration != null) ExplorationState.Instance?.RestoreDevSnapshot(exploration, generation);
                if (PreserveItemsAndCredits && credits > 0 && (PerformanceRewards.Instance == null || !PerformanceRewards.Instance.RestoreDevBalance(credits)))
                { Status = "기존 크레딧을 복원하지 못했습니다."; yield break; }
                if (!director.DebugReloadCurrentStep()) { Status = "개발 시작 씬을 불러오지 못했습니다."; yield break; }
                Status = $"{point.Day}일차 · {scene} 로딩 중…";
                until = Time.realtimeSinceStartup + 60;
                while (director.IsTransitioning && session.IsHost && campaign.State.Generation == generation
                    && (point.TargetPhase == CampaignPhase.Escape || Time.realtimeSinceStartup < until)) yield return null;
                var loaded = SceneManager.GetSceneByName(scene);
                if (director.IsTransitioning || !session.IsHost || !loaded.isLoaded || campaign.State.Day != point.Day || campaign.State.Phase != point.TargetPhase)
                { Status = "씬 전환을 완료하지 못했습니다. Console과 접속 상태를 확인하세요."; yield break; }
                uiTransition.Dispose();
                // 새 공간의 UI를 다시 붙인 뒤 조작을 돌려준다.
                yield return null;
                yield return null;
                until = Time.realtimeSinceStartup + 30;
                while (CommonUiSceneLoader.IsLoading && Time.realtimeSinceStartup < until) yield return null;
                if (CommonUiSceneLoader.IsLoading) { Status = "공통 UI 로딩 시간을 초과했습니다."; yield break; }
                while (SceneCurtainTransition.IsActive && Time.realtimeSinceStartup < until) yield return null;
                if (SceneCurtainTransition.IsActive) { Status = "커튼 전환 시간을 초과했습니다."; yield break; }
                LastLaunchSucceeded = true;
                Status = $"{point.Day}일차 테스트 시작 완료 · {ShortcutLabel}로 다시 선택";
                IsOpen = false;
            }
            finally
            {
                uiTransition.Dispose();
                IsBusy = false;
                input.SetOpen(IsOpen);
            }
        }

        void OnGUI()
        {
            if (!DevMode.Enabled || !IsOpen) return;
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            button ??= new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 36 };
            heading ??= new GUIStyle(label) { fontSize = 21, fontStyle = FontStyle.Bold };
            float width = Mathf.Min(580, Screen.width - 20), height = Mathf.Min(510, Screen.height - 90);
            var bounds = new Rect((Screen.width - width) / 2, 80, width, height);
            var color = GUI.color;
            GUI.color = new Color(.12f, .15f, .20f, 1);
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = color;
            GUILayout.BeginArea(bounds, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("DEV · 시작 지점", heading);
            GUILayout.Label("새 테스트 런으로 이동합니다. 선택하지 않은 항목과 이전 결과는 초기화되며 접속한 팀원도 함께 이동합니다.", label);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !IsBusy;
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", button, GUILayout.Width(48))) day = Mathf.Max(1, day - 1);
            GUILayout.Label($"{day}일차" + (day > GameSettings.Base.Run.DayCount ? " (마지막 아침)" : ""), heading);
            if (GUILayout.Button("▶", button, GUILayout.Width(48))) day = Mathf.Min(GameSettings.Base.Run.DayCount + 1, day + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Phases.Length; i++)
            {
                var candidate = (RunStartPhase)i;
                GUI.enabled = enabled && !IsBusy && (day <= GameSettings.Base.Run.DayCount || candidate == RunStartPhase.Morning || candidate == RunStartPhase.Escape)
                    && (day > 1 || candidate != RunStartPhase.Morning) && (day == 1 || candidate != RunStartPhase.Rehearsal);
                if (GUILayout.Toggle(phase == candidate, Phases[i], button)) phase = candidate;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = enabled && !IsBusy;
            GUILayout.Space(8);
            PreserveItemsAndCredits = GUILayout.Toggle(PreserveItemsAndCredits, " 현재 기물 및 크레딧 내용 유지");
            PreserveInjuries = GUILayout.Toggle(PreserveInjuries, " 현재 부상 유지");
            PreservePerformanceClears = GUILayout.Toggle(PreservePerformanceClears, " 공연 클리어 이력 유지");
            GUILayout.Label($"대기실 대상: {GameSettings.Base.SpaceTransition.WaitingRoomScene}", label);
            PreserveWaitingRoomUnlocks = GUILayout.Toggle(PreserveWaitingRoomUnlocks, " 대기실 해금 유지");
            if (day > GameSettings.Base.Run.DayCount && phase != RunStartPhase.Escape) phase = RunStartPhase.Morning;
            else if (day == 1 && phase == RunStartPhase.Morning) phase = RunStartPhase.Performance;
            else if (day != 1 && phase == RunStartPhase.Rehearsal) phase = RunStartPhase.Performance;
            if (phase == RunStartPhase.Morning || phase == RunStartPhase.Rehearsal) slot = 0;
            if (phase == RunStartPhase.Escape) GUILayout.Label("탈출로 출발점에서 바로 시작합니다(대기실 합류·로딩 생략). 사망하면 1초 뒤 출발점에서 다시 시작합니다.", label);
            if (phase == RunStartPhase.Rehearsal) GUILayout.Label("대기실 연습실의 최초 리허설부터 시작합니다. 끝나면 무대 입구 통로에 모여 1일차 첫 공연으로 갑니다.", label);
            bool chooseContent = phase != RunStartPhase.Night && phase != RunStartPhase.Escape && phase != RunStartPhase.Rehearsal && day <= GameSettings.Base.Run.DayCount;
            if (chooseContent)
            {
                int minSlot = phase == RunStartPhase.Between ? 1 : 0;
                slot = Mathf.Clamp(slot, minSlot, GameSettings.Base.Run.PerformancesPerDay - 1);
                GUILayout.Label(phase == RunStartPhase.Performance ? "공연 순번" : "쉬는 시간 이후 진행할 공연", label);
                GUILayout.BeginHorizontal();
                for (int i = minSlot; i < GameSettings.Base.Run.PerformancesPerDay; i++)
                {
                    GUI.enabled = enabled && !IsBusy && (phase != RunStartPhase.Morning || i == 0);
                    if (GUILayout.Toggle(slot == i, $"{i + 1}번째 공연", button)) slot = i;
                }
                GUILayout.EndHorizontal();
                GUI.enabled = enabled && !IsBusy;
                content = GUILayout.SelectionGrid(content, Contents, 2, button);
                if (phase == RunStartPhase.Performance)
                {
                    GUILayout.Label("공연 난이도 (이번 테스트 공연에 적용)", label);
                    difficulty = GUILayout.SelectionGrid(difficulty - 1, Difficulties, 3, button) + 1;
                }
            }
            GUILayout.Space(12);
            GUILayout.Label(Status, label);
            if (GUILayout.Button(IsBusy ? "준비 중…" : "선택 지점에서 새 테스트 시작", button))
                TryLaunch(new RunStartPoint(day, phase, slot, Tricks[content], phase == RunStartPhase.Performance ? difficulty : 1));
            if (GUILayout.Button($"닫기 [{ShortcutLabel} / F8] · 치트 창 [{DevCheatPanel.ShortcutLabel}]", button)) Toggle();
            GUI.enabled = enabled;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
