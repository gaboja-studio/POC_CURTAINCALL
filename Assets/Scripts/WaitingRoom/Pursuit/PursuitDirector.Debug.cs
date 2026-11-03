using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitDirector
    {
        int forcedStage = -1;
        bool debugInvulnerable, debugIgnorePlayers;
        public bool DebugInvulnerable => debugInvulnerable;
        public bool DebugRecognitionEnabled => !debugIgnorePlayers;
        internal bool PlayerRecognitionEnabled => !DevMode.Enabled || !debugIgnorePlayers;
        bool CaptureSuppressed => DevMode.Enabled && debugInvulnerable;
        public bool CanDebug => DevMode.Enabled && isActiveAndEnabled && run != null && run.HasAuthority
            && run.State.Phase == CampaignPhase.RestTime;
        public bool StageIsForced => forcedStage >= 0;
        public int CurrentStageIndex => !active ? -1 : forcedStage >= 0 ? forcedStage
            : run != null && !run.HasAuthority ? stage : GameSettings.WaitingRoom.Pursuit.StageAt(Elapsed);
        public PursuitStage CurrentStageRules {
            get {
                var rules = GameSettings.WaitingRoom.Pursuit;
                int index = CurrentStageIndex;
                return index >= 0 && rules.Stages != null && index < rules.Stages.Length && rules.Stages[index] != null
                    ? rules.Stages[index] : rules.Stage(Elapsed);
            }
        }

        public bool DebugResetTimer()
        {
            if (!CanDebug || !run.DebugResetRestTimer()) return false;
            CancelDebugPursuit();
            return true;
        }
        public bool DebugSetInvulnerable(bool enabled)
        {
            if (!CanDebug) return false;
            debugInvulnerable = enabled;
            Broadcast();
            return true;
        }
        public bool DebugSetRecognition(bool enabled)
        {
            if (!CanDebug) return false;
            debugIgnorePlayers = !enabled;
            footsteps.Clear();
            if (!enabled) foreach (var actor in actors) actor.ForgetPlayers();
            Broadcast();
            return true;
        }
        void ResetDebugProtection()
        {
            debugInvulnerable = debugIgnorePlayers = false;
            footsteps.Clear();
            sendAt = 0;
        }
        public bool DebugSetTimer(float seconds)
        {
            if (!CanDebug || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
            if (seconds <= 0) return DebugExpireTimer();
            if (!run.DebugSetRemaining(seconds)) return false;
            CancelDebugPursuit();
            return true;
        }
        public bool DebugAdjustTimer(float delta) => CanDebug && DebugSetTimer(run.State.TimeRemaining + delta);
        public bool DebugExpireTimer()
        {
            if (!CanDebug || !run.DebugBeginPursuit()) return false;
            if (!active) { active = true; Elapsed = 0; }
            Announce(); Broadcast();
            return true;
        }
        public bool DebugSetStage(int index)
        {
            var stages = GameSettings.WaitingRoom.Pursuit.Stages;
            if (!CanDebug || index < -1 || (index >= 0 && (stages == null || index >= stages.Length || stages[index] == null))) return false;
            if (index >= 0 && !DebugExpireTimer()) return false;
            forcedStage = index;
            if (active) Announce();
            Broadcast();
            return true;
        }
        void CancelDebugPursuit()
        {
            foreach (var victim in victims.Values) {
                if (victim.Object == null) continue;
                var player = victim.Object.GetComponent<NetworkPlayer>();
                if (player != null && player.IsSpawned) player.ServerRestoreDevInjury(player.LostParts);
            }
            ResetStep(run.State);
            Broadcast();
        }
    }
}
