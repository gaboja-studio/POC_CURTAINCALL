using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ③ 상점 수화기 아래 배송구. 주문한 물건이 앞바닥에 도형으로 놓이고, 가까이서 F를 누르면 가장 가까운 물건을 받는다.
    /// 놓이는 자리는 주문 순서로만 정해서 네 화면이 같다. 도형은 화면마다 따로 그리는 표시용이다(충돌 없음).
    /// 받은 물건을 들고 다니는 운반은 다음 항목(운반 한도)에서 붙인다.
    /// </summary>
    public sealed class ShopDeliverySlot : MonoBehaviour
    {
        [Tooltip("물건에 닿는 수평 거리(m).")]
        [SerializeField, Min(.3f)] float reach = 1.2f;

        [Tooltip("배송구 로컬 +Z 기준으로 물건이 떨어지는 거리(m). 방 안쪽이 -Z면 음수.")]
        [SerializeField] float dropForward = .6f;

        [Tooltip("한 줄에 놓는 개수와 간격(m).")]
        [SerializeField, Min(1)] int perRow = 4;
        [SerializeField, Min(.1f)] float spacing = .35f;
        [SerializeField, Min(.05f)] float itemSize = .25f;

        readonly Dictionary<int, Item> shown = new();
        readonly List<int> stale = new();
        WaitingRoomShop shop;
        Material material;

        sealed class Item : IShopTarget
        {
            public ShopDeliverySlot Slot;
            public ShopDelivery Delivery;
            public GameObject View;

            public float Reach(Vector3 feet)
            {
                Vector3 offset = feet - View.transform.position;
                float distance = new Vector2(offset.x, offset.z).magnitude;
                return distance <= Slot.reach ? distance : -1f;
            }

            public void Interact()
            {
                if (Slot.shop != null) Slot.shop.RequestClaim(Delivery.Order);
            }
        }

        void OnDisable()
        {
            if (shop != null) shop.Changed -= Rebuild;
            shop = null;
            foreach (var item in shown.Values) Drop(item);
            shown.Clear();
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        void Update()
        {
            if (shop == null && WaitingRoomShop.Instance != null)
            {
                shop = WaitingRoomShop.Instance;
                shop.Changed += Rebuild;
                Rebuild();
            }
            ShopReach.Pump();
        }

        /// <summary>주문 순서 i번째 물건이 놓이는 자리(바닥 위).</summary>
        public Vector3 DropPosition(int index)
        {
            int row = index / perRow, column = index % perRow;
            float x = (column - (perRow - 1) * .5f) * spacing;
            float z = dropForward + Mathf.Sign(dropForward) * row * spacing;
            // 배송구 도형의 크기(scale)와 상관없이 m 단위로 놓는다.
            Vector3 flat = transform.rotation * new Vector3(x, 0f, z);
            Vector3 point = transform.position + new Vector3(flat.x, 0f, flat.z);
            point.y = FloorHeight(point) + itemSize * .5f;
            return point;
        }

        // 배송구는 벽에 떠 있으므로 아래로 바닥을 찾는다. 못 찾으면 배송구 높이.
        float FloorHeight(Vector3 point) =>
            Physics.Raycast(point + Vector3.up * .5f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y : transform.position.y;

        void Rebuild()
        {
            if (shop == null) return;
            var waiting = shop.Waiting;

            stale.Clear();
            foreach (int order in shown.Keys) stale.Add(order);
            for (int i = 0; i < waiting.Count; i++)
            {
                var delivery = waiting[i];
                stale.Remove(delivery.Order);
                if (!shown.TryGetValue(delivery.Order, out var item))
                {
                    item = new Item { Slot = this, Delivery = delivery, View = MakeView(delivery) };
                    shown.Add(delivery.Order, item);
                    ShopReach.Add(item);
                }
                item.View.transform.SetPositionAndRotation(DropPosition(i), Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            }
            foreach (int order in stale)
            {
                Drop(shown[order]);
                shown.Remove(order);
            }
        }

        GameObject MakeView(ShopDelivery delivery)
        {
            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.name = $"Delivery_{delivery.Order}_{delivery.ItemId}";
            Destroy(view.GetComponent<Collider>());
            view.hideFlags = HideFlags.DontSave;
            view.transform.localScale = Vector3.one * itemSize; // 배송구 크기에 묶이지 않게 부모 없이 둔다
            var renderer = view.GetComponent<Renderer>();
            if (material == null)
            {
                var color = new Color(1f, .78f, .25f);
                material = new Material(renderer.sharedMaterial) { color = color };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); // URP Lit
            }
            renderer.sharedMaterial = material;
            return view;
        }

        static void Drop(Item item)
        {
            ShopReach.Remove(item);
            if (item.View != null) Destroy(item.View);
        }

        void OnGUI()
        {
            if (shop == null) return;
            foreach (var item in shown.Values)
            {
                if (!ReferenceEquals(ShopReach.Focused, item)) continue;
                string buyer = WaitingRoomShop.PlayerLabel(item.Delivery.Buyer);
                GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height * .62f, 320, 24),
                    $"[F] {item.Delivery.ItemName} 받기 (#{item.Delivery.Order}, {buyer} 주문)");
                return;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .78f, .25f, .6f);
            for (int i = 0; i < perRow * 2; i++) Gizmos.DrawWireCube(DropPosition(i), Vector3.one * itemSize);
        }
    }
}
