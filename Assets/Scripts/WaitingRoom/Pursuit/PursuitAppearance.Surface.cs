using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitAppearance
    {
        public bool UseLayerOverrides;
        [Range(0, 1)] public float LayerDirt, LayerWear, LayerBlood;
        public int WearSeed;
        static readonly int BloodAmountId = Shader.PropertyToID("_BloodAmount");
        static readonly int DirtAmountId = Shader.PropertyToID("_DirtAmount");
        static readonly int WearAmountId = Shader.PropertyToID("_WearAmount");
        static readonly int BaseBloodId = Shader.PropertyToID("_BaseBlood");
        static readonly int BloodVisibleId = Shader.PropertyToID("_BloodVisible");
        static readonly int VariantSeedId = Shader.PropertyToID("_VariantSeed");
        static readonly int WearSeedId = Shader.PropertyToID("_WearSeed");
        static readonly int ThreatId = Shader.PropertyToID("_Threat");

        public static float PresetBlood(PursuitSurfaceState state) => state switch {
            PursuitSurfaceState.LightBlood => .25f, PursuitSurfaceState.Bloodied => .6f,
            PursuitSurfaceState.HeavyBlood => 1f, _ => 0f
        };
        public static float PresetWear(PursuitSurfaceState state) => state switch {
            PursuitSurfaceState.Clean => .04f, PursuitSurfaceState.Worn => .85f,
            PursuitSurfaceState.LightBlood => .28f, PursuitSurfaceState.HeavyBlood => .65f, _ => .4f
        };
        public float AuthoredBloodAmount => UseLayerOverrides ? Mathf.Clamp01(LayerBlood) : PresetBlood(SurfaceState);
        public float WearAmount => UseLayerOverrides ? Mathf.Clamp01(LayerWear) : PresetWear(SurfaceState);
        public float DirtAmount => UseLayerOverrides ? Mathf.Clamp01(LayerDirt) : PresetWear(SurfaceState);
        public int ResolvedWearSeed => UseLayerOverrides ? WearSeed : SurfaceSeed;

        /// <summary>Base skin/stitches, independent grime/wear, then blood. Shared by editor and runtime.</summary>
        public void RefreshSurface(bool bloodVisible)
        {
            surfaces ??= GetComponentsInChildren<SkinnedMeshRenderer>(true);
            props ??= new MaterialPropertyBlock();
            float captureBlood = bloodVisible && CaptureCount > 0
                ? Mathf.Clamp01(CaptureCount * GameSettings.WaitingRoom.Pursuit.BloodPerCapture) : 0;
            foreach (var surface in surfaces) if (surface != null) {
                surface.GetPropertyBlock(props);
                props.SetFloat(BloodAmountId, captureBlood);
                props.SetFloat(DirtAmountId, DirtAmount);
                props.SetFloat(WearAmountId, WearAmount);
                props.SetFloat(BaseBloodId, AuthoredBloodAmount);
                props.SetFloat(BloodVisibleId, bloodVisible ? 1 : 0);
                props.SetFloat(VariantSeedId, SurfaceSeed);
                props.SetFloat(WearSeedId, ResolvedWearSeed);
                props.SetFloat(ThreatId, IsAdvanced ? 1 : 0);
                surface.SetPropertyBlock(props);
            }
        }
    }
}
