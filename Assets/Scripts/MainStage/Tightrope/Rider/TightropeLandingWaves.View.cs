using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeLandingWaves
    {
        const int WavePoints = 49;
        readonly Vector3[] wavePoints = new Vector3[WavePoints];

        void LateUpdate()
        {
            if (!Active || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            foreach (var pulse in pulses)
            {
                float age = (float)(Clock - pulse.At);
                if (age < 0f) continue;
                if (pulse.View == null)
                {
                    if (material == null)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                        material = new Material(shader) { color = new Color(1f, .65f, .12f), hideFlags = HideFlags.HideAndDontSave };
                    }
                    var root = new GameObject("Landing wave");
                    root.transform.SetParent(transform, false);
                    pulse.View = root.AddComponent<LineRenderer>();
                    pulse.View.sharedMaterial = material;
                    pulse.View.useWorldSpace = true; pulse.View.widthMultiplier = .045f;
                    pulse.View.positionCount = WavePoints;
                    pulse.View.shadowCastingMode = ShadowCastingMode.Off;
                    pulse.View.receiveShadows = false;
                }
                float from = pulse.Distance, to = pulse.Distance;
                foreach (var span in course.GetUsableSpans(pulse.Lane))
                    if (pulse.Distance >= span.x && pulse.Distance < span.y)
                    { from = Mathf.Max(span.x, pulse.Distance - pulse.Range); to = Mathf.Min(span.y, pulse.Distance + pulse.Range); break; }
                float front = age * pulse.Speed;
                for (int i = 0; i < WavePoints; i++)
                {
                    float distance = Mathf.Lerp(from, to, i / (float)(WavePoints - 1));
                    float band = (Mathf.Abs(distance - pulse.Distance) - front) / .45f;
                    float amplitude = .18f * Mathf.Exp(-band * band) * Mathf.Clamp01(1f - age * pulse.Speed / pulse.Range);
                    wavePoints[i] = course.GetLanePosition(pulse.Lane, distance) + Vector3.up * (.13f + amplitude);
                }
                pulse.View.SetPositions(wavePoints);
            }
        }

        static void RemoveView(Pulse pulse)
        { if (pulse.View != null) Destroy(pulse.View.gameObject); }
    }
}
