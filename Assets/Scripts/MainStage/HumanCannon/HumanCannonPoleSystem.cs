using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    [DefaultExecutionOrder(-20)]
    public sealed partial class HumanCannonPoleSystem : MonoBehaviour
    {
        [SerializeField] HumanCannonPole[] poles = Array.Empty<HumanCannonPole>();
        sealed class Runtime
        {
            public readonly PoleGateState State = new();
            public float Angle, Speed, Input;
            public PoleSwingInput SwingInput;
            public double LastInput;
            public uint InputSequence;
            public int Station;
            public uint Launch, Segment;
        }
        Runtime[] states;
        NetworkSessionManager session;
        double nextSnapshot;
        uint inputSequence;
        double nextInput;
        int lastInputDirection;
        public static HumanCannonPoleSystem Current { get; private set; }
        public int PoleCount => poles.Length;
        public HumanCannonPole Pole(int index) => Valid(index) ? poles[index] : null;
        public PoleGateState State(int index) => states[index].State;
        public double Clock => session != null && session.NetworkManager != null
            && session.NetworkManager.IsListening ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        public int HeldBy(ulong actor)
        {
            if (actor == CannonSeatState.Nobody || states == null) return -1;
            for (int i = 0; i < states.Length; i++) if (states[i].State.Occupant == actor) return i;
            return -1;
        }

        public int FindGrabbable(NetworkPlayer player)
        {
            var cannon = HumanCannonSystem.Current;
            if (player == null || cannon == null || !cannon.IsFlying(player.OwnerClientId)
                || HeldBy(player.OwnerClientId) >= 0 || cannon.IsSeated(player.OwnerClientId)) return -1;
            if (HumanCannonTrampoline.Current != null && HumanCannonTrampoline.Current.Carrier == player.OwnerClientId) return -1;
            int closest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < poles.Length; i++)
            {
                if (!Valid(i) || states[i].State.IsOccupied || !poles[i].IsInReach(player)) continue;
                float candidate = (poles[i].HangingPosition(0) - player.transform.position).sqrMagnitude;
                if (candidate < distance) { distance = candidate; closest = i; }
            }
            return closest;
        }

        internal void RequestGrab(int index, int station, uint launch, uint segment) =>
            SendRequest(PoleAction.Grab, index, station, launch, segment, 0f, 0);
        internal void RequestRelease(int index) => SendRequest(PoleAction.Release, index, 0, 0, 0, 0f, 0);
        internal void RequestSwing(int index, float input)
        {
            int direction = PoleSwingInput.DirectionOf(input);
            // 짧은 키 놓기/방향 전환은 20Hz 유지 패킷 제한에 묻히지 않게 바로 전달한다.
            if (Clock < nextInput && direction == lastInputDirection) return;
            lastInputDirection = direction;
            nextInput = Clock + 0.05;
            SendRequest(PoleAction.Swing, index, 0, 0, 0, input, ++inputSequence);
        }
        public Vector3 HangingPosition(int index) => poles[index].HangingPosition(states[index].Angle);

        internal Vector3 ConstrainFlight(Vector3 from, Vector3 displacement, float radius)
        {
            foreach (var pole in poles)
                if (pole != null && pole.LinkedGate != null && pole.LinkedGate.isActiveAndEnabled)
                    displacement = pole.LinkedGate.ConstrainFlight(from, displacement, radius);
            return displacement;
        }

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            states = new Runtime[poles.Length];
            for (int i = 0; i < states.Length; i++) states[i] = new Runtime();
            NetworkPlayer.Restarted += Restart;
        }

        void Update()
        {
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += Connection;
                Connection(session.ConnectionState);
            }
            if (states == null) return;
            for (int i = 0; i < poles.Length; i++)
            {
                var runtime = states[i];
                var pole = poles[i];
                if (pole == null) continue;
                ulong before = runtime.State.Occupant;
                if (session != null && session.IsHost)
                {
                    if (before != CannonSeatState.Nobody)
                    {
                        var player = FindPlayer(before);
                        if (!pole.isActiveAndEnabled || player == null || player.State != PlayerState.Normal)
                        {
                            runtime.State.TryRelease(before, runtime.State.Revision, Clock, pole.WarningSeconds);
                            Publish(i, PoleEvent.Abandoned, before);
                        }
                        else
                        {
                            float input = Clock - runtime.LastInput <= 0.3 ? runtime.Input : 0f;
                            float drive = runtime.SwingInput.Step(input, Time.deltaTime);
                            pole.Step(ref runtime.Angle, ref runtime.Speed, drive, Time.deltaTime, GameSettings.Base.Character.Gravity);
                        }
                    }
                    before = runtime.State.Occupant;
                    if (runtime.State.Tick(Clock, pole.WarningSeconds))
                        Publish(i, before != CannonSeatState.Nobody && !runtime.State.IsOccupied ? PoleEvent.Released : PoleEvent.Sync, before);
                }
                if (pole.LinkedGate != null)
                {
                    pole.LinkedGate.Present(runtime.State.Phase, pole.LinkedGate.CarrierPassed, Clock);
                    if (session != null && session.IsHost && pole.LinkedGate.HostTick(runtime.State.Phase, runtime.State.Occupant))
                        Publish(i, PoleEvent.Sync, CannonSeatState.Nobody);
                }
            }
            if (session != null && session.IsHost && Clock >= nextSnapshot)
            {
                nextSnapshot = Clock + 0.05;
                for (int i = 0; i < poles.Length; i++) if (poles[i] != null) Publish(i, PoleEvent.Sync, CannonSeatState.Nobody);
            }
        }

        void LateUpdate()
        {
            if (states == null) return;
            for (int i = 0; i < poles.Length; i++)
            {
                if (poles[i] == null) continue;
                bool occupied = Valid(i) && states[i].State.IsOccupied;
                poles[i].PresentSwing(occupied ? states[i].Angle : 0f);
                if (!occupied) continue;
                var player = FindPlayer(states[i].State.Occupant);
                if (player != null && player.State == PlayerState.Normal && !player.IsLocal)
                    player.transform.SetPositionAndRotation(HangingPosition(i), poles[i].Grip.rotation);
            }
        }

        void HandleRequest(ulong actor, PoleAction action, int index, uint revision, int station,
            uint launch, uint segment, float input, uint sequence)
        {
            if (session == null || !session.IsHost) return;
            if (action == PoleAction.Sync)
            {
                for (int i = 0; i < poles.Length; i++) if (poles[i] != null) SendState(actor, i, PoleEvent.Sync, CannonSeatState.Nobody);
                return;
            }
            if (!Valid(index)) return;
            var player = FindPlayer(actor);
            var cannon = HumanCannonSystem.Current;
            var runtime = states[index];
            ulong expiredActor = runtime.State.Occupant;
            if (runtime.State.Tick(Clock, poles[index].WarningSeconds))
                Publish(index, expiredActor != CannonSeatState.Nobody && !runtime.State.IsOccupied ? PoleEvent.Released : PoleEvent.Sync, expiredActor);
            if (player == null || player.State != PlayerState.Normal || cannon == null || revision != runtime.State.Revision) return;
            if (action == PoleAction.Grab)
            {
                if (FindGrabbable(player) != index || !cannon.MatchesFlight(actor, station, launch, segment)
                    || !runtime.State.TryGrab(actor, revision, Clock, poles[index].HoldSeconds, poles[index].LinkedGate != null)) return;
                runtime.Angle = runtime.Speed = runtime.Input = 0f;
                runtime.SwingInput = default;
                runtime.InputSequence = 0;
                runtime.LastInput = Clock;
                runtime.Station = station; runtime.Launch = launch; runtime.Segment = segment;
                Publish(index, PoleEvent.Grabbed, actor);
            }
            else if (action == PoleAction.Release)
            {
                if (!runtime.State.TryRelease(actor, revision, Clock, poles[index].WarningSeconds)) return;
                Publish(index, PoleEvent.Released, actor);
            }
            else if (action == PoleAction.Swing && runtime.State.Occupant == actor
                && sequence > runtime.InputSequence && float.IsFinite(input))
            {
                runtime.Input = Mathf.Clamp(input, -1f, 1f);
                runtime.SwingInput.Observe(runtime.Input);
                runtime.InputSequence = sequence;
                runtime.LastInput = Clock;
            }
        }

        void Apply(int index, PoleEvent action, ulong actor)
        {
            var runtime = states[index];
            var cannon = HumanCannonSystem.Current;
            if (cannon == null) return;
            if (action == PoleEvent.Grabbed)
            {
                cannon.ApplyPoleGrab(actor, runtime.Station, runtime.Launch, runtime.Segment, index);
                runtime.Segment++;
            }
            else if (action == PoleEvent.Released)
            {
                cannon.ApplyPoleRelease(actor, runtime.Station, runtime.Launch, runtime.Segment,
                    HangingPosition(index), poles[index].Grip.rotation,
                    poles[index].ReleaseVelocity(runtime.Angle, runtime.Speed), poles[index].AirControlSpeed);
                runtime.Segment++;
            }
            else if (action == PoleEvent.Abandoned) cannon.AbandonPole(actor);
            else if (action == PoleEvent.Reset)
            {
                if (poles[index].LinkedGate != null) poles[index].LinkedGate.ResetGate();
                cannon.CancelLocalPole(index);
            }
            else if (runtime.State.IsOccupied)
                cannon.SyncPoleHold(runtime.State.Occupant, runtime.Station, runtime.Launch, runtime.Segment, index);
        }

        void Restart(NetworkPlayer player)
        {
            if (session == null || !session.IsHost) return;
            for (int i = 0; i < states.Length; i++)
            {
                var runtime = states[i];
                runtime.State.Reset();
                runtime.Angle = runtime.Speed = runtime.Input = 0f;
                runtime.SwingInput = default;
                if (poles[i] != null) Publish(i, PoleEvent.Reset, CannonSeatState.Nobody);
            }
        }

        bool Valid(int index) => index >= 0 && states != null && index < states.Length && poles[index] != null && poles[index].isActiveAndEnabled;
        static NetworkPlayer FindPlayer(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id) return player;
            return null;
        }
        void OnDisable()
        {
            NetworkPlayer.Restarted -= Restart;
            Unregister();
            if (session != null) session.ConnectionStateChanged -= Connection;
            for (int i = 0; states != null && i < states.Length; i++)
            {
                HumanCannonSystem.Current?.CancelLocalPole(i);
                if (poles[i] != null) poles[i].PresentSwing(0f);
            }
            session = null;
            if (Current == this) Current = null;
        }
    }
}
