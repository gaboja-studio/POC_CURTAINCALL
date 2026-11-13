using CurtainCall.Player;
using CurtainCall.Survival;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        /// <summary>Only the host may begin escape capture; normal damage and place rules still apply.</summary>
        public bool ServerBeginEscapeCapture()
        {
            if (!IsServer || !ServerCanTakeDamage() || SurvivalRules.ServerPlace != SurvivalPlace.EscapeRoute) return false;
            SetDeathOrigin(DeathOrigin.Pursuit);
            state.Value = PlayerState.EscapeCaptured;
            return true;
        }

        public void ServerSeverCapturedLimbs(int count = 4)
        {
            if (IsServer && State == PlayerState.EscapeCaptured) lostParts.Value |= PlayerCondition.PartsForLostCount(UnityEngine.Mathf.Clamp(count, 0, 4));
        }

        public void ServerFinishEscapeThrow()
        {
            if (!IsServer || State != PlayerState.EscapeCaptured) return;
            ServerSeverCapturedLimbs();
            state.Value = PlayerState.Retired;
        }

        void MaintainEscapeCapture()
        {
            // Cannon/pole release can arrive after the capture state on an owner. It must not
            // restore gravity/control while the authoritative capture still owns that body.
            if (!IsSpawned || !IsOwner || State != PlayerState.EscapeCaptured) return;
            mover.Simulated = false;
            mover.SetControlEnabled(false);
            if (balance != null) balance.IsSuspended = true;
        }
    }
}

