using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 코스 값을 호스트 기준으로 맞춘다. 게임 시작 전(대기)에는 호스트가 인스펙터 값을 바꿀 때마다 모두에게 보내고,
    /// 클라이언트는 접속하면 현재 값을 요청해 받는다. 클라이언트는 늘 호스트 값만 쓰고(잠금),
    /// 게임이 시작되면(진행·종료) 호스트도 잠가 값을 고정한다. 접속을 끊으면 잠금을 푼다.
    /// 코스는 네트워크 오브젝트가 아니므로 NGO 이름 붙은 메시지로 보낸다. 코스 프리팹의 <see cref="TightropeCourse"/>와 같은 오브젝트에 둔다.
    /// </summary>
    [RequireComponent(typeof(TightropeCourse))]
    public sealed class CourseShapeSync : MonoBehaviour
    {
        const string ShapeMessage = "CurtainCall.Tightrope.CourseShape";
        const string RequestMessage = "CurtainCall.Tightrope.CourseShapeRequest";
        const int MaxMessageBytes = 64 * 1024;

        TightropeCourse course;
        NetworkSessionManager session;
        CustomMessagingManager messaging; // 핸들러를 등록한 곳(접속마다 새로 생긴다)

        void Awake() => course = GetComponent<TightropeCourse>();

        void OnEnable() => course.Rebuilt += HandleRebuilt;

        void OnDisable()
        {
            course.Rebuilt -= HandleRebuilt;
            Unregister();
            if (session != null)
            {
                session.ConnectionStateChanged -= HandleConnectionState;
                session.GameStateChanged -= HandleGameState;
                session = null;
            }
        }

        void Update()
        {
            // 세션 매니저는 접속 UI가 나중에 만들 수 있으므로 생길 때 붙는다
            if (session != null || NetworkSessionManager.Instance == null) return;
            session = NetworkSessionManager.Instance;
            session.ConnectionStateChanged += HandleConnectionState;
            session.GameStateChanged += HandleGameState;
            HandleConnectionState(session.ConnectionState);
        }

        void HandleConnectionState(SessionConnectionState state)
        {
            switch (state)
            {
                case SessionConnectionState.Host:
                    Register(RequestMessage, HandleRequest);
                    UpdateLock();
                    Broadcast();
                    break;
                case SessionConnectionState.Client:
                    Register(ShapeMessage, HandleShape);
                    UpdateLock();
                    SendRequest();
                    break;
                case SessionConnectionState.Offline:
                    Unregister();
                    course.SetLocked(false);
                    break;
            }
        }

        void HandleGameState(GameSessionState state) => UpdateLock();

        void UpdateLock()
        {
            if (session == null) return;
            if (session.ConnectionState == SessionConnectionState.Client)
                course.SetLocked(true, "클라이언트는 호스트의 코스 값을 씁니다.");
            else if (session.ConnectionState == SessionConnectionState.Host && session.GameState != GameSessionState.Waiting)
                course.SetLocked(true, "게임이 시작되어 코스 값이 고정됐습니다.");
            else
                course.SetLocked(false);
        }

        /// <summary>호스트: 대기 중에 코스 값이 바뀌면 모두에게 보낸다.</summary>
        void HandleRebuilt()
        {
            if (session != null && session.IsHost && session.GameState == GameSessionState.Waiting)
                Broadcast();
        }

        void Broadcast()
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = WriteShape();
            manager.SendNamedMessageToAll(ShapeMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        /// <summary>호스트: 클라이언트의 요청에 현재 값을 보낸다.</summary>
        void HandleRequest(ulong senderId, FastBufferReader reader)
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = WriteShape();
            manager.SendNamedMessage(ShapeMessage, senderId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        void SendRequest()
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            manager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer);
        }

        /// <summary>클라이언트: 호스트가 보낸 값으로 코스를 다시 만든다.</summary>
        void HandleShape(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string json);
            course.ImportShape(json);
        }

        FastBufferWriter WriteShape()
        {
            var writer = new FastBufferWriter(1024, Allocator.Temp, MaxMessageBytes);
            writer.WriteValueSafe(course.ExportShape());
            return writer;
        }

        CustomMessagingManager Messaging =>
            session != null && session.NetworkManager != null ? session.NetworkManager.CustomMessagingManager : null;

        void Register(string message, CustomMessagingManager.HandleNamedMessageDelegate handler)
        {
            Unregister();
            messaging = Messaging;
            messaging?.RegisterNamedMessageHandler(message, handler);
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(ShapeMessage);
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging = null;
        }
    }
}
