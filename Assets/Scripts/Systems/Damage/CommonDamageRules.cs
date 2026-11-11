using System;
using CurtainCall.Settings;

namespace CurtainCall.Damage
{
    public enum HazardDamageKind { CutLimb, OrdinaryFire, FatalFire, Crushing }
    public enum FallOutcome { Safe, Injured, Dead }

    public static class CommonDamageRules
    {
        public static bool IsFatal(HazardDamageKind kind) => kind is HazardDamageKind.FatalFire or HazardDamageKind.Crushing;
        public static bool CanHit(double now, double previous, double cooldown) =>
            double.IsFinite(now) && now - previous >= Math.Max(0d, cooldown);
        public static bool IsBelowWorldDeathY(float worldY) => float.IsFinite(worldY)
            && worldY <= GameSettings.Base.BodyDamage.WorldDeathY;
        public static FallOutcome Fall(float height)
        {
            if (!float.IsFinite(height) || height < 0f) return FallOutcome.Safe;
            var rules = GameSettings.Base.BodyDamage;
            if (height >= rules.FallDeathHeight) return FallOutcome.Dead;
            return height >= rules.FallInjuryHeight ? FallOutcome.Injured : FallOutcome.Safe;
        }
    }
}
