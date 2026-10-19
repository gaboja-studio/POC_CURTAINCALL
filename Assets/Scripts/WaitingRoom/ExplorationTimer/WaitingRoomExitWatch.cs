using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 퇴장 감지(탐색 제한 시간 B안의 시작 계기). 판정 화면(호스트·오프라인 런)만 동작한다.
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
        BoxCollider[] areas;
        bool notified;

        /// <summary>이번 쉬는 시간에 대기실에서 나간 인원.</summary>
        public int LeftCount => left.Count;

        /// <summary>판정 화면에서 플레이어가 이번 쉬는 시간에 처음 대기실에서 나갔다.</summary>
        public event Action<Transform> PlayerLeft;

        /// <summary>판정 화면에서 첫 퇴장을 런 진행에 알렸다(탐색 제한 시간 시작).</summary>
        public event Action TimerStarted;

        /// <summary>이 플레이어가 이번 쉬는 시간에 대기실에서 나갔는지(판정 화면 기준).</summary>
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
            if (run == null || !run.HasAuthority || run.State.Phase != CampaignPhase.RestTime)
            {
                Clear();
                return;
            }

            CollectAlive();
            foreach (var player in alive)
            {
                if (Inside(player.position))
                {
                    seenInside.Add(player);
                    continue;
                }
                if (!seenInside.Contains(player) || !left.Add(player)) continue;

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

        void CollectAlive()
        {
            alive.Clear();
            if (NetworkPlayer.All.Count > 0)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || player.State == PlayerState.Fallen || player.State == PlayerState.Retired) continue;
                    alive.Add(player.transform);
                }
                return;
            }
            foreach (var mover in offlinePlayers)
                if (mover != null && mover.isActiveAndEnabled) alive.Add(mover.transform);
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
            notified = false;
            seenInside.Clear();
            left.Clear();
        }
    }
}
