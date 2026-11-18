using System;
using System.Collections.Generic;
using CurtainCall.Cameras.Requests;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// 내 카메라를 가리는 전경 오브젝트는 화면에 고정된 디더 패턴으로 그린다. 다른 캐릭터의 가림은 반투명으로 표시한다.
    /// 메인 카메라에서 내 캐릭터까지 콜라이더와 렌더링 전용 메시를 검사한다.
    /// 콘텐츠가 예외 조건·수치를 등록한다. 관전 중(<see cref="CameraSpectator"/>)에는 모두 되돌린다.
    /// 공용 수치는 CameraViewSettings. URP 계열은 원본을 복사하고, 표준 색·텍스처를 가진 불투명 재질에는 공용 투명 재질을 쓴다.
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
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        static readonly int CoverageId = Shader.PropertyToID("_CameraOcclusionCoverage");

        readonly CameraOcclusionRules fallback = new();
        readonly CameraRequestResolver<UnityEngine.Object, CameraOcclusionRules> ruleRequests = new();
        readonly Dictionary<UnityEngine.Object, Func<Transform, bool>> exclusions = new();
        readonly List<UnityEngine.Object> expired = new();
        CameraViewDirector director;
        SilhouetteOutline occludedSubject;
        int steppedFrame = -1;

        [Tooltip("URP Lit 투명 머티리얼. 빌드에서 투명 셰이더 변형이 빠지지 않게 참조만 둔다(값은 쓰지 않음).")]
        [SerializeField] Material transparentTemplate;

        sealed class Faded
        {
            public Material[] Originals;
            public Material[] Copies; // 지원하지 않는 셰이더 칸은 null(원본 그대로)
            public Color[] BaseColors;
            public bool[] PropertyColors;
            public float[] PropertyAlphas;
            public MaterialPropertyBlock PropertyBlock;
            public Renderer Renderer;
            public float Alpha = 1f;
            public bool Occluding;
        }

        readonly Dictionary<Renderer, Faded> faded = new();
        readonly List<Renderer> finished = new();
        readonly HashSet<Renderer> foregroundOccluders = new();
        readonly Dictionary<Renderer, ForegroundFade> foregroundFades = new();
        readonly List<Renderer> switchedForeground = new();
        readonly List<Material> liveMaterials = new();
        Camera foregroundCamera;
        Shader ditherShader;
        const float ForegroundConfirmDelay = .035f;
        const float ForegroundReleaseDelay = .15f;

        sealed class ForegroundFade
        {
            public float Coverage = 1f;
            public float StartCoverage = 1f;
            public float GoalCoverage = 1f;
            public float Progress = 1f;
            public float FirstHit;
            public float LastHit;
            public bool Confirmed;
            public Material[] Originals;
            public Material[] Dithered;
        }
        RaycastHit[] hits = new RaycastHit[32];
        Collider[] nearCamera = new Collider[8];
        readonly List<Renderer> renderers = new();
        readonly HashSet<Transform> markedRoots = new();
        readonly List<GameObject> sceneRoots = new();
        readonly List<Renderer> sceneRenderers = new();
        readonly List<Renderer> nearbyRenderers = new();
        readonly List<Renderer> rootRenderers = new();
        // 범위 상자에 걸린 메시를 실제 면으로 다시 확인한다. 맵 외곽 껍데기처럼 상자가 카메라를 품기만 하는 물체가 늘 점무늬로 비쳤다(2026-11-18 사용자: 배관 연결 통로 깜빡임).
        readonly Dictionary<Renderer, CameraMeshCollision> meshChecks = new();
        float refreshSceneAt = float.NegativeInfinity;
        float refreshNearbyAt = float.NegativeInfinity;
        Vector3 lastCameraPosition, lastTargetPosition;
        const float NearPlaneRange = .35f;
        CinemachineCamera followCamera;

        void Awake() { director = GetComponent<CameraViewDirector>(); followCamera = GetComponent<CinemachineCamera>(); }

        /// <summary>
        /// 내 캐릭터를 따라가는 카메라가 화면을 잡고 있는가. 연출 카메라(탈출로 문 열림·튜토리얼·침대 등)가 잡는 동안에는
        /// 연출 카메라와 캐릭터 사이 벽이 점무늬로 비치지 않게 투명화하지 않는다(2026-11-18 사용자).
        /// </summary>
        bool Following(CinemachineBrain brain)
        {
            var live = brain != null ? brain.ActiveVirtualCamera : null;
            return live == null || followCamera == null || ReferenceEquals(live, followCamera);
        }

        void OnEnable()
        {
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RenderPipelineManager.beginCameraRendering += BeginForegroundRender;
            RenderPipelineManager.endCameraRendering += EndForegroundRender;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InvalidateRenderers();
        void OnSceneUnloaded(Scene scene) => InvalidateRenderers();
        void InvalidateRenderers()
        {
            refreshSceneAt = float.NegativeInfinity;
            meshChecks.Clear();
            foregroundOccluders.Clear();
            ClearForegroundFades();
        }

        void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            RenderPipelineManager.beginCameraRendering -= BeginForegroundRender;
            RenderPipelineManager.endCameraRendering -= EndForegroundRender;
            foregroundCamera = null;
            sceneRenderers.Clear();
            nearbyRenderers.Clear();
            InvalidateRenderers();
            occludedSubject?.Dispose();
            occludedSubject = null;
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
            if (!Application.IsPlaying(gameObject)) return;
            var view = brain != null ? brain.OutputCamera : null;
            if (view == null || view != Camera.main) return;
            foregroundCamera = view;
            foregroundOccluders.Clear();
            PruneSources();
            var rules = ruleRequests.TryResolve(out _, out var requested) ? requested
                : director != null && director.Settings != null ? director.Settings.Occlusion : fallback;
            foreach (var entry in faded.Values) entry.Occluding = false;

            var local = director != null ? director.LocalPlayer : NetworkPlayer.Local;
            bool trackLocal = local != null && !CameraSpectator.IsSpectating && Following(brain);
            RefreshOccludedSubject(view, trackLocal ? local : null);
            // 관전 중에는 새 가림을 만들지 않고 기존 것만 점진적으로 복구한다.
            if (trackLocal)
            {
                float posture = local.TryGetComponent(out PlayerMover mover) ? mover.PostureHeightScale : 1f;
                MarkOccluders(view.transform.position, local.transform, posture, rules);
                MarkVisualOccluders(view.transform.position, local.transform, posture, rules, view.cullingMask);
            }

            bool newFrame = steppedFrame != Time.frameCount;
            float fadeSpeed = Mathf.Max(0f, rules.FadeSpeed);
            float step = newFrame ? fadeSpeed * Time.deltaTime : 0f;
            // Keep a long frame from advancing most of the foreground transition at once.
            float foregroundStep = newFrame ? Mathf.Min(fadeSpeed * Time.unscaledDeltaTime * .5f, .2f) : 0f;
            steppedFrame = Time.frameCount;
            if (trackLocal) StepForeground(Time.unscaledTime, foregroundStep, Mathf.Clamp01(rules.Alpha));
            else ClearForegroundFades();
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

        void BeginForegroundRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != foregroundCamera) return;
            RestoreForeground();
            if (CameraSpectator.IsSpectating || foregroundFades.Count == 0) return;
            if (ditherShader == null) ditherShader = Resources.Load<Shader>("Prefabs/Controllers/Camera/CameraForegroundDither");
            if (ditherShader == null) return;
            foreach (var pair in foregroundFades)
            {
                var renderer = pair.Key;
                var fade = pair.Value;
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff) continue;
                if (fade.Coverage >= 1f) continue;
                renderer.GetSharedMaterials(liveMaterials);
                if (!MatchesOriginals(fade))
                {
                    DestroyDitherMaterials(fade);
                    fade.Originals = liveMaterials.ToArray();
                    fade.Dithered = new Material[fade.Originals.Length];
                    for (int i = 0; i < fade.Originals.Length; i++)
                    {
                        var source = fade.Originals[i];
                        fade.Dithered[i] = source != null && source.renderQueue < (int)RenderQueue.Transparent
                            ? MakeDithered(source, ditherShader) : source;
                    }
                }
                for (int i = 0; i < fade.Dithered.Length; i++)
                    if (fade.Dithered[i] != fade.Originals[i]) fade.Dithered[i].SetFloat(CoverageId, fade.Coverage);
                renderer.sharedMaterials = fade.Dithered;
                switchedForeground.Add(renderer);
            }
        }

        void EndForegroundRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera == foregroundCamera) RestoreForeground();
        }

        void RestoreForeground()
        {
            foreach (var renderer in switchedForeground)
                if (renderer != null) renderer.sharedMaterials = foregroundFades[renderer].Originals;
            switchedForeground.Clear();
        }

        bool MatchesOriginals(ForegroundFade fade)
        {
            if (fade.Originals == null || fade.Originals.Length != liveMaterials.Count) return false;
            for (int i = 0; i < liveMaterials.Count; i++)
                if (liveMaterials[i] != fade.Originals[i]) return false;
            return true;
        }

        void StepForeground(float now, float step, float targetCoverage)
        {
            foreach (var renderer in foregroundOccluders)
            {
                if (!foregroundFades.TryGetValue(renderer, out var fade))
                    foregroundFades.Add(renderer, fade = new ForegroundFade { FirstHit = now });
                fade.LastHit = now;
                if (now - fade.FirstHit >= ForegroundConfirmDelay) fade.Confirmed = true;
            }

            foreach (var pair in foregroundFades)
            {
                var fade = pair.Value;
                bool held = fade.Confirmed && now - fade.LastHit <= ForegroundReleaseDelay;
                float goal = held ? targetCoverage : 1f;
                if (goal != fade.GoalCoverage)
                {
                    // If the target changes mid-fade, continue from the visible density.
                    fade.StartCoverage = fade.Coverage;
                    fade.GoalCoverage = goal;
                    fade.Progress = 0f;
                }
                fade.Progress = Mathf.Min(fade.Progress + step, 1f);
                fade.Coverage = Mathf.SmoothStep(fade.StartCoverage, fade.GoalCoverage, fade.Progress);
                if (pair.Key == null || (!held && fade.Coverage >= 1f && !foregroundOccluders.Contains(pair.Key)))
                    finished.Add(pair.Key);
            }
            foreach (var renderer in finished)
            {
                DestroyDitherMaterials(foregroundFades[renderer]);
                foregroundFades.Remove(renderer);
            }
            finished.Clear();
        }

        void ClearForegroundFades()
        {
            RestoreForeground();
            foreach (var fade in foregroundFades.Values) DestroyDitherMaterials(fade);
            foregroundFades.Clear();
        }

        static void DestroyDitherMaterials(ForegroundFade fade)
        {
            if (fade.Dithered == null) return;
            for (int i = 0; i < fade.Dithered.Length; i++)
                if (fade.Dithered[i] != fade.Originals[i]) CoreUtils.Destroy(fade.Dithered[i]);
        }

        static Material MakeDithered(Material source, Shader shader)
        {
            var material = new Material(shader) { name = source.name + " (Camera Dither)" };
            material.CopyPropertiesFromMaterial(source);
            material.shaderKeywords = Array.Empty<string>();
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.SetFloat(SurfaceId, 0f);
            CopyBaseAppearance(source, material);
            if (source.IsKeywordEnabled("_ALPHATEST_ON")) material.EnableKeyword("_ALPHATEST_ON");
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
                material.EnableKeyword("_NORMALMAP");
            else if (source.HasProperty("_BaseNormalMap") && source.GetTexture("_BaseNormalMap") != null)
            {
                material.SetTexture("_BumpMap", source.GetTexture("_BaseNormalMap"));
                material.EnableKeyword("_NORMALMAP");
            }
            return material;
        }

        static void CopyBaseAppearance(Material source, Material target)
        {
            int color = source.HasProperty(BaseColorId) ? BaseColorId : ColorId;
            int map = source.HasProperty(BaseMapId) ? BaseMapId : source.HasProperty(BaseColorMapId) ? BaseColorMapId : MainTexId;
            if (source.HasProperty(color)) target.SetColor(BaseColorId, source.GetColor(color));
            if (!source.HasProperty(map)) return;
            target.SetTexture(BaseMapId, source.GetTexture(map));
            target.SetTextureOffset(BaseMapId, source.GetTextureOffset(map));
            target.SetTextureScale(BaseMapId, source.GetTextureScale(map));
        }

        void MarkVisualOccluders(Vector3 origin, Transform target, float posture, CameraOcclusionRules rules, int cullingMask)
        {
            var chest = target.position + Vector3.up * rules.TargetHeights.x * posture;
            var head = target.position + Vector3.up * rules.TargetHeights.y * posture;
            // Bounds-only meshes use a tighter sweep so objects beside the sight line stay solid.
            float radius = Mathf.Clamp(rules.Radius, .001f, .15f);
            RefreshVisualCandidates(origin, chest, head, radius);

            foreach (var renderer in nearbyRenderers)
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy
                    || (cullingMask & (1 << renderer.gameObject.layer)) == 0
                    || renderer.transform.IsChildOf(target) || IsExcluded(renderer.transform)) continue;
                IntersectsView(renderer, origin, chest, radius, out float chestHit);
                IntersectsView(renderer, origin, head, radius, out float headHit);
                float hit = Mathf.Min(chestHit, headHit);
                if (float.IsPositiveInfinity(hit)) continue;
                if (renderer.GetComponentInParent<NetworkPlayer>() != null)
                    Mark(renderer, hit <= NearPlaneRange ? Mathf.Clamp01(rules.Alpha) : 1f);
                else
                    foregroundOccluders.Add(renderer);
            }
        }

        void RefreshVisualCandidates(Vector3 origin, Vector3 chest, Vector3 head, float radius)
        {
            float now = Time.unscaledTime;
            if (now >= refreshSceneAt)
            {
                sceneRenderers.Clear();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    scene.GetRootGameObjects(sceneRoots);
                    foreach (var root in sceneRoots)
                    {
                        root.GetComponentsInChildren(true, rootRenderers);
                        foreach (var renderer in rootRenderers)
                            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) sceneRenderers.Add(renderer);
                    }
                }
                rootRenderers.Clear();
                sceneRoots.Clear();
                refreshSceneAt = now + 1f;
                refreshNearbyAt = float.NegativeInfinity;
            }
            if (now < refreshNearbyAt && (origin - lastCameraPosition).sqrMagnitude < .25f
                && (chest - lastTargetPosition).sqrMagnitude < .25f) return;

            // 좁은 후보 범위는 짧게 보관하되, 실제 시선 교차는 매 카메라 갱신마다 검사한다.
            var viewBounds = new Bounds(origin, Vector3.zero);
            viewBounds.Encapsulate(chest);
            viewBounds.Encapsulate(head);
            viewBounds.Expand(2f * (radius + 1f));
            nearbyRenderers.Clear();
            foreach (var renderer in sceneRenderers)
                if (renderer != null && renderer.gameObject.activeInHierarchy && renderer.bounds.Intersects(viewBounds))
                    nearbyRenderers.Add(renderer);
            refreshNearbyAt = now + .12f;
            lastCameraPosition = origin;
            lastTargetPosition = chest;
        }

        bool IntersectsView(Renderer renderer, Vector3 origin, Vector3 target, float radius, out float hit)
        {
            if (!IntersectsBounds(renderer, origin, target, radius, out hit)) return false;
            if (renderer is SkinnedMeshRenderer || !renderer.TryGetComponent(out MeshFilter filter)
                || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return true;
            if (renderer.TryGetComponent(out CameraCollisionSurface surface) && surface.isActiveAndEnabled && surface.UseMeshGeometry) return true;
            if (!meshChecks.TryGetValue(renderer, out var check)) meshChecks[renderer] = check = new CameraMeshCollision();
            Vector3 segment = target - origin;
            float distance = segment.magnitude;
            if (!check.Intersect(filter.sharedMesh, renderer.localToWorldMatrix, origin, segment / distance, radius, distance, out float meshHit)
                || meshHit >= distance) { hit = float.PositiveInfinity; return false; }
            hit = meshHit;
            return true;
        }

        static bool IntersectsBounds(Renderer renderer, Vector3 origin, Vector3 target, float radius, out float hit)
        {
            hit = float.PositiveInfinity;
            Vector3 segment = target - origin;
            float distance = segment.magnitude;
            if (distance <= .001f) return false;
            Vector3 direction = segment / distance;
            // A skinned mesh's rendered bounds can follow its root bone rather than this transform.
            if (renderer is SkinnedMeshRenderer)
            {
                var worldBounds = renderer.bounds;
                worldBounds.Expand(2f * radius);
                if (worldBounds.Contains(origin)) { hit = 0f; return true; }
                if (!worldBounds.IntersectRay(new Ray(origin, direction), out float worldHit) || worldHit >= distance)
                    return false;
                hit = worldHit;
                return true;
            }

            // 명시된 벽면은 기존 메시 검사로 문과 통로의 빈 공간을 보존한다.
            if (renderer.TryGetComponent(out CameraCollisionSurface surface) && surface.isActiveAndEnabled && surface.UseMeshGeometry
                && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && filter.sharedMesh.isReadable)
            {
                if (!surface.IntersectWithin(origin, direction, radius, distance, out float surfaceHit) || surfaceHit >= distance) return false;
                hit = surfaceHit;
                return true;
            }

            // 월드 AABB의 빈 모서리가 회전한 배관 등을 가림으로 오인하지 않도록 렌더러 로컬 범위를 쓴다.
            var inverse = renderer.worldToLocalMatrix;
            Vector3 localOrigin = inverse.MultiplyPoint3x4(origin);
            Vector3 localDirection = inverse.MultiplyVector(direction);
            float localLength = localDirection.magnitude;
            if (localLength < .000001f) return false;
            var bounds = renderer.localBounds;
            if (bounds.Contains(localOrigin)) { hit = 0f; return true; }
            bounds.Expand(new Vector3(((Vector3)inverse.GetRow(0)).magnitude, ((Vector3)inverse.GetRow(1)).magnitude,
                ((Vector3)inverse.GetRow(2)).magnitude) * (2f * radius));
            if (!bounds.IntersectRay(new Ray(localOrigin, localDirection / localLength), out float localHit)
                || localHit >= distance * localLength) return false;
            hit = localHit / localLength;
            return true;
        }

        void RefreshOccludedSubject(Camera view, NetworkPlayer player)
        {
            Transform model = null;
            if (player != null && player.State == PlayerState.Normal && director != null && !director.IsFirstPerson
                && player.TryGetComponent(out PlayerModelSlot slot) && slot.CurrentModel != null)
                model = slot.CurrentModel.transform;
            if (occludedSubject != null && (model == null || occludedSubject.Target != model))
            {
                occludedSubject.Dispose();
                occludedSubject = null;
            }
            // Depth sees render-only ceilings and pipework too. The outline pass draws just the obscured pixels.
            if (model != null)
            {
                occludedSubject ??= new SilhouetteOutline(model, new Color(.65f, .75f, .77f, .42f), occludedOnly: true);
                occludedSubject.Viewer = view;
            }
        }

        void MarkOccluders(Vector3 origin, Transform target, float posture, CameraOcclusionRules rules)
        {
            float radius = Mathf.Max(0.001f, rules.Radius);
            bool solidWalls = CameraViewDirector.UsesWallCollision(SceneManager.GetActiveScene());
            markedRoots.Clear();
            for (int h = 0; h < 2; h++)
            {
                float height = (h == 0 ? rules.TargetHeights.x : rules.TargetHeights.y) * posture;
                Vector3 to = target.position + Vector3.up * height - origin;
                float distance = to.magnitude - radius; // 내 캐릭터 바로 앞에서 멈춘다
                if (distance <= 0f) continue;

                int count;
                while ((count = Physics.SphereCastNonAlloc(origin, radius, to.normalized, hits,
                    distance, ~0, QueryTriggerInteraction.Ignore)) == hits.Length)
                    Array.Resize(ref hits, hits.Length * 2);
                for (int i = 0; i < count; i++)
                    MarkOccluder(hits[i].collider, target, solidWalls);
            }

            // A sweep skips colliders it starts inside, including a teammate at the near plane.
            int overlaps;
            while ((overlaps = Physics.OverlapSphereNonAlloc(origin, Mathf.Min(radius, .15f),
                nearCamera, ~0, QueryTriggerInteraction.Ignore)) == nearCamera.Length)
                Array.Resize(ref nearCamera, nearCamera.Length * 2);
            for (int i = 0; i < overlaps; i++) MarkOccluder(nearCamera[i], target, solidWalls);
        }

        void MarkOccluder(Collider collider, Transform target, bool solidWalls)
        {
            if (collider == null || collider.transform.IsChildOf(target) || IsExcluded(collider.transform)) return;
            var player = collider.GetComponentInParent<NetworkPlayer>();
            var body = collider.attachedRigidbody;
            if (solidWalls && player == null && (body == null || body.isKinematic)) return;
            Transform root = player != null ? player.transform : body != null ? body.transform : collider.transform;
            if (root.IsChildOf(target) || IsExcluded(root) || !markedRoots.Add(root)) return;
            root.GetComponentsInChildren(renderers);
            foreach (var renderer in renderers)
            {
                if (renderer.transform.IsChildOf(target) || IsExcluded(renderer.transform)
                    || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (player != null) Mark(renderer);
                else foregroundOccluders.Add(renderer);
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

        void Mark(Renderer renderer, float startingAlpha = 1f)
        {
            if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) return;
            if (!faded.TryGetValue(renderer, out var entry))
            {
                entry = CreateFaded(renderer, transparentTemplate);
                if (entry == null) return;
                faded.Add(renderer, entry);
            }
            entry.Alpha = Mathf.Min(entry.Alpha, startingAlpha);
            entry.Occluding = true;
        }

        static Faded CreateFaded(Renderer renderer, Material transparentTemplate)
        {
            var originals = renderer.sharedMaterials;
            var copies = new Material[originals.Length];
            var colors = new Color[originals.Length];
            bool any = false;
            for (int i = 0; i < originals.Length; i++)
            {
                copies[i] = MakeTransparent(originals[i], transparentTemplate);
                if (copies[i] == null) continue;
                colors[i] = copies[i].GetColor(BaseColorId);
                any = true;
            }
            if (!any) return null;

            var shown = new Material[originals.Length];
            for (int i = 0; i < shown.Length; i++) shown[i] = copies[i] != null ? copies[i] : originals[i];
            renderer.sharedMaterials = shown;
            var propertyColors = new bool[originals.Length];
            var propertyAlphas = new float[originals.Length];
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < originals.Length; i++)
            {
                renderer.GetPropertyBlock(block, i);
                propertyColors[i] = block.HasColor(BaseColorId);
                propertyAlphas[i] = propertyColors[i] ? block.GetColor(BaseColorId).a : 1f;
                block.Clear();
            }
            return new Faded { Originals = originals, Copies = copies, BaseColors = colors,
                PropertyColors = propertyColors, PropertyAlphas = propertyAlphas, PropertyBlock = block, Renderer = renderer };
        }

        /// <summary>원본의 셰이더 또는 공용 템플릿으로 투명 복사본을 만든다.</summary>
        static Material MakeTransparent(Material source, Material transparentTemplate)
        {
            if (source == null) return null;
            Material material;
            if (source.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)
                && source.HasProperty(SurfaceId) && source.HasProperty(BaseColorId) && source.HasProperty(ZWriteId)
                && source.HasProperty(SrcBlendId) && source.HasProperty(DstBlendId))
                material = new Material(source);
            else
            {
                if (transparentTemplate == null || source.renderQueue >= (int)RenderQueue.Transparent) return null;
                int color = source.HasProperty(BaseColorId) ? BaseColorId : ColorId;
                int map = source.HasProperty(BaseMapId) ? BaseMapId : source.HasProperty(BaseColorMapId) ? BaseColorMapId : MainTexId;
                if (!source.HasProperty(color) && !source.HasProperty(map)) return null;
                material = new Material(transparentTemplate);
                CopyBaseAppearance(source, material);
            }
            material.name = source.name + " (Fade)";
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
                if (entry.PropertyColors[i] && entry.Renderer != null)
                {
                    var block = entry.PropertyBlock;
                    entry.Renderer.GetPropertyBlock(block, i);
                    Color propertyColor = block.GetColor(BaseColorId);
                    propertyColor.a = entry.PropertyAlphas[i] * entry.Alpha;
                    block.SetColor(BaseColorId, propertyColor);
                    entry.Renderer.SetPropertyBlock(block, i);
                }
            }
        }

        void Restore(Renderer renderer)
        {
            if (!faded.Remove(renderer, out var entry)) return;
            if (renderer != null)
            {
                entry.Alpha = 1f;
                ApplyAlpha(entry);
                renderer.sharedMaterials = entry.Originals;
            }
            foreach (var copy in entry.Copies)
                CoreUtils.Destroy(copy);
        }

        void RestoreAll()
        {
            finished.AddRange(faded.Keys);
            foreach (var renderer in finished) Restore(renderer);
            finished.Clear();
        }
    }
}
