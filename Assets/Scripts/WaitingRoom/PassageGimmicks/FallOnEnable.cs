using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 켜지는 순간 앞으로 밀려 바닥에 떨어져 눕는 물체(환풍구 덮개, 2026-11-13 사용자: 열면 뚜껑이 떨어지게).
    /// 물리 엔진을 쓰지 않아 사람을 밀거나 길을 막지 않는다. 각 화면에서 따로 보이는 연출이고 판정과 무관하다.
    /// 로컬 -z가 떨어져 나가는 쪽(방 쪽)이다.
    /// </summary>
    public sealed class FallOnEnable : MonoBehaviour
    {
        [SerializeField, Min(0f)] float push = .9f;
        [SerializeField, Min(0f)] float spin = 260f;
        Vector3 restPosition; Quaternion restRotation;
        Vector3 outward; float fallSpeed, turned; bool landed, captured;

        void OnEnable()
        {
            if (!captured) { restPosition = transform.localPosition; restRotation = transform.localRotation; captured = true; }
            transform.localPosition = restPosition; transform.localRotation = restRotation;
            outward = -transform.forward; fallSpeed = 0f; turned = 0f; landed = false;
        }

        void Update()
        {
            if (landed) return;
            float dt = Time.deltaTime; fallSpeed += 9.8f * dt;
            var step = outward * (push * dt) + Vector3.down * (fallSpeed * dt);
            // 덮개 가운데에서 아래로 쏴 바닥(가구 포함)에 닿으면 눕힌다.
            var centre = transform.position + transform.up * .1f;
            // 캐릭터 몸에 닿아도 그 위에 눕지 않고 지나쳐 실제 바닥까지 떨어진다(2026-11-17 사용자: 캐릭터 위 허공에 떠 있음).
            if (TryFloor(centre, step.magnitude + .12f, out var hit))
            {
                transform.position = hit.point + Vector3.up * .03f;
                // 판이 바닥에 평평하게: 판의 면(로컬 z)이 위를 보게 눕힌다.
                transform.rotation = Quaternion.LookRotation(Vector3.up, outward);
                landed = true; return;
            }
            transform.position += step;
            if (turned < 80f) { float turn = Mathf.Min(spin * dt, 80f - turned); transform.Rotate(Vector3.right, -turn, Space.Self); turned += turn; }
        }

        readonly RaycastHit[] hits = new RaycastHit[16];
        bool TryFloor(Vector3 from, float distance, out RaycastHit floor)
        {
            floor = default; float best = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || hits[i].distance >= best
                    || c.GetComponentInParent<CurtainCall.Network.PlayerSync.NetworkPlayer>() != null) continue;
                best = hits[i].distance; floor = hits[i];
            }
            return best < float.PositiveInfinity;
        }
    }
}
