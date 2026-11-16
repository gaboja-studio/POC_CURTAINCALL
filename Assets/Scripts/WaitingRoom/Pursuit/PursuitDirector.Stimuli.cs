using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitDirector
    {
        public PursuitNoiseKind LastNoiseKind { get; private set; }
        public Vector3 LastNoisePosition { get; private set; }
        public int NoiseRevision { get; private set; }
        bool CanSenseStimuli => active && Elapsed >= 0 && PlayerRecognitionEnabled && !CutsceneHold && run != null && run.HasAuthority;

        public void EmitNoise(PursuitNoiseKind kind, Vector3 position, Transform source)
        {
            if (!CanSenseStimuli || kind == PursuitNoiseKind.None) return;
            float radius = PursuitStimuli.Radius(kind, GameSettings.WaitingRoom.Pursuit);
            if (radius <= 0) return;
            LastNoiseKind = kind; LastNoisePosition = position; NoiseRevision++;
            foreach (var actor in actors) {
                if (actor == null || !actor.IsActivated) continue;
                float range = radius, distance = Vector3.Distance(actor.transform.position, position);
                if (distance > range) continue;
                if (!ClearStimulusLine(actor.transform.position + Vector3.up, position, source)) range *= CurrentStageRules.OccludedHearingMultiplier;
                if (range > 0 && distance <= range) actor.HearStimulus(position, distance / range, PursuitStimuli.Maximum(kind) ? 2 : 1);
            }
        }

        bool ClearStimulusLine(Vector3 from, Vector3 to, Transform source)
        {
            var delta = to - from;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
                var t = hit.transform;
                if (t.GetComponentInParent<PursuitActor>() != null || t.GetComponentInParent<CurtainCall.Player.PlayerMover>() != null
                    || source != null && (t == source || t.IsChildOf(source))) continue;
                return false;
            }
            return true;
        }

        public void IlluminatePursuers(GameObject source, Vector3 origin, Vector3 direction, float range, float angle)
        {
            var settings = GameSettings.WaitingRoom.Pursuit;
            if (!CanSenseStimuli || !settings.UvDistraction || source == null || !IsAvailable(source) || direction.sqrMagnitude < .01f) return;
            foreach (var actor in actors) {
                if (actor == null || !actor.IsActivated) continue;
                var point = actor.transform.position + Vector3.up * (actor.IsCrouched ? .6f : 1.2f);
                var delta = point - origin;
                if (delta.magnitude > range || Vector3.Angle(direction, delta) > angle * .5f || !ClearStimulusLine(origin, point, source.transform)) continue;
                actor.DistractWithUv(source);
            }
        }
    }
}
