using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>UI 화면 문서 4-1 임시 규칙. 거리 기준은 플레이 테스트 후 이 에셋에서 조정한다.</summary>
    [CreateAssetMenu(menuName = "Curtain Call/Settings/Audience reactions")]
    public sealed class AudienceReactionSettings : ScriptableObject
    {
        public const string ResourcePath = "Settings/GameSettings/Tricks/Rewards/temporary-audience";
        public static AudienceReactionSettings Current => Resources.Load<AudienceReactionSettings>(ResourcePath);
        [Min(1)] public int cap = 100;
        [Min(0)] public int closeDodge = 15, safeDodge = 5, cut = 30;
        [Min(0)] public int safeArrival = 15, closeArrival = 30;
        [Min(0)] public int stackedVertical = 50, laneChangeJeer = 30;
        [Min(0)] public int hangingJeer = 15, trampoline = 15, lowerFloorJeer = 15;
        [Min(.01f)] public float hangingSeconds = 3, lowerFloorSeconds = 10;
        [Tooltip("실제 장애물 판정 캡슐에 더하는 아슬아슬 회피 여유(m)"), Min(.01f)] public float closeDodgeMargin = .3f;
        [Tooltip("장애물 앞 접근 관찰 거리(m)"), Min(.1f)] public float approachDistance = 8;
        [Tooltip("장애물을 지나 통과를 확정하는 거리(m)"), Min(.1f)] public float clearanceDistance = 2;
        [Tooltip("도착 시 불의 실제 전방과 단원 사이 거리(m)"), Min(0)] public float closeFireDistance = 3;
    }
}
