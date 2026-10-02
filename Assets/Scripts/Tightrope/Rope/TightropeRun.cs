using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
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
        /// <summary>묘기 진행 중.</summary>
        Running,
        /// <summary>전원 추락 → 잠시 뒤 모두 재시작.</summary>
        Failed,
        /// <summary>한 명 이상 도착 → 성공(세션 종료).</summary>
        Succeeded
    }

    /// <summary>
    /// 외줄 묘기 진행과 공개 진입점. 판정은 호스트가 한다:
    /// 참가자 = 접속한 모든 플레이어(<see cref="NetworkPlayer.All"/>), 추락 = <see cref="PlayerState.Fallen"/>(007),
    /// 도착 = 보통 상태 플레이어가 코스 도착 거리에 닿음(<see cref="TightropeCourse.HasReachedFinish"/>, 위치는 NetworkTransform으로 호스트에 온다).
    /// 전원 추락 → 실패 → <see cref="restartDelay"/>초 뒤 모두 재시작(<see cref="NetworkPlayer.ServerRestartAll"/>), 한 명 도착 → 성공 → <see cref="NetworkSessionManager.EndGame"/>.
    /// 게임이 시작되면 모두 출발점에서 다시 시작한다. 진행 상태는 NGO 이름 붙은 메시지로 모든 화면에 공유한다.
    /// 다른 기능은 <see cref="Current"/>의 <see cref="State"/>·<see cref="StateChanged"/>·<see cref="RunRestarted"/>를 쓴다.
    /// 코스 프리팹의 <see cref="TightropeCourse"/>와 같은 오브젝트에 둔다.
    /// </summary>
    [RequireComponent(typeof(TightropeCourse))]
    public sealed class TightropeRun : MonoBehaviour
    {
        /// <summary>도착한 사람이 없음.</summary>
        public const int NoWinner = -1;

        const string StateMessage = "CurtainCall.Tightrope.RunState";
        const string RequestMessage = "CurtainCall.Tightrope.RunStateRequest";

        [Tooltip("전원 추락 후 모두 재시작까지 기다리는 시간(초). 기획에 값이 없어 테스트 값(007 TestRoundRestart와 같음).")]
        [SerializeField, Min(0f)] float restartDelay = 3f;

        static TightropeRun current;

        TightropeCourse course;
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        float restartAt = -1f; // 실패 후 재시작할 시각(이 화면 기준). 실패가 아니면 -1

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

        /// <summary>처음 도착한 플레이어의 자리 번호. 성공 전에는 <see cref="NoWinner"/>.</summary>
        public int WinnerSlot { get; private set; } = NoWinner;

        /// <summary>실패 후 재시작까지 남은 시간(초). 실패가 아니면 0.</summary>
        public float RestartRemaining => restartAt < 0f ? 0f : Mathf.Max(0f, restartAt - Time.time);

        /// <summary>참가자 수(접속한 플레이어).</summary>
        public int ParticipantCount => NetworkPlayer.All.Count;

        /// <summary>추락하지 않은 참가자 수.</summary>
        public int RemainingCount
        {
            get
            {
                int count = 0;
                foreach (var player in NetworkPlayer.All)
                    if (player.State != PlayerState.Fallen) count++;
                return count;
            }
        }

        /// <summary>진행 상태가 바뀌었을 때(모든 화면). 인자는 (이전, 새 상태).</summary>
        public event Action<TightropeRunState, TightropeRunState> StateChanged;

        /// <summary>
        /// 모두 재시작했을 때(모든 화면, 한 번). 장애물 등 다른 요소는 이 신호로 처음 상태로 돌아간다.
        /// 플레이어 복구는 007(<see cref="NetworkPlayer.Restarted"/>)이 한다.
        /// </summary>
        public event Action RunRestarted;

        void Awake() => course = GetComponent<TightropeCourse>();

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

            if (session != null && session.IsHost) ServerJudge();
            UpdatePassThrough();
        }

        /// <summary>
        /// 모든 화면: 추락한 플레이어의 보이지 않는 몸통은 통과시킨다. 겉모습인 래그돌은 다른 플레이어 몸통에 부딪혀 밀린다(#16).
        /// 상태가 보통으로 돌아오면 다시 막는다.
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
            var players = NetworkPlayer.All;
            if (players.Count == 0) return;

            switch (State)
            {
                case TightropeRunState.Waiting:
                case TightropeRunState.Running:
                    if (State == TightropeRunState.Running && TryFindArrived(out int winner))
                    {
                        WinnerSlot = winner;
                        ServerSetState(TightropeRunState.Succeeded);
                        session.EndGame();
                    }
                    else if (RemainingCount == 0)
                    {
                        restartAt = Time.time + restartDelay;
                        ServerSetState(TightropeRunState.Failed);
                    }
                    break;

                case TightropeRunState.Failed:
                    if (Time.time >= restartAt) ServerRestart();
                    break;
            }
        }

        bool TryFindArrived(out int slot)
        {
            foreach (var player in NetworkPlayer.All)
            {
                if (player.State == PlayerState.Normal && course.HasReachedFinish(player.transform.position))
                {
                    slot = player.Slot;
                    return true;
                }
            }
            slot = NoWinner;
            return false;
        }

        /// <summary>호스트: 모두 출발점에서 다시 시작하고, 게임 상태에 맞는 진행 상태(대기/진행)로 돌아간다.</summary>
        void ServerRestart()
        {
            restartAt = -1f;
            WinnerSlot = NoWinner;
            NetworkPlayer.ServerRestartAll();
            ServerSetState(session.GameState == GameSessionState.Playing ? TightropeRunState.Running : TightropeRunState.Waiting);
        }

        /// <summary>호스트 규칙: 묘기가 끝났거나 실패 처리 중이면 새 추락을 받지 않는다.</summary>
        bool CanChangeState(NetworkPlayer player, PlayerState next) =>
            next != PlayerState.Fallen || State == TightropeRunState.Waiting || State == TightropeRunState.Running;

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
            Debug.Log($"[Tightrope] 묘기 진행: {previous} → {next}" + (next == TightropeRunState.Succeeded ? $" (자리 {WinnerSlot} 도착)" : ""));
            StateChanged?.Invoke(previous, next);
        }

        /// <summary>007 모두 재시작은 플레이어마다 오므로 한 프레임에 한 번만 알린다.</summary>
        int lastRestartFrame = -1;

        void HandlePlayerRestarted(NetworkPlayer player)
        {
            if (lastRestartFrame == Time.frameCount) return;
            lastRestartFrame = Time.frameCount;
            for (int lane = 0; lane < course.LaneCount; lane++) course.SetLaneBlocked(lane, false);
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

        void ResetLocal()
        {
            restartAt = -1f;
            WinnerSlot = NoWinner;
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

        FastBufferWriter WriteState()
        {
            var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe((byte)State);
            writer.WriteValueSafe(WinnerSlot);
            writer.WriteValueSafe(RestartRemaining);
            return writer;
        }

        void HandleStateMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out int winner);
            reader.ReadValueSafe(out float remaining);
            WinnerSlot = winner;
            restartAt = (TightropeRunState)state == TightropeRunState.Failed ? Time.time + remaining : -1f;
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
