using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 가까이 가면 한쪽으로 걷히는 커튼(2026-11-13 사용자: 숨겨진 방 2). 내 화면에서만 움직이는 연출이다.
    /// 커튼을 한쪽 끝(edge)에 붙인 채 폭 방향(axis)으로 줄여 걷힌 모습을 만든다.
    /// </summary>
    public sealed class ProximityCurtain : MonoBehaviour
    {
        [Tooltip("커튼 폭 방향의 로컬 축(0 x, 1 y, 2 z).")]
        [SerializeField, Range(0, 2)] int axis = 2;
        [Tooltip("걷힐 때 붙어 있을 쪽 끝의 월드 위치.")]
        [SerializeField] Vector3 edge;
        [SerializeField, Range(.05f, 1f)] float openFraction = .16f;
        [SerializeField, Min(.5f)] float radius = 4.5f;
        [SerializeField, Min(.1f)] float seconds = 1.2f;
        [Tooltip("같은 자리의 천 커튼(CurtainController). 다가가면 Open, 멀어지면 Rest를 보낸다.")]
        [SerializeField] GameObject velvet;
        Vector3 closedScale, closedPosition; float t; bool ready; bool? wasNear;

        void Update()
        {
            if (!ready) { closedScale = transform.localScale; closedPosition = transform.position; ready = true; }
            bool near = ExplorationState.Instance != null && ExplorationState.Instance.LocalFeet is Vector3 feet
                && Mathf.Abs(feet.y - closedPosition.y) < 6f && new Vector2(feet.x - closedPosition.x, feet.z - closedPosition.z).sqrMagnitude < radius * radius;
            if (wasNear != near) { wasNear = near; if (velvet != null) velvet.SendMessage(near ? "Open" : "Rest", SendMessageOptions.DontRequireReceiver); }
            float next = Mathf.MoveTowards(t, near ? 1f : 0f, Time.deltaTime / seconds);
            if (Mathf.Approximately(next, t)) return;
            t = next; float k = Mathf.Lerp(1f, openFraction, Mathf.SmoothStep(0f, 1f, t));
            var scale = closedScale; scale[axis] *= k; transform.localScale = scale;
            // 폭 방향(월드)으로 edge를 기준으로 당긴다.
            var dir = transform.TransformDirection(axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward).normalized;
            float along = Vector3.Dot(closedPosition - edge, dir);
            transform.position = closedPosition - dir * (along * (1f - k));
        }
    }
}
