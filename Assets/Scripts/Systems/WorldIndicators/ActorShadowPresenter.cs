using System;
using System.Collections.Generic;
using CurtainCall.Damage;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WorldIndicators
{
    /// <summary>Owns only captured body casting and an optional pursuer ground blob.</summary>
    [DefaultExecutionOrder(1200), DisallowMultipleComponent]
    public sealed class ActorShadowPresenter : MonoBehaviour
    {
        sealed class Body
        {
            public Renderer Renderer;
            public ShadowCastingMode Original;
            public bool Suppressed;
        }

        readonly List<Body> bodies = new();
        readonly RaycastHit[] hits = new RaycastHit[32];
        MonoBehaviour owner;
        Transform bodyRoot;
        PlayerModelSlot playerSlot;
        Func<bool> visible;
        bool player, escape, captured, materialLoadAttempted;
        float nextSample;
        Mesh quad;
        MeshRenderer blob;
        MaterialPropertyBlock properties;
        static Material sharedMaterial;
        const string MaterialPath = "Materials/LandingShadow/LandingShadow";

        public static ActorShadowPresenter AttachPlayer(PlayerModelSlot slot, GameObject model)
        {
            var presenter = model.GetComponent<ActorShadowPresenter>();
            if (!presenter) presenter = model.AddComponent<ActorShadowPresenter>();
            presenter.owner = slot;
            presenter.playerSlot = slot;
            presenter.player = true;
            presenter.bodyRoot = model.transform;
            if (!presenter.captured)
            {
                // Explicit authored bodies exclude limb templates and held-item visuals,
                // including when an existing child already has runtime attachments.
                var humanoids = model.GetComponentsInChildren<HumanoidModel>(true);
                if (humanoids.Length > 0)
                    foreach (var humanoid in humanoids)
                    {
                        presenter.Capture(humanoid.Original);
                        if (humanoid.Bodies != null)
                            foreach (var body in humanoid.Bodies) presenter.Capture(body);
                    }
                else
                {
                    var presentation = slot.GetComponent<HumanoidPlayerPresentation>();
                    var held = presentation ? presentation.HeldItemVisual : null;
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                        if (!held || !renderer.transform.IsChildOf(held.transform)) presenter.Capture(renderer);
                }
                presenter.captured = true;
            }
            presenter.enabled = true;
            presenter.ApplyCasting(slot.isActiveAndEnabled && GameSettings.WorldUi.disablePlayerRealtimeShadows);
            return presenter;
        }

        /// <summary>Call at initialization, before runtime props/UI are attached.</summary>
        public static ActorShadowPresenter AttachPursuer(MonoBehaviour owner, Transform authoredBody,
            bool escape, Func<bool> visible = null)
        {
            if (!authoredBody) return null;
            var presenter = owner.GetComponent<ActorShadowPresenter>();
            if (!presenter) presenter = owner.gameObject.AddComponent<ActorShadowPresenter>();
            presenter.owner = owner;
            presenter.bodyRoot = authoredBody;
            presenter.escape = escape;
            presenter.visible = visible;
            if (!presenter.captured)
            {
                foreach (var renderer in authoredBody.GetComponentsInChildren<Renderer>(true)) presenter.Capture(renderer);
                presenter.captured = true;
            }
            return presenter;
        }

        void Capture(Renderer renderer)
        {
            if (!renderer || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                || renderer.GetComponentInParent<DismemberedLimb>()) return;
            foreach (var body in bodies) if (body.Renderer == renderer) return;
            bodies.Add(new Body { Renderer = renderer, Original = renderer.shadowCastingMode });
        }

        public void ReleasePlayer()
        {
            ApplyCasting(false);
            owner = null;
            playerSlot = null;
            enabled = false;
        }

        void LateUpdate()
        {
            bool attached = owner && owner.isActiveAndEnabled && bodyRoot;
            if (player)
            {
                attached = attached && playerSlot && playerSlot.CurrentModel == gameObject
                    && transform.IsChildOf(playerSlot.Slot);
                ApplyCasting(attached && GameSettings.WorldUi.disablePlayerRealtimeShadows);
                return;
            }

            var settings = GameSettings.WorldUi;
            bool useBlob = attached && settings.pursuerBlobShadows;
            // Do not remove authored shadows if the shared resource is unavailable.
            if (useBlob && !blob && !materialLoadAttempted) CreateBlob();
            ApplyCasting(useBlob && blob);
            if (!useBlob || !blob || visible != null && !visible() || !BodyVisible())
            { HideBlob(); return; }

            var camera = Camera.main;
            var origin = owner.transform.position;
            float distance = Mathf.Max(0, settings.maxDistance);
            if (!camera || (camera.transform.position - origin).sqrMagnitude > distance * distance)
            { HideBlob(); return; }
            var viewport = camera.WorldToViewportPoint(origin);
            if (viewport.z <= 0 || viewport.x < -.2f || viewport.x > 1.2f || viewport.y < -.2f || viewport.y > 1.2f)
            { HideBlob(); return; }
            if (Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + Mathf.Max(.02f, settings.shadowRefreshSeconds);

            // One downward query; never retry a full buffer or sample each quad corner.
            int count = Physics.RaycastNonAlloc(origin + Vector3.up, Vector3.down, hits, 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit ground = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                var collider = hit.collider;
                if (!collider || hit.normal.y < .65f || hit.distance >= nearest
                    || collider.transform.IsChildOf(owner.transform)
                    || collider.GetComponentInParent<PlayerMover>()
                    || collider.GetComponentInParent<PlayerModelSlot>()
                    || collider.GetComponentInParent<DismemberedLimb>()) continue;
                ground = hit;
                nearest = hit.distance;
            }
            // A saturated query may omit the nearest floor: hide instead of guessing.
            if (count == hits.Length || float.IsPositiveInfinity(nearest)) { blob.enabled = false; return; }
            float radius = Mathf.Max(.01f, escape ? settings.escapePursuerShadowRadius : settings.waitingPursuerShadowRadius);
            blob.transform.SetPositionAndRotation(ground.point + ground.normal * .015f,
                Quaternion.FromToRotation(Vector3.up, ground.normal));
            blob.transform.localScale = Vector3.one * radius;
            properties.SetColor("_FillColor", new Color(.015f, .02f, .025f, Mathf.Clamp01(settings.shadowOpacity)));
            bool clip = LandingShadowMarkers.TryGetSurfaceClip(ground.collider, ground.normal, out var matrix, out var half);
            properties.SetFloat("_ClipMode", clip ? 1 : 0);
            if (clip)
            {
                properties.SetMatrix("_ClipMatrix", matrix);
                properties.SetVector("_ClipSize", new Vector4(half.x, half.y, radius, 0));
            }
            blob.SetPropertyBlock(properties);
            blob.enabled = true;
        }

        void ApplyCasting(bool suppress)
        {
            foreach (var body in bodies)
            {
                var renderer = body.Renderer;
                if (!renderer) continue;
                bool attached = bodyRoot && renderer.transform.IsChildOf(bodyRoot);
                bool desired = suppress && attached;
                if (desired == body.Suppressed) continue;
                renderer.shadowCastingMode = desired ? ShadowCastingMode.Off : body.Original;
                body.Suppressed = desired;
                // receiveShadows and all material properties belong to the original model.
            }
        }

        bool BodyVisible()
        {
            foreach (var body in bodies)
                if (body.Renderer && body.Renderer.enabled && !body.Renderer.forceRenderingOff
                    && body.Renderer.gameObject.activeInHierarchy && body.Renderer.transform.IsChildOf(bodyRoot)) return true;
            return false;
        }

        void CreateBlob()
        {
            materialLoadAttempted = true;
            if (!sharedMaterial) sharedMaterial = Resources.Load<Material>(MaterialPath);
            if (!sharedMaterial) return;
            quad = new Mesh { name = "Actor shadow quad" };
            quad.vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, -1) };
            quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            // Unparented: keep a world-space radius even under a scaled animated doll.
            var go = new GameObject("Actor ground shadow", typeof(MeshFilter), typeof(MeshRenderer));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            go.GetComponent<MeshFilter>().sharedMesh = quad;
            blob = go.GetComponent<MeshRenderer>();
            blob.sharedMaterial = sharedMaterial;
            blob.shadowCastingMode = ShadowCastingMode.Off;
            blob.receiveShadows = false;
            blob.lightProbeUsage = LightProbeUsage.Off;
            blob.reflectionProbeUsage = ReflectionProbeUsage.Off;
            blob.enabled = false;
            properties = new MaterialPropertyBlock();
            properties.SetColor("_RimColor", Color.clear);
            properties.SetFloat("_OutlineWidth", 0);
            properties.SetFloat("_Blur", .15f);
        }

        void HideBlob() { if (blob) blob.enabled = false; nextSample = 0; }
        void OnDisable() { ApplyCasting(false); HideBlob(); }
        void OnTransformParentChanged()
        {
            if (player && playerSlot && !transform.IsChildOf(playerSlot.Slot)) ReleasePlayer();
        }
        void OnDestroy()
        {
            ApplyCasting(false);
            if (blob) DisposeOwned(blob.gameObject);
            if (quad) DisposeOwned(quad);
        }

        static void DisposeOwned(UnityEngine.Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
