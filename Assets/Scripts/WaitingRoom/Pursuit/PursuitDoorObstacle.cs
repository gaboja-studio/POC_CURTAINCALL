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
            obstacle.enabled = Source.enabled && Source.gameObject.activeInHierarchy;
            if (Source is BoxCollider box) { obstacle.center = box.center; obstacle.size = box.size; }
            else { obstacle.center = transform.InverseTransformPoint(Source.bounds.center); obstacle.size = Source.bounds.size; }
        }
    }
}
