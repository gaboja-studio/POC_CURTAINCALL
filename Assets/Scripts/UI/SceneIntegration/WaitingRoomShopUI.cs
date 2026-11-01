using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Reward;
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
                ApplyFonts();
            }
            if (pending != null && Time.unscaledTime - sentAt > responseTimeout)
                Answer(requestId, false, Fill(texts.responseTimeout));
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            var model = Build();
            string key = model.GetContext(ShopTab.Items) + model.GetContext(ShopTab.Services) + string.Join("|", model.Entries.Select(e =>
                e.Id + e.Title + e.Subtitle + e.PriceText + e.Description + e.UnavailableReason + e.StatusText + e.Available));
            if (fingerprint == key) return;
            fingerprint = key; view.SetPresentation(model); ApplyFonts();
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
                    string reason = !WaitingRoomShop.CanPurchase ? WaitingRoomShop.PurchaseTimeRefusal : shop.Balance < item.Price ? Fill(texts.reasonNoFunds) : "";
                    string icon = view.ResolveIcon(item.Id) != null ? item.Id : texts.itemFallbackIcon;
                    string description = string.Join("\n", new[] { ItemCatalog.Description(item.Id), Fill(texts.itemDescriptionFooter) }.Where(line => !string.IsNullOrEmpty(line)));
                    entries.Add(new ShopEntryPresentation("item:" + item.Id, ShopTab.Items, icon, item.Name, Fill(texts.itemSubtitle), item.Price.ToString(),
                        description,
                        Fill(texts.itemPriceDetail, ("price", item.Price)), reason.Length == 0, reason,
                        Fill(texts.itemWaiting, ("n", shop.Waiting.Count)), texts.itemStatusIcon,
                        new ShopActionPresentation("buy", Fill(texts.buyButton))));
                }
            }
            AddServices(entries);
            var state = PerformanceRewards.Instance != null ? PerformanceRewards.Instance.State : null;
            if (state != null)
                foreach (var entry in state.Ledger.Reverse())
                    entries.Add(new ShopEntryPresentation("fund:" + entry.Number, ShopTab.Ledger, texts.ledgerIcon, entry.Description,
                        Fill(texts.ledgerSubtitle, ("number", entry.Number), ("day", entry.Day)), entry.Amount.ToString("+#;-#;0"), entry.Description,
                        Fill(texts.ledgerBalance, ("before", entry.Balance - entry.Amount), ("after", entry.Balance)), true, "",
                        Fill(texts.ledgerSettled), texts.ledgerIcon));
            if (shop != null)
            {
                foreach (var d in shop.Waiting.Reverse())
                    entries.Add(new ShopEntryPresentation("delivery:" + d.Order, ShopTab.Ledger, texts.deliveryIcon, d.ItemName,
                        Fill(texts.deliverySubtitle, ("order", d.Order), ("player", WaitingRoomShop.PlayerLabel(d.Buyer))),
                        Fill(texts.deliveryWaiting), Fill(texts.deliveryDescription), "", true));
                foreach (var r in shop.Receipts.Reverse())
                    entries.Add(new ShopEntryPresentation("receipt:" + r.Delivery.Order, ShopTab.Ledger, texts.deliveryIcon, r.Delivery.ItemName,
                        Fill(texts.receiptSubtitle, ("order", r.Delivery.Order)), Fill(texts.receiptDone),
                        Fill(texts.receiptDescription, ("buyer", WaitingRoomShop.PlayerLabel(r.Delivery.Buyer)), ("claimer", WaitingRoomShop.PlayerLabel(r.Claimer))), "", true));
            }
            long balance = shop?.Balance ?? 0;
            return new ShopPresentation(session ?? "unopened", (++revision).ToString(), title, ShopTab.Items, "",
                Fill(texts.itemsContext, ("balance", balance), ("n", shop?.Waiting.Count ?? 0)),
                Fill(texts.servicesContext, ("balance", balance), ("day", ExpertService.Day)),
                Fill(texts.ledgerContext, ("balance", balance)), entries.ToArray());
        }
        // M2: 서비스는 신체 복구 하나(시설 서비스 삭제). 한 항목에서 1~최대(잃은 부위, 3개) 수량을 고르고 예약한다. 비용은 지금 1개당 가격 × 개수.
        // 상점 템플릿은 항목당 버튼 2개까지라 [예약]·[수량 변경] 두 버튼을 쓴다. 수량은 이 화면에만 있는 선택값이다.
        int medicalParts = 1;
        void AddServices(List<ShopEntryPresentation> entries)
        {
            string status = service == null || service.Phase == ExpertRoomPhase.Idle ? ""
                : Fill(texts.serviceStatus, ("player", WaitingRoomShop.PlayerLabel(service.Buyer)), ("phase", ExpertServicePhonePanel.PhaseLabel(service.Phase)));
            int max = Mathf.Max(1, ExpertService.LocalMaxParts);
            medicalParts = Mathf.Clamp(medicalParts, 1, max);
            int parts = medicalParts;
            var refusal = service != null ? service.LocalMedicalRefusal(parts) : ExpertRefusal.InPerformance;
            long cost = service?.MedicalCost(parts) ?? 0;
            string reason = service == null ? Fill(texts.serviceLoading) : refusal != ExpertRefusal.None ? ExpertServiceBook.Explain(refusal) : service.Balance < cost ? Fill(texts.reasonNoFunds) : "";
            entries.Add(new ShopEntryPresentation("medical", ShopTab.Services, texts.medicalIcon,
                Fill(texts.medicalTitle, ("n", parts)),
                Fill(texts.medicalSubtitle, ("lost", ExpertService.LocalLostLimbs), ("max", max)),
                service == null ? "—" : cost.ToString(),
                Fill(texts.medicalDescription),
                service == null ? "" : Fill(texts.medicalPriceDetail, ("price", service.MedicalPrice)),
                reason.Length == 0, reason, status, texts.medicalIcon,
                new ShopActionPresentation("medical", Fill(texts.bookButton, ("n", parts))),
                new ShopActionPresentation("count", Fill(texts.countButton, ("n", parts), ("max", max)), max > 1)));
        }
        void Request(ShopActionRequest request)
        {
            if (phone == null || !phone.IsOpen || pending != null) { view.RejectAction(request, Fill(texts.reopenWindow)); return; }
            if (request.EntryId == "medical" && request.ActionId == "count")
            {
                // 수량 변경은 서버 요청 없이 이 화면에서 1 → 최대 → 1 순서로 돈다.
                int max = Mathf.Max(1, ExpertService.LocalMaxParts);
                medicalParts = medicalParts % max + 1;
                view.AcceptAction(request, Fill(texts.countSelected, ("n", medicalParts)), Build()); fingerprint = null; nextRefresh = 0; ApplyFonts();
                return;
            }
            pending = request; requestId = Guid.NewGuid().ToString("N"); sentAt = Time.unscaledTime;
            if (request.EntryId.StartsWith("item:") && request.ActionId == "buy" && shop != null)
                shop.RequestPurchaseTracked(request.EntryId.Substring(5), requestId);
            else if (request.EntryId == "medical" && request.ActionId == "medical" && service != null)
                service.RequestMedicalTracked(Mathf.Clamp(medicalParts, 1, Mathf.Max(1, ExpertService.LocalMaxParts)), requestId);
            else Answer(requestId, false, Fill(texts.invalidRequest));
        }
        void Answer(string id, bool ok, string text)
        {
            if (pending == null || string.IsNullOrEmpty(id) || id != requestId) return;
            var request = pending; pending = null; requestId = null;
            if (ok) view.AcceptAction(request, text, Build()); else view.RejectAction(request, text);
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
