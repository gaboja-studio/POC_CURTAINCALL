using System;
using System.Collections.Generic;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Campaign
{
    /// <summary>
    /// 런 진행(세대·일차·시간대·쉬는 시간 타이머·밤 준비 완료)의 공개 진입점. 다른 기능은 이 클래스의 메서드·이벤트만 쓴다.
    /// 호스트가 <see cref="RunState"/>로 판정하고 모두에게 상태를 보낸다(이름 붙은 메시지, 별도 네트워크 프리팹 없음).
    /// 방이 진행(<see cref="GameSessionState.Playing"/>)이 되면 세대 1·1일차로 시작하고, 엔딩이면 방을 종료로 바꾼다.
    /// 공연 종료·대기실 나옴·무대 입구 집결·탈출 신호는 각 공간(지금은 테스트 씬 디버그 버튼)이 호스트에서 부른다.
    /// 접속하지 않은 상태에서는 <see cref="StartOfflineRun"/>으로 혼자 흐름을 확인할 수 있다.
    /// </summary>
    public sealed class CampaignRun : MonoBehaviour
    {
        const string StateMessage = "CurtainCall.Campaign.State";
        const string RequestMessage = "CurtainCall.Campaign.Request";
        const string ReadyMessage = "CurtainCall.Campaign.Ready";
        const float TimerBroadcastInterval = 1f;

        static readonly ulong[] OfflinePlayers = { NetworkManager.ServerClientId };

        public static CampaignRun Instance { get; private set; }

        /// <summary>없으면 만들어 돌려준다(씬이 바뀌어도 유지).</summary>
        public static CampaignRun EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(CampaignRun));
            DontDestroyOnLoad(go);
            return go.AddComponent<CampaignRun>();
        }

        NetworkSessionManager session;
        CustomMessagingManager messaging;
        RunState host;
        CampaignSnapshot state;
        bool offlineRun;
        bool localReady;
        float nextBroadcastAt;

        /// <summary>지금 런 상태(모두 같게 본다). 런 전이면 <see cref="CampaignPhase.None"/>.</summary>
        public CampaignSnapshot State => state;

        /// <summary>이 화면이 판정하는지(호스트 또는 오프라인 런).</summary>
        public bool HasAuthority => host != null;

        /// <summary>접속 없이 혼자 돌리는 런인지.</summary>
        public bool IsOfflineRun => offlineRun;

        /// <summary>밤 정비에서 내가 준비 완료를 눌렀는지.</summary>
        public bool IsLocalReady => localReady;

        /// <summary>상태가 바뀌었다(타이머 남은 시간 포함, 매 프레임 올 수 있다).</summary>
        public event Action Changed;

        /// <summary>시간대가 바뀌었다(세대·일차·시간대·쉬는 시간 종류·공연 순번). 시간대 시작 알림으로 쓴다.</summary>
        public event Action<CampaignSnapshot> StepChanged;

        /// <summary>쉬는 시간 소집 경보(종료 1분 전, 공통 세팅).</summary>
        public event Action RecallAlarm;

        /// <summary>런이 끝났다. 실패(기한 초과·전원 리타이어)면 이어서 다음 세대 도입이 시작된다.</summary>
        public event Action<RunEndReason> RunEnded;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            session = NetworkSessionManager.EnsureExists();
            if (session == null) return;
            session.ConnectionStateChanged += HandleConnection;
            session.GameStateChanged += HandleGameState;
            HandleConnection(session.ConnectionState);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Unregister();
            if (session != null)
            {
                session.ConnectionStateChanged -= HandleConnection;
                session.GameStateChanged -= HandleGameState;
            }
            Instance = null;
        }

        void Update()
        {
            if (host != null)
            {
                if (!state.IsRunning) return;
                bool changed = state.Phase == CampaignPhase.Night && host.RefreshNight(Players());
                changed |= host.Tick(Time.deltaTime);
                if (changed) Apply(host.Snapshot);
            }
            else if (state.Timer == RestTimerState.Running && state.TimeRemaining > 0f)
            {
                // 받는 쪽은 다음 상태가 올 때까지 화면 표시만 줄인다(판정은 호스트).
                state.TimeRemaining = Mathf.Max(0f, state.TimeRemaining - Time.deltaTime);
                Changed?.Invoke();
            }
        }

        // ── 런 시작·종료 ──────────────────────────────────────

        /// <summary>접속하지 않은 상태에서 혼자 런을 시작한다(테스트용). 접속 중이면 false.</summary>
        public bool StartOfflineRun()
        {
            if (session != null && session.ConnectionState != SessionConnectionState.Offline)
            {
                Debug.LogWarning("[Campaign] 접속 중에는 오프라인 런을 시작할 수 없습니다. 방이 진행되면 자동으로 시작합니다.");
                return false;
            }
            offlineRun = true;
            host = new RunState();
            host.StartSession();
            Apply(host.Snapshot);
            return true;
        }

        /// <summary>오프라인 런을 끝내고 런 전 상태로 돌린다.</summary>
        public void StopOfflineRun()
        {
            if (!offlineRun) return;
            ClearRun();
        }

        // ── 시간대 전환 신호 (호스트·오프라인 전용) ──────────

        /// <summary>도입 컷씬(세대 교체 연출)이 끝났다.</summary>
        public bool EndIntro() => HostDo(r => r.EndIntro(), nameof(EndIntro));

        /// <summary>공연이 끝났다(공연 결과 처리 뒤 부른다).</summary>
        public bool EndPerformance() => HostDo(r => r.EndPerformance(), nameof(EndPerformance));

        /// <summary>누군가 대기실에서 백스테이지로 나왔다(B안 타이머 시작).</summary>
        public bool NotifyLeftWaitingRoom() => HostDo(r => r.NotifyLeftWaitingRoom(), nameof(NotifyLeftWaitingRoom));

        /// <summary>전원이 무대 입구 통로에 들어갔다(쉬는 시간 조기 종료).</summary>
        public bool NotifyAllAtStageEntrance() => HostDo(r => r.NotifyAllAtStageEntrance(), nameof(NotifyAllAtStageEntrance));

        /// <summary>전원이 탈출로 입구에 모여 탈출을 개시했다(쉬는 시간 타이머 정지).</summary>
        public bool StartEscape() => HostDo(r => r.StartEscape(), nameof(StartEscape));

        /// <summary>탈출로가 끝났다. 한 명 이상 도착이면 true(엔딩), 전원 리타이어면 false(런 실패).</summary>
        public bool EndEscape(bool anyoneArrived) => HostDo(r => r.EndEscape(anyoneArrived), nameof(EndEscape));

        /// <summary><paramref name="day"/>일차(2일차부터) 공연 순서를 정한다(밤 정비의 세트리스트).</summary>
        public bool SetSetlist(int day, string[] tricks) => HostDo(r => r.SetSetlist(day, tricks), nameof(SetSetlist));

        /// <summary>디버그: 쉬는 시간 남은 시간을 바꾼다(0이면 바로 끝난다).</summary>
        public bool DebugSetRemaining(float seconds) => HostDo(r => r.DebugSetRemaining(seconds), nameof(DebugSetRemaining));

        /// <summary>에디터/개발 빌드의 코스 전체 재시험. 방은 유지하고 새 세대 도입부터 시작한다(호스트 전용).</summary>
        public bool DebugRestartRun()
        {
            if ((!Application.isEditor && !CurtainCall.Dev.DevMode.Enabled) || !HasAuthority) return false;
            return HostDo(r => r.DebugRestartRun(), nameof(DebugRestartRun));
        }

        // ── 밤 정비 준비 완료 (누구나) ────────────────────────

        /// <summary>내 밤 정비 준비 완료를 바꾼다. 전원이 준비되면 다음 날 아침 쉬는 시간이 된다.</summary>
        public bool SetReady(bool value)
        {
            if (state.Phase != CampaignPhase.Night) return false;
            localReady = value;

            if (host != null)
            {
                ulong me = offlineRun ? NetworkManager.ServerClientId : session.NetworkManager.LocalClientId;
                return HostDo(r =>
                {
                    bool changed = r.SetReady(me, value);
                    r.RefreshNight(Players());
                    return changed;
                }, nameof(SetReady));
            }

            if (messaging == null) return false;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe(value);
            messaging.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            Changed?.Invoke();
            return true;
        }

        // ── 판정·적용 ────────────────────────────────────────

        bool HostDo(Func<RunState, bool> op, string name)
        {
            if (host == null)
            {
                Debug.LogWarning($"[Campaign] {name}: 호스트(또는 오프라인 런)에서만 부를 수 있습니다.");
                return false;
            }
            if (!op(host))
            {
                Debug.Log($"[Campaign] {name}: 지금 시간대({state.Phase})에는 해당하지 않아 무시합니다.");
                return false;
            }
            Apply(host.Snapshot);
            return true;
        }

        void Apply(CampaignSnapshot next)
        {
            var previous = state;
            state = next;

            if (host != null && !offlineRun && (!previous.SameStep(next) || Time.unscaledTime >= nextBroadcastAt))
                Broadcast();

            RaiseEvents(previous, next);
        }

        void RaiseEvents(CampaignSnapshot previous, CampaignSnapshot next)
        {
            bool newGeneration = previous.Generation > 0 && next.Generation > previous.Generation;
            bool stepChanged = previous.Generation != next.Generation || previous.Day != next.Day || previous.Phase != next.Phase
                || previous.Rest != next.Rest || previous.PerformanceIndex != next.PerformanceIndex;

            if (newGeneration && next.LastEnd != RunEndReason.None)
            {
                Debug.Log($"[Campaign] 런 실패({next.LastEnd}) → 세대 {next.Generation}로 교체");
                RunEnded?.Invoke(next.LastEnd);
            }
            if (next.Phase == CampaignPhase.Ending && previous.Phase != CampaignPhase.Ending)
            {
                Debug.Log("[Campaign] 탈출 성공 → 엔딩");
                RunEnded?.Invoke(RunEndReason.EscapeSucceeded);
                if (host != null && !offlineRun && session != null) session.EndGame();
            }
            if (stepChanged)
            {
                if (next.Phase != CampaignPhase.Night) localReady = false;
                Debug.Log($"[Campaign] 세대 {next.Generation} · {next.Day}일차 · {Describe(next)}");
                StepChanged?.Invoke(next);
            }
            if (next.AlarmRaised && !previous.AlarmRaised && next.Phase == CampaignPhase.RestTime)
            {
                Debug.Log($"[Campaign] 소집 경보: 쉬는 시간 {next.TimeRemaining:0}초 남음");
                RecallAlarm?.Invoke();
            }
            Changed?.Invoke();
        }

        void ClearRun()
        {
            host = null;
            offlineRun = false;
            localReady = false;
            Apply(default);
        }

        IReadOnlyCollection<ulong> Players()
        {
            if (offlineRun || session == null || session.NetworkManager == null) return OfflinePlayers;
            return session.NetworkManager.ConnectedClientsIds;
        }

        /// <summary>로그·디버그 화면용 시간대 이름.</summary>
        public static string Describe(in CampaignSnapshot s)
        {
            switch (s.Phase)
            {
                case CampaignPhase.None: return "런 전";
                case CampaignPhase.Intro: return s.IsGenerationChangeIntro ? "세대 교체 연출" : "도입 컷씬";
                case CampaignPhase.Performance: return $"공연 {s.PerformanceIndex + 1}({Trick(s)})";
                case CampaignPhase.RestTime:
                    string kind = s.Rest == RestKind.Morning ? (s.IsFinalMorning ? "마지막 아침 쉬는 시간" : "아침 쉬는 시간")
                        : s.Rest == RestKind.Guide ? "가이드 쉬는 시간" : "쉬는 시간";
                    return s.IsFinalMorning ? kind : $"{kind} → 공연 {s.PerformanceIndex + 1}({Trick(s)})";
                case CampaignPhase.Night: return "밤 정비";
                case CampaignPhase.Escape: return "탈출로";
                case CampaignPhase.Ending: return "엔딩";
                default: return s.Phase.ToString();
            }
        }

        static string Trick(in CampaignSnapshot s) => string.IsNullOrEmpty(s.TrickId) ? "미정" : s.TrickId;

        // ── 접속·메시지 ──────────────────────────────────────

        void HandleConnection(SessionConnectionState connection)
        {
            Unregister();
            if (connection == SessionConnectionState.Connecting) return;

            if (connection == SessionConnectionState.Offline)
            {
                if (!offlineRun && (host != null || state.IsRunning)) ClearRun();
                return;
            }

            if (offlineRun) ClearRun();
            messaging = session.NetworkManager != null ? session.NetworkManager.CustomMessagingManager : null;
            if (messaging == null) return;

            if (connection == SessionConnectionState.Host)
            {
                messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
                messaging.RegisterNamedMessageHandler(ReadyMessage, HandleReady);
                HandleGameState(session.GameState);
            }
            else
            {
                messaging.RegisterNamedMessageHandler(StateMessage, HandleState);
                using var writer = new FastBufferWriter(1, Allocator.Temp);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer);
            }
        }

        void HandleGameState(GameSessionState gameState)
        {
            if (session == null || !session.IsHost) return;

            if (gameState == GameSessionState.Playing && host == null)
            {
                host = new RunState();
                host.StartSession();
                Apply(host.Snapshot);
            }
            else if (gameState == GameSessionState.Waiting && host != null)
            {
                ClearRun();
                Broadcast();
            }
        }

        void Broadcast()
        {
            if (messaging == null || session == null || !session.IsHost) return;
            nextBroadcastAt = Time.unscaledTime + TimerBroadcastInterval;
            using var writer = WriteState();
            messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        FastBufferWriter WriteState()
        {
            var writer = new FastBufferWriter(128, Allocator.Temp, 1024);
            state.Write(writer);
            return writer;
        }

        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            if (messaging == null || session == null || !session.IsHost) return;
            using var writer = WriteState();
            messaging.SendNamedMessage(StateMessage, sender, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleReady(ulong sender, FastBufferReader reader)
        {
            if (host == null) return;
            reader.ReadValueSafe(out bool value);
            if (!host.SetReady(sender, value)) return;
            host.RefreshNight(Players());
            Apply(host.Snapshot);
        }

        void HandleState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || session == null || session.IsHost) return;
            Apply(CampaignSnapshot.Read(reader));
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(ReadyMessage);
            messaging = null;
        }
    }
}
