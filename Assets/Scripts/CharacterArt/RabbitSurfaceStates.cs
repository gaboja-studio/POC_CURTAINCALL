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
        HumanoidModel model;
        bool needsApply = true;
        public SurfaceState State => state;

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
            Material surface = state == SurfaceState.Light ? light : state == SurfaceState.Heavy ? heavy : cleanCostume;
            Material trim = state == SurfaceState.Clean ? cleanTrim : surface;
            if (surface == null || trim == null) return;
            ApplyTo(model.Original, surface, trim);
            foreach (var skin in model.Bodies) ApplyTo(skin, surface, trim);
            foreach (var skin in model.Limbs) ApplyTo(skin, surface, trim);
            needsApply = false;
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
