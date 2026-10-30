using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 쉬는 시간 탐색 타이머의 구역별 재생/멈춤(2026-10-30 확정 규칙). 호스트만 판정한다.
    /// 재생 구역: 배관·폐기물 보관소·화물 적재·무대 장치 보관실·계단·기계실·관리실(연결 통로 포함)·탈의 방·탈출로.
    /// 그 밖의 구역(로비·숙소/화장실·연습실·휴게·상점·전문가·작업실·무대 입구 통로·소품실·승강기 홀·서쪽 복도)과 어느 구역도 아닌 곳은 멈춤.
    /// 구역은 이 오브젝트 아래 활성 BoxCollider(Frame8Areas)를 이름으로 나눈다. 0이 되면 기존 규칙대로 공연으로 넘어간다.
    /// </summary>
    public sealed class ExplorationTimerZones : MonoBehaviour
    {
        [Tooltip("타이머가 흐르는 구역 콜라이더 이름. 나머지는 멈춤 구역.")]
        [SerializeField] string[] runningZones =
        {
            "Area_P05_PipeZone", "PipeLink", "F8_PipeCrawl", "Area_S05_JunkStorage", "Area_S12_CargoBay", "F8_Cargo",
            "Area_S11_StageGear", "Area_ST_Stairwell", "Area_B1_MachineRoom", "Area_B1_Office", "Area_B1_OfficeLink",
            "Area_B1_ChangingRoom", "Area_B1_EscapeCorridor", "Area_B1_EscapeEntrance",
        };
        [Tooltip("구역 콜라이더 묶음. 비우면 이 오브젝트 아래 전부.")]
        [SerializeField] Transform areas;

        readonly List<BoxCollider> running = new(), stopping = new();
        readonly List<PlayerMover> offlinePlayers = new();
        readonly List<Transform> alive = new();
        readonly List<(bool, bool)> states = new();

        /// <summary>마지막 판정에서 재생 구역에 있던 사람 수(호스트 화면 표시·검사용).</summary>
        public int ExploringCount { get; private set; }
        public int RunningZoneCount => running.Count;
        public int StoppingZoneCount => stopping.Count;

        void Awake()
        {
            var root = areas != null ? areas : transform;
            foreach (var box in root.GetComponentsInChildren<BoxCollider>(false))
                (System.Array.IndexOf(runningZones, box.name) >= 0 ? running : stopping).Add(box);
        }

        void Start()
        {
            if (NetworkPlayer.All.Count == 0) offlinePlayers.AddRange(FindObjectsByType<PlayerMover>(FindObjectsSortMode.None));
        }

        void Update()
        {
            var run = CampaignRun.Instance;
            if (run == null || !run.HasAuthority || run.State.Phase != CampaignPhase.RestTime) { ExploringCount = 0; return; }
            CollectAlive();
            states.Clear(); int exploring = 0;
            foreach (var player in alive)
            {
                var state = (Inside(running, player.position), Inside(stopping, player.position));
                states.Add(state);
                if (ExplorationTimerZoneRule.IsExploring(state.Item1, state.Item2)) exploring++;
            }
            ExploringCount = exploring;
            run.SetRestTimerPaused(ExplorationTimerZoneRule.Paused(states));
        }

        public bool IsRunningZone(Vector3 feet) => ExplorationTimerZoneRule.IsExploring(Inside(running, feet), Inside(stopping, feet));

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
                if (mover != null && mover.isActiveAndEnabled && mover.GetComponent<NetworkPlayer>() == null) alive.Add(mover.transform);
        }

        static bool Inside(List<BoxCollider> zones, Vector3 feet)
        {
            var point = feet + Vector3.up * .5f;
            foreach (var zone in zones) if (zone != null && zone.enabled && zone.bounds.Contains(point)) return true;
            return false;
        }
    }
}
