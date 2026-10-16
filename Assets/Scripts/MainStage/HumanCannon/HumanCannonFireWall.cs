using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>서버 시각으로 구간 안에서만 움직이는 불꽃 압박. 실제 사망 판정은 호스트 RouteSystem만 실행한다.</summary>
    public sealed class HumanCannonFireWall : MonoBehaviour
    {
        [SerializeField] Transform start;
        [SerializeField] Transform boundary;
        [SerializeField] Transform visual;
        [Tooltip("시험용 불꽃 예고 시간(초). 마지막 대포 출발 여부와 무관하다.")]
        [Min(0.1f), SerializeField] float warningSeconds = 15f;
        [Tooltip("시험용 전진 속도(m/s). 최종 밸런스가 아니다.")]
        [Min(0.01f), SerializeField] float speed = 0.7f;
        [Tooltip("공중/지상 압박 범위의 좌우 반폭(m).")]
        [Min(1f), SerializeField] float halfWidth = 18f;
        [Tooltip("바닥 위 불꽃 판정 높이(m).")]
        [Min(1f), SerializeField] float height = 24f;
        [Tooltip("기획의 접촉 사망 설계안을 시험한다. 속도·판정은 실제 플레이에서 조정한다.")]
        [SerializeField] bool killOnContact = true;
        public bool KillOnContact => killOnContact;
        public double ArmedAt { get; private set; } = -1;
        public bool Finished { get; private set; }
        public bool Armed => ArmedAt >= 0;
        public bool IsConfigured => start != null && boundary != null && visual != null && speed > 0;
        public double EndTime => ArmedAt + warningSeconds + (IsConfigured ? Vector3.Distance(start.position, boundary.position) / speed : 0);
        public double RemainingWarning(double now) => Armed && !Finished ? System.Math.Max(0, ArmedAt + warningSeconds - now) : 0;
        public bool Advancing(double now) => double.IsFinite(now) && now >= 0 && Armed && !Finished && now >= ArmedAt + warningSeconds;
        public Vector3 Front(double now)
        {
            if (!IsConfigured) return transform.position;
            float distance = double.IsFinite(now) && Armed ? (float)System.Math.Max(0, now - ArmedAt - warningSeconds) * speed : 0;
            return Vector3.MoveTowards(start.position, boundary.position, distance);
        }

        public bool Arm(double now)
        {
            if (Armed || !IsConfigured || !double.IsFinite(now) || now < 0) return false;
            ArmedAt = now;
            return true;
        }

        public bool Tick(double now)
        {
            if (!double.IsFinite(now) || now < 0 || !Armed || Finished || now < EndTime) return false;
            Finished = true;
            return true;
        }

        public void ResetWall() { ArmedAt = -1; Finished = false; Present(0); }
        public void Receive(double armedAt, bool finished) { ArmedAt = armedAt; Finished = finished; }

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
            visual.gameObject.SetActive(Armed && !Finished);
            visual.position = Front(now) + Vector3.up * (height * 0.5f);
            visual.rotation = start.rotation;
            visual.localScale = new Vector3(halfWidth * 2f, height, Advancing(now) ? 0.45f : 0.1f);
        }
    }
}
