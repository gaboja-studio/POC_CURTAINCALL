using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// 인간 대포 공개 진입점. 같은 씬의 stations 순서를 모든 화면이 공유한다.
    /// 기존 목마와 같은 NGO 이름 붙은 메시지로 요청하고 호스트만 점유/발사/취소를 확정한다.
    /// 공용 Player/NetworkManager 프리팹은 변경하지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed partial class HumanCannonSystem : MonoBehaviour
    {
        const string RequestMessage = "CurtainCall.HumanCannon.Request";
        const string StateMessage = "CurtainCall.HumanCannon.State";
        const int RequestSize = 25;
        const int StateSize = 65;

        [Tooltip("대포 목록. 네 화면에서 같은 씬·같은 순서여야 한다.")]
        [SerializeField] HumanCannonStation[] stations = Array.Empty<HumanCannonStation>();

        sealed class SeatRuntime
        {
            public CannonSeatState State = new();
            public Vector3 ReturnPosition;
            public Quaternion ReturnRotation;
        }

        SeatRuntime[] seats;
        readonly CannonFlightState flying = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        HumanCannonPilot pilot;
        NetworkPlayer boundPlayer;

        public static HumanCannonSystem Current { get; private set; }
        public event Action<int, CannonAction, ulong> StateChanged;
        public int StationCount => stations.Length;
        public HumanCannonStation Station(int index) => stations[index];
        public ulong Occupant(int index) => seats[index].State.Occupant;
        public bool IsFlying(ulong playerId) => flying.Contains(playerId);

        public bool IsSeated(ulong playerId)
        {
            foreach (var seat in seats) if (seat.State.Occupant == playerId) return true;
            return false;
        }

        internal bool ApplyRebound(ulong actor, int index, uint revision, uint segment, Vector3 position, float upwardSpeed)
        {
            if (HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(actor) >= 0) return false;
            if (!flying.TryRebound(actor, index, revision, segment)) return false;
            if (boundPlayer != null && actor == boundPlayer.OwnerClientId && pilot != null)
                pilot.Rebound(index, revision, segment, position, upwardSpeed);
            return true;
        }

        internal void RejectRebound(int index, uint revision, uint segment)
        {
            if (pilot != null) pilot.RejectRebound(index, revision, segment);
        }

        internal bool BeginGroundBounce(ulong actor, uint sequence, uint epoch, Vector3 contact, float speed, float controlSpeed)
        {
            if (IsFlying(actor) || IsSeated(actor)) return false;
            if (boundPlayer != null && actor == boundPlayer.OwnerClientId)
            {
                if (pilot == null || !pilot.BeginGroundBounce(sequence, epoch, contact, speed, controlSpeed)) return false;
            }
            // -1은 대포 좌석 없이 지상 점프로 시작한 비행이다. 이후 반동/착지는 같은 식별자로 검증한다.
            flying.Begin(actor, -1, sequence);
            return true;
        }

        internal void RejectGroundBounce(uint sequence, uint epoch)
        {
            if (pilot != null) pilot.RejectGroundBounce(sequence, epoch);
        }

        internal void StartCarrying(ulong actor)
        {
            flying.Remove(actor);
            if (boundPlayer != null && actor == boundPlayer.OwnerClientId && pilot != null)
                pilot.StartCarrying();
        }

        void OnEnable()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError("[HumanCannon] 씬에 HumanCannonSystem은 하나만 둡니다.", this);
                enabled = false;
                return;
            }
            Current = this;
            CreateInjuryPolicy();
            ResetSeats();
            NetworkPlayer.LocalSpawned += Bind;
            NetworkPlayer.LocalDespawned += Unbind;
            NetworkPlayer.Restarted += HandleRestart;
            if (NetworkPlayer.Local != null) Bind(NetworkPlayer.Local);
        }

        void ResetSeats()
        {
            awaitingFlightSnapshot = false;
            injuryInteractions.Clear();
            LocalSafeLandingCount = 0;
            seats = new SeatRuntime[stations.Length];
            for (int i = 0; i < seats.Length; i++) seats[i] = new SeatRuntime();
            flying.Clear();
            if (pilot != null) pilot.ReleaseMovement();
        }

        void Update()
        {
            UpdateInjuryPolicies();
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += HandleConnection;
                HandleConnection(session.ConnectionState);
            }
            if (session == null) return;
            if (session.IsHost)
            {
                injuryInteractions.Tick(Time.timeAsDouble);
                for (int i = 0; i < seats.Length; i++)
                {
                    var state = seats[i].State;
                    if (!state.IsOccupied) continue;
                    var player = FindPlayer(state.Occupant);
                    if (ValidIndex(i) && player != null && player.State == PlayerState.Normal) continue;
                    ulong actor = state.Occupant;
                    state.TryApply(CannonAction.Release, actor, state.Revision);
                    Publish(i, CannonAction.Release, actor, Vector3.zero, Quaternion.identity, Vector3.zero);
                }
            }
            PruneFlights();
        }

        void LateUpdate()
        {
            // 원격 화면은 보간보다 좌석 고정을 우선한다. 로컬 이동/발사는 소유자만 계산한다.
            for (int i = 0; i < seats.Length; i++)
            {
                if (stations[i] == null || !seats[i].State.IsOccupied) continue;
                var player = FindPlayer(seats[i].State.Occupant);
                if (player == null || player.IsLocal || player.State != PlayerState.Normal) continue;
                player.transform.SetPositionAndRotation(stations[i].Seat.position, stations[i].Seat.rotation);
            }
        }

        public int FindBoardableStation(NetworkPlayer player)
        {
            if (player == null || player.State != PlayerState.Normal || player.IsAirborne || IsFlying(player.OwnerClientId)) return -1;
            if (HumanCannonTrampoline.Current != null && HumanCannonTrampoline.Current.Carrier == player.OwnerClientId) return -1;
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].State.Occupant == player.OwnerClientId) return -1;
            int nearest = -1;
            float best = float.PositiveInfinity;
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i];
                if (station == null || !station.isActiveAndEnabled || seats[i].State.IsOccupied) continue;
                float distance = (station.BoardingPoint.position - player.transform.position).sqrMagnitude;
                float radius = station.BoardingDistance * reach;
                if (radius <= 0f || distance > radius * radius || distance >= best) continue;
                nearest = i;
                best = distance;
            }
            return nearest;
        }

        /// <summary>로컬 입력 요청. 발사/취소는 내가 받은 점유 revision과 함께 호스트가 다시 확인한다.</summary>
        public void Request(int index, CannonAction action)
        {
            if (!ValidIndex(index) || boundPlayer == null || boundPlayer.State != PlayerState.Normal || InputGate.IsBlocked) return;
            if (action != CannonAction.Board && action != CannonAction.Launch && action != CannonAction.Cancel) return;
            SendRequest(index, action, seats[index].State.Revision);
        }

        internal void RequestLanding(int index, uint launchRevision, uint segment, Vector3 position)
        {
            if (boundPlayer == null || boundPlayer.State != PlayerState.Normal) return;
            // 물리 착지는 입력 잠금 중에도 처리하되, 발사 때 받은 식별자를 그대로 보낸다.
            SendRequest(index, CannonAction.Land, launchRevision, segment, position);
        }

        void SendRequest(int index, CannonAction action, uint revision, uint segment = 0, Vector3 position = default)
        {
            if (session != null && session.IsHost)
                HandleRequest(boundPlayer.OwnerClientId, index, action, revision, segment, position);
            else if (messaging != null)
            {
                using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
                writer.WriteValueSafe((byte)action);
                writer.WriteValueSafe(index);
                writer.WriteValueSafe(revision);
                writer.WriteValueSafe(segment);
                writer.WriteValueSafe(position);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        void HandleRequestMessage(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length - reader.Position != RequestSize) return;
            reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out Vector3 position);
            if ((CannonAction)action == CannonAction.Sync)
            {
                for (int i = 0; i < seats.Length; i++) SendState(sender, i, CannonAction.Sync, CannonSeatState.Nobody,
                    Vector3.zero, Quaternion.identity, Vector3.zero);
                SendFlightSnapshot(sender);
                return;
            }
            HandleRequest(sender, index, (CannonAction)action, revision, segment, position);
        }

        void HandleRequest(ulong actor, int index, CannonAction action, uint revision, uint segment, Vector3 landingPosition,
            bool interactionReady = false)
        {
            var player = FindPlayer(actor); // actor는 클라이언트가 보낸 값이 아니라 NGO의 실제 발신자다.
            if (player == null || player.State != PlayerState.Normal) return;
            if (action == CannonAction.Land)
            {
                HandleLanding(player, index, revision, segment, landingPosition);
                return;
            }
            if (!ValidIndex(index) || (action != CannonAction.Board && action != CannonAction.Launch && action != CannonAction.Cancel)) return;
            var runtime = seats[index];
            var station = stations[index];
            if (action == CannonAction.Board && FindBoardableStation(player) != index) return;
            if (action == CannonAction.Board && !interactionReady)
            {
                if (runtime.State.Revision != revision) return;
                if (DelayInjuredInteraction(player, index, revision,
                    () => player != null && player.IsSpawned && player.State == PlayerState.Normal
                        && ValidIndex(index) && seats[index].State.Revision == revision && FindBoardableStation(player) == index,
                    () => HandleRequest(actor, index, action, revision, segment, landingPosition, true))) return;
            }
            if (!runtime.State.TryApply(action, actor, revision)) return;

            Vector3 position = station.Seat.position;
            Quaternion rotation = station.Seat.rotation;
            Vector3 velocity = Vector3.zero;
            if (action == CannonAction.Board)
            {
                runtime.ReturnPosition = player.transform.position;
                runtime.ReturnRotation = player.transform.rotation;
            }
            else if (action == CannonAction.Cancel)
            {
                position = runtime.ReturnPosition;
                rotation = runtime.ReturnRotation;
            }
            else if (action == CannonAction.Launch)
            {
                position = station.Muzzle.position;
                rotation = station.Muzzle.rotation;
                velocity = station.LaunchVelocity;
            }
            Publish(index, action, actor, position, rotation, velocity);
        }

        void Publish(int index, CannonAction action, ulong actor, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            Apply(index, action, actor, position, rotation, velocity);
            foreach (ulong id in session.NetworkManager.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId) SendState(id, index, action, actor, position, rotation, velocity);
        }

        void SendState(ulong client, int index, CannonAction action, ulong actor, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            using var writer = new FastBufferWriter(StateSize, Allocator.Temp);
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe(index);
            writer.WriteValueSafe(seats[index].State.Revision);
            writer.WriteValueSafe(actor);
            writer.WriteValueSafe(seats[index].State.Occupant);
            writer.WriteValueSafe(position);
            writer.WriteValueSafe(rotation);
            writer.WriteValueSafe(velocity);
            messaging.SendNamedMessage(StateMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleStateMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != StateSize) return;
            reader.ReadValueSafe(out byte rawAction);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out ulong occupant);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Quaternion rotation);
            reader.ReadValueSafe(out Vector3 velocity);
            // 대포가 꺼지거나 제거된 뒤에도 호스트의 점유 해제는 받아야 한다.
            if (index < 0 || index >= seats.Length) return;
            var action = (CannonAction)rawAction;
            if (action != CannonAction.Sync && revision <= seats[index].State.Revision) return;
            if (!seats[index].State.Receive(revision, occupant)) return;
            Apply(index, action, actor, position, rotation, velocity);
        }

        void Apply(int index, CannonAction action, ulong actor, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (action == CannonAction.Reset)
            {
                // Restart 알림과 이전 발사 응답의 도착 순서가 달라도 최종 상태는 초기화다.
                flying.Clear();
                awaitingFlightSnapshot = false;
                if (pilot != null) pilot.ResetAfterRestart();
                StateChanged?.Invoke(index, action, actor);
                return;
            }
            if (action == CannonAction.Launch) flying.Begin(actor, index, seats[index].State.Revision);
            if (action == CannonAction.Release) flying.Remove(actor);
            if (boundPlayer != null && pilot != null)
            {
                if (seats[index].State.Occupant == boundPlayer.OwnerClientId && ValidIndex(index))
                    pilot.SeatAt(index);
                else if (actor == boundPlayer.OwnerClientId)
                {
                    if (action == CannonAction.Launch) pilot.Launch(index, seats[index].State.Revision, position, rotation, velocity);
                    else if (action == CannonAction.Cancel) pilot.Cancel(position, rotation);
                    else if (action == CannonAction.Release) pilot.ReleaseMovement();
                }
            }
            StateChanged?.Invoke(index, action, actor);
        }

        void Bind(NetworkPlayer local)
        {
            if (boundPlayer == local) return;
            Unbind();
            boundPlayer = local;
            BindInjuryPolicy(local);
            pilot = local.gameObject.AddComponent<HumanCannonPilot>();
            pilot.Initialize(this, local);
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].State.Occupant == local.OwnerClientId) pilot.SeatAt(i);
        }

        void Unbind()
        {
            if (pilot != null) { pilot.Detach(); Destroy(pilot); }
            pilot = null;
            boundPlayer = null;
        }

        void HandleRestart(NetworkPlayer player)
        {
            awaitingFlightSnapshot = false;
            injuryInteractions.Clear();
            flying.Remove(player.OwnerClientId);
            if (player == boundPlayer)
            {
                LocalSafeLandingCount = 0;
                if (pilot != null) pilot.ReleaseMovement();
                // 다른 참가자가 재시작을 요청한 화면에서도 남아 있던 F/이동 입력을 버린다.
                InputGate.RequireRelease();
            }
            if (session == null || !session.IsHost) return;
            for (int i = 0; i < seats.Length; i++)
            {
                var state = seats[i].State;
                state.Reset();
                Publish(i, CannonAction.Reset, CannonSeatState.Nobody, Vector3.zero, Quaternion.identity, Vector3.zero);
            }
        }

        void HandleConnection(SessionConnectionState state)
        {
            Unregister();
            if (state == SessionConnectionState.Offline) { ResetSeats(); return; }
            if (state != SessionConnectionState.Host && state != SessionConnectionState.Client) return;
            messaging = session.NetworkManager.CustomMessagingManager;
            if (state == SessionConnectionState.Host)
                messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequestMessage);
            else
            {
                messaging.RegisterNamedMessageHandler(StateMessage, HandleStateMessage);
                messaging.RegisterNamedMessageHandler(LandingMessage, HandleLandingMessage);
                messaging.RegisterNamedMessageHandler(FlightSnapshotMessage, HandleFlightSnapshot);
                messaging.RegisterNamedMessageHandler(FlightRemovedMessage, HandleFlightRemoved);
                awaitingFlightSnapshot = true;
                using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
                writer.WriteValueSafe((byte)CannonAction.Sync);
                writer.WriteValueSafe(-1);
                writer.WriteValueSafe(0u);
                writer.WriteValueSafe(0u);
                writer.WriteValueSafe(Vector3.zero);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        bool ValidIndex(int index) => index >= 0 && index < stations.Length && stations[index] != null && stations[index].isActiveAndEnabled;

        static NetworkPlayer FindPlayer(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id) return player;
            return null;
        }

        void Unregister()
        {
            awaitingFlightSnapshot = false;
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(LandingMessage);
            messaging.UnregisterNamedMessageHandler(FlightSnapshotMessage);
            messaging.UnregisterNamedMessageHandler(FlightRemovedMessage);
            messaging = null;
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Bind;
            NetworkPlayer.LocalDespawned -= Unbind;
            NetworkPlayer.Restarted -= HandleRestart;
            Unbind();
            ReleaseInjuryPolicies();
            Unregister();
            if (session != null) session.ConnectionStateChanged -= HandleConnection;
            session = null;
            if (Current == this) Current = null;
        }
    }
}
