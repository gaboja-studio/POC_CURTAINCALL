using CurtainCall.UI.BackstageHudV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.BackstageHud
{
    public sealed class BackstageHudPreview : MonoBehaviour
    {
        [SerializeField] BackstageHUDView backstage;
        [SerializeField] LobbyRoomInfoView lobby;
        [SerializeField] InventorySlotsView standalone;
        [SerializeField] TMP_Text status;
        [SerializeField] Sprite[] items;
        public BackstageHUDView Backstage => backstage;
        public LobbyRoomInfoView Lobby => lobby;
        public InventorySlotsView Standalone => standalone;
        public int Sample { get; private set; }
        public int CopyCount { get; private set; }
        public string LastCopiedCode { get; private set; }

        public void Configure(BackstageHUDView hud, LobbyRoomInfoView room, InventorySlotsView slots, TMP_Text text, Sprite[] sprites)
        {
            backstage = hud; lobby = room; standalone = slots; status = text; items = sprites;
        }

        void OnEnable()
        {
            if (lobby != null) { lobby.CopyRequested -= OnCopy; lobby.CopyRequested += OnCopy; }
        }
        void OnDisable() { if (lobby != null) lobby.CopyRequested -= OnCopy; }
        void Start() { ResetPreview(); }
        void OnCopy(string code)
        {
            CopyCount++; LastCopiedCode = code;
            status.text = "합성 로비 · 복사 요청 " + CopyCount + "회 · 클립보드 쓰기 없음";
        }

        public void Normal() => ShowSample(0);
        public void Paused() => ShowSample(1);
        public void Alarm() => ShowSample(2);
        public void Unavailable() => ShowSample(3);
        public void Inventory() => ShowSample(4);
        public void Priority() => ShowSample(5);
        public void LobbySample() => ShowSample(6);
        public void LongValues() => ShowSample(Sample == 7 ? 10 : 7);
        public void Disabled() => ShowSample(8);
        public void SlotTemplate() => ShowSample(9);
        public void ResetPreview()
        {
            CopyCount = 0; LastCopiedCode = null;
            ShowSample(0);
        }

        public void ShowSample(int sample)
        {
            if (backstage == null || lobby == null || standalone == null || items == null || items.Length != 4) return;
            Sample = sample;
            // 하위 Awake가 먼저 끝난 다음 표시 API에 데이터를 공급합니다.
            backstage.gameObject.SetActive(true);
            lobby.transform.root.gameObject.SetActive(true);
            standalone.transform.root.gameObject.SetActive(true);
            backstage.ResetForRuntime(); lobby.ResetForRuntime(); standalone.ResetForRuntime();
            bool room = sample == 6 || sample == 7 || sample == 8;
            backstage.SetContext(!room && sample != 9, true, true, sample != 5);
            lobby.SetContext(room);
            standalone.SetContext(sample == 9);
            if (sample == 9)
            {
                standalone.SetSlots(Slots(1)); standalone.ShowInventory();
            }
            else if (room)
            {
                lobby.SetRoomInfo(sample == 7 ? "DEMO42LONGROOMCODE" : "DEMO42",
                    sample == 7 ? "아주긴방장이름가나다라마바사" : "단장", 3, 4);
                lobby.SetControlsEnabled(sample != 8);
            }
            else if (sample != 3)
            {
                backstage.Clock.SetClock(sample == 2 ? 9 : 134, sample == 2 ? .06f : .72f,
                    sample == 1 ? BackstageClockState.Paused : sample == 2 ? BackstageClockState.Alarm : BackstageClockState.Normal);
                backstage.Funds.SetAmount(1280);
                backstage.Funds.SetChangeAccent(sample == 1 ? FundsChangeAccent.Decrease : sample == 2 ? FundsChangeAccent.Increase : FundsChangeAccent.None);
                backstage.Gather.SetCounts(3, 4);
                backstage.Inventory.SetSlots(Slots(sample == 4 ? 6 : 4));
                backstage.Inventory.ShowInventory();
            }
            status.text = "합성 데이터 · 게임 연결 없음 · " + new[] { "평소", "멈춤/자금 감소", "소집/자금 증가", "미수신", "가변 슬롯/빈칸", "상위 하단 UI 우선", "로비 가림", "로비 긴 값", "로비 비활성", "슬롯 컨테이너", "자금 긴 값" }[Mathf.Clamp(sample, 0, 10)];
            if (sample == 10) backstage.Funds.SetAmount(long.MaxValue);
        }

        public InventorySlotData[] Slots(int count)
        {
            var values = new InventorySlotData[count];
            for (int i = 0; i < count; i++) values[i] = new InventorySlotData
            {
                IsEmpty = count > 4 && i == 4, ItemSprite = items[i % items.Length], IsHeld = i == 1,
                KeyLabel = (i + 1).ToString(), QuantityLabel = (i == 0 ? 2 : i == 2 ? 3 : 1).ToString()
            };
            return values;
        }
    }
}
