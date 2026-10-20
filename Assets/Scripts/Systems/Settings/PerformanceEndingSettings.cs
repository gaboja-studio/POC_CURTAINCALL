using UnityEngine;

namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName = "Curtain Call/Settings/Performance ending")]
    public sealed class PerformanceEndingSettings : ScriptableObject
    {
        public const string ResourcePath = "Settings/GameSettings/Tricks/Rewards/performance-ending";
        public static PerformanceEndingSettings Current => Resources.Load<PerformanceEndingSettings>(ResourcePath);
        [Min(.1f)] public float deathNoticeSeconds = 2;
        [Min(0)] public float resultDelaySeconds = 1.5f;
        [Min(1)] public float resultDisplaySeconds = 4;
        [Min(.1f)] public float blackLeadSeconds = .3f;
        [Min(.1f)] public float loadedHoldSeconds = .5f;
        [Min(10)] public float timeoutSeconds = 45;
        public float ResultAt => Mathf.Max(0, resultDelaySeconds);
        public float BlackAt => ResultAt + Mathf.Max(1, resultDisplaySeconds);
        public float TransitionAt => BlackAt + Mathf.Max(.1f, blackLeadSeconds);
        public static float HoldFor(string trick) => (trick == "Tightrope" || trick == "HumanCannon")
            ? (Current != null ? Current.TransitionAt : 5.8f) : 0;
    }
}
