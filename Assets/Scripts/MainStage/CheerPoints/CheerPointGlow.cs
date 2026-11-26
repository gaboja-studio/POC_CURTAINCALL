using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>
    /// 불 고리를 주변 배경보다 밝게 보이게 한다(2026-11-26 M3 피드백: 배경과 겹쳐 눈에 안 띔).
    /// 외부 에셋 프리팹은 바꾸지 않고 실행 중에만 덧입힌다: 고리 HDR 발광, 고리 모양 빛 번짐 원판, 주변 조명 강화.
    /// 수치는 GameSettings › 공연 장비 › '환호 고리 — 주변보다 밝게'.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheerPointGlow : MonoBehaviour
    {
        Renderer ring;
        Material ringMaterial;
        Light accent;
        Transform halo;
        Material haloMaterial;
        static Texture2D haloTexture;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        void Start() => Build();

        void Build()
        {
            var settings = GameSettings.PerformanceEquipment;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name.IndexOf("Hoop", System.StringComparison.OrdinalIgnoreCase) >= 0) { ring = renderer; break; }
            accent = GetComponentInChildren<Light>(true);
            if (ring != null)
            {
                ringMaterial = ring.material; // 이 고리 전용 사본. 공유 재질은 바꾸지 않는다.
                ringMaterial.EnableKeyword("_EMISSION");
                ringMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                BuildHalo(settings);
            }
            Apply(1f);
        }

        void BuildHalo(PerformanceEquipmentSettings settings)
        {
            // 불꽃 파티클과 같은 셰이더(가산/투명)를 쓰면 빌드에서도 같은 셰이더 변형이 보장된다.
            // 가산 합성 불티 재질(GoldSpark)을 우선 쓰고, 없으면 첫 파티클 재질을 쓴다.
            Material source = null;
            foreach (var particles in GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var material = particles.sharedMaterial;
                if (material == null) continue;
                if (source == null) source = material;
                if (material.name.IndexOf("Spark", System.StringComparison.OrdinalIgnoreCase) >= 0) { source = material; break; }
            }
            if (source == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HoopGlowHalo";
            Destroy(quad.GetComponent<Collider>());
            halo = quad.transform;
            halo.SetParent(transform, false);
            var bounds = ring.bounds;
            float diameter = Mathf.Max(bounds.size.x, bounds.size.y);
            halo.position = bounds.center;
            halo.rotation = transform.rotation;
            halo.localScale = Vector3.one * diameter * Mathf.Max(1f, settings.cheerHaloScale) / Mathf.Max(.0001f, transform.lossyScale.x);
            haloMaterial = new Material(source) { name = "HoopGlowHalo (runtime)" };
            haloMaterial.SetTexture(BaseMap, HaloTexture);
            haloMaterial.mainTextureScale = Vector2.one;
            haloMaterial.mainTextureOffset = Vector2.zero;
            if (haloMaterial.HasProperty("_Cull")) haloMaterial.SetFloat("_Cull", 0f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = haloMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>가운데가 비고 고리 둘레가 가장 밝은 부드러운 빛 번짐 텍스처(한 번 만들어 공유).</summary>
        static Texture2D HaloTexture
        {
            get
            {
                if (haloTexture != null) return haloTexture;
                const int size = 128;
                haloTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HoopGlowHalo", wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    // 고리 위치(지름 배율의 역수 근처)에 밝은 띠 + 바깥으로 넓게 퍼지는 은은한 빛.
                    float band = Mathf.Exp(-Mathf.Pow((r - .6f) / .13f, 2f));
                    float wide = Mathf.Exp(-Mathf.Pow((r - .6f) / .34f, 2f)) * .45f;
                    float alpha = Mathf.Clamp01((band + wide) * Mathf.Clamp01((1f - r) / .12f));
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
                haloTexture.SetPixels32(pixels);
                haloTexture.Apply(false, true);
                return haloTexture;
            }
        }

        void LateUpdate()
        {
            var settings = GameSettings.PerformanceEquipment;
            float pulse = 1f + settings.cheerGlowPulse * Mathf.Sin(Time.time * settings.cheerGlowPulseSpeed * Mathf.PI);
            Apply(pulse * NearFade(settings));
        }

        /// <summary>카메라가 고리에 가까워지면 빛을 줄인다(통과 직전 화면을 가득 채워 눈부신 문제).</summary>
        float NearFade(PerformanceEquipmentSettings settings)
        {
            var view = Camera.main;
            if (view == null || settings.cheerGlowNearFade <= 0f) return 1f;
            Vector3 center = ring != null ? ring.bounds.center : transform.position;
            float distance = Vector3.Distance(view.transform.position, center);
            return Mathf.Lerp(.25f, 1f, Mathf.Clamp01((distance - 2f) / settings.cheerGlowNearFade));
        }

        void Apply(float pulse)
        {
            var settings = GameSettings.PerformanceEquipment;
            bool on = settings.cheerGlow;
            Color color = settings.cheerGlowColor;
            if (ringMaterial != null) ringMaterial.SetColor(EmissionColor, on ? color * settings.cheerGlowEmission * pulse : Color.black);
            if (halo != null)
            {
                halo.gameObject.SetActive(on);
                if (haloMaterial != null)
                {
                    var tint = color * settings.cheerHaloStrength * pulse;
                    tint.a = 1f;
                    haloMaterial.SetColor(BaseColor, tint);
                }
            }
            if (accent != null && on)
            {
                accent.intensity = settings.cheerGlowLight * pulse;
                accent.range = settings.cheerGlowLightRange;
                accent.color = color;
            }
        }

        void OnDestroy()
        {
            if (ringMaterial != null) Destroy(ringMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }
    }
}
