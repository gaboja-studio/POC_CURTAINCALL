using System.Collections;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using CurtainCall.UI.SceneIntegration;
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
        int day = 1, slot, content;
        RunStartPhase phase;
        GUIStyle label, button, heading;
        Vector2 scroll;
        static readonly string[] Contents = { "외줄타기", "인간대포" };
        static readonly string[] Tricks = { "Tightrope", "HumanCannon" };
        static readonly string[] Phases = { "공연", "아침", "공연 사이", "밤 정비" };

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
                // 같은 프레임에 최종 요청을 넣어 도입/로비를 경유하지 않는다.
                if (!campaign.DebugStartAt(point) || !director.DebugReloadCurrentStep())
                { Status = "개발 시작 상태를 적용하지 못했습니다."; yield break; }
                Status = $"{point.Day}일차 · {scene} 로딩 중…";
                until = Time.realtimeSinceStartup + 60;
                while (director.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
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
            GUILayout.Label("새 테스트 런으로 시작합니다. 자금·소지품·부상·이전 결과가 초기화되며, 접속한 팀원도 함께 이동합니다.", label);
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
                GUI.enabled = enabled && !IsBusy && (day <= GameSettings.Base.Run.DayCount || i == 1) && (day > 1 || i != 1);
                if (GUILayout.Toggle((int)phase == i, Phases[i], button)) phase = (RunStartPhase)i;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = enabled && !IsBusy;
            if (day > GameSettings.Base.Run.DayCount) phase = RunStartPhase.Morning;
            else if (day == 1 && phase == RunStartPhase.Morning) phase = RunStartPhase.Performance;
            if (phase == RunStartPhase.Morning) slot = 0;
            bool chooseContent = phase != RunStartPhase.Night && day <= GameSettings.Base.Run.DayCount;
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
            }
            GUILayout.Space(12);
            GUILayout.Label(Status, label);
            if (GUILayout.Button(IsBusy ? "준비 중…" : "선택 지점에서 새 테스트 시작", button))
                TryLaunch(new RunStartPoint(day, phase, slot, Tricks[content]));
            if (GUILayout.Button($"닫기 [{ShortcutLabel} / F8]", button)) Toggle();
            GUI.enabled = enabled;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
