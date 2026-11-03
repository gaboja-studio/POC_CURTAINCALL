using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>서버 시각으로 구간 안에서만 움직이는 불꽃 압박. 실제 사망 판정은 호스트 RouteSystem만 실행한다.</summary>
    public sealed class HumanCannonFireWall : MonoBehaviour
    {
        [SerializeField] Transform start;
        [SerializeField] Transform boundary;
        [SerializeField] Transform visual;
        [Tooltip("선택 불꽃 파티클 벽. 있으면 빨간 상자 대신 보인다. 바닥(전선)에 두고 X 배율로 판정 폭을 맞춘다.")]
        [SerializeField] Transform effect;
        [Tooltip("예고 중 파티클 방출 비율. 전진 중은 1.")]
        [Range(0f, 1f), SerializeField] float warningEmission = 0.35f;
        ParticleSystem[] effectSystems;
        float[] effectRates;
        [Tooltip("시험용 불꽃 예고 시간(초). 마지막 대포 출발 여부와 무관하다.")]
        [Min(0.1f), SerializeField] float warningSeconds = 30f;
        [Tooltip("시험용 전진 속도(m/s). 최종 밸런스가 아니다.")]
        [Min(0.01f), SerializeField] float speed = 0.7f;
        [Tooltip("공중/지상 압박 범위의 좌우 반폭(m).")]
        [Min(1f), SerializeField] float halfWidth = 18f;
        [Tooltip("바닥 위 불꽃 판정 높이(m).")]
        [Min(1f), SerializeField] float height = 24f;
        [Tooltip("기획의 접촉 사망 설계안을 시험한다. 속도·판정은 실제 플레이에서 조정한다.")]
        [SerializeField] bool killOnContact = true;
        [Tooltip("현재 코스에서 불꽃벽 사용. 끄면 예고·전진·접촉 피해·표시를 모두 보류한다.")]
        [SerializeField] bool available = true;
        [Tooltip("생존자 전원이 해당 구간 시작선(stageLines)을 넘어 내려서면 그때부터 속도에 곱하는 배율. 순서대로 커진다.")]
        [SerializeField] float[] stageSpeedMultipliers = System.Array.Empty<float>();
        [Tooltip("구간 시작선. 불의 진행 방향으로 이 지점을 넘어 선 사람을 해당 구간 도착으로 본다. 배율과 같은 수.")]
        [SerializeField] Transform[] stageLines = System.Array.Empty<Transform>();
        double[] stageAt;
        public bool Available => available;
        public int StageCount => Mathf.Min(stageSpeedMultipliers.Length, stageLines.Length);
        /// <summary>속도 단계가 오른 호스트 시각. 아직이면 -1.</summary>
        public double StageAt(int stage) => stage >= 0 && stage < StageCount && stageAt != null && stage < stageAt.Length ? stageAt[stage] : -1;
        /// <summary>지금 적용 중인 속도 단계(0 = 기본, k = k번째 배율).</summary>
        public int CurrentStage(double now)
        {
            int stage = 0;
            for (int i = 0; i < StageCount; i++) if (StageAt(i) >= 0 && StageAt(i) <= now) stage = i + 1;
            return stage;
        }
        public float SpeedAt(double now) { int stage = CurrentStage(now); return speed * (stage > 0 ? stageSpeedMultipliers[stage - 1] : 1f); }
        public bool KillOnContact => killOnContact;
        public double ArmedAt { get; private set; } = -1;
        public bool Finished { get; private set; }
        public bool Armed => ArmedAt >= 0;
        public bool IsConfigured => start != null && boundary != null && visual != null && speed > 0;
        public double EndTime
        {
            get
            {
                double from = AdvanceAt;
                if (!IsConfigured) return from;
                double left = Vector3.Distance(start.position, boundary.position), rate = speed;
                // 시작선을 지난 단계 시각마다 남은 거리를 그때 속도로 나눈다.
                for (int i = 0; i < StageCount; i++)
                {
                    double at = StageAt(i);
                    if (at < 0) break;
                    if (at <= from) { rate = speed * stageSpeedMultipliers[i]; continue; }
                    if (left <= (at - from) * rate) break;
                    left -= (at - from) * rate; from = at; rate = speed * stageSpeedMultipliers[i];
                }
                return from + left / rate;
            }
        }
        /// <summary>예고가 끝난 뒤 지금까지 전진한 거리. 단계마다 그때 속도로 이어 계산한다.</summary>
        public double Travelled(double now)
        {
            if (!double.IsFinite(now) || !Armed) return 0;
            double from = AdvanceAt, distance = 0, rate = speed;
            if (now <= from) return 0;
            for (int i = 0; i < StageCount; i++)
            {
                double at = StageAt(i);
                if (at < 0 || at >= now) break;
                if (at > from) { distance += (at - from) * rate; from = at; }
                rate = speed * stageSpeedMultipliers[i];
            }
            return distance + (now - from) * rate;
        }
        /// <summary>진행 방향으로 이 위치가 구간 시작선을 넘었는지.</summary>
        public bool Reached(int stage, Vector3 feet)
        {
            if (!IsConfigured || stage < 0 || stage >= StageCount || stageLines[stage] == null) return false;
            Vector3 forward = (boundary.position - start.position).normalized;
            return Vector3.Dot(feet - start.position, forward) >= Vector3.Dot(stageLines[stage].position - start.position, forward);
        }
        /// <summary>
        /// 전진 시작 시각. 예고 중 생존자 전원이 다음 구간에 도착하면 남은 예고를 건너뛰고 그 순간 출발한다(2026-11-03 담당자 결정).
        /// 단계 시각은 이미 동기화되므로 별도 값 없이 같은 결과를 낸다.
        /// </summary>
        public double AdvanceAt => StageAt(0) >= 0 ? System.Math.Min(ArmedAt + warningSeconds, StageAt(0)) : ArmedAt + warningSeconds;
        public double RemainingWarning(double now) => Armed && !Finished ? System.Math.Max(0, AdvanceAt - now) : 0;
        public bool Advancing(double now) => available && double.IsFinite(now) && now >= 0 && Armed && !Finished && now >= AdvanceAt;
        public Vector3 Front(double now)
        {
            if (!IsConfigured) return transform.position;
            return Vector3.MoveTowards(start.position, boundary.position, (float)Travelled(now));
        }

        /// <summary>
        /// 생존자 전원이 다음 구간에 도착한 순간부터 더 빨라진다. 앞 단계를 건너뛰었으면 같은 시각으로 함께 올린다.
        /// 단계는 내려가지 않으며 재시작 때만 지운다.
        /// </summary>
        public bool Boost(int stage, double now)
        {
            if (!available || !Armed || Finished || stage < 0 || stage >= StageCount || !double.IsFinite(now) || now < ArmedAt
                || StageAt(stage) >= 0) return false;
            EnsureStages();
            for (int i = 0; i <= stage; i++) if (stageAt[i] < 0) stageAt[i] = now;
            return true;
        }

        void EnsureStages()
        {
            if (stageAt != null && stageAt.Length == StageCount) return;
            stageAt = new double[StageCount];
            System.Array.Fill(stageAt, -1d);
        }

        public bool Arm(double now)
        {
            if (!available || Armed || !IsConfigured || !double.IsFinite(now) || now < 0) return false;
            ArmedAt = now;
            return true;
        }

        public bool Tick(double now)
        {
            if (!available || !double.IsFinite(now) || now < 0 || !Armed || Finished || now < EndTime) return false;
            Finished = true;
            return true;
        }

        public void ResetWall() { ArmedAt = -1; Finished = false; EnsureStages(); System.Array.Fill(stageAt, -1d); Present(0); }
        public void Receive(double armedAt, bool finished) { ArmedAt = armedAt; Finished = finished; }
        public void Receive(double armedAt, bool finished, double[] stages)
        {
            Receive(armedAt, finished);
            EnsureStages();
            for (int i = 0; i < stageAt.Length; i++) stageAt[i] = stages != null && i < stages.Length ? stages[i] : -1;
        }

        public bool Threatens(Vector3 feet, float radius, double now)
        {
            if (!IsConfigured || !Advancing(now)) return false;
            Vector3 forward = (boundary.position - start.position).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 offset = feet - start.position;
            float depth = Vector3.Dot(offset, forward);
            float length = Vector3.Distance(start.position, boundary.position);
            // 경계 너머는 판정에서 제외한다. 뒤에 남은 마지막 출발자도 압박한다.
            return depth >= -radius && depth < length && depth <= Vector3.Dot(Front(now) - start.position, forward) + radius
                && Mathf.Abs(Vector3.Dot(offset, right)) <= halfWidth + radius
                && offset.y >= -10f && offset.y <= height;
        }

        public void Present(double now)
        {
            if (!IsConfigured) return;
            bool shown = available && Armed && !Finished;
            visual.gameObject.SetActive(shown);
            visual.position = Front(now) + Vector3.up * (height * 0.5f);
            visual.rotation = start.rotation;
            visual.localScale = new Vector3(halfWidth * 2f, height, Advancing(now) ? 0.45f : 0.1f);
            if (effect == null) return;
            // 파티클이 있으면 상자는 판정 크기 표시용으로만 남기고 그리지 않는다.
            foreach (var box in visual.GetComponents<Renderer>()) box.enabled = false;
            if (effect.gameObject.activeSelf != shown) effect.gameObject.SetActive(shown);
            if (!shown) return;
            effect.SetPositionAndRotation(Front(now), start.rotation);
            effect.localScale = new Vector3(halfWidth * 2f, 1f, 1f);
            CacheEffect();
            float rate = Advancing(now) ? 1f : warningEmission;
            for (int i = 0; i < effectSystems.Length; i++)
            {
                var emission = effectSystems[i].emission;
                emission.rateOverTimeMultiplier = effectRates[i] * rate;
            }
        }

        void CacheEffect()
        {
            if (effectSystems != null) return;
            effectSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
            effectRates = new float[effectSystems.Length];
            for (int i = 0; i < effectSystems.Length; i++) effectRates[i] = effectSystems[i].emission.rateOverTimeMultiplier;
        }

        void OnDisable()
        {
            if (visual != null) visual.gameObject.SetActive(false);
            if (effect != null) effect.gameObject.SetActive(false);
        }
    }
}
