using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>출발 운송/쉼터 승강의 배치. 상태·요청은 HumanCannonRouteSystem에서 관리한다.</summary>
    public sealed class HumanCannonTransport : MonoBehaviour
    {
        [SerializeField] Transform platform;
        [SerializeField] Transform start;
        [SerializeField] Transform destination;
        [Tooltip("발판의 좌우·앞뒤 반폭(m). 플레이어와 트램폴린 전체가 올라와야 출발한다.")]
        [SerializeField] Vector2 halfSize = new(3f, 4f);
        [Tooltip("시험용 이동 시간(초). 최종 밸런스가 아니다.")]
        [Min(0.1f), SerializeField] float travelSeconds = 5f;
        [Tooltip("좌우 바닥의 바깥쪽 경첩. 두 장이 아래로 대칭 회전한다.")]
        [SerializeField] Transform leftHinge, rightHinge;
        [SerializeField] Collider[] trapColliders = System.Array.Empty<Collider>();
        [Min(0.05f), SerializeField] float doorOpenSeconds = 0.35f;
        [Min(0.1f), SerializeField] float emptyResetSeconds = 1.2f;
        [SerializeField] Renderer[] boardingIndicators = System.Array.Empty<Renderer>();
        MaterialPropertyBlock indicatorBlock;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly Color OccupiedColor = new(0.15f, 0.95f, 0.3f, 1f);

        public float TravelSeconds => travelSeconds;
        public Transform Platform => platform;
        public Vector3 StartPosition => start.position;
        public Quaternion Rotation => start.rotation;
        public Vector3 Position(float progress) => Vector3.Lerp(start.position, destination.position, progress);
        public bool IsConfigured => platform != null && start != null && destination != null && travelSeconds > 0;
        public bool HasTrapDoors => leftHinge != null && rightHinge != null && trapColliders.Length == 2;
        public float EmptyResetSeconds => emptyResetSeconds;
        public Vector2 HalfSize => halfSize;

        public void Present(float progress)
        {
            if (IsConfigured) platform.SetPositionAndRotation(Position(progress), Rotation);
        }

        public void PresentTrap(bool open, double openedAt, double now)
        {
            float angle = open ? 90f * Mathf.Clamp01((float)(now - openedAt) / Mathf.Max(0.05f, doorOpenSeconds)) : 0;
            if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(0, 0, -angle);
            if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(0, 0, angle);
            // 패널의 옆면/모서리에 매달리지 않고 두 사람 모두 즉시 바닥을 잃는다.
            foreach (var floor in trapColliders) if (floor != null) floor.enabled = !open;
        }

        public void PresentOccupied(bool occupied)
        {
            indicatorBlock ??= new MaterialPropertyBlock();
            foreach (var indicator in boardingIndicators)
            {
                if (indicator == null) continue;
                indicator.GetPropertyBlock(indicatorBlock);
                var material = indicator.sharedMaterial;
                var idle = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
                indicatorBlock.SetColor(BaseColor, occupied ? OccupiedColor : idle);
                indicator.SetPropertyBlock(indicatorBlock);
            }
        }

        public bool ContainsPassenger(Vector3 feet)
        {
            if (!IsConfigured) return false;
            Vector3 point = Quaternion.Inverse(Rotation) * (feet - platform.position);
            // 점프·트램폴린 위 동승도 포함하되 아래로 떨어진 사람은 세지 않는다.
            return Mathf.Abs(point.x) < halfSize.x && Mathf.Abs(point.z) < halfSize.y
                && point.y >= -0.15f && point.y <= 2f;
        }

        public bool ContainsFallColumn(Vector3 contact)
        {
            if (!IsConfigured) return false;
            Vector3 point = Quaternion.Inverse(Rotation) * (contact - platform.position);
            return Mathf.Abs(point.x) < halfSize.x && Mathf.Abs(point.z) < halfSize.y && point.y <= 2f;
        }

        public bool ContainsFeet(Vector3 feet, float margin = 0.15f)
        {
            if (!IsConfigured) return false;
            Vector3 point = Quaternion.Inverse(Rotation) * (feet - platform.position);
            const float tolerance = 0.00001f;
            return Mathf.Abs(point.x) <= halfSize.x - margin + tolerance && Mathf.Abs(point.z) <= halfSize.y - margin + tolerance
                && point.y >= -0.15f - tolerance && point.y <= 0.3f + tolerance;
        }

        public bool ContainsLoad(HumanCannonTrampoline trampoline)
        {
            if (trampoline == null || !ContainsFeet(trampoline.Handle.position)) return false;
            Vector2 size = trampoline.HalfSize;
            foreach (int x in new[] { -1, 1 })
                foreach (int z in new[] { -1, 1 })
                    if (!ContainsFeet(trampoline.transform.TransformPoint(new Vector3(x * size.x, 0, z * size.y)), 0.05f))
                        return false;
            return true;
        }
    }
}
