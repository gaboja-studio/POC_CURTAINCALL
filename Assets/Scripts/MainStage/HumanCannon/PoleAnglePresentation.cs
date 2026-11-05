using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>Blend snapshot corrections without changing authoritative swing physics.</summary>
    public struct PoleAnglePresentation
    {
        double receivedAt;
        float correction, duration;
        bool received;

        public float Sample(float predictedAngle, double now) => predictedAngle +
            (received ? correction * (1f - Mathf.Clamp01((float)((now - receivedAt) / duration))) : 0f);

        public void Receive(float displayedAngle, float predictedAngle, double now, bool continuous, float interval)
        {
            correction = received && continuous && now >= receivedAt && now - receivedAt <= .25d
                ? displayedAngle - predictedAngle : 0f;
            duration = Mathf.Max(.001f, interval);
            receivedAt = now;
            received = true;
        }
    }
}
