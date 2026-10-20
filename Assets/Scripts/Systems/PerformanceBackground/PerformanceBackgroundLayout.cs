using UnityEngine;

namespace CurtainCall.PerformanceBackground
{
    public static class PerformanceBackgroundLayout
    {
        public const string ScenePath = "Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity";
        public const string SceneName = "performance_background";
        public const float Scale = 1f / 3f;

        public static bool TryGetPosition(string sceneName, out Vector3 position)
        {
            switch (sceneName)
            {
                case "HumanCannon":
                    position = new Vector3(0f, 0f, 54.458333f);
                    return true;
                case "Tightrope":
                case "proto_0.0.2":
                    position = new Vector3(0f, 0f, 55.833333f);
                    return true;
                default:
                    position = default;
                    return false;
            }
        }
    }
}
