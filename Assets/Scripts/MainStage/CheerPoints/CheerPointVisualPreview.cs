using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Visual prototype only: does not award cheer or damage the player.</summary>
    public sealed class CheerPointVisualPreview : MonoBehaviour
    {
        public Transform visual;
        public Light accent;
        public float floatHeight = .07f;
        public float tiltDegrees = 2f;
        public float period = 4.5f;
        public float lightIntensity = 3f;

        void Update() => Sample(Time.time);

        public void Sample(float seconds)
        {
            float phase = seconds * Mathf.PI * 2f / Mathf.Max(.1f, period);
            if (visual != null)
            {
                visual.localPosition = Vector3.up * (Mathf.Sin(phase) * floatHeight);
                visual.localRotation = Quaternion.Euler(0, Mathf.Sin(phase * .7f) * tiltDegrees,
                    Mathf.Sin(phase + .5f) * tiltDegrees * .6f);
            }
            if (accent != null) accent.intensity = lightIntensity * (1f + Mathf.Sin(phase * 1.9f) * .13f);
        }

        void OnDisable()
        {
            if (visual != null) { visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; }
            if (accent != null) accent.intensity = lightIntensity;
        }
    }
}
