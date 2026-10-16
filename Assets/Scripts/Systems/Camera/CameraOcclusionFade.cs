using System;
using System.Collections.Generic;
using CurtainCall.Cameras.Requests;
using CurtainCall.Network.PlayerSync;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// 카메라와 내 캐릭터 사이를 가리는 것(다른 플레이어·목마·장애물 등)을 내 화면에서만 반투명으로 흐린다. 다른 화면에는 영향이 없다.
    /// 메인 카메라에서 내 캐릭터의 몸(가슴·머리 높이)까지 굵은 선(구)을 쏘아 걸린 콜라이더의 겉모습을 흐리고, 벗어나면 되돌린다.
    /// 콘텐츠가 예외 조건·수치를 등록한다. 관전 중(<see cref="CameraSpectator"/>)에는 모두 되돌린다.
    /// 공용 수치는 CameraViewSettings. 지원 셰이더: URP Lit 계열(_Surface·_BaseColor)만. 다른 셰이더는 그대로 둔다.
    /// 카메라 리그 프리팹(PlayerFollowCamera)에 둔다.
    /// </summary>
    public sealed class CameraOcclusionFade : MonoBehaviour
    {
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static readonly int BlendId = Shader.PropertyToID("_Blend");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly CameraOcclusionRules fallback = new();
        readonly CameraRequestResolver<UnityEngine.Object, CameraOcclusionRules> ruleRequests = new();
        readonly Dictionary<UnityEngine.Object, Func<Transform, bool>> exclusions = new();
        readonly List<UnityEngine.Object> expired = new();
        CameraViewDirector director;
        int steppedFrame = -1;

        [Tooltip("URP Lit 투명 머티리얼. 빌드에서 투명 셰이더 변형이 빠지지 않게 참조만 둔다(값은 쓰지 않음).")]
        [SerializeField] Material transparentTemplate;

        sealed class Faded
        {
            public Material[] Originals;
            public Material[] Copies; // 지원하지 않는 셰이더 칸은 null(원본 그대로)
            public Color[] BaseColors;
            public float Alpha = 1f;
            public bool Occluding;
        }

        readonly Dictionary<Renderer, Faded> faded = new();
        readonly List<Renderer> finished = new();
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly List<Renderer> renderers = new();

        void Awake() => director = GetComponent<CameraViewDirector>();

        void OnEnable() => CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);

        void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
            RestoreAll();
            ruleRequests.Clear();
            exclusions.Clear();
        }

        /// <summary>콘텐츠의 가림 수치 공급. 같은 출처 갱신은 등록 순서를 유지한다.</summary>
        public void RequestRules(UnityEngine.Object source, CameraOcclusionRules rules, int priority = 0)
        {
            if (source != null && rules != null) ruleRequests.Set(source, rules, priority);
        }

        public void RegisterExclusion(UnityEngine.Object source, Func<Transform, bool> predicate)
        {
            if (source != null && predicate != null) exclusions[source] = predicate;
        }

        public void ReleaseSource(UnityEngine.Object source)
        {
            ruleRequests.Remove(source);
            if (!ReferenceEquals(source, null)) exclusions.Remove(source);
        }

        void OnCameraUpdated(CinemachineBrain brain)
        {
            var view = brain != null ? brain.OutputCamera : null;
            if (view == null || view != Camera.main) return;
            PruneSources();
            var rules = ruleRequests.TryResolve(out _, out var requested) ? requested
                : director != null && director.Settings != null ? director.Settings.Occlusion : fallback;
            foreach (var entry in faded.Values) entry.Occluding = false;

            var local = NetworkPlayer.Local;
            // 관전 중에는 새 가림을 만들지 않고 기존 것만 점진적으로 복구한다.
            if (local != null && !CameraSpectator.IsSpectating) MarkOccluders(view.transform.position, local.transform, rules);

            float step = steppedFrame == Time.frameCount ? 0f : Mathf.Max(0f, rules.FadeSpeed) * Time.deltaTime;
            steppedFrame = Time.frameCount;
            foreach (var pair in faded)
            {
                var entry = pair.Value;
                if (pair.Key == null) { finished.Add(pair.Key); continue; }
                entry.Alpha = Mathf.MoveTowards(entry.Alpha, entry.Occluding ? Mathf.Clamp01(rules.Alpha) : 1f, step);
                if (!entry.Occluding && entry.Alpha >= 1f) { finished.Add(pair.Key); continue; }
                ApplyAlpha(entry);
            }

            foreach (var renderer in finished) Restore(renderer);
            finished.Clear();
        }

        void MarkOccluders(Vector3 origin, Transform target, CameraOcclusionRules rules)
        {
            float radius = Mathf.Max(0.001f, rules.Radius);
            for (int h = 0; h < 2; h++)
            {
                float height = h == 0 ? rules.TargetHeights.x : rules.TargetHeights.y;
                Vector3 to = target.position + Vector3.up * height - origin;
                float distance = to.magnitude - radius; // 내 캐릭터 바로 앞에서 멈춘다
                if (distance <= 0f) continue;

                int count = Physics.SphereCastNonAlloc(origin, radius, to.normalized, hits, distance, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i].collider.transform;
                    if (hit.IsChildOf(target) || IsExcluded(hit)) continue;
                    hit.GetComponentsInChildren(renderers);
                    foreach (var renderer in renderers) Mark(renderer);
                }
            }
            renderers.Clear();
        }

        bool IsExcluded(Transform target)
        {
            foreach (var predicate in exclusions.Values)
                if (predicate(target)) return true;
            return false;
        }

        void PruneSources()
        {
            ruleRequests.RemoveWhere((source, value) => !SourceActive(source));
            foreach (var source in exclusions.Keys)
                if (!SourceActive(source)) expired.Add(source);
            foreach (var source in expired) exclusions.Remove(source);
            expired.Clear();
        }

        static bool SourceActive(UnityEngine.Object source) => source != null && source switch
        {
            Behaviour behaviour => behaviour.isActiveAndEnabled,
            Component component => component.gameObject.activeInHierarchy,
            GameObject gameObject => gameObject.activeInHierarchy,
            _ => true,
        };

        void Mark(Renderer renderer)
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) return;
            if (!faded.TryGetValue(renderer, out var entry))
            {
                entry = CreateFaded(renderer);
                if (entry == null) return;
                faded.Add(renderer, entry);
            }
            entry.Occluding = true;
        }

        static Faded CreateFaded(Renderer renderer)
        {
            var originals = renderer.sharedMaterials;
            var copies = new Material[originals.Length];
            var colors = new Color[originals.Length];
            bool any = false;
            for (int i = 0; i < originals.Length; i++)
            {
                copies[i] = MakeTransparent(originals[i]);
                if (copies[i] == null) continue;
                colors[i] = originals[i].GetColor(BaseColorId);
                any = true;
            }
            if (!any) return null;

            var shown = new Material[originals.Length];
            for (int i = 0; i < shown.Length; i++) shown[i] = copies[i] != null ? copies[i] : originals[i];
            renderer.sharedMaterials = shown;
            return new Faded { Originals = originals, Copies = copies, BaseColors = colors };
        }

        /// <summary>URP Lit 계열 머티리얼의 투명 복사본. 지원하지 않으면 null.</summary>
        static Material MakeTransparent(Material source)
        {
            if (source == null || !source.HasProperty(SurfaceId) || !source.HasProperty(BaseColorId)) return null;
            var material = new Material(source) { name = source.name + " (Fade)" };
            material.SetFloat(SurfaceId, 1f);
            if (material.HasProperty(BlendId)) material.SetFloat(BlendId, 0f); // Alpha
            material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty(SrcBlendAlphaId)) material.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            if (material.HasProperty(DstBlendAlphaId)) material.SetFloat(DstBlendAlphaId, (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat(ZWriteId, 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        static void ApplyAlpha(Faded entry)
        {
            for (int i = 0; i < entry.Copies.Length; i++)
            {
                if (entry.Copies[i] == null) continue;
                Color color = entry.BaseColors[i];
                color.a *= entry.Alpha;
                entry.Copies[i].SetColor(BaseColorId, color);
            }
        }

        void Restore(Renderer renderer)
        {
            if (!faded.Remove(renderer, out var entry)) return;
            if (renderer != null) renderer.sharedMaterials = entry.Originals;
            foreach (var copy in entry.Copies)
                if (copy != null) Destroy(copy);
        }

        void RestoreAll()
        {
            finished.AddRange(faded.Keys);
            foreach (var renderer in finished) Restore(renderer);
            finished.Clear();
        }
    }
}
