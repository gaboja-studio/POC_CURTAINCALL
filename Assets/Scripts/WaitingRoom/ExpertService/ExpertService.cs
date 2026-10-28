using System;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 신체 복구 서비스의 공개 진입점. 다른 기능은 이 컴포넌트만 쓴다.
    /// 판정은 공동 자금과 같은 화면(호스트, <see cref="PerformanceRewards.HasAuthority"/>)이 한다. 참가자는 요청만 보내고 상태를 받는다.
    /// M2(2026-10-28): 서비스는 신체 복구 하나(시설 서비스·전문가 일정 삭제). 쉬는 시간·밤 정비 언제든 이용한다.
    /// ③ 수화기에서 복구할 팔다리 수(1~3)를 골라 구매 → 구매자가 ⑧ 붉은 커튼 앞에 오면 커튼이 열리며 황금빛 → 구매자만 F로 입장
    /// → 커튼 닫힘·쇠붙이 소리 → 고른 수만큼 절단 역순으로 복구 → 커튼이 열리며 재등장 → 커튼 닫힘.
    /// 가격(<see cref="WaitingRoomSettings"/>)은 1개당 기본가에서 시작해 팀 이용 1회마다 오르고, 런이 새로 시작되면 처음으로 돌아간다.
    /// 연출·커튼·순간이동은 <see cref="ExpertRoomView"/>가 이 상태를 보고 각 화면에서 한다. 복구 결과는 공용 NetworkPlayer가 모두에게 맞춘다.
    /// </summary>
    public sealed class ExpertService : MonoBehaviour
    {
        const int SyncRequest = 0, MedicalRequest = 1, EnterRequest = 2;

        [Tooltip("커튼이 닫힌 뒤 복구까지 걸리는 시간(초).")]
        [SerializeField, Min(.5f)] float workSeconds = 3f;

        [Tooltip("복구 뒤 커튼이 열려 있는 시간(초). 이 동안 구매자가 커튼 앞에 다시 나타난다.")]
        [SerializeField, Min(.5f)] float exitSeconds = 2f;

        public static ExpertService Instance { get; private set; }

        ExpertServiceBook book;
        PerformanceRewards rewards;
        ShopChannel channel;
        int seenGeneration;

        static WaitingRoomSettings Prices => GameSettings.WaitingRoom;

        /// <summary>지금 팔다리 1개당 가격(팀 이용 횟수 반영).</summary>
        public long MedicalPrice => ExpertServiceBook.UnitPrice(book.TeamUses, Prices.BodyRestoreBasePrice, Prices.BodyRestorePriceStep);

        /// <summary>이번 런에서 팀 전체가 신체 복구를 이용한 횟수.</summary>
        public int TeamUses => book.TeamUses;

        /// <summary>한 번에 고를 수 있는 최대 팔다리 수(세팅).</summary>
        public static int MaxPartsPerUse => Prices.BodyRestoreMaxParts;

        public ExpertRoomPhase Phase => book.Phase;

        /// <summary>예약·이용 중인 사람(접속 번호). <see cref="ExpertRoomPhase.Idle"/>이면 의미 없다.</summary>
        public ulong Buyer => book.Buyer;

        /// <summary>이번 이용에서 복구할 팔다리 수.</summary>
        public int BookedLimbs => book.BookedLimbs;

        /// <summary>이 화면이 구매·입장을 판정한다.</summary>
        public bool HasAuthority => rewards != null && rewards.HasAuthority;

        /// <summary>내가 예약·이용 중인 사람이다.</summary>
        public bool LocalIsBuyer => book.HasBuyer && book.Buyer == channel.LocalClientId;

        public long Balance => rewards != null ? rewards.Balance : 0;

        /// <summary>내 마지막 요청 결과 문구(화면 안내용).</summary>
        public string LastMessage { get; private set; } = "";

        /// <summary>방 단계가 바뀌었다(모든 화면). 이전 단계, 새 단계.</summary>
        public event Action<ExpertRoomPhase, ExpertRoomPhase> PhaseChanged;
        public event Action<string, bool, string> TrackedRequestAnswered;

        /// <summary>오늘(런 일차). 런 전이면 0.</summary>
        public static int Day => CampaignRun.Instance != null ? CampaignRun.Instance.State.Day : 0;

        /// <summary>지금 서비스를 쓸 수 있는 시간대(쉬는 시간·밤 정비).</summary>
        public static bool IsServiceTime => CampaignRun.Instance != null
            && (CampaignRun.Instance.State.Phase == CampaignPhase.RestTime || CampaignRun.Instance.State.Phase == CampaignPhase.Night);

        /// <summary>지금 이용할 수 있는 시간대인지(창 표시용).</summary>
        public static ExpertRefusal Availability() => ExpertServiceBook.Availability(IsServiceTime);

        /// <summary>내 잃은 팔다리 수.</summary>
        public static int LocalLostLimbs => NetworkPlayer.Local != null ? PlayerCondition.CountLimbs(NetworkPlayer.Local.LostParts) : 0;

        /// <summary>내가 이번에 고를 수 있는 최대 개수(잃은 수와 한 번 최대치 중 작은 값).</summary>
        public static int LocalMaxParts => ExpertServiceBook.MaxParts(LocalLostLimbs, MaxPartsPerUse);

        /// <summary>내 몸으로 <paramref name="parts"/>개 복구를 살 수 있는지(창 표시용, 자금 제외). 최종 판정은 호스트가 한다.</summary>
        public ExpertRefusal LocalMedicalRefusal(int parts)
        {
            var me = NetworkPlayer.Local;
            bool alive = me != null && me.State == PlayerState.Normal;
            return book.CanBuyMedical(channel.LocalClientId, IsServiceTime, alive, LocalLostLimbs, parts, MaxPartsPerUse);
        }

        /// <summary>고를 수 있는 최대 개수 기준의 거절 사유(창 표시용).</summary>
        public ExpertRefusal LocalMedicalRefusal() => LocalMedicalRefusal(Math.Max(1, LocalMaxParts));

        /// <summary><paramref name="parts"/>개 복구 비용(지금 단가 × 개수).</summary>
        public long MedicalCost(int parts) => ExpertServiceBook.Cost(parts, book.TeamUses, Prices.BodyRestoreBasePrice, Prices.BodyRestorePriceStep);

        /// <summary>내가 고를 수 있는 최대 개수의 비용.</summary>
        public long LocalMedicalCost => MedicalCost(LocalMaxParts);

        /// <summary>지금 예약에 낸 비용(예약 때 단가 × 개수). 예약이 없으면 0.</summary>
        public long BookedCost => book.HasBuyer
            ? ExpertServiceBook.Cost(book.BookedLimbs, book.TeamUses - 1, Prices.BodyRestoreBasePrice, Prices.BodyRestorePriceStep) : 0;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            book = new ExpertServiceBook(workSeconds, exitSeconds);
            channel = new ShopChannel("CurtainCall.WaitingRoom.Expert", HandleRequest, HandleState, HandleResult, HandleReset,
                () => channel.Request(JsonUtility.ToJson(new RequestData { kind = SyncRequest })));
        }

        void OnDestroy()
        {
            channel?.Dispose();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (rewards == null) rewards = PerformanceRewards.EnsureExists();
            channel.Update();
            if (!HasAuthority) return;

            var before = book.Phase;
            long revision = book.Revision;
            WatchRun();
            // 공연이 시작됐거나 구매자가 나가면 방을 비운다(이미 낸 비용은 돌려주지 않는다 — 임시 규칙).
            if (book.HasBuyer && (!IsServiceTime || !channel.IsConnected(book.Buyer))) book.Clear();
            if (book.Tick(Time.deltaTime)) Restore(book.Buyer);
            if (book.Revision != revision) Publish(before);
        }

        // 세대 교체(런 실패)·런 끝이면 방과 가격을 처음으로.
        void WatchRun()
        {
            var run = CampaignRun.Instance;
            if (run == null) return;
            int generation = run.State.Generation;
            if ((seenGeneration > 0 && generation > seenGeneration) || !run.State.IsRunning) book.Reset();
            if (generation > 0) seenGeneration = generation;
        }

        /// <summary>③ 수화기 창: 고를 수 있는 최대 개수로 신체 복구를 산다. 결과는 <see cref="LastMessage"/>로 온다.</summary>
        public void RequestMedical() => RequestMedicalTracked("");
        /// <summary>③ 수화기 창: <paramref name="parts"/>개(1~3) 신체 복구를 산다.</summary>
        public void RequestMedical(int parts) => RequestMedicalTracked(parts, "");
        public void RequestMedicalTracked(string requestId) => RequestMedicalTracked(Math.Max(1, LocalMaxParts), requestId);
        public void RequestMedicalTracked(int parts, string requestId)
        {
            if (HasAuthority) Answer(channel.LocalClientId, BuyMedical(channel.LocalClientId, parts), requestId);
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = MedicalRequest, parts = parts, requestId = requestId })))
                Answer(channel.LocalClientId, (false, "접속 중이 아니라 구매할 수 없습니다."), requestId);
        }

        /// <summary>⑧ 커튼 앞에서 F: 구매자만 들어간다.</summary>
        public void RequestEnter()
        {
            if (HasAuthority) Answer(channel.LocalClientId, Enter(channel.LocalClientId));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = EnterRequest })))
                LastMessage = "접속 중이 아니라 들어갈 수 없습니다.";
        }

        /// <summary>화면 안내 문구를 바꾼다(⑧ 커튼 앞 F 안내 등, 판정 없음).</summary>
        public void ShowLocalMessage(string text) => LastMessage = text;

        /// <summary>판정 화면의 거절 결과: 요청자, MEDICAL_RESERVE/MEDICAL_ENTER, 실패 코드.</summary>
        public event Action<ulong, string, ExpertRefusal> OperationRejected;

        (bool, string) Reject(ulong actor, string operation, ExpertRefusal refusal)
        {
            if (OperationRejected != null)
                foreach (Action<ulong, string, ExpertRefusal> observer in OperationRejected.GetInvocationList())
                    try { observer(actor, operation, refusal); }
                    catch (Exception error) { Debug.LogException(error, this); }
            return (false, ExpertServiceBook.Explain(refusal));
        }

        (bool, string) BuyMedical(ulong buyer, int parts)
        {
            var player = Find(buyer);
            bool alive = player != null && player.State == PlayerState.Normal;
            int lost = player != null ? PlayerCondition.CountLimbs(player.LostParts) : 0;
            long cost = MedicalCost(parts);
            var before = book.Phase;
            var refusal = book.BookMedical(buyer, IsServiceTime, alive, lost, parts, MaxPartsPerUse,
                () => rewards.TrySpend(cost, $"신체 복구 {parts}개 — {WaitingRoomShop.PlayerLabel(buyer)}"));
            if (refusal != ExpertRefusal.None) return Reject(buyer, "MEDICAL_RESERVE", refusal);
            Debug.Log($"[Expert] 신체 복구 {parts}개 구매({cost}) ← {WaitingRoomShop.PlayerLabel(buyer)} / 잔액 {rewards.Balance} / 다음 단가 {MedicalPrice}");
            Publish(before);
            return (true, $"구매 완료({parts}개, {cost}) — ⑧ 전문가 대기실 커튼 앞으로 가세요.");
        }

        (bool, string) Enter(ulong who)
        {
            var before = book.Phase;
            var refusal = book.Enter(who);
            if (refusal != ExpertRefusal.None) return Reject(who, "MEDICAL_ENTER", refusal);
            Debug.Log($"[Expert] 입장 {WaitingRoomShop.PlayerLabel(who)}");
            Publish(before);
            return (true, "");
        }

        // 예약 때 고른 수만큼 되찾는다(공용 NetworkPlayer가 절단 역순으로 1개씩).
        void Restore(ulong buyer)
        {
            var player = Find(buyer);
            int restored = 0;
            for (int i = 0; player != null && i < book.BookedLimbs; i++)
                if (player.ServerRestorePart() != BodyPart.None) restored++;
            Debug.Log(restored > 0
                ? $"[Expert] 신체 복구 {restored}개 → {WaitingRoomShop.PlayerLabel(buyer)} (남은 결손 {player.LostParts})"
                : $"[Expert] 복구할 수 없었습니다({WaitingRoomShop.PlayerLabel(buyer)}).");
        }

        static NetworkPlayer Find(ulong clientId)
        {
            foreach (var player in NetworkPlayer.All)
                if (player.IsSpawned && player.OwnerClientId == clientId) return player;
            return null;
        }

        void Answer(ulong target, (bool ok, string text) result, string requestId = "")
        {
            if (target == channel.LocalClientId || !channel.IsHost)
            {
                LastMessage = result.text;
                TrackedRequestAnswered?.Invoke(requestId, result.ok, result.text);
                return;
            }
            channel.SendResult(target, JsonUtility.ToJson(new ResultData { ok = result.ok, text = result.text, requestId = requestId }));
        }

        void Publish(ExpertRoomPhase before)
        {
            channel.SendState(JsonUtility.ToJson(StateData.From(book)));
            if (before != book.Phase) PhaseChanged?.Invoke(before, book.Phase);
        }

        // ---- 메시지 ----

        void HandleRequest(ulong sender, string json)
        {
            if (!HasAuthority) return;
            var request = JsonUtility.FromJson<RequestData>(json);
            if (request == null) return;
            switch (request.kind)
            {
                case SyncRequest: channel.SendState(JsonUtility.ToJson(StateData.From(book)), sender); break;
                case MedicalRequest: Answer(sender, BuyMedical(sender, request.parts), request.requestId); break;
                case EnterRequest: Answer(sender, Enter(sender)); break;
            }
        }

        void HandleState(string json)
        {
            var state = JsonUtility.FromJson<StateData>(json);
            if (state == null || state.revision < book.Revision) return;
            var before = book.Phase;
            book.Apply((ExpertRoomPhase)state.phase, state.buyer, state.limbs, state.uses, state.revision);
            if (before != book.Phase) PhaseChanged?.Invoke(before, book.Phase);
        }

        void HandleResult(string json)
        {
            var result = JsonUtility.FromJson<ResultData>(json);
            if (result != null)
            {
                LastMessage = result.text;
                TrackedRequestAnswered?.Invoke(result.requestId, result.ok, result.text);
            }
        }

        void HandleReset()
        {
            // 방이 바뀌면 이전 방의 예약·가격 상승을 남기지 않는다.
            var before = book.Phase;
            book.Apply(ExpertRoomPhase.Idle, 0, 0, 0, 0);
            seenGeneration = 0;
            LastMessage = "";
            if (before != book.Phase) PhaseChanged?.Invoke(before, book.Phase);
        }

        [Serializable] sealed class RequestData { public int kind; public int parts; public string requestId; }
        [Serializable] sealed class ResultData { public bool ok; public string text; public string requestId; }

        [Serializable]
        sealed class StateData
        {
            public long revision;
            public int phase;
            public ulong buyer;
            public int limbs;
            public int uses;

            public static StateData From(ExpertServiceBook book) => new()
            {
                revision = book.Revision,
                phase = (int)book.Phase,
                buyer = book.Buyer,
                limbs = book.BookedLimbs,
                uses = book.TeamUses,
            };
        }
    }
}
