using System;
using UnityEngine;

namespace CurtainCall.PerformanceBackground
{
    // This visual-only assembly must not depend on Assembly-CSharp (game settings or NetworkPlayer).
    public struct PerformanceLightingProfile
    {
        public bool Enabled;
        public float AmbientScale, ReflectionScale, MainLightScale;
        public float SceneryColorScale, SceneryEmissionScale, LampEmissionScale;
        public float ReadabilityIntensity, ReadabilityRange;
        public Vector3 ReadabilityOffset;
    }
    public static class PerformanceLightingConfiguration
    {
        public static Func<PerformanceLightingProfile> ReadSettings;
        public static Func<Vector3?> ReadPosition;
    }
}
