using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Local presentation only: a slow warning glow reveals an approaching pursuer.</summary>
    public sealed class EscapePursuitBeacon : MonoBehaviour
    {
        [SerializeField] EscapeUnderstageChase pursuer;
        [SerializeField] EscapePursuerWalls walls;
        [SerializeField] Light lamp;
        [SerializeField] Light emergenceLight;
        [SerializeField] int gate;
        static readonly Color Rest = new(1f, .62f, .32f);
        static readonly Color Warning = new(1f, .16f, .07f);

        void LateUpdate()
        {
            if (!lamp) return;
            var timer = EscapeRouteTimer.Current;
            bool active = pursuer && pursuer.isActiveAndEnabled && timer && timer.HasStarted;
            float proximity = active ? Mathf.InverseLerp(23, 6, Vector3.Distance(pursuer.transform.position, transform.position)) : 0;
            var panel = walls ? walls.At(gate) : null;
            float impact = active && panel != null && panel.started >= 0
                ? 1f - Mathf.Clamp01((float)(timer.Clock - panel.started)) : 0;
            float pulse = active ? .8f + .2f * Mathf.Sin((float)timer.Clock * 2.4f) : 0;
            lamp.color = Color.Lerp(Rest, Warning, proximity);
            lamp.intensity = .55f + proximity * pulse * 1.9f + impact * 1.5f;
            if (emergenceLight)
            {
                // The fixture lights the breach and public deck from outside. It never
                // follows the actor into the service corridor.
                float glow = active && panel != null && panel.started >= 0
                    ? Mathf.InverseLerp(32f, 8f, Vector3.Distance(pursuer.transform.position, emergenceLight.transform.position)) : 0;
                emergenceLight.enabled = glow > 0;
                emergenceLight.intensity = glow * 78f;
            }
        }
    }
}
