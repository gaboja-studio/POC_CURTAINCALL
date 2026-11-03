using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeExitSaw
    {
        public static bool CrewSlotEnabled(int slot, int connectedPlayers, bool occupied) =>
            slot >= 0 && slot < 4 && (occupied || slot < Mathf.Clamp(connectedPlayers, 1, 4));

        bool CrewAvailable => !scaleWithCrew || CrewSlotEnabled(deviceId,
            session != null ? session.PlayerCount : 1, Carrier != CannonSeatState.Nobody);

        void RefreshCrewAvailability(int connectedPlayers)
        {
            if (!scaleWithCrew || carryRoot == null || carryRoot == transform) return;
            bool show = CrewSlotEnabled(deviceId, connectedPlayers, Carrier != CannonSeatState.Nobody);
            if (carryRoot.gameObject.activeSelf != show) carryRoot.gameObject.SetActive(show);
            // Controller stays subscribed: hidden spare launchers still receive shared cable cuts and restart state.
        }
    }
}
