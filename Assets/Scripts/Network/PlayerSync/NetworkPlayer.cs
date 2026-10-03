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

        readonly NetworkVariable<PlayerState> state = new(PlayerState.Normal);
        readonly NetworkVariable<int> restartCount = new(0);

        PlayerMover mover;
        PlayerBalance balance;
        PlayerRagdoll ragdoll;
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

        /// <summary>호스트: 모든 플레이어를 보통 상태로 출발점에서 다시 시작시킨다(균형 초기화·래그돌 복구·조작 잠금 해제). 언제 부를지는 묘기 진행(005)이 정한다.</summary>
        public static void ServerRestartAll()
        {
            foreach (var player in all)
            {
                if (!player.IsServer) return;
                player.state.Value = PlayerState.Normal;
                player.restartCount.Value++;
            }
        }

        /// <summary>디버그·테스트용: 호스트에 모두 재시작을 요청한다(테스트 HUD의 R).</summary>
        public void RequestRestartAll()
        {
            if (IsSpawned && IsOwner) RequestRestartAllRpc();
        }

        [Rpc(SendTo.Server)]
        void RequestStateRpc(PlayerState next)
        {
            if (state.Value == next) return;
            if (ServerCanChangeState != null && !ServerCanChangeState(this, next)) return;
            state.Value = next;
        }

        [Rpc(SendTo.Server)]
        void RequestRestartAllRpc() => ServerRestartAll();

        void Awake()
        {
            mover = GetComponent<PlayerMover>();
            balance = GetComponent<PlayerBalance>();
            ragdoll = GetComponent<PlayerRagdoll>();
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
            if (IsOwner) mover.SetControlEnabled(false);
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
