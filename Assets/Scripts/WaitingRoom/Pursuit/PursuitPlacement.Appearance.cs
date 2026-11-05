using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public enum PursuitAppearanceChoice { Varied, Prefab, Clean, Worn, LightBlood, Bloodied, HeavyBlood }

    public sealed partial class PursuitPlacement
    {
        [Tooltip("개체별 다양화 또는 이 배치에 고정할 외관입니다.")]
        public PursuitAppearanceChoice Appearance;
        [Range(0, 15), Tooltip("같은 혈흔 정도에서 얼룩 위치와 모양을 바꿉니다.")]
        public int AppearancePattern;

        public bool CustomSurfaceLayers;
        [Range(0, 1)] public float DirtAmount, WearAmount, BloodAmount;
        [Range(0, 15)] public int WearPattern;
        PursuitAppearance PrefabAppearance => Prefab != null ? Prefab.GetComponent<PursuitAppearance>() : null;
        bool UsePrefabLayers => !CustomSurfaceLayers && Appearance == PursuitAppearanceChoice.Prefab && PrefabAppearance != null;
        public float ResolvedDirtAmount => CustomSurfaceLayers ? Mathf.Clamp01(DirtAmount) : UsePrefabLayers ? PrefabAppearance.DirtAmount : PursuitAppearance.PresetWear(ResolvedSurface);
        public float ResolvedWearAmount => CustomSurfaceLayers ? Mathf.Clamp01(WearAmount) : UsePrefabLayers ? PrefabAppearance.WearAmount : PursuitAppearance.PresetWear(ResolvedSurface);
        public float ResolvedBloodAmount => CustomSurfaceLayers ? Mathf.Clamp01(BloodAmount) : UsePrefabLayers ? PrefabAppearance.AuthoredBloodAmount : PursuitAppearance.PresetBlood(ResolvedSurface);
        public int ResolvedWearPattern => CustomSurfaceLayers ? Mathf.Clamp(WearPattern, 0, 15) : Mathf.Clamp(AppearancePattern, 0, 15);
        public int ResolvedWearSeed => UsePrefabLayers ? PrefabAppearance.ResolvedWearSeed + Mathf.Clamp(AppearancePattern, 0, 15) * 97
            : ResolvedSurfaceSeed + (ResolvedWearPattern - Mathf.Clamp(AppearancePattern, 0, 15)) * 97;

        public PursuitSurfaceState ResolvedSurface {
            get {
                switch (Appearance) {
                    case PursuitAppearanceChoice.Clean: return PursuitSurfaceState.Clean;
                    case PursuitAppearanceChoice.Worn: return PursuitSurfaceState.Worn;
                    case PursuitAppearanceChoice.LightBlood: return PursuitSurfaceState.LightBlood;
                    case PursuitAppearanceChoice.Bloodied: return PursuitSurfaceState.Bloodied;
                    case PursuitAppearanceChoice.HeavyBlood: return PursuitSurfaceState.HeavyBlood;
                    case PursuitAppearanceChoice.Prefab:
                        var source = Prefab != null ? Prefab.GetComponent<PursuitAppearance>() : null;
                        return source != null ? source.SurfaceState : PursuitSurfaceState.Bloodied;
                    default:
                        // Stable across host/replicas and editor reloads. Never use instance IDs,
                        // string.GetHashCode or UnityEngine.Random for authored appearance.
                        switch (StableAppearanceKey() % 5) {
                            case 0: return PursuitSurfaceState.Clean;
                            case 1: return PursuitSurfaceState.Worn;
                            case 2: return PursuitSurfaceState.LightBlood;
                            case 3: return PursuitSurfaceState.Bloodied;
                            default: return PursuitSurfaceState.HeavyBlood;
                        }
                }
            }
        }
        public int ResolvedSurfaceSeed {
            get {
                var source = Prefab != null ? Prefab.GetComponent<PursuitAppearance>() : null;
                int seed = Appearance == PursuitAppearanceChoice.Prefab && source != null
                    ? source.SurfaceSeed : (int)(StableAppearanceKey() % 4093);
                return seed + Mathf.Clamp(AppearancePattern, 0, 15) * 97;
            }
        }
        uint StableAppearanceKey()
        {
            // Room-relative hierarchy is identical even when a replica lives under another root.
            uint hash = 2166136261;
            unchecked {
                for (var t = transform; t != null; t = t.parent) {
                    foreach (char c in t.name) hash = (hash ^ c) * 16777619;
                    if (t.GetComponent<PursuitRoom>() != null) break;
                    hash = (hash ^ (uint)t.GetSiblingIndex()) * 16777619;
                }
            }
            return hash;
        }
        public void ApplyAppearance(PursuitAppearance target)
        {
            if (target == null) return;
            target.SurfaceState = ResolvedSurface;
            target.SurfaceSeed = ResolvedSurfaceSeed;
            target.UseLayerOverrides = CustomSurfaceLayers || (UsePrefabLayers && PrefabAppearance.UseLayerOverrides);
            target.LayerDirt = ResolvedDirtAmount;
            target.LayerWear = ResolvedWearAmount;
            target.LayerBlood = ResolvedBloodAmount;
            target.WearSeed = ResolvedWearSeed;
            target.RefreshSurface(!Application.isPlaying || CurtainCall.Damage.InjuryBloodEffects.IsVisible);
        }
    }
}
