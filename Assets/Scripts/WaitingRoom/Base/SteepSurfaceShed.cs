using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 걸을 수 없는 가파른 면(로비 천막 등)에 올라서지 못하게 한다(2026-11-20 사용자: 천막 위는 플레이어만 올라가고 추격자가 따라오지 못함).
    /// 둥근 몸통이 가파른 면에 닿으면 접지로 판정되어 점프를 이어 가며 꼭대기까지 올라설 수 있었다.
    /// 지정한 면의 가파른 곳을 밟으면 이동 처리보다 먼저 면 바깥쪽으로 떼어 놓아, 그 프레임에 접지·점프가 되지 않고 미끄러져 내려오게 한다.
    /// 다른 물체의 바닥·윗면을 밟고 있으면 건드리지 않는다. 공용 이동 코드는 바꾸지 않는다(<see cref="RailTopShed"/>와 같은 방식).
    /// </summary>
    [DefaultExecutionOrder(-10)] // PlayerMover(0)보다 먼저
    public sealed class SteepSurfaceShed : MonoBehaviour
    {
        [Tooltip("올라서지 못하게 할 충돌체(통째로 설 곳이 아닌 것만 넣는다). 가파른 면에 닿거나 이 충돌체 위에 서면 바깥으로 떼어 놓는다.")]
        [SerializeField] Collider[] surfaces = System.Array.Empty<Collider>();
        [Tooltip("면 바깥쪽으로 떼어 놓는 속도(m/s).")]
        [SerializeField, Min(.5f)] float shedSpeed = 4f;

        readonly Dictionary<PlayerMover, CharacterController> bodies = new();
        static readonly Collider[] overlaps = new Collider[16];

        void Update()
        {
            var players = PlayerMover.All;
            for (int p = 0; p < players.Count; p++)
            {
                var mover = players[p];
                // 내 화면이 움직이는 몸만 민다. 다른 사람 몸은 그 사람 화면의 결과를 따라온다.
                if (mover == null || !mover.Simulated) continue;
                if (!bodies.TryGetValue(mover, out var body) || body == null) bodies[mover] = body = mover.GetComponent<CharacterController>();
                if (body == null || !body.enabled || !body.isGrounded) continue;
                Vector3 feet = mover.transform.position;
                float radius = mover.BodyRadius;
                if (!Near(feet, radius + .5f)) continue;

                if (!TryShed(mover, feet, radius, body.skinWidth, body.slopeLimit, out var away)) continue;
                body.Move(away * (shedSpeed * Time.deltaTime));
            }
        }

        /// <summary>
        /// 이 자리의 몸을 떼어 놓아야 하면 그 수평 방향을 준다. 발밑이 다른 물체의 걸을 수 있는 면(진짜 바닥·소품 윗면)이면 건드리지 않는다.
        /// 지정한 면 자체는 평평한 조각(천막 꼭대기)이라도 설 곳이 아니므로 떼어 놓는다.
        /// </summary>
        public bool TryShed(PlayerMover mover, Vector3 feet, float radius, float skin, float slopeLimit, out Vector3 away)
        {
            Collider standingOn = null;
            if (WalkableSupport.TryGetSupport(mover, feet, out var support))
            {
                if (System.Array.IndexOf(surfaces, support.collider) < 0) { away = default; return false; }
                standingOn = support.collider;
            }
            if (TryShedDirection(feet, radius, skin, Mathf.Cos(slopeLimit * Mathf.Deg2Rad), out away)) return true;
            // 뾰족한 꼭대기·모서리는 면 검사에 잡히지 않는다. 몸통 아래 반구가 지정한 면에 겹쳐 있으면 걸린 것으로 본다.
            if (standingOn == null)
            {
                int count = Physics.OverlapSphereNonAlloc(feet + Vector3.up * radius, radius + skin + .05f, overlaps,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count && standingOn == null; i++)
                    if (System.Array.IndexOf(surfaces, overlaps[i]) >= 0) standingOn = overlaps[i];
            }
            if (standingOn == null) return false;
            away = feet - standingOn.bounds.center; away.y = 0f;
            away = away.sqrMagnitude < .0001f ? Vector3.forward : away.normalized;
            return true;
        }

        static readonly Vector3[] Probes = BuildProbes();
        static Vector3[] BuildProbes()
        {
            // 몸통 아래 반구에서 아래·비스듬히(45°·75°) 16방향. 가파른 면의 접점은 바로 아래가 아니라 옆에 있다.
            var list = new List<Vector3> { Vector3.down };
            foreach (float tilt in new[] { 45f, 75f })
                for (int k = 0; k < 8; k++)
                    list.Add(Quaternion.Euler(0f, k * 45f, 0f) * (Quaternion.Euler(tilt, 0f, 0f) * Vector3.down));
            return list.ToArray();
        }

        /// <summary>
        /// 몸통 아래 반구가 지정한 면의 가파른 곳에 닿아 있으면 그 면에서 멀어지는 수평 방향을 준다.
        /// 접힌 주름에 끼어 양쪽 면이 서로 반대로 밀면 면 전체의 중심에서 바깥쪽으로 민다.
        /// </summary>
        public bool TryShedDirection(Vector3 feet, float radius, float skin, float minWalkableNormalY, out Vector3 away)
        {
            away = default;
            Vector3 center = feet + Vector3.up * radius, sum = Vector3.zero;
            float reach = (radius + skin) * 1.25f + .05f;
            Collider touched = null;
            for (int i = 0; i < Probes.Length; i++)
            {
                if (!Physics.Raycast(center, Probes[i], out var hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || System.Array.IndexOf(surfaces, hit.collider) < 0 || hit.normal.y >= minWalkableNormalY) continue;
                // 면에 실제로 닿아 있을 때만(면까지 수직 거리로 잰다).
                if (Vector3.Dot(center - hit.point, hit.normal) > radius + skin + .06f) continue;
                touched = hit.collider;
                sum += new Vector3(hit.normal.x, 0f, hit.normal.z);
            }
            if (touched == null) return false;
            if (sum.magnitude < .3f)
            {
                sum = feet - touched.bounds.center; sum.y = 0f;
                if (sum.sqrMagnitude < .0001f) sum = Vector3.forward;
            }
            away = sum.normalized;
            return true;
        }

        bool Near(Vector3 feet, float range)
        {
            for (int i = 0; i < surfaces.Length; i++)
            {
                var surface = surfaces[i];
                if (surface != null && surface.enabled && surface.gameObject.activeInHierarchy
                    && surface.bounds.SqrDistance(feet) <= range * range) return true;
            }
            return false;
        }
    }
}
