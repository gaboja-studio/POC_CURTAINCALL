using CurtainCall.CheerGauge;
using CurtainCall.Reward;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>
    /// 팀 호응 게이지가 한 번에 크게 오르면(불 고리 통과·구조 성공 등) 화면 양옆 아래에서 불꽃을 쏜다(2026-11-26 M3).
    /// 각 화면이 공유 보상 상태(게이지와 같은 값)를 보고 스스로 연출하므로 네트워크 메시지를 늘리지 않는다.
    /// 위치는 이 화면 카메라 기준이라 공연 종류(외줄·인간 대포)와 무관하게 '공연장 옆 아래'에 보인다.
    /// </summary>
    public sealed class PerformanceFireworks : MonoBehaviour
    {
        readonly C1GaugeProjection projection = new();
        PerformanceRewards source;
        string identity;
        long lastTotal = -1;
        Material fallbackMaterial;

        void Update()
        {
            if (!ReferenceEquals(source, PerformanceRewards.Instance)) Bind();
        }

        void OnDisable()
        {
            if (!ReferenceEquals(source, null)) source.Changed -= OnChanged;
            source = null;
            projection.Reset();
            identity = null;
            lastTotal = -1;
        }

        void Bind()
        {
            if (!ReferenceEquals(source, null)) source.Changed -= OnChanged;
            source = PerformanceRewards.Instance;
            projection.Reset();
            identity = null;
            lastTotal = -1;
            if (source != null) { source.Changed += OnChanged; Observe(false); }
        }

        void OnChanged() => Observe(true);

        void Observe(bool react)
        {
            if (source == null || !projection.TryProject(source.State, out var frame)) { identity = null; lastTotal = -1; return; }
            bool same = identity == frame.Identity && lastTotal >= 0;
            long gain = same ? frame.Total - lastTotal : 0;
            identity = frame.Identity;
            lastTotal = frame.Total;
            var settings = GameSettings.PerformanceEquipment;
            if (react && settings.bigCheerFireworks && gain >= settings.bigCheerThreshold) Launch();
        }

        /// <summary>화면 양옆 아래에서 위로 불꽃을 쏜다. 시험·연출 확인용으로도 부른다.</summary>
        public void Launch()
        {
            var view = Camera.main;
            if (view == null) return;
            var settings = GameSettings.PerformanceEquipment;
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 center = view.transform.position + forward * settings.fireworkAhead + Vector3.down * settings.fireworkBelow;
            int count = Mathf.Clamp(settings.fireworkPerSide, 1, 6);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < count; i++)
                {
                    Vector3 origin = center + right * side * (settings.fireworkSide + i * 2.2f) + forward * (i * 3f - count * 1.5f);
                    SpawnRocket(origin, i * .18f + (side > 0 ? .09f : 0f), i % 2 == 0 ? settings.fireworkColorA : settings.fireworkColorB, settings);
                }
        }

        Material Material
        {
            get
            {
                var configured = GameSettings.PerformanceEquipment.fireworkMaterial;
                if (configured != null) return configured;
                if (fallbackMaterial != null) return fallbackMaterial;
                // 설정이 비어 있으면 장면의 불티 파티클 재질을 빌려 쓴다.
                foreach (var renderer in FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.name.IndexOf("Spark", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return fallbackMaterial = renderer.sharedMaterial;
                return null;
            }
        }

        void SpawnRocket(Vector3 origin, float delay, Color color, PerformanceEquipmentSettings settings)
        {
            var material = Material;
            if (material == null) return;
            var root = new GameObject("BigCheerFirework");
            root.transform.position = origin;
            float rise = Mathf.Max(2f, settings.fireworkRise);
            const float flight = .9f;

            // 상승: 빛나는 꼬리를 남기며 올라가는 한 발.
            var rocket = root.AddComponent<ParticleSystem>();
            rocket.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rocket.main;
            main.duration = 1f; main.loop = false; main.startDelay = delay;
            main.startLifetime = flight; main.startSpeed = rise / flight * 1.15f; main.startSize = .3f;
            main.startColor = color * 1.2f; main.gravityModifier = .25f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = rocket.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = rocket.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 4f; shape.radius = .05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var trails = rocket.trails; trails.enabled = true; trails.lifetime = .3f; trails.widthOverTrail = .3f;
            trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(color);
            var rocketRenderer = root.GetComponent<ParticleSystemRenderer>();
            rocketRenderer.sharedMaterial = material; rocketRenderer.trailMaterial = material;

            // 터짐: 상승이 끝나는 순간 공 모양으로 퍼지는 불티.
            var burstObject = new GameObject("Burst");
            burstObject.transform.SetParent(root.transform, false);
            var burst = burstObject.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var burstMain = burst.main;
            burstMain.duration = 1f; burstMain.loop = false; burstMain.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 1.5f);
            burstMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f); burstMain.startSize = new ParticleSystem.MinMaxCurve(.15f, .3f);
            burstMain.startColor = new ParticleSystem.MinMaxGradient(color * 1.2f, Color.Lerp(color, Color.white, .2f));
            burstMain.gravityModifier = .6f; burstMain.simulationSpace = ParticleSystemSimulationSpace.World; burstMain.maxParticles = 200;
            var burstEmission = burst.emission; burstEmission.rateOverTime = 0f;
            burstEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });
            var burstShape = burst.shape; burstShape.shapeType = ParticleSystemShapeType.Sphere; burstShape.radius = .2f;
            var fade = burst.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .55f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var burstTrails = burst.trails; burstTrails.enabled = true; burstTrails.lifetime = .08f; burstTrails.widthOverTrail = .2f;
            burstTrails.inheritParticleColor = true;
            var burstRenderer = burstObject.GetComponent<ParticleSystemRenderer>();
            burstRenderer.sharedMaterial = material; burstRenderer.trailMaterial = material;
            // 상승이 끝나는 높이에서 터지게 둔다(시뮬레이션 공간이 월드라 위치만 옮기면 된다).
            burstObject.transform.position = origin + Vector3.up * rise;
            var burstMainDelay = burst.main; burstMainDelay.startDelay = delay + flight * .95f;

            rocket.Play(true);
            Destroy(root, delay + flight + 2.5f);
        }

        void OnDestroy() { }
    }
}
