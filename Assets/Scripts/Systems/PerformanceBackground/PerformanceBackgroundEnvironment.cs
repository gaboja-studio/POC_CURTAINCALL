using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.PerformanceBackground
{
    // 배경 원본의 환경값을 런타임에만 적용한다. 코스 씬의 저장된 설정은 바꾸지 않는다.
    sealed class PerformanceBackgroundEnvironment
    {
        readonly Dictionary<Camera, (CameraClearFlags flags, Color color)> cameras = new();
        Scene scene;
        bool applied;
        Material skybox;
        AmbientMode ambientMode;
        Color sky, equator, ground, fogColor;
        float ambientIntensity, fogDensity;
        bool fog;
        FogMode fogMode;

        public void Update(bool backgroundLoaded)
        {
            var active = SceneManager.GetActiveScene();
            bool needed = backgroundLoaded && PerformanceBackgroundLayout.TryGetPosition(active.name, out _);
            if (applied && (!needed || active != scene)) Restore();
            if (!needed) return;
            if (!applied)
            {
                scene = active;
                skybox = RenderSettings.skybox;
                ambientMode = RenderSettings.ambientMode;
                sky = RenderSettings.ambientSkyColor;
                equator = RenderSettings.ambientEquatorColor;
                ground = RenderSettings.ambientGroundColor;
                ambientIntensity = RenderSettings.ambientIntensity;
                fog = RenderSettings.fog;
                fogMode = RenderSettings.fogMode;
                fogDensity = RenderSettings.fogDensity;
                fogColor = RenderSettings.fogColor;
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.13f, 0.19f, 0.30f);
                RenderSettings.ambientEquatorColor = new Color(0.10f, 0.115f, 0.16f);
                RenderSettings.ambientGroundColor = new Color(0.30f, 0.11f, 0.035f);
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.00115f / PerformanceBackgroundLayout.Scale;
                RenderSettings.fogColor = new Color(0.028f, 0.034f, 0.055f);
                applied = true;
            }
            var camera = Camera.main;
            if (camera == null || cameras.ContainsKey(camera)) return;
            cameras.Add(camera, (camera.clearFlags, camera.backgroundColor));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
        }

        public void Restore()
        {
            if (applied && SceneManager.GetActiveScene() == scene)
            {
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogColor = fogColor;
            }
            foreach (var pair in cameras)
            {
                if (pair.Key == null) continue;
                pair.Key.clearFlags = pair.Value.flags;
                pair.Key.backgroundColor = pair.Value.color;
            }
            cameras.Clear();
            applied = false;
        }
    }
}
