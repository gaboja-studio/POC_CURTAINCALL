using System;
using System.Collections.Generic;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.StartLobby;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Player
{
    /// <summary>
    /// 한 번 누른 F의 대상을 놓을 때까지 고정한다. 모든 입력 소비자가 같은 선택을 확인한다.
    /// 2026-11-16 상호작용 고도화: 플레이어 상호작용 거리 안 대상 중 분류 우선순위(<see cref="InteractionFocusSettings"/>)
    /// → 화면 기준점에 가까운 순 → 플레이어에 가까운 순으로 하나를 고른다. 기준점은 범위를 거르지 않고 순서만 정한다(2026-11-16 사용자).
    /// 이미 잡거나 타고 있는 장치(봉·대포·운반·구조 중)는 순위·기준점과 무관하게 계속 입력을 받는다.
    /// </summary>
    public static class CommonInteractionSelection
    {
        public readonly struct Candidate
        {
            public readonly object Target;
            public readonly float Priority;
            public readonly float Distance;
            public readonly string Key;
            /// <summary>화면 기준점까지 거리(화면 높이 비율). 기준점을 쓰지 않는 후보는 0.</summary>
            public readonly float ScreenDistance;
            /// <summary>안내·외곽선이 같은 대상을 찾는 표시 루트(없으면 null).</summary>
            public readonly Transform Root;
            public Candidate(object target, float priority, float distance, string key)
                : this(target, priority, distance, key, 0f, null) { }
            public Candidate(object target, float priority, float distance, string key, float screenDistance, Transform root)
            { Target = target; Priority = priority; Distance = distance; Key = key; ScreenDistance = screenDistance; Root = root; }
        }
        sealed class Gesture
        {
            public int Frame = int.MinValue;
            public bool Held, Consumed;
            public Candidate Choice;
            public PiggybackSystem LongRelease;
        }
        static readonly Dictionary<int, Gesture> gestures = new();
        public static readonly object Idle = new();
        public static readonly object Inventory = new();

        // 분류 순위보다 앞서는 특수 순위. 이미 사용 중인 장치는 가장 앞, 목마 위 협동 버튼은 그다음.
        const float ActivePriority = -100f, MountedButtonPriority = -50f, IdlePriority = 10000f, ReleasePriority = IdlePriority - 1f;

        public static bool Better(Candidate next, Candidate current) => next.Target != null
            && float.IsFinite(next.Distance) && next.Distance >= 0
            && (current.Target == null || next.Priority < current.Priority
                || next.Priority == current.Priority && (next.ScreenDistance < current.ScreenDistance
                    || next.ScreenDistance == current.ScreenDistance && (next.Distance < current.Distance
                        || next.Distance == current.Distance && string.CompareOrdinal(next.Key, current.Key) < 0)));

        public static object Selected(Component actor) => SelectedCandidate(actor).Target;

        /// <summary>안내·외곽선이 강조할 표시 루트. 선택이 없거나 화면 안내형 장치면 null.</summary>
        public static Transform SelectedRoot(Component actor) => SelectedCandidate(actor).Root;

        static Candidate SelectedCandidate(Component actor)
        {
            if (!PlayerInteraction.CanInteract(actor)) return default;
            if (!actor.TryGetComponent<PlayerInputReader>(out var reader) || !reader.isActiveAndEnabled)
                return Choose(actor);
            int id = reader.GetInstanceID();
            if (!gestures.TryGetValue(id, out var gesture)) gestures[id] = gesture = new Gesture();
            if (gesture.Frame != Time.frameCount)
            {
                var input = reader.Current;
                if (!gesture.Held)
                {
                    gesture.Choice = Choose(actor); gesture.Consumed = false; gesture.LongRelease = null;
                    // The tap can operate the selected device; a hold is a separate dismount gesture.
                    if (actor.TryGetComponent(out NetworkPlayer rider) && PiggybackSystem.Current is { } stack
                        && stack.GetSupportingPlayer(rider) != null && stack.GetAbove(rider) == null)
                        gesture.LongRelease = stack;
                }
                if (!gesture.Consumed && input.InteractLongPressed && gesture.LongRelease != null
                    && actor.TryGetComponent(out NetworkPlayer mounted) && gesture.LongRelease.GetBelow(mounted) != null)
                    gesture.Choice = new Candidate(gesture.LongRelease, ActivePriority, 0, "piggyback-release");
                gesture.Held = input.InteractHeld;
                gesture.Frame = Time.frameCount;
            }
            return gesture.Choice;
        }

        public static bool Allows(Component actor, object target) => target != null && Equals(Selected(actor), target);
        public static bool Consume(Component actor, object target)
        {
            if (!Allows(actor, target)) return false;
            if (!actor.TryGetComponent<PlayerInputReader>(out var reader) || !reader.isActiveAndEnabled) return true;
            var gesture = gestures[reader.GetInstanceID()];
            if (gesture.Consumed) return false;
            gesture.Consumed = true;
            return true;
        }
        public static void Forget(Component actor)
        {
            if (actor == null || !actor.TryGetComponent<PlayerInputReader>(out var reader)
                || !gestures.TryGetValue(reader.GetInstanceID(), out var gesture)) return;
            gesture.Held = false;
            gesture.Choice = default;
            gesture.LongRelease = null;
            gesture.Consumed = true;
            // Invalidate this frame, but let the first new gesture choose after an input lock ends.
            gesture.Frame = Time.frameCount;
        }

        /// <summary>지금 장면이 대기실 우선순위 표를 쓰는지. 공연·탈출로·로비는 공연 표.</summary>
        public static bool UsesWaitingRoomOrder => WaitingRoomScenes.IsWaitingRoom(SceneManager.GetActiveScene());

        /// <summary>대기실 대상(IShopTarget)의 분류(2026-11-16 확정 표). 안내만 하는 분류(해금 불가능)는 F를 실행하지 않는다.</summary>
        public static InteractionCategory Category(IShopTarget target)
        {
            bool available = target is not IShopPresentation shown || shown.IsAvailable;
            switch (target)
            {
                case ClueInspectable clue:
                    return !available ? InteractionCategory.Locked : clue.Discovered ? InteractionCategory.Service : InteractionCategory.FirstClue;
                case ExplorationGate { IsKeyPickup: true }:
                    return available && !LocalHandsFull() ? InteractionCategory.PickupItem : InteractionCategory.PickupBlocked;
                case ExplorationGate { IsSocket: true, Opened: true } socket:
                    return available && socket.ConditionsMet ? InteractionCategory.Service : InteractionCategory.Locked;
                case ExplorationGate gate:
                    return available && gate.ConditionsMet && (!gate.UsesKey || gate.LocalHasKey) && HasTool(gate)
                        ? InteractionCategory.Unlockable : InteractionCategory.Locked;
                case ToolTarget or BreakableWall or CurtainCall.ItemArt.ItemPracticeDoor:
                    return available && (target is not IExplorationTarget tool || HasTool(tool))
                        ? InteractionCategory.Unlockable : InteractionCategory.Locked;
                case DebugMoneyProp:
                    return available ? InteractionCategory.PickupItem : InteractionCategory.PickupBlocked;
                case IItemPickupTarget or PerformancePropPickup or DebugEscapeKeyProp:
                    return available && !LocalHandsFull() ? InteractionCategory.PickupItem : InteractionCategory.PickupBlocked;
                case IExplorationTarget other when !HasTool(other):
                    return InteractionCategory.Locked;
                default:
                    return available ? InteractionCategory.Service : InteractionCategory.Locked;
            }
        }

        static bool HasTool(IExplorationTarget tool) => string.IsNullOrEmpty(tool.ToolId)
            || ExplorationTargets.LocalHolds(tool.ToolId) || ExplorationTargets.AllowsBareHands(tool)
            // 배터리가 꽂힌 동력 소켓은 빈손으로도 고를 수 있어야 한다(배터리 빼기, 2026-11-14).
            || tool is ExplorationGate { IsSocket: true, Opened: true };

        static bool LocalHandsFull()
        {
            var carry = PropCarry.Instance;
            if (carry == null) return false;
            ulong me = carry.LocalClientId;
            var held = carry.Held(me);
            return held != null && held.Count >= carry.Slots(me);
        }

        static Candidate Choose(Component actor)
        {
            if (InputGate.IsBlocked || !PlayerInteraction.CanInteract(actor)) return default;
            Candidate best = default;
            Vector3 center = PlayerInteraction.Center(actor);
            bool waitingRoom = UsesWaitingRoomOrder;
            var settings = InteractionFocusSettings.Current;
            float Rank(InteractionCategory category) => settings.Rank(category, waitingRoom);
            void Offer(Candidate next) { if (Better(next, best)) best = next; }
            // 이미 사용 중인 장치·목마 해제처럼 기준점과 무관한 후보.
            void Active(object target, float priority, string key) => Offer(new Candidate(target, priority, 0f, key));
            // 상호작용 거리 안 후보. 기준점 거리는 같은 분류 안의 순서에만 쓴다.
            void Add(object target, Transform root, Vector3 point, float priority, string key, float? distance = null, bool exempt = false)
            {
                if (target == null) return;
                float screen = exempt ? 0f : InteractionFocus.Distance(actor, root, point);
                Offer(new Candidate(target, priority, distance ?? Vector3.Distance(center, point), key, screen, root));
            }
            var player = actor.GetComponent<NetworkPlayer>();
            if (player != null && player.State == PlayerState.Normal)
            {
                float device = Rank(InteractionCategory.PerformanceDevice);
                float carryRank = Rank(waitingRoom ? InteractionCategory.GrabObject : InteractionCategory.PerformanceDevice);
                var poles = HumanCannonPoleSystem.Current;
                int held = poles != null ? poles.HeldBy(player.OwnerClientId) : -1;
                if (held >= 0) Active(poles.Pole(held), ActivePriority, "pole-held");
                var carried = HumanCannonCarry.Held(player.OwnerClientId);
                if (carried != null) Active(carried, ActivePriority, "carry-held");
                var cannon = HumanCannonSystem.Current;
                if (cannon != null && cannon.IsSeated(player.OwnerClientId)) Active(cannon, ActivePriority, "cannon-seated");
                var stack = PiggybackSystem.Current;
                int grabbable = poles != null ? poles.FindGrabbable(player) : -1;
                int boardable = cannon != null ? cannon.FindBoardableStation(player) : -1;
                bool mounted = stack != null && stack.GetSupportingPlayer(player) != null;
                if (mounted)
                {
                    if (grabbable >= 0) Add(poles.Pole(grabbable), PoleRoot(poles.Pole(grabbable)), poles.InteractionPoint(grabbable, player),
                        device, "piggyback-to-pole");
                    else
                    {
                        if (boardable >= 0) Add(cannon.Station(boardable), cannon.Station(boardable).transform,
                            cannon.Station(boardable).BoardingPoint.position, device, "piggyback-to-cannon");
                        if (!ShopReach.Locked && !WaitingRoomRehearsal.IsPreTutorialRestricted)
                            foreach (var target in ShopReach.Targets)
                                if (target is ExpertRoomView room && room.CanBoard(player))
                                    Add(room, room.VisualRoot, room.PresentationPoint, Rank(InteractionCategory.Service), "piggyback-to-medical");
                        if (stack.GetAbove(player) == null) Active(stack, ReleasePriority, "piggyback-held");
                    }
                }
                var hang = TightropeHangSystem.Current;
                if (hang != null)
                {
                    if (hang.IsHanging(player) || hang.IsRescuing(player)) Active(hang, ActivePriority, "rescue-active");
                    else if (hang.NearestRescueTarget(player) is { } rescue)
                        Add(hang, PlayerRoot(rescue.Id), hang.GripPoint(rescue),
                            GameSettings.Base.Interaction.PrioritizeRescue ? device - .5f : device, "rescue");
                }
                if (poles != null && grabbable >= 0)
                    Add(poles.Pole(grabbable), PoleRoot(poles.Pole(grabbable)), poles.InteractionPoint(grabbable, player),
                        device, "pole:" + grabbable);
                var carry = HumanCannonCarry.FindGrabbable(player);
                if (carry != null) Add(carry, carry.CarryRoot, carry.GrabPoint, carryRank, "carry:" + Path(carry.CarryRoot));
                if (!mounted && boardable >= 0)
                    Add(cannon.Station(boardable), cannon.Station(boardable).transform, cannon.Station(boardable).BoardingPoint.position,
                        device, "cannon:" + boardable);
                if (stack != null && stack.FindMountTarget(player) is { } partner)
                    Add(stack, partner.transform, PlayerInteraction.Center(partner), Rank(InteractionCategory.Piggyback),
                        "piggyback:" + partner.OwnerClientId);
                var locks = EscapeCooperativeLocks.Current;
                if (locks != null) for (int i = 0; i < locks.Count; i++)
                    if (locks.CanUseControl(player, i))
                        Add(locks.Control(i), locks.Control(i), locks.ControlPoint(i), mounted ? MountedButtonPriority : device, "lock:" + i);
                var ropes = EscapeCooperativeRopes.Current;
                if (ropes != null && ropes.CanUseButton(player))
                    Add(ropes, ropes.Button, ropes.ButtonPoint, mounted ? MountedButtonPriority : device, "rope-button");
            }
            if (!ShopReach.Locked && !WaitingRoomRehearsal.IsPreTutorialRestricted)
                foreach (var target in ShopReach.Targets)
                {
                    float distance = PlayerInteraction.RestrictReach(target.Reach(actor.transform.position, actor), actor);
                    if (distance < 0f) continue;
                    var view = target as IShopPresentation;
                    // 화면에 보이는 모양이 없는 대상(표시 어댑터 없음)은 기준점 거리를 재지 않는다.
                    Add(target, view?.VisualRoot, view != null ? view.PresentationPoint : actor.transform.position, Rank(Category(target)),
                        "shop:" + (view != null ? Path(view.VisualRoot) : target.GetType().FullName), distance, exempt: view == null);
                }
            foreach (var room in UnityEngine.Object.FindObjectsByType<LobbyRoom>(FindObjectsSortMode.None))
                foreach (var item in room.interactables)
                {
                    if (item == null || !item.isActiveAndEnabled) continue;
                    item.Prompt(out bool usable);
                    if (usable && PlayerInteraction.InRange(actor, item.transform.position, GameSettings.Base.Interaction.Distance, item.transform))
                        Add(item, item.transform, item.transform.position, Rank(InteractionCategory.Service), "lobby:" + Path(item.transform));
                }
            if (best.Target == null) best = new Candidate(Idle, IdlePriority, 0, "idle");
            return best;
        }

        static Transform PoleRoot(HumanCannonPole pole) => pole == null ? null : pole.CommonVisual != null ? pole.CommonVisual : pole.transform;

        static Transform PlayerRoot(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.OwnerClientId == id) return player.transform;
            return null;
        }

        static string Path(Transform target)
        {
            string result = "";
            for (var at = target; at != null; at = at.parent) result = at.GetSiblingIndex() + "/" + result;
            return target != null ? target.gameObject.scene.path + ":" + result : "";
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => gestures.Clear();
    }
}
