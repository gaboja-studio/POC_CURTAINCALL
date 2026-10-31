using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ④ 소품실 물려줄 선반(4칸). 선반 앞에서 F: 들고 있고 빈 칸이 있으면 마지막에 든 기물을 아래 칸부터 올리고,
    /// 아니면 위 칸부터 꺼낸다(<see cref="PropCarry.RequestShelf"/>). 여기 올린 기물만 세대 교체 뒤 다음 세대로 넘어간다.
    /// 칸 위치는 자식 판자(이름이 "Board_"로 시작, 이름 순 = 아래부터)를 쓴다. 기물 도형은 화면마다 따로 그리는 표시용이다.
    /// </summary>
    public sealed class HandoverShelfView : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [Tooltip("같은 층에서 가림 없이 선반 앞면에 닿는 수평 거리(m).")]
        [SerializeField, Min(.3f)] float reach = 1.2f;
        [SerializeField, Min(.05f)] float itemSize = .25f;
        [SerializeField] Color color = new(.7f, 1f, .6f);

        [Tooltip("비우면 자식 중 이름이 Board_로 시작하는 판자를 이름 순으로 쓴다(아래 칸부터).")]
        [SerializeField] Transform[] boards = new Transform[0];
        [SerializeField] bool showLegacyLabels = true;
        [SerializeField] Transform visualRoot;
        public IReadOnlyList<Transform> Boards => boards;
        public void CollectLabels(List<ShopWorldLabel> labels)
        {
            if (carry == null) return;
            for (int i = 0; i < shapes.Length && i < carry.Shelf.Count; i++)
                if (shapes[i] != null)
                {
                    var at = shapes[i].transform.position;
                    var offset = FrontOf(at, .07f) - at + Vector3.up * (itemSize + .12f);
                    labels.Add(new ShopWorldLabel(shapes[i].transform, carry.Shelf[i].Name, offset, VisualRoot));
                }
        }

        readonly GameObject[] shapes = new GameObject[ItemCarryBook.ShelfSlots];
        PropCarry carry;
        Material material;
        Bounds area;
        public Transform VisualRoot => visualRoot != null ? visualRoot : transform;

        /// <summary>점을 선반 앞면(VisualRoot 앞쪽) 바로 바깥으로 옮긴다. 선반을 돌려 옮겨도 이름표가 앞면에 붙는다.</summary>
        public Vector3 FrontOf(Vector3 point, float margin)
        {
            var front = VisualRoot.forward;
            if (!VisualRoot.TryGetComponent<Collider>(out var frame)) return point + front * margin;
            var b = frame.bounds;
            float face = Vector3.Dot(b.center, front) + Vector3.Dot(b.extents, new Vector3(Mathf.Abs(front.x), Mathf.Abs(front.y), Mathf.Abs(front.z)));
            return point + front * (face + margin - Vector3.Dot(point, front));
        }

        /// <summary>i번째 칸 이름표 자리: 판자 왼쪽 끝에서 30cm 안쪽, 선반 앞면 5cm 바깥.</summary>
        public bool TrySlotLabelPoint(int i, out Vector3 point)
        {
            point = default;
            if (boards == null || i < 0 || i >= boards.Length || boards[i] == null || !boards[i].TryGetComponent<Renderer>(out var surface)) return false;
            var b = surface.bounds; var right = VisualRoot.right;
            float half = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)));
            point = FrontOf(b.center - right * (half - .3f), .05f);
            return true;
        }
        public Vector3 PresentationPoint => area.center;
        // 문구는 사용자가 고칠 수 있으므로 쓸 수 있는지는 문구가 아니라 상태로 판단한다.
        public bool IsAvailable => Action(out _, out _) != ShelfAction.None;
        public string ActionLabel => Action(out string held, out int empty) switch
        {
            ShelfAction.Put => WaitingRoomText.Format("shelf.put", "{item} 선반 {n}칸에 올리기", ("item", held), ("n", empty + 1)),
            ShelfAction.Take => WaitingRoomText.Get("shelf.take", "선반 위 칸에서 꺼내기"),
            _ => WaitingRoomText.Get("shelf.empty", "선반이 비어 있습니다"),
        };

        enum ShelfAction { None, Put, Take }

        ShelfAction Action(out string heldName, out int empty)
        {
            heldName = ""; empty = -1;
            if (carry == null) return ShelfAction.None;
            bool filled = false;
            for (int i = 0; i < carry.Shelf.Count; i++)
                if (ItemCarryBook.IsEmpty(carry.Shelf[i])) { if (empty < 0) empty = i; } else filled = true;
            var held = carry.Held(carry.LocalClientId);
            if (held.Count > 0 && empty >= 0) { heldName = held[held.Count - 1].Name; return ShelfAction.Put; }
            return filled ? ShelfAction.Take : ShelfAction.None;
        }

        void Awake()
        {
            if (boards == null || boards.Length == 0)
            {
                var found = new List<Transform>();
                foreach (var child in GetComponentsInChildren<Transform>(true))
                    if (child.name.StartsWith("Board_")) found.Add(child);
                found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                boards = found.ToArray();
            }
            if (boards.Length != ItemCarryBook.ShelfSlots)
                Debug.LogWarning($"[Carry] 물려줄 선반 판자가 {boards.Length}개입니다({ItemCarryBook.ShelfSlots}개 필요).", this);

            area = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var board in boards)
            {
                if (board == null || !board.TryGetComponent(out Renderer r)) continue;
                if (first) { area = r.bounds; first = false; }
                else area.Encapsulate(r.bounds);
            }
        }

        void OnEnable() => ShopReach.Add(this);

        void OnDisable()
        {
            ShopReach.Remove(this);
            if (carry != null) carry.Changed -= Rebuild;
            carry = null;
            for (int i = 0; i < shapes.Length; i++)
            {
                if (shapes[i] != null) Destroy(shapes[i]);
                shapes[i] = null;
            }
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

        public float Reach(Vector3 feet)
        {
            // 다른 층(바로 아래 지하 관리실 등)에서는 닿지 않는다(2026-10-31: 관리실 문 F가 위층 선반에 보관되던 문제).
            if (feet.y < area.min.y - .6f || feet.y > area.max.y) return -1f;
            Vector3 closest = area.ClosestPoint(new Vector3(feet.x, area.center.y, feet.z));
            return ExplorationReach.From(VisualRoot, closest, feet, reach);
        }

        public static bool CanUse(Vector3 feet)
        {
            foreach (var target in ShopReach.Targets)
                if (target is HandoverShelfView shelf && shelf.isActiveAndEnabled && shelf.Reach(feet) >= 0f)
                    return true;
            return false;
        }

        public void Interact()
        {
            if (carry != null) carry.RequestShelf();
        }

        void Rebuild()
        {
            if (carry == null) return;
            var shelf = carry.Shelf;
            for (int i = 0; i < shapes.Length; i++)
            {
                bool filled = i < shelf.Count && !ItemCarryBook.IsEmpty(shelf[i]) && i < boards.Length && boards[i] != null;
                if (!filled)
                {
                    if (shapes[i] != null) Destroy(shapes[i]);
                    shapes[i] = null;
                    continue;
                }
                if (shapes[i] == null || !shapes[i].name.EndsWith($"_{shelf[i].Uid}"))
                {
                    if (shapes[i] != null) Destroy(shapes[i]);
                    shapes[i] = MakeShape(i, shelf[i]);
                }
                var top = boards[i].TryGetComponent(out Renderer r) ? r.bounds.max.y : boards[i].position.y;
                shapes[i].transform.position = new Vector3(boards[i].position.x, top + itemSize * .5f, boards[i].position.z);
            }
        }

        GameObject MakeShape(int slot, PropItem item)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = $"ShelfProp_{slot + 1}_{item.Uid}";
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

        void OnGUI()
        {
            if (carry == null) return;
            var shelf = carry.Shelf;
            if (showLegacyLabels)
                for (int i = 0; i < shapes.Length && i < shelf.Count; i++)
                    if (shapes[i] != null) PropLabels.Draw(shapes[i].transform.position + Vector3.up * itemSize, $"{i + 1}칸 {shelf[i].Name}");
            if (ShopReach.HasExternalPresentation || !ReferenceEquals(ShopReach.Focused, this)) return;
            int filled = 0, empty = -1;
            for (int i = 0; i < shelf.Count; i++)
            {
                if (ItemCarryBook.IsEmpty(shelf[i])) { if (empty < 0) empty = i; }
                else filled++;
            }
            ulong me = carry.LocalClientId;
            var held = carry.Held(me);
            string action = held.Count > 0 && empty >= 0 ? $"[F] {held[held.Count - 1].Name} 선반 {empty + 1}칸에 올리기"
                : filled > 0 ? "[F] 선반 위 칸에서 꺼내기" : "선반이 비어 있습니다";
            GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height * .62f, 360, 24), $"{action} (물려줄 선반 {filled}/{shelf.Count})");
        }
    }
}
