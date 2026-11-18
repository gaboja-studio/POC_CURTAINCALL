using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    public sealed partial class FireView
    {
        [Tooltip("구간 예고에 사용할 밧줄 표면 소실 머티리얼")]
        [SerializeField] Material segmentRopeBurn;

        static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
        readonly Dictionary<Material, Material> burnMaterials = new();
        readonly List<(MeshRenderer renderer, Material[] originals)> burningSurfaces = new();
        readonly List<MeshRenderer> ropeRenderers = new();
        readonly List<Vector2> burningSpans = new();
        readonly List<Transform> hiddenPieces = new();
        Transform burningRoot, slices;
        int burningSegment = -1;

        void UpdateRopeBurn()
        {
            int index = fire.PendingSegment;
            float progress = SegmentIntensity(index);
            if (progress < 0f) { ClearRopeBurn(); return; }

            int lane = fire.Settings.segments[index].lane - 1;
            var root = course.HangTargetRoot(lane);
            if (root == null) { ClearRopeBurn(); return; }
            fire.GetSegmentSpans(index, segmentSpans);
            if (burningRoot != root || burningSegment != index || !SameBurnSpans())
            {
                ClearRopeBurn();
                burningRoot = root;
                burningSegment = index;
                burningSpans.AddRange(segmentSpans);
                slices = new GameObject("Burning rope visuals").transform;
                slices.SetParent(root, false);
                course.CollectBurnSurfaces(lane, burningSpans, slices, ropeRenderers, hiddenPieces);
                foreach (var renderer in ropeRenderers)
                {
                    if (!renderer.enabled) continue;
                    var originals = renderer.sharedMaterials;
                    var replacements = (Material[])originals.Clone();
                    for (int i = 0; i < originals.Length; i++)
                        if (originals[i] != null) replacements[i] = BurnMaterial(originals[i]);
                    renderer.sharedMaterials = replacements;
                    burningSurfaces.Add((renderer, originals));
                }
                ropeRenderers.Clear();
            }
            // Keep the last glowing strands visible until the course removes the support.
            float amount = Mathf.Lerp(.16f, .65f, progress * progress);
            foreach (var material in burnMaterials.Values)
                material.SetFloat(DissolveAmount, amount);
        }

        bool SameBurnSpans()
        {
            if (segmentSpans.Count != burningSpans.Count) return false;
            for (int i = 0; i < segmentSpans.Count; i++)
                if (segmentSpans[i] != burningSpans[i]) return false;
            return true;
        }

        Material BurnMaterial(Material rope)
        {
            if (burnMaterials.TryGetValue(rope, out var burn)) return burn;
            burn = new Material(segmentRopeBurn) { hideFlags = HideFlags.DontSave };
            burn.SetTexture("_Base_Tex", rope.mainTexture != null ? rope.mainTexture : Texture2D.whiteTexture);
            burn.SetColor("_BaseColor", rope.color);
            var tiling = rope.mainTextureScale;
            burn.SetVector("_Tiling", new Vector4(tiling.x, tiling.y, 0f, 0f));
            // The package demo supplies its own glowing texture and surface maps.
            burn.SetColor("_EmissionColor", Color.black);
            burn.SetFloat("_NormalStrength", 0f);
            burn.SetFloat("_AO_Strength", 0f);
            burn.SetTexture("_MetallicSmooth", Texture2D.blackTexture);
            burnMaterials.Add(rope, burn);
            return burn;
        }

        void ClearRopeBurn()
        {
            foreach (var surface in burningSurfaces)
                if (surface.renderer != null) surface.renderer.sharedMaterials = surface.originals;
            burningSurfaces.Clear();
            if (slices != null)
            {
                slices.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(slices.gameObject); else DestroyImmediate(slices.gameObject);
            }
            foreach (var original in hiddenPieces)
                if (original != null) original.gameObject.SetActive(true);
            hiddenPieces.Clear();
            burningSpans.Clear();
            burningRoot = null;
            burningSegment = -1;
        }
    }
}
