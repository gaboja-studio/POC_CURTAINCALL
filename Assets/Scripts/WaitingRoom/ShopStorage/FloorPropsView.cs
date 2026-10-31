using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 바닥에 내려놓은 기물(<see cref="PropCarry.Floor"/>)을 도형으로 그리고, 가까이서 F를 누르면 줍는다.
    /// 위치는 판정 화면이 정한 값을 그대로 써서 네 화면이 같다. 도형은 화면마다 따로 그리는 표시용이다(충돌 없음).
    /// 여러 개가 같은 자리에 떨어지면 조금씩 옆으로 비켜 그린다.
    /// </summary>
    public sealed class FloorPropsView : MonoBehaviour
    {
        [Tooltip("기물에 닿는 수평 거리(m).")]
        [SerializeField, Min(.3f)] float reach = 1f;
        [SerializeField, Min(.05f)] float itemSize = .25f;
        [SerializeField] Color color = new(.55f, .85f, 1f);
        [SerializeField] bool showLegacyLabels = true;

        public void CollectLabels(List<ShopWorldLabel> labels)
        {
            foreach (var item in shown.Values)
                if (item.Shape != null) labels.Add(new ShopWorldLabel(item.Shape.transform, item.Prop.Item.Name, Vector3.up * (itemSize + .12f)));
        }

        readonly Dictionary<int, Item> shown = new();
        readonly List<int> stale = new();
        PropCarry carry;
        Material material;
        static int activeCount;
        static readonly List<FloorPropsView> views = new();
        public static bool CanPickUp(int uid, Vector3 feet)
        {
            foreach (var view in views)
                if (view != null && view.shown.TryGetValue(uid, out var item) && item.Reach(feet) >= 0f) return true;
            return false;
        }

        /// <summary>대기실 씬의 기물 표시가 켜져 있다(소지 HUD·한도 초과 떨어뜨리기를 대기실에서만 하게 한다).</summary>
        public static bool Active => activeCount > 0;

        void OnEnable() { activeCount++; if (!views.Contains(this)) views.Add(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { activeCount = 0; views.Clear(); }

        sealed class Item : IShopTarget, IShopPresentation
        {
            public FloorPropsView View;
            public FloorProp Prop;
            public GameObject Shape;
            public Transform VisualRoot => Shape != null ? Shape.transform : null;
            public Vector3 PresentationPoint => Shape != null ? Shape.transform.position : Vector3.zero;
            public string ActionLabel => WaitingRoomText.Format("prompt.pick-up-floor-item", "{item} 줍기", ("item", Prop.Item.Name));
            public bool IsAvailable => Shape != null && View.carry != null;

            public float Reach(Vector3 feet)
            {
                return Shape != null ? ExplorationReach.From(Shape.transform, Shape.transform.position, feet, View.reach, insideFurniture: true) : -1f;
            }

            public void Interact()
            {
                if (View.carry != null) View.carry.RequestPickUp(Prop.Item.Uid);
            }
        }

        void OnDisable()
        {
            activeCount--; views.Remove(this);
            if (carry != null) carry.Changed -= Rebuild;
            carry = null;
            foreach (var item in shown.Values) Remove(item);
            shown.Clear();
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        void Update()
        {
            if (carry == null && PropCarry.Instance != null)
            {
                carry = PropCarry.Instance;
                carry.Changed += Rebuild;
                Rebuild();
            }
            ShopReach.Pump();
        }

        void Rebuild()
        {
            if (carry == null) return;
            stale.Clear();
            stale.AddRange(shown.Keys);
            var placed = new List<Vector3>();
            foreach (var prop in carry.Floor)
            {
                stale.Remove(prop.Item.Uid);
                if (!shown.TryGetValue(prop.Item.Uid, out var item))
                {
                    item = new Item { View = this, Prop = prop, Shape = MakeShape(prop) };
                    shown.Add(prop.Item.Uid, item);
                    ShopReach.Add(item);
                }
                Vector3 point = Spread(new Vector3(prop.X, prop.Y + itemSize * .5f, prop.Z), placed);
                placed.Add(point);
                item.Shape.transform.position = point;
            }
            foreach (int uid in stale)
            {
                Remove(shown[uid]);
                shown.Remove(uid);
            }
        }

        // 바닥 순서대로 비켜 놓으므로 모든 화면에서 같은 결과가 나온다.
        Vector3 Spread(Vector3 point, List<Vector3> placed)
        {
            float gap = itemSize * 1.2f;
            for (int ring = 0; ring < 6; ring++)
            {
                for (int step = 0; step < Mathf.Max(1, ring * 6); step++)
                {
                    float angle = ring == 0 ? 0f : step * Mathf.PI * 2f / (ring * 6);
                    Vector3 candidate = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * gap);
                    bool free = true;
                    foreach (var other in placed)
                        if ((other - candidate).sqrMagnitude < gap * gap * .9f) { free = false; break; }
                    if (free) return candidate;
                }
            }
            return point;
        }

        GameObject MakeShape(FloorProp prop)
        {
            var artwork = CurtainCall.ItemArt.ItemVisualAssets.Create(prop.Item.ItemId);
            if (artwork != null)
            {
                artwork.name = $"FloorProp_{prop.Item.Uid}_{prop.Item.ItemId}";
                artwork.hideFlags = HideFlags.DontSave;
                return artwork;
            }
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = $"FloorProp_{prop.Item.Uid}_{prop.Item.ItemId}";
            Destroy(shape.GetComponent<Collider>());
            shape.hideFlags = HideFlags.DontSave;
            shape.transform.localScale = Vector3.one * itemSize;
            var renderer = shape.GetComponent<Renderer>();
            if (material == null)
            {
                material = new Material(renderer.sharedMaterial) { color = color };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); // URP Lit
            }
            renderer.sharedMaterial = material;
            return shape;
        }

        static void Remove(Item item)
        {
            ShopReach.Remove(item);
            if (item.Shape != null) Destroy(item.Shape);
        }

        void OnGUI()
        {
            if (carry == null) return;
            if (showLegacyLabels)
                foreach (var item in shown.Values)
                    PropLabels.Draw(item.Shape.transform.position + Vector3.up * itemSize, item.Prop.Item.Name);
            if (ShopReach.HasExternalPresentation) return;
            foreach (var item in shown.Values)
            {
                if (!ReferenceEquals(ShopReach.Focused, item)) continue;
                GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height * .62f, 320, 24),
                    $"[F] {item.Prop.Item.Name} 줍기 (#{item.Prop.Item.Uid})");
                return;
            }
        }
    }
}
