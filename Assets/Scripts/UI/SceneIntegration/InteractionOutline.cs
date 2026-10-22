using System.Collections.Generic;
using System.Linq;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>선택된 사물의 메시 외곽 셸. 원본 메시/재질/충돌체를 수정하지 않는다.</summary>
    public sealed class InteractionOutline : System.IDisposable
    {
        sealed class Part { public Renderer Source, Shell; public Mesh Copy; public LODGroup Lod; }
        readonly List<Part> parts = new();
        readonly List<Material> sourceMaterials = new();
        public Transform Target { get; }
        public InteractionOutline(Transform target, Material material)
        {
            Target = target;
            foreach (var source in target.GetComponentsInChildren<Renderer>(true))
            {
                if (source.name.StartsWith("__")) continue;
                var skin = source as SkinnedMeshRenderer;
                Mesh mesh = skin != null ? skin.sharedMesh
                    : source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                Mesh copy = null;
                if (skin == null && mesh.isReadable && mesh.normals.Length == mesh.vertexCount)
                {
                    copy = Object.Instantiate(mesh); copy.hideFlags = HideFlags.HideAndDontSave;
                    copy.normals = TightropeOutline.SmoothNormals(mesh.vertices, mesh.normals);
                    mesh = copy;
                }
                var go = new GameObject("__InteractionOutline") { hideFlags = HideFlags.HideAndDontSave, layer = source.gameObject.layer };
                go.transform.SetParent(source.transform, false);
                Renderer shell;
                if (skin != null)
                {
                    var animated = go.AddComponent<SkinnedMeshRenderer>();
                    animated.sharedMesh = mesh; animated.bones = skin.bones; animated.rootBone = skin.rootBone;
                    animated.localBounds = skin.localBounds; animated.updateWhenOffscreen = skin.updateWhenOffscreen; animated.quality = skin.quality;
                    shell = animated;
                }
                else
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    shell = go.AddComponent<MeshRenderer>();
                }
                var materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                shell.sharedMaterials = materials;
                shell.shadowCastingMode = ShadowCastingMode.Off; shell.receiveShadows = false;
                shell.lightProbeUsage = LightProbeUsage.Off; shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var lod = source.GetComponentInParent<LODGroup>();
                if (lod != null)
                {
                    var levels = lod.GetLODs();
                    for (int i = 0; i < levels.Length; i++)
                        if (levels[i].renderers.Contains(source)) levels[i].renderers = levels[i].renderers.Append(shell).ToArray();
                    lod.SetLODs(levels);
                }
                parts.Add(new Part { Source = source, Shell = shell, Copy = copy, Lod = lod });
            }
            RenderPipelineManager.beginCameraRendering += BeforeRender;
            Update();
        }
        void BeforeRender(ScriptableRenderContext _, Camera camera) => Update();
        public void Update()
        {
            foreach (var part in parts)
                if (part.Shell != null)
                {
                    if (part.Source is SkinnedMeshRenderer source && part.Shell is SkinnedMeshRenderer animated)
                    {
                        animated.sharedMesh = source.sharedMesh;
                        animated.localBounds = source.localBounds;
                        if (source.sharedMesh != null)
                            for (int i = 0; i < source.sharedMesh.blendShapeCount; i++) animated.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
                    }
                    part.Shell.enabled = part.Source != null && part.Source.enabled && part.Source.gameObject.activeInHierarchy && HasOpaqueDepth(part.Source);
                    part.Shell.forceRenderingOff = part.Source == null || part.Source.forceRenderingOff;
                }
        }
        bool HasOpaqueDepth(Renderer source)
        {
            source.GetSharedMaterials(sourceMaterials);
            foreach (var mat in sourceMaterials)
                if (mat != null && (mat.renderQueue >= (int)RenderQueue.Transparent ||
                    mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > .5f ||
                    mat.HasProperty("_ZWrite") && mat.GetFloat("_ZWrite") < .5f)) return false;
            return true;
        }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeRender;
            foreach (var part in parts)
            {
                if (part.Lod != null)
                {
                    var levels = part.Lod.GetLODs();
                    for (int i = 0; i < levels.Length; i++) levels[i].renderers = levels[i].renderers.Where(r => r != part.Shell).ToArray();
                    part.Lod.SetLODs(levels);
                }
                if (part.Shell != null) { part.Shell.enabled = false; Object.Destroy(part.Shell.gameObject); }
                if (part.Copy != null) Object.Destroy(part.Copy);
            }
            parts.Clear();
        }
    }
}
