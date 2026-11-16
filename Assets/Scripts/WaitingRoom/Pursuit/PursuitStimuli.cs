using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public enum PursuitNoiseKind { None, SawFire, SawGrab, TrampolineBounce, CannonFire, VentOpen, Television, Door }

    public static class PursuitStimuli
    {
        public static bool Maximum(PursuitNoiseKind kind) => kind is PursuitNoiseKind.SawFire or PursuitNoiseKind.CannonFire or PursuitNoiseKind.Door;
        public static float Radius(PursuitNoiseKind kind, PursuitSettings rules) => kind == PursuitNoiseKind.None ? 0
            : Maximum(kind) ? Mathf.Max(rules.LoudNoiseRadius, rules.MaximumNoiseRadius) : Mathf.Max(0, rules.LoudNoiseRadius);
        // Called at confirmed gameplay events. Only the pursuit authority consumes it;
        // replicated presentation can call this without producing duplicate stimuli.
        public static void Emit(PursuitNoiseKind kind, Vector3 position, Transform source = null)
            => PursuitDirector.Current?.EmitNoise(kind, position, source);
    }
}
