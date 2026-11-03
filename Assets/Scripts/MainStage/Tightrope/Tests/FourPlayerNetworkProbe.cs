#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;

namespace CurtainCall.Tightrope.Tests
{
    /// <summary>Opt-in, real multi-process LAN A/B capture. Never enabled in a player build.</summary>
    public sealed class FourPlayerNetworkProbe : MonoBehaviour
    {
        static FourPlayerNetworkProbe current;
        readonly List<Frame> frames = new(12000);
        readonly List<Pose> poses = new(48000);
        string label;
        double until, started, cpuStarted;
        UnityTransport transport;
        bool completed;
        uint delay, jitter;
        float loss;
        bool unreliable;
        readonly List<(NetworkTransform transform, bool unreliable)> originalTransforms = new();

        [Serializable] public struct Frame { public long utcTicks; public float ms; public ulong rtt; public int players; }
        [Serializable] public struct Pose { public long utcTicks; public ulong owner; public bool local; public Vector3 position; public int state, lostParts; }
        [Serializable] public class Result {
            public string label; public int pid; public ulong localId; public bool host, unreliable;
            public uint oneWayDelayMs, jitterMs; public float sendLossPercent;
            public double seconds, cpuSeconds;
            public Frame[] frames; public Pose[] poses;
        }

        public static bool Begin(string label, float seconds, bool unreliable, uint delayMs, uint jitterMs, float lossPercent)
        {
            var session = NetworkSessionManager.Instance;
            if (!Application.isPlaying || current != null || session == null || session.PlayerCount != 4
                || !float.IsFinite(seconds) || seconds < 5 || seconds > 120 || string.IsNullOrEmpty(label)
                || label.Any(c => !char.IsLetterOrDigit(c) && c != '-') || delayMs > 2000 || jitterMs > 2000
                || !float.IsFinite(lossPercent) || lossPercent < 0 || lossPercent > 100) return false;
            var manager = session.NetworkManager;
            var transport = manager.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null || !transport.GetNetworkDriver().IsCreated) return false;
            transport.GetNetworkDriver().ModifyNetworkSimulatorParameters(new NetworkSimulatorParameter {
                SendDelayMS = delayMs, SendJitterMS = jitterMs, SendPacketLossPercent = lossPercent
            });
            var go = new GameObject("Four-player network measurement") { hideFlags = HideFlags.DontSave };
            current = go.AddComponent<FourPlayerNetworkProbe>();
            foreach (var player in NetworkPlayer.All)
                if (player.IsOwner && player.TryGetComponent(out NetworkTransform transform)) {
                    current.originalTransforms.Add((transform, transform.UseUnreliableDeltas));
                    transform.UseUnreliableDeltas = unreliable;
                }
            current.transport = transport; current.label = label; current.unreliable = unreliable;
            current.delay = delayMs; current.jitter = jitterMs; current.loss = lossPercent;
            current.started = Time.realtimeSinceStartupAsDouble; current.until = current.started + seconds;
            current.cpuStarted = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
            return true;
        }

        void LateUpdate()
        {
            if (completed) return;
            var manager = NetworkSessionManager.Instance?.NetworkManager;
            if (manager == null || !manager.IsListening || Time.realtimeSinceStartupAsDouble >= until) { Finish(); return; }
            long utc = DateTime.UtcNow.Ticks;
            frames.Add(new Frame { utcTicks = utc, ms = Time.unscaledDeltaTime * 1000,
                rtt = manager.IsHost ? 0 : transport.GetCurrentRtt(NetworkManager.ServerClientId),
                players = NetworkSessionManager.Instance.PlayerCount });
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned) poses.Add(new Pose { utcTicks = utc,
                    owner = player.OwnerClientId, local = player.IsOwner, position = player.transform.position,
                    state = (int)player.State, lostParts = (int)player.LostParts });
        }

        void Finish()
        {
            if (completed) return;
            completed = true;
            var manager = NetworkSessionManager.Instance?.NetworkManager;
            var result = new Result { label = label, pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                localId = manager != null ? manager.LocalClientId : ulong.MaxValue, host = manager != null && manager.IsHost,
                unreliable = unreliable, oneWayDelayMs = delay, jitterMs = jitter, sendLossPercent = loss,
                seconds = Time.realtimeSinceStartupAsDouble - started,
                cpuSeconds = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds - cpuStarted,
                frames = frames.ToArray(), poses = poses.ToArray() };
            string directory = Path.Combine(Application.dataPath, "../Logs/FourPlayer");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, label + "-" + result.pid + ".json"), JsonUtility.ToJson(result));
            if (transport != null && transport.GetNetworkDriver().IsCreated)
                transport.GetNetworkDriver().ModifyNetworkSimulatorParameters(default);
            foreach (var original in originalTransforms)
                if (original.transform != null) original.transform.UseUnreliableDeltas = original.unreliable;
            current = null; Destroy(gameObject);
        }
        void OnDestroy() { if (!completed && current == this) Finish(); }
    }
}
#endif
