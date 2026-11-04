using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>Owner contact edges, verified against the source's historical geometry before host damage.</summary>
    internal sealed class OwnerContactChannel : IDisposable
    {
        internal delegate bool ContactTest(int index, double at, Vector3 feet, float height, float radius);
        const int Bytes = 45;
        readonly string message;
        readonly Func<int> count;
        readonly Func<int, double> epoch;
        readonly ContactTest test;
        readonly Action<NetworkPlayer, int> confirm;
        readonly HashSet<int> localTouching = new(), nextTouching = new();
        readonly List<int> departed = new();
        readonly HashSet<(ulong, int, int, double)> confirmed = new();
        readonly Dictionary<ulong, uint> received = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        bool boundServer, active, wasActive;
        uint serial;
        double clock, previousAt = -1, minimumAt;
        float retryAt;
        Vector3 previousFeet;
        float previousHeight;
        PlayerMover trackedMover;
        int previousRestart = -1;

        internal OwnerContactChannel(string message, Func<int> count, Func<int, double> epoch,
            ContactTest test, Action<NetworkPlayer, int> confirm)
        { this.message = message; this.count = count; this.epoch = epoch; this.test = test; this.confirm = confirm; }

        internal void Tick(NetworkSessionManager next, double now, bool enabled, bool sweep = true)
        {
            var manager = next != null ? next.NetworkManager : null;
            var nextMessaging = manager != null && manager.IsListening ? manager.CustomMessagingManager : null;
            bool server = next != null && next.IsHost;
            if (messaging != nextMessaging || boundServer != server) {
                Dispose(); minimumAt = 0; session = next; messaging = nextMessaging; boundServer = server;
                if (server) messaging?.RegisterNamedMessageHandler(message, Receive);
            }
            clock = now; active = enabled && messaging != null;
            if (!active) { if (wasActive) Reset(); wasActive = false; return; }
            wasActive = true;
            if (server) return;
            var player = NetworkPlayer.Local;
            if (player == null || !player.IsOwner || !player.IsSpawned || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover mover)) { previousAt = -1; return; }
            Vector3 feet = player.transform.position;
            if (trackedMover != mover) {
                if (trackedMover != null) trackedMover.Teleported -= Teleported;
                trackedMover = mover; trackedMover.Teleported += Teleported; previousAt = -1;
            }
            bool continuous = sweep && previousRestart == player.RestartVersion && previousAt >= 0
                && now > previousAt && now - previousAt <= .1 && Vector3.Distance(feet, previousFeet) < 4
                && Mathf.Abs(previousHeight - mover.BodyHeight) < .01f;
            int steps = continuous ? Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Vector3.Distance(feet, previousFeet) / .1f,
                (float)(now - previousAt) * 200)), 1, 32) : 1;
            nextTouching.Clear(); bool retry = Time.unscaledTime >= retryAt;
            for (int index = 0; index < count(); index++) {
                double stamp = epoch(index);
                if (!double.IsFinite(stamp) || stamp < 0) continue;
                for (int step = continuous ? 0 : steps; step <= steps; step++) {
                    float t = step / (float)steps;
                    double at = continuous ? previousAt + (now - previousAt) * t : now;
                    var point = continuous ? Vector3.Lerp(previousFeet, feet, t) : feet;
                    if (!test(index, at, point, mover.BodyHeight, mover.BodyRadius)) continue;
                    nextTouching.Add(index);
                    if (retry || !localTouching.Contains(index)) Send(player, index, stamp, at, point, mover.BodyHeight, true);
                    break;
                }
            }
            departed.Clear();
            foreach (int index in localTouching) if (!nextTouching.Contains(index)) departed.Add(index);
            foreach (int index in departed) Send(player, index, epoch(index), now, feet, mover.BodyHeight, false);
            localTouching.Clear(); localTouching.UnionWith(nextTouching);
            if (retry) retryAt = Time.unscaledTime + .15f;
            previousAt = now; previousFeet = feet; previousHeight = mover.BodyHeight; previousRestart = player.RestartVersion;
        }

        void Send(NetworkPlayer player, int index, double stamp, double at, Vector3 feet, float height, bool contact)
        {
            using var writer = new FastBufferWriter(Bytes, Allocator.Temp);
            writer.WriteValueSafe(index); writer.WriteValueSafe(++serial); writer.WriteValueSafe(player.RestartVersion);
            writer.WriteValueSafe(stamp); writer.WriteValueSafe(at); writer.WriteValueSafe(feet);
            writer.WriteValueSafe(height); writer.WriteValueSafe(contact);
            messaging.SendNamedMessage(message, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void Receive(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || !session.NetworkManager.ConnectedClients.ContainsKey(sender)
                || reader.Length - reader.Position != Bytes) return;
            reader.ReadValueSafe(out int index); reader.ReadValueSafe(out uint sequence); reader.ReadValueSafe(out int restart);
            reader.ReadValueSafe(out double stamp); reader.ReadValueSafe(out double at); reader.ReadValueSafe(out Vector3 feet);
            reader.ReadValueSafe(out float height); reader.ReadValueSafe(out bool contact);
            Validate(sender, index, sequence, restart, stamp, at, feet, height, contact);
        }

        internal bool Validate(ulong sender, int index, uint sequence, int restart, double stamp, double at,
            Vector3 feet, float height, bool contact)
        {
            double now = session != null && session.NetworkManager != null ? session.NetworkManager.ServerTime.Time : clock;
            if (!active || session == null || !session.IsHost || index < 0 || index >= count()
                || !double.IsFinite(stamp) || stamp < 0 || stamp != epoch(index) || !double.IsFinite(at)
                || at < minimumAt || at < stamp || at > now + .05 || now - at > 1.25
                || !float.IsFinite(feet.x) || !float.IsFinite(feet.y) || !float.IsFinite(feet.z)
                || !float.IsFinite(height) || height < .1f
                || received.TryGetValue(sender, out uint last) && unchecked((int)(sequence - last)) <= 0) return false;
            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All)
                if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == sender) { player = candidate; break; }
            if (player == null || player.State != PlayerState.Normal || player.RestartVersion != restart
                || NetworkPlayer.ServerCanChangeState != null && !NetworkPlayer.ServerCanChangeState(player, PlayerState.Fallen)
                || !player.TryGetComponent(out PlayerMover mover)
                || height > Mathf.Max(mover.BodyHeight, GameSettings.Base.Character.CapsuleHeight) + .05f) return false;
            var key = (sender, restart, index, stamp);
            if (!contact) { received[sender] = sequence; confirmed.Remove(key); return true; }
            float allowance = 1 + 30 * (float)Math.Max(0, now - at + .1);
            if ((feet - player.transform.position).sqrMagnitude > allowance * allowance
                || !test(index, at, feet, height, mover.BodyRadius)) return false;
            received[sender] = sequence;
            if (confirmed.Add(key)) confirm(player, index);
            return true;
        }

        internal void Reset()
        {
            localTouching.Clear(); nextTouching.Clear(); confirmed.Clear(); received.Clear();
            previousAt = -1; retryAt = 0; minimumAt = clock;
        }
        public void Dispose()
        {
            if (trackedMover != null) trackedMover.Teleported -= Teleported;
            trackedMover = null;
            if (boundServer) messaging?.UnregisterNamedMessageHandler(message);
            messaging = null; session = null; active = wasActive = false; Reset();
        }
        void Teleported() => previousAt = -1;
    }
}
