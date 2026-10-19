using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ⑥ 무대 입구 통로 집결 판정(테스트 대체 영역, 백스테이지 연결 CC-111 때 실제 ⑥으로 교체).
    /// 판정 화면(호스트·오프라인 런)이 쉬는 시간에 살아 있는 전원이 이 상자 안에 있으면 <see cref="CampaignRun.NotifyAllAtStageEntrance"/>를 한 번 부른다.
    /// 그 뒤 공연 전환은 런 진행·장소 전환이 모두에게 함께 한다. 일부만 들어오면 아무 일도 없다.
    /// 위치로만 판정하므로 원격 캐릭터도 동기화된 위치로 센다. 접속하지 않은 맵 확인 씬에서는 씬에 놓인 플레이어를 센다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StageEntranceGather : MonoBehaviour
    {
        [Tooltip("화면 왼쪽 위에 집결 인원을 표시한다(공동 테스트 확인용).")]
        [SerializeField] bool showStatus = true;

        readonly List<PlayerMover> offlinePlayers = new();
        BoxCollider area;
        bool notified;

        /// <summary>영역 안에 있는 살아 있는 인원.</summary>
        public int InsideCount { get; private set; }

        /// <summary>살아 있는 전체 인원(사망·리타이어 제외).</summary>
        public int AliveCount { get; private set; }

        /// <summary>판정 화면에서 전원 집결을 런 진행에 알렸다.</summary>
        public event Action Gathered;

        void Awake() => area = GetComponent<BoxCollider>();

        void Start()
        {
            if (NetworkPlayer.All.Count == 0)
                offlinePlayers.AddRange(FindObjectsByType<PlayerMover>(FindObjectsSortMode.None));
        }

        void Update()
        {
            Count();

            var run = CampaignRun.Instance;
            if (run == null || !run.HasAuthority || run.State.Phase != CampaignPhase.RestTime)
            {
                notified = false;
                return;
            }
            // 쉬는 시간마다 한 번만 알린다(마지막 아침처럼 거절돼도 매 프레임 다시 부르지 않는다).
            if (notified || AliveCount == 0 || InsideCount < AliveCount) return;

            notified = true;
            if (!run.NotifyAllAtStageEntrance()) return;
            Debug.Log($"[WaitingRoom] ⑥ 전원 집결 {InsideCount}/{AliveCount} → 공연 전환");
            Gathered?.Invoke();
        }

        void Count()
        {
            int inside = 0, alive = 0;
            if (NetworkPlayer.All.Count > 0)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (player.State == PlayerState.Fallen || player.State == PlayerState.Retired) continue;
                    alive++;
                    if (Contains(player.transform.position)) inside++;
                }
            }
            else
            {
                foreach (var mover in offlinePlayers)
                {
                    if (mover == null || !mover.isActiveAndEnabled) continue;
                    alive++;
                    if (Contains(mover.transform.position)) inside++;
                }
            }
            InsideCount = inside;
            AliveCount = alive;
        }

        // 발 위치는 바닥에 닿아 있으므로 조금 올려서 본다.
        bool Contains(Vector3 feet) => area.bounds.Contains(feet + Vector3.up * 0.5f);

        void OnGUI()
        {
            if (!showStatus) return;
            GUI.Label(new Rect(10, 10, 320, 24), $"⑥ 무대 입구 집결 {InsideCount}/{AliveCount}");
        }
    }
}
