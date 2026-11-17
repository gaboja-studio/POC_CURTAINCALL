using System;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Actual score deltas only. Independent sign lanes never cancel one another.</summary>
    public sealed class NumericReactionState
    {
        public long Value { get; private set; }
        public float Started { get; private set; }
        public float Expires { get; private set; }
        public bool Visible(float now) => Value != 0 && now < Expires;
        public void Add(int delta, float now, float combine, float lifetime)
        {
            if (delta == 0) return;
            if (Visible(now) && Math.Sign(Value) == Math.Sign(delta) && now - Started <= combine)
                Value += delta;
            else { Value = delta; Started = now; }
            Expires = now + lifetime;
        }
        public void Clear() { Value = 0; Started = Expires = 0; }
    }
}
