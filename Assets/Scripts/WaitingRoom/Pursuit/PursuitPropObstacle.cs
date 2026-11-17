using CurtainCall.HumanCannon;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 플레이어가 옮겨 내려놓는 소품(운반 트램펄린)은 NavMesh 굽기에 없어서 추격자 경로가 그 몸통을 지나갔다.
    /// 그 결과 추격자가 소품 앞·속에서 앉은 채 멈추거나 끼었다(2026-11-17 사용자 보고).
    /// 내려놓여 멈춘 동안만 NavMesh를 깎아 경로가 돌아가게 하고, 들고 있는 동안은 깎지 않는다(들고 있는 플레이어를 잡을 수 있게).
    /// 실행 중 호스트에서만 붙으며 원본 소품·프리팹은 수정하지 않는다.
    /// </summary>
    public sealed class PursuitPropObstacle : MonoBehaviour
    {
        HumanCannonTrampoline trampoline;
        NavMeshObstacle obstacle;
        bool blocking;

        public static void Track()
        {
            foreach (var prop in Object.FindObjectsByType<HumanCannonTrampoline>(FindObjectsSortMode.None))
                if (prop.GetComponent<PursuitPropObstacle>() == null) prop.gameObject.AddComponent<PursuitPropObstacle>();
        }

        void Awake()
        {
            trampoline = GetComponent<HumanCannonTrampoline>();
            // 자식 충돌체를 묶은 범위를 소품 기준 상자로 둔다(회전한 채 내려놓아도 같이 돈다).
            var bounds = new Bounds(); bool any = false;
            foreach (var c in GetComponentsInChildren<Collider>(true)) {
                if (c.isTrigger) continue;
                var b = c.bounds;
                foreach (var corner in Corners(b)) {
                    var local = transform.InverseTransformPoint(corner);
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; } else bounds.Encapsulate(local);
                }
            }
            if (!any) { enabled = false; return; }
            var holder = new GameObject("Pursuit prop obstacle") { hideFlags = HideFlags.DontSave };
            holder.transform.SetParent(transform, false);
            obstacle = holder.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center; obstacle.size = bounds.size;
            obstacle.carving = true; obstacle.carveOnlyStationary = true;
            obstacle.enabled = false;
        }

        static Vector3[] Corners(Bounds b)
        {
            Vector3 min = b.min, max = b.max;
            return new[] {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z)
            };
        }

        void LateUpdate()
        {
            if (obstacle == null) return;
            bool next = trampoline != null && trampoline.isActiveAndEnabled && trampoline.Carrier == CannonSeatState.Nobody;
            if (next == blocking) return;
            blocking = next; obstacle.enabled = next;
            // 깎기 반영 뒤 추격자들이 같은 목표라도 다시 길을 찾고 오르기 통로도 새로 만든다.
            PursuitNavigation.Invalidate(); PursuitClimbLinks.Invalidate();
        }

        void OnDestroy()
        {
            if (obstacle != null) Destroy(obstacle.gameObject);
            if (blocking) { PursuitNavigation.Invalidate(); PursuitClimbLinks.Invalidate(); }
        }
    }
}
