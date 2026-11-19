using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 2층 통로 난간 위에 서지 못하게 한다(2026-11-19 사용자: 투명 벽은 난간 높이까지만, 트램펄린으로 넘을 수 있어야 하고 끼임이 없어야 한다).
    /// 난간(높이 1.1m)은 그대로 막고, 그 위는 비어 있어 점프·트램펄린으로 양쪽 모두 넘을 수 있다.
    /// 넘다가 난간 윗면(폭 6cm)에 올라서면 넘어가려던 쪽(오르기 전에 있던 쪽의 반대편)으로 밀어 내려보낸다. 예전에는 천장까지 보이지 않는 벽(RailTopWall)으로 막았다(Task-148).
    /// 공용 이동 코드는 건드리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class RailTopShed : MonoBehaviour
    {
        [Tooltip("난간 충돌체. inward[i]는 그 난간에서 통로(바닥판) 쪽을 향하는 수평 방향.")]
        [SerializeField] Collider[] rails = System.Array.Empty<Collider>();
        [SerializeField] Vector3[] inward = System.Array.Empty<Vector3>();
        [Tooltip("이 거리(수평, m) 안의 난간만 매 프레임 살핀다.")]
        [SerializeField, Min(1f)] float watchRange = 2f;
        [Tooltip("난간 윗면에 선 플레이어를 미는 속도(m/s).")]
        [SerializeField, Min(.1f)] float shedSpeed = 2.5f;

        readonly Dictionary<PlayerMover, CharacterController> bodies = new();
        readonly Dictionary<(int player, int rail), bool> cameFrom = new();

        void Update()
        {
            var players = PlayerMover.All;
            for (int p = 0; p < players.Count; p++)
            {
                var mover = players[p];
                if (mover == null) continue;
                if (!bodies.TryGetValue(mover, out var body) || body == null) bodies[mover] = body = mover.GetComponent<CharacterController>();
                if (body == null || !body.enabled) continue;
                Vector3 feet = mover.transform.position;
                float radius = mover.BodyRadius;
                int id = mover.GetInstanceID();
                for (int i = 0; i < rails.Length && i < inward.Length; i++)
                {
                    var rail = rails[i];
                    if (rail == null || !rail.enabled || !rail.gameObject.activeInHierarchy) continue;
                    var bounds = rail.bounds;
                    if (bounds.SqrDistance(new Vector3(feet.x, bounds.center.y, feet.z)) > watchRange * watchRange) continue;
                    float side = Vector3.Dot(feet - bounds.center, inward[i]);
                    // 난간에서 떨어져 있는 동안 어느 쪽에 있었는지 기억한다(+: 통로 쪽, -: 바깥). 난간에 올라서면 그 반대쪽, 곧 넘어가려던 쪽으로 민다.
                    if (Mathf.Abs(side) > radius + .05f) { cameFrom[(id, i)] = side > 0f; continue; }
                    // 발이 난간 윗면 높이에 있고, 몸이 난간 위에 걸쳐 있을 때만.
                    if (!body.isGrounded || feet.y < bounds.max.y - .1f || feet.y > bounds.max.y + .3f || Along(bounds, inward[i], feet) > radius) continue;
                    // 바깥에서 올라왔으면 통로 쪽으로, 통로에서 넘어가던 중이면 바깥으로. 기억이 없으면 통로 쪽(안전한 쪽).
                    // 캐릭터 속도는 쓰지 않는다: 여기서 민 움직임이 다음 프레임의 속도로 읽혀, 이동 입력과 싸우다 떨어졌다(2026-11-19 사용자: 끼어서 추락).
                    float sign = cameFrom.TryGetValue((id, i), out bool fromDeck) && fromDeck ? -1f : 1f;
                    body.Move(inward[i] * (sign * shedSpeed * Time.deltaTime) + Vector3.down * .02f);
                    break;
                }
            }
        }

        // 난간 길이 방향으로 난간 끝에서 벗어난 거리(안이면 0).
        static float Along(Bounds bounds, Vector3 normal, Vector3 point)
        {
            Vector3 along = Vector3.Cross(Vector3.up, normal);
            float half = Mathf.Abs(along.x) * bounds.extents.x + Mathf.Abs(along.z) * bounds.extents.z;
            return Mathf.Max(0f, Mathf.Abs(Vector3.Dot(point - bounds.center, along)) - half);
        }
    }
}
