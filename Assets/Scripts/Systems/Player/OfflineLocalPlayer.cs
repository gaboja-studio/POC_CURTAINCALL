using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 접속하지 않고 씬을 바로 확인할 때의 내 캐릭터(입력 리더). 여러 스크립트가 매 프레임 씬 전체를 뒤져 찾던 것을 한 번 찾아 함께 쓴다(2026-11-14 최적화).
    /// 없으면 0.5초마다 다시 찾는다.
    /// </summary>
    public static class OfflineLocalPlayer
    {
        static PlayerInputReader cached;
        static float nextSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cached = null; nextSearch = 0f; }

        public static PlayerInputReader Reader
        {
            get
            {
                if (cached != null && cached.isActiveAndEnabled) return cached;
                if (Time.unscaledTime < nextSearch) return null;
                nextSearch = Time.unscaledTime + .5f;
                return cached = Object.FindFirstObjectByType<PlayerInputReader>();
            }
        }
    }
}
