using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>A presentation snapshot, separate from attack authority and networking.</summary>
    public readonly struct PursuitThreatSample
    {
        public readonly GameObject Observer;
        public readonly Object Source;
        public readonly double Round;
        public readonly bool Available, Threatened, IsEscape;
        public readonly float Danger, Escalation;

        public PursuitThreatSample(GameObject observer, Object source, double round, bool available,
            bool threatened, float danger, float escalation = 0, bool isEscape = false)
        {
            Observer = observer; Source = source; Round = round;
            Available = available && observer && source;
            Threatened = Available && threatened;
            IsEscape = isEscape;
            Danger = Mathf.Clamp01(danger); Escalation = Mathf.Clamp01(escalation);
        }

        public static PursuitThreatSample Escape(GameObject observer, Object source, double round,
            bool available, bool targeted, float distance, float threshold)
        {
            available &= double.IsFinite(round) && round >= 0 && float.IsFinite(distance) && distance >= 0;
            bool proximity = float.IsFinite(threshold) && threshold > 0;
            bool threatened = targeted || proximity && distance <= threshold;
            float danger = proximity ? Mathf.Clamp01(1 - distance / threshold) : 0;
            return new PursuitThreatSample(observer, source, round, available, threatened, danger, isEscape: true);
        }
    }
}
