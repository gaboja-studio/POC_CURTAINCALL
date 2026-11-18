using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    public sealed partial class FireView
    {
        [Tooltip("구간 예고에 사용할 밧줄 표면 소실 머티리얼")]
        [SerializeField] Material segmentRopeBurn;

        static readonly int BurnAmount = Shader.PropertyToID("_burnAmount");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int Direction = Shader.PropertyToID("_Direction");
        static readonly int ProjectionMin = Shader.PropertyToID("_ProjectionMin");
        static readonly int ProjectionMax = Shader.PropertyToID("_ProjectionMax");
        static readonly Color IgnitionGlow = new(2.4f, .33f, .025f);
        readonly MaterialPropertyBlock burnProperties = new();
        readonly Dictionary<Material, Material> burnMaterials = new();
        readonly List<(MeshRenderer renderer, Material[] originals, MaterialPropertyBlock originalBlock,
            Vector3 direction, float min, float max, float offset)> burningSurfaces = new();
        readonly List<Vector2> burnSections = new();
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
                foreach (var span in burningSpans)
                {
                    if (span.y - span.x < 3f) { burnSections.Add(span); continue; }
                    float first = Mathf.Lerp(span.x, span.y, .29f);
                    float second = Mathf.Lerp(span.x, span.y, .66f);
                    burnSections.Add(new Vector2(span.x, first));
                    burnSections.Add(new Vector2(first, second));
                    burnSections.Add(new Vector2(second, span.y));
                }
                course.CollectBurnSurfaces(lane, burnSections, slices, ropeRenderers, hiddenPieces);
                foreach (var renderer in ropeRenderers)
                {
                    if (!renderer.enabled || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
                    var direction = renderer.transform.InverseTransformVector(course.Forward).normalized;
                    var bounds = filter.sharedMesh.bounds;
                    float center = Vector3.Dot(bounds.center, direction);
                    float extent = Vector3.Dot(bounds.extents,
                        new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z)));
                    float distance = course.GetDistance(renderer.bounds.center);
                    float offset = 0;
                    foreach (var span in burningSpans)
                    {
                        if (distance < span.x - .01f || distance > span.y + .01f) continue;
                        offset = Mathf.InverseLerp(span.x, span.y, distance);
                        break;
                    }
                    var originalBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(originalBlock);
                    var originals = renderer.sharedMaterials;
                    var replacements = (Material[])originals.Clone();
                    for (int i = 0; i < originals.Length; i++)
                        if (originals[i] != null) replacements[i] = BurnMaterial(originals[i]);
                    renderer.sharedMaterials = replacements;
                    burningSurfaces.Add((renderer, originals, originalBlock, direction,
                        center - extent, center + Mathf.Max(.001f, extent), offset));
                }
                ropeRenderers.Clear();
            }
            ApplyRopeBurnProgress(progress);
        }

        void ApplyRopeBurnProgress(float progress)
        {
            float flicker = .88f + .12f * Mathf.Sin(Time.time * 17f);
            foreach (var surface in burningSurfaces)
            {
                if (surface.renderer == null) continue;
                float ignite = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 4f - surface.offset));
                // Keep most of the silhouette during the warning and sweep the last portion just before support is removed.
                float sweep = Mathf.InverseLerp(.58f + surface.offset * .17f, 1f, progress);
                float amount = Mathf.Lerp(-.025f, .79f, sweep * sweep);
                surface.renderer.GetPropertyBlock(burnProperties);
                burnProperties.SetVector(Direction, surface.direction);
                burnProperties.SetFloat(ProjectionMin, surface.min);
                burnProperties.SetFloat(ProjectionMax, surface.max);
                burnProperties.SetFloat(BurnAmount, amount);
                float glow = Mathf.Lerp(.20f, 1f, ignite) * Mathf.Lerp(1f, .1f, sweep) * flicker;
                burnProperties.SetColor(EmissionColor, IgnitionGlow * glow);
                surface.renderer.SetPropertyBlock(burnProperties);
            }
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
            // Use the rope's atlas and the package's directional burn, with little noise at the glowing edge.
            burn.SetTexture("_Emission_Tex", Texture2D.whiteTexture);
            burn.SetFloat("_NoiseStrength", .045f);
            burn.SetFloat("_BurnEdgeWidth", .095f);
            burn.SetFloat("_CharredWidth", .17f);
            burn.SetColor("_CharredColor", new Color(.09f, .036f, .02f));
            burn.SetColor("_BurnColor", new Color(9f, 1.65f, .14f, 1f));
            burn.SetFloat("_NormalStrength", 0f);
            burn.SetFloat("_AO_Strength", 0f);
            burn.SetTexture("_MetallicSmooth", Texture2D.blackTexture);
            burnMaterials.Add(rope, burn);
            return burn;
        }

        void ClearRopeBurn()
        {
            foreach (var surface in burningSurfaces)
            {
                if (surface.renderer == null) continue;
                surface.renderer.sharedMaterials = surface.originals;
                surface.renderer.SetPropertyBlock(surface.originalBlock.isEmpty ? null : surface.originalBlock);
            }
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
            burnSections.Clear();
            burningRoot = null;
            burningSegment = -1;
        }
    }
}
