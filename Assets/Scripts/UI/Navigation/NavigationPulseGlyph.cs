using UnityEngine;

namespace CurtainCall.UI.Wayfinding
{
    /// <summary>GPU-only preview changes: does not dirty sprite colors or transform values in the prefab.</summary>
    public sealed class NavigationPulseGlyph : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] layers;
        MaterialPropertyBlock block;
        static readonly int OpacityId = Shader.PropertyToID("_PulseOpacity");
        static readonly int ScaleId = Shader.PropertyToID("_PulseScale");
        static readonly int TintId = Shader.PropertyToID("_SignalColor");
        static readonly int UseTintId = Shader.PropertyToID("_UseSignalTint");
        Color signalColor = new(.12f, 1, .86f, 1);
        float emphasis = 1, rawOpacity;
        public float Opacity { get; private set; }
        public float VisualScale { get; private set; } = 1;

        public void BindLayers() => layers = GetComponentsInChildren<SpriteRenderer>(true);

        public void SetAppearance(Color color, float strength)
        {
            signalColor = color; emphasis = Mathf.Clamp01(strength);
            Apply(rawOpacity, VisualScale);
        }

        public void Apply(float opacity, float scale = 1)
        {
            if (layers == null || layers.Length == 0) BindLayers();
            block ??= new MaterialPropertyBlock();
            rawOpacity = Mathf.Clamp01(opacity);
            Opacity = rawOpacity * emphasis * signalColor.a; VisualScale = Mathf.Max(.01f, scale);
            foreach (var layer in layers)
            {
                if (layer == null) continue;
                layer.GetPropertyBlock(block);
                block.SetFloat(OpacityId, Opacity); block.SetFloat(ScaleId, VisualScale);
                block.SetColor(TintId, signalColor);
                block.SetFloat(UseTintId, layer.color.r + layer.color.g + layer.color.b > .1f ? 1 : 0);
                layer.SetPropertyBlock(block);
            }
        }
    }
}
