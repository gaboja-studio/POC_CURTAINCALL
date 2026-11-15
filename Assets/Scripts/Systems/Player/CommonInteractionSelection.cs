using System;
using System.Collections.Generic;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.StartLobby;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>한 번 누른 F의 대상을 놓을 때까지 고정한다. 모든 입력 소비자가 같은 선택을 확인한다.</summary>
    public static class CommonInteractionSelection
    {
        public readonly struct Candidate
        {
            public readonly object Target;
            public readonly int Priority;
            public readonly float Distance;
            public readonly string Key;
            public Candidate(object target, int priority, float distance, string key)
            { Target = target; Priority = priority; Distance = distance; Key = key; }
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

        public static bool Better(Candidate next, Candidate current) => next.Target != null
            && float.IsFinite(next.Distance) && next.Distance >= 0
            && (current.Target == null || next.Priority < current.Priority
                || next.Priority == current.Priority && (next.Distance < current.Distance
                    || next.Distance == current.Distance && string.CompareOrdinal(next.Key, current.Key) < 0));

        public static object Selected(Component actor)
        {
            if (!PlayerInteraction.CanInteract(actor)) return null;
            if (!actor.TryGetComponent<PlayerInputReader>(out var reader) || !reader.isActiveAndEnabled)
                return Choose(actor).Target;
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
                    gesture.Choice = new Candidate(gesture.LongRelease, 0, 0, "piggyback-release");
                gesture.Held = input.InteractHeld;
                gesture.Frame = Time.frameCount;
            }
            return gesture.Choice.Target;
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

        static Candidate Choose(Component actor)
        {
            if (InputGate.IsBlocked || !PlayerInteraction.CanInteract(actor)) return default;
            Candidate best = default;
            Vector3 center = PlayerInteraction.Center(actor);
            void Add(object target, Vector3 point, int priority, string key)
            {
                var next = new Candidate(target, priority, Vector3.Distance(center, point), key);
                if (Better(next, best)) best = next;
            }
            var player = actor.GetComponent<NetworkPlayer>();
            if (player != null && player.State == PlayerState.Normal)
            {
                var poles = HumanCannonPoleSystem.Current;
                int held = poles != null ? poles.HeldBy(player.OwnerClientId) : -1;
                if (held >= 0) Add(poles.Pole(held), center, 0, "pole-held");
                var carried = HumanCannonCarry.Held(player.OwnerClientId);
                if (carried != null) Add(carried, center, 0, "carry-held");
                var cannon = HumanCannonSystem.Current;
                if (cannon != null && cannon.IsSeated(player.OwnerClientId)) Add(cannon, center, 0, "cannon-seated");
                var stack = PiggybackSystem.Current;
                int grabbable = poles != null ? poles.FindGrabbable(player) : -1;
                int boardable = cannon != null ? cannon.FindBoardableStation(player) : -1;
                bool mounted = stack != null && stack.GetSupportingPlayer(player) != null;
                if (mounted)
                {
                    if (grabbable >= 0) Add(poles.Pole(grabbable), center, 0, "piggyback-to-pole");
                    else
                    {
                        if (boardable >= 0) Add(cannon.Station(boardable), cannon.Station(boardable).BoardingPoint.position, 0, "piggyback-to-cannon");
                        if (!ShopReach.Locked && !WaitingRoomRehearsal.IsPreTutorialRestricted)
                            foreach (var target in ShopReach.Targets)
                                if (target is ExpertRoomView room && room.CanBoard(player))
                                    Add(room, room.PresentationPoint, 0, "piggyback-to-medical");
                        if (stack.GetAbove(player) == null) Add(stack, center, 3, "piggyback-held");
                    }
                }
                var hang = TightropeHangSystem.Current;
                if (hang != null)
                {
                    if (hang.IsHanging(player) || hang.IsRescuing(player)) Add(hang, center, 0, "rescue-active");
                    else if (hang.NearestRescueTarget(player) is { } rescue)
                        Add(hang, hang.GripPoint(rescue), Settings.GameSettings.Base.Interaction.PrioritizeRescue ? 1 : 2, "rescue");
                }
                if (poles != null)
                {
                    int index = grabbable;
                    if (index >= 0) Add(poles.Pole(index), poles.InteractionPoint(index, player), 2, "pole:" + index);
                }
                var carry = HumanCannonCarry.FindGrabbable(player);
                if (carry != null) Add(carry, carry.GrabPoint, 2, "carry:" + Path(carry.CarryRoot));
                if (!mounted && boardable >= 0)
                    Add(cannon.Station(boardable), cannon.Station(boardable).BoardingPoint.position, 2, "cannon:" + boardable);
                if (stack != null && stack.FindMountTarget(player) is { } partner)
                    Add(stack, PlayerInteraction.Center(partner), 2, "piggyback:" + partner.OwnerClientId);
                var locks = EscapeCooperativeLocks.Current;
                if (locks != null) for (int i = 0; i < locks.Count; i++)
                    if (locks.CanUseControl(player, i)) Add(locks.Control(i), locks.ControlPoint(i), mounted ? -1 : 2, "lock:" + i);
                var ropes = EscapeCooperativeRopes.Current;
                if (ropes != null && ropes.CanUseButton(player)) Add(ropes, ropes.ButtonPoint, mounted ? -1 : 2, "rope-button");
            }
            if (!ShopReach.Locked && !WaitingRoomRehearsal.IsPreTutorialRestricted)
                foreach (var target in ShopReach.Targets)
                {
                    if (target is IShopPresentation display && !display.IsAvailable) continue;
                    if (target is ExplorationGate gate && (!gate.ConditionsMet || gate.UsesKey && !gate.LocalHasKey)) continue;
                    if (target is IExplorationTarget tool && !string.IsNullOrEmpty(tool.ToolId)
                        && !ExplorationTargets.LocalHolds(tool.ToolId) && !ExplorationTargets.AllowsBareHands(tool)
                        // 배터리가 꽂힌 동력 소켓은 빈손으로도 고를 수 있어야 한다(배터리 빼기, 2026-11-14).
                        && !(target is ExplorationGate { IsSocket: true, Opened: true })) continue;
                    float distance = target.Reach(actor.transform.position, actor);
                    var next = new Candidate(target, 2, distance, "shop:" + (target is IShopPresentation view ? Path(view.VisualRoot) : target.GetType().FullName));
                    if (Better(next, best)) best = next;
                }
            foreach (var room in UnityEngine.Object.FindObjectsByType<LobbyRoom>(FindObjectsSortMode.None))
                foreach (var item in room.interactables)
                {
                    if (item == null || !item.isActiveAndEnabled) continue;
                    item.Prompt(out bool usable);
                    if (usable && PlayerInteraction.InRange(actor, item.transform.position, Settings.GameSettings.Base.Interaction.Distance, item.transform))
                        Add(item, item.transform.position, 2, "lobby:" + Path(item.transform));
                }
            if (best.Target == null) best = new Candidate(Idle, 3, 0, "idle");
            return best;
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
