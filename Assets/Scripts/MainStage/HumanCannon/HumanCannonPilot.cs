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
        Vector3 returnPosition;
        Quaternion returnRotation;
        bool hasReturnPose;
        public bool IsFlying { get; private set; }
        public bool IsDriving => stationIndex >= 0 || IsFlying || groundRequest.Pending;

        internal void Initialize(HumanCannonSystem owner, NetworkPlayer local)
        {
            system = owner;
            player = local;
            mover = GetComponent<PlayerMover>();
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
            mover.ClearPendingInput();
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
            hasReturnPose = false;
        }

        void Update()
        {
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
            Vector3 correction = airControl.ConsumeVelocity(Time.frameCount, InputGate.IsBlocked, flightForward, airControlSpeed);
            Vector3 displacement = (velocity + correction) * dt + Vector3.down * (0.5f * gravity * dt * dt);
            if (HumanCannonPoleSystem.Current != null)
                displacement = HumanCannonPoleSystem.Current.ConstrainFlight(transform.position, displacement, mover.BodyRadius);
            velocity.y -= gravity * dt;
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

        internal void Detach()
        {
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
