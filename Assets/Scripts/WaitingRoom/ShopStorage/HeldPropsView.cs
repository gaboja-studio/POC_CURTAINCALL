using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 플레이어가 든 기물(<see cref="PropCarry.Held"/>)을 선택한 한 개만 캐릭터 앞에 보이고, 배낭을 메면 등 뒤에 상자로 보인다.
    /// 모든 화면이 공유 상태를 그대로 그리므로 다른 사람이 무엇을 들고 있는지도 보인다. 도형은 표시용이다(충돌 없음).
    /// 접속하지 않은 씬 확인에서는 씬에 놓인 플레이어를 쓴다.
    /// </summary>
    public sealed class HeldPropsView : MonoBehaviour
    {
        [SerializeField, Min(.05f)] float itemSize = .26f;
        [Tooltip("캐릭터 기준 첫 기물 위치(m, 앞쪽 +Z).")]
        [SerializeField] Vector3 firstOffset = new(0f, .95f, .45f);
        [Tooltip("기물을 하나 더 들 때마다 위로 쌓는 간격(m).")]
        [SerializeField, Min(.05f)] float stack = .28f;
        [SerializeField] Vector3 backpackOffset = new(0f, 1.15f, -.3f);
        [SerializeField] Vector3 backpackSize = new(.42f, .46f, .2f);
        [SerializeField] Color itemColor = new(.55f, .85f, 1f);
        [SerializeField] Color backpackColor = new(.55f, .38f, .2f);

        readonly Dictionary<(ulong client, int uid), GameObject> shapes = new();
        readonly List<(ulong, int)> stale = new();
        readonly List<(Transform body, ulong client)> bodies = new();
        PropCarry carry;
        Material itemMaterial, backpackMaterial;
        float nextRefresh;

        void OnDisable()
        {
            if (carry != null) carry.Changed -= Rebuild;
            carry = null;
            foreach (var shape in shapes.Values) if (shape != null) Destroy(shape);
            shapes.Clear();
        }

        void OnDestroy()
        {
            if (itemMaterial != null) Destroy(itemMaterial);
            if (backpackMaterial != null) Destroy(backpackMaterial);
        }

        void Update()
        {
            if (carry == null && PropCarry.Instance != null)
            {
                carry = PropCarry.Instance;
                carry.Changed += Rebuild;
                Rebuild();
            }
            // 늦게 생성된 캐릭터도 붙이도록 가끔 다시 그린다.
            if (carry != null && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .5f;
                Rebuild();
            }
        }

        void Rebuild()
        {
            if (carry == null) return;
            CollectBodies();
            stale.Clear();
            stale.AddRange(shapes.Keys);
            foreach (var (body, client) in bodies)
            {
                var item = carry.Selected(client);
                if (item.Uid != 0)
                {
                    var key = (client, item.Uid);
                    stale.Remove(key);
                    Place(Get(key, body, false), body, firstOffset, Vector3.one * itemSize);
                }
                if (carry.HasBackpack(client))
                {
                    var key = (client, 0); // 배낭은 기물 번호 0 자리에 둔다
                    stale.Remove(key);
                    Place(Get(key, body, true), body, backpackOffset, backpackSize);
                }
            }
            foreach (var key in stale)
            {
                if (shapes.TryGetValue(key, out var shape) && shape != null) Destroy(shape);
                shapes.Remove(key);
            }
        }

        void CollectBodies()
        {
            bodies.Clear();
            if (NetworkPlayer.All.Count > 0)
            {
                foreach (var player in NetworkPlayer.All)
                    if (player != null && player.IsSpawned) bodies.Add((player.transform, player.OwnerClientId));
                return;
            }
            var reader = FindFirstObjectByType<PlayerInputReader>(); // 접속하지 않은 씬 확인
            if (reader != null) bodies.Add((reader.transform, carry.LocalClientId));
        }

        GameObject Get((ulong client, int uid) key, Transform body, bool backpack)
        {
            if (shapes.TryGetValue(key, out var shape) && shape != null && shape.transform.parent == body) return shape;
            if (shape != null) Destroy(shape);
            shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = backpack ? $"HeldBackpack_{key.client}" : $"HeldProp_{key.client}_{key.uid}";
            Destroy(shape.GetComponent<Collider>());
            shape.hideFlags = HideFlags.DontSave;
            shape.transform.SetParent(body, false);
            shape.GetComponent<Renderer>().sharedMaterial = Material(shape.GetComponent<Renderer>(), backpack);
            shapes[key] = shape;
            return shape;
        }

        static void Place(GameObject shape, Transform body, Vector3 offset, Vector3 size)
        {
            shape.transform.localPosition = offset;
            shape.transform.localRotation = Quaternion.identity;
            Vector3 lossy = body.lossyScale;
            shape.transform.localScale = new Vector3(size.x / Mathf.Max(.001f, lossy.x), size.y / Mathf.Max(.001f, lossy.y), size.z / Mathf.Max(.001f, lossy.z));
        }

        Material Material(Renderer renderer, bool backpack)
        {
            ref Material target = ref backpack ? ref backpackMaterial : ref itemMaterial;
            if (target != null) return target;
            var color = backpack ? backpackColor : itemColor;
            target = new Material(renderer.sharedMaterial) { color = color };
            if (target.HasProperty("_BaseColor")) target.SetColor("_BaseColor", color); // URP Lit
            return target;
        }

        void OnGUI()
        {
            if (carry == null || !showLegacyLabels) return;
            foreach (var (body, client) in bodies)
            {
                if (body == null) continue;
                var held = carry.Held(client);
                if (held.Count == 0) continue;
                PropLabels.Draw(body.TransformPoint(firstOffset + Vector3.up * (stack * held.Count)), held[held.Count - 1].Name);
            }
        }

        [SerializeField] bool showLegacyLabels = false;
        public void CollectLabels(List<ShopWorldLabel> labels)
        {
            if (carry == null) return;
            foreach (var pair in shapes)
            {
                if (pair.Value == null) continue;
                foreach (var item in carry.Held(pair.Key.client))
                    if (item.Uid == pair.Key.uid)
                        labels.Add(new ShopWorldLabel(pair.Value.transform, item.Name, Vector3.up * .3f));
            }
        }
    }
}
