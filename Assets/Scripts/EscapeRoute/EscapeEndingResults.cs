using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.ItemArt;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.UI.EscapeResultsKit;
using CurtainCall.WaitingRoom;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Host freezes final player records; clients receive the same roster, including disconnected participants.</summary>
    public sealed class EscapeEndingResults : MonoBehaviour
    {
        [Serializable] public sealed class Person
        {
            public ulong client;
            public int slot, model, outfit;
            public bool escaped;
            public string[] items = Array.Empty<string>();
        }
        [Serializable] sealed class Snapshot { public int generation; public Person[] people; }
        [SerializeField] GameObject prefab;
        [Tooltip("Appearance catalog model order: rabbit, bear, pierrot, girl")]
        [SerializeField] Sprite[] portraits;
        readonly Dictionary<ulong, Person> observed = new();
        readonly HashSet<ulong> arrivals = new();
        Snapshot snapshot;
        ShopChannel channel;
        EscapeResultsView view;
        EscapeEndingPresentation owner;
        bool populated;
        long shownRevision = -1;
        public bool HasSnapshot => snapshot != null;
        public IReadOnlyCollection<ulong> Arrivals => arrivals;
        public EscapeResultsView View => view;

        void Awake()
        {
            owner = GetComponent<EscapeEndingPresentation>();
            channel = new ShopChannel("CurtainCall.Escape.Results", (_, _) => Publish(), Receive, _ => { }, Reset, () => channel.Request("sync"));
        }
        void Update()
        {
            channel.Update();
            var run = CampaignRun.Instance;
            if (run == null || run.State.Phase != CampaignPhase.Escape || snapshot != null) return;
            foreach (var actor in NetworkPlayer.All)
            {
                if (actor == null || !actor.IsSpawned) continue;
                if (actor.State == PlayerState.Arrived) arrivals.Add(actor.OwnerClientId);
                Observe(actor);
            }
        }
        Person Capture(NetworkPlayer actor)
        {
            var held = PropCarry.Instance?.Held(actor.OwnerClientId);
            return new Person { client = actor.OwnerClientId, slot = actor.Slot, model = actor.AppearanceModel,
                outfit = actor.AppearanceOutfit, escaped = arrivals.Contains(actor.OwnerClientId) || actor.State == PlayerState.Arrived,
                items = held == null ? Array.Empty<string>() : held.Take(2).Select(item => item.ItemId).ToArray() };
        }
        void Observe(NetworkPlayer actor)
        {
            // Death may discard inventory in another system. Keep the last live inventory for this row.
            if (actor.State is PlayerState.Fallen or PlayerState.Retired && observed.TryGetValue(actor.OwnerClientId, out var previous))
            { previous.escaped = arrivals.Contains(actor.OwnerClientId); return; }
            observed[actor.OwnerClientId] = Capture(actor);
        }
        public void Freeze()
        {
            channel.Update();
            if (snapshot != null || CampaignRun.Instance == null || !CampaignRun.Instance.HasAuthority) return;
            foreach (var actor in NetworkPlayer.All)
            {
                if (actor == null || !actor.IsSpawned) continue;
                if (actor.State == PlayerState.Arrived) arrivals.Add(actor.OwnerClientId);
                Observe(actor);
            }
            snapshot = new Snapshot { generation = CampaignRun.Instance.State.Generation,
                people = observed.Values.OrderBy(p => p.slot).Take(4).ToArray() };
            Publish();
        }
        void Publish() { if (snapshot != null) channel.SendState(JsonUtility.ToJson(snapshot)); }
        void Receive(string json)
        {
            var incoming = JsonUtility.FromJson<Snapshot>(json);
            if (incoming == null || incoming.people == null || incoming.people.Length < 1 || incoming.people.Length > 4
                || CampaignRun.Instance == null || incoming.generation != CampaignRun.Instance.State.Generation) return;
            var ids = new HashSet<ulong>();
            foreach (var p in incoming.people)
                if (p == null || p.slot < 0 || p.slot > 63 || p.items == null || p.items.Length > 2 || !ids.Add(p.client)) return;
            // The roster is immutable for this ending. Periodic sync must not reset the selected tab or button guard.
            if (snapshot != null && snapshot.generation == incoming.generation) return;
            snapshot = incoming;
            arrivals.Clear();
            foreach (var p in snapshot.people) if (p.escaped) arrivals.Add(p.client);
            populated = false;
        }
        public void Show(bool authority)
        {
            if (snapshot == null || snapshot.people.Length == 0) return;
            if (view == null)
            {
                var canvas = Instantiate(prefab, transform);
                view = canvas.GetComponentInChildren<EscapeResultsView>(true);
                // Gameplay HUD < ending result < the shared transition curtain (2000).
                canvas.GetComponent<Canvas>().sortingOrder = 600;
                view.ReturnRequested.AddListener(RequestReturn);
                if (EventSystem.current == null)
                {
                    var events = new GameObject("Escape result events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    events.transform.SetParent(view.transform, false);
                }
            }
            if (!populated)
            {
                view.SetPlayers(snapshot.people.Select(p => new EscapePlayerData {
                    Name = $"단원 {p.slot + 1}", Portrait = Portrait(p.model),
                    SlotColor = GameSettings.Base.Character.GetPlayerColor(p.outfit >= 0 ? p.outfit : p.slot),
                    Escaped = p.escaped, Items = p.items.Select(id => ItemVisualAssets.Icon(id)).ToArray()
                }).ToArray());
                view.SetReturnAllowed(authority); view.ShowPlayers(); view.SetVisible(true);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                EventSystem.current?.SetSelectedGameObject(view.PlayersTab.gameObject);
                populated = true;
            }
            var rewards = PerformanceRewards.Instance;
            long revision = rewards?.State?.Revision ?? 0;
            if (revision != shownRevision)
            {
                shownRevision = revision;
                var stats = rewards?.RunStatistics;
                var rows = new List<EscapeStatisticData>();
                foreach (string trick in new[] { CampaignSnapshot.TightropeId, CampaignSnapshot.HumanCannonId })
                    for (int level = 1; level <= 3; level++)
                    {
                        var row = stats?.rows.Find(r => r.trick == trick && r.difficulty == level);
                        rows.Add(new EscapeStatisticData { Performance = trick == CampaignSnapshot.TightropeId ? "외줄타기" : "인간 대포",
                            Difficulty = $"{level}단계", ClearRate = row == null ? "—" : $"{100d * row.clears / row.attempts:0}%",
                            AverageCheer = row == null ? "—" : $"{(double)row.cheer / row.attempts:N0}" });
                    }
                view.SetStatistics(rows.ToArray(), stats != null && stats.rows.Count > 0 ? stats.teamBest.ToString("N0") : "—",
                    stats?.hasPersonal == true ? stats.personalBest.ToString("N0") : "—",
                    Portrait(stats?.personalModel ?? -1), (stats?.earned ?? 0).ToString("N0"), (stats?.spent ?? 0).ToString("N0"));
            }
        }
        Sprite Portrait(int model) => portraits != null && model >= 0 && model < portraits.Length ? portraits[model] : null;
        void RequestReturn()
        {
            if (owner != null && owner.ReturnToLobby()) return;
            view.SetReturnAllowed(CampaignRun.Instance != null && CampaignRun.Instance.HasAuthority);
        }
        public void Reset()
        {
            snapshot = null; populated = false; shownRevision = -1; observed.Clear(); arrivals.Clear();
            if (view != null) view.SetVisible(false);
        }
        void OnDisable() { if (view != null) view.SetVisible(false); }
        void OnDestroy() => channel?.Dispose();
    }
}
