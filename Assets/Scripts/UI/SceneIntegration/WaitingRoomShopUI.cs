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
                phone = room.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopPhone>()).FirstOrDefault();
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
                Answer(requestId, false, "응답 미확인 · 재구매 전 장부와 배송구를 확인하세요.");
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
                    string reason = !WaitingRoomShop.CanPurchase ? WaitingRoomShop.PurchaseTimeRefusal : shop.Balance < item.Price ? "공동 자금 부족" : "";
                    string icon = view.ResolveIcon(item.Id) != null ? item.Id : "delivery";
                    entries.Add(new ShopEntryPresentation("item:" + item.Id, ShopTab.Items, icon, item.Name, "구매 후 배송구에서 수령", item.Price.ToString(),
                        "공동 자금으로 주문합니다. 배송구에서 직접 수령하세요.",
                        "구매 " + item.Price, reason.Length == 0, reason, "배송구 대기 " + shop.Waiting.Count + "개", "delivery", new ShopActionPresentation("buy", "구매")));
                }
            }
            AddServices(entries);
            var state = PerformanceRewards.Instance != null ? PerformanceRewards.Instance.State : null;
            if (state != null)
                foreach (var entry in state.Ledger.Reverse())
                    entries.Add(new ShopEntryPresentation("fund:" + entry.Number, ShopTab.Ledger, "currency", entry.Description,
                        $"#{entry.Number} · {entry.Day}일차", entry.Amount.ToString("+#;-#;0"), entry.Description,
                        $"잔액 {entry.Balance - entry.Amount} → {entry.Balance}", true, "", "정산 완료", "currency"));
            if (shop != null)
            {
                foreach (var d in shop.Waiting.Reverse())
                    entries.Add(new ShopEntryPresentation("delivery:" + d.Order, ShopTab.Ledger, "delivery", d.ItemName,
                        $"주문 #{d.Order} · {WaitingRoomShop.PlayerLabel(d.Buyer)}", "수령 대기", "배송구에 도착했습니다. 가까이서 상호작용하여 수령하세요.", "", true));
                foreach (var r in shop.Receipts.Reverse())
                    entries.Add(new ShopEntryPresentation("receipt:" + r.Delivery.Order, ShopTab.Ledger, "delivery", r.Delivery.ItemName,
                        $"수령 #{r.Delivery.Order}", "수령 완료", $"{WaitingRoomShop.PlayerLabel(r.Delivery.Buyer)} 주문 → {WaitingRoomShop.PlayerLabel(r.Claimer)} 수령", "", true));
            }
            return new ShopPresentation(session ?? "unopened", (++revision).ToString(), "상점 수화기", ShopTab.Items, "",
                $"공동 자금 {shop?.Balance ?? 0} · 배송구 대기 {shop?.Waiting.Count ?? 0}개 · {(WaitingRoomShop.CanPurchase ? "밤 정비 · 구매 가능" : "기물 구매는 밤 정비에만 가능")}",
                $"공동 자금 {shop?.Balance ?? 0} · {ExpertService.Day}일차 · {(ExpertService.IsNight ? "밤 정비" : "밤 정비 때 이용 가능")}", "최근 공동 자금 장부 · 배송 및 수령 기록", entries.ToArray());
        }
        void AddServices(List<ShopEntryPresentation> entries)
        {
            var medical = service != null ? service.LocalMedicalRefusal() : ExpertRefusal.NotNight;
            var facility = service != null ? service.FacilityRefusal() : ExpertRefusal.NotNight;
            string medicalReason = service == null ? "서비스 정보를 불러오는 중입니다." : medical != ExpertRefusal.None ? ExpertServiceBook.Explain(medical) : service.Balance < service.LocalMedicalCost ? "공동 자금 부족" : "";
            string facilityReason = service == null ? "서비스 정보를 불러오는 중입니다." : facility != ExpertRefusal.None ? ExpertServiceBook.Explain(facility) : service.Balance < service.FacilityPrice ? "공동 자금 부족" : "";
            string status = service == null ? "" : service.Phase != ExpertRoomPhase.Idle ? $"{WaitingRoomShop.PlayerLabel(service.Buyer)} · {ExpertServicePhonePanel.PhaseLabel(service.Phase)}" : "";
            entries.Add(new ShopEntryPresentation("medical", ShopTab.Services, "medical", "의료 서비스", $"신체 복구 {ExpertService.LocalLostLimbs}개",
                service?.LocalMedicalCost.ToString() ?? "—", "예약 후 전문가실에서\n잃은 신체 부위를 복구합니다.",
                service == null ? "" : $"부위당 {service.MedicalPrice} · 합계 {service.LocalMedicalCost}", medicalReason.Length == 0, medicalReason, status, "medical", new ShopActionPresentation("medical", "예약")));
            entries.Add(new ShopEntryPresentation("facility", ShopTab.Services, "tools", "시설 서비스", "다음 날 장애물 조정", service?.FacilityPrice.ToString() ?? "—",
                "강화: 보상 증가 / 약화: 보상 감소\n하루 한 번, 다음 날 적용", service == null ? "" : "비용 " + service.FacilityPrice,
                facilityReason.Length == 0, facilityReason, service != null && service.ObstacleDay > 0 ? $"{service.ObstacleDay}일차 · {ExpertServiceBook.AdjustLabel(service.Obstacle)}" : "", "tools",
                new ShopActionPresentation("strengthen", "강화"), new ShopActionPresentation("weaken", "약화")));
        }
        void Request(ShopActionRequest request)
        {
            if (phone == null || !phone.IsOpen || pending != null) { view.RejectAction(request, "창을 다시 열어 주세요."); return; }
            pending = request; requestId = Guid.NewGuid().ToString("N"); sentAt = Time.unscaledTime;
            if (request.EntryId.StartsWith("item:") && request.ActionId == "buy" && shop != null)
                shop.RequestPurchaseTracked(request.EntryId.Substring(5), requestId);
            else if (request.EntryId == "medical" && request.ActionId == "medical" && service != null) service.RequestMedicalTracked(requestId);
            else if (request.EntryId == "facility" && service != null && (request.ActionId == "strengthen" || request.ActionId == "weaken"))
                service.RequestFacilityTracked(request.ActionId == "strengthen" ? ObstacleAdjust.Strengthen : ObstacleAdjust.Weaken, requestId);
            else Answer(requestId, false, "사용할 수 없는 요청입니다.");
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
