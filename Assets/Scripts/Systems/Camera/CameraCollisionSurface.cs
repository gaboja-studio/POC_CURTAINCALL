using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Cameras
{
    /// <summary>장식용 벽/천장의 카메라 전용 범위 또는 실제 메시 검사. 물리 충돌과 NavMesh에는 참여하지 않는다.</summary>
    public sealed class CameraCollisionSurface : MonoBehaviour
    {
        [Tooltip("로컬 좌표의 카메라 차단 범위. 직육면체 벽/천장의 Mesh bounds로 맞춥니다.")]
        public Bounds LocalBounds = new(Vector3.zero, Vector3.one);
        [Tooltip("돔/곡면/출입구가 있는 메시의 실제 면을 양면 검사합니다. 같은 오브젝트의 읽기 가능한 MeshFilter가 필요합니다.")]
        public bool UseMeshGeometry;
        [Tooltip("배관과 철골 같은 장식의 캐릭터 가림만 검사합니다.")]
        public bool VisibilityOnly;
        CameraMeshCollision meshCollision;
        MeshFilter meshFilter;
        Renderer surfaceRenderer;
        static readonly HashSet<CameraCollisionSurface> surfaces = new();
        void OnEnable() { surfaceRenderer = GetComponent<Renderer>(); surfaces.Add(this); }
        void OnDisable() => surfaces.Remove(this);

        public static float LimitDistance(Scene scene, Vector3 origin, Vector3 direction, float radius, float distance)
        {
            foreach (var surface in surfaces)
            {
                if (surface == null || surface.VisibilityOnly || !surface.IsVisibleIn(scene)) continue;
                if (surface.IntersectWithin(origin, direction, radius, distance, out float hit)) distance = Mathf.Min(distance, hit);
            }
            return distance;
        }

        internal static void CollectSightSurfaces(Scene scene, Bounds region, List<CameraCollisionSurface> nearby)
        {
            nearby.Clear();
            foreach (var surface in surfaces)
            {
                if (surface == null || !surface.IsVisibleIn(scene)) continue;
                if (surface.surfaceRenderer == null || surface.surfaceRenderer.bounds.Intersects(region))
                    nearby.Add(surface);
            }
        }

        internal static bool HasSight(List<CameraCollisionSurface> nearby, Vector3 origin, Vector3 direction, float distance)
        {
            var ray = new Ray(origin, direction);
            foreach (var surface in nearby)
            {
                if (surface.surfaceRenderer != null)
                {
                    Bounds bounds = surface.surfaceRenderer.bounds;
                    if (!bounds.Contains(origin) && (!bounds.IntersectRay(ray, out float near) || near >= distance)) continue;
                }
                if (surface.IntersectWithin(origin, direction, 0f, distance, out float hit) && hit < distance) return false;
            }
            return true;
        }

        bool IsVisibleIn(Scene scene) => isActiveAndEnabled && gameObject.scene == scene
            && (surfaceRenderer == null || surfaceRenderer.enabled && !surfaceRenderer.forceRenderingOff);

        public bool Intersect(Vector3 origin, Vector3 direction, float radius, out float distance)
            => IntersectWithin(origin, direction, radius, float.PositiveInfinity, out distance);

        bool IntersectWithin(Vector3 origin, Vector3 direction, float radius, float maximum, out float distance)
        {
            if (UseMeshGeometry) {
                if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
                meshCollision ??= new CameraMeshCollision();
                return meshCollision.Intersect(meshFilter != null ? meshFilter.sharedMesh : null,
                    transform.localToWorldMatrix, origin, direction, radius, maximum, out distance);
            }
            distance = 0;
            Vector3 scale = transform.lossyScale;
            if (Mathf.Abs(scale.x * scale.y * scale.z) < .000001f) return false;
            var bounds = LocalBounds;
            bounds.Expand(new Vector3(2 * radius / Mathf.Abs(scale.x), 2 * radius / Mathf.Abs(scale.y), 2 * radius / Mathf.Abs(scale.z)));
            Vector3 start = transform.InverseTransformPoint(origin);
            if (bounds.Contains(start)) return true;
            Vector3 localDirection = transform.InverseTransformVector(direction.normalized);
            float length = localDirection.magnitude;
            if (length < .000001f || !bounds.IntersectRay(new Ray(start, localDirection / length), out float hit)) return false;
            distance = hit / length;
            return true;
        }

        void OnDrawGizmosSelected()
        {
            if (UseMeshGeometry) return;
            Gizmos.color = new Color(.2f, .8f, 1, .7f);
            var previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(LocalBounds.center, LocalBounds.size);
            Gizmos.matrix = previous;
        }
    }
}
