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
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    [DefaultExecutionOrder(500)]
    public sealed class CommonInteractionUI : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/UI/CommonInteractionUI.unity";
        [SerializeField] InteractionPromptView prompt;
        [SerializeField] RectTransform promptAnchor;
        [SerializeField, Min(.1f)] float highlightDistance = 4f;
        [SerializeField, Range(0, 6)] float outlinePixels = 6f;
        [SerializeField, ColorUsage(false, true)] Color outlineColor = new Color(3f, 2.8f, 2.2f, 1f);
        readonly List<Candidate> candidates = new();
        readonly List<Candidate> selected = new();
        readonly List<InteractionOutline> outlines = new();
        readonly RaycastHit[] hits = new RaycastHit[64];
        LobbyInteractable[] lobby = System.Array.Empty<LobbyInteractable>();
        LobbyRoom lobbyRoom;
        PlayerInputReader offlineReader;
        Material material;
        readonly List<RectTransform> rescueRings = new();
        Sprite rescueTrack;
        public int HighlightCount => outlines.Count;
        public IReadOnlyList<Transform> Highlighted => outlines.ConvertAll(o => o.Target);
        public InteractionPromptView Prompt => prompt;

        sealed class Candidate
        {
            public Transform Root; public Vector3 Point; public string Action; public bool Available, Focused, WaitingRoom, Lobby, Pole;
            public float Distance; public float? Progress;
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
                // 기존 검정 외곽선 다음에 그려 상호작용 강조가 덮이지 않게 한다.
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 2;
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
            UpdateRescueRings(camera);
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
            AddTightropeDevices(feet);
            AddEscapeCooperativeButton(feet);
            AddCannon(feet);
            AddPiggyback(feet);
            foreach (var candidate in candidates)
            {
                if (candidate.ScreenGuide || !candidate.Available || !IsVisible(candidate, camera, reader.transform)
                    || !candidate.Pole && candidate.Distance > highlightDistance) continue;
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
                focused.Hold, focused.Hold && focused.Available ? focused.Progress ?? reader.Current.InteractHoldProgress : 0,
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
            if (AddHangGuide(player, feet)) return;
            var trapeze = TightropeTrapeze.Current;
            if (trapeze != null && trapeze.IsActive)
            {
                if (trapeze.IsHeld(player))
                {
                    bool lower = trapeze.IsLower(player);
                    bool canJump = lower || trapeze.PassengerAt(trapeze.HeldPole(player)) == ulong.MaxValue;
                    DeviceGuide(player, feet, "SPACE", lower ? "다음 봉으로 점프" : "봉에서 점프", canJump,
                        canJump ? "공중에서 F로 봉 또는 동료를 잡으세요" : "아래 단원이 먼저 점프해야 합니다");
                    return;
                }
                for (int i = 0; i < trapeze.PoleCount; i++)
                {
                    if (trapeze.CanGrabPole(i, player) && trapeze.PoleVisual(i) != null)
                    {
                        Add(trapeze.PoleVisual(i), Center(trapeze.PoleVisual(i)), "봉 잡기", true, true, feet);
                        candidates[candidates.Count - 1].Pole = true;
                        return;
                    }
                    if (trapeze.CanAttachToPole(i, player))
                    { DeviceGuide(player, feet, "F", "매달린 동료 잡기", true, ""); return; }
                }
                if (trapeze.IsInFlight(player))
                { DeviceGuide(player, feet, "F", "봉 또는 동료 잡기", false, "봉이나 동료 가까이 이동하세요"); return; }
            }
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
                    if (relay.CanUsePole(i, player) && relay.PoleVisual(i) != null)
                    {
                        Add(relay.PoleVisual(i), relay.PoleVisual(i).position, "매달려 길 만들기", true, true, feet);
                        candidates[candidates.Count - 1].Pole = true;
                        return;
                    }
            }
            var swing = TightropeSwingPole.Current?.ForPlayer(player);
            if (swing != null && swing.PoleVisual != null && swing.CanGrabPole(player))
            {
                Add(swing.PoleVisual, swing.PoleVisual.position, "봉 잡기", true, true, feet);
                candidates[candidates.Count - 1].Pole = true;
            }
        }
        /// <summary>M2 추락 매달림: 매달린 본인은 남은 시간, 끌어올리는 동료는 진행, 근처 동료에게는 F 안내.</summary>
        bool AddHangGuide(NetworkPlayer player, Vector3 feet)
        {
            var hangs = TightropeHangSystem.Current;
            if (hangs == null) return false;
            foreach (var hang in hangs.Hangs)
            {
                if (hang.Id == player.OwnerClientId)
                {
                    DeviceGuide(player, feet, "-", hang.IsPulling ? "동료가 끌어올리는 중" : "줄에 매달려 버티는 중", false,
                        hang.IsPulling ? $"끌어올리기 {hangs.PullProgress(hang) * 100f:0}%"
                            : $"동료가 F로 끌어올려야 합니다 · 남은 {hangs.RemainingSeconds(hang):0.0}초");
                    return true;
                }
                if (hang.Rescuer == player.OwnerClientId)
                {
                    DeviceGuide(player, feet, "F 홀드", "끌어올리는 중", true, "손을 떼면 취소됩니다", true);
                    candidates[candidates.Count - 1].Progress = hangs.PullProgress(hang);
                    return true;
                }
            }
            if (hangs.IsHanging(player))
            { DeviceGuide(player, feet, "-", "줄을 잡는 중", false, ""); return true; }
            var target = hangs.NearestRescueTarget(player);
            if (target == null) return false;
            NetworkPlayer targetPlayer = null;
            foreach (var other in NetworkPlayer.All) if (other != null && other.OwnerClientId == target.Id) { targetPlayer = other; break; }
            string label = targetPlayer != null && targetPlayer.Slot >= 0 ? $"플레이어 {targetPlayer.Slot + 1} 올리기" : "동료 올리기";
            DeviceGuide(player, feet, "F", label, true, $"계속 누르기 · 남은 {hangs.RemainingSeconds(target):0.0}초", true);
            return true;
        }
        // 구조자의 머리 위에 진행 표시를 둔다.
        void UpdateRescueRings(Camera camera)
        {
            foreach (var ring in rescueRings) if (ring != null) ring.gameObject.SetActive(false);
            var hangs = TightropeHangSystem.Current;
            var canvas = prompt != null ? prompt.GetComponentInParent<Canvas>() : null;
            if (hangs == null || canvas == null) return;
            if (rescueTrack == null)
            {
                var prefab = Resources.Load<GameObject>("Prefabs/UIs/Interaction/InteractionPrompt");
                if (prefab == null) return;
                foreach (var image in prefab.GetComponentsInChildren<Image>(true))
                {
                    if (image.name == "50_HoldTrack") rescueTrack = image.sprite;
                }
                if (rescueTrack == null) return;
            }
            var parent = canvas.transform as RectTransform;
            if (parent == null) return;
            int index = 0;
            foreach (var hang in hangs.Hangs)
            {
                if (!hang.IsPulling) continue;
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || player.OwnerClientId != hang.Rescuer) continue;
                    Vector3 screen = camera.WorldToScreenPoint(player.transform.position + Vector3.up * 2.15f);
                    if (screen.z <= 0f) continue;
                    Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var pos)) continue;
                    if (index == rescueRings.Count) rescueRings.Add(CreateRescueRing(parent));
                    var ring = rescueRings[index++];
                    ring.anchoredPosition = pos;
                    ring.GetComponentInChildren<RescueProgressRing>(true).Progress = hangs.PullProgress(hang);
                    ring.gameObject.SetActive(true);
                }
            }
        }

        RectTransform CreateRescueRing(RectTransform parent)
        {
            var template = Resources.Load<GameObject>("Prefabs/UIs/Interaction/RescueProgress");
            if (template != null)
            {
                var instance = Instantiate(template, parent, false);
                return (RectTransform)instance.transform;
            }
            // 프리팹을 아직 생성하지 않은 프로젝트에서도 구조 안내를 유지한다.
            var root = new GameObject("RescueProgress", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(64f, 64f);
            var track = root.GetComponent<Image>(); track.sprite = rescueTrack; track.color = new Color(.25f, .08f, .08f, .9f); track.raycastTarget = false;
            var outline = root.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(2f, -2f);
            var child = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(RescueProgressRing));
            var fillRect = (RectTransform)child.transform; fillRect.SetParent(rect, false);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fill = child.GetComponent<RescueProgressRing>(); fill.Sprite = rescueTrack; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360; fill.fillOrigin = (int)Image.Origin360.Top; fill.fillClockwise = true; fill.color = new Color(1f, .12f, .12f, 1f); fill.raycastTarget = false;
            return rect;
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
            if (candidates.Exists(candidate => candidate.Focused && candidate.Available)) return;
            var target = system.FindMountTarget(player);
            if (target != null)
            {
                Add(target.transform, PlayerInteraction.Center(target), "목마 타기", true, true, feet);
                candidates[candidates.Count - 1].Pole = true;
            }
        }
        static bool UsingTightropeDevice(NetworkPlayer player) =>
            TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsCrossing(player)
            || TightropeRelayGate.Current != null && TightropeRelayGate.Current.IsHeld(player)
            || TightropeTrapeze.Current != null && TightropeTrapeze.Current.IsCrossing(player);

        void AddCannon(Vector3 feet)
        {
            var player = NetworkPlayer.Local; var system = HumanCannonSystem.Current;
            if (player == null || system == null || player.State != PlayerState.Normal) return;
            if (player.TryGetComponent(out PlayerController controller) && controller.Scheme is TightropeControlScheme)
            {
                // 줄 위 F는 목마/받기/기존 기물 우선순위를 지키는 끝 영역 후보 하나만 받는다.
                var course = TightropeCourse.Current;
                int exitPole = course != null && course.TryGetComponent(out CourseControlSwitcher switcher)
                    ? switcher.FindCannonPoleExit(player) : -1;
                var exitPoles = HumanCannonPoleSystem.Current;
                if (exitPole >= 0 && exitPoles != null && exitPoles.Pole(exitPole) is { } pole)
                {
                    Add(pole.transform, pole.BarPosition(0), "봉 잡기", true, true, feet);
                    candidates[candidates.Count - 1].Pole = true;
                }
                return;
            }
            var trampoline = HumanCannonTrampoline.Current;
            if (system.IsSeated(player.OwnerClientId) || trampoline != null && trampoline.Carrier == player.OwnerClientId) return;
            bool canCarry = trampoline != null && trampoline.CanGrab(player);
            var poles = HumanCannonPoleSystem.Current;
            int focused = poles != null ? poles.FindGrabbable(player) : -1;
            if (focused >= 0) canCarry = false;
            // 원본 입력은 봉 → 트램폴린 → 대포 순서다. 지상 봉을 잡을 때 대포 안내가 앞서지 않게 한다.
            int boardable = canCarry || focused >= 0 ? -1 : system.FindBoardableStation(player);
            if (boardable >= 0)
            {
                var station = system.Station(boardable);
                Add(station.transform, station.Seat.position, "대포 타기", true, true, feet);
                candidates[candidates.Count - 1].Pole = true;
            }
            if (canCarry)
            {
                Add(trampoline.transform, trampoline.Surface, "트램펄린 잡기", true, true, feet);
                candidates[candidates.Count - 1].Pole = true;
            }
            // FindGrabbable은 대포 비행뿐 아니라 허용된 지상/일반 점프/낙하 진입도 판정한다.
            // 표시만 IsFlying으로 제한하면 F로 잡을 수 있는 봉의 안내/강조가 사라진다.
            if (poles != null && focused >= 0)
            {
                for (int i = 0; i < poles.PoleCount; i++)
                {
                    var pole = poles.Pole(i);
                    if (pole != null && pole.isActiveAndEnabled && !poles.State(i).IsOccupied && pole.IsInReach(player))
                    {
                        Add(pole.transform, pole.BarPosition(0), "봉 잡기", true, i == focused, feet);
                        candidates[candidates.Count - 1].Pole = true;
                    }
                }
            }
        }

        void AddEscapeCooperativeButton(Vector3 feet)
        {
            var course = CurtainCall.EscapeRoute.EscapeCooperativeRopes.Current;
            if (course == null || !course.CanUseButton(NetworkPlayer.Local)) return;
            Add(course.Button, course.Button.position, "복귀 계단 열기", true, true, feet);
            candidates[candidates.Count - 1].Pole = true;
            candidates[candidates.Count - 1].Helper = "아래 단원이 올라올 수 있게 문을 엽니다";
        }
        static Vector3 Center(Transform root)
        {
            foreach (var source in root.GetComponentsInChildren<Renderer>())
                if ((source is MeshRenderer || source is SkinnedMeshRenderer) && source.enabled && !source.name.StartsWith("__")) return source.bounds.center;
            Renderer renderer = root.GetComponentInChildren<MeshRenderer>();
            if (renderer == null) renderer = root.GetComponentInChildren<Renderer>();
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
            float range = target.Pole ? Mathf.Max(highlightDistance, target.Distance + .01f) : highlightDistance;
            if (!InViewAndRange(camera, local.position + Vector3.up * .75f, target.Point, range)) return false;
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
            foreach (var ring in rescueRings) if (ring != null) ring.gameObject.SetActive(false);
            foreach (var outline in outlines) outline.Dispose(); outlines.Clear();
            if (prompt != null) prompt.HideImmediate();
        }
        void OnDisable()
        {
            SceneManager.sceneLoaded -= SceneLoaded; ShopReach.HasExternalPresentation = false;
            if (lobbyRoom != null) lobbyRoom.ExternalInteractionPresentation = false;
            Hide();
        }
        void OnDestroy() { foreach (var ring in rescueRings) if (ring != null) Destroy(ring.gameObject); if (material != null) Destroy(material); }
    }
}
