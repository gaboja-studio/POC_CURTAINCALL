using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.StartLobby
{
    /// <summary>
    /// 로비 고정 3D 시점. 방향은 고정이고, 내 캐릭터 쪽으로 조금만 따라 움직이되 <see cref="bounds"/> 밖으로는 나가지 않는다.
    /// 그래서 다른 단원과 로비 전체가 늘 화면 안에 있다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class LobbyCamera : MonoBehaviour
    {
        [Tooltip("카메라가 바라보는 로비 중심(월드).")]
        [SerializeField] Vector3 focus = new(0f, 0.8f, 3f);
        [Tooltip("내 캐릭터를 따라가는 비율(0 = 완전 고정, 1 = 그대로 따라감).")]
        [SerializeField, Range(0f, 1f)] float follow = 0.3f;
        [Tooltip("카메라 이동 한계(월드 좌표, x·z). 맵 밖을 비추지 않게 작게 둔다.")]
        [SerializeField] Rect bounds = new(-1.2f, -0.6f, 2.4f, 1.2f);
        [SerializeField, Min(0.1f)] float smoothTime = 0.35f;

        Vector3 home;
        Vector3 velocity;

        void Awake() => home = transform.position;

        void LateUpdate()
        {
            Vector3 offset = Vector3.zero;
            var local = NetworkPlayer.Local;
            if (local != null && local.isActiveAndEnabled)
            {
                Vector3 d = (local.transform.position - focus) * follow;
                offset = new Vector3(Mathf.Clamp(d.x, bounds.xMin, bounds.xMax), 0f, Mathf.Clamp(d.z, bounds.yMin, bounds.yMax));
            }
            transform.position = Vector3.SmoothDamp(transform.position, home + offset, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }
}
