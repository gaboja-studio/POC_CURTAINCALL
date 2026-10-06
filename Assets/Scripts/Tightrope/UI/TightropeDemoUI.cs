using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Telemetry;
using CurtainCall.Tightrope.Fire;
using CurtainCall.Tightrope.Results;
using UnityEngine;

namespace CurtainCall.Tightrope.UI
{
    /// <summary>공개 상태를 UI에 표시한다. 재도전 실행과 전체 입력 gate는 Prototype 연결부가 맡는다.</summary>
    [RequireComponent(typeof(TightropeDemoView), typeof(TightropeUIFocus))]
    public sealed class TightropeDemoUI : MonoBehaviour
    {
        readonly List<TightropeResult> history = new();
        TightropeDemoView view;
        TightropeUIFocus focus;
        NetworkSessionManager session;
        TightropeResults results;
        TightropeRun run;
        GameplayRecorder recorder;
        TightropeResult displayed;
        string seenAttempt;
        string error = "";
        bool busy, leaving, offlineSeen = true, playingSeen;
        int operation;
        float nextRefresh;
        public event Action<string, int> RetryRequested;

        void Awake()
        {
            view = GetComponent<TightropeDemoView>();
            focus = GetComponent<TightropeUIFocus>();
            view.Build();
            focus.SetFields(view.JoinInput, view.LanInput);
            view.HostServices.onClick.AddListener(() => Connect(true, false));
            view.JoinServices.onClick.AddListener(() => Connect(false, false));
            view.HostLan.onClick.AddListener(() => Connect(true, true));
            view.JoinLan.onClick.AddListener(() => Connect(false, true));
            view.Start.onClick.AddListener(StartGame);
            view.Copy.onClick.AddListener(CopyCode);
            view.Leave.onClick.AddListener(Leave);
            view.OpenRoom.onClick.AddListener(() => { view.ShowResult(false); view.ShowLobby(true); UpdateFocus(); });
            view.CloseRoom.onClick.AddListener(() => { view.ShowLobby(false); UpdateFocus(); });
            view.History.onClick.AddListener(ShowHistory);
            view.Retry.onClick.AddListener(RequestRetry);
            view.CloseResult.onClick.AddListener(() => { view.ShowResult(false); UpdateFocus(); });
            view.PreviousResult.onClick.AddListener(() => StepHistory(-1));
            view.NextResult.onClick.AddListener(() => StepHistory(1));
            UpdateFocus();
        }

        void OnEnable()
        {
            session = NetworkSessionManager.EnsureExists();
            nextRefresh = 0f;
        }

        void OnDisable()
        {
            operation++;
            busy = leaving = false;
            UnbindResults();
            if (focus != null) focus.SetModal(false);
        }

        void Update()
        {
            if (session == null) session = NetworkSessionManager.Instance;
            run = TightropeRun.Current;
            var source = run != null ? run.Results : null;
            if (source != results)
            {
                UnbindResults();
                results = source;
                if (results != null)
                {
                    seenAttempt = results.AttemptId;
                    results.ResultConfirmed += ReceiveResult;
                    results.Cleared += ClearResults;
                    if (results.Latest != null) ReceiveResult(results.Latest);
                }
            }
            if (results != null && results.AttemptId != seenAttempt)
            {
                seenAttempt = results.AttemptId;
                displayed = null;
                view.ShowResult(false);
                if (results.Latest != null && results.Latest.AttemptId == seenAttempt)
                    ReceiveResult(results.Latest);
            }
            bool offline = session == null || session.ConnectionState == SessionConnectionState.Offline;
            if (offline && !offlineSeen)
            {
                leaving = false;
                ClearResults();
                view.ShowLobby(true);
                if (!string.IsNullOrEmpty(session?.LastDisconnectReason))
                    error = "접속이 종료되었습니다. 방 코드와 네트워크 상태를 확인해 주세요.";
            }
            offlineSeen = offline;
            bool playing = !offline && session.GameState == GameSessionState.Playing;
            if (playing && !playingSeen) view.ShowLobby(false);
            playingSeen = playing;
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.1f;
                Refresh();
            }
            UpdateFocus();
        }

        void UnbindResults()
        {
            if (results != null)
            {
                results.ResultConfirmed -= ReceiveResult;
                results.Cleared -= ClearResults;
            }
            results = null;
        }

