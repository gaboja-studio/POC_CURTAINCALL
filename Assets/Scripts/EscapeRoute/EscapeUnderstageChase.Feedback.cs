using Unity.Netcode;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        /// <summary>Read-only gate for both targeting and proximity screen feedback.</summary>
        public bool CanShowThreatFeedback
        {
            get
            {
                var timer = EscapeRouteTimer.Current;
                var net = NetworkManager.Singleton;
                return isActiveAndEnabled && !InShowcase && EscapeCampaignContent.IsTraversalActive
                    && timer && timer.isActiveAndEnabled && timer.HasStarted && net && net.IsListening
                    && (!boss || !boss.Defeated) && (net.IsServer || CurrentSnapshot(timer));
            }
        }
    }
}
