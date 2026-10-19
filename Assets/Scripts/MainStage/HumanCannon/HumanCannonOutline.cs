using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.HumanCannon
{
    /// <summary>톱·봉·트램폴린의 실제 메시 외곽선. 원본 재질과 충돌 형상은 바꾸지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class HumanCannonOutline : MonoBehaviour
    {
        const string GeneratedName = "__CannonOutline";
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        [SerializeField] Material outlineMaterial;
        readonly List<OutlinePart> parts = new();
        readonly List<Material> sourceMaterials = new();
        static readonly Dictionary<Mesh, CachedMesh> meshes = new();

        sealed class CachedMesh
        {
            public Mesh Mesh;
            public int Users;
        }

        sealed class OutlinePart
        {
            public MeshRenderer Source;
            public MeshRenderer Outline;
            public Mesh OriginalMesh;
        }

        /// <summary>씬 작성 시 공유 outline 재질 하나를 연결한다. 실제 외곽선은 Play에서만 만든다.</summary>
        public static HumanCannonOutline AttachToHierarchy(GameObject target, Material material)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var component = target.GetComponent<HumanCannonOutline>();
            if (component == null) component = target.AddComponent<HumanCannonOutline>();
            component.Configure(material);
            return component;
        }

        public void Configure(Material material)
        {
            Clear();
            outlineMaterial = material;
            if (Application.isPlaying && isActiveAndEnabled) Build();
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
            if (outlineMaterial == null || parts.Count > 0) return;
            foreach (var source in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (source.gameObject.name == GeneratedName || source.GetComponentInParent<HumanCannonOutline>() != this
                    || !source.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
                var original = filter.sharedMesh;
                var mesh = Acquire(original);
                var item = new GameObject(GeneratedName) { hideFlags = HideFlags.HideAndDontSave, layer = source.gameObject.layer };
                item.transform.SetParent(source.transform, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = item.AddComponent<MeshRenderer>();
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.enabled = source.enabled;
                parts.Add(new OutlinePart { Source = source, Outline = renderer, OriginalMesh = original });
            }
        }

        void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // 공용 가림 처리는 Cinemachine의 LateUpdate 뒤에 재질을 교체할 수 있다.
            // 렌더 직전에 확인해야 투명화가 시작/끝난 프레임에도 검은 뒷면이 남지 않는다.
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
                // 뒤집힌 외곽 메시의 내부는 원본의 depth로 가려진다. 투명 원본에는
                // 이 조건이 없으므로 외곽선을 잠시 숨겨 원래의 가림 투명화를 보존한다.
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
                    Destroy(part.Outline.gameObject);
                }
                Release(part.OriginalMesh);
            }
            parts.Clear();
            sourceMaterials.Clear();
        }

        static Mesh Acquire(Mesh source)
        {
            // 읽기 불가 imported mesh는 원래 노멀을 사용한다. primitive는 공유 사본 하나로 이음새를 없앤다.
            if (!source.isReadable) return source;
            if (!meshes.TryGetValue(source, out var cached))
            {
                var copy = Instantiate(source);
                copy.name = source.name + " (Cannon Outline)";
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.normals = SmoothNormals(source.vertices, source.normals);
                cached = new CachedMesh { Mesh = copy };
                meshes.Add(source, cached);
            }
            cached.Users++;
            return cached.Mesh;
        }

        static void Release(Mesh source)
        {
            if (source == null || !meshes.TryGetValue(source, out var cached)) return;
            if (--cached.Users > 0) return;
            meshes.Remove(source);
            if (cached.Mesh != null) Destroy(cached.Mesh);
        }

        /// <summary>같은 꼭짓점을 공유하는 면의 노멀을 합쳐 primitive 모서리의 외곽선 틈을 막는다.</summary>
        public static Vector3[] SmoothNormals(Vector3[] vertices, Vector3[] normals)
        {
            if (vertices == null || normals == null || vertices.Length != normals.Length)
                throw new ArgumentException("메시의 위치와 노멀 수가 같아야 합니다.");
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
