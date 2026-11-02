using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Reads the local player's existing replicated limb state. Never applies damage or changes gameplay.</summary>
    [DefaultExecutionOrder(2000)]
    public sealed class EscapeBodyStatusHud : MonoBehaviour
    {
        [SerializeField] CanvasGroup visibility;
        [SerializeField] HumanBodyDiagramGraphic diagram;
        BodyPart shown = (BodyPart)(-1);
        public BodyPart DisplayedParts => shown == (BodyPart)(-1) ? BodyPart.None : shown & PlayerCondition.AllLimbs;
        public bool IsVisible => visibility != null && visibility.alpha > .5f;

        void Awake() => Hide();
        void LateUpdate()
        {
            var player = NetworkPlayer.Local;
            var content = EscapeCampaignContent.Current;
            bool visible = content != null && content.isActiveAndEnabled && content.IsActive
                && content.gameObject.scene == SceneManager.GetActiveScene() && player != null && player.IsSpawned;
            if (!visible) { Hide(); return; }
            visibility.alpha = 1;
            visibility.blocksRaycasts = visibility.interactable = false;
            Present(player.LostParts);
        }
        public void Present(BodyPart parts)
        {
            parts &= PlayerCondition.AllLimbs;
            if (shown == parts) return;
            shown = parts;
            if (diagram != null) diagram.Present(parts);
        }
        void Hide() { if (visibility != null) { visibility.alpha = 0; visibility.blocksRaycasts = visibility.interactable = false; } }
        void OnDisable() => Hide();
    }
}
