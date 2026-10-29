using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>세로 속도나 반동을 바꾸지 않는 구간 송풍. 충돌 이동은 로컬 Pilot 한 곳에서만 적용한다.</summary>
    public sealed class HumanCannonWindZone : MonoBehaviour
    {
        [SerializeField] BoxCollider area;
        [SerializeField] HumanCannonHazardCue cue;
        [SerializeField] Vector3 direction = Vector3.right;
        [SerializeField] bool affectsGround = true;
        [SerializeField] bool affectsAir = true;
        [SerializeField] LineRenderer[] streams = System.Array.Empty<LineRenderer>();
        public BoxCollider Area => area;
        public Vector3 Direction => Vector3.ProjectOnPlane(transform.TransformDirection(direction), Vector3.up).normalized;
        public bool Contains(Vector3 feet, float height, bool airborne) => isActiveAndEnabled && area != null
            && (cue == null || HumanCannonHazardCourse.Current != null && cue.Live(HumanCannonHazardCourse.Current.Elapsed))
            && (airborne ? affectsAir : affectsGround) && area.bounds.Contains(feet + Vector3.up * height * 0.5f);

        void Update() => PresentFlow(HumanCannonHazardCourse.Current != null
            ? HumanCannonHazardCourse.Current.Elapsed : Time.timeAsDouble);

        public void PresentFlow(double elapsed)
        {
            if (area == null) return;
            Vector3 half = area.size * 0.5f;
            float sign = direction.x < 0 ? -1 : 1;
            for (int i = 0; i < streams.Length; i++)
            {
                var line = streams[i];
                if (line == null) continue;
                // 흐름은 시각 안내다. 실제 밀림은 WindVelocity의 승인 난도값만 사용한다.
                float travel = Mathf.Repeat((float)(elapsed * 0.16) + i * 0.618034f, 1);
                float x = Mathf.Lerp(-half.x, half.x, travel) * sign;
                float y = Mathf.Lerp(-half.y + 0.3f, half.y - 0.3f, (i % 4) / 3f);
                float z = Mathf.Lerp(-half.z, half.z, (i / 4 + 0.5f) / 4f);
                line.SetPosition(0, area.center + new Vector3(x, y, z));
                line.SetPosition(1, area.center + new Vector3(Mathf.Clamp(x - sign * 1.8f, -half.x, half.x), y, z));
            }
        }
    }
}
