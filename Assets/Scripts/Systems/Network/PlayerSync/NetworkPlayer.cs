using System;
using System.Collections.Generic;
using CurtainCall.Player;
using CurtainCall.Settings;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// 플레이어 프리팹의 온라인 진입점. 접속하면 NetworkManager가 사람마다 하나씩 만든다.
    /// 호스트가 빈 자리 번호(0부터)를 정해 모두에게 공유하고, 각 화면은 그 번호의 출발 위치에 캐릭터를 세운다.
    /// 내 캐릭터가 아니면 입력·조작 규칙·균형 계산·이동 계산을 끈다(위치·자세는 소유자가 보낸 값을 따른다).
    /// 위치·방향은 같은 오브젝트의 NetworkTransform(소유자 권한)이 공유하고, 점프 상태·균형 표시값(몸 기울기)은 이 컴포넌트가 공유한다.
    /// 판정은 호스트가 한다: 소유자가 <see cref="RequestState"/>로 요청 → 호스트가 확인(<see cref="ServerCanChangeState"/>) → <see cref="State"/>로 모두에게 공유.
    /// 첫 사용처는 추락(균형 무너짐·줄 밖 착지 → 추락 → 조작 잠금·래그돌). 모두 재시작은 호스트가 <see cref="ServerRestartAll"/>로 한다.
    /// 신체 손상: 장애물은 호스트에서 <see cref="ServerCutPart"/>·<see cref="ServerCutLegThenArm"/>로 부위를 자르고, 잃은 부위는 모두에게 공유되어
    /// 각 화면의 <see cref="PlayerCondition"/>에 들어간다. 네 팔다리를 모두 잃으면 호스트가 추락(사망)으로 확정한다.
    /// 다른 기능은 <see cref="Local"/>(내 캐릭터)·<see cref="All"/>·<see cref="Slot"/>·<see cref="State"/>를 쓴다.
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        /// <summary>자리 번호가 아직 정해지지 않음.</summary>
        public const int NoSlot = -1;

        static readonly List<NetworkPlayer> all = new();

        readonly NetworkVariable<int> slot = new(NoSlot);
        readonly NetworkVariable<JumpKind> jump = new(JumpKind.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        readonly NetworkVariable<bool> balanceActive = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> balanceValue = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // 잃은 부위는 상태보다 먼저 둔다: 같은 순간 바뀌면 받는 화면에서 마지막 팔다리가 먼저 떨어지고 그다음 몸이 래그돌이 된다
        readonly NetworkVariable<BodyPart> lostParts = new(BodyPart.None);
        readonly NetworkVariable<PlayerState> state = new(PlayerState.Normal);
        readonly NetworkVariable<int> restartCount = new(0);

        PlayerMover mover;
        PlayerBalance balance;
        PlayerRagdoll ragdoll;
        PlayerCondition condition;
        NetworkTransform networkTransform;

        /// <summary>
        /// 호스트: 상태 변경 요청을 받아들일지 정하는 규칙(플레이어, 바꿀 상태 → 허용). 비어 있으면 모두 허용.
        /// 이후 기능이 "진행 중일 때만 추락" 같은 조건을 여기에 붙인다.
        /// </summary>
        public static Func<NetworkPlayer, PlayerState, bool> ServerCanChangeState { get; set; }

        /// <summary>모두 재시작했을 때(모든 화면, 플레이어마다 한 번).</summary>
        public static event Action<NetworkPlayer> Restarted;

        /// <summary>이 컴퓨터의 내 캐릭터. 접속 전·생성 전에는 null.</summary>
        public static NetworkPlayer Local { get; private set; }

        /// <summary>내 캐릭터가 생성됐을 때.</summary>
        public static event Action<NetworkPlayer> LocalSpawned;

        /// <summary>내 캐릭터가 사라졌을 때(나가기·접속 끊김).</summary>
        public static event Action LocalDespawned;

        /// <summary>지금 생성되어 있는 모든 플레이어.</summary>
        public static IReadOnlyList<NetworkPlayer> All => all;

        /// <summary>자리 번호(0~인원-1). 출발 위치 순서. 정해지기 전에는 <see cref="NoSlot"/>.</summary>
        public int Slot => slot.Value;

        /// <summary>이 캐릭터가 이 컴퓨터 사람의 것인지.</summary>
        public bool IsLocal => IsOwner;

        /// <summary>자리 번호가 정해지거나 바뀌었을 때.</summary>
        public event Action<int> SlotChanged;

        /// <summary>지금 공중에 있는 이유(모든 화면에서 같음). 땅이면 None.</summary>
        public JumpKind CurrentJump => IsOwner || !IsSpawned ? mover.CurrentJump : jump.Value;

        /// <summary>공중에 있는지(모든 화면에서 같음).</summary>
        public bool IsAirborne => CurrentJump != JumpKind.None;

        /// <summary>점프 상태가 바뀌었을 때(모든 화면). 인자는 새 상태.</summary>
        public event Action<JumpKind> JumpChanged;

        /// <summary>호스트가 확정한 상태(모든 화면에서 같음).</summary>
        public PlayerState State => state.Value;

        /// <summary>상태가 바뀌었을 때(모든 화면). 인자는 (이전, 새 상태).</summary>
        public event Action<PlayerState, PlayerState> StateChanged;

        /// <summary>내 캐릭터: 상태 변경을 호스트에 요청한다. 호스트가 허용하면 <see cref="State"/>가 바뀐다.</summary>
        public void RequestState(PlayerState next)
        {
            if (!IsSpawned || !IsOwner) return;
            RequestStateRpc(next);
        }

        /// <summary>호스트: 요청 없이 상태를 바로 정한다.</summary>
        public void ServerSetState(PlayerState next)
        {
            if (!IsServer) return;
            state.Value = next;
        }

        /// <summary>
        /// 호스트: 모든 플레이어를 보통 상태로 출발점에서 다시 시작시킨다(균형 초기화·래그돌 복구·조작 잠금 해제). 언제 부를지는 묘기 진행(005)이 정한다.
        /// 잃은 부위는 게임 기본 세팅(신체 손상 · 재시작 복구)에 따라 사망자만 또는 모두 되돌린다.
        /// </summary>
        public static void ServerRestartAll()
        {
            var recovery = GameSettings.Base.BodyDamage.RestartRecovery;
            foreach (var player in all)
            {
                if (!player.IsServer) return;
                // Normal로 바꾸기 전에 사망 여부를 본다
                if (recovery == BaseGameSettings.BodyRestartRecovery.RestoreAll || player.state.Value == PlayerState.Fallen)
                    player.lostParts.Value = BodyPart.None;
                player.state.Value = PlayerState.Normal;
                player.restartCount.Value++;
            }
        }

        /// <summary>디버그·테스트용: 호스트에 모두 재시작을 요청한다(테스트 HUD의 R).</summary>
        public void RequestRestartAll()
        {
            if (IsSpawned && IsOwner) RequestRestartAllRpc();
        }

        /// <summary>잃은 부위(모든 화면에서 같음).</summary>
        public BodyPart LostParts => lostParts.Value;

        /// <summary>
        /// 호스트: 일반 절단. 요청 부위를 자르고, 이미 잃었으면 같은 종류의 남은 쪽을 자른다. 실제로 잘린 부위를 돌려준다(거절·변화 없음은 None).
        /// 진행 중인 사람만 다친다(도착 완료·사망 후·묘기 종료 후는 거절, 사망 허용 규칙 <see cref="ServerCanChangeState"/>를 따른다).
        /// </summary>
        public BodyPart ServerCutPart(BodyPart requested) => ServerApplyCut(PlayerCondition.ResolveCut(lostParts.Value, requested));

        /// <summary>호스트: 가로 톱날 순서 절단(다리 → 남은 다리 → 팔). 좌우가 둘 다 남았을 때의 선택은 부르는 쪽이 정한다(기획 미정).</summary>
        public BodyPart ServerCutLegThenArm(bool leftFirst) => ServerApplyCut(PlayerCondition.NextLegThenArmCut(lostParts.Value, leftFirst));

        /// <summary>디버그·테스트용: 내 캐릭터의 부위 절단을 호스트에 요청한다. None이면 가로 톱날 순서(좌우 랜덤).</summary>
        public void RequestCutPart(BodyPart part)
        {
            if (IsSpawned && IsOwner) RequestCutPartRpc(part);
        }

        BodyPart ServerApplyCut(BodyPart part)
        {
            if (!IsServer || part == BodyPart.None || !ServerCanTakeDamage()) return BodyPart.None;
            lostParts.Value |= part;
            if (PlayerCondition.IsAllLimbsLost(lostParts.Value))
                state.Value = PlayerState.Fallen; // 네 팔다리 모두 잃으면 사망(2026-10-04 PM)
            return part;
        }

        bool ServerCanTakeDamage() =>
            state.Value == PlayerState.Normal && (ServerCanChangeState == null || ServerCanChangeState(this, PlayerState.Fallen));

        [Rpc(SendTo.Server)]
        void RequestCutPartRpc(BodyPart part)
        {
            if (part == BodyPart.None) ServerCutLegThenArm(UnityEngine.Random.value < 0.5f);
            else ServerCutPart(part);
        }

        [Rpc(SendTo.Server)]
        void RequestStateRpc(PlayerState next)
        {
            if (state.Value == next) return;
            if (ServerCanChangeState != null && !ServerCanChangeState(this, next)) return;
            state.Value = next;
        }

        [Rpc(SendTo.Server)]
        void RequestRestartAllRpc()
        {
            var run = CurtainCall.Tightrope.TightropeRun.Current;
            if (run == null) ServerRestartAll();
            else if (OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId) run.RestartByHost();
        }

        void Awake()
        {
            mover = GetComponent<PlayerMover>();
            balance = GetComponent<PlayerBalance>();
            ragdoll = GetComponent<PlayerRagdoll>();
            condition = GetComponent<PlayerCondition>();
            networkTransform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            ApplyOwnership(IsOwner);
            slot.OnValueChanged += HandleSlotChanged;
            jump.OnValueChanged += HandleJumpChanged;
            state.OnValueChanged += HandleStateChanged;
            restartCount.OnValueChanged += HandleRestart;
            lostParts.OnValueChanged += HandleLostPartsChanged;
            if (condition != null) condition.SetLostParts(lostParts.Value); // 늦게 생성된 화면도 현재 몸 상태를 맞춘다
            if (IsOwner)
            {
                mover.Teleported += SendTeleport;
                if (balance != null) balance.Fell += RequestFall;
            }
            else ShowRemoteBalance();
            if (State != PlayerState.Normal) ApplyState(State); // 늦게 생성된 화면도 현재 상태를 맞춘다

            if (IsServer)
                slot.Value = FindFreeSlot();
            else if (Slot != NoSlot)
                PlaceAtStart();

            if (IsOwner)
            {
                Local = this;
                LocalSpawned?.Invoke(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            slot.OnValueChanged -= HandleSlotChanged;
            jump.OnValueChanged -= HandleJumpChanged;
            state.OnValueChanged -= HandleStateChanged;
            restartCount.OnValueChanged -= HandleRestart;
            lostParts.OnValueChanged -= HandleLostPartsChanged;
            mover.Teleported -= SendTeleport;
            if (balance != null) balance.Fell -= RequestFall;

            if (Local == this)
            {
                Local = null;
                LocalDespawned?.Invoke();
            }
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (!IsOwner)
            {
                ShowRemoteBalance();
                return;
            }

            // 내 캐릭터: 바뀐 값만 보낸다(NetworkVariable은 변할 때만 전송된다)
            if (jump.Value != mover.CurrentJump)
                jump.Value = mover.CurrentJump;

            if (balance != null)
            {
                if (balanceActive.Value != balance.IsActive)
                    balanceActive.Value = balance.IsActive;
                if (Mathf.Abs(balanceValue.Value - balance.Value) >= GameSettings.Base.Sync.BalanceSendThreshold
                    || (balance.Value == 0f && balanceValue.Value != 0f))
                    balanceValue.Value = balance.Value;
            }
        }

        /// <summary>남의 캐릭터: 소유자가 보낸 균형 값을 표시용으로 넣는다(몸 기울기가 따른다).</summary>
        void ShowRemoteBalance()
        {
            if (balance != null)
                balance.SetDisplayedState(balanceActive.Value, balanceValue.Value);
        }

        void HandleJumpChanged(JumpKind previous, JumpKind current) => JumpChanged?.Invoke(current);

        void HandleLostPartsChanged(BodyPart previous, BodyPart current)
        {
            if (condition != null) condition.SetLostParts(current);
        }

        /// <summary>내 캐릭터의 균형이 무너지면(줄 밖 착지 포함) 호스트에 추락을 요청한다. 내 화면의 래그돌은 바로 시작된다.</summary>
        void RequestFall() => RequestState(PlayerState.Fallen);

        void HandleStateChanged(PlayerState previous, PlayerState current)
        {
            ApplyState(current);
            StateChanged?.Invoke(previous, current);
        }

        /// <summary>확정된 상태를 이 화면에 반영한다.</summary>
        void ApplyState(PlayerState current)
        {
            if (current != PlayerState.Fallen) return;
            if (IsOwner)
            {
                mover.SetControlEnabled(false);
                // 균형 추락은 이미 래그돌이 됐다. 신체 손상 사망처럼 호스트가 정한 추락은 여기서 무너뜨린다(이미 추락했으면 무시됨)
                if (balance != null) balance.ForceFall();
                else if (ragdoll != null) ragdoll.GoLimp();
            }
            else if (ragdoll != null) ragdoll.GoLimp(); // 남의 캐릭터: 래그돌 연출은 각자 화면에서(물리 결과는 맞추지 않음)
        }

        /// <summary>모두 재시작: 내 캐릭터는 균형 초기화·출발점 순간이동·조작 해제, 남의 캐릭터는 래그돌만 되돌린다(위치는 소유자가 보냄).</summary>
        void HandleRestart(int previous, int current)
        {
            if (IsOwner)
            {
                if (balance != null) balance.ResetBalance(); // 래그돌 복구도 함께(BalanceReset)
                mover.ResetToStart();
                mover.SetControlEnabled(true);
            }
            else if (ragdoll != null)
            {
                ragdoll.Restore();
            }
            Restarted?.Invoke(this);
        }

        /// <summary>내 캐릭터가 순간이동하면 다른 화면에서도 보간 없이 옮긴다.</summary>
        void SendTeleport()
        {
            if (networkTransform != null && networkTransform.IsSpawned && networkTransform.CanCommitToTransform)
                networkTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        /// <summary>내 캐릭터만 키 입력을 받고 이동·균형을 계산한다.</summary>
        void ApplyOwnership(bool owner)
        {
            if (TryGetComponent(out PlayerInputReader input)) input.enabled = owner;
            if (TryGetComponent(out PlayerController controller)) controller.enabled = owner;
            if (TryGetComponent(out PlayerBalance balance)) balance.enabled = owner;
            mover.Simulated = owner;
        }

        void HandleSlotChanged(int previous, int current)
        {
            if (current != NoSlot)
                PlaceAtStart();
            SlotChanged?.Invoke(current);
        }

        /// <summary>자리 번호의 출발 위치를 이 캐릭터의 출발 위치로 정하고 그 자리로 옮긴다.</summary>
        void PlaceAtStart()
        {
            PlayerSpawnPoints.GetPose(Slot, mover.LaneSpacing, out Vector3 position, out Quaternion rotation);
            mover.SetStartPose(position, rotation);
        }

        /// <summary>호스트: 다른 플레이어가 쓰지 않는 가장 작은 번호. 나간 사람의 번호는 다음 사람이 쓴다.</summary>
        int FindFreeSlot()
        {
            int candidate = 0;
            while (all.Exists(p => p != this && p.Slot == candidate))
                candidate++;
            return candidate;
        }
    }
}
