using System;
using System.Collections.Generic;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 스케줄표 이름 메시지 연결. 편집·확정은 호스트만 하므로 메시지는 두 가지뿐이다.
    /// 호스트 → 모두/한 명: 상태(JSON). 참가자 → 호스트: 동기화 요청(상태를 받을 때까지 1초, 받은 뒤 10초마다).
    /// 분할 전송(<see cref="NetworkDelivery.ReliableFragmentedSequenced"/>)이라 조각 없는 전송 크기 상한(약 1.3KB)에 걸리지 않는다.
    /// 방이 바뀌면 <c>reset</c>을 부른다.
    /// </summary>
    sealed class ScheduleChannel : IDisposable
    {
        const int MaxPayloadBytes = 16 * 1024;
        const float MinRequestGap = .1f;

        readonly string stateName, syncName;
        readonly Action<string> onState;
        readonly Action<ulong> onSyncRequest;
        readonly Action reset;
        readonly Dictionary<ulong, float> requestTimes = new();
        CustomMessagingManager messaging;
        bool messagingHost;
        bool synced;
        float nextSync;

        public ScheduleChannel(string prefix, Action<string> onState, Action<ulong> onSyncRequest, Action reset)
        {
            stateName = prefix + ".State";
            syncName = prefix + ".Sync";
            this.onState = onState;
            this.onSyncRequest = onSyncRequest;
            this.reset = reset;
        }

        public NetworkSessionManager Session { get; private set; }

        /// <summary>참가자로 접속 중.</summary>
        public bool IsClient => Session != null && Session.ConnectionState == SessionConnectionState.Client;

        /// <summary>호스트로 방을 열었다.</summary>
        public bool IsHost => Session != null && Session.IsHost;

        public void Update()
        {
            if (Session == null)
            {
                Session = NetworkSessionManager.EnsureExists();
                if (Session != null) Session.ConnectionStateChanged += HandleConnection;
            }
            Bind();
            if (IsClient && messaging != null && Time.unscaledTime >= nextSync)
            {
                nextSync = Time.unscaledTime + (synced ? 10f : 1f);
                Write(syncName, "", NetworkManager.ServerClientId);
            }
        }

        public void Dispose()
        {
            Unregister();
            if (Session != null) Session.ConnectionStateChanged -= HandleConnection;
        }

        /// <summary>호스트: 상태를 모두에게(또는 한 명에게) 보낸다.</summary>
        public bool SendState(string json, ulong? target = null) => IsHost && Write(stateName, json, target);

        void Bind()
        {
            var next = Session != null && (Session.ConnectionState == SessionConnectionState.Host
                || Session.ConnectionState == SessionConnectionState.Client) ? Session.NetworkManager?.CustomMessagingManager : null;
            bool host = IsHost;
            if (next == messaging && messagingHost == host) return;
            Unregister();
            messaging = next;
            messagingHost = host;
            if (messaging == null) return;
            if (host) messaging.RegisterNamedMessageHandler(syncName, HandleSync);
            else
            {
                messaging.RegisterNamedMessageHandler(stateName, HandleState);
                synced = false;
                nextSync = 0;
            }
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(messagingHost ? syncName : stateName);
            messaging = null;
        }

        void HandleConnection(SessionConnectionState connection)
        {
            requestTimes.Clear();
            synced = false;
            reset?.Invoke();
        }

        bool Write(string message, string json, ulong? target)
        {
            if (messaging == null) return false;
            int size = FastBufferWriter.GetWriteSize(json);
            if (size > MaxPayloadBytes) { Debug.LogError($"[NightSchedule] {message} 메시지가 상한을 넘었습니다."); return false; }
            try
            {
                using var writer = new FastBufferWriter(size, Allocator.Temp);
                writer.WriteValueSafe(json);
                if (target.HasValue) messaging.SendNamedMessage(message, target.Value, writer, NetworkDelivery.ReliableFragmentedSequenced);
                else messaging.SendNamedMessageToAll(message, writer, NetworkDelivery.ReliableFragmentedSequenced);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return false;
            }
        }

        void HandleSync(ulong sender, FastBufferReader reader)
        {
            var manager = Session?.NetworkManager;
            if (manager == null || !manager.ConnectedClients.ContainsKey(sender)) return;
            if (requestTimes.TryGetValue(sender, out float last) && Time.unscaledTime - last < MinRequestGap) return;
            requestTimes[sender] = Time.unscaledTime;
            onSyncRequest?.Invoke(sender);
        }

        void HandleState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length > MaxPayloadBytes) return;
            try
            {
                reader.ReadValueSafe(out string json);
                synced = true;
                onState?.Invoke(json);
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException || error is IndexOutOfRangeException)
            {
                Debug.LogWarning("[NightSchedule] 잘못된 메시지를 무시했습니다.");
            }
        }
    }
}