        void ClearResults()
        {
            history.Clear();
            displayed = null;
            seenAttempt = null;
            view.ShowResult(false);
        }

        void ReceiveResult(TightropeResult value)
        {
            if (value == null || history.Exists(r => r.AttemptId == value.AttemptId)) return;
            history.Add(value);
            if (history.Count > 10) history.RemoveAt(0);
            if (results != null && value.AttemptId != results.AttemptId) return;
            seenAttempt = results != null ? results.AttemptId : value.AttemptId;
            displayed = value;
            view.ShowLobby(false);
            view.ShowResult(true);
            UpdateFocus();
            RefreshResult();
        }

        void ShowHistory()
        {
            if (history.Count == 0) return;
            displayed = history[history.Count - 1];
            view.ShowLobby(false);
            view.ShowResult(true);
            UpdateFocus();
            RefreshResult();
        }

        void StepHistory(int step)
        {
            int index = history.IndexOf(displayed) + step;
            if (index < 0 || index >= history.Count) return;
            displayed = history[index];
            RefreshResult();
        }

        async void Connect(bool host, bool lan)
        {
            if (busy || leaving || session == null || session.ConnectionState != SessionConnectionState.Offline) return;
            string key = (lan ? view.LanInput.text : view.JoinInput.text).Trim();
            if (!host && key.Length == 0) { error = "참가할 방 코드 또는 LAN 주소를 입력해 주세요."; Refresh(); return; }
            busy = true;
            error = "";
            int ticket = ++operation;
            var target = session;
            Refresh();
            try
            {
                Task<bool> request = lan
                    ? (host ? target.HostLanAsync() : target.JoinLanAsync(key))
                    : (host ? target.HostSessionAsync() : target.JoinSessionAsync(key));
                bool accepted = await request;
                if (this == null || !isActiveAndEnabled || ticket != operation || target != session) return;
                if (!accepted) error = "접속 요청에 실패했습니다. 방 코드와 네트워크 상태를 확인해 주세요.";
                // 참가 요청 성공만으로 Client 화면으로 전환하지 않는다.
            }
            catch (Exception)
            {
                if (this != null && isActiveAndEnabled && ticket == operation)
                    error = "접속 요청을 처리하지 못했습니다. 다시 시도해 주세요.";
            }
            finally
            {
                if (this != null && isActiveAndEnabled && ticket == operation)
                { busy = false; Refresh(); }
            }
        }

        async void Leave()
        {
            if (busy || leaving || session == null || session.ConnectionState == SessionConnectionState.Offline) return;
            leaving = true;
            error = "";
            int ticket = ++operation;
            var target = session;
            Refresh();
            try { await target.LeaveAsync(); }
            catch (Exception)
            {
                if (this != null && isActiveAndEnabled && ticket == operation)
                { leaving = false; error = "나가기 요청에 실패했습니다. 다시 시도해 주세요."; }
            }
            if (this != null && isActiveAndEnabled && ticket == operation)
            {
                // Offline은 접속 이벤트 경계에서만 확정된다.
                if (target.ConnectionState == SessionConnectionState.Offline) leaving = false;
                Refresh();
            }
        }

        void StartGame()
        {
            if (busy || leaving || session == null || !session.CanStartGame) return;
            if (session.StartGame()) { view.ShowLobby(false); UpdateFocus(); }
        }

        void CopyCode()
        {
            if (session == null || string.IsNullOrEmpty(session.JoinKey)) return;
            GUIUtility.systemCopyBuffer = session.JoinKey;
        }

        void RequestRetry()
        {
            if (!CanRetry()) return;
            RetryRequested?.Invoke(displayed.AttemptId, displayed.Round);
        }

        bool CanRetry() => !busy && !leaving && session != null && session.IsHost
            && run != null && run.State == TightropeRunState.Succeeded && displayed != null
            && displayed.Succeeded && results != null && results.Latest == displayed
            && results.Round == displayed.Round && results.AttemptId == displayed.AttemptId
            && RetryRequested != null;

        void UpdateFocus() => focus.SetModal(view.LobbyVisible || view.ResultVisible);

