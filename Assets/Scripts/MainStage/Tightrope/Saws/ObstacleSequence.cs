using System;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>단계가 시작되는 조건.</summary>
    public enum StepTrigger
    {
        /// <summary>게임 시작(제한시간 시작) 후.</summary>
        GameStart,
        /// <summary>묘기 시작(살아 있는 전원이 줄에 오름) 후.</summary>
        PerformanceStart,
        /// <summary>진행 중인 선두가 정한 거리(m)에 닿은 뒤.</summary>
        LeaderDistance,
        /// <summary>기준 단계가 시작된 뒤.</summary>
        AfterStepStarted,
        /// <summary>기준 단계가 끝난 뒤(가로 작동·정지는 시작과 동시에 끝, 수직 톱날은 제거될 때 끝).</summary>
        AfterStepFinished,
    }

    /// <summary>단계가 하는 일.</summary>
    public enum StepAction
    {
        /// <summary>가로 톱날 범위를 작동시킨다(멈춘 쪽 끝에서 출발).</summary>
        StartHorizontal,
        /// <summary>가로 톱날 범위를 멈춘다(가까운 끝, 맵 밖까지 간 뒤 멈춤).</summary>
        StopHorizontal,
        /// <summary>수직 톱날 1개를 내보낸다(이미 나와 있으면 제거될 때까지 기다림).</summary>
        SpawnVertical,
    }

    /// <summary>
    /// 장애물 순서의 한 단계(한 행). 위에서 아래로 읽지만 실행 순서는 시작 조건이 정한다.
    /// 나중에 표(구글 시트 등)로 옮기기 쉽게 평평한 값만 둔다.
    /// </summary>
    [Serializable]
    public sealed class ObstacleStep
    {
        [Tooltip("단계 이름. 다른 단계가 기준으로 부를 때 쓴다.")]
        public string name = "";

        [Tooltip("시작 조건.")]
        public StepTrigger trigger = StepTrigger.GameStart;

        [Tooltip("AfterStepStarted/Finished의 기준 단계 이름. 비우면 바로 위 단계.")]
        public string reference = "";

        [Tooltip("LeaderDistance의 거리(m).")]
        public float leaderDistance;

        [Tooltip("조건이 맞은 뒤 기다리는 시간(초).")]
        [Min(0f)] public float delay;

        [Tooltip("할 일.")]
        public StepAction action = StepAction.StartHorizontal;

        [Tooltip("가로 톱날 범위 시작 번호(1부터).")]
        [Min(1)] public int firstSaw = 1;

        [Tooltip("가로 톱날 범위 끝 번호(1부터, 포함).")]
        [Min(1)] public int lastSaw = 1;

        [Tooltip("수직 톱날 줄(0부터 왼쪽). -1이면 진행 중인 플레이어가 있는 줄 중 랜덤.")]
        public int lane = -1;

        [Tooltip("추가 반복 횟수. 0이면 한 번, -1이면 끝없이.")]
        public int repeat;

        [Tooltip("반복: 이 단계가 끝난 뒤 다시 실행까지 기다리는 시간(초).")]
        [Min(0f)] public float repeatDelay;

        public ObstacleStep() { }

        public ObstacleStep(string name, StepTrigger trigger, float delay, StepAction action)
        {
            this.name = name;
            this.trigger = trigger;
            this.delay = delay;
            this.action = action;
        }
    }

    /// <summary>
    /// 장애물 순서 실행기(호스트, MonoBehaviour와 분리). 시각은 모두 "게임 경과 시간"(모든 화면에서 같음)이다.
    /// 매 프레임 <see cref="Tick"/>으로 조건을 보고, 때가 된 단계를 실행 함수에 넘긴다. 실행 함수가 false를 내면(수직 톱날이 이미 있음 등) 다음 프레임에 다시 시도한다.
    /// </summary>
    public sealed class ObstacleSequence
    {
        sealed class State
        {
            public int fired;
            public bool running;              // 시작했지만 아직 안 끝남(수직 톱날)
            public float startedAt = -1f;
            public float finishedAt = -1f;
            public float leaderReachedAt = -1f;
        }

        readonly IReadOnlyList<ObstacleStep> steps;
        readonly State[] states;
        readonly int[] references;

        public ObstacleSequence(IReadOnlyList<ObstacleStep> steps)
        {
            this.steps = steps ?? Array.Empty<ObstacleStep>();
            states = new State[this.steps.Count];
            references = new int[this.steps.Count];
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = new State();
                references[i] = FindReference(i);
            }
        }

        /// <summary>단계 수.</summary>
        public int Count => steps.Count;

        /// <summary>단계 정의.</summary>
        public ObstacleStep this[int index] => steps[index];

        /// <summary>그 단계가 실행된 횟수.</summary>
        public int GetFiredCount(int index) => states[index].fired;

        /// <summary>
        /// 때가 된 단계를 실행한다. <paramref name="performanceStartedAt"/>은 묘기 시작 시각(시작 전이면 음수),
        /// <paramref name="leaderDistance"/>는 진행 중인 선두의 거리(없으면 NaN). <paramref name="execute"/>(단계 번호) → 실행했는지.
        /// </summary>
        public void Tick(float now, float performanceStartedAt, float leaderDistance, Func<int, bool> execute)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var state = states[i];
                if (step.trigger == StepTrigger.LeaderDistance && state.leaderReachedAt < 0f
                    && !float.IsNaN(leaderDistance) && leaderDistance >= step.leaderDistance)
                    state.leaderReachedAt = now;

                if (state.running || !HasRunsLeft(step, state)) continue;
                float due = GetDueTime(i, performanceStartedAt);
                if (float.IsNaN(due) || now < due) continue;
                if (!execute(i)) continue;

                state.fired++;
                state.startedAt = now;
                if (step.action == StepAction.SpawnVertical) state.running = true;
                else state.finishedAt = now;
            }
        }

        /// <summary>수직 톱날처럼 시간이 걸리는 단계가 끝났음을 알린다(제거됨).</summary>
        public void NotifyFinished(int index, float now)
        {
            if (index < 0 || index >= states.Length || !states[index].running) return;
            states[index].running = false;
            states[index].finishedAt = now;
        }

        /// <summary>처음 상태로(묘기 재시작).</summary>
        public void Reset()
        {
            for (int i = 0; i < states.Length; i++) states[i] = new State();
        }

        static bool HasRunsLeft(ObstacleStep step, State state) => step.repeat < 0 || state.fired <= step.repeat;

        /// <summary>실행할 시각. 아직 조건이 안 맞으면 NaN.</summary>
        float GetDueTime(int index, float performanceStartedAt)
        {
            var step = steps[index];
            var state = states[index];
            if (state.fired > 0)
                return state.finishedAt < 0f ? float.NaN : state.finishedAt + step.repeatDelay;

            float baseTime = step.trigger switch
            {
                StepTrigger.GameStart => 0f,
                StepTrigger.PerformanceStart => performanceStartedAt >= 0f ? performanceStartedAt : float.NaN,
                StepTrigger.LeaderDistance => state.leaderReachedAt >= 0f ? state.leaderReachedAt : float.NaN,
                StepTrigger.AfterStepStarted => references[index] < 0 || states[references[index]].startedAt < 0f ? float.NaN : states[references[index]].startedAt,
                StepTrigger.AfterStepFinished => references[index] < 0 || states[references[index]].finishedAt < 0f ? float.NaN : states[references[index]].finishedAt,
                _ => float.NaN,
            };
            return baseTime + step.delay;
        }

        /// <summary>기준 단계 번호. 이름이 비면 바로 위 단계, 못 찾으면 -1.</summary>
        int FindReference(int index)
        {
            var step = steps[index];
            if (step.trigger != StepTrigger.AfterStepStarted && step.trigger != StepTrigger.AfterStepFinished) return -1;
            if (string.IsNullOrEmpty(step.reference)) return index - 1;
            for (int i = 0; i < steps.Count; i++)
                if (i != index && steps[i].name == step.reference) return i;
            Debug.LogWarning($"[Saws] 장애물 단계 '{step.name}'의 기준 단계 '{step.reference}'를 찾지 못해 실행하지 않습니다.");
            return -1;
        }
    }

    /// <summary>
    /// 가로 톱날 하나의 작동·정지 기록과 위치 계산(모든 화면이 같은 기록으로 같은 위치를 낸다).
    /// 작동 전에는 출발 쪽 끝(맵 밖)에 멈춰 있다. 정지하면 가까운 끝까지 간 뒤 멈추고, 다시 작동하면 멈춘 쪽에서 출발한다.
    /// </summary>
    public sealed class HorizontalSawTrack
    {
        struct Run
        {
            public float start;
            public bool fromLeft;
            public float stop; // 정지 명령 시각. 계속 도는 중이면 +∞
        }

        readonly float speed, halfWidth;
        readonly bool initialLeft;
        readonly List<Run> runs = new();

        public HorizontalSawTrack(float speed, float halfWidth, bool startsLeft)
        {
            this.speed = speed;
            this.halfWidth = halfWidth;
            initialLeft = startsLeft;
        }

        /// <summary>그 시각에 작동한다. 도는 중이면 무시, 끝으로 가는 중이면 정지를 취소한다.</summary>
        public void Start(float time)
        {
            if (runs.Count > 0)
            {
                var last = runs[^1];
                if (float.IsPositiveInfinity(last.stop)) return;
                if (time < ParkTime(last))
                {
                    last.stop = float.PositiveInfinity;
                    runs[^1] = last;
                    return;
                }
                runs.Add(new Run { start = time, fromLeft = ParkedLeft(last), stop = float.PositiveInfinity });
                return;
            }
            runs.Add(new Run { start = time, fromLeft = initialLeft, stop = float.PositiveInfinity });
        }

        /// <summary>그 시각에 정지한다(가까운 끝까지 간 뒤 멈춤). 멈춰 있으면 무시.</summary>
        public void Stop(float time)
        {
            if (runs.Count == 0) return;
            var last = runs[^1];
            if (!float.IsPositiveInfinity(last.stop) || time < last.start) return;
            last.stop = time;
            runs[^1] = last;
        }

        /// <summary>기록을 지우고 처음(출발 쪽 끝 대기)으로.</summary>
        public void Clear() => runs.Clear();

        /// <summary>그 시각에 움직이고 있는지(끝으로 가는 중 포함).</summary>
        public bool IsMoving(float time)
        {
            if (runs.Count == 0) return false;
            var last = runs[^1];
            return time >= last.start && time < ParkTime(last);
        }

        /// <summary>그 시각의 중심 옆 좌표(m, 코스 가운데 0, 오른쪽 +).</summary>
        public float GetSide(float time)
        {
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                var run = runs[i];
                if (time < run.start) continue;
                float travelTime = Mathf.Min(time, ParkTime(run)) - run.start;
                return SawMath.PingPongSide(speed, halfWidth, run.fromLeft, travelTime);
            }
            return SawMath.PingPongSide(speed, halfWidth, initialLeft, 0f);
        }

        /// <summary>정지 명령 뒤 끝에 닿아 멈추는 시각. 계속 돌면 +∞.</summary>
        float ParkTime(Run run)
        {
            if (float.IsPositiveInfinity(run.stop)) return float.PositiveInfinity;
            float sweep = halfWidth * 2f;
            if (sweep <= 0f || speed <= 0f) return run.stop;
            float travel = speed * (run.stop - run.start);
            return run.start + Mathf.Ceil(travel / sweep - 1e-4f) * sweep / speed;
        }

        /// <summary>멈춘 끝이 왼쪽인지. 한 번 건널 때마다 반대쪽.</summary>
        bool ParkedLeft(Run run)
        {
            float sweep = halfWidth * 2f;
            int crossings = sweep <= 0f ? 0 : Mathf.RoundToInt(speed * (ParkTime(run) - run.start) / sweep);
            return (crossings % 2 == 0) == run.fromLeft;
        }
    }
}
