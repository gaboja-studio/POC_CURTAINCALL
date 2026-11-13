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
            [NonSerialized] public bool sounded;
        }

        [SerializeField] EscapeUnderstageChase actor;
        [SerializeField] EscapePursuerRoute route;
        [SerializeField] Gate[] gates = Array.Empty<Gate>();
        const string Message = "CurtainCall.Escape.ConcreteBreaches.v1";
        CustomMessagingManager receiver;
        double round = -1, nextSend, nextReveal;
        static float BreakSeconds => Mathf.Max(.1f, CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer.wallBreakSeconds);
        public int GateCount => gates.Length;
        public int BrokenCount { get; private set; }

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
            if (net && net.IsServer && route.Following)
            {
                Gate candidate = null;
                int available = 0;
                foreach (var gate in gates)
                {
                    if (now < nextReveal || gate.started >= 0 || Mathf.Abs(route.Progress - gate.routePoint) > 2.5f) continue;
                    if (Vector3.Distance(actor.transform.position, gate.panel.position) > 32f) continue;
                    foreach (var player in NetworkPlayer.All)
                        if (player && player.IsSpawned && player.State == PlayerState.Normal &&
                            (player.transform.position - gate.panel.position).sqrMagnitude < 45f * 45f)
                        {
                            available++;
                            if (UnityEngine.Random.Range(0, available) == 0) candidate = gate;
                            break;
                        }
                }
                if (candidate != null)
                {
                    candidate.started = now;
                    nextSend = 0;
                    var tuning = CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer;
                    nextReveal = now + UnityEngine.Random.Range(tuning.revealCooldownMin,
                        Mathf.Max(tuning.revealCooldownMin, tuning.revealCooldownMax));
                }
                if (now >= nextSend) Publish(net, now);
            }
            BrokenCount = 0;
            foreach (var gate in gates) Present(gate, now);
        }

        public bool PrepareTravel(Vector3 from, Vector3 to)
        {
            var timer = EscapeRouteTimer.Current;
            if (!timer || !timer.HasStarted) return true;
            bool clear = true;
            Vector3 delta = to - from;
            foreach (var gate in gates)
            {
                if (gate.started >= 0 && timer.Clock - gate.started >= BreakSeconds) continue;
                var bounds = gate.barrier.bounds;
                bounds.Expand(new Vector3(3f, 5f, 3f));
                if (!bounds.IntersectRay(new Ray(from + Vector3.up * 2.5f, delta.normalized), out float distance)
                    || distance > Mathf.Min(delta.magnitude, 8f)) continue;
                if (gate.started < 0 && NetworkManager.Singleton && NetworkManager.Singleton.IsServer)
                { gate.started = timer.Clock; nextSend = 0; }
                clear = false;
            }
            return clear;
        }

        void Present(Gate gate, double now)
        {
            float age = gate.started < 0 ? -1 : Mathf.Max(0, (float)(now - gate.started));
            bool broken = age >= BreakSeconds;
            gate.panel.gameObject.SetActive(!broken);
            if (broken) BrokenCount++;
            if (age >= 0 && !broken)
                gate.panel.localPosition = gate.rest + gate.outward * (.025f * Mathf.Sin(age * 70f));
            gate.debris.gameObject.SetActive(broken && age < BreakSeconds + 1.5f);
            if (!broken) return;
            if (!gate.sounded)
            {
                gate.sounded = true;
                if (age < BreakSeconds + .3f) actor.PlayStep(.55f);
            }
            float t = age - BreakSeconds;
            if (t >= 1.5f) return;
            for (int i = 0; i < gate.fragments.Length; i++)
            {
                var piece = gate.debris.GetChild(i);
                float spread = (i % 3 - 1) * .6f;
                piece.localPosition = gate.fragments[i] + gate.outward * (t * (3f + i % 4))
                    + Vector3.Cross(Vector3.up, gate.outward) * (spread * t) + Vector3.up * (1.5f * t - 5f * t * t);
                piece.localRotation = Quaternion.Euler(t * (90 + i * 13), t * 70, t * spread * 100);
            }
        }

        void Publish(NetworkManager net, double now)
        {
            nextSend = now + .5;
            using var writer = new FastBufferWriter(20 + gates.Length * 8, Allocator.Temp);
            writer.WriteValueSafe(round);
            writer.WriteValueSafe(now);
            writer.WriteValueSafe(gates.Length);
            foreach (var gate in gates) writer.WriteValueSafe(gate.started);
            foreach (var id in net.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId)
                    net.CustomMessagingManager.SendNamedMessage(Message, id, writer, NetworkDelivery.ReliableSequenced);
        }

        void Read(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != 20 + gates.Length * 8) return;
            reader.ReadValueSafe(out double epoch);
            reader.ReadValueSafe(out double sent);
            reader.ReadValueSafe(out int count);
            var timer = EscapeRouteTimer.Current;
            if (!timer || !timer.HasStarted || epoch != timer.StartedAt || count != gates.Length || !double.IsFinite(sent)) return;
            var times = new double[count];
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out times[i]);
                if (!double.IsFinite(times[i]) || times[i] != -1 && (times[i] < epoch || times[i] > sent)) return;
            }
            if (round != epoch) { ResetWalls(); round = epoch; }
            for (int i = 0; i < count; i++) if (times[i] >= 0) gates[i].started = times[i];
        }

        void ResetWalls()
        {
            BrokenCount = 0; nextSend = nextReveal = 0;
            foreach (var gate in gates)
            {
                gate.started = -1; gate.sounded = false;
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
