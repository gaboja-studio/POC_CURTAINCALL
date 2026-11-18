using CurtainCall.Damage;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    [DisallowMultipleComponent]
    public sealed class EscapeBloodVisibility : MonoBehaviour
    {
        [SerializeField] ParticleSystemRenderer[] bloodEffects = System.Array.Empty<ParticleSystemRenderer>();

        bool? lastVisible;

        void OnEnable()
        {
            lastVisible = null;
            ApplyVisibility();
        }

        void LateUpdate() => ApplyVisibility();

        void ApplyVisibility()
        {
            bool visible = InjuryBloodEffects.IsVisible;
            if (lastVisible == visible) return;
            lastVisible = visible;
            foreach (var effect in bloodEffects)
            {
                if (effect)
                    effect.forceRenderingOff = !visible;
            }
        }

        void OnDisable()
        {
            foreach (var effect in bloodEffects)
                if (effect) effect.forceRenderingOff = false;
        }
    }
}
