using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    // Scene-owned circuit: preserve original light states, including deliberately unlit lamps.
    public sealed class NightPower : MonoBehaviour
    {
        [SerializeField] Light[] circuit;
        [Tooltip("이 배선이 잘리는 순간 회로 전체가 한 번 꺼졌다가 다시 켜집니다.")]
        [SerializeField] EscapeCuttableCable[] blackoutCables = System.Array.Empty<EscapeCuttableCable>();
        [SerializeField, Min(0)] float blackoutSeconds = 3f;
        bool[] original, wasCut;
        bool cut;
        float blackoutUntil;
        public bool IsCut => cut;
        void Awake()
        {
            original = new bool[circuit.Length];
            for (int i = 0; i < circuit.Length; i++) original[i] = circuit[i] != null && circuit[i].enabled;
            wasCut = new bool[blackoutCables.Length];
        }
        void LateUpdate()
        {
            var state = CampaignRun.Instance != null ? CampaignRun.Instance.State : default;
            cut = CurtainCall.Settings.GameSettings.Base.Run.IsExplorationLocked(state) || state.Phase == CampaignPhase.Dawn;
            // 배선 상태는 모든 참가자에게 동기화되므로 각자 화면에서 같은 순간에 꺼진다.
            for (int i = 0; i < wasCut.Length; i++)
            {
                bool now = blackoutCables[i] != null && blackoutCables[i].IsCut;
                if (now && !wasCut[i]) blackoutUntil = Time.time + blackoutSeconds;
                wasCut[i] = now;
            }
            bool dark = cut || Time.time < blackoutUntil;
            for (int i = 0; i < circuit.Length; i++) if (circuit[i] != null) circuit[i].enabled = !dark && original[i];
        }
        void OnDisable()
        {
            if (original == null) return;
            for (int i = 0; i < circuit.Length; i++) if (circuit[i] != null) circuit[i].enabled = original[i];
            cut = false; blackoutUntil = 0;
        }
    }
}
