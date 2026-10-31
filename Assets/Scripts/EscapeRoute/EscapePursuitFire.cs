using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Host starts one shared escape clock. Each side-wall eruption consumes its whole bay permanently until restart.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(550)]
    public sealed partial class EscapePursuitFire : MonoBehaviour
    {
        public enum FirePhase : byte { Dormant, Warning, Burst, Burning }

        [Serializable]
        public sealed class Bay
        {
            public BoxCollider coverage;
            public ParticleSystem[] effects = Array.Empty<ParticleSystem>();
            public Light[] lights = Array.Empty<Light>();
            public AudioSource burstAudio;
        }

        [SerializeField] BoxCollider trigger;
        [SerializeField] Bay[] bays = Array.Empty<Bay>();
        [SerializeField, Min(0)] float startDelay = 20;
        [SerializeField, Min(.1f)] float stepSeconds = 12;
        [SerializeField, Min(0)] float warnSeconds = 3;
        [SerializeField, Min(.1f)] float burstSeconds = 3;
        [SerializeField] float safeExitX = 400;
        [SerializeField] Transform coordinateSpace;
        [SerializeField, Min(0)] float burstEmissionMultiplier = 2.5f;
        [SerializeField, Min(.01f)] float steadyEmissionMultiplier = .45f;

        readonly Dictionary<ulong, Vector3> awaitingSpawn = new();
        NetworkSessionManager session;
        double armedAt = -1;
        uint revision;
        bool synchronized;
        int resetFrame = -1;
        double nextSnapshot, nextSync;

        public static EscapePursuitFire Current { get; private set; }
        public double ArmedAt => armedAt;
        public bool IsArmed => armedAt >= 0;
        public int BayCount => bays.Length;
        public bool IsSynchronized => synchronized && !resetBarrier.Waiting;
        public double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        bool Ready => isActiveAndEnabled && IsSynchronized && session != null && session.GameState == GameSessionState.Playing;
        bool Configured => trigger != null && bays.Length > 0 && ValidSchedule(startDelay, stepSeconds, warnSeconds, burstSeconds)
            && float.IsFinite(safeExitX);

        public FirePhase PhaseAt(int index, double now) => SchedulePhase(now, armedAt, index, bays.Length,
            startDelay, stepSeconds, warnSeconds, burstSeconds);

        /// <summary>Absolute server timestamps keep late join and dropped presentation frames on the same stage.</summary>
        public static FirePhase SchedulePhase(double now, double epoch, int index, int count,
            float delay, float interval, float warning, float burst)
        {
            if (!double.IsFinite(now) || !double.IsFinite(epoch) || epoch < 0 || now < epoch
                || index < 0 || index >= count || !ValidSchedule(delay, interval, warning, burst)) return FirePhase.Dormant;
            double ignition = epoch + delay + index * (double)interval;
            if (now >= ignition + burst) return FirePhase.Burning;
            if (now >= ignition) return FirePhase.Burst;
            return now >= Math.Max(epoch, ignition - warning) ? FirePhase.Warning : FirePhase.Dormant;
        }

        public static int ConsumedCount(double now, double epoch, int count, float delay, float interval)
        {
            if (!double.IsFinite(now) || !double.IsFinite(epoch) || epoch < 0 || now < epoch || count <= 0
                || !float.IsFinite(delay) || delay < 0 || !float.IsFinite(interval) || interval <= 0) return 0;
            double elapsed = now - epoch - delay;
            if (elapsed < 0) return 0;
            return (int)Math.Min(count, Math.Floor(elapsed / interval) + 1);
        }

        static bool ValidSchedule(float delay, float interval, float warning, float burst) =>
            float.IsFinite(delay) && delay >= 0 && float.IsFinite(interval) && interval > 0
            && float.IsFinite(warning) && warning >= 0 && float.IsFinite(burst) && burst > 0;

        public bool Threatens(Vector3 feet, float radius, float height, double now)
        {
            // The exit is a hard safe boundary, including bodies whose trailing radius still overlaps the last bay.
            if (!Finite(feet) || feet.x >= safeExitX || !float.IsFinite(radius) || radius < 0
                || !float.IsFinite(height) || height <= 0) return false;
            int consumed = ConsumedCount(now, armedAt, bays.Length, startDelay, stepSeconds);
            for (int i = 0; i < consumed; i++)
                if (bays[i] != null && OverlapsBody(bays[i].coverage, feet, radius, height)) return true;
            return false;
        }

        /// <summary>Authoring uses world-aligned boxes. Transform-space bounds also support an entire rotated facility.</summary>
        public static bool OverlapsBody(BoxCollider volume, Vector3 feet, float radius, float height)
        {
            if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy || !Finite(feet)
                || !float.IsFinite(radius) || radius < 0 || !float.IsFinite(height) || height <= 0) return false;
            Vector3 scale = volume.transform.lossyScale;
            if (Mathf.Abs(scale.x) < .0001f || Mathf.Abs(scale.y) < .0001f || Mathf.Abs(scale.z) < .0001f) return false;
            float capRadius = Mathf.Min(radius, height * .5f);
            Vector3 a = volume.transform.InverseTransformPoint(feet + Vector3.up * capRadius) - volume.center;
            Vector3 b = volume.transform.InverseTransformPoint(feet + Vector3.up * (height - capRadius)) - volume.center;
            var margin = new Vector3(capRadius / Mathf.Abs(scale.x), capRadius / Mathf.Abs(scale.y), capRadius / Mathf.Abs(scale.z));
            Vector3 half = volume.size * .5f + margin;
            // Segment-versus-expanded-box is conservative around the capsule's rounded corners.
            float enter = 0, leave = 1;
            Vector3 direction = b - a;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < .00001f)
                { if (a[axis] < -half[axis] || a[axis] > half[axis]) return false; continue; }
                float first = (-half[axis] - a[axis]) / direction[axis];
                float last = (half[axis] - a[axis]) / direction[axis];
                if (first > last) (first, last) = (last, first);
                enter = Mathf.Max(enter, first); leave = Mathf.Min(leave, last);
                if (enter > leave) return false;
            }
            return true;
        }

        bool CanArm(NetworkPlayer player)
        {
            if (!Alive(player) || player.IsAirborne || (coordinateSpace != null
                    ? coordinateSpace.InverseTransformPoint(player.transform.position).x : player.transform.position.x) >= safeExitX
                || !DamageAllowed(player) || !player.TryGetComponent(out PlayerMover mover)
                || trigger == null || !trigger.enabled || !trigger.gameObject.activeInHierarchy
                || HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0
                || HumanCannonSystem.Current != null && (HumanCannonSystem.Current.IsFlying(player.OwnerClientId)
                    || HumanCannonSystem.Current.IsSeated(player.OwnerClientId))) return false;
            Vector3 point = trigger.transform.InverseTransformPoint(player.transform.position) - trigger.center;
            Vector3 half = trigger.size * .5f;
            if (!Finite(point) || Mathf.Abs(point.x) > half.x || Mathf.Abs(point.y) > half.y || Mathf.Abs(point.z) > half.z) return false;
            if (TightropeCourse.Current != null && TightropeCourse.Current.TryGetSupportingRopeAt(mover, player.transform.position, out _)) return true;
            // Do not use mover.Simulated: it is false for every remote owner on the host.
            foreach (var hit in Physics.RaycastAll(player.transform.position + Vector3.up * .12f, Vector3.down, .3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider != null && !hit.transform.IsChildOf(player.transform) && hit.normal.y > .5f) return true;
            return false;
        }

        static bool Alive(NetworkPlayer player) => player != null && player.IsSpawned && player.State == PlayerState.Normal;
        static bool DamageAllowed(NetworkPlayer player) => NetworkPlayer.ServerCanChangeState == null
            || NetworkPlayer.ServerCanChangeState(player, PlayerState.Fallen);
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this; NetworkPlayer.Restarted += Restart;
            CachePresentation(); HidePresentation();
        }

        void Update()
        {
            Attach();
            if (!Ready)
            {
                HidePresentation();
                if (messaging != null && session != null && !session.IsHost && Time.unscaledTimeAsDouble >= nextSync)
                { nextSync = Time.unscaledTimeAsDouble + .5; RequestSnapshot(resetBarrier.Waiting ? resetBarrier.Token : 0); }
                return;
            }
            if (!Configured) { HidePresentation(); return; }
            double now = Clock;
            if (session.IsHost && resetFrame != Time.frameCount)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (!Alive(player)) continue;
                    if (awaitingSpawn.TryGetValue(player.OwnerClientId, out Vector3 spawn))
                    {
                        if ((player.transform.position - spawn).sqrMagnitude > 9) continue;
                        awaitingSpawn.Remove(player.OwnerClientId);
                    }
                    if (!IsArmed && CanArm(player)) { armedAt = now; revision++; Publish(); }
                    if (IsArmed && DamageAllowed(player) && player.TryGetComponent(out PlayerMover mover)
                        && Threatens(player.transform.position, mover.BodyRadius, mover.BodyHeight, now)) player.ServerKill();
                }
                if (Time.unscaledTimeAsDouble >= nextSnapshot) { nextSnapshot = Time.unscaledTimeAsDouble + .5; Publish(); }
            }
            Present(now);
        }

        void OnDisable()
        {
            if (Current != this) return;
            NetworkPlayer.Restarted -= Restart; Detach(); RestorePresentation(); Current = null;
        }
    }
}
