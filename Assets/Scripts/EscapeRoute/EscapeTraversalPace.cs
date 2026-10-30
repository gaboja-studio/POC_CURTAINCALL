using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>원본 조작·부상은 유지하면서 탈출로의 일반 이동만 빠르게 한다. 각 소유자에게 같은 씬 설정 적용.</summary>
    public sealed class EscapeTraversalPace : MonoBehaviour
    {
        [SerializeField, Min(1)] float movementScale = 1.35f;
        PlayerMover mover;
        float previousScale;
        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += Bind;
            NetworkPlayer.LocalDespawned += Release;
            if (NetworkPlayer.Local != null) Bind(NetworkPlayer.Local);
        }
        void Bind(NetworkPlayer player)
        {
            Release();
            if (player == null || !player.TryGetComponent(out mover)) return;
            previousScale = mover.FreeMovementSpeedScale;
            mover.FreeMovementSpeedScale = movementScale;
        }
        void Release()
        {
            if (mover != null) mover.FreeMovementSpeedScale = previousScale;
            mover = null;
        }
        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Bind;
            NetworkPlayer.LocalDespawned -= Release;
            Release();
        }
    }
}
