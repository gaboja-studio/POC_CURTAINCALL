using System.Linq;
using System.Collections.Generic;
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
        [SerializeField] ResultsPanelView resultsView;
        [SerializeField] TMP_Text title, summary;
        [SerializeField] CanvasGroup black;
        [SerializeField] TransitionCurtainView curtain;
        public TransitionCurtainView Curtain => curtain;
        PerformanceRewards rewards;
        string deathIdentity, shownIdentity;
        PerformanceResult pending;
        double started, loadedAt = -1;
        string sourceScene;
        readonly Dictionary<ulong, bool> observedDeaths = new();
        string observedPerformance;
        string resultBody;
        int lastCountdown = -1;
        public bool ResultVisible => (resultPanel != null && resultPanel.IsShown) || (resultsView != null && resultsView.IsVisible);
        bool UsesResults => resultsView != null && pending != null && pending.Participants.Count >= 1 && pending.Participants.Count <= 4;
        public bool BlackVisible => curtain != null ? curtain.IsBusy : black != null && black.alpha > .99f;
        HudRoleCard deathNotice;
        public HudRoleCard TeamWarning {get;private set;}
        // 첫 생존 도착 뒤 종료 여유 시간 안내(상단 시스템 안내, 2026-11-13).
        const string GraceTitle = "공연 종료 알림";
        HudRoleCard graceNotice;
        int graceSeconds = -1;
        public HudRoleCard GraceNotice => graceNotice != null && !graceNotice.Closing ? graceNotice : null;
        // 마지막 생존자에게만 묻는 공연 포기 선택. 공연마다 한 번, 시간이 지나면 계속하기.
        CurtainCall.WaitingRoom.RehearsalVotePanel forfeitPanel;
        string forfeitAsked;
        double forfeitStarted;
        public bool ForfeitVisible => forfeitPanel != null && forfeitPanel.IsVisible;
        public CurtainCall.WaitingRoom.RehearsalVotePanel ForfeitPanel => forfeitPanel;
        public bool DeathVisible => deathNotice != null && !deathNotice.Closing;
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
            if (CommonUiSceneLoader.CurrentSpace != null && CommonUiSceneLoader.CurrentSpace.PresentationOnly)
            { ResetPresentation(); return; }
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
            ObserveTeammates(snapshot);
            ObserveFinishGrace();
            ObserveForfeit(snapshot);
            // 공유 스냅샷이 구독보다 먼저 도착한 경우도 현재 공연 결과만 복원한다.
            if (pending == null && CommonUiSceneLoader.HasPerformanceSpace && snapshot.Latest != null
                && snapshot.Latest.Performance.Id == snapshot.Performance.Id) PresentResult(snapshot.Latest);
            if (pending == null) return;
            if (snapshot.Performance.Id != pending.Performance.Id) { ResetPresentation(); return; }
            double elapsed = Time.unscaledTimeAsDouble - started;
            if (elapsed >= ResultAt && !ResultVisible && !BlackVisible)
            {
                if (UsesResults) HudUIManager.ShowOutcome(BuildResults(pending),resultsView,false);

            }
            int seconds = Mathf.CeilToInt(Mathf.Max(0, BlackAt - (float)elapsed));
            if (seconds != lastCountdown)
            {
                lastCountdown = seconds;
                if (UsesResults && resultsView.IsVisible) resultsView.SetPresentation(BuildResults(pending));

            }
            if (elapsed >= BlackAt && loadedAt < 0) Cover();
            float hold = PerformanceEndingSettings.Current != null ? PerformanceEndingSettings.Current.loadedHoldSeconds : .5f;
            if (loadedAt >= 0 && Time.unscaledTimeAsDouble - loadedAt >= Mathf.Max(.1f, hold)
                && (SpaceLinkDirector.Instance == null || !SpaceLinkDirector.Instance.IsTransitioning)) ResetPresentation();
            float timeout = PerformanceEndingSettings.Current != null ? PerformanceEndingSettings.Current.timeoutSeconds : 45;
            if (pending != null && elapsed > Mathf.Max(10, timeout))
            {
                ResetPresentation();
                HudUIManager.Current.ShowSystemEvent("연결 상태를 확인해 주세요",transform,"화면 전환을 완료하지 못했습니다",4,HudEventKind.Warning);
            }
        }
        void ObserveTeammates(RewardSnapshot snapshot)
        {
            if (!CommonUiSceneLoader.HasPerformanceSpace) return;
            if (observedPerformance != snapshot.Performance.Id)
            {
                observedPerformance = snapshot.Performance.Id; observedDeaths.Clear();
                foreach (var person in snapshot.Segments) observedDeaths[person.ClientId] = person.Dead;
                return; // 늦은 접속/공연 변경은 이전 사망 알림을 재생하지 않는다.
            }
            var deaths = new List<string>();
            foreach (var person in snapshot.Segments)
            {
                if (observedDeaths.TryGetValue(person.ClientId, out bool previous) && !previous && person.Dead
                    && (NetworkPlayer.Local == null || person.ClientId != NetworkPlayer.Local.OwnerClientId))
                    deaths.Add($"단원 {person.Slot + 1}");
                observedDeaths[person.ClientId] = person.Dead;
            }
            if (deaths.Count == 0) return;
            string message = string.Join(", ", deaths) + " 사망";
            if (DeathVisible)
            {
                deathNotice.SetText(deathNotice.Content, deathNotice.Title + " · " + message);
                return;
            }
            TeamWarning=HudUIManager.Current.ShowDeathWarning("해당 단원의 호응도가 절반으로 줄어듭니다",transform,message,2);
        }
        void ObserveFinishGrace()
        {
            float remaining = rewards != null && pending == null && CommonUiSceneLoader.HasPerformanceSpace ? rewards.FinishGraceRemaining : 0f;
            if (remaining <= 0f) { CloseGraceNotice(); return; }
            int seconds = Mathf.Max(1, Mathf.CeilToInt(remaining));
            string text = $"{seconds}초 후에 공연이 종료됩니다";
            if (GraceNotice == null)
                graceNotice = HudUIManager.Current.ShowSystemEvent(text, transform, GraceTitle, remaining + .5f, HudEventKind.Notice);
            else if (seconds != graceSeconds) graceNotice.SetText(text, GraceTitle);
            // 표시 시간이 고정된 카드도 여유 시간이 끝날 때까지 남긴다.
            graceNotice.Extend(remaining + .5f);
            graceSeconds = seconds;
        }

        void CloseGraceNotice()
        {
            if (graceNotice != null) graceNotice.Close();
            graceNotice = null;
            graceSeconds = -1;
        }

        void ObserveForfeit(RewardSnapshot snapshot)
        {
            var local = NetworkPlayer.Local;
            bool eligible = local != null && local.State == PlayerState.Normal && pending == null
                && CommonUiSceneLoader.HasPerformanceSpace && rewards.IsForfeitCandidate(local.OwnerClientId);
            if (ForfeitVisible)
            {
                float limit = GameSettings.Base.Run.ForfeitPromptSeconds;
                float left = limit - (float)(Time.unscaledTimeAsDouble - forfeitStarted);
                // 선택 없이 시간이 지나면 계속하기와 같다.
                if (!eligible || left <= 0f) { HideForfeit(); return; }
                forfeitPanel.SetTimerGauge(left / limit);
                return;
            }
            if (!eligible || forfeitAsked == snapshot.Performance.Id) return;
            forfeitAsked = snapshot.Performance.Id;
            forfeitStarted = Time.unscaledTimeAsDouble;
            if (forfeitPanel == null) forfeitPanel = gameObject.AddComponent<CurtainCall.WaitingRoom.RehearsalVotePanel>();
            forfeitPanel.Show("공연 포기", "공연을 포기하고 대기실로 돌아갈까요?",
                new HudVoteChoice("계속하기", true, HideForfeit),
                new HudVoteChoice("포기하기", true, Forfeit));
            forfeitPanel.SetTimerGauge(1f);
        }

        void Forfeit()
        {
            HideForfeit();
            if (rewards != null) rewards.RequestForfeit();
        }

        void HideForfeit()
        {
            if (forfeitPanel != null) forfeitPanel.Hide();
        }

        public static string ResultTitle(bool succeeded, long amount, int participants, int cap) => !succeeded ? "공연 실패" :
            participants > 0 && cap > 0 && amount >= (double)participants * cap * .8 ? "공연 대성공" : "공연 성공";
        void ObserveDeath()
        {
            var local = NetworkPlayer.Local;
            var state = rewards.State;
            if (local == null || state?.Performance == null || !CommonUiSceneLoader.HasPerformanceSpace) return;
            bool dead = local.State == PlayerState.Fallen || local.State == PlayerState.Retired
                || state.Segments.Any(p => p.ClientId == local.OwnerClientId && p.Dead);
            if (!dead || deathIdentity == state.Performance.Id) return;
            deathIdentity = state.Performance.Id;
            float duration = PerformanceEndingSettings.Current != null ? PerformanceEndingSettings.Current.deathNoticeSeconds : 2;
            deathNotice = HudUIManager.Current.ShowDeathWarning("사망한 단원의 호응도가 절반으로 줄어듭니다",transform,"사망했습니다",Mathf.Max(.1f,duration));
        }
        public void PresentResult(PerformanceResult result)
        {
            if (!isActiveAndEnabled || result == null || shownIdentity == result.Performance.Id || !CommonUiSceneLoader.HasPerformanceSpace
                || (result.Performance.TrickId != "Tightrope" && result.Performance.TrickId != "HumanCannon")) return;
            shownIdentity = result.Performance.Id;
            pending = result; started = Time.unscaledTimeAsDouble; loadedAt = -1;
            sourceScene = CommonUiSceneLoader.CurrentSpace.gameObject.scene.path;
            lastCountdown = -1;
            InputGate.SetBlocking(this, true);
            ObserveDeath();
        }
        public ResultsPresentation BuildResults(PerformanceResult result)
        {
            var members = new List<ResultsMemberPresentation>();
            float? maximum = null;
            foreach (var person in result.Participants.OrderBy(p => p.Slot))
            {
                double? distance = person.FinalDistance;
                float? progress = result.CourseLength.HasValue && distance.HasValue
                    ? Mathf.Clamp01((float)(distance.Value / result.CourseLength.Value)) : null;
                if (progress.HasValue) maximum = Mathf.Max(maximum ?? 0, progress.Value);
                int id = members.Count + 1;
                bool local = NetworkPlayer.Local != null && NetworkPlayer.Local.OwnerClientId == person.ClientId;
                // 결과 칸(이름 208px·거리 160px, 24pt)에서 말줄임 없이 들어가도록 핵심만 짧게 쓴다. 사망 규칙은 하단 안내에 둔다.
                string status = person.Dead ? "사망" : !person.Connected ? "이탈" : person.Arrived ? "완주" : "생존";
                members.Add(new ResultsMemberPresentation(id, $"단원 {person.Slot + 1}{(local ? "(나)" : "")} · {status}",
                    progress, (distance.HasValue ? $"{distance:0}m" : "-") + $" · +{person.Settlement}",
                    Color.HSVToRGB((id - 1) * .23f, .5f, 1), local));
            }
            long score = result.Participants.Sum(p => p.Segment);
            int cap = rewards?.State != null ? rewards.State.Rules.Cap : 100;
            string heading = ResultTitle(result.Succeeded, score, result.Participants.Count, cap);
            var outcome = !result.Succeeded ? ResultsOutcome.Failure : heading == "공연 대성공" ? ResultsOutcome.GrandSuccess : ResultsOutcome.Success;
            var entry = rewards?.State?.Ledger.LastOrDefault(e => e.PerformanceId == result.Performance.Id && e.Kind == FundEntryKind.PerformanceIncome);
            string time = result.ElapsedSeconds.HasValue ? $"완료 시간 {result.ElapsedSeconds:0.0}초" : "불 타이머 시작 전 종료";
            return new ResultsPresentation(result.Performance.Id, result.Performance.Id, outcome, heading,
                $"{result.Performance.Day}일차 · {(result.Performance.TrickId == "Tightrope" ? "외줄타기" : "인간대포")}", time,
                "개인 최종 위치", result.CourseLength.HasValue ? $"도착 지점 {result.CourseLength:0.0}m" : "코스 기록 없음",
                maximum, "호응 보상", $"+{result.CheerReward}", $"완주 보너스 ({result.Participants.Count(p => p.Arrived)}명)", $"+{result.CompletionReward}", "공동 자금",
                $"이번 공연 +{result.Income}", (entry?.Balance ?? rewards?.Balance ?? 0).ToString(),
                $"호응 보상: 생존 2배 · 사망 1배   |   다음 공간으로 이동 {Mathf.Max(0, lastCountdown)}초", "", false, members.ToArray());
        }
        void Cover()
        {
            if (curtain != null) { curtain.SetLoadingText("다음 공간 준비 중"); curtain.Close(); }
            else if (black != null) black.alpha = 1;
            if (black != null) black.blocksRaycasts = curtain == null;
            if (resultPanel != null) resultPanel.SetVisible(false, true);
            if (resultsView != null) resultsView.Hide();
        }
        // 개발용 IMGUI도 임시 검은 화면 위에 비치지 않게 한다. 게임 UI의 본체는 씬의 Canvas다.
        void OnGUI()
        {
            if (curtain != null || !BlackVisible) return;
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
            loadedAt = Time.unscaledTimeAsDouble;
        }
        public void ResetPresentation()
        {
            if (deathNotice != null) deathNotice.Close(true); deathNotice = null;
            if(TeamWarning)TeamWarning.Close(true);TeamWarning=null;
            if (graceNotice != null) graceNotice.Close(true);
            graceNotice = null; graceSeconds = -1;
            HideForfeit();
            pending = null; loadedAt = -1;
            if (curtain != null && !SceneCurtainTransition.IsActive) curtain.Open();
            if (black != null) { black.alpha = 0; black.blocksRaycasts = false; }
            if (resultPanel != null) resultPanel.SetVisible(false, true);
            if (resultsView != null) resultsView.Hide();
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
