using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope.Results;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>외줄 묘기 진행 상태(모든 화면에서 같음).</summary>
    public enum TightropeRunState : byte
    {
        /// <summary>게임 시작 전(세션 대기). 연습 중에도 전원 추락하면 재시작한다.</summary>
        Waiting,
        /// <summary>묘기 진행 중(제한시간이 흐른다).</summary>
        Running,
        /// <summary>실패(도착자 없이 진행자가 없어지거나 시간 종료) → 잠시 뒤 묘기 재시작.</summary>
        Failed,
        /// <summary>클리어(도착자 ≥1인 채로 진행자가 없어지거나 시간 종료) → 게임 종료.</summary>
        Succeeded
    }

    /// <summary>
    /// 외줄 묘기 진행과 공개 진입점. 규칙: Harness/Project/Decisions/tightrope-course-rules.md (2026-10-03).
    /// 판정은 호스트가 한다. 참가자 = 접속한 플레이어(<see cref="NetworkPlayer.All"/>, 나간 사람은 자동으로 빠짐).
    /// 플레이어 상태(007): 진행 중 = <see cref="PlayerState.Normal"/>, 도착 완료 = <see cref="PlayerState.Arrived"/>(도착선 통과 시 호스트가 정함, 이후 추락 거절),
    /// 사망 = <see cref="PlayerState.Fallen"/>.
    /// 게임이 시작되면 모두 출발점에서 다시 시작하고 제한시간이 흐른다. **한 명이라도 도착하면 바로 클리어**(<see cref="NetworkSessionManager.EndGame"/>)하고
    /// 살아 있는 플레이어는 모두 도착 완료가 되어 도착 지점으로 옮겨진다(2026-10-03 PM). 도착 없이 진행자가 없어지거나 시간이 끝나면
    /// 실패 → 세팅의 재시작 대기 시간 뒤 묘기 재시작(타이머 포함 처음부터). 도착은 소유자도 요청하고(내 위치 기준) 호스트도 위치로 확인한다.
    /// 묘기 시작 = 게임 시작 후 진행 중인 플레이어 중 **한 명이라도** 외줄 시작점을 넘은 순간(<see cref="PerformanceStarted"/>, 2026-10-03 PM — 한 명이 플랫폼에 남아 장애물을 늦추는 것을 막음). 장애물 출현 시간은 이때부터 잰다.
    /// 진행 상태·시간은 NGO 이름 붙은 메시지로 모든 화면에 공유한다(바뀔 때만 보내고 각 화면이 시간을 이어서 센다).
    /// 다른 기능은 <see cref="Current"/>의 <see cref="State"/>·<see cref="StateChanged"/>·<see cref="IsPerformanceStarted"/>·<see cref="PerformanceElapsed"/>·<see cref="RunRestarted"/>를 쓴다.
    /// 제한시간·재시작 대기 시간은 외줄 세팅(진행)에서 쓸 때마다 읽는다.
    /// 코스 프리팹의 <see cref="TightropeCourse"/>와 같은 오브젝트에 둔다.
    /// </summary>
    [RequireComponent(typeof(TightropeCourse))]
    public sealed class TightropeRun : MonoBehaviour
    {
        const string StateMessage = "CurtainCall.Tightrope.RunState";
        const string RequestMessage = "CurtainCall.Tightrope.RunStateRequest";

        static TightropeRun current;

        TightropeCourse course;
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        float runStartedAt = -1f;         // 진행 시작 시각(이 화면 기준). 진행 전이면 -1
        float performanceStartedAt = -1f; // 묘기 시작 시각(이 화면 기준). 시작 전이면 -1
        readonly System.Collections.Generic.HashSet<NetworkPlayer> seenAtStart = new(); // 호스트: 재시작 뒤 출발 공간(0m 앞)에서 확인된 플레이어
        float restartAt = -1f;            // 실패 후 재시작할 시각(이 화면 기준). 실패가 아니면 -1
        float stoppedRemaining = -1f;     // 끝난 뒤(실패·성공) 멈춘 남은 시간
        int lastRestartFrame = -1;

        /// <summary>씬의 묘기 진행. 없으면 null.</summary>
        public static TightropeRun Current
        {
            get
            {
                if (current == null) current = FindAnyObjectByType<TightropeRun>();
                return current;
            }
        }

        /// <summary>진행 상태(모든 화면에서 같음).</summary>
        public TightropeRunState State { get; private set; } = TightropeRunState.Waiting;

        /// <summary>제한시간(초).</summary>
        public float TimeLimit => GameSettings.Tightrope.Run.TimeLimit;

        /// <summary>남은 시간(초). 진행 전이면 제한시간 전체, 끝나면 멈춘 값.</summary>
        public float TimeRemaining
        {
            get
            {
                if (stoppedRemaining >= 0f && State != TightropeRunState.Running) return stoppedRemaining;
                if (runStartedAt < 0f) return TimeLimit;
                return Mathf.Max(0f, TimeLimit - (Time.time - runStartedAt));
            }
        }

        /// <summary>묘기가 시작됐는지(진행 중인 플레이어 중 한 명이라도 외줄에 오름). 재시작하면 다시 false.</summary>
        public bool IsPerformanceStarted => performanceStartedAt >= 0f;

        /// <summary>묘기 시작 후 지난 시간(초). 시작 전이면 0. 장애물 출현 시간 기준.</summary>
        public float PerformanceElapsed => IsPerformanceStarted ? Time.time - performanceStartedAt : 0f;

        /// <summary>실패 후 재시작까지 남은 시간(초). 실패가 아니면 0.</summary>
        public float RestartRemaining => restartAt < 0f ? 0f : Mathf.Max(0f, restartAt - Time.time);

        /// <summary>참가자 수(접속한 플레이어).</summary>
        public int ParticipantCount => NetworkPlayer.All.Count;

        /// <summary>진행 중(도착·사망 전) 인원.</summary>
        public int InProgressCount => Count(PlayerState.Normal);

        /// <summary>호스트: 묘기를 처음부터 다시 시작한다(디버그 R). 호스트가 아니면 false.</summary>
        public bool RestartByHost()
        {
            if (session == null || !session.IsHost || ParticipantCount == 0) return false;
            if (State == TightropeRunState.Succeeded)
                return RetryByHost(Results.AttemptId, Round);
            ServerRestart();
            return true;
        }

        /// <summary>최신 성공 결과에만 적용한다. 오래된 결과·중복 클릭·이탈 후 요청은 거절한다.</summary>
        public bool RetryByHost(string attemptId, int round)
        {
            var result = Results?.Latest;
            if (session == null || !session.IsHost || State != TightropeRunState.Succeeded
                || result == null || !result.Succeeded || result.AttemptId != attemptId
                || result.Round != round || Results.AttemptId != attemptId || Round != round) return false;
            if (!session.RestartEndedGame()) return false;
            ServerRestart();
            return true;
        }

        /// <summary>도착 완료 인원.</summary>
        public int ArrivedCount => Count(PlayerState.Arrived);

        /// <summary>사망 인원.</summary>
        public int DeadCount => Count(PlayerState.Fallen);

        /// <summary>진행 상태가 바뀌었을 때(모든 화면). 인자는 (이전, 새 상태).</summary>
        public event Action<TightropeRunState, TightropeRunState> StateChanged;

        /// <summary>묘기가 시작됐을 때(모든 화면, 진행마다 한 번). 수직 톱날·화재 등이 이때부터 시간을 잰다.</summary>
        public event Action PerformanceStarted;

        /// <summary>
        /// 묘기를 재시작했을 때(모든 화면, 한 번). 장애물 등 다른 요소는 이 신호로 처음 상태로 돌아간다.
        /// 플레이어 복구는 007(<see cref="NetworkPlayer.Restarted"/>)이 한다.
        /// </summary>
        public event Action RunRestarted;

        /// <summary>호스트가 정한 회차 번호. 접속 전은 0, 재시작마다 증가한다.</summary>
        public int Round { get; private set; }

        /// <summary>회차가 바뀔 때. 플레이어별 복구 메시지와 별개인 장애물 스냅샷 경계.</summary>
        public event Action RoundChanged;

        /// <summary>호스트: 도착 확인 후 도착자가 없을 때만 호출한다. 위험 판정 후 전원 사망을 다시 검사한다.</summary>
        public event Action ServerHazardCheck;

        /// <summary>각 화면의 성공 집결 직전. 기록은 이 시점의 위치를 수집한다.</summary>
        public event Action BeforeSuccessMovement;

        static int Count(PlayerState state)
        {
            int count = 0;
            foreach (var player in NetworkPlayer.All)
                if (player.State == state) count++;
            return count;
        }

        public TightropeResults Results { get; private set; }

        void Awake()
        {
            course = GetComponent<TightropeCourse>();
            Results = GetComponent<TightropeResults>();
            if (Results == null) Results = gameObject.AddComponent<TightropeResults>();
        }

        void OnEnable()
        {
            current = this;
            NetworkPlayer.Restarted += HandlePlayerRestarted;
        }

        void OnDisable()
        {
            if (current == this) current = null;
            NetworkPlayer.Restarted -= HandlePlayerRestarted;
            if (NetworkPlayer.ServerCanChangeState == CanChangeState) NetworkPlayer.ServerCanChangeState = null;
            Unregister();
            if (session != null)
            {
                session.ConnectionStateChanged -= HandleConnectionState;
                session.GameStateChanged -= HandleGameState;
                session = null;
            }
        }

        void Update()
        {
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += HandleConnectionState;
                session.GameStateChanged += HandleGameState;
                HandleConnectionState(session.ConnectionState);
            }

            if (session != null && session.IsHost)
            {
                Results.ObserveParticipants();
                ServerJudge();
            }
            RequestLocalArrival();
            UpdatePassThrough();
        }

        /// <summary>
        /// 모든 화면: 사망한 플레이어의 보이지 않는 몸통은 통과시킨다. 겉모습인 래그돌은 다른 플레이어 몸통에 부딪혀 밀린다(#16).
        /// 상태가 바뀌면 다시 막는다.
        /// </summary>
        void UpdatePassThrough()
        {
            foreach (var player in NetworkPlayer.All)
                if (player.TryGetComponent(out PlayerMover mover))
                    mover.PassThrough = player.State == PlayerState.Fallen;
        }

        // ── 호스트 판정 ─────────────────────────────────────

        void ServerJudge()
        {
            if (NetworkPlayer.All.Count == 0) return;

            switch (State)
            {
                case TightropeRunState.Waiting:
                    // 게임 시작 전 연습: 전원 사망하면 바로 다시 시작
                    if (InProgressCount == 0) ServerFail();
                    break;

                case TightropeRunState.Running:
                    ServerMarkArrivals();
                    if (ArrivedCount > 0) ServerSucceed(); // 도착이 이번 위험 판정보다 우선
                    else
                    {
                        if (InProgressCount > 0 && TimeRemaining > 0f)
                        {
                            if (!IsPerformanceStarted && AnyInProgressOnRope()) ServerStartPerformance();
                            ServerHazardCheck?.Invoke();
                        }
                        if (InProgressCount == 0 || TimeRemaining <= 0f) ServerFail();
                    }
                    break;

                case TightropeRunState.Failed:
                    if (Time.time >= restartAt) ServerRestart();
                    break;
            }
        }

        /// <summary>호스트: 진행 중인 플레이어가 도착선을 넘으면 도착 완료로 바꾼다(위치는 NetworkTransform으로 호스트에 온다).</summary>
        void ServerMarkArrivals()
        {
            foreach (var player in NetworkPlayer.All)
                if (player.State == PlayerState.Normal && course.HasReachedFinish(player.transform.position))
                    player.ServerSetState(PlayerState.Arrived);
        }

        /// <summary>
        /// 진행 중인 플레이어 중 한 명이라도 외줄 시작점을 넘었는지. 재시작 뒤 출발 공간에서 한 번 확인된 플레이어만 센다 —
        /// 재시작 직후 호스트에는 아직 남의 캐릭터의 재시작 전 위치(줄 위)가 남아 있어, 그대로 보면 묘기가 바로 시작된다.
        /// </summary>
        bool AnyInProgressOnRope()
        {
            bool any = false;
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State != PlayerState.Normal) continue;
                if (course.GetDistance(player.transform.position) < 0f) seenAtStart.Add(player);
                else if (seenAtStart.Contains(player)) any = true;
            }
            return any;
        }

        void ServerStartPerformance()
        {
            SetPerformanceStarted(0f);
            Broadcast();
        }

        /// <summary>
        /// 내 캐릭터가 도착선을 넘으면 호스트에 도착 완료를 요청한다. 호스트도 위치로 확인하지만,
        /// 위치 공유가 늦거나 순간이동한 경우를 놓치지 않으려고 소유자가 직접 알린다.
        /// </summary>
        void RequestLocalArrival()
        {
            var player = NetworkPlayer.Local;
            if (State != TightropeRunState.Running || player == null || player.State != PlayerState.Normal) return;
            if (!course.HasReachedFinish(player.transform.position)) return;
            if (Time.time < nextArrivalRequest) return;
            nextArrivalRequest = Time.time + 0.5f; // 답이 오기 전에 매 프레임 보내지 않게
            player.RequestState(PlayerState.Arrived);
        }

        float nextArrivalRequest;

        void ServerSucceed()
        {
            Results.Confirm(true, TightropeEndReason.Completed, Time.time - runStartedAt, TimeRemaining, PerformanceElapsed);
            // 살아 있는(진행 중) 플레이어도 모두 도착 완료. 위치는 각 소유자가 도착 지점으로 옮긴다(SetState → MoveLocalToFinish)
            foreach (var player in NetworkPlayer.All)
                if (player.State == PlayerState.Normal) player.ServerSetState(PlayerState.Arrived);
            stoppedRemaining = TimeRemaining;
            ServerSetState(TightropeRunState.Succeeded);
            session.EndGame();
        }

        void ServerFail()
        {
            if (State == TightropeRunState.Running)
                Results.Confirm(false, InProgressCount == 0 ? TightropeEndReason.AllFallen : TightropeEndReason.TimeExpired,
                    Time.time - runStartedAt, TimeRemaining, PerformanceElapsed);
            stoppedRemaining = State == TightropeRunState.Running ? TimeRemaining : -1f;
            restartAt = Time.time + GameSettings.Tightrope.Run.RestartDelay;
            ServerSetState(TightropeRunState.Failed);
        }

        /// <summary>호스트: 모두 출발점에서 다시 시작하고, 게임 상태에 맞는 진행 상태(대기/진행)로 돌아간다. 진행이면 타이머도 처음부터.</summary>
        void ServerRestart()
        {
            restartAt = -1f;
            stoppedRemaining = -1f;
            performanceStartedAt = -1f;
            seenAtStart.Clear();
            bool playing = session.GameState == GameSessionState.Playing;
            runStartedAt = playing ? Time.time : -1f;
            SetRound(Round + 1);
            NetworkPlayer.ServerRestartAll();
            if (playing) Results.BeginAttempt(Round);
            ServerSetState(playing ? TightropeRunState.Running : TightropeRunState.Waiting);
        }

        /// <summary>
        /// 호스트 규칙: 사망은 진행 중인 플레이어만, 묘기가 진행(또는 연습) 중일 때만 받는다. 도착 완료자는 사망하지 않는다.
        /// 도착 완료 요청은 진행 중인 플레이어가 묘기 진행 중일 때만 받는다.
        /// </summary>
        bool CanChangeState(NetworkPlayer player, PlayerState next) => next switch
        {
            PlayerState.Fallen => player.State == PlayerState.Normal && (State == TightropeRunState.Waiting || State == TightropeRunState.Running),
            PlayerState.Arrived => player.State == PlayerState.Normal && State == TightropeRunState.Running,
            _ => true,
        };

        void ServerSetState(TightropeRunState next)
        {
            SetState(next);
            Broadcast();
        }

        void HandleGameState(GameSessionState state)
        {
            if (session == null || !session.IsHost) return;
            if (state == GameSessionState.Playing && State == TightropeRunState.Waiting)
                ServerRestart(); // 게임 시작: 모두 출발점에서 진행 시작
        }

        // ── 모든 화면 ───────────────────────────────────────

        void SetState(TightropeRunState next)
        {
            if (State == next) return;
            var previous = State;
            State = next;
            Debug.Log($"[Tightrope] 묘기 진행: {previous} → {next} (도착 {ArrivedCount}, 사망 {DeadCount}, 남은 시간 {TimeRemaining:0}s)");
            if (next == TightropeRunState.Succeeded) MoveLocalToFinish();
            StateChanged?.Invoke(previous, next);
        }

        /// <summary>
        /// 클리어: 내 캐릭터가 살아 있고 아직 도착선 전이면 도착 지점(세리머니 공간, 자기 줄 자리)으로 순간이동한다.
        /// 위치는 소유자가 정하므로 각 화면이 자기 캐릭터만 옮긴다(다른 화면에는 NetworkTransform 순간이동으로 전해진다).
        /// </summary>
        void MoveLocalToFinish()
        {
            BeforeSuccessMovement?.Invoke();
            PiggybackSystem.Current?.ReleaseForSuccess();
            var player = NetworkPlayer.Local;
            if (player == null || player.State == PlayerState.Fallen || !player.TryGetComponent(out PlayerMover mover)) return;
            if (player.TryGetComponent(out PlayerBalance balance) && balance.HasFallen) return;

            // 출발 위치를 잠깐 도착 지점으로 바꿔 순간이동하고 원래 출발 위치로 되돌린다
            Pose finish = course.GetFinishPose(player.Slot);
            Pose start = course.GetStartPose(player.Slot);
            mover.SetStartPose(finish.position, finish.rotation, true);
            mover.SetStartPose(start.position, start.rotation, false);
        }

        void SetPerformanceStarted(float elapsed)
        {
            bool wasStarted = IsPerformanceStarted;
            performanceStartedAt = Time.time - elapsed;
            if (wasStarted) return;
            Debug.Log("[Tightrope] 묘기 시작(첫 플레이어가 외줄에 오름)");
            PerformanceStarted?.Invoke();
        }

        /// <summary>007 모두 재시작은 플레이어마다 오므로 한 프레임에 한 번만 알린다.</summary>
        void HandlePlayerRestarted(NetworkPlayer player)
        {
            if (lastRestartFrame == Time.frameCount) return;
            lastRestartFrame = Time.frameCount;
            for (int lane = 0; lane < course.LaneCount; lane++) course.SetLaneBlocked(lane, false);
            course.ClearBlockedSegments();
            RunRestarted?.Invoke();
        }

        // ── 공유 ───────────────────────────────────────────

        void HandleConnectionState(SessionConnectionState state)
        {
            switch (state)
            {
                case SessionConnectionState.Host:
                    Register(RequestMessage, HandleRequest);
                    NetworkPlayer.ServerCanChangeState = CanChangeState;
                    ResetLocal();
                    break;
                case SessionConnectionState.Client:
                    Register(StateMessage, HandleStateMessage);
                    SendRequest();
                    break;
                case SessionConnectionState.Offline:
                    Unregister();
                    if (NetworkPlayer.ServerCanChangeState == CanChangeState) NetworkPlayer.ServerCanChangeState = null;
                    ResetLocal();
                    break;
            }
        }

        void SetRound(int round)
        {
            if (Round == round) return;
            Round = round;
            RoundChanged?.Invoke();
        }

        void ResetLocal()
        {
            SetRound(0);
            seenAtStart.Clear();
            restartAt = -1f;
            runStartedAt = -1f;
            performanceStartedAt = -1f;
            stoppedRemaining = -1f;
            SetState(TightropeRunState.Waiting);
        }

        void Broadcast()
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = WriteState();
            manager.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleRequest(ulong senderId, FastBufferReader reader)
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = WriteState();
            manager.SendNamedMessage(StateMessage, senderId, writer, NetworkDelivery.ReliableSequenced);
        }

        void SendRequest()
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            manager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer);
        }

        /// <summary>상태, 진행 경과(진행 전 -1), 멈춘 남은 시간(-1 = 흐르는 중), 묘기 경과(시작 전 -1), 재시작까지 남은 시간.</summary>
        FastBufferWriter WriteState()
        {
            var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe((byte)State);
            writer.WriteValueSafe(Round);
            writer.WriteValueSafe(runStartedAt < 0f ? -1f : Time.time - runStartedAt);
            writer.WriteValueSafe(State == TightropeRunState.Running ? -1f : stoppedRemaining);
            writer.WriteValueSafe(IsPerformanceStarted ? PerformanceElapsed : -1f);
            writer.WriteValueSafe(RestartRemaining);
            return writer;
        }

        void HandleStateMessage(ulong senderId, FastBufferReader reader)
        {
            if (senderId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out int round);
            if (round < Round) return;
            reader.ReadValueSafe(out float runElapsed);
            reader.ReadValueSafe(out float stopped);
            reader.ReadValueSafe(out float performanceElapsed);
            reader.ReadValueSafe(out float restartRemaining);

            runStartedAt = runElapsed < 0f ? -1f : Time.time - runElapsed;
            stoppedRemaining = stopped;
            restartAt = (TightropeRunState)state == TightropeRunState.Failed ? Time.time + restartRemaining : -1f;
            if (round != Round) performanceStartedAt = -1f;
            SetRound(round);
            if (performanceElapsed >= 0f) SetPerformanceStarted(performanceElapsed);
            else performanceStartedAt = -1f;
            SetState((TightropeRunState)state);
        }

        CustomMessagingManager Messaging =>
            session != null && session.NetworkManager != null ? session.NetworkManager.CustomMessagingManager : null;

        void Register(string message, CustomMessagingManager.HandleNamedMessageDelegate handler)
        {
            Unregister();
            messaging = Messaging;
            messaging?.RegisterNamedMessageHandler(message, handler);
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging = null;
        }
    }
}
