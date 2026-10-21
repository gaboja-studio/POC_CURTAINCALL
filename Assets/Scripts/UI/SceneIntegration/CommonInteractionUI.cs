using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.StartLobby;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.UI.InteractionV1;
using CurtainCall.WaitingRoom;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration
{
    [DefaultExecutionOrder(500)]
    public sealed class CommonInteractionUI : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/UI/CommonInteractionUI.unity";
        [SerializeField] InteractionPromptView prompt;
        [SerializeField] RectTransform promptAnchor;
        [SerializeField, Min(.1f)] float highlightDistance = 4f;
        [SerializeField, Range(0, 6)] float outlinePixels = 3f;
        [SerializeField] Color outlineColor = new Color32(240, 238, 220, 255);
        readonly List<Candidate> candidates = new();
        readonly List<Candidate> selected = new();
        readonly List<InteractionOutline> outlines = new();
        readonly RaycastHit[] hits = new RaycastHit[64];
        LobbyInteractable[] lobby = System.Array.Empty<LobbyInteractable>();
        LobbyRoom lobbyRoom;
        PlayerInputReader offlineReader;
        Material material;
        public int HighlightCount => outlines.Count;
        public IReadOnlyList<Transform> Highlighted => outlines.ConvertAll(o => o.Target);
        public InteractionPromptView Prompt => prompt;

        sealed class Candidate
        {
            public Transform Root; public Vector3 Point; public string Action; public bool Available, Focused, WaitingRoom, Lobby;
            public float Distance;
            public string Helper, Key = "F";
            public bool Hold, ScreenGuide;
        }
        public void Configure(InteractionPromptView view, RectTransform anchor) { prompt = view; promptAnchor = anchor; }
        void Awake()
        {
            var shader = Resources.Load<Shader>("TightropeOutline");
            if (shader != null)
            {
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, name = "Interaction highlight (runtime)" };
                material.SetColor("_OutlineColor", outlineColor); material.SetFloat("_OutlinePixels", outlinePixels);
            }
        }
        void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; Refresh(); }
        void SceneLoaded(Scene _, LoadSceneMode mode) => Refresh();
        void Refresh()
        {
            if (lobbyRoom != null) lobbyRoom.ExternalInteractionPresentation = false;
            lobby = FindObjectsByType<LobbyInteractable>(FindObjectsSortMode.None);
            lobbyRoom = FindFirstObjectByType<LobbyRoom>();
            if (lobbyRoom != null) lobbyRoom.ExternalInteractionPresentation = prompt != null;
        }
        PlayerInputReader Reader()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                var local = NetworkPlayer.Local;
                return local != null && local.State == PlayerState.Normal ? local.GetComponent<PlayerInputReader>() : null;
            }
            if (offlineReader == null) offlineReader = FindFirstObjectByType<PlayerInputReader>();
            return offlineReader;
        }
        void LateUpdate()
        {
            ShopReach.HasExternalPresentation = prompt != null;
            var reader = Reader(); var camera = Camera.main;
            if (reader == null || !reader.isActiveAndEnabled || camera == null || InputGate.IsBlocked || SceneCurtainTransition.IsActive) { Hide(); return; }
            candidates.Clear(); selected.Clear();
            Vector3 feet = reader.transform.position;
            foreach (var target in ShopReach.Targets)
            {
                if (target is not IShopPresentation p || p.VisualRoot == null || !p.VisualRoot.gameObject.activeInHierarchy) continue;
                string action = target is ExpertRoomView && !p.IsAvailable
                    ? "서비스를 구매한 고객만 이용 가능합니다" : p.ActionLabel;
                Add(p.VisualRoot, p.PresentationPoint, action, p.IsAvailable, ReferenceEquals(target, ShopReach.Focused), feet);
                candidates[candidates.Count - 1].WaitingRoom = true;
                candidates[candidates.Count - 1].Helper = (target as IShopPresentationDetails)?.HelperLabel;
            }
            foreach (var item in lobby)
            {
                if (item == null || !item.isActiveAndEnabled) continue;
                string action = item.Prompt(out bool usable);
                if (action.StartsWith("[F] ")) action = action.Substring(4);
                Add(item.transform, Center(item.transform), action, usable, lobbyRoom != null && lobbyRoom.FocusedInteraction == item, feet);
                candidates[candidates.Count - 1].Lobby = true;
            }
            AddCannon(feet);
            AddTightropeDevices(feet);
            AddPiggyback(feet);
            foreach (var candidate in candidates)
            {
                if (candidate.ScreenGuide || !candidate.Available || !IsVisible(candidate, camera, reader.transform) || candidate.Distance > highlightDistance) continue;
                int index = 0;
                while (index < selected.Count && (selected[index].Distance < candidate.Distance ||
                    (Mathf.Approximately(selected[index].Distance, candidate.Distance) && selected[index].Root.GetInstanceID() < candidate.Root.GetInstanceID()))) index++;
                selected.Insert(index, candidate);
                if (selected.Count > 2) selected.RemoveAt(2);
            }
            for (int i = outlines.Count - 1; i >= 0; i--)
            {
                bool keep = false;
                foreach (var candidate in selected) if (candidate.Root == outlines[i].Target) keep = true;
                if (!keep) { outlines[i].Dispose(); outlines.RemoveAt(i); }
            }
            if (material != null)
                foreach (var candidate in selected)
                {
                    bool exists = false;
                    foreach (var outline in outlines) if (outline.Target == candidate.Root) exists = true;
                    if (!exists) outlines.Add(new InteractionOutline(candidate.Root, material));
                }
            // F still goes to the game's focused target, never to a second highlighted object.
            Candidate focused = null;
            foreach (var candidate in candidates)
                if (candidate.Focused && IsVisible(candidate, camera, reader.transform)) { focused = candidate; break; }
            if (focused == null)
                foreach (var candidate in selected) if (candidate.WaitingRoom || candidate.Lobby) { focused = candidate; break; }
            if (focused == null || prompt == null) { if (prompt != null) prompt.HideImmediate(); return; }
            prompt.Show(focused.Key, focused.Action, focused.Available && focused.Focused ? InteractionAvailability.Available : InteractionAvailability.Unavailable,
                focused.Hold, focused.Hold && focused.Available ? reader.Current.InteractHoldProgress : 0,
                focused.Focused ? focused.Helper ?? "" : "가까이 다가가세요");
            var layout = prompt.GetComponent<NotificationTextLayout>();
            if (layout == null) layout = prompt.gameObject.AddComponent<NotificationTextLayout>();
            layout.RefreshLayout();
            if (focused.ScreenGuide && promptAnchor != null && promptAnchor.parent is RectTransform parent)
                promptAnchor.anchoredPosition = new Vector2(parent.rect.xMin + promptAnchor.rect.width * .5f + 24,
                    parent.rect.yMin + promptAnchor.rect.height + 144);
            else PositionPrompt(focused.Point, camera);
        }
        void Add(Transform root, Vector3 point, string action, bool available, bool focused, Vector3 feet)
        {
            candidates.Add(new Candidate { Root = root, Point = point, Action = action, Available = available, Focused = focused,
                Distance = Vector3.Distance(feet + Vector3.up * .75f, point) });
        }
        void AddTightropeDevices(Vector3 feet)
        {
            var player = NetworkPlayer.Local;
            var course = TightropeCourse.Current;
            if (player == null || player.State != PlayerState.Normal || course == null || !course.IsGameplayActive) return;
            var relay = TightropeRelayGate.Current;
            if (relay != null && relay.IsActive)
            {
                int held = relay.HeldPole(player);
                if (held >= 0)
                {
                    DeviceGuide(player, feet, "F / F 홀드", "봉 놓기", true,
                        $"SPACE로도 놓기 · 남은 {relay.HoldRemaining(held):0.0}초", true);
                    return;
                }
                for (int i = 0; i < relay.PoleCount; i++)
                    if (relay.CanUsePole(i, player))
                    {
                        Add(relay.PhysicalPole(i).transform, relay.PoleFeet(i) + Vector3.up, "매달려 길 만들기", true, true, feet);
                        return;
                    }
            }
            var trapeze = TightropeTrapeze.Current;
            if (trapeze == null || !trapeze.IsActive) return;
            if (trapeze.IsHeld(player))
            {
                bool lower = trapeze.IsLower(player);
                bool canJump = lower || trapeze.PassengerAt(trapeze.HeldPole(player)) == ulong.MaxValue;
                DeviceGuide(player, feet, "SPACE", lower ? "다음 봉으로 점프" : "봉에서 점프", canJump,
                    canJump ? "공중에서 F로 봉 또는 동료를 잡으세요" : "아래 단원이 먼저 점프해야 합니다");
                return;
            }
            // 실제 입력과 같은 순서로 봉 잡기/동료 잡기를 평가한다. 표시가 판정을 대신하지 않는다.
            for (int i = 0; i < trapeze.PoleCount; i++)
            {
                if (trapeze.CanGrabPole(i, player))
                { DeviceGuide(player, feet, "F", "봉 잡기", true, ""); return; }
                if (trapeze.CanAttachToPole(i, player))
                { DeviceGuide(player, feet, "F", "매달린 동료 잡기", true, ""); return; }
            }
            if (trapeze.IsInFlight(player))
                DeviceGuide(player, feet, "F", "봉 또는 동료 잡기", false, "봉이나 동료 가까이 이동하세요");
        }
        void DeviceGuide(NetworkPlayer player, Vector3 feet, string key, string action, bool available, string helper, bool hold = false)
        {
            candidates.Add(new Candidate { Root = player.transform, Point = feet, Key = key, Action = action,
                Available = available, Focused = true, ScreenGuide = true, Hold = hold, Helper = helper });
        }
        void AddPiggyback(Vector3 feet)
        {
            var player = NetworkPlayer.Local;
            var system = PiggybackSystem.Current;
            var course = TightropeCourse.Current;
            if (player == null || player.State != PlayerState.Normal || system == null || !system.isActiveAndEnabled
                || course == null || !course.IsGameplayActive || UsingTightropeDevice(player)) return;
            if (system.GetBelow(player) != null)
            {
                bool canRelease = system.GetAbove(player) == null;
                candidates.Add(new Candidate { Root = player.transform, Point = feet, Action = "목마 해제",
                    Available = canRelease, Focused = true, ScreenGuide = true, Hold = true,
                    Helper = canRelease ? "F를 길게 누르세요" : "위 단원이 내려온 뒤 해제할 수 있습니다" });
                return;
            }
            if (player.IsAirborne || system.IsInStack(player)) return;
            var catcher = TightropeCatchSystem.Current;
            if (catcher != null && catcher.IsReady(player)) return;
            float reach = GameSettings.Tightrope.Piggyback.LinkDistance;
            if (player.TryGetComponent(out PlayerInteraction interaction)) reach *= interaction.ReachMultiplier;
            NetworkPlayer target = null;
            float best = float.MaxValue;
            // F 선택 API가 없어 서버의 XZ 최근접/최하단 선택을 그대로 투영한다. 방향·층수 제한을 추가하지 않는다.
            foreach (var other in NetworkPlayer.All)
            {
                if (other == null || other == player || other.State != PlayerState.Normal
                    || UsingTightropeDevice(other) || system.GetBelow(other) != null) continue;
                Vector3 offset = other.transform.position - feet;
                offset.y = 0;
                float distance = offset.magnitude;
                if (distance > reach || distance >= best) continue;
                target = other; best = distance;
            }
            if (target != null)
                Add(target.transform, target.transform.position + Vector3.up, "목마 타기", true, true, feet);
        }
        static bool UsingTightropeDevice(NetworkPlayer player) =>
            TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsCrossing(player)
            || TightropeRelayGate.Current != null && TightropeRelayGate.Current.IsHeld(player)
            || TightropeTrapeze.Current != null && TightropeTrapeze.Current.IsCrossing(player);

        void AddCannon(Vector3 feet)
        {
            var player = NetworkPlayer.Local; var system = HumanCannonSystem.Current;
            if (player == null || system == null || player.State != PlayerState.Normal) return;
            var trampoline = HumanCannonTrampoline.Current;
            if (system.IsSeated(player.OwnerClientId) || trampoline != null && trampoline.Carrier == player.OwnerClientId) return;
            if (!player.IsAirborne && !system.IsFlying(player.OwnerClientId))
                for (int i = 0; i < system.StationCount; i++)
                {
                    var station = system.Station(i);
                    if (station != null && station.isActiveAndEnabled && system.Occupant(i) == CannonSeatState.Nobody)
                        Add(station.transform, station.BoardingPoint.position, "대포 타기", true, false, feet);
                }
            if (trampoline != null && trampoline.CanGrab(player)) Add(trampoline.transform, trampoline.Handle.position, "트램펄린 잡기", true, false, feet);
            var poles = HumanCannonPoleSystem.Current;
            if (poles != null && system.IsFlying(player.OwnerClientId) && poles.HeldBy(player.OwnerClientId) < 0)
                for (int i = 0; i < poles.PoleCount; i++)
                {
                    var pole = poles.Pole(i);
                    if (pole != null && pole.isActiveAndEnabled && !poles.State(i).IsOccupied && pole.IsInReach(player))
                        Add(pole.transform, poles.HangingPosition(i), "봉 잡기", true, false, feet);
                }
        }
        static Vector3 Center(Transform root)
        {
            var renderer = root.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.bounds.center : root.position;
        }
        public static bool InViewAndRange(Camera camera, Vector3 feet, Vector3 point, float maxDistance)
        {
            if (camera == null || (point - feet).sqrMagnitude > maxDistance * maxDistance) return false;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z > camera.nearClipPlane && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
        }
        bool IsVisible(Candidate target, Camera camera, Transform local)
        {
            if (target.ScreenGuide) return true;
            if (!InViewAndRange(camera, local.position + Vector3.up * .75f, target.Point, highlightDistance)) return false;
            Vector3 offset = target.Point - camera.transform.position;
            int count = Physics.RaycastNonAlloc(camera.transform.position, offset.normalized, hits, Mathf.Max(0, offset.magnitude - .08f), ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i].transform;
                if (hit.IsChildOf(target.Root) || hit.IsChildOf(local)) continue;
                return false;
            }
            return true;
        }
        void PositionPrompt(Vector3 point, Camera camera)
        {
            if (promptAnchor == null || promptAnchor.parent is not RectTransform parent) return;
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local)) return;
            // The outer view position is separate from the prefab Motion child's offsets.
            var size = promptAnchor.rect.size;
            float half = Mathf.Max(160, size.x * .5f);
            promptAnchor.anchoredPosition = new Vector2(Mathf.Clamp(local.x, parent.rect.xMin + half, parent.rect.xMax - half),
                Mathf.Clamp(local.y - 70, parent.rect.yMin + Mathf.Max(100, size.y + 16), parent.rect.yMax - 100));
        }
        void Hide()
        {
            foreach (var outline in outlines) outline.Dispose(); outlines.Clear();
            if (prompt != null) prompt.HideImmediate();
        }
        void OnDisable()
        {
            SceneManager.sceneLoaded -= SceneLoaded; ShopReach.HasExternalPresentation = false;
            if (lobbyRoom != null) lobbyRoom.ExternalInteractionPresentation = false;
            Hide();
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
