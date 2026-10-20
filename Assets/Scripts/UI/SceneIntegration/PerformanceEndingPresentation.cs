using System.Linq;
using System.Text;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Results;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using CurtainCall.UI.NotificationsV1;
using CurtainCall.UI.PanelsV1;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>전환 씬에서 생성하고 Single 공간 교체 동안 유지하는 로컬 표시. 판정/씬 로드는 기존 시스템 소유다.</summary>
    public sealed class PerformanceEndingPresentation : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/UI/PerformanceTransitionUI.unity";
        public static PerformanceEndingPresentation Current { get; private set; }
        [SerializeField] SystemNotificationsView notifications;
        [SerializeField] CurtainCallPanelView resultPanel;
        [SerializeField] TMP_Text title, summary;
        [SerializeField] CanvasGroup black;
        PerformanceRewards rewards;
        string deathIdentity, shownIdentity;
        PerformanceResult pending;
        double started, loadedAt = -1;
        string sourceScene;
        public bool ResultVisible => resultPanel != null && resultPanel.IsShown;
        public bool BlackVisible => black != null && black.alpha > .99f;
        public bool DeathVisible => notifications != null && notifications.Banner.IsVisible;
        float ResultAt => PerformanceEndingSettings.Current != null ? PerformanceEndingSettings.Current.ResultAt : 1.5f;
        float BlackAt => PerformanceEndingSettings.Current != null ? PerformanceEndingSettings.Current.BlackAt : 5.5f;

        void Awake()
        {
            if (Current != null && Current != this) { Destroy(gameObject); return; }
            Current = this;
            DontDestroyOnLoad(gameObject);
            ResetPresentation();
            SceneManager.sceneLoaded += SceneLoaded;
        }
        void Update()
        {
            if (rewards != PerformanceRewards.Instance)
            {
                if (rewards != null) rewards.ResultConfirmed -= PresentResult;
                rewards = PerformanceRewards.Instance;
                if (rewards != null) rewards.ResultConfirmed += PresentResult;
            }
            var snapshot = rewards != null ? rewards.State : null;
            if (snapshot == null || snapshot.Performance == null)
            { ResetPresentation(); shownIdentity = deathIdentity = null; return; }
            ObserveDeath();
            // 공유 스냅샷이 구독보다 먼저 도착한 경우도 현재 공연 결과만 복원한다.
            if (pending == null && CommonUiSceneLoader.CurrentSpace != null && snapshot.Latest != null
                && snapshot.Latest.Performance.Id == snapshot.Performance.Id) PresentResult(snapshot.Latest);
            if (pending == null) return;
            if (snapshot.Performance.Id != pending.Performance.Id) { ResetPresentation(); return; }
            double elapsed = Time.unscaledTimeAsDouble - started;
            if (elapsed >= ResultAt && !ResultVisible && !BlackVisible) resultPanel.SetVisible(true, false);
            if (elapsed >= BlackAt) Cover();
            float hold = PerformanceEndingSettings.Current != null ? PerformanceEndingSettings.Current.loadedHoldSeconds : .5f;
            if (loadedAt >= 0 && Time.unscaledTimeAsDouble - loadedAt >= Mathf.Max(.1f, hold)
                && (SpaceLinkDirector.Instance == null || !SpaceLinkDirector.Instance.IsTransitioning)) ResetPresentation();
            float timeout = PerformanceEndingSettings.Current != null ? PerformanceEndingSettings.Current.timeoutSeconds : 45;
            if (pending != null && elapsed > Mathf.Max(10, timeout))
            {
                ResetPresentation();
                notifications.Banner.Show("화면 전환을 완료하지 못했습니다", "연결 상태를 확인해 주세요", BannerState.Danger, 4);
            }
        }
        void ObserveDeath()
        {
            var local = NetworkPlayer.Local;
            var state = rewards.State;
            if (local == null || state?.Performance == null || CommonUiSceneLoader.CurrentSpace == null) return;
            bool dead = local.State == PlayerState.Fallen || local.State == PlayerState.Retired
                || state.Segments.Any(p => p.ClientId == local.OwnerClientId && p.Dead);
            if (!dead || deathIdentity == state.Performance.Id) return;
            deathIdentity = state.Performance.Id;
            float duration = PerformanceEndingSettings.Current != null ? PerformanceEndingSettings.Current.deathNoticeSeconds : 2;
            notifications.Banner.Show("사망했습니다", "이 공연의 개인 호응은 0으로 고정됩니다", BannerState.Danger,
                Mathf.Max(.1f, duration), BannerIcon.Skull);
        }
        public void PresentResult(PerformanceResult result)
        {
            if (!isActiveAndEnabled || result == null || shownIdentity == result.Performance.Id || CommonUiSceneLoader.CurrentSpace == null
                || (result.Performance.TrickId != "Tightrope" && result.Performance.TrickId != "HumanCannon")) return;
            shownIdentity = result.Performance.Id;
            pending = result; started = Time.unscaledTimeAsDouble; loadedAt = -1;
            sourceScene = CommonUiSceneLoader.CurrentSpace.gameObject.scene.path;
            title.text = result.Succeeded ? "공연 성공" : "공연 실패";
            var text = new StringBuilder();
            text.Append("팀 호응  ").Append(result.Participants.Sum(p => p.Segment)).Append('\n');
            text.Append("공동 자금 수입  +").Append(result.Income).Append("\n\n");
            foreach (var person in result.Participants.OrderBy(p => p.Slot))
                text.Append("단원 ").Append(person.Slot + 1).Append("   ")
                    .Append(person.Dead ? "사망" : !person.Connected ? "연결 종료" : "생존")
                    .Append("   호응 ").Append(person.Segment).Append('\n');
            text.Append("\n잠시 후 다음 공간으로 이동합니다");
            summary.text = text.ToString();
            resultPanel.SetInputAllowed(false); resultPanel.SetVisible(false, true);
            InputGate.SetBlocking(this, true);
            ObserveDeath();
        }
        void Cover()
        {
            black.alpha = 1;
            black.blocksRaycasts = true;
            resultPanel.SetVisible(false, true);
        }
        // 개발용 IMGUI도 임시 검은 화면 위에 비치지 않게 한다. 게임 UI의 본체는 씬의 Canvas다.
        void OnGUI()
        {
            if (!BlackVisible) return;
            GUI.depth = -10000;
            var color = GUI.color;
            bool enabled = GUI.enabled;
            GUI.color = Color.white;
            GUI.enabled = true;
            GUI.ModalWindow(GetInstanceID(), new Rect(0, 0, Screen.width, Screen.height),
                _ => GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture), "", GUIStyle.none);
            GUI.BringWindowToFront(GetInstanceID());
            GUI.color = color;
            GUI.enabled = enabled;
        }
        void OnDisable() => ResetPresentation();
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (pending == null || scene.path == sourceScene || scene.path == ScenePath || scene.path == CommonPerformanceUI.ScenePath) return;
            if (!SpaceRoute.TryFind(scene.name, GameSettings.Base.SpaceTransition, out _)) return;
            Cover(); loadedAt = Time.unscaledTimeAsDouble;
        }
        public void ResetPresentation()
        {
            pending = null; loadedAt = -1;
            if (black != null) { black.alpha = 0; black.blocksRaycasts = false; }
            if (resultPanel != null) resultPanel.SetVisible(false, true);
            if (notifications != null) notifications.ResetForRuntime();
            InputGate.SetBlocking(this, false);
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (rewards != null) rewards.ResultConfirmed -= PresentResult;
            InputGate.SetBlocking(this, false);
            if (Current == this) Current = null;
        }
    }
}
