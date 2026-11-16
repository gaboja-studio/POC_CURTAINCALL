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
        CameraMeshCollision meshCollision;
        MeshFilter meshFilter;
        Renderer surfaceRenderer;
        static readonly HashSet<CameraCollisionSurface> surfaces = new();
        void OnEnable() { surfaceRenderer = GetComponent<Renderer>(); surfaces.Add(this); }
        void OnDisable() => surfaces.Remove(this);

        public static float LimitDistance(Scene scene, Vector3 origin, Vector3 direction, float radius, float distance)
        {
            foreach (var surface in surfaces) {
                if (surface == null || !surface.isActiveAndEnabled || surface.gameObject.scene != scene) continue;
                // Opened/hidden architectural panels must not leave invisible camera blockers.
                if (surface.surfaceRenderer != null && !surface.surfaceRenderer.enabled) continue;
                if (surface.IntersectWithin(origin, direction, radius, distance, out float hit)) distance = Mathf.Min(distance, hit);
            }
            return distance;
        }

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
