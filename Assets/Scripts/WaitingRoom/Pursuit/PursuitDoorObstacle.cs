using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>잠긴 문은 막고 열거나 비활성화하면 통행한다. 원본 문의 규칙은 변경하지 않는다.</summary>
    public sealed class PursuitDoorObstacle : MonoBehaviour
    {
        public Collider Source;
        NavMeshObstacle obstacle;
        void Awake()
        {
            obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true; obstacle.carveOnlyStationary = false;
        }
        void LateUpdate()
        {
            if (obstacle == null || Source == null) return;
            bool blocking = Source.enabled && Source.gameObject.activeInHierarchy;
            bool changed = false;
            if (obstacle.enabled != blocking) { obstacle.enabled = blocking; changed = true; }
            Vector3 center, size;
            if (Source is BoxCollider box) { center = box.center; size = box.size; }
            else { center = transform.InverseTransformPoint(Source.bounds.center); size = Source.bounds.size; }
            // 같은 값을 매 프레임 다시 넣으면 NavMesh를 계속 다시 깎아 추격자 경로가 무효화된다. 바뀐 때만 반영한다.
            if ((obstacle.center - center).sqrMagnitude > .0001f) { obstacle.center = center; changed = true; }
            if ((obstacle.size - size).sqrMagnitude > .0001f) { obstacle.size = size; changed = true; }
            // 문이 잠기거나 열린 때만 추격자들이 고정 목표 경로를 다시 계산한다.
            if (changed) PursuitNavigation.Invalidate();
        }
    }
}
