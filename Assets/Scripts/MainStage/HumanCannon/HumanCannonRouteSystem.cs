using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>042 경로의 호스트 확정 상태. 공용 플레이어·부상·세션 코드는 변경하지 않는다.</summary>
    [DefaultExecutionOrder(-35)]
    public sealed partial class HumanCannonRouteSystem : MonoBehaviour
    {
        [Tooltip("0 출발 이동장치, 1 쉼터 승강기, 2 쉼터 재출발 이동장치.")]
        [SerializeField] HumanCannonTransport[] transports = Array.Empty<HumanCannonTransport>();
        [SerializeField] HumanCannonLandingZone shelter;
        [Tooltip("한 사람이 탑승 조건을 유지한 뒤 자동 출발하기까지 대기(초).")]
        [Min(0.1f), SerializeField] float boardingSeconds = 1.5f;
        [Tooltip("구간별 불꽃 2개. 쿠션/최종 구간의 불꽃은 없다.")]
        [SerializeField] HumanCannonFireWall[] fires = Array.Empty<HumanCannonFireWall>();
        [Tooltip("첫 쉼터 대포의 stations 인덱스. 시작 3대 다음은 쉼터 3대.")]
        [SerializeField] int shelterStationIndex = 3;
        [Tooltip("쉼터 구간 불꽃에 대응하는 대포 수. 뒤의 최종 대포는 불꽃을 작동시키지 않는다. 탑승 제한은 아니다.")]
        [Min(0), SerializeField] int shelterStationCount = 3;
        [Tooltip("코스 밖 추락 사망 높이(m). 지상 담당의 틈새 추락에도 공용 사망 처리를 사용한다.")]
        [SerializeField] float fallHeight = -10f;

        sealed class Transit
        {
            public DepartureTravelState State = new();
            public LiftTrapState Trap = new();
            public LiftBoardingState Boarding = new();
            public bool Occupied;
            public Vector3 Offset;
            public Quaternion Rotation = Quaternion.identity;
        }
        Transit[] runtime;
        uint[] deliveredArrival;
        NetworkSessionManager session;
        HumanCannonSystem cannon;
        HumanCannonPilot carriedPilot;
        int carriedTransit = -1;
        uint revision;
        readonly RouteResetBarrier resetBarrier = new();
        double nextSnapshot;
        public static HumanCannonRouteSystem Current { get; private set; }
        public int AliveFlyers { get; private set; }
        public int ArrivedFlyers { get; private set; }
        public bool ShelterReady => AliveFlyers == ArrivedFlyers;
        public int TransportCount => runtime?.Length ?? 0;
        public HumanCannonTransport Transport(int index) => index >= 0 && index < transports.Length ? transports[index] : null;
        public DepartureTravelState TransportState(int index) => runtime[index].State;
        public bool TrapOpen(int index) => runtime[index].Trap.IsOpen;
        public bool IsBoarding(int index) => runtime[index].Boarding.IsCounting;
        public float BoardingRemaining(int index) => runtime[index].Boarding.IsCounting
            ? Mathf.Max(0, boardingSeconds - (float)(Clock - runtime[index].Boarding.BoardedAt)) : 0;
        public bool BlocksTrampolineBounce(Vector3 contact)
        {
            if (runtime == null) return false;
            for (int i = 0; i < runtime.Length; i++)
                if (runtime[i].Trap.IsOpen && transports[i] != null && transports[i].ContainsFallColumn(contact)) return true;
            return false;
        }
        public double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public string FireStatus(int index) => index < 0 || index >= fires.Length || fires[index] == null ? "없음"
            : !fires[index].Available ? "사용 안 함"
            : fires[index].Finished ? "경계 종료" : !fires[index].Armed ? "대기"
            : fires[index].RemainingWarning(Clock) > 0 ? $"예고 {fires[index].RemainingWarning(Clock):F1}초" : "전진 중";

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            runtime = new Transit[transports.Length];
            deliveredArrival = new uint[transports.Length];
            for (int i = 0; i < runtime.Length; i++) runtime[i] = new Transit();
            NetworkPlayer.Restarted += Restart;
        }

        void Update()
        {
            if (cannon == null && HumanCannonSystem.Current != null)
            {
                cannon = HumanCannonSystem.Current;
                cannon.StateChanged += CannonChanged;
            }
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += Connection;
                Connection(session.ConnectionState);
            }
            if (session == null || !session.IsHost) return;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State == PlayerState.Normal && player.transform.position.y < fallHeight)
                    player.ServerKill();
            bool changed = false;
            int alive = AliveFlyers, arrived = ArrivedFlyers;
            CountShelterFlyers();
            changed |= alive != AliveFlyers || arrived != ArrivedFlyers;
            for (int i = 0; i < runtime.Length; i++)
            {
                var transit = runtime[i];
                if (transports[i] == null) continue;
                bool wasOpen = transit.Trap.IsOpen;
                int passengers = CountPassengers(i, out var soleRider);
                bool occupied = passengers == 1;
                changed |= transit.Occupied != occupied;
                transit.Occupied = occupied;
                if (transit.Trap.Tick(passengers, Clock, transit.State.Progress(Clock), transports[i].EmptyResetSeconds))
                {
                    changed = true;
                    if (wasOpen)
                    {
                        transit.State.Reset();
                        Debug.Log($"[HumanCannonRoute] transport {i} empty; returned to start");
                    }
                    else Debug.Log($"[HumanCannonRoute] transport {i} overloaded; floor opened");
                }
                if (!transit.Trap.IsOpen && transit.State.Tick(Clock))
                { changed = true; Debug.Log($"[HumanCannonRoute] transport {i} arrived"); }
                bool eligible = occupied && CanStart(soleRider, i);
                changed |= transit.Boarding.Tick(occupied ? soleRider.OwnerClientId : CannonSeatState.Nobody,
                    eligible, Clock, boardingSeconds);
                if (transit.Boarding.Ready(Clock, boardingSeconds))
                {
                    if (transit.State.TryStart(soleRider.OwnerClientId, transit.State.Revision, Clock, transports[i].TravelSeconds))
                    {
                        transit.Offset = soleRider.transform.position - transports[i].Platform.position;
                        transit.Rotation = soleRider.transform.rotation;
                        if (i == 0) ArmFire(0);
                        if (i == 2) ArmFire(1);
                        Debug.Log($"[HumanCannonRoute] transport {i} auto-started actor={soleRider.OwnerClientId} after={boardingSeconds:F1}s");
                    }
                    transit.Boarding.Reset();
                    changed = true;
                }
            }
            // 마지막 프레임도 경계 도달 전에 남은 플레이어 접촉을 판정한다.
            foreach (var fire in fires)
            {
                if (fire == null) continue;
                if (fire.KillOnContact)
                    foreach (var player in NetworkPlayer.All)
                        if (player != null && player.IsSpawned && player.State == PlayerState.Normal
                            && fire.Threatens(player.transform.position, 0.3f, Clock))
                        { player.ServerKill(); Debug.Log($"[HumanCannonRoute] fire contact actor={player.OwnerClientId}"); }
                changed |= fire.Tick(Clock);
            }
            if (changed) { revision++; Publish(); }
            else if (Clock >= nextSnapshot) { nextSnapshot = Clock + 0.25; Publish(); }
        }

        void LateUpdate()
        {
            for (int i = 0; i < runtime.Length; i++)
                if (transports[i] != null)
                {
                    var transit = runtime[i];
                    transports[i].Present(resetBarrier.Waiting ? 0 : transit.Trap.IsOpen ? transit.Trap.FrozenProgress : transit.State.Progress(Clock));
                    transports[i].PresentTrap(!resetBarrier.Waiting && transit.Trap.IsOpen, transit.Trap.OpenedAt, Clock);
                    bool departing = transit.Boarding.IsCounting || transit.State.Phase == DeparturePhase.Moving;
                    transports[i].PresentOccupied(!resetBarrier.Waiting && !transit.Trap.IsOpen && departing);
                }
            UpdatePassenger();
            foreach (var fire in fires) if (fire != null) fire.Present(Clock);
        }

        /// <summary>발사로 예고할 기존 구간 불꽃. 대포 탑승은 장치/다른 대포의 상태와 무관하다.</summary>
        public int FireSectionForStation(int station) => station < 0 ? -1 : station < shelterStationIndex ? 0
            : station < shelterStationIndex + shelterStationCount ? 1 : -1;

        internal bool ReadInput(NetworkPlayer player, in PlayerInputFrame input)
        {
            if (player == null) return false;
            if (resetBarrier.Waiting) return true;
            return IsRiding(player.OwnerClientId);
        }

        public bool IsRiding(ulong actor)
        {
            if (runtime == null) return false;
            foreach (var transit in runtime)
                if (!transit.Trap.IsOpen && transit.State.Phase == DeparturePhase.Moving && transit.State.Rider == actor) return true;
            return false;
        }

        public bool CanStart(NetworkPlayer player, int index)
        {
            if (runtime == null || index < 0 || index >= runtime.Length || transports[index] == null
                || !transports[index].IsConfigured || runtime[index].State.Phase != DeparturePhase.Waiting || runtime[index].Trap.IsOpen
                || player == null || !player.IsSpawned || player.State != PlayerState.Normal || player.IsAirborne
                || cannon == null || cannon.IsFlying(player.OwnerClientId) || cannon.IsSeated(player.OwnerClientId)
                || IsRiding(player.OwnerClientId)) return false;
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline == null || trampoline.Carrier != player.OwnerClientId) return false;
            if (!transports[index].ContainsFeet(player.transform.position, 0)
                || CountPassengers(index, out _) != 1) return false;
            if (index == 1 && !ShelterReady) return false;
            return true;
        }

        int CountPassengers(int index, out NetworkPlayer soleRider)
        {
            int count = 0;
            soleRider = null;
            var transit = runtime[index];
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                // 운송 중 소유자 위치 패킷이 한 프레임 늦어도 담당자를 누락하지 않는다.
                bool carried = !transit.Trap.IsOpen && transit.State.Phase == DeparturePhase.Moving
                    && transit.State.Rider == player.OwnerClientId;
                if (carried || transports[index].ContainsPassenger(player.transform.position)) { count++; soleRider = player; }
            }
            return count;
        }

        void CountShelterFlyers()
        {
            AliveFlyers = ArrivedFlyers = 0;
            ulong carrier = HumanCannonTrampoline.Current != null ? HumanCannonTrampoline.Current.Carrier : CannonSeatState.Nobody;
            // 트램폴린 담당자만 승강할 수 있으므로 다른 생존자의 쉼터 합류를 센다.
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal || player.OwnerClientId == carrier) continue;
                AliveFlyers++;
                // 쉼터 대포에 먼저 앉으면 발 위치가 바닥보다 높아진다. 대기 중인 동료도 합류 완료다.
                // 이를 제외하면 대포는 두 번째 운송을, 승강기는 대포의 하차를 서로 기다린다.
                if (IsSeatedAtShelter(player.OwnerClientId) || (shelter != null && shelter.SupportsLanding(player.transform.position)
                    && cannon != null && !cannon.IsFlying(player.OwnerClientId) && !cannon.IsSeated(player.OwnerClientId))) ArrivedFlyers++;
            }
        }

        bool IsSeatedAtShelter(ulong actor)
        {
            if (cannon == null) return false;
            int end = Math.Min(cannon.StationCount, shelterStationIndex + shelterStationCount);
            for (int i = Math.Max(0, shelterStationIndex); i < end; i++)
                if (cannon.Occupant(i) == actor) return true;
            return false;
        }

        void HandleRequest(ulong actor, RouteAction action, int index, uint expected)
        {
            if (session == null || !session.IsHost) return;
            if (action == RouteAction.Sync) SendState(actor);
            else if (action == RouteAction.ResetSync && index > 0) SendState(actor, index);
        }

        void CannonChanged(int index, CannonAction action, ulong actor)
        {
            if (session == null || !session.IsHost || action != CannonAction.Launch) return;
            if (ArmFire(FireSectionForStation(index))) { revision++; Publish(); }
        }

        bool ArmFire(int index) => index >= 0 && index < fires.Length && fires[index] != null && fires[index].Arm(Clock);

        void UpdatePassenger()
        {
            if (resetBarrier.Waiting) return;
            int localIndex = -1;
            var local = NetworkPlayer.Local;
            for (int i = 0; i < runtime.Length; i++)
            {
                var transit = runtime[i];
                if (transit.Trap.IsOpen) continue;
                if (transit.State.Phase == DeparturePhase.Arrived && deliveredArrival[i] != transit.State.Revision)
                {
                    deliveredArrival[i] = transit.State.Revision;
                    if (local != null && local.State == PlayerState.Normal && local.OwnerClientId == transit.State.Rider
                        && transports[i] != null)
                    {
                        var pilot = local.GetComponent<HumanCannonPilot>();
                        if (pilot != null)
                        {
                            pilot.BeginTransport();
                            pilot.SetTransportPose(transports[i].Position(1) + transit.Offset, transit.Rotation);
                            pilot.EndTransport();
                        }
                    }
                }
                if (transit.State.Phase != DeparturePhase.Moving || transports[i] == null) continue;
                var rider = FindPlayer(transit.State.Rider);
                if (rider == null || rider.State != PlayerState.Normal) continue;
                var position = transports[i].Platform.position + transit.Offset;
                if (rider == local)
                {
                    localIndex = i;
                    if (carriedPilot == null) carriedPilot = rider.GetComponent<HumanCannonPilot>();
                    if (carriedPilot != null) { carriedPilot.BeginTransport(); carriedPilot.SetTransportPose(position, transit.Rotation); }
                }
                else if (HumanCannonTrampoline.Current != null && HumanCannonTrampoline.Current.Carrier == rider.OwnerClientId)
                    // 원격 운반자의 회전은 소유자가 공유한 시선 방향이다. 리프트가 탑승 당시 방향으로 덮어쓰지 않는다.
                    rider.transform.position = position;
                else rider.transform.SetPositionAndRotation(position, transit.Rotation);
            }
            if (localIndex < 0 && carriedPilot != null)
            {
                // 마지막 Moving 프레임이 없더라도 정확한 도착점에서 운반을 마친다. R/사망은 현재 위치를 보존한다.
                if (carriedTransit >= 0 && !runtime[carriedTransit].Trap.IsOpen && runtime[carriedTransit].State.Phase == DeparturePhase.Arrived
                    && local != null && local.State == PlayerState.Normal)
                    carriedPilot.SetTransportPose(transports[carriedTransit].Position(1) + runtime[carriedTransit].Offset, runtime[carriedTransit].Rotation);
                carriedPilot.EndTransport();
                carriedPilot = null;
            }
            carriedTransit = localIndex;
        }

        void Restart(NetworkPlayer player)
        {
            // RequestRestartAll은 플레이어마다 알린다. 로컬 알림 한 번으로 처리해야 늦은 원격 알림이 재차 잠그지 않는다.
            if (player == null || !player.IsLocal) return;
            if (carriedPilot != null) { carriedPilot.EndTransport(); carriedPilot = null; }
            carriedTransit = -1;
            Array.Clear(deliveredArrival, 0, deliveredArrival.Length);
            if (session == null || !session.IsHost)
            {
                // 호스트는 재시작 알림을 보내기 전에 reset한다. 알림 이후 요청한 응답으로 순서 차이를 닫는다.
                // 응답 시점에는 다음 라운드가 이미 Moving/Arrived여도 정상이다.
                SendRequest(RouteAction.ResetSync, resetBarrier.Begin());
                return;
            }
            ResetRoute(); revision++; Publish();
        }

        void ResetRoute()
        {
            AliveFlyers = ArrivedFlyers = 0;
            foreach (var transit in runtime) { transit.State.Reset(); transit.Trap.Reset(); transit.Boarding.Reset(); transit.Occupied = false; }
            foreach (var fire in fires) if (fire != null) fire.ResetWall();
            for (int i = 0; i < runtime.Length; i++) if (transports[i] != null) transports[i].Present(0);
        }

        static NetworkPlayer FindPlayer(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id) return player;
            return null;
        }

        void OnDisable()
        {
            NetworkPlayer.Restarted -= Restart;
            if (cannon != null) cannon.StateChanged -= CannonChanged;
            if (session != null) session.ConnectionStateChanged -= Connection;
            if (carriedPilot != null) carriedPilot.EndTransport();
            Unregister();
            cannon = null;
            session = null;
            carriedPilot = null;
            carriedTransit = -1;
            if (Current == this) Current = null;
        }
    }
}
