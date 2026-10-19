using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeRelayGate
    {
        sealed class Binding
        {
            public NetworkPlayer Player;
            public PlayerMover Mover;
            public PlayerBalance Balance;
            public bool BodyEnabled, Simulated;
            public Vector3 Position;
            public Quaternion Rotation;
            public int Pole;
        }
        Binding binding;
        bool localAborted, localAbortWaiting;
        double nextAbortRetry;

        void UpdateBinding()
        {
            var local = NetworkPlayer.Local;
            int pole = HeldPole(local);
            if (pole < 0 && !localAbortWaiting) localAborted = false;
            if (binding != null && (local == null || binding.Player != local || local.State != PlayerState.Normal || pole != binding.Pole))
                RestoreBinding(local != null && local.State == PlayerState.Normal && IsActive && !localAborted);
            if (pole < 0 || local == null || local.State != PlayerState.Normal || localAborted || binding != null) return;
            if (!local.TryGetComponent(out PlayerMover mover) || mover.Body == null) return;
            binding = new Binding { Player = local, Mover = mover, Balance = local.GetComponent<PlayerBalance>(), Pole = pole,
                BodyEnabled = mover.Body.enabled, Simulated = mover.Simulated, Position = mover.transform.position, Rotation = mover.transform.rotation };
            mover.Teleported += OnTeleported;
            mover.ClearPendingInput(); mover.Simulated = false; mover.Body.enabled = false;
            mover.ResetExternalMotion(true);
            binding.Balance?.SetAirborne(true);
            pendingUntil = 0d;
            PlaceHeldPlayers();
        }
        void PlaceHeldPlayers()
        {
            if (!IsActive) return;
            for (int i = 0; i < PoleCount; i++)
            {
                var player = Find(HolderAt(i));
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal
                    || (player.IsLocal && (binding == null || localAborted))) continue;
                player.transform.SetPositionAndRotation(PhysicalPole(i).HangingPosition(0f), Quaternion.LookRotation(course.Forward));
            }
            if (binding != null) { binding.Mover.ClearPendingInput(); binding.Balance?.SetAirborne(true); }
        }
        void RestoreBinding(bool placeOnRope)
        {
            var held = binding; binding = null;
            if (held == null || held.Mover == null) return;
            var mover = held.Mover;
            mover.Teleported -= OnTeleported;
            if (placeOnRope) mover.transform.SetPositionAndRotation(held.Position, held.Rotation);
            if (mover.Body != null)
            {
                mover.Body.enabled = held.BodyEnabled;
                foreach (var other in PlayerMover.All)
                    if (other != mover && other.Body != null) Physics.IgnoreCollision(mover.Body, other.Body, true);
            }
            mover.ClearPendingInput(); mover.Simulated = held.Simulated;
            if (held.Balance != null)
            {
                held.Balance.SetLandingReduction(1f);
                mover.ResetExternalMotion(false);
                held.Balance.SetLandingReduction(0f);
            }
            else mover.ResetExternalMotion(false);
        }
        void OnTeleported()
        {
            AbortLocalBinding();
        }
        int SnapshotHeldPole(NetworkPlayer player)
        {
            if (player == null) return -1;
            for (int i = 0; i < PoleCount; i++) if (HolderAt(i) == player.OwnerClientId) return i;
            return -1;
        }
        void AbortLocalBinding()
        {
            localAborted = true;
            localAbortWaiting = session != null && !session.IsHost;
            nextAbortRetry = 0d;
            RestoreBinding(false);
            RetryLocalAbort();
        }
        void RetryLocalAbort()
        {
            if (!localAborted || Clock < nextAbortRetry) return;
            int held = SnapshotHeldPole(NetworkPlayer.Local);
            if (held < 0) return;
            nextAbortRetry = Clock + .25d;
            Send(Operation.Release, held);
        }
        void ConfirmLocalAbort()
        {
            if (!localAborted) return;
            if (SnapshotHeldPole(NetworkPlayer.Local) < 0) localAbortWaiting = false;
            else RetryLocalAbort();
        }
    }
}
