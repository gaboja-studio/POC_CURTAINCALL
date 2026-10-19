using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>최후반에 배치된 마지막 관문만 같은 주기로 열고 닫는다. 인원수는 주기를 바꾸지 않는다.</summary>
    public static class FinaleGateCycle
    {
        public readonly struct Phase
        {
            public readonly float Lift, UntilOpen, OpenRemaining;
            public readonly bool Closing;
            public Phase(float lift, float untilOpen, float openRemaining, bool closing = false)
            { Lift = lift; UntilOpen = untilOpen; OpenRemaining = openRemaining; Closing = closing; }
        }

        public static bool Applies(int index, float[] distances, float finishDistance, float startFraction) =>
            distances != null && index >= 0 && index == distances.Length - 1 && finishDistance > 0f
            && float.IsFinite(distances[index]) && distances[index] < finishDistance
            && distances[index] >= finishDistance * Mathf.Clamp(startFraction, .5f, 1f);

        public static Phase Sample(float elapsed, float closedSeconds, float openSeconds, float transition)
        {
            if (!float.IsFinite(elapsed) || !float.IsFinite(closedSeconds)
                || !float.IsFinite(openSeconds) || !float.IsFinite(transition)) return default;
            float closed = Mathf.Max(.1f, closedSeconds), hold = Mathf.Max(.1f, openSeconds);
            float move = Mathf.Max(.1f, transition), period = closed + move * 2f + hold;
            if (elapsed < 0f) return new Phase(0f, closed + move - elapsed, 0f);
            float phase = Mathf.Repeat(elapsed, period);
            if (phase < closed) return new Phase(0f, closed + move - phase, 0f);
            if (phase < closed + move)
                return new Phase(Mathf.SmoothStep(0f, 1f, (phase - closed) / move), closed + move - phase, hold);
            if (phase < closed + move + hold)
                return new Phase(1f, 0f, closed + move + hold - phase);
            return new Phase(1f - Mathf.SmoothStep(0f, 1f, (phase - closed - move - hold) / move),
                period - phase + closed + move, 0f, true);
        }
    }
}
