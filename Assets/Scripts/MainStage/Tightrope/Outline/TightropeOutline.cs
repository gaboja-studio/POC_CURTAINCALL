using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope
{
    /// <summary>정적 메시의 테두리만 표시한다. 원본 재질·메시·충돌체와 게임 판정은 바꾸지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class TightropeOutline : MonoBehaviour
    {
        const string GeneratedName = "__TightropeOutline";
        const string ShaderResource = "TightropeOutline";
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int WidthId = Shader.PropertyToID("_OutlinePixels");
        static readonly Dictionary<Mesh, CachedMesh> meshes = new();
        static Material sharedMaterial;
        static int materialUsers;
        readonly List<OutlinePart> parts = new();
        readonly List<Material> sourceMaterials = new();
        [SerializeField, Range(0f, 6f)] float width = 2f;
        MaterialPropertyBlock properties;
        bool ownsMaterial;

        sealed class CachedMesh { public Mesh Mesh; public int Users; }
        sealed class OutlinePart
        {
            public MeshRenderer Source, Outline;
            public Mesh OriginalMesh;
        }

        /// <summary>도형·프리팹 생성이 끝난 뒤 호출한다. 이미 붙어 있으면 같은 컴포넌트를 갱신한다.</summary>
        public static TightropeOutline AttachToHierarchy(GameObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var outline = target.GetComponent<TightropeOutline>();
            if (outline == null) return target.AddComponent<TightropeOutline>();
            outline.Rebuild();
            return outline;
        }

        /// <summary>원본 렌더러만 색칠하거나 집계할 때 생성된 테두리를 제외한다.</summary>
        public static bool IsOutlineRenderer(Renderer renderer) => renderer != null
            && renderer.gameObject.name == GeneratedName && renderer.GetComponentInParent<TightropeOutline>() != null;

        public void Rebuild()
        {
            Clear();
            if (Application.isPlaying && isActiveAndEnabled) Build();
        }

        /// <summary>재질을 복제하지 않고 이 계층의 테두리만 조절한다. 다른 발판·톱날에는 영향을 주지 않는다.</summary>
        public void SetWidth(float pixels)
        {
            width = Mathf.Clamp(pixels, 0f, 6f);
            foreach (var part in parts) if (part.Outline != null) ApplyWidth(part.Outline);
        }

        void ApplyWidth(Renderer renderer)
        {
            properties ??= new MaterialPropertyBlock();
            properties.SetFloat(WidthId, width);
            renderer.SetPropertyBlock(properties);
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Build();
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            Clear();
        }

        void Build()
        {
            if (parts.Count > 0) return;
            foreach (var source in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (IsOutlineRenderer(source) || source.GetComponentInParent<TightropeOutline>() != this
                    || !source.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
                if (!ownsMaterial)
                {
                    var shader = Resources.Load<Shader>(ShaderResource);
                    if (shader == null) return;
                    if (sharedMaterial == null)
                    {
                        sharedMaterial = new Material(shader)
                        {
                            name = "Tightrope Outline (runtime)", hideFlags = HideFlags.HideAndDontSave
                        };
                    }
                    materialUsers++;
                    ownsMaterial = true;
                }
                var original = filter.sharedMesh;
                var mesh = Acquire(original);
                var item = new GameObject(GeneratedName) { hideFlags = HideFlags.HideAndDontSave, layer = source.gameObject.layer };
                item.transform.SetParent(source.transform, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = item.AddComponent<MeshRenderer>();
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = sharedMaterial;
                renderer.sharedMaterials = materials;
                ApplyWidth(renderer);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.enabled = source.enabled && HasOpaqueDepth(source);
                renderer.forceRenderingOff = source.forceRenderingOff;
                parts.Add(new OutlinePart { Source = source, Outline = renderer, OriginalMesh = original });
            }
        }

        void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // 공용 가림 처리가 LateUpdate 뒤에 투명 재질을 끼워도 검은 외곽 셸이 남지 않게 한다.
            foreach (var part in parts)
            {
                if (part.Outline == null) continue;
                part.Outline.enabled = part.Source != null && part.Source.enabled && HasOpaqueDepth(part.Source);
                part.Outline.forceRenderingOff = part.Source == null || part.Source.forceRenderingOff;
            }
        }

        bool HasOpaqueDepth(MeshRenderer source)
        {
            source.GetSharedMaterials(sourceMaterials);
            foreach (var material in sourceMaterials)
            {
                if (material == null) continue;
                if (material.renderQueue >= (int)RenderQueue.Transparent
                    || (material.HasProperty(SurfaceId) && material.GetFloat(SurfaceId) > 0.5f)
                    || (material.HasProperty(ZWriteId) && material.GetFloat(ZWriteId) < 0.5f)) return false;
            }
            return true;
        }

        void Clear()
        {
            foreach (var part in parts)
            {
                if (part.Outline != null)
                {
                    part.Outline.enabled = false;
                    // 부모 비활성화 중에는 계층을 바꾸지 않는다. 지연 파괴 전에도 그리지 않으며
                    // 생성된 셸은 이름/소유자로 구분해 원본 렌더러 수집에서 제외한다.
                    DestroyGenerated(part.Outline.gameObject);
                }
                Release(part.OriginalMesh);
            }
            parts.Clear();
            sourceMaterials.Clear();
            if (!ownsMaterial) return;
            ownsMaterial = false;
            if (--materialUsers == 0)
            {
                DestroyGenerated(sharedMaterial);
                sharedMaterial = null;
            }
        }

        static Mesh Acquire(Mesh source)
        {
            if (!source.isReadable) return source;
            if (!meshes.TryGetValue(source, out var cached))
            {
                var copy = Instantiate(source);
                copy.name = source.name + " (Tightrope Outline)";
                copy.hideFlags = HideFlags.HideAndDontSave;
                var vertices = source.vertices;
                var normals = source.normals;
                if (normals.Length != vertices.Length) { copy.RecalculateNormals(); normals = copy.normals; }
                copy.normals = SmoothNormals(vertices, normals);
                meshes.Add(source, cached = new CachedMesh { Mesh = copy });
            }
            cached.Users++;
            return cached.Mesh;
        }

        static void Release(Mesh source)
        {
            if (ReferenceEquals(source, null) || !meshes.TryGetValue(source, out var cached)) return;
            if (--cached.Users > 0) return;
            meshes.Remove(source);
            DestroyGenerated(cached.Mesh);
        }

        static void DestroyGenerated(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        /// <summary>같은 위치의 노멀을 합쳐 얇은 도형·모서리의 테두리 틈을 줄인다.</summary>
        public static Vector3[] SmoothNormals(Vector3[] vertices, Vector3[] normals)
        {
            if (vertices == null || normals == null || vertices.Length != normals.Length)
                throw new ArgumentException("메시 위치와 노멀 개수가 같아야 합니다.");
            var sums = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                sums.TryGetValue(vertices[i], out var sum);
                sums[vertices[i]] = sum + normals[i];
            }
            var result = new Vector3[normals.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var sum = sums[vertices[i]];
                result[i] = sum.sqrMagnitude > 0.000001f ? sum.normalized : normals[i];
            }
            return result;
        }
    }
}
