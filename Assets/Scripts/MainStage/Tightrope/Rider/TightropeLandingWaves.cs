using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>호스트가 확인한 착지만 파동으로 공유한다. 각 소유자는 자기 균형에 한 번만 충격을 적용한다.</summary>
    [DefaultExecutionOrder(50)]
    public sealed partial class TightropeLandingWaves : MonoBehaviour
    {
        sealed class AirObservation
        {
            public bool Airborne;
            public double Since, PendingUntil;
            public bool ExternalFlight, ExternalConsumed, SharedExternalAir, ExternalAirSeen;
            public NetworkPlayer Player;
            public PlayerMover Mover;
            public System.Action Teleported;
            public System.Action<JumpKind> JumpChanged;
        }

        sealed class Pulse
        {
            public int Serial, Lane;
            public ulong Source;
            public float Distance, Range, Speed, Shock;
            public double At;
            public bool Handled;
            public LineRenderer View;
        }

        readonly Dictionary<NetworkPlayer, AirObservation> observed = new();
        readonly List<NetworkPlayer> removed = new();
        readonly List<Pulse> pulses = new();
        TightropeCourse course;
        TightropeRun run;
        Material material;
        int round = -1, serial, receivedSerial;
        double nextImpactAt, feedbackUntil;
        public static TightropeLandingWaves Current { get; private set; }
        public int EmittedCount { get; private set; }
        public int LocalImpactCount { get; private set; }
        public float LastImpact { get; private set; }
        public int ActivePulseCount => pulses.Count;
        public string LocalFeedback => Clock < feedbackUntil ? "동료 착지로 줄이 흔들려요 · A/D로 균형 잡기" : null;
        bool Active => isActiveAndEnabled && course != null && course.IsGameplayActive
            && run != null && run.State == TightropeRunState.Running;
        double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        void OnEnable() => Current = this;

        void Update()
        {
            course = TightropeCourse.Current;
            run = TightropeRun.Current;
            Connect();
            int nextRound = run != null ? run.Round : -1;
            if (round != nextRound) { Clear(); round = nextRound; }
            if (!Active) { if (observed.Count > 0 || pulses.Count > 0) Clear(); return; }
            if (session != null && session.IsHost) ObserveLandings();
            TickPulses();
        }

        void ObserveLandings()
        {
            removed.Clear();
            foreach (var pair in observed)
                if (pair.Key == null || !pair.Key.IsSpawned || pair.Key.State != PlayerState.Normal) removed.Add(pair.Key);
            foreach (var player in removed) { Forget(observed[player]); observed.Remove(player); }
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal
                    || !player.TryGetComponent(out PlayerMover mover)) continue;
                bool airborne = player.IsLocal ? mover.IsAirborne : player.IsAirborne;
                if (!observed.TryGetValue(player, out var state))
                {
                    state = ObservePlayer(player, mover);
                    // 생성 자체는 착지가 아니다. 이미 진행 중인 외부 비행은 아래에서 표시한다.
                    state.Airborne = airborne;
                }
                bool trapeze = TightropeTrapeze.Current != null && TightropeTrapeze.Current.IsCrossing(player);
                bool external = trapeze || (TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsCrossing(player));
                if (external)
                {
                    if (!state.ExternalFlight)
                    {
                        state.ExternalFlight = true;
                    }
                    state.SharedExternalAir |= trapeze;
                    state.ExternalAirSeen |= state.SharedExternalAir && player.CurrentJump == JumpKind.Fall;
                    state.Airborne = false; state.PendingUntil = 0d;
                    continue; // 같은 비행의 파동은 호스트가 승인한 외부 착지에서만 만든다.
                }
                if (state.ExternalFlight)
                {
                    state.ExternalAirSeen |= state.SharedExternalAir && player.CurrentJump == JumpKind.Fall;
                    if (!airborne && (!state.SharedExternalAir || state.ExternalAirSeen)
                        && TrySupport(player, mover, out int externalLane, out float externalDistance))
                    {
                        if (!state.ExternalConsumed) Emit(player.OwnerClientId, externalLane, externalDistance);
                        // 외부 착지 요청이 늦게 와도 다음 출발 전까지 같은 착지를 다시 만들지 않는다.
                        state.ExternalConsumed = true; state.ExternalFlight = false;
                    }
                    state.Airborne = false; state.PendingUntil = 0d;
                    continue;
                }
                if (airborne)
                {
                    if (!state.Airborne) state.Since = Clock;
                    state.Airborne = true;
                    state.PendingUntil = 0d;
                    continue;
                }
                if (state.Airborne)
                {
                    state.Airborne = false;
                    if (Clock - state.Since >= GameSettings.Tightrope.RopeMovement.MinAirTimeForShock)
                        state.PendingUntil = Clock + .35d; // 소유자 위치와 착지 상태 메시지의 도착 순서 차이를 기다린다.
                }
                if (state.PendingUntil <= 0d) continue;
                if (Clock > state.PendingUntil) { state.PendingUntil = 0d; continue; }
                if (!TrySupport(player, mover, out int lane, out float distance)) continue;
                state.PendingUntil = 0d;
                Emit(player.OwnerClientId, lane, distance);
            }
        }

        AirObservation ObservePlayer(NetworkPlayer player, PlayerMover mover)
        {
            var state = new AirObservation { Player = player, Mover = mover, Since = Clock };
            state.Teleported = () =>
            {
                state.Airborne = false; state.Since = Clock; state.PendingUntil = 0d;
                ResetExternalObservation(state);
            };
            // 여러 네트워크 값이 같은 프레임에 도착해도 Fall을 한 번 보았다는 사실을 보존한다.
            state.JumpChanged = kind =>
            {
                if (state.ExternalFlight && state.SharedExternalAir && kind == JumpKind.Fall) state.ExternalAirSeen = true;
                if (state.ExternalConsumed && (kind == JumpKind.InPlace || kind == JumpKind.Lane))
                {
                    ResetExternalObservation(state);
                    state.Airborne = true; state.Since = Clock; state.PendingUntil = 0d;
                }
            };
            mover.Teleported += state.Teleported;
            player.JumpChanged += state.JumpChanged;
            observed.Add(player, state);
            return state;
        }

        static void ResetExternalObservation(AirObservation state)
        {
            state.ExternalFlight = state.ExternalConsumed = state.SharedExternalAir = state.ExternalAirSeen = false;
        }

        bool TrySupport(NetworkPlayer player, PlayerMover mover, out int lane, out float distance)
        {
            lane = TightropeCourse.NoLane;
            distance = 0f;
            var stack = PiggybackSystem.Current;
            return mover != null && !mover.PassThrough && (stack == null || stack.GetBelow(player) == null)
                && course.TryGetSupportingRope(mover, out lane)
                && course.TryGetRopePoint(player.transform.position, out int nearest, out distance) && lane == nearest
                && course.IsLaneUsable(lane, distance)
                && Mathf.Abs(player.transform.position.y - course.GetLanePosition(lane, distance).y) <= .3f;
        }

        /// <summary>새 외부 도약마다 이전 착지의 소비 상태를 해제한다.</summary>
        public void RegisterExternalLaunch(NetworkPlayer player, bool sharedAirborne = false)
        {
            if (!Active || session == null || !session.IsHost || player == null || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover mover)) return;
            if (!observed.TryGetValue(player, out var state)) state = ObservePlayer(player, mover);
            ResetExternalObservation(state);
            state.ExternalFlight = true; state.SharedExternalAir = sharedAirborne;
            state.ExternalAirSeen = sharedAirborne && player.CurrentJump == JumpKind.Fall;
            state.Airborne = false; state.PendingUntil = 0d;
        }

        /// <summary>회전봉처럼 이동 부품 밖에서 수행한 착지. 호스트가 승인한 도약의 실제 줄 지지만 받는다.</summary>
        public void RegisterExternalLanding(NetworkPlayer player, bool sharedAirborne = false)
        {
            if (!Active || session == null || !session.IsHost || player == null || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover mover) || !TrySupport(player, mover, out int lane, out float distance)) return;
            if (!observed.TryGetValue(player, out var state)) state = ObservePlayer(player, mover);
            if (state.ExternalConsumed) return;
            if (!state.ExternalFlight)
                state.ExternalAirSeen = state.SharedExternalAir = false;
            state.ExternalFlight = state.ExternalConsumed = true;
            state.SharedExternalAir |= sharedAirborne;
            state.ExternalAirSeen |= state.SharedExternalAir && player.CurrentJump == JumpKind.Fall;
            state.Airborne = false; state.PendingUntil = 0d;
            Emit(player.OwnerClientId, lane, distance);
        }

        void Emit(ulong source, int lane, float distance)
        {
            var settings = GameSettings.Tightrope.RopeMovement;
            if (settings.LandingWaveRange <= 0f || settings.LandingWaveShock <= 0f) return;
            var pulse = new Pulse { Serial = ++serial, Source = source, Lane = lane, Distance = distance,
                At = Clock + settings.LandingWaveDelay, Range = settings.LandingWaveRange,
                Speed = settings.LandingWaveSpeed, Shock = settings.LandingWaveShock };
            EmittedCount++;
            AddPulse(pulse);
            Publish(pulse);
        }

        void AddPulse(Pulse pulse)
        {
            if (pulses.Count >= 32) { RemoveView(pulses[0]); pulses.RemoveAt(0); }
            pulses.Add(pulse);
        }

        void TickPulses()
        {
            var local = NetworkPlayer.Local;
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                var pulse = pulses[i];
                if (Clock > pulse.At + pulse.Range / pulse.Speed + .25d || !course.IsLaneUsable(pulse.Lane, pulse.Distance))
                { RemoveView(pulse); pulses.RemoveAt(i); continue; }
                if (pulse.Handled || Clock < pulse.At || local == null || local.OwnerClientId == pulse.Source) continue;
                float distance = course.GetDistance(local.transform.position);
                if (Clock < pulse.At + Mathf.Abs(distance - pulse.Distance) / pulse.Speed) continue;
                pulse.Handled = true;
                if (local.State != PlayerState.Normal || !local.TryGetComponent(out PlayerMover mover) || mover.IsAirborne
                    || !local.TryGetComponent(out PlayerBalance balance) || !balance.IsActive || balance.HasFallen
                    || !TrySupport(local, mover, out int lane, out distance) || lane != pulse.Lane
                    || Clock < nextImpactAt || !LandingWaveMath.SharesSupport(course.GetUsableSpans(lane), pulse.Distance, distance)) continue;
                float shock = LandingWaveMath.Strength(distance - pulse.Distance, pulse.Range, pulse.Shock);
                var catches = TightropeCatchSystem.Current;
                if (catches != null && (catches.IsReady(local) || catches.SuccessRemaining(local) > 0f))
                    shock *= 1f - catches.CatchReduction;
                if (shock <= 0f) continue;
                // 동시 착지·목마 배율이 큰 연쇄 충격으로 쌓이지 않게 제한한다.
                balance.ApplyShock(shock / Mathf.Max(1f, balance.StackMultiplier));
                nextImpactAt = Clock + .5d;
                LastImpact = shock;
                LocalImpactCount++;
                feedbackUntil = Clock + 1.2d;
            }
        }

        void Clear()
        {
            foreach (var pulse in pulses) RemoveView(pulse);
            foreach (var state in observed.Values) Forget(state);
            pulses.Clear(); observed.Clear(); removed.Clear();
            serial = receivedSerial = 0;
            nextImpactAt = feedbackUntil = 0d;
            LastImpact = 0f;
            EmittedCount = LocalImpactCount = 0;
        }

        static void Forget(AirObservation state)
        {
            if (state.Mover != null) state.Mover.Teleported -= state.Teleported;
            if (state.Player != null) state.Player.JumpChanged -= state.JumpChanged;
        }

        void OnDisable()
        {
            Disconnect(); Clear();
            if (Current == this) Current = null;
        }

        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
