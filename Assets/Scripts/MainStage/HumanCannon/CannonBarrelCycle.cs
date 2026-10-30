using System;

namespace CurtainCall.HumanCannon
{
    public enum CannonBarrelPhase { Active, Inactive, FireDisable, Recover }

    /// <summary>Absolute shared-clock timeline. Snapshots seek, never replay an old shot.</summary>
    public sealed class CannonBarrelCycle
    {
        public double FiredAt { get; private set; } = -1;
        double unlockedAt = -1;
        public double UnlockedAt => unlockedAt;
        bool available = true;

        public void SetAvailable(bool value, double now)
        {
            if (available == value) return;
            available = value;
            if (value) unlockedAt = now;
        }

        public void Fire(double now) => FiredAt = now;
        public void Restore(double firedAt, double activationAt = -1) { FiredAt = firedAt; unlockedAt = activationAt; }
        public void Reset() { FiredAt = -1; unlockedAt = -1; }

        public CannonBarrelPhase Sample(double now, float lowerSeconds, float restSeconds,
            float recoverySeconds, out float progress)
        {
            progress = 0;
            if (FiredAt >= 0 && now < FiredAt + lowerSeconds)
            {
                progress = Clamp((now - FiredAt) / lowerSeconds);
                return CannonBarrelPhase.FireDisable;
            }
            if (!available) return CannonBarrelPhase.Inactive;
            // A new external ON command skips the remaining rest, while finishing any firing recoil.
            double recoveryAt = FiredAt < 0 ? unlockedAt : unlockedAt > FiredAt
                ? Math.Max(FiredAt + lowerSeconds, unlockedAt)
                : FiredAt + lowerSeconds + restSeconds;
            if (recoveryAt < 0) return CannonBarrelPhase.Active;
            if (now < recoveryAt) return CannonBarrelPhase.Inactive;
            if (now < recoveryAt + recoverySeconds)
            {
                progress = Clamp((now - recoveryAt) / recoverySeconds);
                return CannonBarrelPhase.Recover;
            }
            return CannonBarrelPhase.Active;
        }

        static float Clamp(double value) => (float)Math.Max(0, Math.Min(1, value));
    }
}
