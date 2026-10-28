using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 퇴장 감지와 대기실 안 캐릭터끼리 길막 해제. 퇴장 이벤트는 호스트·오프라인 전용이다(기록용).
    /// 쉬는 시간에 대기실 영역(이 오브젝트와 자식의 BoxCollider 합) 안에서 <b>보였던</b> 살아 있는 플레이어가 영역 밖으로 나가면 알린다.
    /// M2에서 쉬는 시간 타이머는 시작하자마자 흐르므로 퇴장이 타이머를 시작하지 않는다(옛 B안 삭제).
    /// 위치로만 판정하므로 원격 캐릭터도 동기화된 위치로 센다(<see cref="StageEntranceGather"/>와 같은 방식).
    /// 재입장은 허용하며 나간 플레이어 목록은 중복 퇴장 알림 방지를 위해 쉬는 시간이 끝날 때까지 유지한다.
    /// </summary>
    public sealed class WaitingRoomExitWatch : MonoBehaviour
    {
        readonly List<PlayerMover> offlinePlayers = new();
        readonly List<Transform> alive = new();
        readonly HashSet<Transform> seenInside = new();
        readonly HashSet<Transform> left = new();
        readonly Dictionary<PlayerMover, bool> blockingOverrides = new();
        readonly List<PlayerMover> restoreBlocking = new();
        BoxCollider[] areas;
        CampaignRun trackedRun;
        CampaignSnapshot trackedStep;

        /// <summary>이번 쉬는 시간에 대기실에서 나간 인원.</summary>
        public int LeftCount => left.Count;

        /// <summary>판정 화면에서 플레이어가 이번 쉬는 시간에 처음 대기실에서 나갔다.</summary>
        public event Action<Transform> PlayerLeft;

        /// <summary>이 화면에서 안→밖 이동을 관찰한 플레이어가 이번 쉬는 시간에 나갔는지.</summary>
        public bool HasLeft(Component player) => player != null && left.Contains(player.transform);

        void Awake() => areas = GetComponentsInChildren<BoxCollider>();

        void Start()
        {
            if (NetworkPlayer.All.Count == 0)
                offlinePlayers.AddRange(FindObjectsByType<PlayerMover>(FindObjectsSortMode.None));
        }

        void Update()
        {
            var run = CampaignRun.Instance;
            CollectAlive();
            RefreshPlayerBlocking(run == null || run.State.Phase == CampaignPhase.None
                || run.State.Phase == CampaignPhase.RestTime || run.State.Phase == CampaignPhase.Night);
            if (run == null || run.State.Phase != CampaignPhase.RestTime)
            {
                Clear();
                return;
            }

            var step = run.State;
            if (trackedRun != run || trackedStep.Generation != step.Generation || trackedStep.Day != step.Day
                || trackedStep.PerformanceIndex != step.PerformanceIndex || trackedStep.Rest != step.Rest)
            {
                Clear();
                trackedRun = run;
                trackedStep = step;
            }

            RemoveDestroyed();
            foreach (var player in alive)
            {
                if (Inside(player.position))
                {
                    seenInside.Add(player);
                    continue;
                }
                // 시작 스냅샷·늦은 입장으로 처음부터 밖에 있는 사람은 퇴장으로 세지 않는다.
                if (!seenInside.Contains(player)) continue;
                if (!left.Add(player) || !run.HasAuthority) continue;

                Debug.Log($"[WaitingRoom] {player.name} 대기실 → 백스테이지 ({left.Count}명째)");
                PlayerLeft?.Invoke(player);
            }
        }

        void RemoveDestroyed()
        {
            seenInside.RemoveWhere(player => player == null);
            left.RemoveWhere(player => player == null);
        }

        void RefreshPlayerBlocking(bool roomActive)
        {
            restoreBlocking.Clear();
            foreach (var entry in blockingOverrides)
                if (entry.Key == null || !roomActive || !alive.Contains(entry.Key.transform) || !Inside(entry.Key.transform.position))
                    restoreBlocking.Add(entry.Key);
            foreach (var mover in restoreBlocking)
            {
                if (mover != null) mover.IgnorePlayerBlocking = blockingOverrides[mover];
                blockingOverrides.Remove(mover);
            }
            if (!roomActive) return;
            foreach (var player in alive)
            {
                if (!Inside(player.position) || !player.TryGetComponent<PlayerMover>(out var mover)) continue;
                if (!blockingOverrides.ContainsKey(mover)) blockingOverrides.Add(mover, mover.IgnorePlayerBlocking);
                mover.IgnorePlayerBlocking = true;
            }
        }

        void OnDisable()
        {
            foreach (var entry in blockingOverrides)
                if (entry.Key != null) entry.Key.IgnorePlayerBlocking = entry.Value;
            blockingOverrides.Clear();
            Clear();
        }

        void CollectAlive()
        {
            alive.Clear();
            if (NetworkPlayer.All.Count > 0)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || player.Slot == NetworkPlayer.NoSlot
                        || player.IsApplyingInitialCondition || player.State == PlayerState.Fallen || player.State == PlayerState.Retired) continue;
                    alive.Add(player.transform);
                }
                return;
            }
            foreach (var mover in offlinePlayers)
                if (mover != null && mover.isActiveAndEnabled && mover.GetComponent<NetworkPlayer>() == null)
                    alive.Add(mover.transform);
        }

        // 발 위치는 바닥에 닿아 있으므로 조금 올려서 본다.
        bool Inside(Vector3 feet)
        {
            var point = feet + Vector3.up * 0.5f;
            foreach (var area in areas)
                if (area != null && area.enabled && area.bounds.Contains(point)) return true;
            return false;
        }

        void Clear()
        {
            trackedRun = null;
            trackedStep = default;
            seenInside.Clear();
            left.Clear();
        }
    }
}
