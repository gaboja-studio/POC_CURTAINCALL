using System;
using System.Collections.Generic;
using System.Linq;
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
        public const float DemoEndingSeconds = 10f;

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
        /// <summary>판정 화면의 실제 시간 만료 직전 쉬는 시간. 표시 타이머 0과 구분한다.</summary>
        public event Action<CampaignSnapshot> RestExpired;
        /// <summary>내 준비 상태가 바뀌었을 때. 마지막 준비자의 즉시 시간대 전환보다 먼저 알린다.</summary>
        public event Action<bool> LocalReadyChanged;

        /// <summary>런이 끝났다. 실패(기한 초과·전원 리타이어)면 다음 세대의 1일차 첫 공연이 시작된다.</summary>
        public event Action<RunEndReason> RunEnded;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            gameObject.AddComponent<CurtainCall.UI.SceneIntegration.DemoRunEnding>();
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
                bool changed = (state.Phase == CampaignPhase.Night || state.Phase == CampaignPhase.Dawn
                    || state.Phase == CampaignPhase.DemoEnding) && host.RefreshNight(Players());
                changed |= host.Tick(Time.deltaTime, Expired);
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
            host = new RunState(Application.version == "0.1.0", true);
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

        /// <summary>전원이 무대 입구 통로에 들어갔다(쉬는 시간 조기 종료).</summary>
        public bool NotifyAllAtStageEntrance() => HostDo(r => r.NotifyAllAtStageEntrance(), nameof(NotifyAllAtStageEntrance));

        /// <summary>전원이 탈출로 입구에 모여 탈출을 개시했다(쉬는 시간 타이머 정지).</summary>
        public bool StartEscape() => HostDo(r => r.StartEscape(), nameof(StartEscape));

        /// <summary>탈출로가 끝났다. 한 명 이상 도착이면 true(엔딩), 전원 리타이어면 false(런 실패).</summary>
        public bool EndEscape(bool anyoneArrived) => HostDo(r => r.EndEscape(anyoneArrived), nameof(EndEscape));

        /// <summary><paramref name="day"/>일차(2일차부터) 공연 순서를 정한다(밤 정비의 세트리스트).</summary>
        public bool SetSetlist(int day, string[] tricks) => HostDo(r => r.SetSetlist(day, tricks), nameof(SetSetlist));

        /// <summary><paramref name="day"/>일차 공연 순서와 묘기별 난이도 단계(1~3)를 함께 정한다(M2 난이도 선택).</summary>
        public bool SetSetlist(int day, string[] tricks, int[] tiers) => HostDo(r => r.SetSetlist(day, tricks, tiers), nameof(SetSetlist));

        /// <summary>호스트: 공연 성공 단계를 기록한다. 그 단계의 다음 단계를 고를 수 있게 된다.</summary>
        public bool RecordClear(string trickId, int tier) => HostDo(r => r.RecordClear(trickId, tier), nameof(RecordClear));

        /// <summary>호스트: 그 묘기에 고를 수 있는 가장 높은 단계(깬 단계 + 1, 최대 3). 판정 화면이 아니면 1.</summary>
        public int MaxSelectableTier(string trickId) => host != null ? host.MaxSelectableTier(trickId) : 1;

        /// <summary>호스트: 이번 런에서 그 묘기를 성공한 가장 높은 단계(없으면 0).</summary>
        public int ClearedTier(string trickId) => host != null ? host.ClearedTier(trickId) : 0;

        /// <summary>호스트/오프라인이 대기실의 쉬는 시간·밤 정비에서만 런을 포기할 수 있다.</summary>
        public bool CanAbandonRun => HasAuthority
            && (state.Phase == CampaignPhase.RestTime || state.Phase == CampaignPhase.Night)
            && CurtainCall.SpaceLink.SpaceLinkDirector.Instance != null
            && CurtainCall.SpaceLink.SpaceLinkDirector.Instance.Current == CurtainCall.SpaceLink.SpaceId.WaitingRoom
            && !CurtainCall.SpaceLink.SpaceLinkDirector.Instance.IsTransitioning
            && !CurtainCall.UI.SceneIntegration.SceneCurtainTransition.IsActive
            && !(CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation.Current != null
                && CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation.Current.ResultVisible);

        /// <summary>클라이언트 요청을 전달하지 않고 호스트 자신의 확인만 받는다.</summary>
        public bool AbandonRun(CampaignSnapshot expected)
        {
            if (!CanAbandonRun) return false;
            return HostDo(r => r.AbandonRun(expected), nameof(AbandonRun));
        }

        /// <summary>디버그: 쉬는 시간 남은 시간을 바꾼다(0이면 바로 끝난다).</summary>
        public bool DebugSetRemaining(float seconds) => HostDo(r => r.DebugSetRemaining(seconds), nameof(DebugSetRemaining));

        /// <summary>에디터/개발 빌드의 코스 전체 재시험. 방은 유지하고 새 세대 도입부터 시작한다(호스트 전용).</summary>
        public bool DebugRestartRun()
        {
            if ((!Application.isEditor && !CurtainCall.Dev.DevMode.Enabled) || !HasAuthority) return false;
            return HostDo(r => r.DebugRestartRun(), nameof(DebugRestartRun));
        }

        /// <summary>Dev 모드의 호스트/오프라인 전용. 선택 지점에서 새 테스트 런을 시작한다.</summary>
        public bool DebugStartAt(RunStartPoint point)
        {
            if (!CurtainCall.Dev.DevMode.Enabled || !HasAuthority || !point.IsValid(out _)) return false;
            if (!host.DebugStartAt(point)) return false;
            localReady = false;
            Apply(host.Snapshot);
            return true;
        }

        // ── 밤 정비 준비 완료 (누구나) ────────────────────────

        /// <summary>밤 준비 / 전환 가림 완료 / 데모 확인을 보낸다. 현재 세대·일차·시간대에만 적용한다.</summary>
        public bool SetReady(bool value)
        {
            if (state.Phase != CampaignPhase.Night && state.Phase != CampaignPhase.Dawn && state.Phase != CampaignPhase.DemoEnding) return false;
            if (state.Phase != CampaignPhase.Night && !value) return false;
            bool changedReady = localReady != value;
            localReady = value;
            if (changedReady && state.Phase == CampaignPhase.Night && LocalReadyChanged != null)
                foreach (Action<bool> observer in LocalReadyChanged.GetInvocationList())
                    try { observer(value); } catch (Exception error) { Debug.LogException(error, this); }

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
            using var writer = new FastBufferWriter(10, Allocator.Temp);
            writer.WriteValueSafe(value);
            writer.WriteValueSafe(state.Generation);
            writer.WriteValueSafe(state.Day);
            writer.WriteValueSafe((byte)state.Phase);
            messaging.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            Changed?.Invoke();
            return true;
        }

        // ── 판정·적용 ────────────────────────────────────────

        void Expired(CampaignSnapshot snapshot)
        {
            if (RestExpired != null)
                foreach (Action<CampaignSnapshot> observer in RestExpired.GetInvocationList())
                    try { observer(snapshot); } catch (Exception error) { Debug.LogException(error, this); }
        }

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
                case CampaignPhase.DemoEnding: return "데모 플레이 종료";
                case CampaignPhase.Dawn: return "다음 날 아침으로 전환 중";
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
                host = new RunState(Application.version == "0.1.0", true);
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
            reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out int day);
            reader.ReadValueSafe(out byte phase);
            if (generation != state.Generation || day != state.Day || phase != (byte)state.Phase || !Players().Contains(sender)) return;
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
