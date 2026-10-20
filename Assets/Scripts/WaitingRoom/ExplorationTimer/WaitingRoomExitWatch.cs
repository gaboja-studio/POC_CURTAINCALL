using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 퇴장 감지(탐색 제한 시간 B안의 시작 계기)와 개인별 재진입 차단. 런 알림·이벤트는 호스트·오프라인 전용이다.
    /// 쉬는 시간에 대기실 영역(이 오브젝트와 자식의 BoxCollider 합) 안에서 <b>보였던</b> 살아 있는 플레이어가 영역 밖(백스테이지)으로 나가면,
    /// 그 쉬는 시간에 처음 한 번 <see cref="CampaignRun.NotifyLeftWaitingRoom"/>을 부른다. A안(처음부터 흐름)이면 부르지 않는다.
    /// 위치로만 판정하므로 원격 캐릭터도 동기화된 위치로 센다(<see cref="StageEntranceGather"/>와 같은 방식).
    /// 나간 플레이어 목록은 쉬는 시간이 끝날 때까지 유지한다(재진입 불가 판정용).
    /// </summary>
    public sealed class WaitingRoomExitWatch : MonoBehaviour
    {
        readonly List<PlayerMover> offlinePlayers = new();
        readonly List<Transform> alive = new();
        readonly HashSet<Transform> seenInside = new();
        readonly HashSet<Transform> left = new();
        readonly Dictionary<Transform, Pose> outsidePoses = new();
        readonly List<Transform> destroyed = new();
        BoxCollider[] areas;
        CampaignRun trackedRun;
        CampaignSnapshot trackedStep;
        bool notified;

        /// <summary>이번 쉬는 시간에 대기실에서 나간 인원.</summary>
        public int LeftCount => left.Count;

        /// <summary>판정 화면에서 플레이어가 이번 쉬는 시간에 처음 대기실에서 나갔다.</summary>
        public event Action<Transform> PlayerLeft;

        /// <summary>판정 화면에서 첫 퇴장을 런 진행에 알렸다(탐색 제한 시간 시작).</summary>
        public event Action TimerStarted;

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
            CollectAlive();
            foreach (var player in alive)
            {
                if (Inside(player.position))
                {
                    if (left.Contains(player))
                        CorrectLocalOwner(player);
                    else
                        seenInside.Add(player);
                    continue;
                }
                // 시작 스냅샷·늦은 입장으로 처음부터 밖에 있는 사람은 퇴장으로 세지 않는다.
                if (!seenInside.Contains(player)) continue;
                outsidePoses[player] = new Pose(player.position, player.rotation);
                if (!left.Add(player) || !run.HasAuthority) continue;

                Debug.Log($"[WaitingRoom] {player.name} 대기실 → 백스테이지 ({left.Count}명째)");
                PlayerLeft?.Invoke(player);
                TryStartTimer(run);
            }
        }

        void TryStartTimer(CampaignRun run)
        {
            // 쉬는 시간마다 한 번만 알린다. A안은 이미 흐르므로 부르지 않는다.
            if (notified || run.State.Timer != RestTimerState.WaitingForExit) return;
            notified = true;
            if (!run.NotifyLeftWaitingRoom()) return;
            Debug.Log($"[WaitingRoom] 첫 퇴장 → 탐색 제한 시간 시작 {run.State.TimeRemaining:0}초");
            TimerStarted?.Invoke();
        }

        void CorrectLocalOwner(Transform player)
        {
            // 남의 복제본은 움직이지 않는다. 소유자의 Teleported 경로가 호스트·다른 화면에 보정을 공유한다.
            var networkPlayer = player.GetComponent<NetworkPlayer>();
            if (networkPlayer != null && (!networkPlayer.IsSpawned || !networkPlayer.IsOwner)) return;
            if (!outsidePoses.TryGetValue(player, out var pose) || Inside(pose.position)) return;
            if (player.TryGetComponent<PlayerMover>(out var mover) && mover.isActiveAndEnabled)
                mover.SetStartPose(pose.position, pose.rotation);
        }

        void RemoveDestroyed()
        {
            seenInside.RemoveWhere(player => player == null);
            left.RemoveWhere(player => player == null);
            destroyed.Clear();
            foreach (var player in outsidePoses.Keys)
                if (player == null) destroyed.Add(player);
            foreach (var player in destroyed) outsidePoses.Remove(player);
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
            notified = false;
            seenInside.Clear();
            left.Clear();
            outsidePoses.Clear();
            destroyed.Clear();
        }
    }
}