        void Refresh()
        {
            bool offline = session == null || session.ConnectionState == SessionConnectionState.Offline;
            bool connected = session != null && (session.ConnectionState == SessionConnectionState.Host
                || session.ConnectionState == SessionConnectionState.Client);
            bool editable = offline && !busy && !leaving && session != null;
            view.HostServices.interactable = view.JoinServices.interactable = editable;
            view.HostLan.interactable = view.JoinLan.interactable = editable;
            view.JoinInput.interactable = view.LanInput.interactable = editable;
            view.Leave.interactable = !offline && !busy && !leaving;
            view.Copy.interactable = connected && !string.IsNullOrEmpty(session.JoinKey);
            view.Start.interactable = !busy && !leaving && session != null && session.CanStartGame;
            view.CloseRoom.interactable = connected && !busy && !leaving;
            view.History.interactable = history.Count > 0;
            view.Connection.text = session == null ? "접속 관리자를 찾지 못했습니다."
                : leaving ? "나가기 처리 중…" : busy ? "접속 요청 처리 중…"
                : session.ConnectionState switch
                {
                    SessionConnectionState.Connecting => "실제 연결을 기다리는 중…",
                    SessionConnectionState.Host => "호스트 · 접속 완료",
                    SessionConnectionState.Client => "참가자 · 접속 완료",
                    _ => "접속하지 않음"
                };
            view.Room.text = connected
                ? $"방 코드 / 주소: {session.JoinKey}\n인원 {session.PlayerCount}/{session.RequiredPlayers} · 시작 최소 {session.MinPlayers}명"
                : "Services 방 코드로 참가하거나 같은 네트워크에서 LAN을 사용하세요.";
            view.Error.text = error;
            view.ShowHud(connected);
            RefreshHud();
            RefreshStorage();
            if (view.ResultVisible) RefreshResult();
            UpdateFocus();
        }

        void RefreshHud()
        {
            var player = NetworkPlayer.Local;
            var course = TightropeCourse.Current;
            string distance = player != null && course != null
                ? $"{course.GetDistance(player.transform.position):F1} / {course.FinishDistance:F1} m" : "대기";
            string state = player == null ? "생성 대기" : player.State switch
            {
                PlayerState.Arrived => "도착", PlayerState.Fallen => "추락", _ => "진행"
            };
            view.HudText.text = run == null ? "코스 연결 대기"
                : $"{RunLabel(run.State)} · 남은 시간 {run.TimeRemaining:F1}초\n"
                + $"묘기 {(run.IsPerformanceStarted ? $"{run.PerformanceElapsed:F1}초" : "시작 대기")} · 거리 {distance}\n"
                + $"진행 {run.InProgressCount} / 도착 {run.ArrivedCount} / 사망 {run.DeadCount}\n"
                + $"내 상태 {state} · {(player != null ? Limbs(player.LostParts) : "사지 정보 대기")}";
            if (view.Gauge != null) view.Gauge.Target = player != null ? player.GetComponent<PlayerBalance>() : null;
            var fire = TightropeFire.Current;
            if (fire == null || fire.Settings == null || run == null || fire.Round != run.Round)
            { view.FireText.text = "화재 정보 대기"; return; }
            var text = new StringBuilder();
            text.Append(fire.IsChasing ? $"추격 불 {fire.ChaseDistance:F1} m" : "추격 불 대기");
            var segments = fire.Settings.segments;
            if (segments != null)
                for (int i = 0; i < segments.Length; i++)
                {
                    float remaining = fire.WarningRemaining(i);
                    if (remaining < 0f && !fire.IsSegmentActive(i)) continue;
                    var segment = segments[i];
                    text.Append($"\n{segment.lane}번 줄 {segment.from:F0}~{segment.to:F0} m · ");
                    text.Append(fire.IsSegmentActive(i) ? "발화 중" : $"{remaining:F1}초 뒤 발화");
                }
            view.FireText.text = text.ToString();
        }

