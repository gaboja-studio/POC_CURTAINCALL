using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// 카메라와 내 캐릭터 사이를 가리는 것(다른 플레이어·목마·장애물 등)을 내 화면에서만 반투명으로 흐린다. 다른 화면에는 영향이 없다.
    /// 메인 카메라에서 내 캐릭터의 몸(가슴·머리 높이)까지 굵은 선(구)을 쏘아 걸린 콜라이더의 겉모습을 흐리고, 벗어나면 되돌린다.
    /// 코스(줄·플랫폼)와 나와 같은 목마에 속한 사람(위·아래)은 흐리지 않는다. 관전 중(<see cref="CameraSpectator"/>)에는 모두 되돌린다. 지원 셰이더: URP Lit 계열(_Surface·_BaseColor)만. 다른 셰이더 머티리얼은 그대로 둔다.
    /// 수치는 외줄 세팅(카메라)에서 매 프레임 읽는다. 근거: Integrations/Active/Integration-tightrope-prototype/camera-review-20261004.md (2026-10-04 PM).
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

        static readonly float[] TargetHeights = { 0.8f, 1.35f }; // 내 캐릭터 가슴·머리 쪽(발 기준 m)

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

        void OnDisable() => RestoreAll();

        void LateUpdate()
        {
            foreach (var entry in faded.Values) entry.Occluding = false;

            var local = NetworkPlayer.Local;
            var view = Camera.main;
            // 관전 중(내 캐릭터 사망)에는 흐리지 않는다: 아무것도 표시하지 않으면 아래에서 모두 원래대로 돌아온다
            if (local != null && view != null && !CameraSpectator.IsSpectating) MarkOccluders(view.transform.position, local.transform);

            var rules = GameSettings.Tightrope.Camera;
            float step = rules.FadeSpeed * Time.deltaTime;
            foreach (var pair in faded)
            {
                var entry = pair.Value;
                if (pair.Key == null) { finished.Add(pair.Key); continue; }
                entry.Alpha = Mathf.MoveTowards(entry.Alpha, entry.Occluding ? rules.OccluderAlpha : 1f, step);
                if (!entry.Occluding && entry.Alpha >= 1f) { finished.Add(pair.Key); continue; }
                ApplyAlpha(entry);
            }

            foreach (var renderer in finished) Restore(renderer);
            finished.Clear();
        }

        void MarkOccluders(Vector3 origin, Transform target)
        {
            var course = TightropeCourse.Current;
            var piggyback = PiggybackSystem.Current;
            var local = target.GetComponent<NetworkPlayer>();
            float radius = GameSettings.Tightrope.Camera.OccluderRadius;
            foreach (float height in TargetHeights)
            {
                Vector3 to = target.position + Vector3.up * height - origin;
                float distance = to.magnitude - radius; // 내 캐릭터 바로 앞에서 멈춘다
                if (distance <= 0f) continue;

                int count = Physics.SphereCastNonAlloc(origin, radius, to.normalized, hits, distance, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i].collider.transform;
                    if (hit.IsChildOf(target)) continue;                              // 내 캐릭터
                    if (course != null && hit.IsChildOf(course.transform)) continue;  // 줄·플랫폼
                    if (piggyback != null && piggyback.IsSameStack(local, hit.GetComponentInParent<NetworkPlayer>())) continue; // 같은 목마
                    hit.GetComponentsInChildren(renderers);
                    foreach (var renderer in renderers) Mark(renderer);
                }
            }
            renderers.Clear();
        }

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
