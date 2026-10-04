using System;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>가로 톱날이 처음 출발하는 쪽.</summary>
    public enum SawStartSide
    {
        /// <summary>왼쪽 끝에서 오른쪽으로(원문 "좌측 톱날 좌→우").</summary>
        Left,
        /// <summary>오른쪽 끝에서 왼쪽으로(원문 "우측 톱날 우→좌").</summary>
        Right,
    }

    /// <summary>"○초"를 언제부터 셀지.</summary>
    public enum ObstacleTimeBase
    {
        /// <summary>게임 시작(제한시간이 흐르기 시작한 때)부터.</summary>
        GameStart,
        /// <summary>묘기 시작(진행 중인 플레이어가 처음 줄에 오른 때)부터.</summary>
        PerformanceStart,
    }

    /// <summary>가로 톱날 하나의 배치와 시간.</summary>
    [Serializable]
    public struct HorizontalSawEntry
    {
        [Tooltip("중심 위치(m, 외줄 시작점 기준). 씬 뷰에서 끌어 옮길 수 있다.")]
        public float distance;

        [Tooltip("처음 출발하는 쪽. 움직이기 전에는 이쪽 끝(맵 밖)에서 기다린다.")]
        public SawStartSide startSide;

        [Tooltip("움직이기 시작: 언제부터 셀지.")]
        public ObstacleTimeBase startBase;

        [Tooltip("움직이기 시작: 기준 시각에서 몇 초 뒤(초).")]
        [Min(0f)] public float startDelay;

        [Tooltip("멈추는지. 끄면 끝까지 계속 왕복한다.")]
        public bool stops;

        [Tooltip("멈춤: 움직이기 시작하고 몇 초 뒤(초). 가까운 끝(맵 밖)까지 간 뒤 멈춘다.")]
        [Min(0f)] public float stopAfter;

        public HorizontalSawEntry(float distance, SawStartSide startSide)
        {
            this.distance = distance;
            this.startSide = startSide;
            startBase = ObstacleTimeBase.GameStart;
            startDelay = 0f;
            stops = false;
            stopAfter = 0f;
        }
    }

    /// <summary>수직 톱날 등장 규칙.</summary>
    [Serializable]
    public sealed class VerticalSawRule
    {
        [Tooltip("수직 톱날을 내보내는지.")]
        public bool enabled = true;

        [Tooltip("첫 등장: 묘기 시작 후 몇 초 뒤(초). 기획 60.")]
        [Min(0f)] public float firstDelay = 60f;

        [Tooltip("다음 등장까지: 앞 톱날이 사라지고 몇 초 뒤(초). 기획 20.")]
        [Min(0f)] public float nextDelay = 20f;

        [Tooltip("최대 등장 횟수. 0이면 끝없이.")]
        [Min(0)] public int maxCount;

        [Tooltip("등장할 줄(0부터 왼쪽). -1이면 진행 중인 플레이어가 있는 줄 중 랜덤.")]
        public int lane = -1;

        [Tooltip("선두 앞 최소 거리(m). 그 줄의 가장 앞선 진행자보다 이만큼 앞에 생긴다(생성 한계 거리는 넘지 않음). 임시값 15(2026-10-05 PM).")]
        [Min(0f)] public float minLeadDistance = 15f;
    }

    /// <summary>
    /// 외줄 장애물 배치(레벨 디자인): 가로 톱날 목록·수직 톱날 등장 규칙. 크기·속도 같은 공통 규격은 <see cref="SawSettings"/>.
    /// 에셋: Assets/Resources/GameSettings/Tricks/TightropeObstacles.asset, 외줄 세팅이 연결한다(<see cref="CurtainCall.Settings.TightropeSettings.Obstacles"/>).
    /// 씬 뷰 편집기(Editor/)가 이 파일을 보여 주고 고친다. 시간은 쉬운 칸(○초에 움직임·○초 뒤 멈춤·다음 등장까지 ○초)만 둔다(2026-10-05 PM).
    /// 불타는 구간(014)도 이 파일에 묶음을 더한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Settings/Tricks/Tightrope Obstacles", fileName = "TightropeObstacles")]
    public sealed class ObstacleLayout : ScriptableObject
    {
        [Header("가로 톱날")]
        [Tooltip("가로 톱날 목록. 번호는 위에서부터 1번. 원문 5장 22개, 출발 쪽은 원문에 배정이 없어 번갈아(1번 왼쪽) 임시값. 기본: 게임 시작 0초에 움직이고 멈추지 않음.")]
        public HorizontalSawEntry[] horizontalSaws = CreateDefaultHorizontalSaws();

        [Header("수직 톱날")]
        public VerticalSawRule vertical = new();

        /// <summary>값을 바꿨을 때(인스펙터·씬 뷰 편집).</summary>
        public event Action Changed;

        void OnValidate() => Changed?.Invoke();

        /// <summary>편집기가 값을 바꾼 뒤 알린다.</summary>
        public void NotifyChanged() => Changed?.Invoke();

        /// <summary>가로 톱날 수.</summary>
        public int HorizontalCount => horizontalSaws?.Length ?? 0;

        /// <summary>가로 톱날 위치(m).</summary>
        public float GetHorizontalDistance(int index) => horizontalSaws[index].distance;

        /// <summary>가로 톱날 속도(m/s). 첫 톱날 거리 → 마지막 톱날 거리 사이를 직선으로 잇는다.</summary>
        public float GetHorizontalSpeed(int index, SawSettings saws)
        {
            int count = HorizontalCount;
            if (count <= 1) return saws.horizontalFirstSpeed;
            float first = horizontalSaws[0].distance, last = horizontalSaws[count - 1].distance;
            float t = Mathf.Approximately(first, last) ? 0f : Mathf.InverseLerp(first, last, horizontalSaws[index].distance);
            return Mathf.Lerp(saws.horizontalFirstSpeed, saws.horizontalLastSpeed, t);
        }

        /// <summary>가로 톱날이 왼쪽 끝(좌→우)에서 출발하는지.</summary>
        public bool StartsLeft(int index) => horizontalSaws[index].startSide == SawStartSide.Left;

        /// <summary>가로 톱날의 작동 기록·위치 계산을 새로 만든다(작동 전, 출발 쪽 끝에서 대기).</summary>
        public HorizontalSawTrack CreateTrack(int index, SawSettings saws) =>
            new(GetHorizontalSpeed(index, saws), saws.horizontalTravelHalfWidth, StartsLeft(index));

        /// <summary>
        /// 실행기(<see cref="ObstacleSequence"/>)가 쓰는 단계 목록으로 바꾼다.
        /// 가로 톱날마다 작동(+정지) 단계, 수직 톱날은 반복 단계 1개.
        /// </summary>
        public ObstacleStep[] BuildSteps()
        {
            var steps = new List<ObstacleStep>();
            for (int i = 0; i < HorizontalCount; i++)
            {
                var saw = horizontalSaws[i];
                var trigger = ToTrigger(saw.startBase);
                steps.Add(new ObstacleStep($"가로 #{i + 1} 작동", trigger, saw.startDelay, StepAction.StartHorizontal) { firstSaw = i + 1, lastSaw = i + 1 });
                if (saw.stops)
                    steps.Add(new ObstacleStep($"가로 #{i + 1} 정지", trigger, saw.startDelay + saw.stopAfter, StepAction.StopHorizontal) { firstSaw = i + 1, lastSaw = i + 1 });
            }
            if (vertical != null && vertical.enabled)
                steps.Add(new ObstacleStep("수직", StepTrigger.PerformanceStart, vertical.firstDelay, StepAction.SpawnVertical)
                {
                    lane = vertical.lane,
                    repeat = vertical.maxCount <= 0 ? -1 : vertical.maxCount - 1,
                    repeatDelay = vertical.nextDelay,
                });
            return steps.ToArray();
        }

        /// <summary>
        /// 미리보기: 그 시각 가로 톱날의 옆 좌표(m). 실행 때와 같은 작동·정지 기록(<see cref="HorizontalSawTrack"/>)으로 계산한다.
        /// <paramref name="time"/>은 게임 경과(초), <paramref name="performanceStartedAt"/>은 묘기 시작 시각(게임 경과 기준).
        /// </summary>
        public float GetHorizontalSideAt(int index, SawSettings saws, float time, float performanceStartedAt)
        {
            var saw = horizontalSaws[index];
            var track = CreateTrack(index, saws);
            float start = (saw.startBase == ObstacleTimeBase.PerformanceStart ? performanceStartedAt : 0f) + saw.startDelay;
            if (time >= start) track.Start(start);
            if (saw.stops && time >= start + saw.stopAfter) track.Stop(start + saw.stopAfter);
            return track.GetSide(time);
        }

        /// <summary>
        /// 수직 톱날이 생길 거리(m): 그 줄 선두 + 선두 앞 최소 거리. 생성 한계를 넘으면 NaN(그 줄은 지금 못 씀).
        /// 그 줄에 진행자가 없으면(<paramref name="laneLeader"/>가 NaN) 생성 한계 거리.
        /// </summary>
        public float GetVerticalSpawnDistance(float laneLeader, SawSettings saws)
        {
            if (float.IsNaN(laneLeader)) return saws.verticalSpawnDistance;
            float distance = laneLeader + vertical.minLeadDistance;
            return distance > saws.verticalSpawnDistance + 1e-4f ? float.NaN : distance;
        }

        /// <summary>수직 톱날이 생긴 뒤 그 줄 선두에게 닿기까지 걸리는 시간(초, 대략): 낙하 시간 + 거리 ÷ (톱날 속도 + 걷기 속도).</summary>
        public float EstimateVerticalContactTime(SawSettings saws, float walkSpeed) =>
            saws.verticalDropDuration + vertical.minLeadDistance / Mathf.Max(0.01f, saws.verticalSpeed + Mathf.Max(0f, walkSpeed));

        static StepTrigger ToTrigger(ObstacleTimeBase timeBase) =>
            timeBase == ObstacleTimeBase.PerformanceStart ? StepTrigger.PerformanceStart : StepTrigger.GameStart;

        /// <summary>원문 5장 위치 22개, 출발 쪽은 번갈아(1번 왼쪽), 게임 시작 0초에 움직이고 멈추지 않음.</summary>
        public static HorizontalSawEntry[] CreateDefaultHorizontalSaws()
        {
            float[] distances =
            {
                5.6f, 8.8f, 12.0f, 15.2f, 18.4f,
                22.5f, 27.5f, 32.5f, 37.5f, 42.5f, 47.5f, 52.5f, 57.5f, 62.5f, 67.5f,
                71.8f, 75.4f, 78.9f, 82.5f, 86.1f, 89.6f, 93.2f,
            };
            var saws = new HorizontalSawEntry[distances.Length];
            for (int i = 0; i < saws.Length; i++)
                saws[i] = new HorizontalSawEntry(distances[i], i % 2 == 0 ? SawStartSide.Left : SawStartSide.Right);
            return saws;
        }
    }
}
