using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Reward;
using CurtainCall.WaitingRoom;
using CurtainCall.UI.ShopV1;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>완성 W2 프리팹의 표시/요청을 실제 상점·전문가 서비스에 연결한다.</summary>
    public sealed class WaitingRoomShopUI : MonoBehaviour
    {
        [SerializeField] ShopPanelView view;
        [SerializeField, Min(5)] float responseTimeout = 15;
        WaitingRoomShop shop;
        ExpertService service;
        ShopPhone phone;
        ShopActionRequest pending;
        string requestId, session, fingerprint;
        int revision;
        float sentAt, nextRefresh;
        EventSystem ownedEvents;
        public ShopPanelView View => view;
        public void Configure(ShopPanelView panel) => view = panel;

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
                Answer(requestId, false, WaitingRoomText.Get("shopui.response-timeout", "응답 미확인 · 재구매 전 장부와 배송구를 확인하세요."));
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
                    string reason = !WaitingRoomShop.CanPurchase ? WaitingRoomShop.PurchaseTimeRefusal : shop.Balance < item.Price ? WaitingRoomText.Get("shopui.reason-no-funds", "공동 자금 부족") : "";
                    string icon = view.ResolveIcon(item.Id) != null ? item.Id : "delivery";
                    string generic = WaitingRoomText.Get("shopui.item-description", "공동 자금으로 주문합니다. 배송구에서 직접 수령하세요.");
                    string itemDesc = ItemTexts.Description(item.Id);
                    string description = string.IsNullOrEmpty(itemDesc) ? generic : itemDesc + "\n" + generic;
                    entries.Add(new ShopEntryPresentation("item:" + item.Id, ShopTab.Items, icon, item.Name, WaitingRoomText.Get("shopui.item-subtitle", "구매 후 배송구에서 수령"), item.Price.ToString(),
                        description,
                        WaitingRoomText.Format("shopui.item-price-detail", "구매 {price}", ("price", item.Price)), reason.Length == 0, reason,
                        WaitingRoomText.Format("shopui.item-waiting", "배송구 대기 {n}개", ("n", shop.Waiting.Count)), "delivery",
                        new ShopActionPresentation("buy", WaitingRoomText.Get("shopui.button-buy", "구매"))));
                }
            }
            AddServices(entries);
            var state = PerformanceRewards.Instance != null ? PerformanceRewards.Instance.State : null;
            if (state != null)
                foreach (var entry in state.Ledger.Reverse())
                    entries.Add(new ShopEntryPresentation("fund:" + entry.Number, ShopTab.Ledger, "currency", entry.Description,
                        WaitingRoomText.Format("shopui.ledger-subtitle", "#{number} · {day}일차", ("number", entry.Number), ("day", entry.Day)), entry.Amount.ToString("+#;-#;0"), entry.Description,
                        WaitingRoomText.Format("shopui.ledger-balance", "잔액 {before} → {after}", ("before", entry.Balance - entry.Amount), ("after", entry.Balance)), true, "",
                        WaitingRoomText.Get("shopui.ledger-settled", "정산 완료"), "currency"));
            if (shop != null)
            {
                foreach (var d in shop.Waiting.Reverse())
                    entries.Add(new ShopEntryPresentation("delivery:" + d.Order, ShopTab.Ledger, "delivery", d.ItemName,
                        WaitingRoomText.Format("shopui.delivery-subtitle", "주문 #{order} · {player}", ("order", d.Order), ("player", WaitingRoomShop.PlayerLabel(d.Buyer))),
                        WaitingRoomText.Get("shopui.delivery-waiting", "수령 대기"),
                        WaitingRoomText.Get("shopui.delivery-description", "배송구에 도착했습니다. 가까이서 상호작용하여 수령하세요."), "", true));
                foreach (var r in shop.Receipts.Reverse())
                    entries.Add(new ShopEntryPresentation("receipt:" + r.Delivery.Order, ShopTab.Ledger, "delivery", r.Delivery.ItemName,
                        WaitingRoomText.Format("shopui.receipt-subtitle", "수령 #{order}", ("order", r.Delivery.Order)),
                        WaitingRoomText.Get("shopui.receipt-done", "수령 완료"),
                        WaitingRoomText.Format("shopui.receipt-description", "{buyer} 주문 → {claimer} 수령", ("buyer", WaitingRoomShop.PlayerLabel(r.Delivery.Buyer)), ("claimer", WaitingRoomShop.PlayerLabel(r.Claimer))), "", true));
            }
            string buyState = WaitingRoomShop.CanPurchase
                ? WaitingRoomText.Get("shopui.items-state-open", "밤 정비 · 구매 가능")
                : WaitingRoomText.Get("shopui.items-state-closed", "기물 구매는 밤 정비에만 가능");
            string serviceState = ExpertService.IsServiceTime
                ? WaitingRoomText.Get("shopui.services-state-open", "신체 복구 이용 가능")
                : WaitingRoomText.Get("shopui.services-state-closed", "공연 중 이용 불가");
            return new ShopPresentation(session ?? "unopened", (++revision).ToString(), WaitingRoomText.Get("shopui.title", "상점 수화기"), ShopTab.Items, "",
                WaitingRoomText.Format("shopui.items-context", "공동 자금 {balance} · 배송구 대기 {n}개 · {state}", ("balance", shop?.Balance ?? 0), ("n", shop?.Waiting.Count ?? 0), ("state", buyState)),
                WaitingRoomText.Format("shopui.services-context", "공동 자금 {balance} · {day}일차 · {state}", ("balance", shop?.Balance ?? 0), ("day", ExpertService.Day), ("state", serviceState)),
                WaitingRoomText.Get("shopui.ledger-context", "최근 공동 자금 장부 · 배송 및 수령 기록"), entries.ToArray());
        }
        // M2: 서비스는 신체 복구 하나(시설 서비스 삭제). 한 항목에서 1~최대(잃은 부위, 3개) 수량을 고르고 예약한다. 비용은 지금 1개당 가격 × 개수.
        // 상점 템플릿은 항목당 버튼 2개까지라 [예약]·[수량 변경] 두 버튼을 쓴다. 수량은 이 화면에만 있는 선택값이다.
        int medicalParts = 1;
        void AddServices(List<ShopEntryPresentation> entries)
        {
            string status = service == null ? "" : service.Phase != ExpertRoomPhase.Idle ? WaitingRoomText.Format("shopui.service-status", "{player} · {phase}", ("player", WaitingRoomShop.PlayerLabel(service.Buyer)), ("phase", ExpertServicePhonePanel.PhaseLabel(service.Phase))) : "";
            int max = Mathf.Max(1, ExpertService.LocalMaxParts);
            medicalParts = Mathf.Clamp(medicalParts, 1, max);
            int parts = medicalParts;
            var refusal = service != null ? service.LocalMedicalRefusal(parts) : ExpertRefusal.InPerformance;
            long cost = service?.MedicalCost(parts) ?? 0;
            string reason = service == null ? WaitingRoomText.Get("shopui.service-loading", "서비스 정보를 불러오는 중입니다.") : refusal != ExpertRefusal.None ? ExpertServiceBook.Explain(refusal) : service.Balance < cost ? WaitingRoomText.Get("shopui.reason-no-funds", "공동 자금 부족") : "";
            entries.Add(new ShopEntryPresentation("medical", ShopTab.Services, "medical",
                WaitingRoomText.Format("service.medical-title", "신체 복구 {n}개", ("n", parts)),
                WaitingRoomText.Format("service.medical-subtitle", "잃은 팔다리 {lost}개 · 최대 {max}개", ("lost", ExpertService.LocalLostLimbs), ("max", max)),
                service == null ? "—" : cost.ToString(),
                WaitingRoomText.Get("service.medical-description", "수량을 고르고 예약하세요.\n예약 뒤 로비 신체 복구 승강기에서 F로 이용하며,\n최근 잃은 부위부터 복구합니다."),
                service == null ? "" : WaitingRoomText.Format("service.medical-price-detail", "1개당 {price} · 팀 이용 {uses}회", ("price", service.MedicalPrice), ("uses", service.TeamUses)),
                reason.Length == 0, reason, status, "medical",
                new ShopActionPresentation("medical", WaitingRoomText.Format("service.button-book", "{n}개 예약", ("n", parts))),
                new ShopActionPresentation("count", WaitingRoomText.Format("service.button-count", "수량 {n}/{max}", ("n", parts), ("max", max)), max > 1)));
        }
        void Request(ShopActionRequest request)
        {
            if (phone == null || !phone.IsOpen || pending != null) { view.RejectAction(request, WaitingRoomText.Get("shopui.reopen-window", "창을 다시 열어 주세요.")); return; }
            if (request.EntryId == "medical" && request.ActionId == "count")
            {
                // 수량 변경은 서버 요청 없이 이 화면에서 1 → 최대 → 1 순서로 돈다.
                int max = Mathf.Max(1, ExpertService.LocalMaxParts);
                medicalParts = medicalParts % max + 1;
                view.AcceptAction(request, WaitingRoomText.Format("service.count-selected", "{n}개 선택", ("n", medicalParts)), Build()); fingerprint = null; nextRefresh = 0; ApplyFonts();
                return;
            }
            pending = request; requestId = Guid.NewGuid().ToString("N"); sentAt = Time.unscaledTime;
            if (request.EntryId.StartsWith("item:") && request.ActionId == "buy" && shop != null)
                shop.RequestPurchaseTracked(request.EntryId.Substring(5), requestId);
            else if (request.EntryId == "medical" && request.ActionId == "medical" && service != null)
                service.RequestMedicalTracked(Mathf.Clamp(medicalParts, 1, Mathf.Max(1, ExpertService.LocalMaxParts)), requestId);
            else Answer(requestId, false, WaitingRoomText.Get("shopui.invalid-request", "사용할 수 없는 요청입니다."));
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
