using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ③ 상점 벽면 수화기. 가까이서 F를 누르면 임시 품목 목록 창이 열리고, 골라서 주문한다. ESC 또는 멀어지면 닫힌다.
    /// 창이 열린 동안 게임 입력을 막는다(커서도 풀린다). 테스트용 IMGUI 창이다.
    /// </summary>
    public sealed class ShopPhone : MonoBehaviour, IShopTarget
    {
        [Tooltip("수화기에 닿는 수평 거리(m).")]
        [SerializeField, Min(.5f)] float reach = 1.8f;

        UIInputBlocker blocker;
        bool open;
        Vector2 scroll;

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

        void SetOpen(bool value)
        {
            open = value;
            if (blocker != null) blocker.SetOpen(value);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void OnGUI()
        {
            var shop = WaitingRoomShop.Instance;
            if (shop == null) return;
            if (!open)
            {
                if (ReferenceEquals(ShopReach.Focused, this))
                    GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height * .62f, 240, 24), "[F] 수화기 들기 (상점)");
                return;
            }

            const float width = 340, height = 300;
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height), GUI.skin.box);
            GUILayout.Label("③ 상점 — 수화기 (임시 품목·가격)");
            GUILayout.Label($"공동 자금 {shop.Balance}");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(150));
            foreach (var item in shop.Catalog)
            {
                GUI.enabled = item.Price <= shop.Balance;
                if (GUILayout.Button($"{item.Name} — {item.Price}")) shop.RequestPurchase(item.Id);
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.Label($"배송구 대기 {shop.Waiting.Count}개");
            if (!string.IsNullOrEmpty(shop.LastMessage)) GUILayout.Label(shop.LastMessage);
            if (GUILayout.Button("닫기 (ESC)")) SetOpen(false);
            GUILayout.EndArea();
        }
    }
}
