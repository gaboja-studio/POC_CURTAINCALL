using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using Unity.Netcode.Components;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>소유자 화면의 탑승 고정/발사 이동. 위치 공유는 기존 NetworkTransform을 그대로 쓴다.</summary>
    [DefaultExecutionOrder(-25)]
    public sealed partial class HumanCannonPilot : MonoBehaviour
    {
        readonly CannonInputLatch latch = new();
        readonly CannonAirControl airControl = new();
        HumanCannonSystem system;
        NetworkPlayer player;
        PlayerMover mover;
        PlayerCondition condition;
        PlayerController controller;
        HumanCannonControlScheme scheme;
        PlayerControlScheme previousScheme;
        bool previousAssigned;
        bool previousSimulation;
        int stationIndex = -1;
        int flightStationIndex = -1;
        uint launchRevision;
        uint flightSegment;
        bool awaitingBounce;
        bool awaitingLanding;
        bool rejectedSurface;
        Vector3 velocity;
        Vector3 flightForward;
        float airControlSpeed;
        bool steerPoleMomentum;
        Vector3 returnPosition;
        Quaternion returnRotation;
        bool hasReturnPose;
        public bool IsFlying { get; private set; }
        internal Vector3 FlightVelocity => velocity;
        public bool IsDriving => stationIndex >= 0 || IsFlying || groundRequest.Pending || onTransport;

        internal void Initialize(HumanCannonSystem owner, NetworkPlayer local)
        {
            system = owner;
            player = local;
            mover = GetComponent<PlayerMover>();
            condition = GetComponent<PlayerCondition>();
            controller = GetComponent<PlayerController>();
            previousAssigned = controller.HasAssignedScheme;
            previousScheme = controller.Scheme;
            scheme = ScriptableObject.CreateInstance<HumanCannonControlScheme>();
            scheme.hideFlags = HideFlags.DontSave;
            scheme.Pilot = this;
            controller.SetScheme(scheme);
        }

        internal void ReadInput(in PlayerInputFrame input)
        {
            airControl.Clear();
            if (system == null || player.State != PlayerState.Normal || InputGate.IsBlocked) return;
            if (groundRequest.Pending || awaitingLanding) return;
            if (ReadPoleInput(input)) return;
            if (HumanCannonRouteSystem.Current != null && HumanCannonRouteSystem.Current.ReadInput(player, input)) return;
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline != null && trampoline.Carrier == player.OwnerClientId)
            {
                if (latch.Read(input.InteractTapped, input.InteractLongPressed, input.InteractHoldProgress) == CannonAction.Cancel)
                    trampoline.RequestDrop();
                return;
            }
            if (input.InteractTapped && trampoline != null && trampoline.CanGrab(player))
            {
                trampoline.RequestGrab();
                return;
            }
            if (IsFlying)
                airControl.SetInput(new Vector2(input.Posture, input.Move), Time.frameCount);
            else if (stationIndex >= 0)
            {
                var action = latch.Read(input.InteractTapped, input.InteractLongPressed, input.InteractHoldProgress);
                if (action != CannonAction.Sync) system.Request(stationIndex, action);
            }
            else if (!IsFlying && input.InteractTapped)
            {
                int nearest = system.FindBoardableStation(player);
                if (nearest >= 0) system.Request(nearest, CannonAction.Board);
            }
        }

        internal void SeatAt(int index)
        {
            if (stationIndex == index) return;
            airControl.Clear();
            // 소유자 화면에서 실제로 탑승하기 직전의 위치를 보관한다.
            // 호스트가 보간 중인 원격 위치는 네트워크 지연만큼 과거일 수 있다.
            returnPosition = transform.position;
            returnRotation = transform.rotation;
            hasReturnPose = true;
            TakeMovement();
            stationIndex = index;
            IsFlying = false;
            latch.Reset();
            var seat = system.Station(index).Seat;
            Teleport(seat.position, seat.rotation);
        }

        internal void Launch(int index, uint revision, Vector3 position, Quaternion rotation, Vector3 launchVelocity)
        {
            airControl.Clear();
            steerPoleMomentum = false;
            TakeMovement();
            stationIndex = -1;
            flightStationIndex = index;
            launchRevision = revision;
            flightSegment = 0;
            awaitingBounce = awaitingLanding = rejectedSurface = false;
            IsFlying = true;
            hasReturnPose = false;
            velocity = launchVelocity;
            flightForward = mover.CourseForward;
            var station = index >= 0 ? system.Station(index) : null;
            airControlSpeed = station != null ? station.AirControlSpeed : 0f;
            Teleport(position, rotation);
        }

        internal void Cancel(Vector3 position, Quaternion rotation)
        {
            Teleport(hasReturnPose ? returnPosition : position, hasReturnPose ? returnRotation : rotation);
            ReleaseMovement();
        }

        internal void StartCarrying()
        {
            ReleaseMovement();
            latch.Reset();
        }

        internal void Rebound(int index, uint revision, uint segment, Vector3 contact, float upwardSpeed)
        {
            if (!MatchesFlight(index, revision, segment)) return;
            flightSegment++;
            awaitingBounce = rejectedSurface = false;
            // 반동은 자동 전진 관성만 없앤다. WASD 보정은 누르는 동안 계속 적용한다.
            velocity = Vector3.up * upwardSpeed;
            steerPoleMomentum = false;
            airControl.Clear();
            Teleport(contact + Vector3.up * 0.05f, transform.rotation);
        }

        internal void RejectRebound(int index, uint revision, uint segment)
        {
            if (!MatchesFlight(index, revision, segment)) return;
            awaitingBounce = false;
            rejectedSurface = true;
        }

        internal void CompleteLanding(int index, uint revision, uint segment)
        {
            if (awaitingLanding && MatchesFlight(index, revision, segment)) ReleaseMovement();
        }

        internal void ResetAfterRestart()
        {
            ReleaseMovement();
            // 늦은 발사/취소 응답이 먼저 위치를 옮겼어도 R의 출발 위치로 복구한다.
            if (mover != null && player != null && player.State == PlayerState.Normal) mover.ResetToStart();
            InputGate.RequireRelease();
        }

        bool MatchesFlight(int index, uint revision, uint segment) =>
            IsFlying && flightStationIndex == index && launchRevision == revision && flightSegment == segment;

        void TakeMovement()
        {
            if (!IsDriving) previousSimulation = mover.Simulated;
            mover.ClearPendingInput(); // 달리기·앉기 요청도 함께 비운다
            mover.TryStand(); // 대포가 이동을 맡는 동안 자세는 바뀌지 않으므로 공간이 있으면 일어선 채로 맡긴다(막혀 있으면 앉은 채 유지)
            mover.Simulated = false;
        }

        internal void ReleaseMovement()
        {
            airControl.Clear();
            if (mover != null && IsDriving)
            {
                mover.ClearPendingInput();
                mover.Simulated = previousSimulation;
            }
            stationIndex = -1;
            HeldPole = -1;
            onTransport = false;
            groundRequest.Cancel();
            sampledGroundJump = false;
            flightStationIndex = -1;
            launchRevision = 0;
            flightSegment = 0;
            awaitingBounce = awaitingLanding = rejectedSurface = false;
            IsFlying = false;
            velocity = Vector3.zero;
            flightForward = Vector3.zero;
            airControlSpeed = 0f;
            steerPoleMomentum = false;
            hasReturnPose = false;
        }

        void Update()
        {
            UpdateCannonView();
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal)
            {
                ReleaseMovement();
                return;
            }
            SampleGroundJump();
        }

        void SimulateFlight()
        {
            if (IsHoldingPole) return;
            if (awaitingBounce && HumanCannonTrampoline.Current == null)
            {
                awaitingBounce = false;
                rejectedSurface = true;
            }
            if (!IsFlying || awaitingBounce || awaitingLanding || mover == null || !mover.Body.enabled) return;
            float dt = Time.deltaTime;
            float gravity = GameSettings.Base.Character.Gravity;
            // 입력이 막혀도 탄도는 계속된다. 보정은 이번 프레임의 수평 변위에만 더한다.
            float controlMultiplier = system.Injuries.AirMultiplier(condition != null ? condition.LostLegCount : 0);
            Vector3 inputForward = cannonView != null && cannonView.UsesMouseLook ? cannonView.ViewForward : flightForward;
            Vector3 correction = airControl.ConsumeVelocity(Time.frameCount, InputGate.IsBlocked, inputForward,
                airControlSpeed * controlMultiplier);
            if (steerPoleMomentum) velocity = CannonAirControl.SteerPoleMomentum(velocity, correction);
            Vector3 wind = HumanCannonHazardCourse.Current != null
                ? HumanCannonHazardCourse.Current.WindVelocity(transform.position, mover.BodyHeight, true) : Vector3.zero;
            Vector3 displacement = BallisticMotion.Step(ref velocity, correction + wind, gravity, dt);
            if (HumanCannonPoleSystem.Current != null)
                displacement = HumanCannonPoleSystem.Current.ConstrainFlight(transform.position, displacement, mover.BodyRadius);
            var trampoline = HumanCannonTrampoline.Current;
            if (!rejectedSurface && velocity.y <= 0f && trampoline != null &&
                trampoline.TryContact(transform.position, transform.position + displacement, mover.BodyRadius, out var contact))
            {
                // 호스트 반동 응답 전에는 바닥 사망을 요청하지 않는다. R/사망은 이 대기도 해제한다.
                awaitingBounce = true;
                Teleport(contact + Vector3.up * 0.05f, transform.rotation);
                trampoline.RequestBounce(flightStationIndex, launchRevision, flightSegment, contact);
                return;
            }
            var collision = mover.Body.Move(displacement);
            if ((collision & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            // 운반자의 정상 몸 점프는 여기로 들어오지 않는다. 코스 밖으로 떨어진 비행도 종료한다.
            if (((collision & CollisionFlags.Below) != 0 && velocity.y <= 0f) || transform.position.y < -10f)
            {
                // 호스트가 쿠션/일반 바닥을 확정할 때까지 착지 위치와 비행 식별자를 유지한다.
                awaitingLanding = true;
                system.RequestLanding(flightStationIndex, launchRevision, flightSegment, transform.position);
            }
        }

        void LateUpdate()
        {
            // 운반자 Update → 장치 LateUpdate(-30) → 비행 sweep(-25) 순서다.
            SimulateFlight();
            ApplyGroundWind();
            UpdatePolePose();
            DetectGroundBounce();
            if (stationIndex < 0 || system == null) return;
            var station = system.Station(stationIndex);
            if (station == null || !station.isActiveAndEnabled) return;
            var seat = station.Seat;
            transform.SetPositionAndRotation(seat.position, seat.rotation);
        }

        void Teleport(Vector3 position, Quaternion rotation)
        {
            var body = mover.Body;
            bool wasEnabled = body.enabled;
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = wasEnabled;
            // CharacterController를 다시 켜면 플레이어끼리의 충돌 무시도 다시 연결해야 한다.
            if (wasEnabled)
                foreach (var other in PlayerMover.All)
                    if (other != mover && other.Body != null) Physics.IgnoreCollision(body, other.Body, true);
            var sync = GetComponent<NetworkTransform>();
            if (sync != null && sync.IsSpawned && sync.CanCommitToTransform)
                sync.Teleport(position, rotation, transform.localScale);
        }

        void ApplyGroundWind()
        {
            if (IsDriving || player == null || !player.IsSpawned || player.State != PlayerState.Normal
                || mover == null || !mover.Simulated || !mover.Body.enabled || HumanCannonHazardCourse.Current == null) return;
            Vector3 wind = HumanCannonHazardCourse.Current.WindVelocity(transform.position, mover.BodyHeight, mover.IsAirborne);
            if (wind.sqrMagnitude <= 0) return;
            // 접지 중 추가 Move가 접지 플래그를 지우지 않도록 작은 아래쪽 접촉만 유지한다.
            mover.Body.Move(wind * Time.deltaTime + (mover.Body.isGrounded ? Vector3.down * 0.001f : Vector3.zero));
        }

        internal void Detach()
        {
            DestroyCannonView();
            ReleaseMovement();
            if (scheme != null && controller != null && controller.Scheme == scheme)
                controller.SetScheme(previousAssigned ? previousScheme : null);
            if (scheme != null) Destroy(scheme);
            scheme = null;
            system = null;
        }

        void OnDestroy() => Detach();
    }
}
