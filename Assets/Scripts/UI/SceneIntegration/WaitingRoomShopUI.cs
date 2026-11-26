using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Reward;
using CurtainCall.Sound;
using CurtainCall.WaitingRoom;
using CurtainCall.UI.ShopV1;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 완성 W2 프리팹의 표시/요청을 실제 상점·전문가 서비스에 연결한다.
    /// - 창 제목은 프리팹의 제목 TMP에 적힌 글을 그대로 쓴다(인스펙터·프리팹에서 고침).
    /// - 바뀌는 문구·아이콘 키는 <see cref="ShopUiTexts"/> 에셋, 기물 이름·설명·가격은 기물 목록(ItemCatalog)에서 온다.
    /// </summary>
    public sealed class WaitingRoomShopUI : MonoBehaviour
    {
        // 2026-11-26 M3 피드백: 장부 탭을 없애고 기물을 '탐색'·'공연'으로 나눠 판다. 상점 키트는 탭이 3칸(Items/Services/Ledger) 고정이라
        // 칸 이름은 그대로 두고 용도만 바꾼다. 탭 글자는 상점 프리팹(00~02_Tab)에서 탐색/공연/서비스로 적는다.
        public const ShopTab ExplorationTab = ShopTab.Items;
        public const ShopTab PerformanceTab = ShopTab.Services;
        public const ShopTab ServiceTab = ShopTab.Ledger;

        [SerializeField] ShopPanelView view;
        [SerializeField, Min(5)] float responseTimeout = 15;
        [Tooltip("상점 문구·아이콘 키 설정")]
        [SerializeField] ShopUiTexts texts;
        [Tooltip("창 제목 TMP. 여기에 적힌 글이 제목으로 쓰인다")]
        [SerializeField] TMP_Text titleLabel;
        string title;
        WaitingRoomShop shop;
        ExpertService service;
        ShopPhone phone;
        ShopActionRequest pending;
        string requestId, session, fingerprint;
        int revision;
        float sentAt, nextRefresh;
        EventSystem ownedEvents;
        ShopTab observedTab;
        string observedEntry;
        public ShopPanelView View => view;
        public ShopUiTexts Texts => texts;
        public void Configure(ShopPanelView panel) => view = panel;

        void Awake() => title = titleLabel != null ? titleLabel.text : "";
        static string Fill(string template, params (string name, object value)[] values) =>
            string.IsNullOrEmpty(template) ? "" : WaitingRoomText.Fill(template, values);

        void OnEnable()
        {
            if (view == null) return;
            view.ActionRequested += Request;
            view.Closed += Closed;
        }
        void Update()
        {
            if (view == null) return;
            BindServices();
            if (ownedEvents != null && FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Any(e => e != ownedEvents && e.isActiveAndEnabled))
            { Destroy(ownedEvents.gameObject); ownedEvents = null; }
            var room = CommonUiSceneLoader.CurrentWaitingRoom;
            if (phone != null && (room == null || phone.gameObject.scene != room.gameObject.scene))
            {
                phone.SetOpen(false);
                view.Hide();
                phone = null;
            }
            if (phone == null && room != null)
                phone = room.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopPhone>()).FirstOrDefault(p => p.isActiveAndEnabled);
            bool open = room != null && phone != null && phone.isActiveAndEnabled && phone.IsOpen && shop != null && !WaitingRoomShop.InPerformance;
            if (!open) { if (view != null && view.IsVisible) view.Hide(); return; }
            if (!view.IsVisible)
            {
                EnsureEvents();
                session = Guid.NewGuid().ToString("N"); revision = 0; fingerprint = null;
                view.Open(Build());
                ObserveSelection(false);
                ApplyFonts();
            }
            ObserveSelection(true);
            if (pending != null && Time.unscaledTime - sentAt > responseTimeout)
                Answer(requestId, false, Fill(texts.responseTimeout));
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            var model = Build();
            string key = model.GetContext(ExplorationTab) + model.GetContext(PerformanceTab) + model.GetContext(ServiceTab) + string.Join("|", model.Entries.Select(e =>
                e.Id + e.IconKey + e.Title + e.Subtitle + e.PriceText + e.Description + e.UnavailableReason + e.StatusText + e.Available));
            if (fingerprint == key) return;
            fingerprint = key; view.SetPresentation(model); ObserveSelection(false); ApplyFonts();
        }
        void ObserveSelection(bool playSound)
        {
            if (playSound && (observedTab != view.CurrentTab || observedEntry != view.SelectedEntryId))
                SoundManager.Play(SoundSituations.ShopSelection);
            observedTab = view.CurrentTab;
            observedEntry = view.SelectedEntryId;
        }
        void EnsureEvents()
        {
            if (EventSystem.current != null) return;
            var root = new GameObject("ShopEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            root.transform.SetParent(transform, false); ownedEvents = root.GetComponent<EventSystem>();
        }
        void ApplyFonts() => GetComponentInParent<UiRuntimeFontFallback>()?.ApplyTo(view.transform);
        void BindServices()
        {
            if (shop != WaitingRoomShop.Instance)
            {
                if (shop != null) shop.TrackedRequestAnswered -= Answer;
                shop = WaitingRoomShop.Instance;
                if (shop != null) shop.TrackedRequestAnswered += Answer;
            }
            if (service != ExpertService.Instance)
            {
                if (service != null) service.TrackedRequestAnswered -= Answer;
                service = ExpertService.Instance;
                if (service != null) service.TrackedRequestAnswered += Answer;
            }
        }
        public ShopPresentation Build()
        {
            var entries = new List<ShopEntryPresentation>();
            if (shop != null)
            {
                foreach (var item in shop.Catalog)
                {
                    var tab = ItemCatalog.Get(item.Id)?.use == ItemUse.Performance ? PerformanceTab : ExplorationTab;
                    if (WaitingRoomShop.ShopLocked(item.Id))
                    {
                        // 아직 상점에 추가되지 않은 기물. 누군가 처음 습득하면 알림과 함께 추가된다(2026-11-18 사용자).
                        // 공연 탭에는 검은 실루엣과 '해금되지 않은 기물' 안내로 미리 보여 준다(2026-11-26 M3). 살 수 없다.
                        if (tab == PerformanceTab) entries.Add(LockedEntry(item.Id, tab));
                        continue;
                    }
                    string reason = !WaitingRoomShop.CanPurchase ? WaitingRoomShop.PurchaseTimeRefusal : shop.Balance < item.Price ? Fill(texts.reasonNoFunds) : "";
                    string icon = view.ResolveIcon(item.Id) != null ? item.Id : texts.itemFallbackIcon;
                    string description = string.Join("\n", new[] { ItemCatalog.Description(item.Id), Fill(texts.itemDescriptionFooter) }.Where(line => !string.IsNullOrEmpty(line)));
                    entries.Add(new ShopEntryPresentation("item:" + item.Id, tab, icon, item.Name, Fill(texts.itemSubtitle), item.Price.ToString(),
                        description,
                        Fill(texts.itemPriceDetail, ("price", item.Price)), reason.Length == 0, reason,
                        Fill(texts.itemWaiting, ("n", shop.Waiting.Count)), texts.itemStatusIcon,
                        new ShopActionPresentation("buy", Fill(texts.buyButton))));
                }
            }
            AddServices(entries);
            long balance = shop?.Balance ?? 0;
            int waiting = shop?.Waiting.Count ?? 0;
            return new ShopPresentation(session ?? "unopened", (++revision).ToString(), title, ExplorationTab, "",
                Fill(texts.itemsContext, ("balance", balance), ("n", waiting)),
                Fill(texts.performanceContext, ("balance", balance), ("n", waiting)),
                Fill(texts.servicesContext, ("balance", balance), ("day", ExpertService.Day)), entries.ToArray());
        }
        /// <summary>해금 전 공연 기물 한 줄: 검은 실루엣 아이콘(키 "locked:기물ID"), 이름·가격 가림, 구매 버튼 없음.</summary>
        ShopEntryPresentation LockedEntry(string itemId, ShopTab tab)
        {
            string icon = view.ResolveIcon("locked:" + itemId) != null ? "locked:" + itemId : texts.lockedFallbackIcon;
            // 설명 칸에만 안내를 한 번 적는다(이용 불가 사유 줄은 비워 같은 문장이 두 번 보이지 않게).
            return new ShopEntryPresentation("locked:" + itemId, tab, icon, Fill(texts.lockedTitle), Fill(texts.lockedSubtitle),
                Fill(texts.lockedPrice), Fill(texts.lockedDescription), "", false);
        }
        // M2: 서비스는 신체 복구 하나(시설 서비스 삭제). 한 항목에서 1~최대(잃은 부위, 3개) 수량을 고르고 예약한다. 비용은 지금 1개당 가격 × 개수.
        // 상점 템플릿은 항목당 버튼 2개까지라 [예약]·[수량 변경] 두 버튼을 쓴다. 수량은 이 화면에만 있는 선택값이다.
        // 예약한 사람은 입장 전까지 남은 팔다리를 같은 두 버튼으로 추가 예약한다(2026-11-18 사용자). 1개당 가격은 고정(2026-11-26).
        int medicalParts = 1;
        int MedicalMax => Mathf.Max(1, service != null ? service.LocalSelectableParts : ExpertService.LocalMaxParts);
        void AddServices(List<ShopEntryPresentation> entries)
        {
            string status = service == null || service.Phase == ExpertRoomPhase.Idle ? ""
                : Fill(texts.serviceStatus, ("player", WaitingRoomShop.PlayerLabel(service.Buyer)), ("phase", ExpertServicePhonePanel.PhaseLabel(service.Phase)));
            int max = MedicalMax;
            medicalParts = Mathf.Clamp(medicalParts, 1, max);
            int parts = medicalParts;
            bool adding = service != null && service.LocalIsAdding;
            int booked = adding ? service.BookedLimbs : 0;
            var refusal = service != null ? service.LocalMedicalRefusal(parts) : ExpertRefusal.InPerformance;
            long cost = service?.LocalCost(parts) ?? 0;
            string reason = service == null ? Fill(texts.serviceLoading) : refusal != ExpertRefusal.None ? ExpertServiceBook.Explain(refusal) : service.Balance < cost ? Fill(texts.reasonNoFunds) : "";
            bool canBook = reason.Length == 0;
            bool canChangeCount = service != null && refusal == ExpertRefusal.None && max > 1;
            string subtitle = adding && !string.IsNullOrEmpty(texts.medicalAddSubtitle)
                ? Fill(texts.medicalAddSubtitle, ("booked", booked), ("max", max), ("lost", ExpertService.LocalLostLimbs))
                : Fill(texts.medicalSubtitle, ("lost", ExpertService.LocalLostLimbs), ("max", max));
            string bookLabel = adding && !string.IsNullOrEmpty(texts.addBookButton)
                ? Fill(texts.addBookButton, ("n", parts), ("booked", booked))
                : Fill(texts.bookButton, ("n", parts));
            entries.Add(new ShopEntryPresentation("medical", ServiceTab, texts.medicalIcon,
                Fill(texts.medicalTitle, ("n", parts)),
                subtitle,
                service == null ? "—" : cost.ToString(),
                Fill(texts.medicalDescription),
                service == null ? "" : Fill(texts.medicalPriceDetail, ("price", service.MedicalPrice)),
                // Entry availability gates both buttons. Keep quantity selection usable when only funds are short.
                canBook || canChangeCount, reason, status, texts.medicalIcon,
                new ShopActionPresentation("medical", bookLabel, canBook),
                new ShopActionPresentation("count", Fill(texts.countButton, ("n", parts), ("max", max)), canChangeCount)));
        }
        void Request(ShopActionRequest request)
        {
            if (phone == null || !phone.IsOpen || pending != null) { view.RejectAction(request, Fill(texts.reopenWindow)); return; }
            if (request.EntryId == "medical" && request.ActionId == "count")
            {
                // 수량 변경은 서버 요청 없이 이 화면에서 1 → 최대 → 1 순서로 돈다.
                int max = MedicalMax;
                medicalParts = medicalParts % max + 1;
                view.AcceptAction(request, Fill(texts.countSelected, ("n", medicalParts)), Build()); fingerprint = null; nextRefresh = 0; ApplyFonts();
                return;
            }
            pending = request; requestId = Guid.NewGuid().ToString("N"); sentAt = Time.unscaledTime;
            if (request.EntryId.StartsWith("item:") && request.ActionId == "buy" && shop != null)
                shop.RequestPurchaseTracked(request.EntryId.Substring(5), requestId);
            else if (request.EntryId == "medical" && request.ActionId == "medical" && service != null)
                service.RequestMedicalTracked(Mathf.Clamp(medicalParts, 1, MedicalMax), requestId);
            else Answer(requestId, false, Fill(texts.invalidRequest));
        }
        void Answer(string id, bool ok, string text)
        {
            if (pending == null || string.IsNullOrEmpty(id) || id != requestId) return;
            var request = pending; pending = null; requestId = null;
            bool displayed = ok ? view.AcceptAction(request, text, Build()) : view.RejectAction(request, text);
            if (displayed)
            {
                SoundManager.Play(ok ? SoundSituations.ShopAccepted : SoundSituations.ShopRejected);
                ObserveSelection(false);
            }
            fingerprint = null; nextRefresh = 0; ApplyFonts();
        }
        void Closed()
        {
            pending = null; requestId = null;
            if (phone != null) phone.SetOpen(false);
        }
        void OnDisable()
        {
            if (view != null) { view.Hide(); view.ActionRequested -= Request; view.Closed -= Closed; }
            Closed();
            if (shop != null) shop.TrackedRequestAnswered -= Answer;
            if (service != null) service.TrackedRequestAnswered -= Answer;
            shop = null; service = null;
            if (ownedEvents != null) Destroy(ownedEvents.gameObject);
        }
    }
}
