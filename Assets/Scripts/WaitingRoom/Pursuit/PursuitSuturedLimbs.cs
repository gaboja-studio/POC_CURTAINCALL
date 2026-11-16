using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.CharacterArt;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    // Keep serialized choices compatible with existing placements.
    public enum PursuitLimb { LeftArm, RightArm, LeftLeg, RightLeg }
    public enum PursuitSeam { ShoulderOrHip, ElbowOrKnee }
    [Serializable] public sealed class PursuitLimbGraft
    {
        public PursuitLimb Target;
        public PursuitSeam Seam;
        public int DonorModel;
        public int DonorOutfit;
        public PursuitLimb Source;
    }

    /// <summary>Surface-only grafts. Never replace geometry, bones, materials or add limb renderers.</summary>
    public sealed class PursuitSuturedLimbs : MonoBehaviour
    {
        sealed class Atlas
        {
            public RenderTexture Color, Surface;
            public int Users;
        }
        static readonly Dictionary<string, Atlas> Cache = new();
        readonly List<(SkinnedMeshRenderer skin, string key)> bindings = new();
        MaterialPropertyBlock properties;
        string signature;
        static readonly int Enabled = Shader.PropertyToID("_GraftEnabled"), ColorMap = Shader.PropertyToID("_GraftMap"), SurfaceMap = Shader.PropertyToID("_GraftSurfaceMap");

        public void Apply(PursuitLimbGraft[] grafts)
        {
            properties ??= new MaterialPropertyBlock();
            var catalog = PlayerAppearanceCatalog.Current;
            var choices = (grafts ?? Array.Empty<PursuitLimbGraft>()).Where(g => g != null && (int)g.Target >= 0 && (int)g.Target < 4
                && (int)g.Source >= 0 && (int)g.Source < 4 && catalog != null && catalog.Valid(g.DonorModel, g.DonorOutfit))
                .GroupBy(g => g.Target).Select(g => g.First()).OrderBy(g => g.Target).ToArray();
            string key = string.Join(";", choices.Select(JsonUtility.ToJson));
            if (signature == key) return;
            Clear(); signature = key;
            if (choices.Length == 0) return;
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (skin.sharedMesh == null || !skin.sharedMaterials.Any(m => m != null && m.HasProperty(Enabled))) continue;
                string cacheKey = skin.sharedMesh.GetInstanceID() + ":" + key;
                if (!Cache.TryGetValue(cacheKey, out var atlas)) {
                    var maps = PursuitGraftProjection.Bake(skin, transform, choices, catalog);
                    atlas = new Atlas { Color = maps.color, Surface = maps.surface }; Cache.Add(cacheKey, atlas);
                }
                atlas.Users++; bindings.Add((skin, cacheKey));
                skin.GetPropertyBlock(properties);
                properties.SetFloat(Enabled, 1); properties.SetTexture(ColorMap, atlas.Color); properties.SetTexture(SurfaceMap, atlas.Surface);
                skin.SetPropertyBlock(properties);
            }
        }

        void Clear()
        {
            foreach (var (skin, key) in bindings) {
                if (skin != null) {
                    skin.GetPropertyBlock(properties); properties.SetFloat(Enabled, 0);
                    properties.SetTexture(ColorMap, Texture2D.blackTexture); properties.SetTexture(SurfaceMap, Texture2D.blackTexture);
                    skin.SetPropertyBlock(properties);
                }
                if (!Cache.TryGetValue(key, out var atlas) || --atlas.Users > 0) continue;
                atlas.Color.Release(); atlas.Surface.Release(); Dispose(atlas.Color); Dispose(atlas.Surface); Cache.Remove(key);
            }
            bindings.Clear(); signature = null;
        }
        internal static void Dispose(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        void OnDestroy() => Clear();
    }
}
