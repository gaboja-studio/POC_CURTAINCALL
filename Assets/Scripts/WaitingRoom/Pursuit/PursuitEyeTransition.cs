using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public enum PursuitEyeState { Normal, Alert, Chase }

    /// <summary>Local presentation only. Never changes the authoritative AI or sends a message.</summary>
    public sealed class PursuitEyeTransition
    {
        public float Alpha { get; private set; }
        public float Openness { get; private set; }
        public Color Color { get; private set; } = Color.white;
        public float Emission { get; private set; }
        public PursuitEyeState State { get; private set; }
        Color startColor;
        float startEmission, blend;
        bool initialized;

        public static PursuitEyeState Resolve(PursuitBehaviour behaviour, bool targetsObserver, bool targetsOther)
        {
            // A replicated actor chasing a teammate is not a detection of this observer.
            if (targetsOther) return PursuitEyeState.Normal;
            if (targetsObserver && (behaviour == PursuitBehaviour.Prepare || behaviour == PursuitBehaviour.Chase || behaviour == PursuitBehaviour.Capture)) return PursuitEyeState.Chase;
            if (behaviour == PursuitBehaviour.Listen || behaviour == PursuitBehaviour.Investigate || behaviour == PursuitBehaviour.Search) return PursuitEyeState.Alert;
            return PursuitEyeState.Normal;
        }

        public static float AwarenessRadius(PursuitStage stage) => Mathf.Max(0, Mathf.Max(stage.SightRadius, Mathf.Max(stage.WalkHearingRadius, stage.RunHearingRadius)));
        public static bool InRange(Vector3 actor, Vector3 observer, PursuitStage stage)
        {
            float radius = AwarenessRadius(stage);
            return radius > 0 && (actor - observer).sqrMagnitude <= radius * radius;
        }

        public void Reset() { initialized = false; Alpha = Openness = Emission = 0; State = PursuitEyeState.Normal; }

        public void Tick(bool visible, PursuitEyeState state, PursuitEyeSettings settings, float deltaTime)
        {
            deltaTime = Mathf.Max(0, deltaTime);
            if (!initialized) {
                Color = settings.NormalColor; startColor = Color; startEmission = Emission = settings.NormalEmission;
                State = PursuitEyeState.Normal; blend = 1; initialized = true;
            }
            // Match the floor cue: prepare/chase/capture are red on this frame,
            // including transitions from yellow or a partially hidden state.
            if (visible && state == PursuitEyeState.Chase) {
                State = state; Alpha = Openness = blend = 1;
                startColor = Color = settings.ColorFor(state);
                startEmission = Emission = settings.EmissionFor(state);
                return;
            }
            if (state != State) {
                State = state; startColor = Color; startEmission = Emission; blend = 0;
            }
            Alpha = Mathf.MoveTowards(Alpha, visible ? 1 : 0, settings.FadeSeconds <= 0 ? 1 : deltaTime / settings.FadeSeconds);
            Openness = Mathf.MoveTowards(Openness, state == PursuitEyeState.Normal ? 0 : 1,
                settings.TransitionSeconds <= 0 ? 1 : deltaTime / settings.TransitionSeconds);
            blend = Mathf.Min(1, blend + (settings.TransitionSeconds <= 0 ? 1 : deltaTime / settings.TransitionSeconds));
            Color = UnityEngine.Color.Lerp(startColor, settings.ColorFor(state), blend);
            Emission = Mathf.Lerp(startEmission, settings.EmissionFor(state), blend);
        }
    }
}
