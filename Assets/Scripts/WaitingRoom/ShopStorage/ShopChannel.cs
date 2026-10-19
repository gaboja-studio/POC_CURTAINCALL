using System;
using System.Collections.Generic;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 상점·기물 소지가 함께 쓰는 이름 메시지 연결(공용 보상·런 진행과 같은 방식).
    /// 참가자 → 호스트: 요청. 호스트 → 모두/한 명: 상태, 호스트 → 요청자: 결과. 내용은 JSON 문자열.
    /// 방이 바뀌면 <c>reset</c>을 부르고, 참가자는 상태를 받을 때까지 1초, 받은 뒤에는 10초마다 <c>requestSync</c>를 부른다.
    /// </summary>
    sealed class ShopChannel : IDisposable
    {
        const int MaxPayloadBytes = 16 * 1024;
        const int MaxRequestBytes = 1024;

        readonly string stateName, requestName, resultName;
        readonly Action<ulong, string> onRequest;
        readonly Action<string> onState, onResult;
        readonly Action reset, requestSync;
        readonly Dictionary<ulong, float> requestTimes = new();
        CustomMessagingManager messaging;
        bool messagingHost;
        bool synced;
        float nextSync;

        public ShopChannel(string prefix, Action<ulong, string> onRequest, Action<string> onState, Action<string> onResult,
            Action reset, Action requestSync)
        {
            stateName = prefix + ".State";
            requestName = prefix + ".Request";
            resultName = prefix + ".Result";
            this.onRequest = onRequest;
            this.onState = onState;
            this.onResult = onResult;
            this.reset = reset;
            this.requestSync = requestSync;
        }

        public NetworkSessionManager Session { get; private set; }

        /// <summary>참가자로 접속 중.</summary>
        public bool IsClient => Session != null && Session.ConnectionState == SessionConnectionState.Client;

        /// <summary>호스트로 방을 열었다.</summary>
        public bool IsHost => Session != null && Session.IsHost;

        /// <summary>내 접속 번호. 접속하지 않았으면 호스트 번호(0).</summary>
        public ulong LocalClientId => Session != null && Session.NetworkManager != null && Session.NetworkManager.IsListening
            ? Session.NetworkManager.LocalClientId : NetworkManager.ServerClientId;

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
                requestSync?.Invoke();
            }
        }

        public void Dispose()
        {
            Unregister();
            if (Session != null) Session.ConnectionStateChanged -= HandleConnection;
        }

        /// <summary>참가자: 호스트에 요청을 보낸다.</summary>
        public bool Request(string json) => IsClient && Write(requestName, json, NetworkManager.ServerClientId);

        /// <summary>호스트: 상태를 모두에게(또는 한 명에게) 보낸다.</summary>
        public bool SendState(string json, ulong? target = null) => IsHost && Write(stateName, json, target);

        /// <summary>호스트: 요청자에게 결과를 보낸다.</summary>
        public bool SendResult(ulong target, string json) => IsHost && Write(resultName, json, target);

        public bool IsConnected(ulong clientId)
        {
            var manager = Session?.NetworkManager;
            return manager != null && manager.ConnectedClients.ContainsKey(clientId);
        }

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
            if (host) messaging.RegisterNamedMessageHandler(requestName, HandleRequest);
            else
            {
                messaging.RegisterNamedMessageHandler(stateName, HandleState);
                messaging.RegisterNamedMessageHandler(resultName, HandleResult);
                synced = false;
                nextSync = 0;
            }
        }

        void Unregister()
        {
            if (messaging == null) return;
            if (messagingHost) messaging.UnregisterNamedMessageHandler(requestName);
            else
            {
                messaging.UnregisterNamedMessageHandler(stateName);
                messaging.UnregisterNamedMessageHandler(resultName);
            }
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
            if (size > MaxPayloadBytes) { Debug.LogError($"[Shop] {message} 메시지가 상한을 넘었습니다."); return false; }
            try
            {
                using var writer = new FastBufferWriter(size, Allocator.Temp);
                writer.WriteValueSafe(json);
                if (target.HasValue) messaging.SendNamedMessage(message, target.Value, writer, NetworkDelivery.ReliableSequenced);
                else messaging.SendNamedMessageToAll(message, writer, NetworkDelivery.ReliableSequenced);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return false;
            }
        }

        static bool InvalidMessage(Exception error) =>
            error is OverflowException || error is ArgumentException || error is IndexOutOfRangeException;

        static bool TryRead(FastBufferReader reader, int limit, out string json)
        {
            json = null;
            if (reader.Length > limit) return false;
            try
            {
                reader.ReadValueSafe(out json);
                return true;
            }
            catch (Exception error) when (InvalidMessage(error))
            {
                Debug.LogWarning("[Shop] 잘못된 메시지를 무시했습니다.");
                return false;
            }
        }

        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            if (!IsConnected(sender)) return;
            if (requestTimes.TryGetValue(sender, out float last) && Time.unscaledTime - last < .1f) return;
            requestTimes[sender] = Time.unscaledTime;
            if (TryRead(reader, MaxRequestBytes, out string json)) Guard(() => onRequest(sender, json));
        }

        void HandleState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || !TryRead(reader, MaxPayloadBytes, out string json)) return;
            synced = true;
            Guard(() => onState(json));
        }

        void HandleResult(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || !TryRead(reader, MaxRequestBytes, out string json)) return;
            Guard(() => onResult(json));
        }

        // JSON이 깨져 있어도 메시지 처리기가 예외로 끊기지 않게 한다.
        static void Guard(Action action)
        {
            try { action(); }
            catch (Exception error) when (InvalidMessage(error)) { Debug.LogWarning("[Shop] 잘못된 메시지 내용을 무시했습니다."); }
        }
    }
}
