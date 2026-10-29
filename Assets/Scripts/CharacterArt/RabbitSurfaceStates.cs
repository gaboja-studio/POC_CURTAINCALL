using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.CharacterArt
{
    /// <summary>표면만 교체한다. 부상/복구 규칙은 변경하지 않으며 떨어진 조각은 당시 재질을 유지한다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(HumanoidModel))]
    public sealed class RabbitSurfaceStates : MonoBehaviour
    {
        public enum SurfaceState { Clean, Light, Heavy }
        [SerializeField] SurfaceState state;
        [SerializeField] Material cleanCostume;
        [SerializeField] Material cleanTrim;
        [SerializeField] Material light;
        [SerializeField] Material heavy;
        [SerializeField] Material variantClean;
        HumanoidModel model;
        bool needsApply = true;
        public SurfaceState State => state;
        int variant = -1;
        static readonly Color[] Palettes = { new(.65f,.13f,.16f), new(.12f,.35f,.65f), new(.90f,.60f,.17f), new(.23f,.48f,.25f) };

        public void SetVariant(int slot)
        {
            int next = slot >= 0 && slot < 4 ? slot : 0;
            if (variant == next && !needsApply) return;
            variant = next;
            Apply();
        }

        public void SetState(SurfaceState value)
        {
            if (state == value && !needsApply) return;
            state = value;
            Apply();
        }

        void OnEnable() => needsApply = true;
        void OnValidate() => needsApply = true;
        void Update() { if (needsApply) Apply(); }

        void Apply()
        {
            if (model == null) model = GetComponent<HumanoidModel>();
            if (model == null || model.Original == null) return;
            Material surface = state == SurfaceState.Light ? light : state == SurfaceState.Heavy ? heavy : (variant >= 0 && variantClean != null ? variantClean : cleanCostume);
            Material trim = state == SurfaceState.Clean && variant < 0 ? cleanTrim : surface;
            if (surface == null || trim == null) return;
            ApplyTo(model.Original, surface, trim);
            foreach (var skin in model.Bodies) ApplyTo(skin, surface, trim);
            foreach (var skin in model.Limbs) ApplyTo(skin, surface, trim);
            if (variant >= 0)
            {
                SetPalette(model.Original);
                foreach (var skin in model.Bodies) SetPalette(skin);
                foreach (var skin in model.Limbs) SetPalette(skin);
            }
            needsApply = false;
        }

        void SetPalette(SkinnedMeshRenderer skin)
        {
            if (skin == null) return;
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < 2; i++)
            {
                skin.GetPropertyBlock(block, i);
                var catalog = PlayerAppearanceCatalog.Current;
                var color = catalog != null && catalog.outfitColors != null && variant < catalog.outfitColors.Length ? catalog.outfitColors[variant] : Palettes[variant];
                block.SetColor("_VariantColor", color);
                block.SetFloat("_VariantPattern", variant);
                skin.SetPropertyBlock(block, i);
                block.Clear();
            }
        }

        static void ApplyTo(SkinnedMeshRenderer skin, Material surface, Material trim)
        {
            if (skin == null) return;
            var materials = skin.sharedMaterials;
            if (materials.Length < 2 || (materials[0] == surface && materials[1] == trim)) return;
            materials[0] = surface;
            materials[1] = trim;
            skin.sharedMaterials = materials; // Later cap slots and property blocks are preserved.
        }
    }
}
