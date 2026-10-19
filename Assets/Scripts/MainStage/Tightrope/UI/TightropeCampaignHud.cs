using System;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Tightrope.UI
{
    /// <summary>독립 외줄 테스트 씬에서 공용 공연 시작과 정산 결과를 확인한다.</summary>
    public sealed class TightropeCampaignHud : MonoBehaviour
    {
        static TightropeCampaignHud activeHud;
        int restartingGeneration;
        float lastRestartAt = -10f;
        NetworkSessionManager scopedSession;
        IDisposable soloStartScope;
        bool connectingSolo, startPending;
        float startDeadline;
        string startError;
        TightropeActionButtons actions;
        static bool IsExternallyManaged => TightropeRun.Current != null && TightropeRun.Current.ExternallyManaged;

        /// <summary>독립 테스트 씬의 시작 버튼이 필요한 동안 카메라가 커서를 잠그지 않게 한다.</summary>
        public static bool RequiresStartCursor
        {
            get
            {
                if (activeHud == null || !activeHud.isActiveAndEnabled || IsExternallyManaged) return false;
                var campaign = TightropeCampaignBridge.Current != null ? TightropeCampaignBridge.Current.Campaign : null;
                return campaign == null || campaign.State.Phase == CampaignPhase.None || campaign.State.Phase == CampaignPhase.Intro;
            }
        }

        void OnEnable()
        {
            activeHud = this;
            BindSoloStart();
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            actions = new TightropeActionButtons(transform, "Tightrope start", () =>
            {
                var session = NetworkSessionManager.Instance;
                if (session == null || session.ConnectionState == SessionConnectionState.Offline) _ = StartSoloAsync();
                else StartCourseByHost();
            });
        }

        void BindSoloStart()
        {
            if (IsExternallyManaged)
            {
                soloStartScope?.Dispose();
                soloStartScope = null;
                scopedSession = null;
                return;
            }
            var session = NetworkSessionManager.Instance;
            if (session == scopedSession && soloStartScope != null) return;
            soloStartScope?.Dispose();
            scopedSession = session;
            soloStartScope = session != null ? session.RequestMinimumStartPlayers(1) : null;
        }

        void Update()
        {
            BindSoloStart();
            if (IsExternallyManaged) { startPending = false; restartingGeneration = 0; return; }
            if (startPending) ContinueStart();
            var bridge = TightropeCampaignBridge.Current;
            if (restartingGeneration != 0)
            {
                var campaign = bridge != null ? bridge.Campaign : null;
                int expected = restartingGeneration;
                restartingGeneration = 0;
                // 새 세대의 전원 복구·정산 초기화 알림이 끝난 다음 프레임에 다시 공연한다.
                if (campaign != null && campaign.State.Generation == expected && campaign.State.Phase == CampaignPhase.Intro
                    && NetworkSessionManager.Instance?.IsHost == true)
                    campaign.EndIntro();
            }
            if (!InputGate.IsBlocked && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RestartCourseByHost();
        }

        /// <summary>테스트 씬 R: 방은 유지하고 코스·플레이어·타이머·현재 명부·자금을 처음부터 다시 시작한다.</summary>
        public bool RestartCourseByHost()
        {
            var bridge = TightropeCampaignBridge.Current;
            var session = NetworkSessionManager.Instance;
            if (!isActiveAndEnabled || restartingGeneration != 0 || Time.unscaledTime - lastRestartAt < 0.25f
                || bridge == null || !bridge.isActiveAndEnabled || session == null || !session.IsHost
                || NetworkPlayer.All.Count == 0) return false;
            if (TightropeRun.Current != null && TightropeRun.Current.ExternallyManaged) return false;
            var campaign = bridge.Campaign;
            if (campaign == null || (campaign.State.Phase == CampaignPhase.Performance && !bridge.IsTightropePerformance)) return false;
            if (session.GameState == GameSessionState.Waiting)
            {
                if (TightropeRun.Current == null || !TightropeRun.Current.ResetWaitingPracticeByHost()) return false;
                lastRestartAt = Time.unscaledTime;
                InputGate.RequireRelease();
                return true;
            }
            if (session.GameState != GameSessionState.Playing || !campaign.DebugRestartRun()) return false;
            NetworkPlayer.ServerRestoreEveryone();
            NetworkPlayer.ServerRestartAll();
            restartingGeneration = campaign.State.Generation;
            lastRestartAt = Time.unscaledTime;
            InputGate.RequireRelease();
            return true;
        }

        void OnDisable()
        {
            if (activeHud == this) activeHud = null;
            actions?.Dispose(); actions = null;
            restartingGeneration = 0;
            startPending = false;
            soloStartScope?.Dispose();
            soloStartScope = null;
            scopedSession = null;
        }

        /// <summary>외줄 씬에서만 1인 방을 만들고 공연까지 시작한다. 전역 개발자 모드나 세팅은 변경하지 않는다.</summary>
        public async Task<bool> StartSoloAsync()
        {
            if (!isActiveAndEnabled || IsExternallyManaged || connectingSolo || startPending) return false;
            var session = NetworkSessionManager.EnsureExists();
            if (session == null || session.ConnectionState != SessionConnectionState.Offline) return false;
            BindSoloStart();
            connectingSolo = true;
            startError = null;
            try
            {
                bool connected = await session.HostLanAsync();
                if (this == null || !isActiveAndEnabled || IsExternallyManaged) return false;
                if (!connected) { startError = "방을 만들지 못했습니다. 다시 시도해 주세요."; return false; }
                QueueStart();
                return true;
            }
            catch (Exception exception)
            {
                if (this != null) startError = "방을 만들지 못했습니다. 다시 시도해 주세요.";
                Debug.LogException(exception);
                return false;
            }
            finally { if (this != null) connectingSolo = false; }
        }

        /// <summary>방장용 시작 버튼 하나로 방 대기 → 도입 → 외줄 공연을 연결한다.</summary>
        public bool StartCourseByHost()
        {
            var session = NetworkSessionManager.Instance;
            var bridge = TightropeCampaignBridge.Current;
            if (!isActiveAndEnabled || IsExternallyManaged || startPending || connectingSolo || session == null || !session.IsHost
                || bridge == null || bridge.Campaign == null) return false;
            BindSoloStart();
            if (session.GameState == GameSessionState.Waiting)
            {
                if (!session.CanStartGame) return false;
                QueueStart();
                return true;
            }
            return StartPerformanceByHost();
        }

        void QueueStart()
        {
            startPending = true;
            startDeadline = Time.unscaledTime + 10f;
            startError = null;
            InputGate.RequireRelease();
        }

        void ContinueStart()
        {
            var session = NetworkSessionManager.Instance;
            var bridge = TightropeCampaignBridge.Current;
            if (Time.unscaledTime > startDeadline || session == null || !session.IsHost || bridge == null)
            {
                startPending = false;
                startError = "시작을 완료하지 못했습니다. 다시 눌러 주세요.";
                return;
            }
            if (session.GameState == GameSessionState.Waiting && session.CanStartGame)
                session.StartGame();
            if (session.GameState == GameSessionState.Playing && bridge.Campaign != null
                && bridge.Campaign.State.Phase == CampaignPhase.Intro)
                StartPerformanceByHost();
            if (bridge.PerformanceOpen) startPending = false;
        }

        /// <summary>독립 테스트 씬의 공연 시작 버튼. 방 시작 후 도입 단계에서 호스트가 장애물·타이머를 시작한다.</summary>
        public bool StartPerformanceByHost()
        {
            var bridge = TightropeCampaignBridge.Current;
            var session = NetworkSessionManager.Instance;
            if (!isActiveAndEnabled || IsExternallyManaged || bridge == null || !bridge.isActiveAndEnabled
                || session == null || !session.IsHost || session.GameState != GameSessionState.Playing
                || bridge.Campaign == null || bridge.Campaign.State.Phase != CampaignPhase.Intro) return false;
            if (!bridge.Campaign.EndIntro()) return false;
            InputGate.RequireRelease();
            return true;
        }

        void OnGUI()
        {
            using var buttonsFrame = actions?.Begin();
            TightropeControlHints.Draw();
            if (IsExternallyManaged) return;
            var bridge = TightropeCampaignBridge.Current;
            if (bridge == null || bridge.Campaign == null) return;
            var campaign = bridge.Campaign;
            var state = campaign.State;
            var session = NetworkSessionManager.Instance;
            float width = Mathf.Min(370f, Screen.width - 20f);
            var area = new Rect(Mathf.Max(10f, Screen.width - width - 10f), 10f, width, Mathf.Min(410f, Screen.height - 20f));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("외줄타기 · 공연 진행");
            GUILayout.Label(session != null && session.IsHost
                ? "R: 코스 전체 다시 시작 (부상·타이머·자금 초기화)"
                : "코스 전체 리셋: 방장이 R");
            if (state.Phase == CampaignPhase.None)
            {
                GUILayout.Label("출발 플랫폼에서 원하는 줄을 골라 올라가세요.");
                GUILayout.Label("완주는 4인 협동 · 마지막 정면 톱에서 4명 목마가 필요해요.");
                GUILayout.Label("혼자 시작·연습 봇 1명: 앞쪽 구간 연습용");
                if (session == null || session.ConnectionState == SessionConnectionState.Offline)
                {
                    actions?.Reserve(0, "혼자 시작", area, !connectingSolo && !startPending);
                    GUILayout.Label("함께하려면 왼쪽에서 방을 만들거나 참가하세요.");
                }
                else if (session.IsHost && session.GameState == GameSessionState.Waiting && !startPending)
                {
                    actions?.Reserve(0, "외줄타기 시작 (1명부터 가능)", area);
                }
                else if (!session.IsHost) GUILayout.Label("방장이 공연을 시작할 때까지 기다려 주세요.");
            }
            else
                GUILayout.Label($"{state.Day}일차 · {CampaignRun.Describe(state)}");

            if (state.Phase == CampaignPhase.Intro)
            {
                GUILayout.Label("현재 이동 연습 중 · 톱날과 불은 대기합니다.");
                if (session != null && session.IsHost)
                {
                    GUILayout.Label("아래 버튼을 눌러 공연을 시작하세요.");
                    actions?.Reserve(0, "외줄타기 시작 (톱날 작동)", area, !startPending);
                }
                else GUILayout.Label("방장이 공연을 시작할 때까지 기다려 주세요.");
            }
            if (bridge.PerformanceOpen)
            {
                var run = TightropeRun.Current;
                GUILayout.Label($"남은 시간: {(run != null ? run.TimeRemaining : 0f):0}초");
                var course = TightropeCourse.Current;
                var local = NetworkPlayer.Local;
                if (course != null && local != null)
                {
                    float distance = Mathf.Clamp(course.GetDistance(local.transform.position), 0f, course.FinishDistance);
                    int section = course.GetSectionIndex(distance);
                    if (section >= 0)
                    {
                        var info = course.Shape.Sections[section];
                        var previous = GUI.color;
                        GUI.color = info.color;
                        GUILayout.Label($"{section + 1}구간 · {info.label}   {distance:0} / {course.FinishDistance:0}m");
                        GUI.color = previous;
                    }
                }
                GUILayout.Label("한 명이 도착하면 성공 · 전원 사망하거나 시간이 끝나면 실패");
                GUILayout.Label("사망하면 공연이 끝날 때까지 관전합니다.");
                var saws = Saws.TightropeSaws.Current;
                if (saws != null && saws.IsCooperativeGateActive)
                {
                    GUILayout.Label($"최후반 {saws.CooperativeGateLane + 1}번 줄 · 정면 톱 한 번");
                    GUILayout.Label("4층·3층: 먼저 넘어 버튼을 피하고 앞쪽 대기");
                    GUILayout.Label("2층: 마지막으로 뒤 버튼 · 1층: 톱이 뛰면 전진");
                    GUILayout.Label("정면 톱: 즉사·2.25m 홉 / 측면 톱: 절단");
                    if (!saws.IsCooperativeStackApproved)
                        GUILayout.Label($"현재 출발 표시에 {saws.CooperativeRequiredStackSize}명 목마가 필요합니다.");
                }
                var fire = Fire.TightropeFire.Current;
                if (fire != null && fire.PendingSegment >= 0 && fire.Settings != null)
                {
                    int index = fire.PendingSegment;
                    GUILayout.Label($"{fire.Settings.segments[index].lane}번 줄: {fire.WarningRemaining(index):0.0}초 뒤 불탑니다.");
                    int safeLane = fire.Settings.FindSurvivingLane(TightropeCourse.Current.LaneCount);
                    int burningLane = fire.Settings.segments[index].lane;
                    if (safeLane > 0)
                        GUILayout.Label($"{burningLane}번 줄의 {(safeLane < burningLane ? "왼쪽" : "오른쪽")}에 남은 줄로 건너가세요.");
                    else GUILayout.Label("불붙기 전에 옆줄로 이동하세요.");
                    GUILayout.Label("Q / E를 누른 채 Space: 옆줄로 점프");
                }
            }
            if (connectingSolo || startPending) GUILayout.Label("외줄타기를 준비하고 있습니다…");
            if (!string.IsNullOrEmpty(startError)) GUILayout.Label(startError);
            var result = bridge.Result;
            if (result != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label(result.Succeeded ? "공연 성공" : "공연 실패");
                GUILayout.Label($"이번 수입: {result.Income} · 공동 자금: {bridge.Rewards.Balance}");
                GUILayout.Label($"사망: {result.Deaths.Count}명 / 참가: {result.Participants.Count}명");
                if (state.Phase == CampaignPhase.RestTime || state.Phase == CampaignPhase.Night)
                    GUILayout.Label("다음 시간대로 넘어갔습니다. 사망자는 외팔 상태로 부활합니다.");
            }
            if (state.Phase == CampaignPhase.Performance && !bridge.IsTightropePerformance)
                GUILayout.Label("다른 묘기 차례입니다. 외줄타기 판정은 멈춥니다.");
            GUILayout.EndArea();
        }
    }
}
