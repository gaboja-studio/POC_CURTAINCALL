using System;
using CurtainCall.EscapeRoute;
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
        [Tooltip("도착한 발판이 빈 뒤 출발점으로 돌아가기까지 대기(초).")]
        [Min(0f), SerializeField] float returnDelaySeconds = 2f;
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
            public bool Rejected;
            public double EmptySince = -1;
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
        /// <summary>The rehearsal uses the same single departure device, without the later performance hazards.</summary>
        public void ConfigurePractice(HumanCannonTransport transport, float floorHeight)
        {
            if (isActiveAndEnabled) throw new InvalidOperationException("Configure an inactive course before enabling it.");
            transports = new[] { transport };
            shelter = null; fires = Array.Empty<HumanCannonFireWall>();
            shelterStationIndex = shelterStationCount = 0; fallHeight = floorHeight;
        }

        /// <summary>코스 아래 낭떠러지(<c>fallHeight</c> 아래)인지. 경로 시스템이 없으면 false.</summary>
        public static bool IsBelowCourse(Vector3 position) => Current != null && position.y < Current.fallHeight;

        // 호스트: 이번 추락을 이미 처리한 사람(다시 코스 위로 올라오면 지운다).
        readonly System.Collections.Generic.HashSet<ulong> fallen = new();
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
            // 운반자가 실려 가는 동안 그 트램폴린은 반동하지 않는다. 발판 밖으로 튀어나온 부분도 같다.
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline != null && trampoline.Carrier != CannonSeatState.Nobody && IsRiding(trampoline.Carrier)) return true;
            for (int i = 0; i < runtime.Length; i++)
            {
                if (transports[i] == null) continue;
                if (runtime[i].Trap.IsOpen && transports[i].ContainsFallColumn(contact)) return true;
                // 이동·복귀 중 발판 위 트램폴린에서 튀어 오르며 같이 올라가지 못하게 한다.
                if (Sealed(runtime[i]) && transports[i].ContainsAirColumn(contact)) return true;
            }
            return false;
        }
        public double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public string FireStatus(int index) => index < 0 || index >= fires.Length || fires[index] == null ? "없음"
            : !fires[index].Available ? "사용 안 함"
            : fires[index].Finished ? "경계 종료" : !fires[index].Armed ? "대기"
            : fires[index].RemainingWarning(Clock) > 0 ? $"예고 {fires[index].RemainingWarning(Clock):F1}초"
            : fires[index].CurrentStage(Clock) > 0 ? $"전진 중 {fires[index].SpeedAt(Clock):F2}m/s({fires[index].CurrentStage(Clock) + 1}구간)" : "전진 중";

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            runtime = new Transit[transports.Length];
            deliveredArrival = new uint[transports.Length];
            for (int i = 0; i < runtime.Length; i++) runtime[i] = new Transit();
            NetworkPlayer.Restarted += Restart;
        }

        /// <summary>
        /// 코스 아래 추락은 외줄타기와 같이 공용 사망으로 끝나므로 되살려 올리지 않는다(2026-11-03 담당자 결정).
        /// Dev "추락 무시"로 사망을 막은 경우에만 끝없이 떨어지지 않도록 마지막으로 탄 대포 앞(없으면 출발점)으로 옮긴다.
        /// </summary>
        void RelocateLocalFall()
        {
            var local = NetworkPlayer.Local;
            if (local == null || !local.IsSpawned || local.transform.position.y >= fallHeight
                || local.State != PlayerState.Normal || (cannon != null && cannon.IsFlying(local.OwnerClientId))
                || !EscapeDevelopmentSession.FallConsequencesSuppressed(local)) return;
            if (!local.TryGetComponent(out PlayerMover mover)) return;
            int station = cannon != null ? cannon.LocalLastStation : -1;
            if (cannon != null && station >= 0 && station < cannon.StationCount)
            {
                var point = cannon.Station(station).BoardingPoint;
                mover.Relocate(point.position, point.rotation);
            }
            else mover.ResetToStart();
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
            RelocateLocalFall();
            if (session == null || !session.IsHost) return;
            // 코스 밖 낭떠러지 추락은 외줄타기 낙사와 같이 공용 사망(2026-11-03). 같은 추락은 한 번만 처리한다.
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned) continue;
                if (player.transform.position.y >= fallHeight) { fallen.Remove(player.OwnerClientId); continue; }
                if (player.State == PlayerState.Normal && fallen.Add(player.OwnerClientId)) HumanCannonSystem.ServerFallDeath(player);
            }
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
                if (transports[i].OverloadTrap
                    && transit.Trap.Tick(passengers, Clock, transit.State.Progress(Clock), transports[i].EmptyResetSeconds))
                {
                    changed = true;
                    if (wasOpen)
                    {
                        // 이미 도착한 적 있는 발판은 도착 기록을 남겨 봇이 다시 타러 가지 않게 한다.
                        if (transit.State.Delivered) transit.State.Home(); else transit.State.Reset();
                        Debug.Log($"[HumanCannonRoute] transport {i} empty; returned to start");
                    }
                    else Debug.Log($"[HumanCannonRoute] transport {i} overloaded; floor opened");
                }
                if (!transit.Trap.IsOpen && transit.State.Tick(Clock))
                {
                    changed = true;
                    Debug.Log(transit.State.Phase == DeparturePhase.Arrived
                        ? $"[HumanCannonRoute] transport {i} arrived" : $"[HumanCannonRoute] transport {i} returned to start");
                }
                // 도착 뒤 발판이 비어 있는 시간이 이어지면 출발점으로 되돌아간다(2026-11-02 사용자 결정).
                if (transit.Trap.IsOpen || transit.State.Phase != DeparturePhase.Arrived || passengers > 0) transit.EmptySince = -1;
                else if (transit.EmptySince < 0) transit.EmptySince = Clock;
                else if (Clock - transit.EmptySince >= returnDelaySeconds && transit.State.TryReturn(Clock))
                {
                    transit.EmptySince = -1;
                    changed = true;
                    Debug.Log($"[HumanCannonRoute] transport {i} empty for {returnDelaySeconds:F1}s; returning");
                }
                bool eligible = occupied && CanStart(soleRider, i);
                // 함정 없는 장치는 과승·트램폴린 없음을 바닥 개방 대신 빨간 발판으로 알린다(2026-11-02 사용자 결정).
                bool rejected = !transports[i].OverloadTrap && transit.State.Phase == DeparturePhase.Waiting
                    && (passengers >= 2 || occupied && !Carries(soleRider));
                changed |= transit.Rejected != rejected;
                transit.Rejected = rejected;
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
                changed |= BoostFire(fire);
                if (fire.KillOnContact)
                    foreach (var player in NetworkPlayer.All)
                        if (player != null && player.IsSpawned && player.State == PlayerState.Normal
                            && player.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId
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
                    transports[i].PresentOccupied(!resetBarrier.Waiting && !transit.Trap.IsOpen && departing,
                        !resetBarrier.Waiting && transit.Rejected);
                    transports[i].PresentBarrier(!resetBarrier.Waiting && Sealed(transit));
                }
            EjectStowaways();
            UpdatePassenger();
            foreach (var fire in fires) if (fire != null) fire.Present(Clock);
            TickOwnerFireContacts();
        }

        /// <summary>
        /// 생존자 전원이 다음 구간 시작선을 넘어 내려서면(비행 중 제외) 불을 그 구간 속도로 올린다(2026-11-03 담당자 결정).
        /// 첫 쉼터는 승강기 도착 갑판·쉼터 대포 착석도 도착으로 본다.
        /// </summary>
        bool BoostFire(HumanCannonFireWall fire)
        {
            if (!fire.Available || !fire.Armed || fire.Finished) return false;
            bool changed = false;
            for (int stage = 0; stage < fire.StageCount; stage++)
            {
                if (fire.StageAt(stage) >= 0) continue;
                int alive = 0;
                bool all = true;
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                    alive++;
                    ulong actor = player.OwnerClientId;
                    Vector3 feet = player.transform.position;
                    bool arrived = cannon == null || !cannon.IsFlying(actor);
                    arrived &= fire.Reached(stage, feet) || stage == 0 && (IsSeatedAtShelter(actor)
                        || shelter != null && shelter.SupportsLanding(feet) && !IsRiding(actor));
                    if (!arrived) { all = false; break; }
                }
                if (alive > 0 && all && fire.Boost(stage, Clock))
                {
                    changed = true;
                    Debug.Log($"[HumanCannonRoute] fire {fire.name} stage {stage + 2} speed={fire.SpeedAt(Clock):F2}");
                }
            }
            return changed;
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
            if (!Carries(player)) return false;
            if (!transports[index].ContainsFeet(player.transform.position, 0)
                || CountPassengers(index, out _) != 1) return false;
            if (index == 1 && !ShelterReady) return false;
            return true;
        }

        /// <summary>출발한 뒤 도착 전, 또는 빈 채로 돌아가는 중. 이때는 운반자 외에 누구도 탈 수 없다.</summary>
        static bool Sealed(Transit transit) => !transit.Trap.IsOpen
            && transit.State.Phase is DeparturePhase.Moving or DeparturePhase.Returning;

        /// <summary>
        /// 옆 벽을 넘어(위에서 떨어지거나 뛰어들어) 이동 중 발판에 들어온 사람을 가장 가까운 벽 바깥으로 옮긴다.
        /// 이동은 소유자 권한이므로 각 클라이언트가 자기 플레이어(호스트는 봇 포함)만 옮긴다.
        /// </summary>
        void EjectStowaways()
        {
            if (resetBarrier.Waiting) return;
            for (int i = 0; i < runtime.Length; i++)
            {
                var transit = runtime[i];
                if (transports[i] == null || !transports[i].IsConfigured || !Sealed(transit)) continue;
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || !player.IsOwner || player.State != PlayerState.Normal
                        || transit.State.Phase == DeparturePhase.Moving && transit.State.Rider == player.OwnerClientId
                        || IsRiding(player.OwnerClientId)
                        || cannon != null && (cannon.IsFlying(player.OwnerClientId) || cannon.IsSeated(player.OwnerClientId))
                        || !player.TryGetComponent(out PlayerMover mover) || !mover.Simulated) continue;
                    float radius = mover.Body != null ? mover.Body.radius : 0.5f;
                    Vector3 feet = player.transform.position;
                    if (transports[i].ContainsPassenger(feet))
                        mover.Relocate(transports[i].OutsideBarrier(feet, radius), player.transform.rotation);
                    // 발판 밖으로 튀어나온 운반 트램폴린 위에 서서 버티면 타던 쪽 갑판으로 되돌린다.
                    else if (transit.State.Phase == DeparturePhase.Moving && OnCarriedTrampoline(transit.State.Rider, feet, radius))
                        mover.Relocate(transports[i].BoardingSide(radius), player.transform.rotation);
                    else continue;
                    Debug.Log($"[HumanCannonRoute] transport {i} sealed; moved actor={player.OwnerClientId} off");
                }
            }
        }

        static bool Carries(NetworkPlayer player) => player != null && HumanCannonTrampoline.Current != null
            && HumanCannonTrampoline.Current.Carrier == player.OwnerClientId;

        int CountPassengers(int index, out NetworkPlayer soleRider)
        {
            int count = 0;
            soleRider = null;
            var transit = runtime[index];
            // 이동·복귀 중에는 탑승할 수 없다. 밀려나기 전 늦은 위치가 과승 함정·복귀 대기를 건드리지 않게 운반자만 센다.
            bool sealedOff = Sealed(transit);
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                // 운송 중 소유자 위치 패킷이 한 프레임 늦어도 담당자를 누락하지 않는다.
                bool carried = !transit.Trap.IsOpen && transit.State.Phase == DeparturePhase.Moving
                    && transit.State.Rider == player.OwnerClientId;
                if (carried || !sealedOff && (transports[index].ContainsPassenger(player.transform.position)
                    || BouncingAbove(player, index))) { count++; soleRider = player; }
            }
            return count;
        }

        /// <summary>
        /// 발판 위나 운반 중인 트램폴린 위에서 반동(좌석 없는 비행) 중인 사람. 출발 시 함께 가지 못하게 탑승자로 센다.
        /// 트램폴린이 발판 밖으로 튀어나와 있어도 그 위의 반동자를 놓치지 않는다.
        /// </summary>
        bool BouncingAbove(NetworkPlayer player, int index)
        {
            if (cannon == null || !cannon.TryGetFlight(player.OwnerClientId, out var flight) || flight.StationIndex >= 0) return false;
            Vector3 position = player.transform.position;
            if (transports[index].ContainsAirColumn(position)) return true;
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline == null || trampoline.Carrier == CannonSeatState.Nobody) return false;
            var carrier = FindPlayer(trampoline.Carrier);
            if (carrier == null || !transports[index].ContainsPassenger(carrier.transform.position)
                || position.y < trampoline.Surface.y - .5f) return false;
            float reach = TrampolineRadius(trampoline) + TrampolineAirReach;
            return Vector3.ProjectOnPlane(position - trampoline.Surface, Vector3.up).sqrMagnitude <= reach * reach;
        }

        /// <summary>실려 가는 운반자가 든 트램폴린 위(면 아래 0.5m~위 2m)에 서 있는지.</summary>
        static bool OnCarriedTrampoline(ulong rider, Vector3 feet, float radius)
        {
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline == null || trampoline.Carrier == CannonSeatState.Nobody || trampoline.Carrier != rider) return false;
            float height = feet.y - trampoline.Surface.y, reach = TrampolineRadius(trampoline) + radius;
            return height >= -.5f && height <= 2f
                && Vector3.ProjectOnPlane(feet - trampoline.Surface, Vector3.up).sqrMagnitude <= reach * reach;
        }

        static float TrampolineRadius(HumanCannonTrampoline trampoline)
        {
            Vector2 size = trampoline.LoadHalfSize;
            if (!trampoline.CircularSurface) return trampoline.transform.TransformVector(size.x, 0, size.y).magnitude;
            return Mathf.Max(trampoline.transform.TransformVector(size.x, 0, 0).magnitude,
                trampoline.transform.TransformVector(0, 0, size.y).magnitude);
        }

        /// <summary>반동 중 공중 조종으로 트램폴린 가장자리 밖까지 흘러갈 수 있는 여유(m).</summary>
        const float TrampolineAirReach = 2f;

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
            ownerFireContacts?.Reset();
            AliveFlyers = ArrivedFlyers = 0;
            foreach (var transit in runtime) { transit.State.Reset(); transit.Trap.Reset(); transit.Boarding.Reset(); transit.Occupied = transit.Rejected = false; transit.EmptySince = -1; }
            foreach (var fire in fires) if (fire != null) fire.ResetWall();
            for (int i = 0; i < runtime.Length; i++) if (transports[i] != null) transports[i].Present(0);
        }

        public bool ResetPracticeTransport()
        {
            if (cannon == null || !cannon.PracticeMode || session == null || !session.IsHost) return false;
            if (carriedPilot != null) { carriedPilot.EndTransport(); carriedPilot = null; }
            carriedTransit = -1;
            ResetRoute(); revision++; Publish();
            return true;
        }

        static NetworkPlayer FindPlayer(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id) return player;
            return null;
        }

        void OnDisable()
        {
            ownerFireContacts?.Dispose();
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
