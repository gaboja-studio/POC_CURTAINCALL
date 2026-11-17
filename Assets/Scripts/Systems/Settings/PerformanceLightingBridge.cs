using CurtainCall.Network.PlayerSync;
using CurtainCall.PerformanceBackground;
using UnityEngine;

namespace CurtainCall.Settings
{
    static class PerformanceLightingBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bind()
        {
            PerformanceLightingConfiguration.ReadSettings = ReadSettings;
            PerformanceLightingConfiguration.ReadPosition = ReadPosition;
        }
        static PerformanceLightingProfile ReadSettings()
        {
            var value = GameSettings.Base.PerformanceLighting;
            return new PerformanceLightingProfile {
                Enabled = value.Enabled, AmbientScale = value.AmbientScale, ReflectionScale = value.ReflectionScale,
                MainLightScale = value.MainLightScale, SceneryColorScale = value.SceneryColorScale,
                SceneryEmissionScale = value.SceneryEmissionScale, LampEmissionScale = value.LampEmissionScale,
                ReadabilityIntensity = value.ReadabilityIntensity, ReadabilityRange = value.ReadabilityRange,
                ReadabilityOffset = value.ReadabilityOffset
            };
        }
        static Vector3? ReadPosition()
        {
            var player = NetworkPlayer.Local;
            return player != null ? player.transform.position : (Vector3?)null;
        }
    }
}
