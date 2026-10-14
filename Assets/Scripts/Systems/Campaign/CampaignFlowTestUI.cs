using System.Collections.Generic;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Campaign
{
    /// <summary>
    /// 테스트 씬(Tests/Systems/Campaign/CampaignFlow) 전용 임시 화면. 런 상태를 보여 주고,
    /// 다른 공간이 보낼 신호(공연 종료·대기실 나옴·무대 입구 집결·탈출)를 버튼으로 흉내 낸다. 공간 연결(CC-111) 뒤 지운다.
    /// 접속 화면이 필요하면 같은 씬에 NetworkSessionTestUI를 함께 둔다(이 창은 그 오른쪽에 뜬다).
    /// </summary>
    public sealed class CampaignFlowTestUI : MonoBehaviour
    {
        const int MaxLogLines = 10;

        [SerializeField] Rect _panelRect = new Rect(360, 10, 400, 560);

        readonly List<string> _log = new();
        CampaignRun _run;

        void Start()
        {
            _run = CampaignRun.EnsureExists();
            _run.StepChanged += HandleStep;
            _run.RecallAlarm += HandleAlarm;
            _run.RunEnded += HandleRunEnded;
        }

        void OnDestroy()
        {
            if (_run == null) return;
            _run.StepChanged -= HandleStep;
            _run.RecallAlarm -= HandleAlarm;
            _run.RunEnded -= HandleRunEnded;
        }

        void HandleStep(CampaignSnapshot s) => AddLog($"세대 {s.Generation} · {s.Day}일차 · {CampaignRun.Describe(s)}");

        void HandleAlarm() => AddLog($"소집 경보 ({_run.State.TimeRemaining:0}초 남음)");

        void HandleRunEnded(RunEndReason reason) => AddLog($"런 종료: {reason}");

        void AddLog(string line)
        {
            _log.Add($"[{Time.time:0.0}s] {line}");
            if (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(_panelRect, GUI.skin.box);
            if (_run == null)
            {
                GUILayout.Label("CampaignRun이 없습니다.");
                GUILayout.EndArea();
                return;
            }

            var s = _run.State;
            var rules = GameSettings.Base.Run;
            GUILayout.Label($"런 진행 테스트 — 타이머 {(rules.RestTimerMode == RestTimerMode.FromRestStart ? "A안(시작부터)" : "B안(대기실에서 나온 뒤)")}");
            GUILayout.Label($"판정: {(_run.IsOfflineRun ? "오프라인" : _run.HasAuthority ? "호스트" : "받는 쪽")}");
            GUILayout.Label($"세대 {s.Generation} · {s.Day}일차 / {rules.DayCount}일 · {CampaignRun.Describe(s)}");

            if (s.Phase == CampaignPhase.RestTime || s.Phase == CampaignPhase.Escape)
                GUILayout.Label($"타이머: {TimerText(s)}{(s.AlarmRaised ? "  ⚠ 소집 경보" : "")}");
            if (s.Phase == CampaignPhase.Night)
                GUILayout.Label($"준비 완료 {s.ReadyCount}/{s.RequiredCount} (나: {(_run.IsLocalReady ? "완료" : "아직")})");
            if (s.LastEnd != RunEndReason.None)
                GUILayout.Label($"직전 런 종료: {s.LastEnd}");

            GUILayout.Space(6);
            DrawButtons(s);

            GUILayout.Space(6);
            GUILayout.Label("기록");
            foreach (var line in _log)
                GUILayout.Label(line);

            GUILayout.EndArea();
        }

        void DrawButtons(CampaignSnapshot s)
        {
            if (!s.IsRunning)
            {
                if (GUILayout.Button("오프라인 런 시작(혼자)")) _run.StartOfflineRun();
                GUILayout.Label("접속 중이면 호스트가 게임을 시작할 때 자동으로 시작합니다.");
                return;
            }

            if (s.Phase == CampaignPhase.Night && GUILayout.Button(_run.IsLocalReady ? "준비 취소" : "준비 완료"))
                _run.SetReady(!_run.IsLocalReady);

            if (!_run.HasAuthority)
            {
                GUILayout.Label("시간대 전환 신호는 호스트 화면에서 보냅니다.");
                return;
            }

            switch (s.Phase)
            {
                case CampaignPhase.Intro:
                    if (GUILayout.Button("도입 끝")) _run.EndIntro();
                    break;
                case CampaignPhase.Performance:
                    if (GUILayout.Button("공연 종료")) _run.EndPerformance();
                    break;
                case CampaignPhase.RestTime:
                    if (s.Timer == RestTimerState.WaitingForExit && GUILayout.Button("대기실에서 나옴(B안 타이머 시작)"))
                        _run.NotifyLeftWaitingRoom();
                    if (!s.IsFinalMorning && GUILayout.Button("전원 무대 입구 통로 진입")) _run.NotifyAllAtStageEntrance();
                    if (GUILayout.Button("남은 시간 65초로(경보 확인)")) _run.DebugSetRemaining(65f);
                    if (GUILayout.Button("타이머 끝")) _run.DebugSetRemaining(0f);
                    if (GUILayout.Button("탈출 개시")) _run.StartEscape();
                    break;
                case CampaignPhase.Escape:
                    if (GUILayout.Button("탈출 성공(한 명 이상 도착)")) _run.EndEscape(true);
                    if (GUILayout.Button("전원 리타이어")) _run.EndEscape(false);
                    break;
            }

            if (_run.IsOfflineRun && GUILayout.Button("오프라인 런 끝내기")) _run.StopOfflineRun();
        }

        static string TimerText(CampaignSnapshot s)
        {
            switch (s.Timer)
            {
                case RestTimerState.WaitingForExit: return $"대기 중(누가 대기실에서 나오면 시작) {s.TimeRemaining:0}초";
                case RestTimerState.Running: return $"{Mathf.FloorToInt(s.TimeRemaining / 60f)}:{Mathf.FloorToInt(s.TimeRemaining % 60f):00}";
                case RestTimerState.Stopped: return $"멈춤 {s.TimeRemaining:0}초";
                default: return "-";
            }
        }
    }
}
