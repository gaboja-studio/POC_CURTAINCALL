using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    public sealed partial class FireView
    {
        [Tooltip("구간 예고에 사용할 밧줄 표면 소실 머티리얼")]
        [SerializeField] Material segmentRopeBurn;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly Color IgnitionGlow = new(1.3f, .18f, .025f);
        MaterialPropertyBlock burnProperties;
        readonly Dictionary<Material, Material> burnMaterials = new();
        readonly List<(MeshRenderer renderer, Material[] originals, MaterialPropertyBlock originalBlock, float offset, float from, float to)> burningSurfaces = new();
        readonly List<MeshRenderer> ropeRenderers = new();
        readonly List<Vector2> burningSpans = new();
        readonly List<Transform> hiddenPieces = new();
        Transform burningRoot, slices;
        int burningSegment = -1, burningLane = -1;

        /// <summary>
        /// 이 줄·거리의 밧줄 조각이 화면에서 이미 다 타 없어졌는지. 지지면은 발화 순간 사라지지만 조각은 끝에서부터
        /// 먼저 사라지므로, 매달린 소유자는 이 값으로 손 위 조각이 사라지는 순간 놓는다. 덮는 조각이 없으면 false.
        /// </summary>
        public bool IsRopeBurnedAway(int lane, float distance)
        {
            if (!isActiveAndEnabled || fire == null || fire.Settings == null || burningLane != lane
                || burningSegment < 0 || burningSegment != fire.PendingSegment || burningSurfaces.Count == 0) return false;
            float remaining = fire.WarningRemaining(burningSegment);
            if (remaining < 0f) return false;
            float dissolve = BurnDissolve(remaining, fire.Settings.warningSeconds);
            bool covered = false;
            foreach (var surface in burningSurfaces)
            {
                if (surface.renderer == null || !surface.renderer.enabled
                    || distance < surface.from - CoverMargin || distance > surface.to + CoverMargin) continue;
                covered = true;
                if (RopeDissolve.AtOffset(dissolve, surface.offset) < 1f) return false;
            }
            return covered;
        }

        const float CoverMargin = .05f;

        static float BurnDissolve(float remaining, float warningSeconds)
        {
            float window = Mathf.Min(RopeDissolve.WindowSeconds, warningSeconds);
            return window > 0f ? Mathf.Clamp01(1f - remaining / window) : 1f;
        }

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
                burningLane = lane;
                burningSpans.AddRange(segmentSpans);
                slices = new GameObject("Burning rope visuals").transform;
                slices.SetParent(root, false);
                course.CollectBurnSurfaces(lane, burningSpans, slices, ropeRenderers, hiddenPieces);
                foreach (var renderer in ropeRenderers)
                {
                    if (!renderer.enabled) continue;
                    var bounds = renderer.bounds;
                    var forward = course.Forward;
                    float center = course.GetDistance(bounds.center);
                    float extent = Vector3.Dot(bounds.extents,
                        new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
                    float offset = 0;
                    foreach (var span in burningSpans)
                    {
                        if (center < span.x - .01f || center > span.y + .01f) continue;
                        // Keep the tail of each span legible until close to its support cutoff.
                        offset = Mathf.InverseLerp(span.x, span.y, center + extent);
                        break;
                    }
                    var originalBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(originalBlock);
                    var originals = renderer.sharedMaterials;
                    var replacements = (Material[])originals.Clone();
                    for (int i = 0; i < originals.Length; i++)
                        if (originals[i] != null) replacements[i] = BurnMaterial(originals[i]);
                    renderer.sharedMaterials = replacements;
                    burningSurfaces.Add((renderer, originals, originalBlock, offset, center - extent, center + extent));
                }
                ropeRenderers.Clear();
            }
            ApplyRopeBurnProgress(progress, fire.WarningRemaining(index), fire.Settings.warningSeconds);
        }

        void ApplyRopeBurnProgress(float progress, float remaining, float warningSeconds)
        {
            float dissolve = BurnDissolve(remaining, warningSeconds);
            float pulse = .75f + .25f * Mathf.Sin(Time.time * 13f);
            float warning = Mathf.Lerp(.38f, 1f, progress) * (1f - dissolve) * pulse;
            foreach (var surface in burningSurfaces)
            {
                if (surface.renderer == null) continue;
                var properties = burnProperties ??= new MaterialPropertyBlock();
                surface.renderer.GetPropertyBlock(properties);
                RopeDissolve.Apply(properties, RopeDissolve.AtOffset(dissolve, surface.offset));
                properties.SetColor(EmissionColor, IgnitionGlow * warning);
                surface.renderer.SetPropertyBlock(properties);
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
            // The shared dissolve preset uses a demo emission map. The warning lights the rope itself.
            burn.SetTexture("_Emission_Tex", Texture2D.whiteTexture);
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
            burningRoot = null;
            burningSegment = burningLane = -1;
        }
    }
}
