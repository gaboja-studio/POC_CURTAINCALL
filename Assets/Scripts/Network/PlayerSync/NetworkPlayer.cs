using System;
using System.Collections.Generic;
using CurtainCall.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// 플레이어 프리팹의 온라인 진입점. 접속하면 NetworkManager가 사람마다 하나씩 만든다.
    /// 호스트가 빈 자리 번호(0부터)를 정해 모두에게 공유하고, 각 화면은 그 번호의 출발 위치에 캐릭터를 세운다.
    /// 내 캐릭터가 아니면 입력·조작 규칙·균형 계산·이동 계산을 끈다(위치·자세는 소유자가 보낸 값을 따른다).
    /// 위치·방향은 같은 오브젝트의 NetworkTransform(소유자 권한)이 공유하고, 점프 상태는 이 컴포넌트가 공유한다.
    /// 다른 기능은 <see cref="Local"/>(내 캐릭터)·<see cref="All"/>·<see cref="Slot"/>을 쓴다.
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        /// <summary>자리 번호가 아직 정해지지 않음.</summary>
        public const int NoSlot = -1;

        static readonly List<NetworkPlayer> all = new();

        readonly NetworkVariable<int> slot = new(NoSlot);
        readonly NetworkVariable<JumpKind> jump = new(JumpKind.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        PlayerMover mover;
        NetworkTransform networkTransform;

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

        void Awake()
        {
            mover = GetComponent<PlayerMover>();
            networkTransform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            ApplyOwnership(IsOwner);
            slot.OnValueChanged += HandleSlotChanged;
            jump.OnValueChanged += HandleJumpChanged;
            if (IsOwner) mover.Teleported += SendTeleport;

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
            mover.Teleported -= SendTeleport;

            if (Local == this)
            {
                Local = null;
                LocalDespawned?.Invoke();
            }
        }

        void Update()
        {
            // 내 캐릭터: 점프 상태가 바뀌면 보낸다(변할 때만 전송된다)
            if (IsSpawned && IsOwner && jump.Value != mover.CurrentJump)
                jump.Value = mover.CurrentJump;
        }

        void HandleJumpChanged(JumpKind previous, JumpKind current) => JumpChanged?.Invoke(current);

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
