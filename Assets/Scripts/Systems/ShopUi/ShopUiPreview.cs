using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.ShopV1
{
    /// <summary>W2 확인 씬 전용 표시 자료와 요청 응답 예시. 자금·재고·배송·실제 거래는 계산하지 않는다.</summary>
    public sealed class ShopUiPreview : MonoBehaviour
    {
        [SerializeField] ShopPanelView shop;
        [SerializeField] TMP_Text status;
        [SerializeField] Camera previewCamera;
        [SerializeField] UnityEngine.UI.Button[] controls;
        ShopPanelView subscribedView;
        ShopActionRequest pending, previous;
        float originalScale;
        bool originalBackground, captured, paused, light, disabledRows;
        Color originalCameraColor;
        string result = "데모 열기를 명시적으로 눌러 시작하세요";
        int revision;
        public int ActionCount { get; private set; }
        public int InvocationCount => ActionCount;
        public int RequestCount { get; private set; }
        public string LastAction { get; private set; } = "";
        public bool IsPaused => paused;
        public ShopActionRequest CapturedRequest => pending;
        public const string Instructions = "W2 표시·요청 응답 예시 / 실제 거래·경제 계산·저장 없음\n" +
            "Play 시작 Hidden · 데모를 연 뒤 행/탭/버튼을 마우스·키보드로 확인하세요";
        public const string Caption = "패널 바깥 HUD 영역은 가리지 않습니다 · 1920×1080 / 1920×1200\n" +
            "요청 중 탭·행·닫기 허용 · 성공/거절은 응답만 전달 · 닫기는 이미 보낸 거래 취소가 아닙니다";
        public const string RequiredText = Instructions + Caption +
            "데모 열기를 명시적으로 눌러 시작하세요 예시 요청 수신 응답 보류 성공 응답 거절 응답 이전 응답 무시 확인 " +
            "열린 같은 세션 표시 자료 갱신 실패 기물 서비스 장부 전체 비움 긴 목록 긴 설명 거래 불가 긴 가격 " +
            "요청 없음 또는 이전 토큰 실제 거래 처리 없음 복구 완료 Hidden busy 선택 timeScale runInBackground " +
            "행 비활성 활성 실제 구매 판매 경제 계산 없음 예시 목록 갱신 항목 제거 검증용 요청 거절 0123456789.-/ :·…−+<>";

        /// <summary>확인 씬 참조만 연결한다. 창을 자동으로 열거나 전역 실행 설정을 바꾸지 않는다.</summary>
        public void Configure(ShopPanelView view, TMP_Text label, Camera camera, UnityEngine.UI.Button[] buttons)
        {
            Unbind(); RestoreGlobals(); shop = view; status = label; previewCamera = camera; controls = buttons;
            if (Application.isPlaying && isActiveAndEnabled) { CaptureGlobals(); Bind(); }
            UpdateStatus();
        }
        void OnEnable() { if (Application.isPlaying) { CaptureGlobals(); Bind(); } }
        void OnDisable() { Unbind(); RestoreGlobals(); }
        void OnDestroy() { Unbind(); RestoreGlobals(); }
        void OnApplicationQuit() { RestoreGlobals(); }
        void Update() { if (Application.isPlaying) UpdateStatus(); }
        void CaptureGlobals()
        {
            if (captured) return;
            originalScale = Time.timeScale; originalBackground = Application.runInBackground;
            originalCameraColor = previewCamera == null ? Color.black : previewCamera.backgroundColor;
            captured = true;
        }
        void RestoreGlobals()
        {
            if (!captured) return;
            Time.timeScale = originalScale; Application.runInBackground = originalBackground;
            if (previewCamera != null) previewCamera.backgroundColor = originalCameraColor;
            captured = false; paused = light = false;
        }
        void Bind()
        {
            if (subscribedView == shop) return;
            Unbind(); subscribedView = shop;
            if (subscribedView != null) subscribedView.ActionRequested += OnRequested;
        }
        void Unbind()
        {
            if (subscribedView != null) subscribedView.ActionRequested -= OnRequested;
            subscribedView = null;
        }
        void OnRequested(ShopActionRequest request)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || request == null) return;
            if (pending != null) previous = pending;
            pending = request; RequestCount++;
            result = "예시 요청 수신 · 응답 보류 · 실제 거래 처리 없음"; UpdateStatus();
        }
        void Run(int index, Action action, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
        {
            if (!Application.isPlaying || !isActiveAndEnabled || shop == null) return;
            if (controls != null && controls.Length > 0 && (index < 0 || index >= controls.Length ||
                controls[index] == null || !controls[index].IsActive() || !controls[index].IsInteractable())) return;
            CaptureGlobals();
            try { action(); ActionCount++; LastAction = name; UpdateStatus(); }
            catch { RestoreGlobals(); throw; }
        }
        void NewPresentation(ShopPresentation value)
        {
            if (pending != null) previous = pending;
            pending = null; disabledRows = false; shop.Open(value);
            result = "예시 표시 자료 · 실제 구매·판매·경제 계산 없음";
        }
        public void OpenDemo() => Run(0, () => NewPresentation(ShopDemoData.Create()));
        public void Items() => Run(1, () => shop.SelectTab(ShopTab.Items));
        public void Services() => Run(2, () => shop.SelectTab(ShopTab.Services));
        public void Ledger() => Run(3, () => shop.SelectTab(ShopTab.Ledger));
        public void Empty() => Run(4, () => NewPresentation(CreateSample("empty")));
        public void LongList() => Run(5, () => NewPresentation(CreateSample("long")));
        public void LongText() => Run(6, () => NewPresentation(CreateSample("text")));
        public void Unavailable() => Run(7, () => { shop.SelectTab(ShopTab.Items); shop.SelectEntry("backpack"); });
        public void Buy() => Run(8, () => shop.RequestAction("buy"));
        public void Sell() => Run(9, () => shop.RequestAction("sell"));
        /// <summary>캡처한 요청에 성공만 회신한다. 금액·수량·거래 기록은 바꾸지 않는다.</summary>
        public void AcceptResponse() => Run(10, () =>
        {
            bool consumed = pending != null && shop.AcceptAction(pending, "예시 성공 응답 · 실제 거래 처리 없음");
            result = consumed ? "성공 응답" : "요청 없음 또는 이전 토큰";
            if (consumed) { previous = pending; pending = null; }
        });
        public void RejectResponse() => Run(11, () =>
        {
            bool consumed = pending != null && shop.RejectAction(pending, "검증용 요청 거절 · 실제 거래 처리 없음");
            result = consumed ? "거절 응답" : "요청 없음 또는 이전 토큰";
            if (consumed) { previous = pending; pending = null; }
        });
        public void DeferResponse() => Run(12, () => result = shop.IsBusy ? "응답 보류" : "요청 없음 또는 이전 토큰");
        public void ReplayPreviousResponse() => Run(13, () =>
        {
            bool consumed = previous != null && shop.AcceptAction(previous, "이전 응답");
            result = consumed ? "이전 응답 수락 · 검증 필요" : "이전 응답 무시 확인";
        });
        public void RefreshPresentation() => Run(14, () =>
        {
            var demo = ShopDemoData.Create();
            result = shop.SetPresentation(CopyPresentation(demo, "preview-" + ++revision)) ?
                "열린 같은 세션 표시 자료 갱신" : "표시 자료 갱신 실패";
        });
        public void TogglePause() => Run(15, () => { paused = !paused; Time.timeScale = paused ? 0 : originalScale; });
        public void ToggleBackground() => Run(16, () =>
        {
            light = !light;
            if (previewCamera != null) previewCamera.backgroundColor = light ? new Color32(224, 226, 218, 255) : originalCameraColor;
        });
        public void ToggleRunInBackground() => Run(17, () => Application.runInBackground = !Application.runInBackground);
        public void Close() => Run(18, () => { if (pending != null) previous = pending; pending = null; shop.Close(); });
        public void ResetPreview() => Run(19, () =>
        {
            if (pending != null) previous = pending;
            pending = null; shop.Hide(); RestoreGlobals(); CaptureGlobals(); disabledRows = false;
            result = "복구 완료 · Hidden";
        });
        public void ToggleRowsDisabled() => Run(20, () =>
        {
            disabledRows = !disabledRows;
            foreach (var row in shop.GetComponentsInChildren<ShopRowVisual>(true))
                if (row.NativeButton != null) { row.NativeButton.interactable = !disabledRows; row.RefreshVisual(true); }
            result = disabledRows ? "행 비활성" : "행 활성";
        });
        public void DisabledAction() => Run(21, () => NewPresentation(CreateSample("disabled")));
        public void Reopen() => Run(22, () => { shop.Hide(); NewPresentation(ShopDemoData.Create()); });
        public void RemoveSelected() => Run(23, () =>
        {
            var source = ShopDemoData.Create(); var entries = new List<ShopEntryPresentation>();
            foreach (var entry in source.Entries) if (entry.Id != shop.SelectedEntryId) entries.Add(entry);
            result = shop.SetPresentation(CopyPresentation(source, "preview-" + ++revision, entries.ToArray())) ?
                "예시 목록 갱신 · 항목 제거" : "표시 자료 갱신 실패";
        });
        /// <summary>긴 목록·빈 화면 등 표시 경계만 만든다. 가격은 불투명한 예시 문자열이다.</summary>
        public static ShopPresentation CreateSample(string kind)
        {
            var source = ShopDemoData.Create(); var entries = new List<ShopEntryPresentation>();
            if (kind != "empty")
            {
                int count = kind == "long" ? 12 : 1;
                for (int i = 0; i < count; i++)
                {
                    string text = kind == "text" ? "<b>" + new string('가', 80) + "</b>" : "예시 기물 " + i;
                    entries.Add(new ShopEntryPresentation("sample-" + i, ShopTab.Items, "bandage", text,
                        "기물 설명 보기", kind == "text" ? "12345678901234567890" : "60", text + "\n" + text,
                        "예시 구매 60 · 판매 30", true, "", "배송 안내", "delivery",
                        new ShopActionPresentation("buy", "구매", kind != "disabled"), new ShopActionPresentation("sell", "판매")));
                }
            }
            return CopyPresentation(source, "sample-" + kind, entries.ToArray());
        }
        static ShopPresentation CopyPresentation(ShopPresentation source, string revision, ShopEntryPresentation[] entries = null)
        {
            if (entries == null)
            {
                entries = new ShopEntryPresentation[source.Entries.Count];
                for (int i = 0; i < entries.Length; i++) entries[i] = source.Entries[i];
            }
            return new ShopPresentation(source.SessionId, revision, source.Title, ShopTab.Items, "bandage",
                source.GetContext(ShopTab.Items), source.GetContext(ShopTab.Services), source.GetContext(ShopTab.Ledger), entries);
        }
        void UpdateStatus()
        {
            if (status == null) return;
            status.text = Instructions + "\n" + (shop == null || !shop.IsVisible ? "Hidden" :
                shop.CurrentTab + " / " + shop.SelectedEntryId + " / busy " + shop.IsBusy) +
                " / timeScale " + Time.timeScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
                " / runInBackground " + Application.runInBackground + " · " + result;
        }
    }
}
