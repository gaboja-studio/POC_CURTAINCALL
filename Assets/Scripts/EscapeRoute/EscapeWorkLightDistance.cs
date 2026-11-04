using System;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // Each client budgets its own fixed maintenance lamps. Only the explicitly authored lights are owned here.
    [DisallowMultipleComponent]
    public sealed class EscapeWorkLightDistance : MonoBehaviour
    {
        [SerializeField] Light[] fixtures = Array.Empty<Light>();
        const float FullAfterRange = 55f, OffAfterRange = 85f, WakeMargin = 3f;
        float[] initialIntensities;
        Camera lastCamera;
        Vector3 lastPosition;
        float nextSample;
        bool sampled;

        void OnEnable()
        {
            if (initialIntensities == null || initialIntensities.Length != fixtures.Length)
                initialIntensities = new float[fixtures.Length];
            for (int i = 0; i < fixtures.Length; i++)
            {
                Light lamp = fixtures[i];
                initialIntensities[i] = lamp && lamp.enabled ? lamp.intensity : -1f;
            }
            sampled = false;
        }

        void LateUpdate()
        {
            Camera camera = EscapeSceneryView.LocalCamera;
            if (!camera)
            {
                if (sampled) Restore();
                return;
            }

            Vector3 position = camera.transform.position;
            float delta = (position - lastPosition).sqrMagnitude;
            float now = Time.unscaledTime;
            // Switching spectator target or teleporting should light the new room on the first frame.
            if (sampled && lastCamera == camera && delta < 12f * 12f &&
                (delta < .20f * .20f || now < nextSample)) return;

            ApplyDistance(position);
            lastCamera = camera;
            lastPosition = position;
            nextSample = now + .125f;
            sampled = true;
        }

        void ApplyDistance(Vector3 position)
        {
            for (int i = 0; i < fixtures.Length; i++)
            {
                Light lamp = fixtures[i];
                if (!lamp || initialIntensities[i] < 0f) continue;
                float distanceSquared = (lamp.transform.position - position).sqrMagnitude;
                float far = lamp.range + OffAfterRange;
                if (distanceSquared >= far * far)
                {
                    if (lamp.enabled) lamp.enabled = false;
                    continue;
                }
                float wake = far - WakeMargin;
                if (!lamp.enabled && distanceSquared > wake * wake) continue;
                float near = lamp.range + FullAfterRange;
                float fraction = distanceSquared <= near * near ? 1f :
                    1f - Mathf.SmoothStep(0f, 1f, (Mathf.Sqrt(distanceSquared) - near) / (far - near));
                float intensity = initialIntensities[i] * fraction;
                if (!Mathf.Approximately(lamp.intensity, intensity)) lamp.intensity = intensity;
                if (!lamp.enabled) lamp.enabled = true;
            }
        }

        void OnDisable() => Restore();

        void Restore()
        {
            if (initialIntensities != null)
            {
                for (int i = 0; i < fixtures.Length; i++)
                {
                    Light lamp = fixtures[i];
                    if (!lamp || initialIntensities[i] < 0f) continue;
                    lamp.intensity = initialIntensities[i];
                    lamp.enabled = true;
                }
            }
            sampled = false;
            lastCamera = null;
        }
    }
}
