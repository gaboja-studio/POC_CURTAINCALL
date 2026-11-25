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
        readonly Dictionary<ulong, (int Pole, int Station, uint Launch)> flightOrigins = new();
        HumanCannonSystem observedFlightSystem;

        bool TryRecyclePosition(int index, int seat, ulong actor, out Vector3 position)
        {
            var runtime = states[index];
            position = default;
            if (seat < 0 || seat >= runtime.Riders.Length || !runtime.Recycling || actor == CannonSeatState.Nobody
                || runtime.RecycleActors[seat] != actor || runtime.RecycleLaunches[seat] != runtime.Riders[seat].Launch
                || runtime.RecycleSegments[seat] != runtime.Riders[seat].Segment) return false;
            position = runtime.RecyclePositions[seat];
            return true;
        }

        void ObserveLoopRecycle(int index)
        {
            var runtime = states[index];
            var pole = poles[index];
            var loop = pole.Loop;
            if (loop == null || !loop.IsSynchronized) { runtime.PreviousLoopElapsed = -1; return; }
            double elapsed = loop.Elapsed, previous = runtime.PreviousLoopElapsed;
            runtime.PreviousLoopElapsed = elapsed;
            if (elapsed < previous) { runtime.Recycling = false; return; }
            if (!loop.TryGetRecycleBoundary(previous, elapsed, out double terminalTime)) return;
            runtime.Recycling = true;
            for (int seat = 0; seat < runtime.Riders.Length; seat++)
            {
                runtime.RecyclePositions[seat] = loop.PointAt(pole.HangingPosition(runtime.Angle, seat), terminalTime);
                runtime.RecycleActors[seat] = runtime.State.OccupantAt(seat);
                runtime.RecycleLaunches[seat] = runtime.Riders[seat].Launch;
                runtime.RecycleSegments[seat] = runtime.Riders[seat].Segment;
            }
            // Clients hold the last rail end until the authoritative release arrives.
            // Actor and flight tokens identify this hold on both host and clients; JoinedRevision is host-only.
            if (session == null || !session.IsHost) return;
            for (int seat = runtime.State.Capacity - 1; seat >= 0; seat--)
            {
                ulong actor = runtime.State.OccupantAt(seat);
                if (actor == CannonSeatState.Nobody || !runtime.State.TryRelease(actor, runtime.State.Revision, Clock, pole.WarningSeconds)) continue;
                var player = FindPlayer(actor);
                Publish(index, player != null && player.State == PlayerState.Normal ? PoleEvent.Released : PoleEvent.Abandoned,
                    actor, seat, true);
            }
        }
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
            if (!flightOrigins.TryGetValue(actor, out var origin) || origin.Station != station || origin.Launch != revision) return;
            flightOrigins.Remove(actor);
            Landed?.Invoke(origin.Pole, actor, position, safe);
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
                    var state = states[index].State;
                    if (state.TryRelease(actor, state.Revision, Clock, .001f))
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
        public bool IsFlyingFrom(int index, ulong actor) => index >= 0 && flightOrigins.TryGetValue(actor, out var origin)
            && origin.Pole == index && HasMatchingDeparture(actor, origin) && HeldBy(actor) < 0;

        static bool HasMatchingDeparture(ulong actor, (int Pole, int Station, uint Launch) origin) =>
            HumanCannonSystem.Current != null && HumanCannonSystem.Current.MatchesLaunch(actor, origin.Station, origin.Launch);
        public bool CanApproachPole(int index, NetworkPlayer player) => Valid(index) && poles[index].InteractionAvailable
            && CanApproach(player) && FindSeat(player, index) >= 0;

        /// <summary>
        /// 조작 안내용: 가까이 있지만 아직 잡을 수 없는 목마 전용 높은 봉(맨 위 자리가 빈 것). 없으면 -1.
        /// 판정·요청에는 쓰지 않는다. 거리는 GameSettings 봉 설정의 '높은 봉 안내 거리'(수평)다.
        /// </summary>
        public int FindPiggybackOnlyHint(NetworkPlayer player)
        {
            float range = GameSettings.Poles.PiggybackHintDistance;
            if (player == null || range <= 0f || states == null || player.State != PlayerState.Normal
                || HeldBy(player.OwnerClientId) >= 0 || !FromGround(player) || FindGrabbable(player) >= 0) return -1;
            Vector3 feet = player.transform.position;
            int best = -1;
            float bestDistance = range * range;
            for (int i = 0; i < poles.Length; i++)
            {
                if (!Valid(i) || !poles[i].PiggybackOnly || !poles[i].InteractionAvailable
                    || states[i].State.OccupantAt(0) != CannonSeatState.Nobody) continue;
                Vector3 bar = poles[i].BarPosition(GrabAngle(i));
                // 봉보다 한참 아래(다른 층)나 봉 위에 있는 사람에게는 안내하지 않는다.
                float below = bar.y - feet.y;
                if (below < 0f || below > 10f) continue;
                Vector3 offset = Vector3.ProjectOnPlane(bar - feet, Vector3.up);
                if (offset.sqrMagnitude > bestDistance) continue;
                bestDistance = offset.sqrMagnitude;
                best = i;
            }
            return best;
        }

        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input)
        {
            var player = NetworkPlayer.Local;
            if (player == null || context.Mover == null || context.Mover.gameObject != player.gameObject
                || !player.TryGetComponent(out HumanCannonPilot pilot)) return false;
            bool held = HeldBy(player.OwnerClientId) >= 0;
            bool flying = pilot.IsFlying && flightOrigins.TryGetValue(player.OwnerClientId, out var origin)
                && HasMatchingDeparture(player.OwnerClientId, origin);
            if (!held && !flying && (!input.InteractTapped || FindGrabbable(player) < 0)) return false;
            if (!held && !flying && !CommonInteractionSelection.Allows(player, Pole(FindGrabbable(player)))) return false;
            pilot.ReadInput(input);
            return true;
        }
    }
}
