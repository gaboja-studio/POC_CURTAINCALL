using System;
using CurtainCall.Network.PlayerSync;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Host-timed concrete breaches in the facility's existing walls.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class EscapePursuerWalls : MonoBehaviour
    {
        [Serializable]
        public sealed class Gate
        {
            public Transform panel;
            public BoxCollider barrier;
            public Transform debris;
            public int routePoint;
            public Vector3 outward;
            [NonSerialized] public double started = -1;
            [NonSerialized] public Vector3 rest;
            [NonSerialized] public Vector3[] fragments;
            [NonSerialized] public Vector3 contact;
            [NonSerialized] public bool sounded;
        }

        [SerializeField] EscapeUnderstageChase actor;
        [SerializeField] EscapePursuerRoute route;
        [SerializeField] Gate[] gates = Array.Empty<Gate>();
        const string Message = "CurtainCall.Escape.ConcreteBreaches.v2";
        CustomMessagingManager receiver;
        double round = -1, nextSend;
        static float BreakSeconds => Mathf.Max(.1f, CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer.wallBreakSeconds);
        const float StrikeReach = 2.8f;
        internal const float FollowThrough = .32f;
        readonly RaycastHit[] travelHits = new RaycastHit[128];
        readonly Collider[] travelOverlaps = new Collider[128];
        float[] gateContacts = Array.Empty<float>();
        public int GateCount => gates.Length;
        public int BrokenCount { get; private set; }
        public Gate At(int index) => index >= 0 && index < gates.Length ? gates[index] : null;

        public bool PrepareBreach(int index, Vector3 feet)
        {
            var gate = At(index);
            var timer = EscapeRouteTimer.Current;
            if (gate == null || !timer || !timer.HasStarted) return false;
            return PrepareBreachAt(gate, feet, timer.Clock, NetworkManager.Singleton && NetworkManager.Singleton.IsServer);
        }

        bool PrepareBreachAt(Gate gate, Vector3 feet, double now, bool authority)
        {
            if (gate.started >= 0) return now - gate.started >= BreakSeconds;
            // A wall is opened by the creature's committed strike, never player proximity.
            Vector3 chest = feet + Vector3.up * 2.5f;
            if (!gate.barrier || Vector3.Distance(chest, gate.barrier.ClosestPoint(chest)) > StrikeReach + .002f || !authority) return false;
            gate.contact = gate.barrier.ClosestPoint(feet + Vector3.up * 3.6f);
            gate.started = now;
            nextSend = 0;
            return false;
        }

        public bool StrikePose(Vector3 feet, out Vector3 direction, out float progress)
            => StrikePose(feet, out direction, out progress, out _);

        internal bool StrikePose(Vector3 feet, out Vector3 direction, out float progress, out Vector3 contact)
        {
            direction = contact = Vector3.zero; progress = 0;
            var timer = EscapeRouteTimer.Current;
            if (!timer) return false;
            foreach (var gate in gates)
            {
                float age = (float)(timer.Clock - gate.started);
                if (gate.started < 0 || age < 0 || age >= BreakSeconds + FollowThrough ||
                    Vector3.Distance(feet + Vector3.up * 3.6f, gate.contact) > 4f) continue;
                contact = gate.contact;
                direction = Vector3.ProjectOnPlane(contact - feet, Vector3.up);
                progress = age / BreakSeconds;
                return true;
            }
            return false;
        }

        internal bool FinishingStrike(int index, double now)
        {
            var gate = At(index);
            return gate != null && gate.started >= 0 && now < gate.started + BreakSeconds + FollowThrough;
        }

        void OnEnable()
        {
            foreach (var gate in gates)
            {
                gate.rest = gate.panel.localPosition;
                gate.fragments = new Vector3[gate.debris.childCount];
                for (int i = 0; i < gate.fragments.Length; i++) gate.fragments[i] = gate.debris.GetChild(i).localPosition;
            }
            ResetWalls();
        }

        void Update()
        {
            var net = NetworkManager.Singleton;
            var timer = EscapeRouteTimer.Current;
            var messaging = net && net.IsListening && !net.IsServer ? net.CustomMessagingManager : null;
            if (receiver != messaging)
            {
                receiver?.UnregisterNamedMessageHandler(Message);
                receiver = messaging;
                receiver?.RegisterNamedMessageHandler(Message, Read);
            }
            double epoch = timer && timer.HasStarted ? timer.StartedAt : -1;
            if (round != epoch) { ResetWalls(); round = epoch; }
            if (epoch < 0 || !actor || actor.InShowcase || !EscapeCampaignContent.IsTraversalActive) return;
            double now = timer.Clock;
            if (net && net.IsServer && now >= nextSend) Publish(net, now);
            BrokenCount = 0;
            foreach (var gate in gates) Present(gate, now);
        }

        public bool PrepareTravel(Vector3 from, Vector3 to)
        {
            var timer = EscapeRouteTimer.Current;
            if (!timer || !timer.HasStarted) return true;
            TravelLimitAt(from, to, Vector3.Distance(from, to), timer.Clock,
                NetworkManager.Singleton && NetworkManager.Singleton.IsServer, out bool clear);
            return clear;
        }

        // The ground mover consumes this budget before changing its route cursor. In particular,
        // an expanded AABB's corner must not stop us outside the actual strike distance forever.
        internal float TravelLimit(Vector3 from, Vector3 to, float distance, double now)
            => TravelLimitAt(from, to, distance, now, NetworkManager.Singleton && NetworkManager.Singleton.IsServer, out _);

        float TravelLimitAt(Vector3 from, Vector3 to, float distance, double now, bool authority, out bool clear)
        {
            float length = Vector3.Distance(from, to);
            float limit = Mathf.Clamp(distance, 0, length);
            clear = true;
            if (length < .0001f || !FindGateContacts(from, to, now)) return limit;
            Vector3 direction = (to - from) / length, chest = from + Vector3.up * 2.5f;
            for (int i = 0; i < gates.Length; i++)
            {
                if (float.IsPositiveInfinity(gateContacts[i])) continue;
                var gate = gates[i];
                clear = false;
                float separation = Vector3.Distance(chest, gate.barrier.ClosestPoint(chest));
                if (separation <= StrikeReach + .002f)
                {
                    PrepareBreachAt(gate, from, now, authority);
                    limit = 0;
                    continue;
                }
                if (separation > StrikeReach + limit) continue;
                // At the capsule's first contact the chest is within the strike sphere.
                // Distance to a convex box along a segment gives a single entering boundary.
                float low = 0, high = gateContacts[i];
                Vector3 contact = chest + direction * high;
                if (Vector3.Distance(contact, gate.barrier.ClosestPoint(contact)) > StrikeReach)
                {
                    // A malformed oversized body cannot be advanced through a closed wall.
                    limit = 0;
                    continue;
                }
                for (int step = 0; step < 18; step++)
                {
                    float middle = (low + high) * .5f;
                    Vector3 sample = chest + direction * middle;
                    if (Vector3.Distance(sample, gate.barrier.ClosestPoint(sample)) > StrikeReach) low = middle;
                    else high = middle;
                }
                limit = Mathf.Min(limit, high);
            }
            return limit;
        }

        bool Closed(Gate gate, double now) => gate != null && gate.barrier && gate.barrier.enabled
            && gate.barrier.gameObject.activeInHierarchy && (gate.started < 0 || now - gate.started < BreakSeconds);

        bool FindGateContacts(Vector3 from, Vector3 to, double now)
        {
            if (gateContacts.Length != gates.Length) gateContacts = new float[gates.Length];
            Array.Fill(gateContacts, float.PositiveInfinity);
            Vector3 delta = to - from;
            float length = delta.magnitude;
            BodyCapsule(from, out var low, out var high, out float radius);
            int count = Physics.CapsuleCastNonAlloc(low, high, radius, delta.normalized, travelHits, length,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var hits = travelHits;
            if (count == travelHits.Length)
            {
                hits = Physics.CapsuleCastAll(low, high, radius, delta.normalized, length,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            bool any = false;
            for (int h = 0; h < count; h++)
                for (int i = 0; i < gates.Length; i++)
                    if (Closed(gates[i], now) && hits[h].collider == gates[i].barrier)
                    { gateContacts[i] = Mathf.Min(gateContacts[i], hits[h].distance); any = true; }
            int overlaps = Physics.OverlapCapsuleNonAlloc(low, high, radius, travelOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var contacts = travelOverlaps;
            if (overlaps == travelOverlaps.Length)
            {
                contacts = Physics.OverlapCapsule(low, high, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                overlaps = contacts.Length;
            }
            for (int h = 0; h < overlaps; h++)
                for (int i = 0; i < gates.Length; i++)
                    if (Closed(gates[i], now) && contacts[h] == gates[i].barrier)
                    { gateContacts[i] = 0; any = true; }
            return any;
        }

        void BodyCapsule(Vector3 feet, out Vector3 low, out Vector3 high, out float radius)
        {
            var body = actor ? actor.GetComponent<CapsuleCollider>() : null;
            Vector3 center = feet + (body ? actor.transform.TransformVector(body.center) : Vector3.up * 2.5f);
            Vector3 scale = body ? body.transform.lossyScale : Vector3.one;
            radius = body ? body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : 1.4f;
            float height = body ? body.height * Mathf.Abs(scale.y) : 5f;
            float half = Mathf.Max(0, height * .5f - radius);
            Vector3 up = body ? body.transform.up : Vector3.up;
            low = center - up * half; high = center + up * half;
        }

        internal bool PrepareJump(Vector3 from, Vector3 to, double now)
            => PrepareJumpAt(from, to, now, NetworkManager.Singleton && NetworkManager.Singleton.IsServer);

        bool PrepareJumpAt(Vector3 from, Vector3 to, double now, bool authority)
        {
            // Check the real arc: a low wall below the jump is not a launch obstruction.
            float arc = EscapeUnderstageChase.ServiceJumpArc(from, to);
            var envelope = new Bounds(from, Vector3.zero); envelope.Encapsulate(to);
            envelope.Encapsulate((from + to) * .5f + Vector3.up * (arc * .25f));
            envelope.Expand(new Vector3(3, 10, 3));
            bool possible = false;
            foreach (var gate in gates) if (Closed(gate, now) && envelope.Intersects(gate.barrier.bounds)) { possible = true; break; }
            if (!possible) return true;
            bool clear = true;
            Vector3 previous = from;
            for (int sample = 1; sample <= 32; sample++)
            {
                float t = sample / 32f;
                Vector3 next = Vector3.Lerp(from, to, t) + Vector3.up * (arc * t * (1 - t));
                if (FindGateContacts(previous, next, now))
                    for (int i = 0; i < gates.Length; i++)
                        if (!float.IsPositiveInfinity(gateContacts[i]))
                        {
                            // Only the real takeoff position may start a strike, never a future airborne sample.
                            PrepareBreachAt(gates[i], from, now, authority);
                            clear = false;
                        }
                previous = next;
            }
            return clear;
        }

        void Present(Gate gate, double now)
        {
            float age = gate.started < 0 ? -1 : Mathf.Max(0, (float)(now - gate.started));
            bool broken = age >= BreakSeconds;
            gate.panel.gameObject.SetActive(!broken);
            if (broken) BrokenCount++;
            if (age >= BreakSeconds * .8f && !broken)
                gate.panel.localPosition = gate.rest + gate.outward * (.025f * Mathf.Sin(age * 70f));
            gate.debris.gameObject.SetActive(broken && age < BreakSeconds + 1.5f);
            if (!broken) return;
            if (!gate.sounded)
            {
                gate.sounded = true;
                if (age < BreakSeconds + .3f) actor.PlayWallImpact(gate.contact);
            }
            float t = age - BreakSeconds;
            if (t >= 1.5f) return;
            Vector3 outward = gate.debris.parent.InverseTransformDirection(gate.panel.parent.TransformDirection(gate.outward));
            for (int i = 0; i < gate.fragments.Length; i++)
            {
                var piece = gate.debris.GetChild(i);
                float spread = (i % 4 - 1.5f) * 1.1f;
                piece.localPosition = gate.fragments[i] + outward * (t * (4f + i % 4))
                    + Vector3.Cross(Vector3.up, outward) * (spread * t) + Vector3.up * ((1.5f + i % 3) * t - 6f * t * t);
                piece.localRotation = Quaternion.Euler(t * (90 + i * 13), t * 70, t * spread * 100);
            }
        }

        void Publish(NetworkManager net, double now)
        {
            nextSend = now + .5;
            using var writer = new FastBufferWriter(20 + gates.Length * 20, Allocator.Temp);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(now);
            writer.WriteValueSafe(gates.Length);
            foreach (var gate in gates) { writer.WriteValueSafe(gate.started); writer.WriteValueSafe(gate.contact); }
            foreach (var id in net.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId)
                    net.CustomMessagingManager.SendNamedMessage(Message, id, writer, NetworkDelivery.ReliableSequenced);
        }

        void Read(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != 20 + gates.Length * 20) return;
            reader.ReadValueSafe(out double epoch);
            reader.ReadValueSafe(out double sent);
            reader.ReadValueSafe(out int count);
            var timer = EscapeRouteTimer.Current;
            if (!timer || !timer.HasStarted || epoch != timer.StartedAt || count != gates.Length || !double.IsFinite(sent)) return;
            var times = new double[count];
            var contacts = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out times[i]);
                reader.ReadValueSafe(out contacts[i]);
                if (!double.IsFinite(times[i]) || !float.IsFinite(contacts[i].sqrMagnitude) ||
                    times[i] != -1 && (times[i] < epoch || times[i] > sent)) return;
            }
            if (round != epoch) { ResetWalls(); round = epoch; }
            for (int i = 0; i < count; i++)
                if (times[i] >= 0) { gates[i].started = times[i]; gates[i].contact = contacts[i]; }
        }

        void ResetWalls()
        {
            BrokenCount = 0; nextSend = 0;
            foreach (var gate in gates)
            {
                gate.started = -1; gate.sounded = false; gate.contact = default;
                gate.panel.localPosition = gate.rest;
                gate.panel.gameObject.SetActive(true);
                gate.debris.gameObject.SetActive(false);
            }
        }

        void OnDisable()
        {
            receiver?.UnregisterNamedMessageHandler(Message); receiver = null;
            ResetWalls(); round = -1;
        }
    }
}
