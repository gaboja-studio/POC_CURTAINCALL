using System;
using System.Collections.Generic;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Pursuit
{
    [DefaultExecutionOrder(1200)]
    public sealed class PursuitEyeFeedback : MonoBehaviour
    {
        sealed class View
        {
            public Transform Root, Head;
            public float FallbackHeight;
            public SpriteRenderer Renderer;
            public readonly PursuitEyeTransition Transition = new();
        }
        readonly Dictionary<PursuitActor, View> views = new();
        readonly List<PursuitActor> removed = new();
        PursuitDirector director;
        PlayerInputReader offlineReader;
        GameObject previousObserver;
        Material material;
        Sprite[] frames;
        MaterialPropertyBlock properties;
        static readonly int TintId = Shader.PropertyToID("_Tint"), EmissionId = Shader.PropertyToID("_Emission"), GlowId = Shader.PropertyToID("_GlowOpacity");
        public int VisibleCount { get; private set; }

        void OnEnable()
        {
            director = GetComponent<PursuitDirector>();
            frames = Resources.LoadAll<Sprite>("PursuitEye/Frames");
            Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            var shader = Resources.Load<Shader>("PursuitEye/PursuitEye");
            if (frames.Length != 8 || shader == null) {
                Debug.LogError("Pursuit eye assets missing. Run Tools/Curtain Call/Pursuit/Import Eye Sprites.", this);
                enabled = false; return;
            }
            material = new Material(shader) { name = "Local pursuit eyes", hideFlags = HideFlags.DontSave };
            properties = new MaterialPropertyBlock();
        }

        View CreateView(PursuitActor actor)
        {
            Transform head = null;
            foreach (var bone in actor.GetComponentsInChildren<Transform>(true)) if (bone.name == "Head") { head = bone; break; }
            float top = actor.transform.position.y + 1.7f;
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) top = Mathf.Max(top, renderer.bounds.max.y);
            // Parent under the director, not under scaled FBX bones or the actor's renderer-culling hierarchy.
            var go = new GameObject("Local eye - " + actor.name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = material; sr.sprite = frames[0]; sr.enabled = false;
            sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
            sr.lightProbeUsage = LightProbeUsage.Off; sr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var view = new View { Root = go.transform, Head = head, FallbackHeight = top - actor.transform.position.y, Renderer = sr };
            views.Add(actor, view); return view;
        }

        void LateUpdate()
        {
            VisibleCount = 0;
            var settings = GameSettings.WaitingRoom.Pursuit.Eyes;
            var observer = PursuitFeedback.LocalObserver(ref offlineReader);
            var camera = Camera.main;
            bool available = settings != null && settings.Enabled && director != null && director.Elapsed >= 0
                && director.PlayerRecognitionEnabled && observer != null && camera != null
                && !PursuitDirector.IsCaptured(observer) && !SceneCurtainTransition.IsActive;
            if (!available || observer != previousObserver) HideImmediately();
            previousObserver = observer;
            if (!available) return;
            var stage = director.CurrentStageRules;
            foreach (var actor in director.Actors) {
                if (actor == null) continue;
                bool active = actor.isActiveAndEnabled && actor.IsActivated && actor.IsRendered;
                bool inRange = active && PursuitEyeTransition.InRange(actor.transform.position, observer.transform.position, stage);
                // Visual encounters can interrupt return outside the patrol leash.
                // Keep the full local awareness radius visible behind the actor too.
                inRange &= actor.Room == null || actor.Room.RoamAcrossRooms || actor.Room.Contains(observer.transform.position);
                if (!views.TryGetValue(actor, out var view)) { if (!inRange) continue; view = CreateView(actor); }
                if (!active) { view.Transition.Reset(); view.Renderer.enabled = false; continue; }
                var target = actor.Victim != null ? actor.Victim : actor.ChasedPlayer;
                var state = PursuitEyeTransition.Resolve(actor.Behaviour, target == observer, target != null && target != observer);
                view.Transition.Tick(inRange, state, settings, Time.unscaledDeltaTime);
                var transition = view.Transition;
                view.Renderer.enabled = transition.Alpha > 0;
                if (!view.Renderer.enabled) continue;
                VisibleCount++;
                view.Root.position = view.Head != null ? view.Head.position + Vector3.up * settings.HeadOffset
                    : actor.transform.position + Vector3.up * (view.FallbackHeight + settings.HeadOffset * .5f);
                view.Root.rotation = camera.transform.rotation;
                // White artwork spans 192 of the 256 source pixels.
                float width = Mathf.Max(.02f, settings.Width) / .75f;
                var parentScale = transform.lossyScale;
                view.Root.localScale = new Vector3(width / Mathf.Max(.001f, Mathf.Abs(parentScale.x)), width / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1);
                view.Renderer.sprite = frames[Mathf.Clamp(Mathf.FloorToInt(transition.Openness * frames.Length), 0, frames.Length - 1)];
                var tint = transition.Color; tint.a *= transition.Alpha;
                properties.Clear(); properties.SetColor(TintId, tint); properties.SetFloat(EmissionId, transition.Emission);
                properties.SetFloat(GlowId, settings.GlowOpacity); view.Renderer.SetPropertyBlock(properties);
            }
            removed.Clear();
            foreach (var pair in views) if (pair.Key == null) { Destroy(pair.Value.Root.gameObject); removed.Add(pair.Key); }
            foreach (var actor in removed) views.Remove(actor);
        }

        void HideImmediately()
        {
            foreach (var view in views.Values) { view.Transition.Reset(); if (view.Renderer != null) view.Renderer.enabled = false; }
            VisibleCount = 0;
        }
        void OnDisable()
        {
            foreach (var view in views.Values) if (view.Root != null) Destroy(view.Root.gameObject);
            views.Clear(); previousObserver = null; VisibleCount = 0;
            if (material != null) Destroy(material);
        }
    }
}
