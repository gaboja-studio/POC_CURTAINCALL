using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute
{
    /// <summary>바닥이 없는 구간의 낙사를 호스트가 확정한다. 다른 구간의 개발용 추락 옵션은 바꾸지 않는다.</summary>
    public sealed class EscapeFallVoid : MonoBehaviour
    {
        [Tooltip("월드 X/Z 범위"), SerializeField] Rect footprint = new(20, -82, 126, 100);
        [Tooltip("이 월드 높이 아래로 떨어지면 낙사"), SerializeField] float fallHeight = -30;
        [Tooltip("시설을 통째 배치했을 때 원본 좌표계. 비어 있으면 기존 월드 좌표."), SerializeField] Transform coordinateSpace;
        [Tooltip("심부 구조물에 내려앉아 진행이 끊기는 높이. 공중에서는 기본 낙사선만 적용합니다.")]
        [SerializeField] float strandedLandingHeight = -30;

        public bool Contains(Vector3 feet) => ContainsAtHeight(feet, fallHeight);
        internal bool ContainsStrandedLanding(Vector3 feet) => ContainsAtHeight(feet, strandedLandingHeight);

        bool ContainsAtHeight(Vector3 feet, float height)
        {
            if (coordinateSpace != null) feet = coordinateSpace.InverseTransformPoint(feet);
            return float.IsFinite(feet.x) && float.IsFinite(feet.y) && float.IsFinite(feet.z)
            && feet.y <= height && feet.x >= footprint.xMin && feet.x <= footprint.xMax
            && feet.z >= footprint.yMin && feet.z <= footprint.yMax;
        }

        void Update()
        {
            var session = NetworkSessionManager.Instance;
            if (session == null || !session.IsHost || session.GameState != GameSessionState.Playing
                || !EscapeCampaignContent.IsTraversalActive || gameObject.scene != SceneManager.GetActiveScene()
                || EscapeDevelopmentSession.AwaitingEntrance || EscapeDevelopmentSession.FallConsequencesSuppressed(this)) return;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State == PlayerState.Normal
                    && player.Slot != NetworkPlayer.NoSlot && !player.IsApplyingInitialCondition
                    && EscapeCampaignContent.CanUseRouteDevice(gameObject.scene, player.OwnerClientId)
                    && IsFatalPosition(player))
                    player.ServerKill(NetworkPlayer.DeathOrigin.MapExit);
        }

        bool IsFatalPosition(NetworkPlayer player)
        {
            Vector3 feet = player.LatestOwnerPosition;
            if (Contains(feet)) return true;
            if (!ContainsStrandedLanding(feet) || player.IsAirborne || player.IsBeingRescued
                || HumanCannonSystem.Current?.IsFlying(player.OwnerClientId) == true
                || HumanCannonPoleSystem.Current?.HeldBy(player.OwnerClientId) >= 0
                || TightropeHangSystem.Current?.IsHanging(player) == true
                || PiggybackSystem.Current?.GetSupportingPlayer(player) != null) return false;
            return player.TryGetComponent<PlayerMover>(out var mover)
                && WalkableSupport.TryGetSupport(mover, feet, out var support)
                && ContainsStrandedLanding(support.point);
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
