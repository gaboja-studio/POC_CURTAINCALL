using System;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 밤 정비 전문가 서비스의 공개 진입점(⑧ 전문가 대기실 한 칸). 다른 기능은 이 컴포넌트만 쓴다.
    /// 판정은 공동 자금과 같은 화면(호스트, <see cref="PerformanceRewards.HasAuthority"/>)이 한다. 참가자는 요청만 보내고 상태를 받는다.
    /// 흐름(연출 A안): ③ 수화기에서 구매 → ⑧ 문 열림 → 구매자만 입구 F → 문 닫힘·쇠붙이 소리 → 신체 1개 복구 → 문 열림·재등장 → 문 닫힘.
    /// 연출·문·순간이동은 <see cref="ExpertRoomView"/>가 이 상태를 보고 각 화면에서 한다. 복구 결과(잃은 팔다리)는 공용 NetworkPlayer가 모두에게 맞춘다.
    /// 가격·복구 시간은 임시값이다(전문가 서비스 상세 기획 확정 전).
    /// </summary>
    public sealed class ExpertService : MonoBehaviour
    {
        const int SyncRequest = 0, BuyRequest = 1, EnterRequest = 2;

        [Header("임시값 — 상세 기획 확정 전")]
        [Tooltip("의료 신체 복구 1개 비용(공동 자금).")]
        [SerializeField, Min(1)] long medicalPrice = 30;

        [Tooltip("문이 닫힌 뒤 복구까지 걸리는 시간(초).")]
        [SerializeField, Min(.5f)] float workSeconds = 3f;

        [Tooltip("복구 뒤 문이 열려 있는 시간(초). 이 동안 구매자가 문 앞에 다시 나타난다.")]
        [SerializeField, Min(.5f)] float exitSeconds = 2f;

        public static ExpertService Instance { get; private set; }

        ExpertServiceBook book;
        PerformanceRewards rewards;
        ShopChannel channel;

        public long MedicalPrice => medicalPrice;

        public ExpertRoomPhase Phase => book.Phase;

        /// <summary>예약·이용 중인 사람(접속 번호). <see cref="ExpertRoomPhase.Idle"/>이면 의미 없다.</summary>
        public ulong Buyer => book.Buyer;

        /// <summary>이 화면이 구매·입장을 판정한다.</summary>
        public bool HasAuthority => rewards != null && rewards.HasAuthority;

        /// <summary>내 접속 번호(접속 전이면 호스트 번호).</summary>
        public ulong LocalClientId => channel.LocalClientId;

        /// <summary>내가 예약·이용 중인 사람이다.</summary>
        public bool LocalIsBuyer => book.HasBuyer && book.Buyer == channel.LocalClientId;

        public long Balance => rewards != null ? rewards.Balance : 0;

        /// <summary>내 마지막 요청 결과 문구(화면 안내용).</summary>
        public string LastMessage { get; private set; } = "";

        /// <summary>방 단계가 바뀌었다(모든 화면). 이전 단계, 새 단계.</summary>
        public event Action<ExpertRoomPhase, ExpertRoomPhase> PhaseChanged;

        /// <summary>오늘(런 일차). 런 전이면 0.</summary>
        public static int Day => CampaignRun.Instance != null ? CampaignRun.Instance.State.Day : 0;

        /// <summary>지금 밤 정비 중.</summary>
        public static bool IsNight => CampaignRun.Instance != null && CampaignRun.Instance.State.Phase == CampaignPhase.Night;

        /// <summary>그 전문가의 서비스를 오늘 밤 쓸 수 있는지(창 표시용).</summary>
        public static ExpertRefusal Availability(ExpertKind kind) => ExpertServiceBook.Availability(kind, Day, IsNight);

        /// <summary>내 몸으로 의료 신체 복구를 살 수 있는지(창 표시용, 자금 제외). 최종 판정은 호스트가 한다.</summary>
        public ExpertRefusal LocalMedicalRefusal()
        {
            var me = NetworkPlayer.Local;
            bool alive = me != null && me.State == PlayerState.Normal;
            int lost = me != null ? PlayerCondition.CountLimbs(me.LostParts) : 0;
            return book.CanBuyMedical(Day, IsNight, alive, lost);
        }

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
            // 밤이 끝났거나 구매자가 나가면 방을 비운다(이미 낸 비용은 돌려주지 않는다 — 임시 규칙).
            if (book.HasBuyer && (!IsNight || !channel.IsConnected(book.Buyer))) book.Clear();
            if (book.Tick(Time.deltaTime)) Restore(book.Buyer);
            if (book.Phase != before) Publish(before);
        }

        /// <summary>③ 수화기 창: 의료 신체 복구 1개를 산다. 결과는 <see cref="LastMessage"/>로 온다.</summary>
        public void RequestMedical()
        {
            if (HasAuthority) Answer(channel.LocalClientId, Buy(channel.LocalClientId));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = BuyRequest })))
                LastMessage = "접속 중이 아니라 구매할 수 없습니다.";
        }

        /// <summary>⑧ 입구에서 F: 구매자만 들어간다.</summary>
        public void RequestEnter()
        {
            if (HasAuthority) Answer(channel.LocalClientId, Enter(channel.LocalClientId));
            else if (!channel.Request(JsonUtility.ToJson(new RequestData { kind = EnterRequest })))
                LastMessage = "접속 중이 아니라 들어갈 수 없습니다.";
        }

        (bool, string) Buy(ulong buyer)
        {
            var player = Find(buyer);
            bool alive = player != null && player.State == PlayerState.Normal;
            int lost = player != null ? PlayerCondition.CountLimbs(player.LostParts) : 0;
            var before = book.Phase;
            var refusal = book.Book(buyer, Day, IsNight, alive, lost,
                () => rewards.TrySpend(medicalPrice, $"의료 신체 복구 — {WaitingRoomShop.PlayerLabel(buyer)}"));
            if (refusal != ExpertRefusal.None) return (false, ExpertServiceBook.Explain(refusal));
            Debug.Log($"[Expert] 의료 신체 복구 구매 ← {WaitingRoomShop.PlayerLabel(buyer)} / 잔액 {rewards.Balance}");
            Publish(before);
            return (true, "구매 완료 — ⑧ 전문가 대기실 문이 열렸습니다.");
        }

        (bool, string) Enter(ulong who)
        {
            var before = book.Phase;
            var refusal = book.Enter(who);
            if (refusal != ExpertRefusal.None) return (false, ExpertServiceBook.Explain(refusal));
            Debug.Log($"[Expert] 입장 {WaitingRoomShop.PlayerLabel(who)}");
            Publish(before);
            return (true, "");
        }

        void Restore(ulong buyer)
        {
            var player = Find(buyer);
            var part = player != null ? player.ServerRestorePart() : BodyPart.None;
            Debug.Log(part != BodyPart.None
                ? $"[Expert] 신체 복구 {part} → {WaitingRoomShop.PlayerLabel(buyer)}"
                : $"[Expert] 복구할 수 없었습니다({WaitingRoomShop.PlayerLabel(buyer)}).");
        }

        static NetworkPlayer Find(ulong clientId)
        {
            foreach (var player in NetworkPlayer.All)
                if (player.IsSpawned && player.OwnerClientId == clientId) return player;
            return null;
        }

        void Answer(ulong target, (bool ok, string text) result)
        {
            if (target == channel.LocalClientId || !channel.IsHost) { LastMessage = result.text; return; }
            channel.SendResult(target, JsonUtility.ToJson(new ResultData { ok = result.ok, text = result.text }));
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
                case BuyRequest: Answer(sender, Buy(sender)); break;
                case EnterRequest: Answer(sender, Enter(sender)); break;
            }
        }

        void HandleState(string json)
        {
            var state = JsonUtility.FromJson<StateData>(json);
            if (state == null || state.revision < book.Revision) return;
            var before = book.Phase;
            book.Apply((ExpertRoomPhase)state.phase, state.buyer, state.revision);
            if (before != book.Phase) PhaseChanged?.Invoke(before, book.Phase);
        }

        void HandleResult(string json)
        {
            var result = JsonUtility.FromJson<ResultData>(json);
            if (result != null) LastMessage = result.text;
        }

        void HandleReset()
        {
            // 방이 바뀌면 이전 방의 예약을 남기지 않는다.
            var before = book.Phase;
            book.Apply(ExpertRoomPhase.Idle, 0, 0);
            LastMessage = "";
            if (before != book.Phase) PhaseChanged?.Invoke(before, book.Phase);
        }

        [Serializable] sealed class RequestData { public int kind; }
        [Serializable] sealed class ResultData { public bool ok; public string text; }

        [Serializable]
        sealed class StateData
        {
            public long revision;
            public int phase;
            public ulong buyer;

            public static StateData From(ExpertServiceBook book) =>
                new() { revision = book.Revision, phase = (int)book.Phase, buyer = book.Buyer };
        }
    }
}
