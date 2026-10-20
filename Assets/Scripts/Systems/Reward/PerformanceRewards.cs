using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Results;
using CurtainCall.Settings;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Reward
{
    /// <summary>묘기 공통 결과·호응·공동 자금의 공개 진입점. 판정은 호스트 또는 명시적 오프라인 런만 한다.</summary>
    public sealed class PerformanceRewards : MonoBehaviour
    {
        /// <summary>모든 공연은 생존자 1명까지 계속하며, 0명이 되면 실패한다.</summary>
        public const int OfficialMinSurvivors = 1;

        const string StateMessage = "CurtainCall.Reward.State";
        const string RequestMessage = "CurtainCall.Reward.Request";
        const string PageMessage = "CurtainCall.Reward.LedgerPage";
        const int MaxPayloadBytes = 64 * 1024;
        const string DefaultTablePath = "Settings/GameSettings/Tricks/Rewards/temporary-reward";

        public static PerformanceRewards Instance { get; private set; }
        public static PerformanceRewards EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(PerformanceRewards));
            return go.AddComponent<PerformanceRewards>();
        }

        readonly Dictionary<NetworkPlayer, Action<PlayerState, PlayerState>> observers = new();
        readonly Dictionary<ulong, float> requestTimes = new();
        readonly List<PerformanceResult> receivedHistory = new();
        CampaignRun campaign;
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        bool messagingHost;
        PerformanceRewardModel model;
        RewardSnapshot state;
        RewardSnapshot ledgerPage;
        RewardTable table;
        RewardRules? rulesOverride;
        int difficulty = 1;
        bool dummyRoster;
        bool ending;
        bool pendingTransition;
        string retiredSession;
        float nextRequest;
        float nextTransitionRetry;
        float transitionAt;

        public RewardSnapshot State => state;
        public RewardSnapshot LedgerPage => ledgerPage;
        public long Balance => state?.Balance ?? 0;
        public PerformanceResult Latest => state?.Latest;
        public bool HasAuthority => campaign != null && campaign.HasAuthority && session != null
            && (session.IsHost || (campaign.IsOfflineRun && session.ConnectionState == SessionConnectionState.Offline));
        public bool HasRulesOverride => rulesOverride.HasValue;
        public bool TransitionPending => pendingTransition;
        public IReadOnlyList<PerformanceResult> History => model != null ? model.History : receivedHistory.AsReadOnly();
        public event Action Changed;
        public event Action<PerformanceResult> ResultConfirmed;
        /// <summary>정산 직전 호스트 관찰. 도착 환호를 같은 공연의 정산에 포함한다.</summary>
        public event Action<IReadOnlyDictionary<string, string>> BeforeSettlement;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() => Bind();

        void Update()
        {
            Bind();
            SyncCampaign();
            if (HasAuthority && model != null && model.IsOpen && !ending)
            {
                long previous = model.Revision;
                model.ApplyRules(ReadRules());
                ObserveNetwork();
                if (model.Revision != previous) Publish();
                EndIfInsufficientSurvivors();
            }
            if (pendingTransition && Time.unscaledTime >= nextTransitionRetry && Time.unscaledTime >= transitionAt)
            {
                nextTransitionRetry = Time.unscaledTime + 1f;
                CompleteTransition();
            }
            if (session != null && session.ConnectionState == SessionConnectionState.Client && Time.unscaledTime >= nextRequest)
            {
                nextRequest = Time.unscaledTime + 5f;
                RequestSnapshot();
            }
        }

        void Bind()
        {
            if (campaign == null)
            {
                campaign = CampaignRun.EnsureExists();
                campaign.StepChanged += HandleStep;
                campaign.Changed += SyncCampaign;
            }
            if (session == null)
            {
                session = NetworkSessionManager.EnsureExists();
                if (session != null) session.ConnectionStateChanged += HandleConnection;
            }
            var next = session != null && (session.ConnectionState == SessionConnectionState.Host
                || session.ConnectionState == SessionConnectionState.Client) ? session.NetworkManager?.CustomMessagingManager : null;
            bool host = session != null && session.IsHost;
            if (next == messaging && messagingHost == host) return;
            Unregister();
            messaging = next;
            messagingHost = host;
            if (messaging == null) return;
            if (host) messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
            else
            {
                messaging.RegisterNamedMessageHandler(StateMessage, HandleState);
                messaging.RegisterNamedMessageHandler(PageMessage, HandlePage);
                nextRequest = 0;
            }
        }

        void HandleConnection(SessionConnectionState connection)
        {
            if (connection == SessionConnectionState.Offline && campaign != null && !campaign.IsOfflineRun) Clear();
            Bind();
            SyncCampaign();
        }

        void HandleStep(CampaignSnapshot step)
        {
            if (step.Phase == CampaignPhase.None && session != null && session.GameState == GameSessionState.Waiting) Clear();
            SyncCampaign();
        }

        void SyncCampaign()
        {
            if (campaign == null) return;
            var current = campaign.State;
            if (!current.IsRunning)
            {
                // Campaign 메시지보다 보상 메시지가 먼저 도착할 수 있다. 클라이언트는 접속 종료에서만 지운다.
                if (model != null) Clear();
                return;
            }
            if (!HasAuthority) return;
            if (model == null)
            {
                model = new PerformanceRewardModel(Guid.NewGuid().ToString("N"), current.Generation);
                Publish();
            }
            else if (model.AdvanceGeneration(current.Generation, current.Day))
            {
                pendingTransition = false;
                ClearObservers();
                Publish();
            }
        }

        /// <summary>실제 네트워크 참가 명부로 시작한다. 같은 공연의 재호출로 명부를 다시 고정하지 않는다.</summary>
        public bool BeginPerformance(int difficultyLevel, RewardTable rewardTable = null)
        {
            Bind();
            SyncCampaign();
            if (!CanBegin(difficultyLevel)) return false;
            if (SamePerformance()) return true;
            var people = new List<RewardParticipantSeed>();
            if (session.NetworkManager == null) return false;
            foreach (ulong id in session.NetworkManager.ConnectedClientsIds)
            {
                var player = FindPlayer(id);
                // 아직 spawn되지 않은 사람의 시작 부상을 0으로 추정하지 않는다.
                if (player == null) return false;
                people.Add(new RewardParticipantSeed(ParticipantId(id), id, player.Slot, (int)player.LostParts, IsDead(player.State), player.ForfeitsNextPerformance));
            }
            return people.Count > 0 && Begin(people, difficultyLevel, rewardTable, false);
        }

        /// <summary>오프라인 테스트용 시작. 온라인에서는 더미 명부를 받지 않는다.</summary>
        public bool BeginOfflinePerformance(IReadOnlyList<RewardParticipantSeed> people, int difficultyLevel, RewardTable rewardTable = null)
        {
            Bind();
            SyncCampaign();
            if (!CanBegin(difficultyLevel) || !campaign.IsOfflineRun) return false;
            if (SamePerformance()) return true;
            return Begin(people, difficultyLevel, rewardTable, true);
        }

        bool CanBegin(int level) => !ending && !pendingTransition && HasAuthority && model != null
            && campaign.State.Phase == CampaignPhase.Performance && level >= 1 && level <= 3;
        bool SamePerformance() => model.Performance != null && model.Performance.Id == Identity().Id;
        PerformanceIdentity Identity()
        {
            var current = campaign.State;
            return new PerformanceIdentity(model.SessionId, current.Generation, current.Day, current.PerformanceIndex,
                string.IsNullOrWhiteSpace(current.TrickId) ? "Unassigned" : current.TrickId);
        }
        bool Begin(IReadOnlyList<RewardParticipantSeed> people, int level, RewardTable rewardTable, bool dummy)
        {
            difficulty = level;
            table = rewardTable != null ? rewardTable : Resources.Load<RewardTable>(DefaultTablePath);
            if (!model.Begin(Identity(), people, ReadRules())) return false;
            dummyRoster = dummy;
            Publish();
            return true;
        }

        public bool AddCheer(string participantId, bool positive = true)
        {
            if (!CanMutate()) return false;
            long previous = model.Revision;
            ObserveNetwork();
            bool changed = model.Cheer(participantId, positive, ReadRules());
            if (model.Revision != previous) Publish();
            return changed;
        }

        public bool AddAudienceReaction(string participantId, int delta)
        {
            if (!CanMutate()) return false;
            long previous = model.Revision;
            ObserveNetwork();
            bool changed = model.React(participantId, delta, ReadRules());
            if (model.Revision != previous) Publish();
            return changed;
        }

        /// <summary>호스트 테스트/묘기 관찰 입력. 신체 손실은 0~15 비트마스크이며 결과에는 개수로 고정한다.</summary>
        public bool ObserveParticipant(string participantId, int lostParts, bool dead, bool connected = true)
        {
            if (!CanMutate()) return false;
            long previous = model.Revision;
            bool changed = model.Observe(participantId, lostParts, dead, connected, ReadRules());
            if (model.Revision != previous) Publish();
            if (changed) EndIfInsufficientSurvivors();
            return changed;
        }

        public bool SetRulesOverride(RewardRules? value)
        {
            if (!HasAuthority || ending || pendingTransition || model == null) return false;
            rulesOverride = value;
            model.ApplyRules(ReadRules());
            Publish();
            return true;
        }

        RewardRules ReadRules()
        {
            if (rulesOverride.HasValue) return rulesOverride.Value;
            var original = table != null ? table.ReadRules(difficulty) : RewardRules.Temporary(difficulty);
            var audience = AudienceReactionSettings.Current;
            string trick = campaign != null ? campaign.State.TrickId : null;
            return audience != null && (trick == "Tightrope" || trick == "HumanCannon")
                ? new RewardRules(original.Cheer, original.Jeer, Math.Max(1, audience.cap), original.AllSurvivedBonus, Math.Max(0, audience.cut))
                : original;
        }
        bool CanMutate() => HasAuthority && model != null && model.IsOpen && !ending && !pendingTransition;

        void EndIfInsufficientSurvivors()
        {
            if (CanMutate() && model.SurvivorCount < OfficialMinSurvivors)
                FinishPerformance(false, "NoSurvivors");
        }

        /// <summary>닫기 → 최종 관찰 → 정산 → 상태 송신 enqueue → Campaign 종료. 묘기의 판정 문자열은 그대로 보존한다.</summary>
        public bool FinishPerformance(bool trickSucceeded, string endReason, IReadOnlyDictionary<string, string> judgements = null)
        {
            if (!CanMutate() || campaign.State.Phase != CampaignPhase.Performance || !SamePerformance()) return false;
            if (trickSucceeded && BeforeSettlement != null)
                foreach (Action<IReadOnlyDictionary<string, string>> observer in BeforeSettlement.GetInvocationList())
                    try { observer(judgements); } catch (Exception error) { Debug.LogException(error); }
            ending = true;
            PerformanceResult result = null;
            try
            {
                ObserveNetwork();
                if (!model.Confirm(trickSucceeded, endReason, judgements, OfficialMinSurvivors, ReadRules(), out result)) return false;
                ClearObservers();
                pendingTransition = true;
                // 결과는 즉시 확정/송신하고, 두 공연의 사망·결과·검은 전환 화면을 본 뒤 시간대를 넘긴다.
                float hold = PerformanceEndingSettings.HoldFor(result.Performance.TrickId);
                if (!result.Succeeded) hold = Mathf.Max(hold, GameSettings.Base.Run.FailedPerformanceHold);
                transitionAt = Time.unscaledTime + hold;
                if (Time.unscaledTime >= transitionAt) CompleteTransition();
                else Publish();
                return true;
            }
            finally
            {
                ending = false;
                if (result != null) NotifyResult(result);
            }
        }

        bool CompleteTransition()
        {
            if (!pendingTransition || !HasAuthority || model == null) return false;
            var settled = model.Performance;
            // 네트워크 공유 실패 시 확정 결과는 유지하고 송신만 재시도한다. 정산을 다시 하지 않는다.
            if (!Publish()) return false;
            // Changed 구독자가 Campaign을 먼저 진행시켜도 다음 공연을 종료하지 않는다.
            if (!pendingTransition) return true;
            var current = campaign.State;
            if (settled == null || current.Phase != CampaignPhase.Performance
                || current.Generation != settled.Generation || current.Day != settled.Day || current.PerformanceIndex != settled.Index)
            { pendingTransition = false; return true; }
            bool transitioned = campaign.EndPerformance();
            if (transitioned) pendingTransition = false;
            return transitioned;
        }

        public bool TrySpend(long amount, string description)
        {
            if (!HasAuthority || model == null || ending || pendingTransition || !model.Spend(amount, description, campaign.State.Day)) return false;
            Publish();
            return true;
        }

        void ObserveNetwork()
        {
            if (dummyRoster || model == null || !model.IsOpen || session?.NetworkManager == null) return;
            var currentRules = ReadRules();
            foreach (var segment in model.Snapshot().Segments)
            {
                var player = FindPlayer(segment.ClientId);
                bool connected = ContainsClient(segment.ClientId);
                if (player != null)
                {
                    if (!observers.ContainsKey(player))
                    {
                        string id = segment.ParticipantId;
                        Action<PlayerState, PlayerState> handler = (_, next) => ObserveParticipant(id, (int)player.LostParts, IsDead(next));
                        observers.Add(player, handler);
                        player.StateChanged += handler;
                    }
                    model.Observe(segment.ParticipantId, (int)player.LostParts, IsDead(player.State), connected, currentRules);
                }
                else if (!connected)
                    model.Observe(segment.ParticipantId, PartsForCount(segment.LostCount), segment.Dead, false, currentRules);
            }
        }

        bool ContainsClient(ulong id)
        {
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds) if (client == id) return true;
            return false;
        }
        string ParticipantId(ulong id) => $"{model.SessionId}:{id}";
        static NetworkPlayer FindPlayer(ulong id)
        {
            foreach (var player in NetworkPlayer.All) if (player.IsSpawned && player.OwnerClientId == id) return player;
            return null;
        }
        static bool IsDead(PlayerState value) => value == PlayerState.Fallen || value == PlayerState.Retired;
        static int PartsForCount(int count) => (1 << count) - 1;

        bool Publish()
        {
            if (model == null) return false;
            state = model.Snapshot();
            ledgerPage = state;
            bool sent = campaign.IsOfflineRun || SendState(StateMessage, state);
            NotifyChanged();
            return sent;
        }

        bool SendState(string message, RewardSnapshot snapshot, ulong? target = null)
        {
            if (messaging == null || session == null || !session.IsHost) return false;
            string json = JsonUtility.ToJson(RewardStateData.From(snapshot));
            int size = FastBufferWriter.GetWriteSize(json);
            if (size > MaxPayloadBytes) { Debug.LogError("[Reward] 공유 상태가 메시지 상한을 넘었습니다."); return false; }
            try
            {
                using var writer = new FastBufferWriter(size, Allocator.Temp);
                writer.WriteValueSafe(json);
                if (target.HasValue) messaging.SendNamedMessage(message, target.Value, writer, NetworkDelivery.ReliableFragmentedSequenced);
                else messaging.SendNamedMessageToAll(message, writer, NetworkDelivery.ReliableFragmentedSequenced);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return false;
            }
        }

        public void RequestSnapshot() => SendRequest(-1);
        public bool RequestLedgerPage(int start)
        {
            if (state == null || start < 0 || start > state.LedgerCount) return false;
            if (HasAuthority && model != null)
            {
                ledgerPage = model.Snapshot(start);
                NotifyChanged();
                return true;
            }
            return SendRequest(start);
        }
        bool SendRequest(int start)
        {
            if (messaging == null || session == null || session.ConnectionState != SessionConnectionState.Client) return false;
            string json = JsonUtility.ToJson(new RewardRequestData
            {
                session = state?.SessionId, generation = state?.Generation ?? 0, revision = state?.Revision ?? 0, ledgerStart = start
            });
            using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(json), Allocator.Temp);
            writer.WriteValueSafe(json);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            return true;
        }
        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            if (!HasAuthority || model == null || !ContainsClient(sender) || reader.Length > 1024) return;
            if (requestTimes.TryGetValue(sender, out float last) && Time.unscaledTime - last < .2f) return;
            requestTimes[sender] = Time.unscaledTime;
            try
            {
                reader.ReadValueSafe(out string json);
                var request = JsonUtility.FromJson<RewardRequestData>(json);
                if (request == null || request.ledgerStart < -1) return;
                if (request.ledgerStart >= 0)
                {
                    if (request.session != model.SessionId || request.generation != model.Generation
                        || request.revision != model.Revision || request.ledgerStart > model.Ledger.Count)
                    { SendState(StateMessage, model.Snapshot(), sender); return; }
                    SendState(PageMessage, model.Snapshot(request.ledgerStart), sender);
                }
                else SendState(StateMessage, model.Snapshot(), sender);
            }
            catch (Exception error) when (InvalidMessage(error)) { Debug.LogWarning("[Reward] 잘못된 상태 요청을 거절했습니다."); }
        }
        void HandleState(ulong sender, FastBufferReader reader) => ReadState(sender, reader, false);
        void HandlePage(ulong sender, FastBufferReader reader) => ReadState(sender, reader, true);
        void ReadState(ulong sender, FastBufferReader reader, bool page)
        {
            if (sender != NetworkManager.ServerClientId || session == null || session.IsHost || reader.Length > MaxPayloadBytes) return;
            try
            {
                reader.ReadValueSafe(out string json);
                var next = JsonUtility.FromJson<RewardStateData>(json)?.Read();
                if (next == null || next.SessionId == retiredSession) return;
                if (page)
                {
                    if (state == null || next.SessionId != state.SessionId || next.Generation != state.Generation
                        || next.Revision != state.Revision || next.Balance != state.Balance || next.LedgerCount != state.LedgerCount) return;
                    ledgerPage = next;
                    NotifyChanged();
                    return;
                }
                if (!next.IsNewerThan(state)) return;
                string previousId = state?.Latest?.Performance.Id;
                state = next;
                ledgerPage = next;
                if (next.Latest != null && next.Latest.Performance.Id != previousId)
                {
                    receivedHistory.Add(next.Latest);
                    if (receivedHistory.Count > 10) receivedHistory.RemoveAt(0);
                    NotifyResult(next.Latest);
                }
                NotifyChanged();
            }
            catch (Exception error) when (InvalidMessage(error)) { Debug.LogWarning("[Reward] 잘못된 공유 상태를 거절했습니다."); }
        }
        static bool InvalidMessage(Exception error) => error is ArgumentException || error is OverflowException
            || error is InvalidOperationException || error is IndexOutOfRangeException || error is NullReferenceException;

        void NotifyChanged()
        {
            if (Changed == null) return;
            foreach (Action handler in Changed.GetInvocationList())
                try { handler(); } catch (Exception error) { Debug.LogException(error); }
        }
        void NotifyResult(PerformanceResult result)
        {
            if (ResultConfirmed == null) return;
            foreach (Action<PerformanceResult> handler in ResultConfirmed.GetInvocationList())
                try { handler(result); } catch (Exception error) { Debug.LogException(error); }
        }
        void ClearObservers()
        {
            foreach (var pair in observers) if (pair.Key != null) pair.Key.StateChanged -= pair.Value;
            observers.Clear();
        }
        void Clear()
        {
            retiredSession = state?.SessionId ?? retiredSession;
            ClearObservers();
            model = null;
            state = ledgerPage = null;
            pendingTransition = false;
            rulesOverride = null;
            receivedHistory.Clear();
            requestTimes.Clear();
            NotifyChanged();
        }
        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(PageMessage);
            messaging = null;
        }
        void OnDestroy()
        {
            if (Instance != this) return;
            Unregister();
            ClearObservers();
            if (campaign != null) { campaign.StepChanged -= HandleStep; campaign.Changed -= SyncCampaign; }
            if (session != null) session.ConnectionStateChanged -= HandleConnection;
            Instance = null;
        }
    }
}
