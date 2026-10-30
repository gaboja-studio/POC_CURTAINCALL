using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>A의 봉 입력은 외줄 끝이나 주변 동료의 목마 후보에 가로막히지 않는다.</summary>
    [DefaultExecutionOrder(-65)]
    public sealed class EscapeIndependentInteractions : MonoBehaviour
    {
        void Update()
        {
            var session = NetworkSessionManager.Instance;
            var poles = HumanCannonPoleSystem.Current;
            var player = NetworkPlayer.Local;
            if (InputGate.IsBlocked || session == null || session.GameState != GameSessionState.Playing
                || poles == null || !poles.IndependentInteraction || player == null || !player.IsSpawned
                || player.State != PlayerState.Normal || !player.TryGetComponent(out PlayerInputReader input)
                || !input.isActiveAndEnabled || !input.Current.InteractTapped) return;
            if (player.TryGetComponent(out HumanCannonPilot pilot) && pilot.TryRequestNearbyPole())
                input.ClearPendingInput();
        }
    }
}
