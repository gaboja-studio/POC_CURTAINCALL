using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPoleSystem
    {
        readonly Dictionary<ulong, int> flightOrigins = new();
        HumanCannonSystem observedFlightSystem;
        public event Action<int, ulong, bool> OccupancyChanged;
        public event Action<int, ulong> Released;
        public event Action<int, ulong, Vector3, bool> Landed;

        void ConnectFlightEvents()
        {
            if (observedFlightSystem == HumanCannonSystem.Current) return;
            if (observedFlightSystem != null) observedFlightSystem.LandingConfirmed -= OnLandingConfirmed;
            observedFlightSystem = HumanCannonSystem.Current;
            if (observedFlightSystem != null) observedFlightSystem.LandingConfirmed += OnLandingConfirmed;
        }

        void OnLandingConfirmed(ulong actor, int station, uint revision, uint segment, Vector3 position, bool safe)
        {
            if (!flightOrigins.Remove(actor, out int index)) return;
            Landed?.Invoke(index, actor, position, safe);
        }

        public void Deactivate(int index)
        {
            if (index < 0 || states == null || index >= states.Length) return;
            if (poles[index] != null)
            {
                poles[index].InteractionAvailable = false;
                poles[index].PresentationVisible = false;
            }
            if (session != null && session.IsHost)
            {
                for (int seat = states[index].State.Capacity - 1; seat >= 0; seat--)
                {
                    ulong actor = states[index].State.OccupantAt(seat);
                    if (actor == CannonSeatState.Nobody) continue;
                    states[index].State.TryRelease(actor, states[index].State.Revision, Clock, .001f);
                    Publish(index, PoleEvent.Abandoned, actor, seat);
                }
            }
            HumanCannonSystem.Current?.CancelLocalPole(index);
        }

        /// <summary>생성 외줄 코스도 기존 봉의 호스트 점유·비행 시스템을 함께 사용한다.</summary>
        public static HumanCannonPoleSystem Ensure(GameObject owner)
        {
            if (Current != null) return Current;
            var existing = owner.GetComponentInChildren<HumanCannonPoleSystem>(true);
            if (existing == null)
                foreach (var candidate in FindObjectsByType<HumanCannonPoleSystem>(FindObjectsSortMode.None))
                    if (candidate.enabled && candidate.gameObject.scene == owner.scene) { existing = candidate; break; }
            if (existing != null) { existing.InitializeStates(); return existing; }
            var root = new GameObject("Common pole service");
            root.SetActive(false);
            root.transform.SetParent(owner.transform, false);
            if (HumanCannonSystem.Current == null)
            {
                var flight = root.AddComponent<HumanCannonSystem>();
                flight.ConfigurePoleServiceSpace(owner.transform);
            }
            var service = root.AddComponent<HumanCannonPoleSystem>();
            root.SetActive(true);
            return service;
        }

        public int Register(HumanCannonPole pole)
        {
            InitializeStates();
            int existing = IndexOf(pole);
            if (existing >= 0) return existing;
            return RegisterPlacement(pole);
        }

        public int IndexOf(HumanCannonPole pole) => pole == null ? -1 : Array.IndexOf(poles, pole);
        public float AngleAt(int index) => Valid(index) ? states[index].DisplayAngle(Time.unscaledTimeAsDouble) : 0f;
        public float SpeedAt(int index) => Valid(index) ? states[index].Speed : 0f;
        public Vector3 ReleaseVelocityAt(int index, int seat = -1) => Valid(index)
            ? poles[index].ReleaseVelocity(states[index].Angle, states[index].Speed, seat, states[index].ReleaseDirection)
            : Vector3.zero;
        public double HoldUntil(int index) => Valid(index) ? states[index].State.DeadlineAt(0) : 0d;
        public bool IsLower(ulong actor) { int index = HeldBy(actor); return index >= 0 && states[index].State.SeatOf(actor) > 0; }
        public bool IsReleaseBlockedByPassenger(ulong actor)
        {
            int index = HeldBy(actor);
            if (index < 0 || !GameSettings.Poles.PreventReleaseWithPassengers) return false;
            var state = states[index].State;
            return state.OccupantAt(state.SeatOf(actor) + 1) != CannonSeatState.Nobody;
        }
        public bool IsFlyingFrom(int index, ulong actor) => index >= 0 && flightOrigins.TryGetValue(actor, out int origin)
            && origin == index && HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsFlying(actor) && HeldBy(actor) < 0;
        public bool CanApproachPole(int index, NetworkPlayer player) => Valid(index) && poles[index].InteractionAvailable
            && CanApproach(player) && FindSeat(player, index) >= 0;

        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input)
        {
            var player = NetworkPlayer.Local;
            if (player == null || context.Mover == null || context.Mover.gameObject != player.gameObject
                || !player.TryGetComponent(out HumanCannonPilot pilot)) return false;
            bool held = HeldBy(player.OwnerClientId) >= 0;
            bool flying = flightOrigins.ContainsKey(player.OwnerClientId) && pilot.IsFlying;
            if (!held && !flying && (!input.InteractTapped || FindGrabbable(player) < 0)) return false;
            if (!held && !flying && !CommonInteractionSelection.Allows(player, Pole(FindGrabbable(player)))) return false;
            pilot.ReadInput(input);
            return true;
        }
    }
}
