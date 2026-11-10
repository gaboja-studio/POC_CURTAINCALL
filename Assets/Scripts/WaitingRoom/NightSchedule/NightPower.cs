using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    // Scene-owned circuit: preserve original light states, including deliberately unlit lamps.
    public sealed class NightPower : MonoBehaviour
    {
        [SerializeField] Light[] circuit;
        bool[] original;
        bool cut;
        public bool IsCut => cut;
        void Awake()
        {
            original = new bool[circuit.Length];
            for (int i = 0; i < circuit.Length; i++) original[i] = circuit[i] != null && circuit[i].enabled;
        }
        void LateUpdate()
        {
            var state = CampaignRun.Instance != null ? CampaignRun.Instance.State : default;
            cut = CurtainCall.Settings.GameSettings.Base.Run.IsExplorationLocked(state) || state.Phase == CampaignPhase.Dawn;
            for (int i = 0; i < circuit.Length; i++) if (circuit[i] != null) circuit[i].enabled = !cut && original[i];
        }
        void OnDisable()
        {
            if (original == null) return;
            for (int i = 0; i < circuit.Length; i++) if (circuit[i] != null) circuit[i].enabled = original[i];
            cut = false;
        }
    }
}
