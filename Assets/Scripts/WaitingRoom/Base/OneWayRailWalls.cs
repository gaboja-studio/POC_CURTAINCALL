using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 2층 통로 난간 위의 보이지 않는 벽(RailTopWall)을 한 방향으로만 막는다(2026-11-19 사용자: 통로에서 아래로 내려가는 것만 허용).
    /// - 통로 쪽에 있는 플레이어는 벽에 막히지 않아 난간을 뛰어넘어 내려갈 수 있다.
    /// - 통로 밖에 있는 플레이어는 예전처럼 막힌다(트램펄린으로 올라오기 차단, Task-148).
    /// - 넘다가 난간 윗면(폭 6cm)에 올라서면 통로 밖으로 밀어 떨어뜨린다(난간 위에 서서 끼이는 문제 방지).
    /// 공용 이동 코드는 건드리지 않는다. 충돌 무시는 각 화면에서 그 화면의 캐릭터 충돌체에만 건다.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class OneWayRailWalls : MonoBehaviour
    {
        [Tooltip("난간 위 벽. inward[i]는 그 벽에서 통로(바닥판) 쪽을 향하는 수평 방향.")]
        [SerializeField] Collider[] walls = System.Array.Empty<Collider>();
        [SerializeField] Vector3[] inward = System.Array.Empty<Vector3>();
        [Tooltip("이 거리(수평, m) 안의 벽만 매 프레임 살핀다.")]
        [SerializeField, Min(1f)] float watchRange = 4f;
        [Tooltip("난간 윗면에 선 플레이어를 통로 밖으로 미는 속도(m/s).")]
        [SerializeField, Min(.1f)] float shedSpeed = 2.5f;

        readonly Dictionary<PlayerMover, CharacterController> bodies = new();
        // 벽을 넘는 중(몸이 벽 면에 걸친 동안)에는 직전 판정을 유지한다: 넘자마자 벽이 다시 생겨 몸이 끼지 않게.
        readonly Dictionary<(int player, int wall), bool> passing = new();

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
                for (int i = 0; i < walls.Length && i < inward.Length; i++)
                {
                    var wall = walls[i];
                    if (wall == null || !wall.enabled || !wall.gameObject.activeInHierarchy) continue;
                    var bounds = wall.bounds;
                    var flat = new Vector3(feet.x, bounds.center.y, feet.z);
                    if (bounds.SqrDistance(flat) > watchRange * watchRange) continue;
                    // 벽 면에서 통로 쪽으로 얼마나 들어와 있는가(+: 통로 쪽, -: 바깥).
                    float side = Vector3.Dot(feet - bounds.center, inward[i]);
                    var key = (mover.GetInstanceID(), i);
                    passing.TryGetValue(key, out bool open);
                    if (side > 0f) open = true;
                    else if (side < -(radius + .15f)) open = false;
                    passing[key] = open;
                    // IgnoreCollision은 충돌체를 껐다 켜면 풀리므로 가까이 있는 동안 매 프레임 다시 건다.
                    Physics.IgnoreCollision(body, wall, open);
                    // 난간 윗면(벽 아랫면 높이)에 올라섰으면 통로 밖으로 민다.
                    if (open && body.isGrounded && Mathf.Abs(side) < radius + .05f
                        && feet.y > bounds.min.y - .1f && feet.y < bounds.min.y + .3f && Along(bounds, inward[i], feet) < radius)
                        body.Move(-inward[i] * (shedSpeed * Time.deltaTime) + Vector3.down * .02f);
                }
            }
        }

        // 벽 길이 방향으로 벽 끝에서 벗어난 거리(안이면 0).
        static float Along(Bounds bounds, Vector3 normal, Vector3 point)
        {
            Vector3 along = Vector3.Cross(Vector3.up, normal);
            float half = Mathf.Abs(along.x) * bounds.extents.x + Mathf.Abs(along.z) * bounds.extents.z;
            return Mathf.Max(0f, Mathf.Abs(Vector3.Dot(point - bounds.center, along)) - half);
        }

        void OnDisable()
        {
            // 꺼질 때는 예전처럼 양쪽 모두 막는다.
            foreach (var pair in bodies)
                if (pair.Value != null)
                    foreach (var wall in walls) if (wall != null) Physics.IgnoreCollision(pair.Value, wall, false);
            passing.Clear();
        }
    }
}
