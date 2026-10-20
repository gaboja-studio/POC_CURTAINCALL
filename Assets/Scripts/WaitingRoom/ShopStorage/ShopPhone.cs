using CurtainCall.Player;
using CurtainCall.Reward;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ③ 상점 벽면 수화기. 가까이서 F를 누르면 상점을 열고, ESC 또는 멀어지면 닫는다.
    /// 창의 [장부 보기]로 공동 자금 사용 내역을 열어 본다(평소 화면에는 잔액만, 게임 흐름 5-2).
    /// 창이 열린 동안 게임 입력을 막는다(커서도 풀린다). 표시 어댑터가 없는 테스트 씬만 IMGUI를 사용한다.
    /// </summary>
    public sealed class ShopPhone : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [Tooltip("수화기에 닿는 수평 거리(m).")]
        [SerializeField, Min(.5f)] float reach = 1.8f;
        [SerializeField] bool showLegacyUi = true;
        public bool IsOpen => open;

        UIInputBlocker blocker;
        bool open;
        bool ledger;
        Vector2 scroll;
        public Transform VisualRoot => transform;
        public Vector3 PresentationPoint => transform.position;
        public string ActionLabel => "수화기 들기 (상점)";
        public bool IsAvailable => !open && WaitingRoomShop.Instance != null;

        void Awake()
        {
            blocker = gameObject.AddComponent<UIInputBlocker>();
            blocker.BlockWhileActive = false;
        }

        void OnEnable() => ShopReach.Add(this);

        void OnDisable()
        {
            ShopReach.Remove(this);
            SetOpen(false);
        }

        void Update()
        {
            ShopReach.Pump();
            if (!open) return;
            var keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            // 창을 연 채로 밀려나거나 순간이동(공연 전환)하면 닫는다.
            bool away = !ShopReach.TryLocalFeet(out var feet) || Flat(feet - transform.position) > reach + .5f;
            if (escape || away || WaitingRoomShop.Instance == null) SetOpen(false);
        }

        public float Reach(Vector3 feet)
        {
            float distance = Flat(feet - transform.position);
            return distance <= reach ? distance : -1f;
        }

        public void Interact()
        {
            if (WaitingRoomShop.Instance != null) SetOpen(true);
        }

        public void SetOpen(bool value)
        {
            open = value;
            if (blocker != null) blocker.SetOpen(value);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void OnGUI()
        {
            if (!showLegacyUi) return;
            var shop = WaitingRoomShop.Instance;
            if (shop == null) return;
            if (!open)
            {
                if (!ShopReach.HasExternalPresentation && ReferenceEquals(ShopReach.Focused, this))
                    GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height * .62f, 240, 24), "[F] 수화기 들기 (상점)");
                return;
            }

            GUI.depth = -100; // 창은 테스트 패널(IMGUI)보다 위에 그리고 클릭도 먼저 받는다
            const float width = 380, height = 330;
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            var tint = GUI.color;
            GUI.color = new Color(.12f, .12f, .14f, 1f); // 뒤 패널 글자가 비치지 않게 불투명 배경
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = tint;
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(ledger ? "③ 상점 — 장부(사용 내역)" : "③ 상점 — 수화기 (임시 품목·가격)");
            if (GUILayout.Button(ledger ? "품목 보기" : "장부 보기", GUILayout.Width(90))) { ledger = !ledger; scroll = Vector2.zero; }
            GUILayout.EndHorizontal();
            GUILayout.Label($"공동 자금 {shop.Balance}");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(170));
            if (ledger) DrawLedger(shop);
            else
            {
                foreach (var item in shop.Catalog)
                {
                    GUI.enabled = item.Price <= shop.Balance;
                    if (GUILayout.Button($"{item.Name} — {item.Price}")) shop.RequestPurchase(item.Id);
                }
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
            GUILayout.Label($"배송구 대기 {shop.Waiting.Count}개");
            if (!string.IsNullOrEmpty(shop.LastMessage)) GUILayout.Label(shop.LastMessage);
            if (GUILayout.Button("닫기 (ESC)")) SetOpen(false);
            GUILayout.EndArea();
        }

        // 공동 자금 장부(최근 20줄, 최신이 위)와 배송구 수령 기록. 게임 흐름 5-2: 장부는 나중에 열어 본다.
        static void DrawLedger(WaitingRoomShop shop)
        {
            var state = PerformanceRewards.Instance != null ? PerformanceRewards.Instance.State : null;
            if (state == null || state.Ledger.Count == 0) GUILayout.Label("장부가 비어 있습니다.");
            else
                for (int i = state.Ledger.Count - 1; i >= 0; i--)
                {
                    var entry = state.Ledger[i];
                    GUILayout.Label($"#{entry.Number} {entry.Day}일차 {entry.Amount:+#;-#;0} → {entry.Balance}  {entry.Description}");
                }
            if (shop.Receipts.Count == 0) return;
            GUILayout.Label("— 배송구 수령 기록 —");
            for (int i = shop.Receipts.Count - 1; i >= 0; i--)
            {
                var receipt = shop.Receipts[i];
                GUILayout.Label($"#{receipt.Delivery.Order} {receipt.Delivery.ItemName}: "
                    + $"{WaitingRoomShop.PlayerLabel(receipt.Delivery.Buyer)} 주문 → {WaitingRoomShop.PlayerLabel(receipt.Claimer)} 수령");
            }
        }
    }
}