        void RefreshStorage()
        {
            if (recorder == null) recorder = FindAnyObjectByType<GameplayRecorder>();
            if (recorder == null) { view.Storage.text = "기록 수집기 연결 대기"; return; }
            var record = recorder.Current;
            var capture = record?.captureStatus;
            view.StorageDetails.text = capture == null ? "실제 시도가 시작되면 개인 행동을 로컬 JSON에 기록합니다."
                : $"기록 누락: 이벤트 {capture.droppedEvents}, 샘플 {capture.droppedSamples} · 마지막 저장 순서 {capture.lastSavedSequence}\n"
                + $"미지원: {string.Join(", ", capture.unsupported)}";
            if (!string.IsNullOrEmpty(recorder.StorageError))
                view.StorageDetails.text += "\n저장 오류가 발생했습니다. 재시도 대상은 메모리에 보존하며, 검증 실패·한도 손실은 자동 복구하지 않습니다.";
            string identity = recorder.IdentityPersistence ? "익명 ID 저장됨" : "실행 한정 ID";
            if (!string.IsNullOrEmpty(recorder.StorageError))
            { view.Storage.text = $"기록 저장 오류 · 재시도 한도 손실 {recorder.UnsavedAttemptsDiscarded} · {identity}"; return; }
            if (record == null) { view.Storage.text = $"실제 시도 기록 대기 · {identity}"; return; }
            var status = record.captureStatus;
            string saved = status.lastSavedSequence == record.sequence ? "저장됨" : "저장 대기";
            view.Storage.text = $"기록 {(status.recording ? "수집 중" : "마감")} · {saved} · "
                + $"누락 {status.droppedEvents + status.droppedSamples} · 미지원 {status.unsupported.Count}"
                + (status.incomplete ? " · 불완전" : "") + $" · {identity}";
        }

        void RefreshResult()
        {
            if (displayed == null) return;
            view.ResultTitle.text = $"{displayed.Round}회차 · {(displayed.Succeeded ? "묘기 성공" : "묘기 실패")}";
            var text = new StringBuilder();
            text.Append($"호스트 확정 · {ReasonLabel(displayed.Reason)}\n");
            text.Append($"소요 {displayed.Elapsed:F1}초 · 남은 시간 {displayed.Remaining:F1}초\n묘기 {displayed.PerformanceElapsed:F1}초\n\n");
            foreach (var person in displayed.Participants)
            {
                string slot = person.Slot >= 0 ? $"자리 {person.Slot + 1}" : "자리 미정";
                text.Append($"{slot} · {OutcomeLabel(person.Outcome)}{(person.Connected ? "" : " · 연결 종료")}\n");
                text.Append($"시작: {Limbs(person.StartingLostParts)}\n종료: {Limbs(person.LostParts)}\n\n");
            }
            view.ResultBody.text = text.ToString();
            bool current = results != null && results.Latest == displayed && results.Round == displayed.Round;
            view.RetryText.text = current && run != null && run.State == TightropeRunState.Failed
                ? $"{Mathf.Max(0f, run.RestartRemaining):F1}초 뒤 자동 재시도"
                : current && displayed.Succeeded
                    ? session != null && session.IsHost
                        ? RetryRequested == null ? "재도전 연결 대기" : "호스트가 재도전을 시작할 수 있습니다."
                        : "호스트의 재도전을 기다립니다."
                    : "지난 확정 결과입니다. 현재 진행 시간과는 별개입니다.";
            view.Retry.interactable = CanRetry();
            view.PreviousResult.interactable = history.IndexOf(displayed) > 0;
            view.NextResult.interactable = history.IndexOf(displayed) < history.Count - 1;
        }

        static string RunLabel(TightropeRunState state) => state switch
        {
            TightropeRunState.Running => "진행 중", TightropeRunState.Failed => "실패",
            TightropeRunState.Succeeded => "성공", _ => "시작 대기"
        };
        static string ReasonLabel(TightropeEndReason reason) => reason switch
        {
            TightropeEndReason.Completed => "도착 완료", TightropeEndReason.AllFallen => "전원 추락", _ => "시간 종료"
        };
        static string OutcomeLabel(ParticipantOutcome outcome) => outcome switch
        {
            ParticipantOutcome.DirectFinish => "직접 완주", ParticipantOutcome.CompanionSuccess => "동반 성공",
            ParticipantOutcome.Fallen => "사망", ParticipantOutcome.Disconnected => "이탈", _ => "미완주"
        };
        static string Limbs(BodyPart lost) => $"왼팔 {Part(lost, BodyPart.LeftArm)} / 오른팔 {Part(lost, BodyPart.RightArm)} / "
            + $"왼다리 {Part(lost, BodyPart.LeftLeg)} / 오른다리 {Part(lost, BodyPart.RightLeg)}";
        static string Part(BodyPart lost, BodyPart part) => (lost & part) != 0 ? "손실" : "정상";
    }
}
