using UnityEngine;

namespace CurtainCall.Settings
{
    [CreateAssetMenu(menuName = "Curtain Call/Settings/Performance ending")]
    public sealed class PerformanceEndingSettings : ScriptableObject
    {
        public const string ResourcePath = "Settings/GameSettings/Tricks/Rewards/performance-ending";
        static PerformanceEndingSettings cached;
        // 동기 로드는 시작 때 한 번만 한다. 공연 배경 비동기 로드 중 첫 호출이면 메인 스레드가 수 초 멈춘다(Task-20261105-108).
        public static PerformanceEndingSettings Current => cached != null ? cached : cached = Resources.Load<PerformanceEndingSettings>(ResourcePath);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Preload() => _ = Current;
        [Tooltip("공연 중 내가 사망했을 때 오른쪽 위 사망 경고(호응도 절반 안내)를 보여 주는 시간(초).")]
        [Min(.1f)] public float deathNoticeSeconds = 2;
        [Tooltip("공연 정산이 확정된 뒤 결과창이 뜨기까지 기다리는 시간(초). 그동안 마지막 장면을 보여 준다.")]
        [Min(0)] public float resultDelaySeconds = 1.5f;
        [Tooltip("결과창을 보여 주는 시간(초). 결과창 하단의 '다음 공간으로 이동 n초' 카운트다운도 이 값을 따른다.")]
        [Min(1)] public float resultDisplaySeconds = 4;
        [Tooltip("결과창을 닫고 막이 내려간 뒤 다음 시간대(대기실 등)로 넘어가기까지의 시간(초).")]
        [Min(.1f)] public float blackLeadSeconds = .3f;
        [Tooltip("다음 공간을 다 불러온 뒤 막을 걷기 전까지 기다리는 시간(초). 배치가 끝나기 전 화면이 보이지 않게 한다.")]
        [Min(.1f)] public float loadedHoldSeconds = .5f;
        [Tooltip("정산 뒤 이 시간(초) 안에 다음 공간으로 넘어가지 못하면 화면을 원래대로 돌리고 연결 확인 안내를 띄운다.")]
        [Min(10)] public float timeoutSeconds = 45;
        public float ResultAt => Mathf.Max(0, resultDelaySeconds);
        public float BlackAt => ResultAt + Mathf.Max(1, resultDisplaySeconds);
        public float TransitionAt => BlackAt + Mathf.Max(.1f, blackLeadSeconds);
        public static float HoldFor(string trick) => (trick == "Tightrope" || trick == "HumanCannon")
            ? (Current != null ? Current.TransitionAt : 5.8f) : 0;
    }
}
