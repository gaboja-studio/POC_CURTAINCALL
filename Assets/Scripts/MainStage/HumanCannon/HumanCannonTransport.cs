using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>출발 운송/쉼터 승강의 배치. 상태·요청은 HumanCannonRouteSystem에서 관리한다.</summary>
    public sealed class HumanCannonTransport : MonoBehaviour
    {
        [SerializeField] Transform platform;
        [SerializeField] Transform start;
        [SerializeField] Transform destination;
        [Tooltip("선택 경유점. 있으면 시작→경유점→도착을 같은 속도로 꺾어 이동한다.")]
        [SerializeField] Transform via;
        [Tooltip("발판의 좌우·앞뒤 반폭(m). 플레이어와 트램폴린 전체가 올라와야 출발한다.")]
        [SerializeField] Vector2 halfSize = new(3f, 4f);
        [Tooltip("시험용 이동 시간(초). 최종 밸런스가 아니다.")]
        [Min(0.1f), SerializeField] float travelSeconds = 5f;
        [Tooltip("좌우 바닥의 바깥쪽 경첩. 두 장이 아래로 대칭 회전한다.")]
        [SerializeField] Transform leftHinge, rightHinge;
        [SerializeField] Collider[] trapColliders = System.Array.Empty<Collider>();
        [Min(0.05f), SerializeField] float doorOpenSeconds = 0.35f;
        [Min(0.1f), SerializeField] float emptyResetSeconds = 1.2f;
        [Tooltip("켜면 2명 이상 탑승 시 바닥이 열린다. 끄면 바닥은 닫힌 채 출발만 막고, 과승·트램폴린 없음은 발판을 빨간색으로 표시한다.")]
        [SerializeField] bool overloadTrap = true;
        [SerializeField] Renderer[] boardingIndicators = System.Array.Empty<Renderer>();
        [Tooltip("이동·복귀 중 다른 플레이어가 발판에 올라타지 못하게 둘레에 켜는 투명 벽 높이(m). 0이면 벽 없음.")]
        [Min(0f), SerializeField] float barrierHeight = 3f;
        public const float BarrierThickness = 0.3f;
        GameObject barrier;
        MaterialPropertyBlock indicatorBlock;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly Color OccupiedColor = new(0.15f, 0.95f, 0.3f, 1f);
        public static readonly Color RejectedColor = new(0.95f, 0.15f, 0.12f, 1f);

        public float TravelSeconds => travelSeconds;
        public Transform Platform => platform;
        public Vector3 StartPosition => start.position;
        public Quaternion Rotation => start.rotation;
        public Transform Via => via;
        public Vector3 Position(float progress)
        {
            if (via == null) return Vector3.Lerp(start.position, destination.position, progress);
            // 두 구간 길이에 비례해 나눠 꺾이는 지점에서도 속도가 같다.
            float first = Vector3.Distance(start.position, via.position), second = Vector3.Distance(via.position, destination.position);
            float travelled = Mathf.Clamp01(progress) * (first + second);
            return travelled <= first
                ? Vector3.Lerp(start.position, via.position, first > 0f ? travelled / first : 1f)
                : Vector3.Lerp(via.position, destination.position, second > 0f ? (travelled - first) / second : 1f);
        }
        public bool IsConfigured => platform != null && start != null && destination != null && travelSeconds > 0;
        public bool HasTrapDoors => leftHinge != null && rightHinge != null && trapColliders.Length == 2;
        public float EmptyResetSeconds => emptyResetSeconds;
        public bool OverloadTrap => overloadTrap;
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

        /// <summary>출발 대기·이동 중은 초록, 탑승 거부(과승·트램폴린 없음)는 빨강, 그 외는 재질 색.</summary>
        public void PresentOccupied(bool occupied, bool rejected = false)
        {
            indicatorBlock ??= new MaterialPropertyBlock();
            foreach (var indicator in boardingIndicators)
            {
                if (indicator == null) continue;
                indicator.GetPropertyBlock(indicatorBlock);
                var material = indicator.sharedMaterial;
                var idle = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
                indicatorBlock.SetColor(BaseColor, occupied ? OccupiedColor : rejected ? RejectedColor : idle);
                indicator.SetPropertyBlock(indicatorBlock);
            }
        }

        /// <summary>이동·복귀 중 둘레 투명 벽. 지붕은 두지 않는다(위에 서서 같이 이동하지 않도록).</summary>
        public void PresentBarrier(bool blocked)
        {
            if (barrier == null)
            {
                if (!blocked || !IsConfigured || barrierHeight <= 0) return;
                barrier = BuildBarrier();
            }
            if (barrier.activeSelf != blocked) barrier.SetActive(blocked);
        }

        public bool BarrierActive => barrier != null && barrier.activeSelf;

        GameObject BuildBarrier()
        {
            var root = new GameObject("BoardingBarrier") { layer = platform.gameObject.layer };
            root.transform.SetParent(platform, false);
            // 발판 크기와 무관하게 벽 치수를 미터로 유지한다.
            Vector3 scale = platform.lossyScale;
            root.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            float h = barrierHeight, t = BarrierThickness, x = halfSize.x, z = halfSize.y;
            foreach (int side in new[] { -1, 1 })
            {
                Wall(root, new Vector3(side * (x + t / 2), h / 2, 0), new Vector3(t, h, 2 * (z + t)));
                Wall(root, new Vector3(0, h / 2, side * (z + t / 2)), new Vector3(2 * x, h, t));
            }
            root.SetActive(false);
            return root;
        }

        static void Wall(GameObject root, Vector3 center, Vector3 size)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        /// <summary>발판 위 영역의 발 위치를 가장 가까운 옆면 벽 바깥으로 옮긴 위치.</summary>
        public Vector3 OutsideBarrier(Vector3 feet, float radius)
        {
            Vector3 point = Quaternion.Inverse(Rotation) * (feet - platform.position);
            float clear = BarrierThickness + Mathf.Max(0, radius) + 0.05f;
            if (halfSize.x - Mathf.Abs(point.x) <= halfSize.y - Mathf.Abs(point.z))
                point.x = (point.x < 0 ? -1 : 1) * (halfSize.x + clear);
            else point.z = (point.z < 0 ? -1 : 1) * (halfSize.y + clear);
            return platform.position + Rotation * point;
        }

        /// <summary>출발점 발판의 진행 반대쪽 벽 바깥(탑승하던 갑판). 이동 중 발판 밖에서 버틴 사람을 되돌릴 자리.</summary>
        public Vector3 BoardingSide(float radius)
        {
            Vector3 first = via != null ? via.position : destination.position;
            Vector3 travel = Vector3.ProjectOnPlane(first - start.position, Vector3.up);
            Vector3 local = travel.sqrMagnitude > 0.0001f ? Quaternion.Inverse(Rotation) * travel.normalized : Vector3.forward;
            float extent = Mathf.Abs(local.x) * halfSize.x + Mathf.Abs(local.z) * halfSize.y;
            return start.position - Rotation * local * (extent + BarrierThickness + Mathf.Max(0, radius) + 0.3f);
        }

        public bool ContainsPassenger(Vector3 feet)
        {
            if (!IsConfigured) return false;
            Vector3 point = Quaternion.Inverse(Rotation) * (feet - platform.position);
            // 점프·트램폴린 위 동승도 포함하되 아래로 떨어진 사람은 세지 않는다.
            return Mathf.Abs(point.x) < halfSize.x && Mathf.Abs(point.z) < halfSize.y
                && point.y >= -0.15f && point.y <= 2f;
        }

        /// <summary>발판 면적 위 높이 제한 없는 기둥. 트램폴린 반동으로 높이 뜬 사람도 발판 위로 본다.</summary>
        public bool ContainsAirColumn(Vector3 point)
        {
            if (!IsConfigured) return false;
            Vector3 local = Quaternion.Inverse(Rotation) * (point - platform.position);
            return Mathf.Abs(local.x) < halfSize.x && Mathf.Abs(local.z) < halfSize.y && local.y >= -0.15f;
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
            Vector2 size = trampoline.LoadHalfSize;
            if (trampoline.CircularSurface)
            {
                // 원형 프레임은 회전해도 운송 면적이 늘지 않는다. 손잡이는 위에서 별도로 검사한다.
                float radius = Mathf.Max(trampoline.transform.TransformVector(Vector3.right * size.x).magnitude,
                    trampoline.transform.TransformVector(Vector3.forward * size.y).magnitude);
                foreach (Vector3 edge in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                    if (!ContainsFeet(trampoline.transform.position + Rotation * edge * radius, 0.05f)) return false;
                return true;
            }
            foreach (int x in new[] { -1, 1 })
                foreach (int z in new[] { -1, 1 })
                    if (!ContainsFeet(trampoline.transform.TransformPoint(new Vector3(x * size.x, 0, z * size.y)), 0.05f))
                        return false;
            return true;
        }
    }
}
