using System;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Voice
{
    /// <summary>
    /// 같은 방 사람끼리 같은 음성 채널에 들어가도록 모두가 같게 아는 방 키를 정한다.
    /// Services 세션 = 세션 Id. LAN(세팅으로 켬) = 호스트가 만든 키를 NGO named message로 요청·응답.
    /// <see cref="VoiceChat"/> 내부용.
    /// </summary>
    sealed class VoiceRoomKey
    {
        const string MessageName = "CurtainCall.Voice.RoomKey";

        NetworkManager _registered;
        string _lanKey;
        bool _requested;

        /// <summary>지금 방 키. 아직 모르거나 음성을 쓰지 않는 접속이면 null.</summary>
        public string Resolve(NetworkSessionManager session, bool allowLan)
        {
            if (session == null || session.ConnectionState is not (SessionConnectionState.Host or SessionConnectionState.Client))
            {
                Reset();
                return null;
            }

            if (session.ActiveConnector is ServicesSessionConnector services)
            {
                Reset();
                return services.CurrentSession?.Id;
            }

            var network = session.NetworkManager;
            if (!allowLan || network == null || !network.IsListening || network.CustomMessagingManager == null)
            {
                Reset();
                return null;
            }

            if (_registered != network)
            {
                Reset();
                network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, HandleMessage);
                _registered = network;
            }

            if (network.IsServer)
                return _lanKey ??= Guid.NewGuid().ToString("N");

            if (!_requested && network.IsConnectedClient)
            {
                _requested = true;
                Send(network, NetworkManager.ServerClientId, string.Empty);
            }
            return _lanKey;
        }

        /// <summary>등록한 메시지를 풀고 키를 잊는다(접속이 끝났거나 접속 방식이 바뀜).</summary>
        public void Reset()
        {
            if (_registered != null && _registered.CustomMessagingManager != null)
                _registered.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            _registered = null;
            _lanKey = null;
            _requested = false;
        }

        void HandleMessage(ulong senderId, FastBufferReader reader)
        {
            var network = _registered;
            if (network == null) return;

            if (network.IsServer)
            {
                // 참가자의 요청 → 호스트 키로 답한다.
                Send(network, senderId, _lanKey ??= Guid.NewGuid().ToString("N"));
                return;
            }

            reader.ReadValueSafe(out string key);
            if (!string.IsNullOrEmpty(key))
            {
                _lanKey = key;
                Debug.Log("[Voice] LAN 방 키를 받았습니다.");
            }
        }

        static void Send(NetworkManager network, ulong targetId, string key)
        {
            using var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(key), Allocator.Temp);
            writer.WriteValueSafe(key);
            network.CustomMessagingManager.SendNamedMessage(MessageName, targetId, writer, NetworkDelivery.Reliable);
        }
    }
}
