using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Cameras
{
    /// <summary>장식용 벽/천장의 카메라 전용 OBB. 물리 충돌과 NavMesh에는 참여하지 않는다.</summary>
    public sealed class CameraCollisionSurface : MonoBehaviour
    {
        [Tooltip("로컬 좌표의 카메라 차단 범위. 직육면체 벽/천장의 Mesh bounds로 맞춥니다.")]
        public Bounds LocalBounds = new(Vector3.zero, Vector3.one);
        static readonly HashSet<CameraCollisionSurface> surfaces = new();
        void OnEnable() => surfaces.Add(this);
        void OnDisable() => surfaces.Remove(this);

        public static float LimitDistance(Scene scene, Vector3 origin, Vector3 direction, float radius, float distance)
        {
            foreach (var surface in surfaces) {
                if (surface == null || !surface.isActiveAndEnabled || surface.gameObject.scene != scene) continue;
                if (surface.Intersect(origin, direction, radius, out float hit)) distance = Mathf.Min(distance, hit);
            }
            return distance;
        }

        public bool Intersect(Vector3 origin, Vector3 direction, float radius, out float distance)
        {
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
            Gizmos.color = new Color(.2f, .8f, 1, .7f);
            var previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(LocalBounds.center, LocalBounds.size);
            Gizmos.matrix = previous;
        }
    }
}
