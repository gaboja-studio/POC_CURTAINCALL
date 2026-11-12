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
            if (Physics.Raycast(centre, Vector3.down, out var hit, step.magnitude + .12f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
            {
                transform.position = hit.point + Vector3.up * .03f;
                // 판이 바닥에 평평하게: 판의 면(로컬 z)이 위를 보게 눕힌다.
                transform.rotation = Quaternion.LookRotation(Vector3.up, outward);
                landed = true; return;
            }
            transform.position += step;
            if (turned < 80f) { float turn = Mathf.Min(spin * dt, 80f - turned); transform.Rotate(Vector3.right, -turn, Space.Self); turned += turn; }
        }
    }
}
