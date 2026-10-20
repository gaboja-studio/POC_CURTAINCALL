using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.WorldLabelsV2;
using CurtainCall.WaitingRoom;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>대기실 소유 W4/W5. 상태 판정은 기존 서비스, 표시는 Assets 정본 인스턴스가 소유한다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class WaitingRoomWorldLabels : MonoBehaviour
    {
        [SerializeField] ExpertRoomView expert;
        [SerializeField] WorldLabelView expertLabel;
        [SerializeField] FloorPropsView floor;
        [SerializeField] ShopDeliverySlot delivery;
        [SerializeField] HandoverShelfView shelf;
        [SerializeField] HeldPropsView held;
        [SerializeField] WorldLabelView[] shelfLabels;
        [SerializeField] WorldLabelView itemPrefab;
        [SerializeField] Transform itemsRoot;
        [SerializeField] UiRuntimeFontFallback fonts;
        [SerializeField, Min(.1f)] float enterDistance = 3f;
        [SerializeField, Min(.1f)] float exitDistance = 3.3f;
        readonly List<ShopWorldLabel> data = new();
        readonly Dictionary<Transform, WorldLabelView> items = new();
        readonly HashSet<Transform> seen = new();
        readonly List<Transform> stale = new();
        readonly RaycastHit[] hits = new RaycastHit[32];
        PlayerInputReader offline;
        public WorldLabelView ExpertLabel => expertLabel;
        public int ItemLabelCount => items.Count;

        void LateUpdate()
        {
            if (expertLabel != null && expert != null) expertLabel.SetVisible(SetText(expertLabel, expert.StatusText));
            Transform observer = Observer();
            Camera camera = Camera.main;
            data.Clear(); seen.Clear();
            if (floor != null && floor.isActiveAndEnabled) floor.CollectLabels(data);
            if (delivery != null && delivery.isActiveAndEnabled) delivery.CollectLabels(data);
            if (shelf != null && shelf.isActiveAndEnabled) shelf.CollectLabels(data);
            if (held != null && held.isActiveAndEnabled) held.CollectLabels(data);
            foreach (var entry in data)
            {
                if (entry.Anchor == null) continue;
                seen.Add(entry.Anchor);
                if (!items.TryGetValue(entry.Anchor, out var label))
                {
                    if (itemPrefab == null || itemsRoot == null) continue;
                    label = Instantiate(itemPrefab, itemsRoot);
                    label.name = "Item_" + entry.Anchor.name;
                    if (fonts != null) fonts.ApplyTo(label.transform);
                    items.Add(entry.Anchor, label);
                }
                label.transform.position = entry.Anchor.position + entry.Offset;
                if (SetText(label, entry.Text)) Present(label, entry.OcclusionRoot, observer, camera);
            }
            stale.Clear();
            foreach (var pair in items)
                if (pair.Key == null || !seen.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var key in stale)
            {
                if (items[key] != null) Destroy(items[key].gameObject);
                items.Remove(key);
            }
            if (shelfLabels == null) return;
            for (int i = 0; i < shelfLabels.Length; i++)
            {
                var label = shelfLabels[i];
                if (label == null) continue;
                bool available = shelf != null && shelf.isActiveAndEnabled && i < shelf.Boards.Count && shelf.Boards[i] != null;
                if (available) Present(label, shelf.VisualRoot, observer, camera);
                else label.SetVisible(false);
            }
        }

        Transform Observer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var local = NetworkPlayer.Local;
                return local != null && local.State == PlayerState.Normal ? local.transform : null;
            }
            if (offline == null) offline = FindFirstObjectByType<PlayerInputReader>();
            return offline != null && offline.isActiveAndEnabled ? offline.transform : null;
        }

        void Present(WorldLabelView label, Transform target, Transform observer, Camera camera)
        {
            var proximity = label.GetComponent<WorldLabelProximity>();
            if (proximity != null)
            {
                if (!Mathf.Approximately(proximity.EnterDistance, enterDistance) || !Mathf.Approximately(proximity.ExitDistance, exitDistance))
                    proximity.TrySetDistances(enterDistance, exitDistance);
                if (proximity.Observer != observer) proximity.BindObserver(observer);
            }
            var billboard = label.GetComponent<WorldLabelBillboard>();
            if (billboard != null) { billboard.enabled = true; billboard.SetCamera(camera); }
            label.SetVisible(observer != null && camera != null && !InputGate.IsBlocked &&
                CommonInteractionUI.InViewAndRange(camera, observer.position, label.transform.position, exitDistance) &&
                Unobstructed(camera, target, observer, label.transform.position));
        }

        bool Unobstructed(Camera camera, Transform target, Transform observer, Vector3 point)
        {
            var ray = point - camera.transform.position;
            int count = Physics.RaycastNonAlloc(camera.transform.position, ray.normalized, hits, Mathf.Max(0, ray.magnitude - .05f), ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(target) && !hits[i].transform.IsChildOf(observer)) return false;
            return true;
        }

        static bool SetText(WorldLabelView label, string text)
        {
            if (label.Title == text) return true;
            // Preserve typography and sliced surfaces; grow the instance plate to the actual localized text.
            var tmp = label.transform.Find("50_LabelTMP").GetComponent<TMP_Text>();
            float width = Mathf.Max(label.LogicalSize.x, tmp.GetPreferredValues(text).x + 80f);
            if (!label.TrySetSize(new Vector2(width, label.LogicalSize.y)) || !label.TrySetContent(text))
            {
                label.SetVisible(false);
                Debug.LogWarning("[WorldLabels] " + label.LastIssue, label);
                return false;
            }
            return true;
        }

        void OnDisable()
        {
            foreach (var label in items.Values) if (label != null) Destroy(label.gameObject);
            items.Clear();
            if (shelfLabels != null) foreach (var label in shelfLabels) if (label != null) label.SetVisible(false);
            if (expertLabel != null) expertLabel.SetVisible(false);
        }
        void OnEnable() { if (expertLabel != null) expertLabel.SetVisible(true); }
    }
}
