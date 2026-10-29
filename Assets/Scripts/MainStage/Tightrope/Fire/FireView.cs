using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    /// <summary>
    /// 충돌체 없는 화재 표시. 추격 불은 연결된 파티클 불벽이다.
    /// 구간 발화는 줄 위 불꽃 파티클로, 예고가 시작되면 아주 약한 붉은 불꽃에서 노란 불꽃이 점점 섞이며 발화 순간 최대 크기까지 커진다.
    /// 최대치(발화)에서 줄이 사라지고, 불꽃은 잠시 최대로 타다가 잦아든다.
    /// </summary>
    [RequireComponent(typeof(TightropeFire))]
    public sealed class FireView : MonoBehaviour
    {
        /// <summary>비우면 임시 빨간 상자를 쓴다. 불벽은 너비 1m 기준으로 만들고 X 배율로 코스 폭을 맞춘다.</summary>
        [SerializeField] Transform chaseEffect;
        /// <summary>줄 불꽃 파티클 머티리얼. 비우면 추격 불벽의 파티클 머티리얼을 같이 쓴다.</summary>
        [SerializeField] Material segmentFlameMaterial;
        [Tooltip("발화 후 줄이 사라진 자리에서 불꽃이 최대로 타는 시간(초).")]
        [SerializeField, Min(0f)] float burnHoldSeconds = 2f;

        // 불 온도처럼 약할 때는 붉은 불꽃만, 세질수록 노란 불꽃이 섞인다. 파티클마다 두 색 사이에서 고른다.
        static readonly Color WeakCool = new(0.75f, 0.06f, 0.02f, 0.6f), WeakWarm = new(1f, 0.16f, 0.03f, 0.7f);
        static readonly Color FullCool = new(1f, 0.22f, 0.03f, 1f), FullHot = new(1f, 0.85f, 0.25f, 1f);

        TightropeFire fire;
        TightropeCourse course;
        Transform chase;
        readonly List<ParticleSystem[]> segments = new();
        readonly List<Vector2> segmentSpans = new(FireCourseSpans.MaximumPieces);
        Material flame, particleMaterial;
        bool ownsParticleMaterial;
        FireSettings shown;
        int shownRound = -1;
        float[] burnVisibleUntil = System.Array.Empty<float>();

        /// <summary>구간 불꽃 세기(0~1). 예고 중 0→1로 커지고, 발화 후 유지 시간 동안 1이다. 표시하지 않으면 -1.</summary>
        public float SegmentIntensity(int index)
        {
            if (fire == null || fire.Settings == null || index < 0 || index >= burnVisibleUntil.Length) return -1f;
            if (fire.IsSegmentActive(index)) return burnVisibleUntil[index] < 0f || Time.time < burnVisibleUntil[index] ? 1f : -1f;
            float remaining = fire.WarningRemaining(index);
            if (remaining < 0f) return -1f;
            float total = fire.Settings.warningSeconds;
            return total <= 0f ? 1f : Mathf.Clamp01(1f - remaining / total);
        }

        void Awake()
        {
            // 화재 판정은 TightropeFire가 계속 처리하며, 숨김 봇은 표시만 생략한다.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { enabled = false; return; }
            fire = GetComponent<TightropeFire>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            flame = new Material(shader) { color = new Color(1f, 0.15f, 0.03f) };
            chase = chaseEffect != null ? chaseEffect : Box("ChaseFire");
            particleMaterial = segmentFlameMaterial;
            if (particleMaterial == null && chaseEffect != null)
            {
                var renderer = chaseEffect.GetComponentInChildren<ParticleSystemRenderer>(true);
                if (renderer != null) particleMaterial = renderer.sharedMaterial;
            }
            if (particleMaterial == null)
            {
                var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                particleMaterial = new Material(particleShader);
                ownsParticleMaterial = true;
            }
        }

        void Update()
        {
            course = TightropeCourse.Current;
            var settings = fire.Settings;
            bool available = settings != null && course != null && fire.IsPerformanceActive;
            if (!available) { Hide(); return; }
            if (shown != settings || shownRound != fire.Round) Rebuild(settings);
            chase.gameObject.SetActive(fire.IsChasing);
            if (fire.IsChasing)
            {
                float width = (course.LaneCount - 1) * course.LaneSpacing + course.Shape.SideMargin * 2f;
                bool effect = chaseEffect != null;
                chase.position = course.transform.position + course.Forward * fire.ChaseDistance + Vector3.up * (effect ? 0f : 0.8f);
                chase.rotation = Quaternion.LookRotation(course.Forward);
                chase.localScale = effect ? new Vector3(width, 1f, 1f) : new Vector3(width, 1.6f, 0.25f);
            }
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = settings.segments[i];
                int lane = segment.lane - 1;
                if (fire.IsSegmentActive(i) && burnVisibleUntil[i] < 0f) burnVisibleUntil[i] = Time.time + burnHoldSeconds;
                float intensity = SegmentIntensity(i);
                bool visible = lane >= 0 && lane < course.LaneCount && segment.to > segment.from && intensity >= 0f;
                fire.GetSegmentSpans(i, segmentSpans);
                for (int piece = 0; piece < segments[i].Length; piece++)
                {
                    var view = segments[i][piece];
                    bool showPiece = visible && piece < segmentSpans.Count;
                    if (!showPiece) { Extinguish(view); continue; }
                    var span = segmentSpans[piece];
                    view.gameObject.SetActive(true);
                    view.transform.SetPositionAndRotation(
                        course.GetLanePosition(lane, (span.x + span.y) * 0.5f) + Vector3.up * 0.02f,
                        Quaternion.LookRotation(course.Forward));
                    Ignite(view, intensity, span.y - span.x);
                }
            }
        }

        /// <summary>세기에 따라 불꽃 양·크기·높이를 키우고, 색은 붉은 불(약함)에 노란 불꽃이 점점 섞이게(최대) 바꾼다.</summary>
        void Ignite(ParticleSystem particles, float intensity, float length)
        {
            float t = intensity * intensity; // 처음에는 아주 약하게, 발화 직전에 빠르게 커진다.
            var main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(WeakCool, FullCool, intensity), Color.Lerp(WeakWarm, FullHot, intensity));
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.08f, 0.55f, t), Mathf.Lerp(0.16f, 1.1f, t));
            main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.25f, 0.55f, t), Mathf.Lerp(0.4f, 0.95f, t));
            var velocity = particles.velocityOverLifetime;
            float rise = Mathf.Lerp(0.3f, 2.4f, t);
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.6f, rise);
            var shape = particles.shape;
            shape.scale = new Vector3(course.Shape.RopeWalkWidth * Mathf.Lerp(0.6f, 1.6f, t), 0.05f, Mathf.Max(0.1f, length));
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0.1f, length) * Mathf.Lerp(3f, 45f, t);
            if (!particles.isPlaying) particles.Play();
        }

        /// <summary>새 불꽃만 멈추고 남은 불꽃은 자연스럽게 사라진 뒤 숨긴다.</summary>
        static void Extinguish(ParticleSystem particles)
        {
            if (!particles.gameObject.activeSelf) return;
            if (particles.isEmitting) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (particles.particleCount == 0) particles.gameObject.SetActive(false);
        }

        void Rebuild(FireSettings settings)
        {
            foreach (var pieces in segments) foreach (var piece in pieces) Destroy(piece.gameObject);
            segments.Clear();
            for (int i = 0; i < settings.segments.Length; i++)
            {
                var pieces = new ParticleSystem[FireCourseSpans.MaximumPieces];
                for (int piece = 0; piece < pieces.Length; piece++) pieces[piece] = Flames($"SegmentFire{i + 1}Part{piece + 1}");
                segments.Add(pieces);
            }
            burnVisibleUntil = new float[settings.segments.Length];
            System.Array.Fill(burnVisibleUntil, -1f);
            shown = settings;
            shownRound = fire.Round;
        }

        ParticleSystem Flames(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 600;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.1f)));

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0.6f), new GradientColorKey(new Color(0.5f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 0.8f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        Transform Box(string label)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label;
            box.transform.SetParent(transform, false);
            var collider = box.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            box.GetComponent<Renderer>().sharedMaterial = flame;
            box.SetActive(false);
            return box.transform;
        }

        void Hide()
        {
            if (chase != null) chase.gameObject.SetActive(false);
            foreach (var pieces in segments) foreach (var piece in pieces) piece.gameObject.SetActive(false);
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (flame != null) Destroy(flame);
            if (ownsParticleMaterial && particleMaterial != null) Destroy(particleMaterial);
        }
    }
}
