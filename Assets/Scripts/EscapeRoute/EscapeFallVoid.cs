using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>바닥이 없는 구간의 낙사를 호스트가 확정한다. 다른 구간의 개발용 추락 옵션은 바꾸지 않는다.</summary>
    public sealed class EscapeFallVoid : MonoBehaviour
    {
        [Tooltip("월드 X/Z 범위"), SerializeField] Rect footprint = new(20, -82, 126, 100);
        [Tooltip("이 월드 높이 아래로 떨어지면 낙사"), SerializeField] float fallHeight = -30;
        [Tooltip("시설을 통째 배치했을 때 원본 좌표계. 비어 있으면 기존 월드 좌표."), SerializeField] Transform coordinateSpace;
        public bool Contains(Vector3 feet)
        {
            if (coordinateSpace != null) feet = coordinateSpace.InverseTransformPoint(feet);
            return float.IsFinite(feet.x) && float.IsFinite(feet.y) && float.IsFinite(feet.z)
            && feet.y <= fallHeight && feet.x >= footprint.xMin && feet.x <= footprint.xMax
            && feet.z >= footprint.yMin && feet.z <= footprint.yMax;
        }

        void Update()
        {
            var session = NetworkSessionManager.Instance;
            if (session == null || !session.IsHost || session.GameState != GameSessionState.Playing) return;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State == PlayerState.Normal && Contains(player.transform.position))
                    player.ServerKill(); // 공용 사망/리타이어 및 검토 모드 보호 판정을 사용한다.
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .15f, .1f, .7f);
            Gizmos.matrix = coordinateSpace != null ? coordinateSpace.localToWorldMatrix : Matrix4x4.identity;
            Gizmos.DrawWireCube(new Vector3(footprint.center.x, fallHeight - 3, footprint.center.y),
                new Vector3(footprint.width, 6, footprint.height));
        }
    }
}
