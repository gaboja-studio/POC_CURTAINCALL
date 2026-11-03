using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>한 마네킹의 시작 위치·타입·독립 순찰 경로. 런타임에는 Director가 생성한다.</summary>
    public sealed class PursuitPlacement : MonoBehaviour
    {
        public GameObject Prefab;
        public Transform[] PatrolPoints = System.Array.Empty<Transform>();
        public bool UseRoomPatrol;
        [Tooltip("배치 이동 후 순찰 경로까지 이 거리(m)를 초과하면 경로를 해제합니다. 0은 자동 해제 안 함.")]
        [Min(0)] public float RouteDetachDistance = 10;
        public Transform[] Route(PursuitRoom room) => UseRoomPatrol && room != null ? room.PatrolPoints : PatrolPoints;
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .25f, .15f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .2f, .3f);
            Gizmos.DrawRay(transform.position, transform.forward);
            var points = Route(GetComponentInParent<PursuitRoom>());
            if (points == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < points.Length; i++) {
                if (points[i] == null) continue;
                Gizmos.DrawWireSphere(points[i].position, .2f);
                var next = points[(i + 1) % points.Length];
                if (next != null) Gizmos.DrawLine(points[i].position, next.position);
            }
        }
    }
}
